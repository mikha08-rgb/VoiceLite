using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VoiceLite.Models;
using VoiceLite.Utilities;

namespace VoiceLite.Services
{
    /// <summary>What the controller needs from the audio recorder. AudioRecorder implements it as-is.</summary>
    public interface IDictationRecorder
    {
        bool IsRecording { get; }
        string? ActiveDeviceName { get; }
        double LastPeakPercent { get; }
        event EventHandler<string>? AudioFileReady;
        event EventHandler<Exception>? RecordingError;
        void StartRecording();
        void StopRecording();
    }

    /// <summary>What the controller needs from the transcriber. TranscriptionService implements it as-is.</summary>
    public interface IDictationTranscriber
    {
        bool EffectiveTranslateToEnglish { get; }
        long BeginDictationSession();
        void ExpireDictationSessions();
        bool IsDictationSessionCurrent(long sessionId);
        Task<string> TranscribeAsync(string audioFilePath);
    }

    public enum DictationState { Idle, Recording, Transcribing }

    public enum DictationEventKind
    {
        RecordingStarted,
        RecordingStopped,       // audio handed to the transcriber
        RecordingCancelled,
        RecordingFailed,        // Message says what failed
        RecordingTooShort,
        Busy,                   // start refused: a transcription is still running
        TranscriptionCompleted, // Text is non-empty
        NoSpeech,               // MicName / MicSilent describe the device
        TranscriptionFailed,    // Message is user-facing
        AutoStopped,            // toggle recording hit the hot-mic limit (RecordingStopped already sent)
        StuckRecovered,         // transcription watchdog fired; state was force-reset
        Fault                   // unexpected exception in a hotkey path; Message for the user
    }

    public sealed class DictationEvent
    {
        public DictationEventKind Kind { get; init; }
        public string? Text { get; init; }
        public string? Message { get; init; }
        public string? MicName { get; init; }
        public bool MicSilent { get; init; }
    }

    /// <summary>
    /// The dictation state machine: hotkey → recording → transcription → result.
    /// Owns every flag and timer that used to live in MainWindow. Raises <see cref="Notice"/>
    /// for everything the UI needs to show; it never touches UI itself.
    ///
    /// Threading: commands (HotkeyPressed/Released, Cancel) may be called from any
    /// thread and are serialized by an internal lock. Timer callbacks are routed through the
    /// <c>invoke</c> delegate given at construction (MainWindow passes the dispatcher) so the
    /// recorder is always driven from the same thread the app always used. Notice handlers
    /// may run while the lock is held, so they must not call back into the controller
    /// synchronously; marshal to the UI thread first.
    /// </summary>
    public sealed class DictationController : IDisposable
    {
        public static readonly TimeSpan DefaultAutoStopAfter = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan DefaultStuckAfter = TimeSpan.FromSeconds(120);

        private readonly IDictationRecorder recorder;
        private readonly IDictationTranscriber transcriber;
        private readonly Func<Settings> settings;
        private readonly Action<Action> invoke;
        private readonly TimeSpan autoStopAfter;
        private readonly TimeSpan stuckAfter;

        private readonly object gate = new();
        private bool isRecording;
        private bool isTranscribing;
        private bool isHotkeyMode;          // push-to-talk: the held key started this recording
        private volatile bool discardNextAudio;
        private volatile bool stopOutcomeSeen; // the recorder answered StopRecording with audio or an error
        private long activeSessionId;
        private DateTime lastHotkeyPressUtc = DateTime.MinValue;
        private Timer? autoStopTimer;
        private Timer? stuckTimer;
        private bool disposed;

        public event EventHandler<DictationEvent>? Notice;

        public DateTime RecordingStartedAt { get; private set; }

        /// <summary>The toggle-mode hot-mic limit in use. Lock-free: readonly field, safe from Notice handlers.</summary>
        public TimeSpan AutoStopAfter => autoStopAfter;

        public bool IsRecording { get { lock (gate) return isRecording; } }
        public bool IsTranscribing { get { lock (gate) return isTranscribing; } }

        public DictationState State
        {
            get
            {
                lock (gate)
                {
                    if (isRecording) return DictationState.Recording;
                    if (isTranscribing) return DictationState.Transcribing;
                    return DictationState.Idle;
                }
            }
        }

        public DictationController(
            IDictationRecorder recorder,
            IDictationTranscriber transcriber,
            Func<Settings> settings,
            Action<Action>? invoke = null,
            TimeSpan? autoStopAfter = null,
            TimeSpan? stuckAfter = null)
        {
            this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            this.transcriber = transcriber ?? throw new ArgumentNullException(nameof(transcriber));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.invoke = invoke ?? (a => a());
            this.autoStopAfter = autoStopAfter ?? DefaultAutoStopAfter;
            this.stuckAfter = stuckAfter ?? DefaultStuckAfter;

            recorder.AudioFileReady += OnAudioFileReady;
            recorder.RecordingError += OnRecordingError;
        }

        private RecordMode Mode => settings().Mode;

        // ---------------------------------------------------------------- commands

        public void HotkeyPressed()
        {
            try
            {
                var now = DateTime.UtcNow;
                if ((now - lastHotkeyPressUtc).TotalMilliseconds < TimingConstants.HotkeyDebounceMs)
                {
                    ErrorLogger.LogWarning("Hotkey pressed: ignored (debounced - pressed again too quickly)");
                    return;
                }
                lastHotkeyPressUtc = now;

                lock (gate)
                {
                    ErrorLogger.LogWarning($"Hotkey pressed: mode={Mode}, recording={isRecording}, hotkeyMode={isHotkeyMode}");
                    if (Mode == RecordMode.PushToTalk)
                    {
                        if (!isRecording)
                        {
                            isHotkeyMode = true;
                            StartLocked();
                        }
                        else
                        {
                            ErrorLogger.LogWarning("Push-to-talk: press ignored - already recording");
                        }
                    }
                    else
                    {
                        if (!isRecording) StartLocked();
                        else StopLocked(cancel: false);
                    }
                }
            }
            catch (Exception ex)
            {
                Fault("OnHotkeyPressed", ex);
            }
        }

        public void HotkeyReleased()
        {
            try
            {
                lock (gate)
                {
                    ErrorLogger.LogWarning($"Hotkey released: mode={Mode}, recording={isRecording}, hotkeyMode={isHotkeyMode}");
                    if (Mode != RecordMode.PushToTalk) return; // toggle: everything happens on press

                    if (isRecording && isHotkeyMode)
                    {
                        StopLocked(cancel: false);
                        isHotkeyMode = false;
                    }
                    else
                    {
                        ErrorLogger.LogWarning($"Push-to-talk: release ignored - recording={isRecording}, hotkeyMode={isHotkeyMode}");
                        if (isHotkeyMode && !recorder.IsRecording) isHotkeyMode = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Fault("OnHotkeyReleased", ex);
            }
        }

        /// <summary>Stop and throw the audio away (Esc, app close).</summary>
        public void Cancel()
        {
            lock (gate)
            {
                if (isRecording) StopLocked(cancel: true);
                isHotkeyMode = false;
            }
        }

        // ---------------------------------------------------------------- transitions (lock held)

        private void StartLocked()
        {
            if (recorder.IsRecording)
            {
                ErrorLogger.LogWarning("StartRecording: recorder is already recording, aborting");
                return;
            }
            if (isRecording) return;
            if (isTranscribing)
            {
                ErrorLogger.LogWarning("StartRecording: blocked - transcription still in progress");
                Raise(DictationEventKind.Busy);
                return;
            }

            StopStuckTimer();
            discardNextAudio = false; // a new recording must never start with a stale discard flag
            RecordingStartedAt = DateTime.Now;
            isRecording = true;
            Raise(DictationEventKind.RecordingStarted); // before the device opens: instant feedback

            try
            {
                recorder.StartRecording();
                if (recorder.IsRecording)
                {
                    if (Mode == RecordMode.Toggle) StartAutoStopTimer();
                }
                else
                {
                    ErrorLogger.LogWarning("StartRecording: recorder did not start, rolling back");
                    isRecording = false;
                    Raise(DictationEventKind.RecordingFailed, message: "Failed to start recording");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("StartRecording", ex);
                isRecording = false;
                Raise(DictationEventKind.RecordingFailed, message: "Failed to start recording");
            }
        }

        /// <returns>false when a non-cancel stop produced no audio and no error (already reported as RecordingFailed).</returns>
        private bool StopLocked(bool cancel)
        {
            if (!isRecording) return true;

            StopAutoStopTimer();
            isRecording = false;
            ErrorLogger.LogWarning(cancel ? "Recording cancelled" : "Recording stopped - transcribing");

            // AudioFileReady fires synchronously from inside recorder.StopRecording(), so the
            // discard flag must be set first, and the Stopped/Cancelled notice goes out before
            // any transcription result can.
            discardNextAudio = cancel;
            Raise(cancel ? DictationEventKind.RecordingCancelled : DictationEventKind.RecordingStopped);

            bool recorderWasRecording = recorder.IsRecording;
            stopOutcomeSeen = false;
            try
            {
                recorder.StopRecording();
            }
            catch (Exception ex)
            {
                stopOutcomeSeen = true;
                ErrorLogger.LogError("StopRecording", ex);
                Raise(DictationEventKind.RecordingFailed, message: "Failed to stop recording");
            }

            // Every real stop ends in AudioFileReady or RecordingError, raised synchronously from
            // inside StopRecording(). Neither means the recorder had already died (write failure,
            // device lost) and nothing else would ever replace "Processing..." in the UI.
            // If the recorder ever raises its outcome asynchronously, this check must move.
            if (!cancel && !stopOutcomeSeen)
            {
                ErrorLogger.LogWarning($"StopRecording produced no audio and no error (recorder.IsRecording was {recorderWasRecording}) - recording lost");
                Raise(DictationEventKind.RecordingFailed, message: "Recording failed - audio was not saved");
                return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- recorder callbacks

        private void OnRecordingError(object? sender, Exception ex)
        {
            stopOutcomeSeen = true;
            ErrorLogger.LogError("Recording lost", ex);

            // A cancelled recording can also trip the too-short path; the user asked to
            // discard it, so don't overwrite "Cancelled" with an error.
            if (discardNextAudio)
            {
                discardNextAudio = false;
                return;
            }

            bool tooShort = ex is InvalidOperationException && ex.Message.StartsWith("Recording too short", StringComparison.Ordinal);
            if (tooShort) Raise(DictationEventKind.RecordingTooShort);
            else Raise(DictationEventKind.RecordingFailed, message: "Recording failed - audio was not saved");
        }

        private async void OnAudioFileReady(object? sender, string audioFilePath)
        {
            stopOutcomeSeen = true; // before the await: StopLocked reads it as soon as StopRecording returns
            try
            {
                await TranscribeFileAsync(audioFilePath);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("OnAudioFileReady: catastrophic failure", ex);
                lock (gate) isTranscribing = false;
                StopStuckTimer();
                Raise(DictationEventKind.TranscriptionFailed, message: "Critical error");
            }
        }

        private async Task TranscribeFileAsync(string audioFilePath)
        {
            if (discardNextAudio)
            {
                discardNextAudio = false;
                ErrorLogger.LogWarning($"Discarding cancelled recording: {audioFilePath}");
                TryDelete(audioFilePath);
                return;
            }

            lock (gate)
            {
                if (isTranscribing)
                {
                    ErrorLogger.LogWarning("AudioFileReady: already transcribing - ignoring duplicate");
                    return;
                }
                isTranscribing = true;
            }
            StartStuckTimer();

            // The native decode cannot be cancelled. Binding this dictation to a session id
            // lets a result that arrives after the watchdog fired (or after a newer dictation
            // began) be discarded instead of typed into whatever window is focused by then.
            long session = 0;
            try
            {
                session = transcriber.BeginDictationSession();
                Interlocked.Exchange(ref activeSessionId, session);

                var text = await transcriber.TranscribeAsync(audioFilePath);
                ErrorLogger.LogWarning($"Transcription returned {text?.Length ?? 0} chars");

                if (!transcriber.IsDictationSessionCurrent(session))
                {
                    ErrorLogger.LogWarning($"Discarding stale transcription result (session {session} expired)");
                    return;
                }

                StopStuckTimer();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    Raise(DictationEventKind.TranscriptionCompleted, text: text);
                }
                else
                {
                    Raise(new DictationEvent
                    {
                        Kind = DictationEventKind.NoSpeech,
                        MicName = recorder.ActiveDeviceName ?? "default",
                        MicSilent = recorder.LastPeakPercent < 1.0,
                    });
                }

                lock (gate)
                {
                    // Push-to-talk with the key still held keeps hotkey mode; everything else resets.
                    if (!recorder.IsRecording && !(Mode == RecordMode.PushToTalk && isHotkeyMode))
                    {
                        isHotkeyMode = false;
                        StopAutoStopTimer();
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Transcription failed", ex);
                if (session != 0 && !transcriber.IsDictationSessionCurrent(session))
                {
                    ErrorLogger.LogWarning($"Suppressing error for expired dictation session {session}");
                    return;
                }
                StopStuckTimer();
                Raise(DictationEventKind.TranscriptionFailed, message: DescribeTranscriptionError(ex));
            }
            finally
            {
                // Only the dictation that still owns the active session may reset shared
                // state; a late finisher must not clear a newer dictation's flags or watchdog.
                if (session == 0 || Interlocked.Read(ref activeSessionId) == session)
                {
                    lock (gate) isTranscribing = false;
                    StopStuckTimer();
                }
                TryDelete(audioFilePath);
            }
        }

        private string DescribeTranscriptionError(Exception ex)
        {
            // Actionable messages stay verbatim: a missing model tells the user to download it,
            // a timeout suggests the Speed preset. Everything else is a generic error.
            bool actionable =
                ex is TimeoutException ||
                (ex is FileNotFoundException or DirectoryNotFoundException or InvalidOperationException
                    && ex.Message.Contains("model", StringComparison.OrdinalIgnoreCase));
            if (actionable) return ex.Message;
            return transcriber.EffectiveTranslateToEnglish ? "Translation error" : "Transcription error";
        }

        // ---------------------------------------------------------------- timers

        private void StartAutoStopTimer()
        {
            StopAutoStopTimer();
            autoStopTimer = new Timer(_ => invoke(OnAutoStop), null, autoStopAfter, Timeout.InfiniteTimeSpan);
        }

        private void StopAutoStopTimer()
        {
            var t = Interlocked.Exchange(ref autoStopTimer, null);
            t?.Dispose();
        }

        private void OnAutoStop()
        {
            if (disposed) return;
            bool stopped = false;
            lock (gate)
            {
                if (isRecording)
                {
                    ErrorLogger.LogWarning($"Toggle recording hit the {autoStopAfter.TotalMinutes:0}-minute hot-mic limit - stopping and transcribing");
                    // A stop that lost the recording already said so; "transcribing what was captured" would contradict it.
                    stopped = StopLocked(cancel: false);
                }
            }
            if (stopped) Raise(DictationEventKind.AutoStopped);
        }

        private void StartStuckTimer()
        {
            StopStuckTimer();
            stuckTimer = new Timer(_ => invoke(OnStuck), null, stuckAfter, Timeout.InfiniteTimeSpan);
        }

        private void StopStuckTimer()
        {
            var t = Interlocked.Exchange(ref stuckTimer, null);
            t?.Dispose();
        }

        private void OnStuck()
        {
            if (disposed) return;
            bool wasTranscribing;
            lock (gate)
            {
                wasTranscribing = isTranscribing;
                if (!wasTranscribing) return;
                ErrorLogger.LogWarning($"Transcription watchdog fired after {stuckAfter.TotalSeconds:0}s - force-resetting state");
                // Expire first so the still-running decode's late result is discarded.
                transcriber.ExpireDictationSessions();
                isTranscribing = false;
                isHotkeyMode = false;
            }
            StopStuckTimer();
            StopAutoStopTimer();
            Raise(DictationEventKind.StuckRecovered);
        }

        // ---------------------------------------------------------------- helpers

        private void Fault(string where, Exception ex)
        {
            ErrorLogger.LogError($"CRITICAL: {where} exception - hotkey system may be broken", ex);
            try { Cancel(); } catch { /* last resort */ }
            Raise(DictationEventKind.Fault, message: $"Hotkey system error: {ex.Message}\n\nPlease restart VoiceLite.");
        }

        private void Raise(DictationEventKind kind, string? text = null, string? message = null)
            => Raise(new DictationEvent { Kind = kind, Text = text, Message = message });

        private void Raise(DictationEvent ev)
        {
            try { Notice?.Invoke(this, ev); }
            catch (Exception ex) { ErrorLogger.LogError($"Dictation notice handler threw ({ev.Kind})", ex); }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { ErrorLogger.LogError($"Failed to delete temp audio file: {path}", ex); }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            recorder.AudioFileReady -= OnAudioFileReady;
            recorder.RecordingError -= OnRecordingError;
            StopAutoStopTimer();
            StopStuckTimer();
        }
    }
}

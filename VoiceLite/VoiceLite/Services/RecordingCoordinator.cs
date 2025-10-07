using System;
using System.IO;
using System.Threading.Tasks;
using VoiceLite.Interfaces;
using VoiceLite.Models;
using VoiceLite.Utilities;

namespace VoiceLite.Services
{
    /// <summary>
    /// Coordinates the recording workflow: audio capture, transcription, text injection, and history.
    /// Keeps business logic separate from UI concerns.
    /// </summary>
    public class RecordingCoordinator : IDisposable
    {
        private readonly AudioRecorder audioRecorder;
        private readonly ITranscriber whisperService;
        private readonly TextInjector textInjector;
        private readonly TranscriptionHistoryService? historyService;
        private readonly AnalyticsService? analyticsService;
        private readonly SoundService? soundService;
        private readonly Settings settings;

        // WEEK1-DAY3: State machine replaces 3 separate bool flags (_isRecording, isCancelled, isTranscribing)
        private readonly RecordingStateMachine stateMachine;

        private DateTime recordingStartTime;
        private readonly object recordingLock = new object();
        private System.Threading.Timer? transcriptionWatchdog;
        private int transcriptionCompletedFlag = 0; // 0 = not completed, 1 = completed (atomic)
        private volatile bool isDisposed = false; // DISPOSAL SAFETY: Prevents use-after-dispose
        private DateTime transcriptionStartTime;
        private const int TRANSCRIPTION_TIMEOUT_SECONDS = 120; // 2 minutes max
        private readonly System.Threading.ManualResetEventSlim transcriptionComplete = new System.Threading.ManualResetEventSlim(true); // WEEK1-DAY2: Signaling for disposal (true = initially set)

        // Events for MainWindow to subscribe to
        public event EventHandler<RecordingStatusEventArgs>? StatusChanged;
        public event EventHandler<TranscriptionCompleteEventArgs>? TranscriptionCompleted;
        public event EventHandler<string>? ErrorOccurred;

        // WEEK1-DAY3: Expose state machine state instead of local bool flags
        public bool IsRecording => stateMachine.CurrentState == RecordingState.Recording;
        public bool IsTranscribing => stateMachine.CurrentState == RecordingState.Transcribing;
        public RecordingState CurrentState => stateMachine.CurrentState;

        public RecordingCoordinator(
            AudioRecorder audioRecorder,
            ITranscriber whisperService,
            TextInjector textInjector,
            TranscriptionHistoryService? historyService,
            AnalyticsService? analyticsService,
            SoundService? soundService,
            Settings settings)
        {
            this.audioRecorder = audioRecorder ?? throw new ArgumentNullException(nameof(audioRecorder));
            this.whisperService = whisperService ?? throw new ArgumentNullException(nameof(whisperService));
            this.textInjector = textInjector ?? throw new ArgumentNullException(nameof(textInjector));
            this.historyService = historyService;
            this.analyticsService = analyticsService;
            this.soundService = soundService;
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // WEEK1-DAY3: Initialize state machine
            this.stateMachine = new RecordingStateMachine();

            // Wire up audio recorder events
            audioRecorder.AudioFileReady += OnAudioFileReady;
        }

        /// <summary>
        /// Start recording audio
        /// </summary>
        public void StartRecording()
        {
            ErrorLogger.LogDebug($"RecordingCoordinator.StartRecording: Entry - state={stateMachine.CurrentState}");

            lock (recordingLock)
            {
                // WEEK1-DAY3: Use state machine instead of bool flag
                if (!stateMachine.TryTransition(RecordingState.Recording))
                {
                    ErrorLogger.LogWarning($"RecordingCoordinator.StartRecording: Cannot transition to Recording from {stateMachine.CurrentState}");
                    return;
                }

                try
                {
                    recordingStartTime = DateTime.Now;
                    audioRecorder.StartRecording();

                    // Notify UI
                    StatusChanged?.Invoke(this, new RecordingStatusEventArgs
                    {
                        Status = "Recording",
                        IsRecording = true,
                        ElapsedSeconds = 0
                    });

                    ErrorLogger.LogDebug("RecordingCoordinator.StartRecording: Recording started successfully");
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("No microphone"))
                {
                    ErrorLogger.LogWarning($"StartRecording failed: No microphone - {ex.Message}");
                    stateMachine.TryTransition(RecordingState.Error);
                    stateMachine.TryTransition(RecordingState.Idle);
                    ErrorOccurred?.Invoke(this, $"No microphone detected!\n\nPlease connect a microphone and restart the application.");
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Failed to access"))
                {
                    ErrorLogger.LogWarning($"StartRecording failed: Microphone busy - {ex.Message}");
                    stateMachine.TryTransition(RecordingState.Error);
                    stateMachine.TryTransition(RecordingState.Idle);
                    ErrorOccurred?.Invoke(this, ex.Message);
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("RecordingCoordinator.StartRecording failed", ex);
                    stateMachine.TryTransition(RecordingState.Error);
                    stateMachine.TryTransition(RecordingState.Idle);
                    ErrorOccurred?.Invoke(this, $"Failed to start recording: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Stop recording audio
        /// </summary>
        /// <param name="cancel">If true, discards recording without transcription</param>
        public void StopRecording(bool cancel = false)
        {
            ErrorLogger.LogDebug($"RecordingCoordinator.StopRecording: Entry - state={stateMachine.CurrentState}, cancel={cancel}");

            lock (recordingLock)
            {
                // WEEK1-DAY3: Use state machine transitions
                RecordingState targetState = cancel ? RecordingState.Cancelled : RecordingState.Stopping;

                if (!stateMachine.TryTransition(targetState))
                {
                    ErrorLogger.LogWarning($"RecordingCoordinator.StopRecording: Cannot transition to {targetState} from {stateMachine.CurrentState}");
                    return;
                }

                try
                {
                    if (cancel)
                    {
                        ErrorLogger.LogInfo("Recording cancelled by user");
                    }

                    audioRecorder.StopRecording();

                    // Notify UI
                    StatusChanged?.Invoke(this, new RecordingStatusEventArgs
                    {
                        Status = cancel ? "Cancelled" : "Processing",
                        IsRecording = false,
                        IsCancelled = cancel
                    });

                    ErrorLogger.LogDebug("RecordingCoordinator.StopRecording: Recording stopped successfully");
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("RecordingCoordinator.StopRecording failed", ex);
                    stateMachine.TryTransition(RecordingState.Error);
                    stateMachine.TryTransition(RecordingState.Idle);
                    ErrorOccurred?.Invoke(this, "Error stopping recording");
                }
            }
        }

        /// <summary>
        /// Get current recording duration
        /// </summary>
        public TimeSpan GetRecordingDuration()
        {
            // WEEK1-DAY3: Use state machine instead of bool flag
            if (stateMachine.CurrentState != RecordingState.Recording)
                return TimeSpan.Zero;

            return DateTime.Now - recordingStartTime;
        }

        /// <summary>
        /// Play sound feedback (if enabled)
        /// </summary>
        public void PlaySoundFeedback()
        {
            if (settings.PlaySoundFeedback)
            {
                soundService?.PlaySound();
            }
        }

        /// <summary>
        /// Handle audio file ready event from AudioRecorder
        /// </summary>
        private async void OnAudioFileReady(object? sender, string audioFilePath)
        {
            // CRIT-003 FIX: Wrap entire async void method in try-catch to prevent app crash
            try
            {
                // DISPOSAL SAFETY: Early exit if disposed
                if (isDisposed)
                {
                    ErrorLogger.LogMessage("RecordingCoordinator.OnAudioFileReady: Disposed, skipping transcription");
                    await CleanupAudioFileAsync(audioFilePath).ConfigureAwait(false);
                    return;
                }

                // WEEK1-DAY3: Check state machine instead of isCancelled flag
                var currentState = stateMachine.CurrentState;
                ErrorLogger.LogMessage($"RecordingCoordinator.OnAudioFileReady: Entry - state={currentState}, file={audioFilePath}");

                // If recording was cancelled, just clean up and return
                if (currentState == RecordingState.Cancelled)
                {
                    ErrorLogger.LogMessage("RecordingCoordinator.OnAudioFileReady: Recording was cancelled, skipping transcription");
                    stateMachine.TryTransition(RecordingState.Idle); // Reset to idle
                    await CleanupAudioFileAsync(audioFilePath).ConfigureAwait(false);
                    return;
                }

                await ProcessAudioFileAsync(audioFilePath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("CRITICAL: Unhandled exception in OnAudioFileReady", ex);

                // Fire error event to notify UI
                var errorArgs = new TranscriptionCompleteEventArgs
                {
                    Transcription = string.Empty,
                    ErrorMessage = $"Fatal transcription error: {ex.Message}",
                    Success = false
                };

                try
                {
                    TranscriptionCompleted?.Invoke(this, errorArgs);
                }
                catch (Exception invokeEx)
                {
                    ErrorLogger.LogError("Failed to invoke TranscriptionCompleted error event", invokeEx);
                }

                // Clean up audio file
                try
                {
                    await CleanupAudioFileAsync(audioFilePath).ConfigureAwait(false);
                }
                catch (Exception cleanupEx)
                {
                    ErrorLogger.LogError("Failed to cleanup audio file after error", cleanupEx);
                }
            }
        }

        private async Task ProcessAudioFileAsync(string audioFilePath)
        {
            // PERFORMANCE: Use original audio file directly (no copy needed)
            // AudioRecorder already creates unique temp files per recording
            // Semaphore in WhisperService prevents concurrent access
            // We still need to clean up the original file after transcription
            string workingAudioPath = audioFilePath;

            // WEEK1-DAY3: Transition to Transcribing state
            if (!stateMachine.TryTransition(RecordingState.Transcribing))
            {
                ErrorLogger.LogWarning($"ProcessAudioFileAsync: Cannot transition to Transcribing from {stateMachine.CurrentState}");
                await CleanupAudioFileAsync(audioFilePath).ConfigureAwait(false);
                return;
            }

            // Notify UI that transcription is starting
            StatusChanged?.Invoke(this, new RecordingStatusEventArgs
            {
                Status = "Transcribing",
                IsRecording = false
            });

            // Start watchdog timer to detect stuck transcriptions
            StartTranscriptionWatchdog();

            // WEEK1-DAY2: Signal that transcription is in progress
            transcriptionComplete.Reset();

            // BUG FIX (BUG-007): Move completion flag and event firing logic to prevent double-fire
            // If event handler throws exception, catch block would fire event again
            TranscriptionCompleteEventArgs? eventArgs = null;

            try
            {
                // Run transcription on background thread
                var transcription = await Task.Run(async () =>
                    await whisperService.TranscribeAsync(workingAudioPath).ConfigureAwait(false)).ConfigureAwait(false);

                ErrorLogger.LogMessage($"Transcription result: '{transcription?.Substring(0, Math.Min(transcription?.Length ?? 0, 50))}'... (length: {transcription?.Length})");

                // Track analytics for successful transcription
                if (!string.IsNullOrWhiteSpace(transcription) && analyticsService != null)
                {
                    var wordCount = TextAnalyzer.CountWords(transcription);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await analyticsService.TrackTranscriptionAsync(settings.WhisperModel, wordCount).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            // BUG FIX (BUG-013): Log analytics failures, don't let them crash transcription
                            ErrorLogger.LogError("Analytics tracking failed (non-fatal)", ex);
                        }
                    });
                }

                // Create history item
                TranscriptionHistoryItem? historyItem = null;
                if (!string.IsNullOrWhiteSpace(transcription) && !isDisposed)
                {
                    historyItem = new TranscriptionHistoryItem
                    {
                        Timestamp = DateTime.Now,
                        Text = transcription,
                        WordCount = TextAnalyzer.CountWords(transcription),
                        DurationSeconds = (DateTime.Now - recordingStartTime).TotalSeconds,
                        ModelUsed = settings.WhisperModel
                    };

                    if (!isDisposed)
                    {
                        historyService?.AddToHistory(historyItem);
                    }
                }

                // WEEK1-DAY3-FIX: ALWAYS transition to Injecting state, even if no text
                // This prevents invalid Transcribing → Complete transition
                stateMachine.TryTransition(RecordingState.Injecting);

                // Handle text injection on background thread if we have text
                bool textInjected = false;
                if (!string.IsNullOrWhiteSpace(transcription))
                {
                    textInjector.AutoPaste = settings.AutoPaste;

                    if (settings.AutoPaste)
                    {
                        ErrorLogger.LogMessage("Auto-pasting text via Ctrl+V simulation");
                        StatusChanged?.Invoke(this, new RecordingStatusEventArgs { Status = "Pasting" });
                    }
                    else
                    {
                        ErrorLogger.LogMessage("Copying text to clipboard (manual paste required)");
                        StatusChanged?.Invoke(this, new RecordingStatusEventArgs { Status = "Copied to clipboard" });
                    }

                    await Task.Run(() => textInjector.InjectText(transcription)).ConfigureAwait(false);
                    textInjected = true;
                }

                // WEEK1-DAY3: Transition to Complete state (success path)
                stateMachine.TryTransition(RecordingState.Complete);

                // Prepare event args (don't fire yet)
                eventArgs = new TranscriptionCompleteEventArgs
                {
                    Transcription = transcription ?? string.Empty,
                    HistoryItem = historyItem,
                    TextWasInjected = textInjected,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("RecordingCoordinator.OnAudioFileReady", ex);

                // WEEK1-DAY3: Transition to Error state
                stateMachine.TryTransition(RecordingState.Error);

                // Prepare error event args (don't fire yet)
                eventArgs = new TranscriptionCompleteEventArgs
                {
                    Transcription = string.Empty,
                    ErrorMessage = $"Transcription error: {ex.Message}",
                    Success = false
                };
            }
            finally
            {
                // BUG FIX (CRIT-005): Atomically mark as completed to prevent race condition with watchdog
                // Only fire event if we're the first to complete (either normal or watchdog, but not both)
                bool wasCompleted = System.Threading.Interlocked.CompareExchange(ref transcriptionCompletedFlag, 1, 0) == 1;

                if (!wasCompleted)
                {
                    // We're the first to complete - stop watchdog and fire event
                    StopTranscriptionWatchdog();

                    // Fire event outside try-catch to prevent double-fire if handler throws
                    if (eventArgs != null)
                    {
                        try
                        {
                            TranscriptionCompleted?.Invoke(this, eventArgs);
                        }
                        catch (Exception ex)
                        {
                            ErrorLogger.LogError("TranscriptionCompleted event handler threw exception", ex);
                        }
                    }
                }
                else
                {
                    // Watchdog already fired timeout event - skip event firing to prevent duplicate
                    ErrorLogger.LogWarning("RecordingCoordinator: Transcription completed after watchdog timeout - event already fired");
                }

                // RACE CONDITION FIX: Wait briefly to ensure Whisper process has closed file handle
                // Whisper process might still be reading the file, give it time to release handle
                await Task.Delay(300).ConfigureAwait(false);

                // CLEANUP: Always delete the original audio file from AudioRecorder
                // This prevents temp files from accumulating in AppData
                if (File.Exists(workingAudioPath))
                {
                    await CleanupAudioFileAsync(workingAudioPath).ConfigureAwait(false);
                }

                // WEEK1-DAY3: Return to Idle state (ready for next recording)
                stateMachine.TryTransition(RecordingState.Idle);

                // WEEK1-DAY2: Signal that transcription is complete (success or failure)
                // This allows Dispose() to proceed without spin-waiting
                transcriptionComplete.Set();
            }
        }

        /// <summary>
        /// Start watchdog timer to detect stuck transcriptions
        /// </summary>
        private void StartTranscriptionWatchdog()
        {
            // WEEK1-DAY3: No need to set isTranscribing flag, state machine tracks this
            System.Threading.Interlocked.Exchange(ref transcriptionCompletedFlag, 0); // Reset completion flag atomically
            transcriptionStartTime = DateTime.Now;

            // Create watchdog timer that checks every 10 seconds
            transcriptionWatchdog = new System.Threading.Timer(
                callback: WatchdogCallback,
                state: null,
                dueTime: TimeSpan.FromSeconds(10),
                period: TimeSpan.FromSeconds(10)
            );

            ErrorLogger.LogDebug($"RecordingCoordinator: Watchdog started - timeout={TRANSCRIPTION_TIMEOUT_SECONDS}s");
        }

        /// <summary>
        /// Stop watchdog timer - safe to call multiple times
        /// </summary>
        private void StopTranscriptionWatchdog()
        {
            // WEEK1-DAY3: No need to clear isTranscribing flag, state machine tracks this

            var watchdog = transcriptionWatchdog;
            transcriptionWatchdog = null; // Clear reference first to prevent double-dispose

            if (watchdog != null)
            {
                try
                {
                    watchdog.Dispose();
                    ErrorLogger.LogDebug("RecordingCoordinator: Watchdog stopped");
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, ignore
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("RecordingCoordinator: Error stopping watchdog", ex);
                }
            }
        }

        /// <summary>
        /// Watchdog callback - detects if transcription has been stuck too long
        /// CRITICAL: Must never throw exceptions (runs on timer thread)
        /// </summary>
        private void WatchdogCallback(object? state)
        {
            try
            {
                // WEEK1-DAY3: Check state machine instead of isTranscribing flag
                if (stateMachine.CurrentState != RecordingState.Transcribing)
                    return;

                var elapsed = DateTime.Now - transcriptionStartTime;
                ErrorLogger.LogDebug($"RecordingCoordinator: Watchdog check - elapsed={elapsed.TotalSeconds:F0}s");

                if (elapsed.TotalSeconds > TRANSCRIPTION_TIMEOUT_SECONDS)
                {
                    ErrorLogger.LogWarning($"RecordingCoordinator: Watchdog TIMEOUT! Transcription stuck for {elapsed.TotalSeconds:F0}s");

                    // BUG FIX (CRIT-005): Atomically check-and-set completion flag to prevent race with normal completion
                    // Only one thread (either watchdog or OnAudioFileReady finally) should fire the event
                    bool wasAlreadyCompleted = System.Threading.Interlocked.CompareExchange(ref transcriptionCompletedFlag, 1, 0) == 1;

                    if (!wasAlreadyCompleted)
                    {
                        // We won the race - fire timeout event
                        StopTranscriptionWatchdog();

                        TranscriptionCompleted?.Invoke(this, new TranscriptionCompleteEventArgs
                        {
                            Transcription = string.Empty,
                            ErrorMessage = $"Transcription timed out after {TRANSCRIPTION_TIMEOUT_SECONDS} seconds.\n\n" +
                                         "This may be caused by:\n" +
                                         "• Whisper process hung or crashed\n" +
                                         "• System running low on memory\n" +
                                         "• Antivirus blocking the process\n\n" +
                                         "Try restarting the application or using a smaller model.",
                            Success = false
                        });
                    }
                    else
                    {
                        // Normal completion beat us to it - do nothing
                        ErrorLogger.LogMessage("RecordingCoordinator: Watchdog detected timeout but transcription already completed normally");
                    }
                }
            }
            catch (Exception ex)
            {
                // CRITICAL: Watchdog runs on timer thread - exceptions must not escape
                ErrorLogger.LogError("RecordingCoordinator: Watchdog callback exception", ex);

                // Try to stop watchdog even if error occurred
                try { StopTranscriptionWatchdog(); } catch { /* ignore */ }
            }
        }

        /// <summary>
        /// Clean up audio file with retry logic (fire-and-forget to prevent UI blocking)
        /// </summary>
        private async Task CleanupAudioFileAsync(string audioFilePath)
        {
            // PERFORMANCE FIX: Run cleanup on background thread pool to prevent UI blocking
            // File.Delete() can block for 50-200ms with antivirus scanning
            await Task.Run(async () =>
            {
                // Small delay to let file handles close
                await Task.Delay(100).ConfigureAwait(false);

                for (int i = 0; i < TimingConstants.FileCleanupMaxRetries; i++)
                {
                    try
                    {
                        if (File.Exists(audioFilePath))
                        {
                            File.Delete(audioFilePath);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (i == TimingConstants.FileCleanupMaxRetries - 1) // Last attempt
                        {
                            // Don't block on logging either - fire and forget
                            _ = Task.Run(() => ErrorLogger.LogError("RecordingCoordinator.CleanupAudioFile", ex));
                        }

                        // Exponential backoff: 50ms, 100ms, 200ms, 400ms
                        int delayMs = 50 * (int)Math.Pow(2, i);
                        await Task.Delay(delayMs).ConfigureAwait(false);
                    }
                }
            }).ConfigureAwait(false);
        }

        private volatile bool isDisposing = false; // WEEK1-DAY2: Guard for double-disposal

        public void Dispose()
        {
            // WEEK1-DAY2: Guard against multiple Dispose() calls
            if (isDisposing) return;
            isDisposing = true;

            // DISPOSAL SAFETY: Set flag first to prevent new operations
            isDisposed = true;

            // BUG FIX (BUG-001): Unsubscribe FIRST to prevent new events from firing
            // This ensures no new OnAudioFileReady events can be queued after this point
            if (audioRecorder != null)
            {
                audioRecorder.AudioFileReady -= OnAudioFileReady;
            }

            // WEEK1-DAY2: Wait for transcription to complete using ManualResetEventSlim
            // Old approach: Spin-wait loop (100% CPU for 100ms, then 50ms sleeps up to 30s)
            // New approach: Efficient signaling with 5s timeout
            try
            {
                if (!transcriptionComplete.Wait(5000)) // Wait max 5 seconds
                {
                    ErrorLogger.LogWarning("RecordingCoordinator.Dispose - Transcription did not complete within 5s timeout, forcing disposal");
                }
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }

            StopTranscriptionWatchdog();

            // WEEK1-DAY3: Reset state machine to Idle on disposal
            stateMachine.Reset();

            // WEEK1-DAY2: Dispose ManualResetEventSlim to free resources
            transcriptionComplete?.Dispose();
        }
    }

    /// <summary>
    /// Event args for recording status changes
    /// </summary>
    public class RecordingStatusEventArgs : EventArgs
    {
        public string Status { get; set; } = string.Empty;
        public bool IsRecording { get; set; }
        public bool IsCancelled { get; set; }
        public int ElapsedSeconds { get; set; }
    }

    /// <summary>
    /// Event args for transcription completion
    /// </summary>
    public class TranscriptionCompleteEventArgs : EventArgs
    {
        public string Transcription { get; set; } = string.Empty;
        public TranscriptionHistoryItem? HistoryItem { get; set; }
        public bool TextWasInjected { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

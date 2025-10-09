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
    /// Simplified version with fail-fast errors and single timeout.
    /// </summary>
    public class RecordingCoordinator : IDisposable
    {
        private readonly AudioRecorder audioRecorder;
        private readonly ITranscriber whisperService;
        private readonly TextInjector textInjector;
        private readonly TranscriptionHistoryService? historyService;
        private readonly SoundService? soundService;
        private readonly Settings settings;

        private readonly RecordingStateMachine stateMachine;

        private DateTime recordingStartTime;
        private readonly object recordingLock = new object();
        private System.Threading.Timer? transcriptionWatchdog;
        private volatile bool isDisposed = false;
        private DateTime transcriptionStartTime;
        private const int TRANSCRIPTION_TIMEOUT_SECONDS = 60; // 1 minute max
        private readonly System.Threading.ManualResetEventSlim transcriptionComplete = new System.Threading.ManualResetEventSlim(true);

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
            this.soundService = soundService;
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            this.stateMachine = new RecordingStateMachine();

            audioRecorder.AudioFileReady += OnAudioFileReady;
        }

        /// <summary>
        /// Start recording audio
        /// </summary>
        public void StartRecording()
        {
            lock (recordingLock)
            {
                // Fail-fast: Reset if stuck in non-Idle state (defensive recovery for rapid hotkey presses)
                if (stateMachine.CurrentState != RecordingState.Idle)
                {
                    ErrorLogger.LogWarning($"StartRecording called from {stateMachine.CurrentState}, forcing reset to Idle");
                    stateMachine.Reset();
                }

                if (!stateMachine.TryTransition(RecordingState.Recording))
                {
                    ErrorLogger.LogWarning($"Cannot start recording from {stateMachine.CurrentState}");
                    return;
                }

                try
                {
                    recordingStartTime = DateTime.Now;
                    audioRecorder.StartRecording();

                    StatusChanged?.Invoke(this, new RecordingStatusEventArgs
                    {
                        Status = "Recording",
                        IsRecording = true,
                        ElapsedSeconds = 0
                    });
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("No microphone"))
                {
                    stateMachine.TryTransition(RecordingState.Error);
                    stateMachine.TryTransition(RecordingState.Idle);
                    ErrorOccurred?.Invoke(this, $"No microphone detected!\n\nPlease connect a microphone and restart the application.");
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Failed to access"))
                {
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
            lock (recordingLock)
            {
                RecordingState targetState = cancel ? RecordingState.Cancelled : RecordingState.Stopping;

                if (!stateMachine.TryTransition(targetState))
                {
                    ErrorLogger.LogWarning($"Cannot transition to {targetState} from {stateMachine.CurrentState}");
                    return;
                }

                try
                {
                    if (cancel)
                    {
                        StopTranscriptionWatchdog();
                    }

                    audioRecorder.StopRecording();

                    StatusChanged?.Invoke(this, new RecordingStatusEventArgs
                    {
                        Status = cancel ? "Cancelled" : "Processing",
                        IsRecording = false,
                        IsCancelled = cancel
                    });
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
            try
            {
                if (isDisposed)
                {
                    await CleanupAudioFileAsync(audioFilePath).ConfigureAwait(false);
                    return;
                }

                var currentState = stateMachine.CurrentState;

                if (currentState == RecordingState.Cancelled)
                {
                    stateMachine.TryTransition(RecordingState.Idle);
                    await CleanupAudioFileAsync(audioFilePath).ConfigureAwait(false);
                    return;
                }

                await ProcessAudioFileAsync(audioFilePath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("CRITICAL: Unhandled exception in OnAudioFileReady", ex);

                TranscriptionCompleted?.Invoke(this, new TranscriptionCompleteEventArgs
                {
                    Transcription = string.Empty,
                    ErrorMessage = $"Fatal transcription error: {ex.Message}",
                    Success = false
                });

                await CleanupAudioFileAsync(audioFilePath).ConfigureAwait(false);
            }
        }

        private async Task ProcessAudioFileAsync(string audioFilePath)
        {
            if (!stateMachine.TryTransition(RecordingState.Transcribing))
            {
                ErrorLogger.LogWarning($"Cannot transition to Transcribing from {stateMachine.CurrentState}");
                await CleanupAudioFileAsync(audioFilePath).ConfigureAwait(false);
                return;
            }

            StatusChanged?.Invoke(this, new RecordingStatusEventArgs
            {
                Status = "Transcribing",
                IsRecording = false
            });

            StartTranscriptionWatchdog();
            transcriptionComplete.Reset();

            TranscriptionCompleteEventArgs? eventArgs = null;

            try
            {
                // Single transcription attempt - fail fast with clear errors
                string? transcription = await Task.Run(async () =>
                    await whisperService.TranscribeAsync(audioFilePath).ConfigureAwait(false)).ConfigureAwait(false);

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

                    historyService?.AddToHistory(historyItem);
                }

                stateMachine.TryTransition(RecordingState.Injecting);

                // Handle text injection if we have text
                bool textInjected = false;
                if (!string.IsNullOrWhiteSpace(transcription))
                {
                    textInjector.AutoPaste = settings.AutoPaste;

                    if (settings.AutoPaste)
                    {
                        StatusChanged?.Invoke(this, new RecordingStatusEventArgs { Status = "Pasting" });
                    }
                    else
                    {
                        StatusChanged?.Invoke(this, new RecordingStatusEventArgs { Status = "Copied to clipboard" });
                    }

                    await Task.Run(() => textInjector.InjectText(transcription)).ConfigureAwait(false);
                    textInjected = true;
                }

                stateMachine.TryTransition(RecordingState.Complete);

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
                ErrorLogger.LogError("Transcription failed", ex);
                stateMachine.TryTransition(RecordingState.Error);

                eventArgs = new TranscriptionCompleteEventArgs
                {
                    Transcription = string.Empty,
                    ErrorMessage = $"Transcription error: {ex.Message}",
                    Success = false
                };
            }
            finally
            {
                StopTranscriptionWatchdog();

                if (eventArgs != null)
                {
                    TranscriptionCompleted?.Invoke(this, eventArgs);
                }

                stateMachine.TryTransition(RecordingState.Idle);

                if (File.Exists(audioFilePath))
                {
                    await CleanupAudioFileAsync(audioFilePath).ConfigureAwait(false);
                }

                transcriptionComplete.Set();
            }
        }

        /// <summary>
        /// Start watchdog timer to detect stuck transcriptions
        /// </summary>
        private void StartTranscriptionWatchdog()
        {
            transcriptionStartTime = DateTime.Now;

            transcriptionWatchdog = new System.Threading.Timer(
                callback: WatchdogCallback,
                state: null,
                dueTime: TRANSCRIPTION_TIMEOUT_SECONDS * 1000,
                period: System.Threading.Timeout.Infinite
            );
        }

        /// <summary>
        /// Stop watchdog timer
        /// </summary>
        private void StopTranscriptionWatchdog()
        {
            var watchdog = transcriptionWatchdog;
            transcriptionWatchdog = null;

            watchdog?.Dispose();
        }

        /// <summary>
        /// Watchdog callback - simple timeout handler
        /// </summary>
        private void WatchdogCallback(object? state)
        {
            try
            {
                if (stateMachine.CurrentState == RecordingState.Transcribing)
                {
                    ErrorLogger.LogWarning($"Transcription timeout after {TRANSCRIPTION_TIMEOUT_SECONDS} seconds");

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
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Watchdog callback exception", ex);
            }
        }

        /// <summary>
        /// Clean up audio file
        /// </summary>
        private async Task CleanupAudioFileAsync(string audioFilePath)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (File.Exists(audioFilePath))
                    {
                        File.Delete(audioFilePath);
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Failed to delete audio file", ex);
                }
            }).ConfigureAwait(false);
        }

        private volatile bool isDisposing = false;

        public void Dispose()
        {
            if (isDisposing) return;
            isDisposing = true;

            isDisposed = true;

            if (audioRecorder != null)
            {
                audioRecorder.AudioFileReady -= OnAudioFileReady;
            }

            try
            {
                if (!transcriptionComplete.Wait(5000))
                {
                    ErrorLogger.LogWarning("Transcription did not complete within 5s timeout, forcing disposal");
                }
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }

            StopTranscriptionWatchdog();
            stateMachine.Reset();
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

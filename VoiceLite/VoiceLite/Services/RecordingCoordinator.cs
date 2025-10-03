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

        private bool _isRecording = false;
        private bool isCancelled = false;
        private DateTime recordingStartTime;
        private readonly object recordingLock = new object();

        // Events for MainWindow to subscribe to
        public event EventHandler<RecordingStatusEventArgs>? StatusChanged;
        public event EventHandler<TranscriptionCompleteEventArgs>? TranscriptionCompleted;
        public event EventHandler<string>? ErrorOccurred;

        public bool IsRecording => _isRecording;

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

            // Wire up audio recorder events
            audioRecorder.AudioFileReady += OnAudioFileReady;
        }

        /// <summary>
        /// Start recording audio
        /// </summary>
        public void StartRecording()
        {
            ErrorLogger.LogDebug($"RecordingCoordinator.StartRecording: Entry - isRecording={_isRecording}");

            lock (recordingLock)
            {
                if (_isRecording)
                {
                    ErrorLogger.LogDebug("RecordingCoordinator.StartRecording: Already recording, returning early");
                    return;
                }

                try
                {
                    _isRecording = true;
                    isCancelled = false;
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
                    _isRecording = false;
                    ErrorOccurred?.Invoke(this, $"No microphone detected!\n\nPlease connect a microphone and restart the application.");
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Failed to access"))
                {
                    ErrorLogger.LogWarning($"StartRecording failed: Microphone busy - {ex.Message}");
                    _isRecording = false;
                    ErrorOccurred?.Invoke(this, ex.Message);
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("RecordingCoordinator.StartRecording failed", ex);
                    _isRecording = false;
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
            ErrorLogger.LogDebug($"RecordingCoordinator.StopRecording: Entry - isRecording={_isRecording}, cancel={cancel}");

            lock (recordingLock)
            {
                if (!_isRecording)
                {
                    ErrorLogger.LogDebug("RecordingCoordinator.StopRecording: Not recording, returning early");
                    return;
                }

                try
                {
                    if (cancel)
                    {
                        isCancelled = true;
                        ErrorLogger.LogInfo("Recording cancelled by user");
                    }

                    audioRecorder.StopRecording();
                    _isRecording = false;

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
                    _isRecording = false;
                    ErrorOccurred?.Invoke(this, "Error stopping recording");
                }
            }
        }

        /// <summary>
        /// Get current recording duration
        /// </summary>
        public TimeSpan GetRecordingDuration()
        {
            if (!_isRecording)
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
            ErrorLogger.LogMessage($"RecordingCoordinator.OnAudioFileReady: Entry - isCancelled={isCancelled}, file={audioFilePath}");

            // If recording was cancelled, just clean up and return
            if (isCancelled)
            {
                ErrorLogger.LogMessage("RecordingCoordinator.OnAudioFileReady: Recording was cancelled, skipping transcription");
                isCancelled = false;
                await CleanupAudioFileAsync(audioFilePath);
                return;
            }

            string workingAudioPath = audioFilePath;
            bool createdCopy = false;

            try
            {
                // Create isolated copy of audio file to avoid race conditions
                if (File.Exists(audioFilePath))
                {
                    var audioDirectory = Path.GetDirectoryName(audioFilePath);
                    if (!string.IsNullOrEmpty(audioDirectory))
                    {
                        var uniqueFileName = $"audio_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}.wav";
                        var tempCopyPath = Path.Combine(audioDirectory, uniqueFileName);
                        File.Copy(audioFilePath, tempCopyPath);
                        workingAudioPath = tempCopyPath;
                        createdCopy = true;
                        ErrorLogger.LogMessage($"RecordingCoordinator.OnAudioFileReady: Using isolated copy {workingAudioPath}");
                    }
                }
            }
            catch (Exception copyEx)
            {
                ErrorLogger.LogError("RecordingCoordinator.OnAudioFileReady.CopyAudio", copyEx);
                workingAudioPath = audioFilePath;
            }

            // Notify UI that transcription is starting
            StatusChanged?.Invoke(this, new RecordingStatusEventArgs
            {
                Status = "Transcribing",
                IsRecording = false
            });

            try
            {
                // Run transcription on background thread
                var transcription = await Task.Run(async () =>
                    await whisperService.TranscribeAsync(workingAudioPath));

                ErrorLogger.LogMessage($"Transcription result: '{transcription?.Substring(0, Math.Min(transcription?.Length ?? 0, 50))}'... (length: {transcription?.Length})");

                // Track analytics for successful transcription
                if (!string.IsNullOrWhiteSpace(transcription) && analyticsService != null)
                {
                    var wordCount = TextAnalyzer.CountWords(transcription);
                    _ = analyticsService.TrackTranscriptionAsync(settings.WhisperModel, wordCount);
                }

                // Create history item
                TranscriptionHistoryItem? historyItem = null;
                if (!string.IsNullOrWhiteSpace(transcription))
                {
                    historyItem = new TranscriptionHistoryItem
                    {
                        Timestamp = DateTime.Now,
                        Text = transcription,
                        WordCount = TextAnalyzer.CountWords(transcription),
                        DurationSeconds = (DateTime.Now - recordingStartTime).TotalSeconds,
                        ModelUsed = settings.WhisperModel
                    };

                    // Add to history service
                    historyService?.AddToHistory(historyItem);
                }

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

                    // Run text injection on background thread
                    await Task.Run(() => textInjector.InjectText(transcription));
                    textInjected = true;
                }

                // Notify UI that transcription is complete
                TranscriptionCompleted?.Invoke(this, new TranscriptionCompleteEventArgs
                {
                    Transcription = transcription ?? string.Empty,
                    HistoryItem = historyItem,
                    TextWasInjected = textInjected,
                    Success = true
                });
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("RecordingCoordinator.OnAudioFileReady", ex);
                TranscriptionCompleted?.Invoke(this, new TranscriptionCompleteEventArgs
                {
                    Transcription = string.Empty,
                    ErrorMessage = $"Transcription error: {ex.Message}",
                    Success = false
                });
            }
            finally
            {
                // Clean up working audio file
                if (createdCopy && File.Exists(workingAudioPath))
                {
                    await CleanupAudioFileAsync(workingAudioPath);
                }
            }
        }

        /// <summary>
        /// Clean up audio file with retry logic
        /// </summary>
        private async Task CleanupAudioFileAsync(string audioFilePath)
        {
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
                        ErrorLogger.LogError("RecordingCoordinator.CleanupAudioFile", ex);
                    await Task.Delay(TimingConstants.FileCleanupRetryDelayMs);
                }
            }
        }

        public void Dispose()
        {
            if (audioRecorder != null)
            {
                audioRecorder.AudioFileReady -= OnAudioFileReady;
            }
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

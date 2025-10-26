using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VoiceLite.Core.Interfaces.Controllers;
using VoiceLite.Core.Interfaces.Services;
using VoiceLite.Core.Interfaces.Features;
using VoiceLite.Services;
using VoiceLite.Helpers;

namespace VoiceLite.Core.Controllers
{
    /// <summary>
    /// Orchestrates the recording, transcription, and text injection workflow
    /// </summary>
    public class RecordingController : IRecordingController
    {
        private readonly IAudioRecorder _audioRecorder;
        private readonly IWhisperService _whisperService;
        private readonly ITextInjector _textInjector;
        private readonly ITranscriptionHistoryService _historyService;
        private readonly IErrorLogger _errorLogger;
        private readonly ISettingsService _settingsService;

        private volatile bool _isRecording;
        private volatile bool _isTranscribing;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly object _lockObject = new object();
        private string? _currentAudioFile;
        private readonly Stopwatch _processingStopwatch = new Stopwatch();

        // Properties
        public bool IsRecording => _isRecording;
        public bool IsTranscribing => _isTranscribing;

        // Events
        public event EventHandler? RecordingStarted;
        public event EventHandler? RecordingStopped;
        public event EventHandler? TranscriptionStarted;
        public event EventHandler<TranscriptionResult>? TranscriptionCompleted;
        public event EventHandler<RecordingProgress>? ProgressChanged;

        public RecordingController(
            IAudioRecorder audioRecorder,
            IWhisperService whisperService,
            ITextInjector textInjector,
            ITranscriptionHistoryService historyService,
            IErrorLogger errorLogger,
            ISettingsService settingsService)
        {
            _audioRecorder = audioRecorder ?? throw new ArgumentNullException(nameof(audioRecorder));
            _whisperService = whisperService ?? throw new ArgumentNullException(nameof(whisperService));
            _textInjector = textInjector ?? throw new ArgumentNullException(nameof(textInjector));
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
            _errorLogger = errorLogger ?? throw new ArgumentNullException(nameof(errorLogger));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            // Subscribe to service events
            _audioRecorder.AudioFileReady += OnAudioFileReady;
            _audioRecorder.RecordingError += OnRecordingError;
            _whisperService.TranscriptionError += OnTranscriptionError;
            _whisperService.ProgressChanged += OnWhisperProgressChanged;
        }

        /// <summary>
        /// Starts the complete recording and transcription workflow
        /// </summary>
        public async Task<TranscriptionResult> RecordAndTranscribeAsync(
            string modelPath,
            ITextInjector.InjectionMode injectionMode)
        {
            var result = new TranscriptionResult
            {
                ModelUsed = Path.GetFileNameWithoutExtension(modelPath)
            };

            try
            {
                _processingStopwatch.Restart();

                // Start recording
                await StartRecordingAsync();

                // Wait for user to stop recording (this would typically be triggered by hotkey)
                // For now, we return and let the user stop it manually
                result.Success = true;
                result.Text = "Recording started. Press hotkey to stop.";

                return result;
            }
            catch (Exception ex)
            {
                _errorLogger.LogError(ex, "RecordAndTranscribeAsync failed");
                result.Success = false;
                result.Error = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Starts recording audio
        /// </summary>
        public async Task StartRecordingAsync()
        {
            await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_isRecording)
                    {
                        _errorLogger.LogWarning("Recording already in progress");
                        return;
                    }

                    try
                    {
                        _cancellationTokenSource = new CancellationTokenSource();
                        _isRecording = true;

                        ReportProgress("Starting recording", 0);
                        RecordingStarted?.Invoke(this, EventArgs.Empty);

                        _audioRecorder.StartRecording();

                        ReportProgress("Recording", 10);
                    }
                    catch (Exception ex)
                    {
                        _isRecording = false;
                        _errorLogger.LogError(ex, "Failed to start recording");
                        throw;
                    }
                }
            });
        }

        /// <summary>
        /// Stops recording and optionally transcribes the audio
        /// </summary>
        public async Task<TranscriptionResult> StopRecordingAsync(bool transcribe = true)
        {
            var result = new TranscriptionResult();

            try
            {
                lock (_lockObject)
                {
                    if (!_isRecording)
                    {
                        result.Error = "No recording in progress";
                        return result;
                    }

                    _audioRecorder.StopRecording();
                    _isRecording = false;
                    RecordingStopped?.Invoke(this, EventArgs.Empty);
                    ReportProgress("Recording stopped", 20);
                }

                if (!transcribe)
                {
                    result.Success = true;
                    return result;
                }

                // Get the audio file
                _currentAudioFile = await _audioRecorder.GetLastAudioFileAsync();
                if (string.IsNullOrEmpty(_currentAudioFile))
                {
                    result.Error = "No audio file available";
                    return result;
                }

                // Transcribe the audio
                result = await TranscribeInternalAsync(_currentAudioFile);

                // Add to history if successful
                if (result.Success && !string.IsNullOrWhiteSpace(result.Text))
                {
                    var historyItem = new TranscriptionItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Text = result.Text,
                        Timestamp = DateTime.Now,
                        ModelUsed = result.ModelUsed,
                        ProcessingTime = result.ProcessingTime.TotalSeconds,
                        ApplicationContext = _textInjector.GetFocusedApplicationName()
                    };

                    _historyService.AddTranscription(historyItem);
                }

                return result;
            }
            catch (Exception ex)
            {
                _errorLogger.LogError(ex, "StopRecordingAsync failed");
                result.Success = false;
                result.Error = ex.Message;
                return result;
            }
            finally
            {
                _processingStopwatch.Stop();
                result.ProcessingTime = _processingStopwatch.Elapsed;
            }
        }

        /// <summary>
        /// Cancels the current recording or transcription operation
        /// </summary>
        public void Cancel()
        {
            try
            {
                _cancellationTokenSource?.Cancel();

                if (_isRecording)
                {
                    _audioRecorder.StopRecording();
                    _isRecording = false;
                    RecordingStopped?.Invoke(this, EventArgs.Empty);
                }

                if (_isTranscribing)
                {
                    _whisperService.CancelTranscription();
                    _isTranscribing = false;
                }

                ReportProgress("Cancelled", 0);
            }
            catch (Exception ex)
            {
                _errorLogger.LogError(ex, "Cancel operation failed");
            }
        }

        /// <summary>
        /// Transcribes an existing audio file
        /// </summary>
        public async Task<TranscriptionResult> TranscribeFileAsync(string audioFilePath, string modelPath)
        {
            if (!File.Exists(audioFilePath))
            {
                return new TranscriptionResult
                {
                    Success = false,
                    Error = $"Audio file not found: {audioFilePath}"
                };
            }

            return await TranscribeInternalAsync(audioFilePath, modelPath);
        }

        private async Task<TranscriptionResult> TranscribeInternalAsync(
            string audioFilePath,
            string? modelPath = null)
        {
            var result = new TranscriptionResult
            {
                AudioFilePath = audioFilePath,
                ModelUsed = modelPath ?? _settingsService.SelectedModel
            };

            try
            {
                _isTranscribing = true;
                TranscriptionStarted?.Invoke(this, EventArgs.Empty);
                ReportProgress("Starting transcription", 30);

                // Perform transcription
                var text = await _whisperService.TranscribeAsync(
                    audioFilePath,
                    result.ModelUsed);

                if (string.IsNullOrWhiteSpace(text))
                {
                    result.Error = "Transcription returned empty text";
                    ReportProgress("Transcription failed", 0);
                    return result;
                }

                result.Text = text;
                result.Success = true;
                ReportProgress("Transcription complete", 80);

                // Inject text if settings allow
                if (_settingsService.InjectionMode != ITextInjector.InjectionMode.SmartAuto)
                {
                    ReportProgress("Injecting text", 90);
                    await _textInjector.InjectTextAsync(text, _settingsService.InjectionMode);
                }

                ReportProgress("Complete", 100);
                return result;
            }
            catch (Exception ex)
            {
                _errorLogger.LogError(ex, "TranscribeInternalAsync failed");
                result.Success = false;
                result.Error = ex.Message;
                return result;
            }
            finally
            {
                _isTranscribing = false;
                TranscriptionCompleted?.Invoke(this, result);
            }
        }

        private void OnAudioFileReady(object? sender, string audioFilePath)
        {
            _currentAudioFile = audioFilePath;
            ReportProgress("Audio file ready", 25);
        }

        private void OnRecordingError(object? sender, Exception error)
        {
            _errorLogger.LogError(error, "Recording error");
            _isRecording = false;
            ReportProgress($"Recording error: {error.Message}", 0);
        }

        private void OnTranscriptionError(object? sender, Exception error)
        {
            _errorLogger.LogError(error, "Transcription error");
            _isTranscribing = false;
            ReportProgress($"Transcription error: {error.Message}", 0);
        }

        private void OnWhisperProgressChanged(object? sender, int progress)
        {
            // Map Whisper progress (0-100) to our overall progress (30-80)
            var overallProgress = 30 + (progress * 50 / 100);
            ReportProgress($"Transcribing... {progress}%", overallProgress);
        }

        private void ReportProgress(string status, int percentComplete)
        {
            ProgressChanged?.Invoke(this, new RecordingProgress
            {
                Status = status,
                PercentComplete = percentComplete,
                Elapsed = _processingStopwatch.Elapsed
            });
        }

        public void Dispose()
        {
            // Unsubscribe from events
            if (_audioRecorder != null)
            {
                _audioRecorder.AudioFileReady -= OnAudioFileReady;
                _audioRecorder.RecordingError -= OnRecordingError;
            }

            if (_whisperService != null)
            {
                _whisperService.TranscriptionError -= OnTranscriptionError;
                _whisperService.ProgressChanged -= OnWhisperProgressChanged;
            }

            _cancellationTokenSource?.Dispose();
        }
    }
}
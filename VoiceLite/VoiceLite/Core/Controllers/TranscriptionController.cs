using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VoiceLite.Core.Interfaces.Controllers;
using VoiceLite.Core.Interfaces.Services;
using VoiceLite.Core.Interfaces.Features;
using VoiceLite.Services;

namespace VoiceLite.Core.Controllers
{
    /// <summary>
    /// Manages batch transcription operations and advanced workflows
    /// </summary>
    public class TranscriptionController : ITranscriptionController
    {
        private readonly IWhisperService _whisperService;
        private readonly IErrorLogger _errorLogger;
        private readonly IProFeatureService _proFeatureService;
        private readonly ISettingsService _settingsService;

        private readonly ConcurrentDictionary<string, TranscriptionStatistics> _statistics;
        private readonly SemaphoreSlim _batchSemaphore;
        private readonly int _maxConcurrentTranscriptions = 2;

        // Events
        public event EventHandler<BatchProgressEventArgs>? BatchItemCompleted;

        public TranscriptionController(
            IWhisperService whisperService,
            IErrorLogger errorLogger,
            IProFeatureService proFeatureService,
            ISettingsService settingsService)
        {
            _whisperService = whisperService ?? throw new ArgumentNullException(nameof(whisperService));
            _errorLogger = errorLogger ?? throw new ArgumentNullException(nameof(errorLogger));
            _proFeatureService = proFeatureService ?? throw new ArgumentNullException(nameof(proFeatureService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            _statistics = new ConcurrentDictionary<string, TranscriptionStatistics>();
            _batchSemaphore = new SemaphoreSlim(_maxConcurrentTranscriptions, _maxConcurrentTranscriptions);
        }

        /// <summary>
        /// Processes multiple audio files for transcription
        /// </summary>
        public async Task<IEnumerable<TranscriptionResult>> BatchTranscribeAsync(
            IEnumerable<string> audioFiles,
            string modelPath)
        {
            var fileList = audioFiles.ToList();
            var results = new ConcurrentBag<TranscriptionResult>();
            var currentItem = 0;
            var totalItems = fileList.Count;

            // Process files with controlled concurrency
            var tasks = fileList.Select(async audioFile =>
            {
                await _batchSemaphore.WaitAsync();
                try
                {
                    var itemNumber = Interlocked.Increment(ref currentItem);
                    var result = await TranscribeSingleFileAsync(audioFile, modelPath);

                    results.Add(result);

                    // Report progress
                    BatchItemCompleted?.Invoke(this, new BatchProgressEventArgs
                    {
                        CurrentItem = itemNumber,
                        TotalItems = totalItems,
                        CurrentFile = audioFile,
                        Result = result
                    });

                    UpdateStatistics(result);
                    return result;
                }
                finally
                {
                    _batchSemaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            return results.OrderBy(r => r.AudioFilePath);
        }

        /// <summary>
        /// Retries a failed transcription with exponential backoff
        /// </summary>
        public async Task<TranscriptionResult> RetryTranscriptionAsync(
            string audioFilePath,
            string modelPath,
            int maxRetries = 3)
        {
            if (!File.Exists(audioFilePath))
            {
                return new TranscriptionResult
                {
                    Success = false,
                    Error = $"Audio file not found: {audioFilePath}",
                    AudioFilePath = audioFilePath
                };
            }

            TranscriptionResult? lastResult = null;
            var retryDelay = TimeSpan.FromSeconds(1);

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                if (attempt > 0)
                {
                    _errorLogger.LogInfo($"Retrying transcription, attempt {attempt} of {maxRetries}");
                    await Task.Delay(retryDelay);
                    retryDelay = TimeSpan.FromSeconds(retryDelay.TotalSeconds * 2); // Exponential backoff
                }

                lastResult = await TranscribeSingleFileAsync(audioFilePath, modelPath);

                if (lastResult.Success)
                {
                    if (attempt > 0)
                    {
                        _errorLogger.LogInfo($"Transcription succeeded after {attempt} retries");
                    }
                    UpdateStatistics(lastResult);
                    return lastResult;
                }
            }

            // All retries failed
            _errorLogger.LogError(
                new Exception($"Transcription failed after {maxRetries} retries: {lastResult?.Error}"),
                "RetryTranscriptionAsync");

            UpdateStatistics(lastResult!);
            return lastResult!;
        }

        /// <summary>
        /// Gets the recommended model based on audio characteristics
        /// </summary>
        public async Task<string> GetRecommendedModelAsync(string audioFilePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var fileInfo = new FileInfo(audioFilePath);
                    var fileSizeInMB = fileInfo.Length / (1024.0 * 1024.0);

                    // Get available models based on Pro status
                    var availableModels = _proFeatureService.GetAvailableModels();

                    // Simple heuristic based on file size and available models
                    if (fileSizeInMB < 1)
                    {
                        // Short audio - use fastest model
                        return availableModels.Contains("tiny") ? "tiny" : availableModels.First();
                    }
                    else if (fileSizeInMB < 5)
                    {
                        // Medium length - balance speed and accuracy
                        if (_proFeatureService.IsProUser)
                        {
                            return availableModels.Contains("base") ? "base" : "small";
                        }
                        return "tiny";
                    }
                    else
                    {
                        // Long audio - prioritize accuracy
                        if (_proFeatureService.IsProUser)
                        {
                            return availableModels.Contains("small") ? "small" : "medium";
                        }
                        return "tiny";
                    }
                }
                catch (Exception ex)
                {
                    _errorLogger.LogError(ex, "GetRecommendedModelAsync failed");
                    return _settingsService.SelectedModel;
                }
            });
        }

        /// <summary>
        /// Validates that all transcription prerequisites are met
        /// </summary>
        public async Task<ValidationResult> ValidateTranscriptionSetupAsync()
        {
            var result = new ValidationResult { IsValid = true };

            await Task.Run(() =>
            {
                try
                {
                    // Check Whisper executable
                    if (!_whisperService.ValidateWhisperExecutable())
                    {
                        result.Issues.Add("Whisper executable not found or invalid");
                        result.IsValid = false;
                    }

                    // Check Whisper version
                    var version = _whisperService.GetWhisperVersion();
                    if (version == "Unknown")
                    {
                        result.Warnings.Add("Could not determine Whisper version");
                    }
                    else
                    {
                        _errorLogger.LogInfo($"Whisper version: {version}");
                    }

                    // Check selected model exists
                    var modelPath = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "whisper",
                        $"ggml-{_settingsService.SelectedModel}.bin");

                    if (!File.Exists(modelPath))
                    {
                        result.Issues.Add($"Selected model not found: {_settingsService.SelectedModel}");
                        result.IsValid = false;
                    }

                    // Check temp directory is accessible
                    var tempDir = Path.Combine(Path.GetTempPath(), "VoiceLite");
                    if (!Directory.Exists(tempDir))
                    {
                        try
                        {
                            Directory.CreateDirectory(tempDir);
                        }
                        catch
                        {
                            result.Issues.Add("Cannot create temp directory");
                            result.IsValid = false;
                        }
                    }

                    // Check disk space (warn if < 500MB free)
                    var driveInfo = new DriveInfo(Path.GetPathRoot(tempDir) ?? "C:");
                    var freeSpaceInMB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0);
                    if (freeSpaceInMB < 500)
                    {
                        result.Warnings.Add($"Low disk space: {freeSpaceInMB:F1} MB free");
                    }
                }
                catch (Exception ex)
                {
                    result.Issues.Add($"Validation error: {ex.Message}");
                    result.IsValid = false;
                }
            });

            return result;
        }

        /// <summary>
        /// Cleans up old temporary audio files
        /// </summary>
        public async Task<int> CleanupTemporaryFilesAsync(TimeSpan olderThan)
        {
            return await Task.Run(() =>
            {
                int filesDeleted = 0;

                try
                {
                    var tempDir = Path.Combine(Path.GetTempPath(), "VoiceLite", "audio");
                    if (!Directory.Exists(tempDir))
                    {
                        return 0;
                    }

                    var cutoffTime = DateTime.Now - olderThan;
                    var files = Directory.GetFiles(tempDir, "*.wav")
                        .Select(f => new FileInfo(f))
                        .Where(fi => fi.CreationTime < cutoffTime);

                    foreach (var file in files)
                    {
                        try
                        {
                            file.Delete();
                            filesDeleted++;
                        }
                        catch (Exception ex)
                        {
                            _errorLogger.LogWarning($"Could not delete file {file.Name}: {ex.Message}");
                        }
                    }

                    if (filesDeleted > 0)
                    {
                        _errorLogger.LogInfo($"Cleaned up {filesDeleted} temporary audio files");
                    }
                }
                catch (Exception ex)
                {
                    _errorLogger.LogError(ex, "CleanupTemporaryFilesAsync failed");
                }

                return filesDeleted;
            });
        }

        /// <summary>
        /// Cancels the currently running transcription, if any
        /// </summary>
        public async Task<bool> CancelCurrentTranscriptionAsync()
        {
            try
            {
                _errorLogger.LogInfo("CancelCurrentTranscriptionAsync: Attempting to cancel transcription");
                _whisperService.CancelTranscription();

                // Give async TranscribeAsync time to observe cancellation token before shutdown continues
                await Task.Delay(100);
                return true;
            }
            catch (Exception ex)
            {
                _errorLogger.LogError(ex, "CancelCurrentTranscriptionAsync failed");
                return false;
            }
        }

        /// <summary>
        /// Gets transcription performance statistics
        /// </summary>
        public TranscriptionStatistics GetStatistics()
        {
            // Aggregate statistics from all models
            var allStats = _statistics.Values.ToList();

            if (!allStats.Any())
            {
                return new TranscriptionStatistics
                {
                    ModelUsageCount = new Dictionary<string, int>()
                };
            }

            return new TranscriptionStatistics
            {
                TotalTranscriptions = allStats.Sum(s => s.TotalTranscriptions),
                SuccessfulTranscriptions = allStats.Sum(s => s.SuccessfulTranscriptions),
                FailedTranscriptions = allStats.Sum(s => s.FailedTranscriptions),
                TotalProcessingTime = TimeSpan.FromMilliseconds(
                    allStats.Sum(s => s.TotalProcessingTime.TotalMilliseconds)),
                AverageProcessingTime = allStats.Any()
                    ? TimeSpan.FromMilliseconds(
                        allStats.Average(s => s.AverageProcessingTime.TotalMilliseconds))
                    : TimeSpan.Zero,
                LastTranscription = allStats.Max(s => s.LastTranscription),
                ModelUsageCount = allStats
                    .SelectMany(s => s.ModelUsageCount)
                    .GroupBy(kvp => kvp.Key)
                    .ToDictionary(g => g.Key, g => g.Sum(kvp => kvp.Value))
            };
        }

        private async Task<TranscriptionResult> TranscribeSingleFileAsync(
            string audioFilePath,
            string modelPath)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new TranscriptionResult
            {
                AudioFilePath = audioFilePath,
                ModelUsed = Path.GetFileNameWithoutExtension(modelPath)
            };

            try
            {
                var text = await _whisperService.TranscribeAsync(audioFilePath, modelPath);

                result.Success = !string.IsNullOrWhiteSpace(text);
                result.Text = text ?? string.Empty;

                if (!result.Success)
                {
                    result.Error = "Transcription returned empty text";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                _errorLogger.LogError(ex, $"Failed to transcribe {Path.GetFileName(audioFilePath)}");
            }
            finally
            {
                stopwatch.Stop();
                result.ProcessingTime = stopwatch.Elapsed;
            }

            return result;
        }

        private void UpdateStatistics(TranscriptionResult result)
        {
            var modelKey = result.ModelUsed ?? "unknown";

            var stats = _statistics.AddOrUpdate(modelKey,
                key => new TranscriptionStatistics
                {
                    TotalTranscriptions = 1,
                    SuccessfulTranscriptions = result.Success ? 1 : 0,
                    FailedTranscriptions = result.Success ? 0 : 1,
                    TotalProcessingTime = result.ProcessingTime,
                    AverageProcessingTime = result.ProcessingTime,
                    LastTranscription = DateTime.Now,
                    ModelUsageCount = new Dictionary<string, int> { { key, 1 } }
                },
                (key, existing) =>
                {
                    existing.TotalTranscriptions++;

                    if (result.Success)
                        existing.SuccessfulTranscriptions++;
                    else
                        existing.FailedTranscriptions++;

                    existing.TotalProcessingTime += result.ProcessingTime;
                    existing.AverageProcessingTime = TimeSpan.FromMilliseconds(
                        existing.TotalProcessingTime.TotalMilliseconds / existing.TotalTranscriptions);
                    existing.LastTranscription = DateTime.Now;

                    if (!existing.ModelUsageCount.ContainsKey(key))
                        existing.ModelUsageCount[key] = 0;
                    existing.ModelUsageCount[key]++;

                    return existing;
                });
        }
    }
}
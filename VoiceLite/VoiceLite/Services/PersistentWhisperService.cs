using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;
using VoiceLite.Models;
using VoiceLite.Core.Interfaces.Features;

namespace VoiceLite.Services
{
    public class PersistentWhisperService : IDisposable
    {
        private readonly Settings settings;
        private readonly string baseDir;
        private readonly ModelResolverService modelResolver;
        private readonly SemaphoreSlim transcriptionSemaphore = new(1, 1);
        private readonly CancellationTokenSource transcriptionCts = new();

        private WhisperFactory? whisperFactory;
        private string? currentModelPath;
        private readonly object factoryLock = new();

        private volatile bool isDisposed = false;
        private volatile bool isProcessing = false;

        public bool IsProcessing => isProcessing;
        public event EventHandler<string>? TranscriptionComplete;
        public event EventHandler<Exception>? TranscriptionError;
        public event EventHandler<int>? ProgressChanged;

        public PersistentWhisperService(Settings settings, ModelResolverService? modelResolver = null, IProFeatureService? proFeatureService = null)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            baseDir = AppDomain.CurrentDomain.BaseDirectory;

            this.modelResolver = modelResolver ?? new ModelResolverService(baseDir, proFeatureService);

            // Background-load the model so first transcription is fast
            var modelPath = this.modelResolver.ResolveModelPath(settings.WhisperModel);
            _ = Task.Run(() =>
            {
                try
                {
                    EnsureFactoryLoaded(modelPath);
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("PersistentWhisperService.Warmup", ex);
                }
            });
        }

        private void EnsureFactoryLoaded(string modelPath)
        {
            lock (factoryLock)
            {
                if (currentModelPath == modelPath && whisperFactory != null)
                    return;

                // Dispose old factory if switching models
                if (whisperFactory != null)
                {
                    ErrorLogger.LogMessage($"Switching model from {Path.GetFileName(currentModelPath)} to {Path.GetFileName(modelPath)}");
                    try { whisperFactory.Dispose(); } catch { }
                    whisperFactory = null;
                }

                ErrorLogger.LogMessage($"Loading Whisper model: {Path.GetFileName(modelPath)}");
                whisperFactory = WhisperFactory.FromPath(modelPath);
                currentModelPath = modelPath;
                ErrorLogger.LogMessage("Whisper model loaded successfully");
            }
        }

        private WhisperProcessor BuildProcessor(WhisperFactory factory)
        {
            var presetConfig = WhisperPresetConfig.GetPresetConfig(settings.TranscriptionPreset);
            var language = SanitizeLanguageCode(settings.Language);

            var builder = factory.CreateBuilder();

            // Set sampling strategy (don't chain from return value — it returns IWhisperSamplingStrategyBuilder)
            if (presetConfig.BeamSize <= 1)
            {
                builder.WithGreedySamplingStrategy();
            }
            else
            {
                builder.WithBeamSearchSamplingStrategy();
            }

            // Common configuration
            builder.WithThreads(4)
                   .WithEntropyThreshold(presetConfig.EntropyThreshold > 0 ? (float)presetConfig.EntropyThreshold : 2.4f)
                   .WithMaxLastTextTokens(presetConfig.MaxContext > 0 ? presetConfig.MaxContext : 224);

            // Language
            if (language == "auto")
                builder.WithLanguageDetection();
            else
                builder.WithLanguage(language);

            // Temperature fallback
            if (presetConfig.UseFallback)
            {
                builder.WithTemperature(0.0f)
                       .WithTemperatureInc(0.4f);
            }
            else
            {
                builder.WithTemperature(0.0f)
                       .WithTemperatureInc(0.0f); // No fallback
            }

            // Initial prompt for vocabulary guidance
            var initialPrompt = "Transcribe the following dictation with proper capitalization and punctuation. " +
                               "Common terms: VoiceLite, GitHub, JavaScript, TypeScript, Python, C#, .NET, React, " +
                               "Node.js, API, JSON, SQL, database, function, variable, component, repository.";
            builder.WithPrompt(initialPrompt);

            return builder.Build();
        }

        public async Task<string> TranscribeFromMemoryAsync(byte[] audioData)
        {
            ErrorLogger.LogMessage($"PersistentWhisperService.TranscribeFromMemoryAsync called with {audioData.Length} bytes");

            if (audioData.Length < 100)
            {
                ErrorLogger.LogMessage("TranscribeFromMemoryAsync: Skipping empty audio data");
                return string.Empty;
            }

            using var stream = new MemoryStream(audioData);
            return await TranscribeFromStreamAsync(stream);
        }

        // Backward compatibility overload
        public async Task<string> TranscribeAsync(string audioFilePath)
        {
            return await TranscribeAsync(audioFilePath, modelResolver.ResolveModelPath(settings.WhisperModel));
        }

        public async Task<string> TranscribeAsync(string audioFilePath, string modelPath)
        {
            if (!ValidateAudioFile(audioFilePath))
                return string.Empty;

            using var fileStream = File.OpenRead(audioFilePath);
            return await TranscribeFromStreamAsync(fileStream, modelPath);
        }

        private async Task<string> TranscribeFromStreamAsync(Stream audioStream, string? modelPath = null)
        {
            bool semaphoreAcquired = false;

            try
            {
                await transcriptionSemaphore.WaitAsync(transcriptionCts.Token);
                semaphoreAcquired = true;
                isProcessing = true;
                var startTime = DateTime.Now;

                var effectiveModelPath = modelPath ?? modelResolver.ResolveModelPath(settings.WhisperModel);
                EnsureFactoryLoaded(effectiveModelPath);

                ErrorLogger.LogWarning($"Whisper: threads=4, model={Path.GetFileName(effectiveModelPath)}");

                // Create a linked cancellation token with 60-second timeout
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    transcriptionCts.Token, timeoutCts.Token);

                var result = new StringBuilder(4096);

                WhisperFactory factory;
                lock (factoryLock)
                {
                    factory = whisperFactory ?? throw new InvalidOperationException("Whisper model not loaded");
                }

                using var processor = BuildProcessor(factory);

                await foreach (var segment in processor.ProcessAsync(audioStream, linkedCts.Token))
                {
                    if (!string.IsNullOrWhiteSpace(segment.Text))
                    {
                        result.Append(segment.Text);
                    }
                }

                var rawResult = result.ToString().Trim();

                // Apply text post-processing
                var finalResult = rawResult;
                if (!string.IsNullOrWhiteSpace(rawResult))
                {
                    try
                    {
                        finalResult = TextPostProcessor.Process(rawResult,
                            enablePunctuation: true,
                            enableCapitalization: true);

                        if (finalResult != rawResult)
                        {
                            ErrorLogger.LogDebug($"Text post-processing applied: '{rawResult}' -> '{finalResult}'");
                        }
                    }
                    catch (Exception postProcessEx)
                    {
                        ErrorLogger.LogError("Text post-processing failed, using raw output", postProcessEx);
                        finalResult = rawResult;
                    }
                }

                var totalTime = DateTime.Now - startTime;
                ErrorLogger.LogWarning($"Transcription completed in {totalTime.TotalMilliseconds:F0}ms, result length: {finalResult.Length} chars");
                ErrorLogger.LogWarning($"Transcription result: '{finalResult.Substring(0, Math.Min(finalResult.Length, 200))}'");

                isProcessing = false;
                return finalResult;
            }
            catch (OperationCanceledException) when (!transcriptionCts.IsCancellationRequested)
            {
                // Timeout (not user cancellation)
                isProcessing = false;
                throw new TimeoutException(
                    "Transcription timed out after 60 seconds. Please try speaking less or using a smaller model.");
            }
            catch (OperationCanceledException)
            {
                isProcessing = false;
                throw;
            }
            catch (Exception ex)
            {
                isProcessing = false;
                ErrorLogger.LogError("PersistentWhisperService.TranscribeAsync", ex);
                throw;
            }
            finally
            {
                if (semaphoreAcquired)
                {
                    try
                    {
                        transcriptionSemaphore.Release();
                    }
                    catch (ObjectDisposedException) { }
                    catch (SemaphoreFullException)
                    {
                        ErrorLogger.LogWarning("Attempted to release semaphore that wasn't acquired");
                    }
                }
            }
        }

        private string? ResolveVADModelPath()
        {
            const string vadModelFile = "ggml-silero-vad.bin";

            var bundledPath = Path.Combine(baseDir, "whisper", vadModelFile);
            if (File.Exists(bundledPath))
            {
                ErrorLogger.LogMessage($"VAD model found (bundled): {bundledPath}");
                return bundledPath;
            }

            var basePath = Path.Combine(baseDir, vadModelFile);
            if (File.Exists(basePath))
            {
                ErrorLogger.LogMessage($"VAD model found (base dir): {basePath}");
                return basePath;
            }

            var localDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoiceLite",
                "whisper",
                vadModelFile
            );
            if (File.Exists(localDataPath))
            {
                ErrorLogger.LogMessage($"VAD model found (local data): {localDataPath}");
                return localDataPath;
            }

            ErrorLogger.LogWarning($"VAD model '{vadModelFile}' not found. Checked locations:\n" +
                                 $"1. {bundledPath}\n" +
                                 $"2. {basePath}\n" +
                                 $"3. {localDataPath}");
            return null;
        }

        private string SanitizeLanguageCode(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
                return "en";

            var normalized = languageCode.Trim().ToLower();

            if (normalized == "auto")
                return "auto";

            if (normalized.Length != 2 || !normalized.All(char.IsLower))
            {
                string sanitized = new string(languageCode.Where(c => !char.IsControl(c)).ToArray());
                if (sanitized.Length > 20) sanitized = sanitized.Substring(0, 20) + "...";
                ErrorLogger.LogWarning($"Invalid language code '{sanitized}', defaulting to 'en'");
                return "en";
            }

            return normalized;
        }

        private bool ValidateAudioFile(string audioFilePath)
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(PersistentWhisperService));

            if (!File.Exists(audioFilePath))
                throw new FileNotFoundException($"Audio file not found: {audioFilePath}");

            var fileInfo = new FileInfo(audioFilePath);
            if (fileInfo.Length < 100)
            {
                ErrorLogger.LogMessage($"TranscribeAsync: Skipping empty audio file - {audioFilePath} ({fileInfo.Length} bytes)");
                return false;
            }

            return true;
        }

        public void CancelTranscription()
        {
            transcriptionCts?.Cancel();
        }

        public bool ValidateWhisperSetup()
        {
            try
            {
                var modelPath = modelResolver.ResolveModelPath(settings.WhisperModel);
                if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
                {
                    TranscriptionError?.Invoke(this, new FileNotFoundException("Whisper model not found"));
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                TranscriptionError?.Invoke(this, ex);
                return false;
            }
        }

        public string GetWhisperVersion()
        {
            try
            {
                return $"Whisper.net {typeof(WhisperFactory).Assembly.GetName().Version}";
            }
            catch
            {
                return "Whisper.net (version unknown)";
            }
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;

            try { transcriptionCts.Cancel(); }
            catch (Exception ex) { ErrorLogger.LogDebug($"CancellationTokenSource.Cancel failed: {ex.Message}"); }

            lock (factoryLock)
            {
                whisperFactory?.Dispose();
                whisperFactory = null;
                currentModelPath = null;
            }

            transcriptionSemaphore.SafeDispose();
            transcriptionCts.SafeDispose();
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SherpaOnnx;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    /// <summary>
    /// In-process ASR service backed by Sherpa-ONNX + Parakeet TDT v3.
    /// (Named PersistentWhisperService until the 2026-07-17 vocabulary rename.)
    /// </summary>
    public class TranscriptionService : IDisposable
    {
        private readonly Settings settings;
        private readonly string baseDir;
        private readonly ModelResolverService modelResolver;
        private readonly IProFeatureService proFeatureService;
        private readonly SemaphoreSlim transcriptionSemaphore = new(1, 1);
        private CancellationTokenSource transcriptionCts = new();
        private readonly object ctsLock = new();

        private OfflineRecognizer? recognizer;
        private string? currentModelDir;
        // Preset (Speed/Balanced/Accuracy) is baked into OfflineRecognizer at load time,
        // so it is part of the reload key — otherwise preset changes are silent no-ops
        // until restart (single model means modelDir alone never changes).
        private TranscriptionPreset? currentPreset;
        private readonly object recognizerLock = new();

        // Test observability: how many times a recognizer has been built.
        internal int RecognizerLoadCount { get; private set; }

        // Custom Dictionary is the first real Pro feature: the Settings tab is Pro-gated,
        // and application at transcription time must match — Free tier gets NO dictionary.
        // (Before 2026-07-18 the gate was cosmetic: the tab was hidden but entries were
        // applied for everyone — HEALTH.md "Custom Dictionary 'Pro feature' isn't gated".)
        // Internal for test observability; the tier check itself lives in ProFeatureService.
        internal IReadOnlyList<CustomDictionaryEntry>? EffectiveCustomDictionary =>
            proFeatureService.IsProUser ? settings.CustomDictionary : null;

        private volatile bool isDisposed = false;
        private volatile bool isProcessing = false;

        public bool IsProcessing => isProcessing;
        public event EventHandler<Exception>? TranscriptionError;

        public TranscriptionService(Settings settings, ModelResolverService? modelResolver = null, ProFeatureService? proFeatureService = null)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            baseDir = AppDomain.CurrentDomain.BaseDirectory;
            this.modelResolver = modelResolver ?? new ModelResolverService(baseDir, proFeatureService);
            // Same tier check the Settings UI uses to show/hide the Custom Dictionary tab
            // (ProFeatureService.IsProUser reads settings.IsProLicense).
            this.proFeatureService = proFeatureService ?? new ProFeatureService(settings);

            // Background-load the model so the first transcription is fast.
            // Errors here are logged but non-fatal — the first transcribe call will retry.
            // Runs under transcriptionSemaphore: a late-scheduled warm-up must not observe a
            // changed preset and dispose/rebuild the recognizer while a Decode is in flight.
            _ = Task.Run(async () =>
            {
                bool semaphoreAcquired = false;
                try
                {
                    try
                    {
                        await transcriptionSemaphore.WaitAsync();
                        semaphoreAcquired = true;
                    }
                    catch (ObjectDisposedException)
                    {
                        return; // Disposed during startup — nothing to warm up.
                    }

                    if (isDisposed)
                        return;

                    var modelDir = this.modelResolver.ResolveModelPath();
                    EnsureRecognizerLoaded(modelDir);
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("TranscriptionService.Warmup", ex);
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
            });
        }

        private void EnsureRecognizerLoaded(string modelDir)
        {
            lock (recognizerLock)
            {
                // Re-check under the lock: the background warm-up task can race Dispose().
                // Without this, warm-up could assign a fresh recognizer AFTER Dispose ran,
                // leaking the native OfflineRecognizer for the process lifetime.
                if (isDisposed)
                    return;

                var preset = settings.TranscriptionPreset;

                if (currentModelDir == modelDir && currentPreset == preset && recognizer != null)
                    return;

                if (recognizer != null)
                {
                    ErrorLogger.LogMessage($"Reloading Parakeet recognizer: dir {currentModelDir} -> {modelDir}, preset {currentPreset} -> {preset}");
                    try { recognizer.Dispose(); }
                    catch (Exception ex) { ErrorLogger.LogDebug($"OfflineRecognizer disposal failed during switch: {ex.Message}"); }
                    recognizer = null;
                }

                var encoder = Path.Combine(modelDir, "encoder.int8.onnx");
                var decoder = Path.Combine(modelDir, "decoder.int8.onnx");
                var joiner = Path.Combine(modelDir, "joiner.int8.onnx");
                var tokens = Path.Combine(modelDir, "tokens.txt");

                foreach (var f in new[] { encoder, decoder, joiner, tokens })
                {
                    if (!File.Exists(f))
                        throw new FileNotFoundException($"Parakeet model file missing: {f}");
                }

                var presetConfig = TranscriptionPresetConfig.GetPresetConfig(preset);

                var config = new OfflineRecognizerConfig();
                config.FeatConfig.SampleRate = 16000;
                config.FeatConfig.FeatureDim = 80;
                config.ModelConfig.Tokens = tokens;
                config.ModelConfig.Transducer.Encoder = encoder;
                config.ModelConfig.Transducer.Decoder = decoder;
                config.ModelConfig.Transducer.Joiner = joiner;
                config.ModelConfig.NumThreads = 4;
                config.ModelConfig.Provider = "cpu";
                config.ModelConfig.Debug = 0;
                config.DecodingMethod = presetConfig.DecodingMethod;
                config.MaxActivePaths = presetConfig.MaxActivePaths;

                ErrorLogger.LogMessage($"Loading Parakeet model: dir={modelDir}, decoding={config.DecodingMethod}, beam={config.MaxActivePaths}");
                recognizer = new OfflineRecognizer(config);
                currentModelDir = modelDir;
                currentPreset = preset;
                RecognizerLoadCount++;
                ErrorLogger.LogMessage("Parakeet model loaded successfully");
            }
        }

        public async Task<string> TranscribeFromMemoryAsync(byte[] audioData)
        {
            ErrorLogger.LogMessage($"TranscriptionService.TranscribeFromMemoryAsync called with {audioData.Length} bytes");

            if (audioData.Length < 100)
            {
                ErrorLogger.LogMessage("TranscribeFromMemoryAsync: Skipping empty audio data");
                return string.Empty;
            }

            using var stream = new MemoryStream(audioData);
            return await TranscribeFromStreamAsync(stream);
        }

        // Backward compatibility overload.
        public async Task<string> TranscribeAsync(string audioFilePath)
        {
            return await TranscribeAsync(audioFilePath, modelResolver.ResolveModelPath());
        }

        public async Task<string> TranscribeAsync(string audioFilePath, string modelDir)
        {
            if (!ValidateAudioFile(audioFilePath))
                return string.Empty;

            using var fileStream = File.OpenRead(audioFilePath);
            return await TranscribeFromStreamAsync(fileStream, modelDir);
        }

        private async Task<string> TranscribeFromStreamAsync(Stream audioStream, string? modelDir = null)
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(TranscriptionService));

            bool semaphoreAcquired = false;
            CancellationToken cancellationToken;
            lock (ctsLock)
            {
                cancellationToken = transcriptionCts.Token;
            }

            try
            {
                // ConfigureAwait(false) on every await in this method: the finally block's
                // semaphore Release must NOT be a dispatcher continuation, or Dispose()
                // blocking the dispatcher in Wait() deadlocks against it. This method
                // touches no UI state — MainWindow re-marshals results itself.
                await transcriptionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                semaphoreAcquired = true;
                isProcessing = true;
                var startTime = DateTime.Now;

                var effectiveModelDir = modelDir ?? modelResolver.ResolveModelPath();
                // Model load/rebuild is seconds of blocking native work (esp. after a preset
                // change forces a rebuild) — keep it off the caller's thread, which is the UI
                // thread via OnAudioFileReady. Still under transcriptionSemaphore, so ordering
                // relative to Decode/Dispose is unchanged.
                await Task.Run(() => EnsureRecognizerLoaded(effectiveModelDir), cancellationToken).ConfigureAwait(false);

                ErrorLogger.LogWarning($"Parakeet: dir={Path.GetFileName(effectiveModelDir)}");

                // 120s timeout — generous for long-form on slow CPUs.
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, timeoutCts.Token);

                // Decode WAV → float[] PCM @ 16kHz. AudioRecorder writes 16kHz/16-bit/mono;
                // ToSampleProvider() handles bit-depth conversion to float.
                // Runs in Task.Run — decoding a long recording is CPU work that would
                // otherwise block the UI thread.
                float[] samples = await Task.Run(() =>
                {
                    using var wavReader = new WaveFileReader(audioStream);
                    if (wavReader.WaveFormat.SampleRate != 16000)
                    {
                        ErrorLogger.LogWarning($"Unexpected sample rate {wavReader.WaveFormat.SampleRate}, Parakeet expects 16000");
                    }
                    ISampleProvider provider = wavReader.ToSampleProvider();
                    if (wavReader.WaveFormat.Channels > 1)
                    {
                        // AudioRecorder records mono — this branch is defensive for
                        // any external WAVs fed through the legacy TranscribeAsync(filePath) API.
                        provider = new StereoToMonoSampleProvider(provider);
                    }
                    return ReadAllSamples(provider);
                }, linkedCts.Token).ConfigureAwait(false);

                linkedCts.Token.ThrowIfCancellationRequested();

                OfflineRecognizer rec;
                lock (recognizerLock)
                {
                    rec = recognizer ?? throw new InvalidOperationException("Parakeet recognizer not loaded");
                }

                // OfflineRecognizer.Decode is blocking native code — wrap in Task.Run
                // so the UI thread (which calls us via async void OnAudioFileReady) doesn't freeze.
                string rawResult = await Task.Run(() =>
                {
                    using var sherpaStream = rec.CreateStream();
                    sherpaStream.AcceptWaveform(16000, samples);
                    rec.Decode(sherpaStream);
                    return sherpaStream.Result.Text ?? string.Empty;
                }, linkedCts.Token).ConfigureAwait(false);

                rawResult = rawResult.Trim();

                var finalResult = rawResult;
                if (!string.IsNullOrWhiteSpace(rawResult))
                {
                    try
                    {
                        finalResult = TextPostProcessor.Process(rawResult,
                            enablePunctuation: true,
                            enableCapitalization: true,
                            customDictionary: EffectiveCustomDictionary);

                        if (finalResult != rawResult)
                        {
                            ErrorLogger.LogDebug("Text post-processing applied");
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

                isProcessing = false;
                return finalResult;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                isProcessing = false;
                throw new TimeoutException(
                    "Transcription timed out after 120 seconds. Try speaking less or switching to the Speed preset.");
            }
            catch (OperationCanceledException)
            {
                isProcessing = false;
                throw;
            }
            catch (Exception ex)
            {
                isProcessing = false;
                ErrorLogger.LogError("TranscriptionService.TranscribeAsync", ex);
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

        private static float[] ReadAllSamples(ISampleProvider provider)
        {
            const int chunkSize = 4096;
            var buffer = new float[chunkSize];
            var collected = new List<float>(16000 * 30); // pre-size for ~30s of speech
            int read;
            while ((read = provider.Read(buffer, 0, chunkSize)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    collected.Add(buffer[i]);
                }
            }
            return collected.ToArray();
        }

        private bool ValidateAudioFile(string audioFilePath)
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(TranscriptionService));

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
            lock (ctsLock)
            {
                transcriptionCts.Cancel();
                transcriptionCts.Dispose();
                transcriptionCts = new CancellationTokenSource();
            }
        }

        public bool ValidateTranscriptionSetup()
        {
            try
            {
                var modelDir = modelResolver.ResolveModelPath();
                if (string.IsNullOrEmpty(modelDir) || !Directory.Exists(modelDir))
                {
                    TranscriptionError?.Invoke(this, new DirectoryNotFoundException("Parakeet model directory not found"));
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

        public string GetEngineVersion()
        {
            try
            {
                return $"Sherpa-ONNX {typeof(OfflineRecognizer).Assembly.GetName().Version} (Parakeet TDT v3)";
            }
            catch
            {
                return "Sherpa-ONNX (version unknown)";
            }
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;

            lock (ctsLock)
            {
                try { transcriptionCts.Cancel(); }
                catch (Exception ex) { ErrorLogger.LogDebug($"CancellationTokenSource.Cancel failed: {ex.Message}"); }
            }

            // In-flight transcriptions hold transcriptionSemaphore across the native Decode
            // call (on a captured ref that holds no lock). Wait (bounded) for them to finish
            // so we don't free the recognizer mid-Decode (native use-after-free). Cancel above
            // already ran, so pending waiters exit instead of starting new work. No Release —
            // the semaphore is disposed below.
            try
            {
                if (!transcriptionSemaphore.Wait(TimeSpan.FromSeconds(5)))
                {
                    ErrorLogger.LogWarning("Dispose: timed out waiting for in-flight transcription; disposing recognizer anyway");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogDebug($"Dispose: semaphore wait failed: {ex.Message}");
            }

            lock (recognizerLock)
            {
                try { recognizer?.Dispose(); }
                catch (Exception ex) { ErrorLogger.LogDebug($"OfflineRecognizer dispose failed: {ex.Message}"); }
                recognizer = null;
                currentModelDir = null;
                currentPreset = null;
            }

            try { transcriptionSemaphore?.Dispose(); }
            catch (Exception ex) { ErrorLogger.LogDebug($"transcriptionSemaphore dispose failed: {ex.Message}"); }
            try { transcriptionCts?.Dispose(); }
            catch (Exception ex) { ErrorLogger.LogDebug($"transcriptionCts dispose failed: {ex.Message}"); }
        }
    }
}

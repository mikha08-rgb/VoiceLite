using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using VoiceLite.Interfaces;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    public class PersistentWhisperService : ITranscriber, IDisposable
    {
        private readonly Settings settings;
        private readonly string baseDir;
        private string? cachedWhisperExePath;
        private string? cachedModelPath;
        private string? dummyAudioPath;
        private volatile bool isWarmedUp = false;
        private readonly SemaphoreSlim transcriptionSemaphore = new(1, 1);
        private readonly CancellationTokenSource disposeCts = new();
        private volatile bool isDisposed = false;
        private static bool _integrityWarningLogged = false;
        public PersistentWhisperService(Settings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // Cache paths at initialization
            cachedWhisperExePath = ResolveWhisperExePath();
            cachedModelPath = ResolveModelPath();

            // Create dummy audio file and start warmup process
            _ = Task.Run(async () =>
            {
                try
                {
                    if (!disposeCts.IsCancellationRequested)
                    {
                        CreateDummyAudioFile();
                        await WarmUpWhisperAsync();
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("PersistentWhisperService.Warmup", ex);
                }
            }, disposeCts.Token);
        }

        private string ResolveWhisperExePath()
        {
            var whisperExePath = Path.Combine(baseDir, "whisper", "whisper.exe");
            if (File.Exists(whisperExePath))
            {
                // Validate integrity before using (warns if hash mismatch but continues)
                ValidateWhisperExecutable(whisperExePath);
                return whisperExePath;
            }

            whisperExePath = Path.Combine(baseDir, "whisper.exe");
            if (File.Exists(whisperExePath))
            {
                ValidateWhisperExecutable(whisperExePath);
                return whisperExePath;
            }

            throw new FileNotFoundException("Whisper.exe not found");
        }

        private bool ValidateWhisperExecutable(string path)
        {
            try
            {
                // Expected SHA256 hash of the official whisper.exe binary (whisper.cpp build)
                // Hash verified: DC58771DF4C4E8FC0602879D5CB9AA9D0FB9CD210D8DF555BA84EB63599FB235
                // File size: 111KB (113,664 bytes)
                // Build date: Jan 5, 2024
                const string EXPECTED_HASH = "DC58771DF4C4E8FC0602879D5CB9AA9D0FB9CD210D8DF555BA84EB63599FB235";

                using var sha256 = System.Security.Cryptography.SHA256.Create();
                using var stream = File.OpenRead(path);
                var hash = sha256.ComputeHash(stream);
                var hashString = BitConverter.ToString(hash).Replace("-", "");

                if (!hashString.Equals(EXPECTED_HASH, StringComparison.OrdinalIgnoreCase))
                {
                    // Integrity check failed - log warning only once per session to reduce log noise
                    if (!_integrityWarningLogged)
                    {
                        ErrorLogger.LogMessage("WARNING: Whisper.exe integrity check failed. Using anyway (fail-open mode).");
                        _integrityWarningLogged = true;
                    }

                    // Log warning but allow execution - fail open to avoid breaking legitimate updates
                    // Users should verify they have the correct whisper.exe from official sources
                    return true; // Changed from false to true - warn but don't block
                }

                // Only log success on first check to avoid spam
                if (!_integrityWarningLogged)
                {
                    ErrorLogger.LogMessage("Whisper.exe integrity check passed");
                }
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to validate whisper.exe integrity", ex);
                // Allow execution on validation error (fail open) to avoid breaking legitimate use
                return true;
            }
        }

        private string ResolveModelPath()
        {
            // Support both short names (tiny, base, small) and full filenames (ggml-tiny.bin)
            var modelFile = settings.WhisperModel switch
            {
                "tiny" => "ggml-tiny.bin",
                "base" => "ggml-base.bin",
                "small" => "ggml-small.bin",
                "medium" => "ggml-medium.bin",
                "large" => "ggml-large-v3.bin",
                _ => settings.WhisperModel.EndsWith(".bin") ? settings.WhisperModel : "ggml-small.bin"
            };

            // Check standard model locations
            var modelPath = Path.Combine(baseDir, "whisper", modelFile);
            if (File.Exists(modelPath))
                return modelPath;

            modelPath = Path.Combine(baseDir, modelFile);
            if (File.Exists(modelPath))
                return modelPath;

            // No fallback - fail fast with clear error message
            // Exception is caught by MainWindow and shown to user with reinstall instructions
            throw new FileNotFoundException(
                $"Whisper model '{modelFile}' not found.\n\n" +
                $"Please reinstall VoiceLite to restore missing files.\n\n" +
                $"Expected location: {modelPath}");
        }

        private void CreateDummyAudioFile()
        {
            try
            {
                // Use AppData temp directory instead of Program Files (avoid permission issues)
                var appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VoiceLite",
                    "temp");
                Directory.CreateDirectory(appDataDir);
                dummyAudioPath = Path.Combine(appDataDir, "dummy_warmup.wav");

                // Create 1 second of silence at 16kHz (whisper's preferred format)
                var sampleRate = 16000;
                var duration = TimeSpan.FromSeconds(1);
                var sampleCount = (int)(sampleRate * duration.TotalSeconds);

                using var writer = new WaveFileWriter(dummyAudioPath, new WaveFormat(sampleRate, 1));
                var silenceBuffer = new float[sampleCount];
                // silenceBuffer is already initialized to zeros (silence)
                writer.WriteSamples(silenceBuffer, 0, silenceBuffer.Length);

                // Dummy audio file created successfully (no logging to reduce noise)
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("CreateDummyAudioFile", ex);
                throw;
            }
        }

        private async Task WarmUpWhisperAsync()
        {
            if (string.IsNullOrEmpty(dummyAudioPath) || !File.Exists(dummyAudioPath))
            {
                ErrorLogger.LogMessage("Dummy audio file not available for warmup");
                return;
            }

            try
            {
                // Warmup process starting (no logging to reduce noise)
                var startTime = DateTime.Now;

                // Use the fastest possible settings for warmup
                var modelPath = cachedModelPath ?? ResolveModelPath();
                var whisperExePath = cachedWhisperExePath ?? ResolveWhisperExePath();

                var arguments = $"-m \"{modelPath}\" " +
                              $"-f \"{dummyAudioPath}\" " +
                              $"--threads {Environment.ProcessorCount} " +
                              $"--no-timestamps --language {settings.Language} " +
                              "--beam-size 1 " +          // Fastest possible
                              "--best-of 1 " +            // Single candidate
                              "--entropy-thold 3.0";

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = whisperExePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = new Process { StartInfo = processStartInfo };
                process.Start();

                // Set high priority for warmup
                // BUG FIX: Check if process is still running before accessing PriorityClass
                try
                {
                    if (!process.HasExited)
                    {
                        process.PriorityClass = ProcessPriorityClass.AboveNormal;
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogMessage($"Failed to set Whisper warmup process priority: {ex.Message}");
                }

                await process.WaitForExitAsync();

                // Warmup completed successfully (no timing logs to reduce noise)
                isWarmedUp = true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("WarmUpWhisperAsync", ex);
            }
        }


        public async Task<string> TranscribeFromMemoryAsync(byte[] audioData)
        {
            ErrorLogger.LogMessage($"PersistentWhisperService.TranscribeFromMemoryAsync called with {audioData.Length} bytes");

            // Save to temporary file for whisper.cpp
            var tempPath = Path.Combine(Path.GetTempPath(), $"whisper_temp_{Guid.NewGuid():N}.wav");
            try
            {
                await File.WriteAllBytesAsync(tempPath, audioData);
                return await TranscribeAsync(tempPath);
            }
            finally
            {
                // Clean up temp file immediately
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch { /* Ignore cleanup errors */ }
            }
        }

        public async Task<string> TranscribeAsync(string audioFilePath)
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(PersistentWhisperService));

            if (!File.Exists(audioFilePath))
                throw new FileNotFoundException($"Audio file not found: {audioFilePath}");

            // Check if file has actual audio data (not just headers)
            var fileInfo = new FileInfo(audioFilePath);
            if (fileInfo.Length < 100)
            {
                ErrorLogger.LogMessage($"TranscribeAsync: Skipping empty audio file - {audioFilePath} ({fileInfo.Length} bytes)");
                return string.Empty;
            }

            // BUG FIX (CRIT-004): Track semaphore acquisition to prevent double-release
            // CRITICAL: WaitAsync must be inside try block to ensure proper cleanup in all exception paths
            bool semaphoreAcquired = false;

            try
            {
                // Use semaphore to ensure only one transcription at a time
                await transcriptionSemaphore.WaitAsync();
                semaphoreAcquired = true;
                // Preprocess audio if needed
                try
                {
                    // BUG-004 FIX: Re-enabled AudioPreprocessor with fixed noise gate
                    // Fixed issues: Lower threshold (0.005 vs 0.02), smooth fade zone, lower max cap (0.05 vs 0.2)
                    // This prevents silencing quiet speech while still removing background noise
                    AudioPreprocessor.ProcessAudioFile(audioFilePath, settings);
                }
                catch (Exception preprocessEx)
                {
                    ErrorLogger.LogError("BUG-004: Preprocessing failed, continuing with unprocessed audio", preprocessEx);
                }
                var startTime = DateTime.Now;

                // If not warmed up yet, do a quick warmup
                if (!isWarmedUp)
                {
                    await WarmUpWhisperAsync();
                }

                var modelPath = cachedModelPath ?? ResolveModelPath();
                var whisperExePath = cachedWhisperExePath ?? ResolveWhisperExePath();

                // Build arguments using user settings for optimal accuracy/speed balance
                // NOTE: --temperature is not supported in this version of whisper.exe (removed)
                var arguments = $"-m \"{modelPath}\" " +
                              $"-f \"{audioFilePath}\" " +
                              $"--no-timestamps --language {settings.Language} " +
                              $"--beam-size {settings.BeamSize} " +
                              $"--best-of {settings.BestOf}";

                // Command prepared (no logging to reduce noise and avoid exposing file paths)

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = whisperExePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = new Process { StartInfo = processStartInfo };
                var outputBuilder = new StringBuilder(4096); // PERFORMANCE: Pre-size for typical Whisper output (1-3KB)
                var errorBuilder = new StringBuilder(512);   // PERFORMANCE: Pre-size for error messages

                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorBuilder.AppendLine(e.Data);
                    }
                };

                process.Start();

                // PERFORMANCE FIX: Use Normal priority instead of High to prevent UI thread starvation
                // On systems with limited cores (2-4), HIGH priority Whisper can starve the UI thread
                // causing the app to appear frozen during transcription
                try
                {
                    process.PriorityClass = ProcessPriorityClass.Normal;
                }
                catch { }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Smart timeout calculation
                var audioInfo = new FileInfo(audioFilePath);

                // Calculate timeout based on multiple factors
                int timeoutSeconds;
                if (!isWarmedUp)
                {
                    // BUG-005 FIX: Increased from 120s to 180s (3 minutes) for first run
                    // On slow systems (4GB RAM, antivirus scanning), 466MB model can take 30-120s to load
                    // Additional headroom prevents false timeouts on resource-constrained systems
                    timeoutSeconds = 180; // 180 seconds (3 minutes) for first run
                    ErrorLogger.LogMessage("BUG-005 FIX: Using extended timeout for first run (180s) - model loading may take time on slow systems");
                }
                else
                {
                    // Calculate based on file size and typical processing speed
                    // Whisper typically processes at 10-50x realtime speed depending on model
                    var estimatedAudioSeconds = audioInfo.Length / 32000.0; // Rough estimate (16kHz, 16-bit)

                    // Use model-specific multiplier
                    var processingMultiplier = settings.WhisperModel switch
                    {
                        "ggml-tiny.bin" => 2.0,    // Very fast
                        "ggml-base.bin" => 3.0,    // Fast
                        "ggml-small.bin" => 5.0,   // Default
                        "ggml-medium.bin" => 10.0, // Slower
                        "ggml-large-v3.bin" => 20.0, // Slowest
                        _ => 5.0
                    };

                    timeoutSeconds = Math.Max(10, (int)(estimatedAudioSeconds * processingMultiplier) + 5);

                    // BUG FIX: Apply user multiplier BEFORE capping, not after
                    // This allows users to increase timeout for large models/files
                    timeoutSeconds = (int)(timeoutSeconds * settings.WhisperTimeoutMultiplier);

                    // Cap at reasonable max (10 minutes) to prevent infinite waits
                    timeoutSeconds = Math.Min(timeoutSeconds, 600); // 10 minutes max
                    timeoutSeconds = Math.Max(1, timeoutSeconds); // Minimum 1 second
                }

                ErrorLogger.LogMessage($"Timeout set to {timeoutSeconds}s for {audioInfo.Length} byte file (multiplier: {settings.WhisperTimeoutMultiplier})");

                bool exited = await Task.Run(() => process.WaitForExit(timeoutSeconds * 1000));

                if (!exited)
                {
                    // Kill the entire process tree to ensure cleanup of any child processes
                    // Without entireProcessTree=true, orphaned whisper.exe processes can remain
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        ErrorLogger.LogMessage($"Transcription timed out after {timeoutSeconds}s - killed process tree");

                        // RESOURCE LEAK FIX: Verify process actually died
                        if (!process.WaitForExit(5000)) // Wait up to 5 seconds
                        {
                            ErrorLogger.LogError("Whisper process refused to die after Kill()", new TimeoutException());

                            // Last resort: Use taskkill /F
                            try
                            {
                                var taskkill = Process.Start(new ProcessStartInfo
                                {
                                    FileName = "taskkill",
                                    Arguments = $"/F /PID {process.Id}",
                                    CreateNoWindow = true,
                                    UseShellExecute = false
                                });
                                taskkill?.WaitForExit(2000);
                                taskkill?.Dispose();
                            }
                            catch (Exception taskkillEx)
                            {
                                ErrorLogger.LogError("taskkill also failed", taskkillEx);
                            }
                        }
                    }
                    catch (Exception killEx)
                    {
                        ErrorLogger.LogError("Failed to kill whisper.exe process", killEx);
                        // Try basic kill as fallback
                        try { process.Kill(); } catch { }
                    }

                    // Check if this is a first-run issue
                    if (!isWarmedUp)
                    {
                        throw new TimeoutException(
                            "First transcription timed out (this is normal on slow systems).\n\n" +
                            "Loading the AI model for the first time can take 30-120 seconds on:\n" +
                            "• Systems with 4GB RAM or less\n" +
                            "• Computers with antivirus actively scanning\n" +
                            "• Older CPUs or HDDs (vs SSDs)\n\n" +
                            "What to try:\n" +
                            "1. Wait a moment and try again (second attempt is usually faster)\n" +
                            "2. Run 'Fix Antivirus Issues' from your desktop to add VoiceLite exclusions\n" +
                            "3. Close other applications to free up RAM\n" +
                            "4. Consider using the Tiny model in Settings (smaller, loads faster)");
                    }
                    else
                    {
                        throw new TimeoutException($"Transcription timed out after {timeoutSeconds} seconds. Please try speaking less or using a smaller model.");
                    }
                }

                // Only log errors, not successful transcriptions (reduce noise)
                if (process.ExitCode != 0)
                {
                    ErrorLogger.LogMessage($"Whisper process failed with exit code: {process.ExitCode}");
                    var error = errorBuilder.ToString();
                    if (!string.IsNullOrEmpty(error) && error.Length < 500)
                    {
                        // Log truncated error to avoid excessive log sizes
                        ErrorLogger.LogMessage($"Whisper error: {error.Substring(0, Math.Min(error.Length, 500))}");
                    }
                    throw new ExternalException($"Whisper process failed with exit code {process.ExitCode}", process.ExitCode);
                }

                var result = outputBuilder.ToString();

                // Clean up the output - remove system messages and empty lines
                var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var cleanedResult = new StringBuilder();
                foreach (var line in lines)
                {
                    // Skip system messages from whisper
                    if (!line.StartsWith("[") && !line.Contains("whisper_") && !line.Contains("ggml_"))
                    {
                        cleanedResult.AppendLine(line.Trim());
                    }
                }

                result = cleanedResult.ToString().Trim();

                // Post-process the transcription
                var customDict = settings.EnableCustomDictionary ? settings.CustomDictionaryEntries : null;
                result = TranscriptionPostProcessor.ProcessTranscription(result, settings.UseEnhancedDictionary, customDict, settings.PostProcessing);

                var totalTime = DateTime.Now - startTime;
                ErrorLogger.LogMessage($"Transcription completed in {totalTime.TotalMilliseconds:F0}ms");

                // PERFORMANCE: Removed redundant post-transcription warmup
                // Background warmup only provides benefit on cold start. After first transcription,
                // OS disk caches are already warm. This was wasting 500-2000ms CPU per transcription.

                return result;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("PersistentWhisperService.TranscribeAsync", ex);
                throw;
            }
            finally
            {
                // BUG FIX (BUG-003): Only release semaphore if we successfully acquired it
                // This prevents SemaphoreFullException if exception occurred before WaitAsync
                if (!isDisposed && semaphoreAcquired)
                {
                    transcriptionSemaphore.Release();
                }
            }
        }

        private void CleanupProcess()
        {
            try
            {
                // Clean up dummy audio file
                if (!string.IsNullOrEmpty(dummyAudioPath) && File.Exists(dummyAudioPath))
                {
                    try
                    {
                        File.Delete(dummyAudioPath);
                    }
                    catch { /* Ignore cleanup errors */ }
                }

                isWarmedUp = false;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("PersistentWhisperService.CleanupProcess", ex);
            }
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;

            // Cancel all background tasks (warmup tasks)
            try
            {
                disposeCts.Cancel();
            }
            catch { /* Ignore cancellation errors */ }

            // Wait briefly for background tasks to complete
            Thread.Sleep(200);

            CleanupProcess();

            // Dispose semaphore safely with a small delay to prevent race conditions
            if (transcriptionSemaphore != null)
            {
                try
                {
                    // Brief wait to ensure any Release() calls have completed
                    Thread.Sleep(100);
                    transcriptionSemaphore.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, ignore
                }
            }

            // Dispose cancellation token source
            try
            {
                disposeCts.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }
        }
    }
}



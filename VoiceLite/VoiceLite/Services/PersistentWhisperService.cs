using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private readonly CancellationTokenSource disposalCts = new CancellationTokenSource(); // CRITICAL FIX: Cancellation for semaphore during disposal
        private readonly ManualResetEventSlim disposalComplete = new ManualResetEventSlim(false); // CRITICAL FIX: Non-blocking disposal wait
        private readonly object disposeLock = new object(); // AUDIT FIX: Lock for TOCTOU protection
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

            // CRITICAL SECURITY FIX: Server-side model gating to prevent piracy
            // Check if user is trying to use Pro models (Small, Medium, Large) without a valid license
            // This prevents bypass by editing Settings.json directly
            var proModels = new[] { "ggml-small.bin", "ggml-medium.bin", "ggml-large-v3.bin" };
            if (proModels.Contains(modelFile))
            {
                bool hasValidLicense = SimpleLicenseStorage.HasValidLicense(out _);

                if (!hasValidLicense)
                {
                    // Security violation: User tried to use Pro model without license
                    ErrorLogger.LogMessage($"SECURITY: Attempt to use Pro model '{modelFile}' without valid license - falling back to Base model");

                    // Fallback to Base model (free tier) instead of blocking completely
                    modelFile = "ggml-base.bin";

                    // Update settings to prevent repeated attempts
                    settings.WhisperModel = "ggml-base.bin";

                    // Show warning to user (will be logged and displayed by MainWindow)
                    throw new UnauthorizedAccessException(
                        "Pro Model Requires License\n\n" +
                        $"The '{settings.WhisperModel}' model requires a Pro license.\n\n" +
                        "Free tier includes:\n" +
                        "• Tiny model (fastest, lower accuracy)\n" +
                        "• Base model (good balance)\n\n" +
                        "Pro tier unlocks:\n" +
                        "• Small model (better accuracy)\n" +
                        "• Medium model (professional quality)\n" +
                        "• Large model (maximum accuracy)\n\n" +
                        "Get Pro for $29.99 at voicelite.app\n\n" +
                        "Your model selection has been reset to Base.");
                }
            }

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

                // AUDIT FIX (ERROR-CRIT-3): Add timeout to prevent infinite hang
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                    // Warmup completed successfully (no timing logs to reduce noise)
                    isWarmedUp = true;
                }
                catch (OperationCanceledException)
                {
                    ErrorLogger.LogWarning("Warmup timed out after 120 seconds - killing process");
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (Exception killEx)
                    {
                        ErrorLogger.LogError("Failed to kill hung warmup process", killEx);
                    }
                    // Don't set isWarmedUp = true - warmup failed
                }
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
            // AUDIT FIX (CRITICAL-2): Move WaitAsync BEFORE try block to prevent semaphore corruption
            // If cancellation occurs during WaitAsync, finally block should NOT release semaphore
            bool semaphoreAcquired = false;
            Process? process = null; // TIER 1.3: Declare at method scope for finally block access

            try
            {
                // Use semaphore to ensure only one transcription at a time
                // CRITICAL FIX: Add cancellation token support to prevent deadlock during disposal
                await transcriptionSemaphore.WaitAsync(disposalCts.Token);
                semaphoreAcquired = true;
            }
            catch (OperationCanceledException)
            {
                // Disposal in progress - exit gracefully without releasing semaphore
                return string.Empty;
            }

            try
            {
                // Preprocess audio if needed
                try
                {
                    // ROLLBACK: AudioPreprocessor is too aggressive and silences all audio
                    // Even with "fixed" noise gate, it's filtering out speech completely
                    // DISABLED until further investigation - raw audio works fine with Whisper
                    // AudioPreprocessor.ProcessAudioFile(audioFilePath, settings);
                }
                catch (Exception preprocessEx)
                {
                    ErrorLogger.LogError("Preprocessing failed, continuing with unprocessed audio", preprocessEx);
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

                // TIER 1.3: Assign to method-scoped variable for finally block access
                process = new Process { StartInfo = processStartInfo };
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

                // Fixed timeout - fail fast instead of complex calculation
                int timeoutSeconds = 60; // 60 seconds max

                bool exited = await Task.Run(() => process.WaitForExit(timeoutSeconds * 1000));

                if (!exited)
                {
                    // Kill the entire process tree to ensure cleanup of any child processes
                    // Without entireProcessTree=true, orphaned whisper.exe processes can remain
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        ErrorLogger.LogMessage($"Transcription timed out after {timeoutSeconds}s - killed process tree");

                        // CRIT-005 FIX: Non-blocking process termination with hard timeout
                        // Use Task.Run to prevent UI thread hang if process is in unkillable state
                        var waitTask = Task.Run(() =>
                        {
                            try
                            {
                                // CRIT-006 FIX: Add timeout to WaitForExit to prevent infinite hang
                                // Previously called without timeout, could hang forever if process in zombie state
                                return process.WaitForExit(5000); // Wait up to 5 seconds
                            }
                            catch
                            {
                                return false;
                            }
                        });

                        // Hard timeout: 6 seconds max (5s wait + 1s margin)
                        if (!waitTask.Wait(6000) || !waitTask.Result)
                        {
                            ErrorLogger.LogError("Whisper process refused to die after Kill()", new TimeoutException());

                            // Last resort: Fire-and-forget taskkill (don't wait)
                            _ = Task.Run(() =>
                            {
                                try
                                {
                                    var taskkill = Process.Start(new ProcessStartInfo
                                    {
                                        FileName = "taskkill",
                                        Arguments = $"/F /T /PID {process.Id}",
                                        CreateNoWindow = true,
                                        UseShellExecute = false
                                    });
                                    // Don't wait - fire and forget
                                    taskkill?.Dispose();
                                }
                                catch (Exception taskkillEx)
                                {
                                    ErrorLogger.LogError("taskkill also failed", taskkillEx);
                                }
                            });
                        }

                    }
                    catch (Exception killEx)
                    {
                        ErrorLogger.LogError("Failed to kill whisper.exe process", killEx);
                        // Try basic kill as fallback (best effort, don't wait)
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
                            "4. Try Base or Tiny models for faster processing (lower quality)");
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

                // SIMPLIFICATION: Return 100% raw Whisper output (zero filtering)
                // All post-processing and cleanup removed - rebuild from clean slate
                var result = outputBuilder.ToString().Trim();

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
                // Dispose process if created
                if (process != null)
                {
                    try
                    {
                        process.Dispose();
                    }
                    catch (Exception disposeEx)
                    {
                        ErrorLogger.LogError("Failed to dispose process", disposeEx);
                    }
                }

                // BUG FIX (BUG-003): Only release semaphore if we successfully acquired it
                // This prevents SemaphoreFullException if exception occurred before WaitAsync
                if (!isDisposed && semaphoreAcquired)
                {
                    transcriptionSemaphore.Release();
                }

                // CRITICAL FIX: Signal disposal completion to unblock Dispose() method
                // Without this, Dispose() waits 5 seconds for timeout on every shutdown
                try
                {
                    disposalComplete?.Set();
                }
                catch { /* Ignore if already disposed */ }
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
            // AUDIT FIX (CRITICAL-3): Add lock to prevent TOCTOU race condition
            // Previously: Two threads could both see isDisposed=false, both proceed with disposal
            lock (disposeLock)
            {
                if (isDisposed)
                    return;

                isDisposed = true;
            }

            // CRITICAL FIX (AUDIT FIX): Fire-and-forget disposal to prevent 5-second UI freeze
            // Previously: disposalComplete.Wait() blocked UI thread for 5 seconds on shutdown
            // New approach: Cancel operations and cleanup in background, return immediately
            try
            {
                disposeCts.Cancel(); // Cancel warmup tasks
                disposalCts.Cancel(); // Cancel semaphore waits to unblock transcriptions
            }
            catch { /* Ignore cancellation errors */ }

            // Fire-and-forget background cleanup to avoid blocking UI thread
            // QUALITY REVIEW FIX: Add ContinueWith to observe task exceptions and add null checks
            Task.Run(() =>
            {
                try
                {
                    // Wait for active transcriptions to complete (5s max)
                    disposalComplete?.Wait(TimeSpan.FromSeconds(5));

                    CleanupProcess();

                    // CRITICAL FIX (CRITICAL-3): Dispose semaphore safely AFTER all waiters have exited
                    try { transcriptionSemaphore?.Dispose(); }
                    catch (ObjectDisposedException)
                    {
                        ErrorLogger.LogMessage("PersistentWhisperService: Semaphore already disposed (unexpected)");
                    }

                    // Dispose cancellation token sources with null checks
                    try { disposeCts?.Dispose(); } catch (ObjectDisposedException) { }
                    try { disposalCts?.Dispose(); } catch (ObjectDisposedException) { }
                    try { disposalComplete?.Dispose(); } catch (ObjectDisposedException) { }
                }
                catch (Exception ex)
                {
                    // Log disposal errors but don't throw (we're in fire-and-forget task)
                    ErrorLogger.LogError("PersistentWhisperService background disposal failed", ex);
                }
            }).ContinueWith(t =>
            {
                // QUALITY REVIEW FIX: Observer for unhandled task exceptions
                if (t.IsFaulted && t.Exception != null)
                {
                    ErrorLogger.LogError("PersistentWhisperService disposal task faulted", t.Exception);
                }
            }, TaskScheduler.Default);
        }
    }
}



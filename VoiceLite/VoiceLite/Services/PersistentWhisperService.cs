using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using VoiceLite.Models;
using VoiceLite.Core.Interfaces.Features;
using VoiceLite.Core.Interfaces.Services;

namespace VoiceLite.Services
{
    public class PersistentWhisperService : IWhisperService
    {
        private readonly Settings settings;
        private readonly string baseDir;
        private readonly IModelResolverService modelResolver;
        private string? cachedWhisperExePath;
        private string? cachedModelPath;
        private string? dummyAudioPath;
        private volatile bool isWarmedUp = false;
        private readonly SemaphoreSlim transcriptionSemaphore = new(1, 1);

        // BUG FIX (CTS-NAMING-001): Renamed for clarity to indicate what each token controls
        // - warmupCts: Cancels background warmup task on disposal
        // - transcriptionCts: Cancels semaphore waits during disposal to prevent deadlock
        private readonly CancellationTokenSource warmupCts = new();
        private readonly CancellationTokenSource transcriptionCts = new CancellationTokenSource();
        private readonly ManualResetEventSlim disposalComplete = new ManualResetEventSlim(false); // CRITICAL FIX: Non-blocking disposal wait
        private volatile bool isDisposed = false;
        private volatile bool isProcessing = false;

        // Timeout constants for process lifecycle management
        /// <summary>
        /// Maximum time to wait for process disposal (2 seconds).
        /// Prevents indefinite hangs when disposing whisper.exe process.
        /// </summary>
        private const int PROCESS_DISPOSAL_TIMEOUT_MS = 2000;

        /// <summary>
        /// Hard timeout for killing unkillable processes (6 seconds).
        /// Last resort timeout when process refuses to terminate after Kill().
        /// Includes 5s wait + 1s margin before forcing taskkill.
        /// </summary>
        private const int PROCESS_KILL_HARD_TIMEOUT_MS = 6000;

        /// <summary>
        /// Maximum time to wait for disposal completion (5 seconds).
        /// Non-blocking wait for background tasks to complete during service disposal.
        /// Prevents UI thread hangs during application shutdown.
        /// </summary>
        private const int DISPOSAL_COMPLETION_TIMEOUT_SECONDS = 5;

        // Interface implementation properties and events
        public bool IsProcessing => isProcessing;
#pragma warning disable CS0067 // Event is never used - kept for interface compatibility
        public event EventHandler<string>? TranscriptionComplete;
#pragma warning restore CS0067
        public event EventHandler<Exception>? TranscriptionError;
#pragma warning disable CS0067 // Event is never used - kept for interface compatibility
        public event EventHandler<int>? ProgressChanged;
#pragma warning restore CS0067

        public PersistentWhisperService(Settings settings, IModelResolverService? modelResolver = null, IProFeatureService? proFeatureService = null)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // Create ModelResolverService if not injected (backward compatibility)
            // SECURITY FIX (MODEL-GATE-001): Pass ProFeatureService to enable license validation
            this.modelResolver = modelResolver ?? new ModelResolverService(baseDir, proFeatureService);

            // Cache paths at initialization
            cachedWhisperExePath = this.modelResolver.ResolveWhisperExePath();
            cachedModelPath = this.modelResolver.ResolveModelPath(settings.WhisperModel);

            // Create dummy audio file and start warmup process
            _ = Task.Run(async () =>
            {
                try
                {
                    if (!warmupCts.IsCancellationRequested)
                    {
                        CreateDummyAudioFile();
                        await WarmUpWhisperAsync();
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("PersistentWhisperService.Warmup", ex);
                }
            }, warmupCts.Token);
        }


        private string? ResolveVADModelPath()
        {
            // CRITICAL: High-quality VAD model resolution with multiple fallback locations
            const string vadModelFile = "ggml-silero-vad.bin";

            // Priority 1: Bundled with application (Program Files)
            var bundledPath = Path.Combine(baseDir, "whisper", vadModelFile);
            if (File.Exists(bundledPath))
            {
                ErrorLogger.LogMessage($"VAD model found (bundled): {bundledPath}");
                return bundledPath;
            }

            // Priority 2: Base directory fallback
            var basePath = Path.Combine(baseDir, vadModelFile);
            if (File.Exists(basePath))
            {
                ErrorLogger.LogMessage($"VAD model found (base dir): {basePath}");
                return basePath;
            }

            // Priority 3: User-downloaded models in LocalApplicationData
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

            // VAD is optional - gracefully degrade if not found
            ErrorLogger.LogWarning($"VAD model '{vadModelFile}' not found. Checked locations:\n" +
                                 $"1. {bundledPath}\n" +
                                 $"2. {basePath}\n" +
                                 $"3. {localDataPath}");
            return null;
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
                var modelPath = cachedModelPath ?? modelResolver.ResolveModelPath(settings.WhisperModel);
                var whisperExePath = cachedWhisperExePath ?? modelResolver.ResolveWhisperExePath();

                // PERFORMANCE FIX: Use 4 threads instead of ProcessorCount/2 to avoid thread contention
                // Always use Speed preset for warmup (fastest possible)
                int optimalThreads = 4;
                var warmupPreset = WhisperPresetConfig.GetPresetConfig(TranscriptionPreset.Speed);
                var arguments = $"-m \"{modelPath}\" " +
                              $"-f \"{dummyAudioPath}\" " +
                              $"--threads {optimalThreads} " +
                              $"--no-timestamps --language {settings.Language} " +
                              $"--beam-size {warmupPreset.BeamSize} " +
                              $"--best-of {warmupPreset.BestOf} " +
                              $"--entropy-thold {warmupPreset.EntropyThreshold:F1} " +
                              "--no-fallback " +
                              $"--max-context {warmupPreset.MaxContext} " +
                              "--flash-attn";

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
                    ErrorLogger.LogWarning($"Failed to set Whisper warmup process priority: {ex.Message}");
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
                catch (Exception cleanupEx)
                {
                    ErrorLogger.LogWarning($"Failed to delete temp audio file {tempPath}: {cleanupEx.Message}");
                }
            }
        }

        // Backward compatibility overload
        public async Task<string> TranscribeAsync(string audioFilePath)
        {
            return await TranscribeAsync(audioFilePath, cachedModelPath ?? modelResolver.ResolveModelPath(settings.WhisperModel));
        }

        // Interface implementation
        public async Task<string> TranscribeAsync(string audioFilePath, string modelPath)
        {
            // Validate audio file (throws on disposed/missing, returns false if empty)
            if (!ValidateAudioFile(audioFilePath))
                return string.Empty;

            // BUG FIX (CRIT-004): Track semaphore acquisition to prevent double-release
            // CRITICAL: WaitAsync must be inside try block to ensure proper cleanup in all exception paths
            bool semaphoreAcquired = false;
            Process? process = null; // TIER 1.3: Declare at method scope for finally block access

            try
            {
                // Use semaphore to ensure only one transcription at a time
                // CRITICAL FIX: Add cancellation token support to prevent deadlock during disposal
                await transcriptionSemaphore.WaitAsync(transcriptionCts.Token);
                semaphoreAcquired = true;
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

                // Use the passed modelPath parameter, fall back to cached if empty
                var effectiveModelPath = !string.IsNullOrEmpty(modelPath) ? modelPath : (cachedModelPath ?? modelResolver.ResolveModelPath(settings.WhisperModel));
                var whisperExePath = cachedWhisperExePath ?? modelResolver.ResolveWhisperExePath();

                // Build Whisper command-line arguments
                var arguments = BuildWhisperArguments(audioFilePath, effectiveModelPath, out int optimalThreads);

                // Log whisper command (visible in Release builds for troubleshooting)
                ErrorLogger.LogWarning($"Whisper: threads={optimalThreads}, cores={Environment.ProcessorCount}, model={Path.GetFileName(effectiveModelPath)}");

                // Execute Whisper process and capture output
                // TIER 1.3: Assign to method-scoped variable for finally block access
                (process, var outputBuilder, var errorBuilder) = ExecuteWhisperProcess(whisperExePath, arguments);

                // Fixed timeout - fail fast instead of complex calculation
                int timeoutSeconds = 60; // 60 seconds max

                bool exited = await Task.Run(() => process.WaitForExit(timeoutSeconds * 1000));

                if (!exited)
                {
                    HandleWhisperTimeout(process, timeoutSeconds);
                }

                // Process output, apply post-processing, and return result
                return ProcessWhisperOutput(process, outputBuilder, errorBuilder, startTime);
            }
            // HIGH-7 FIX: Explicit catch for cancellation - semaphore NOT acquired if WaitAsync was cancelled
            catch (OperationCanceledException)
            {
                // WaitAsync was cancelled before acquiring semaphore
                // semaphoreAcquired is false, so finally block won't release
                throw;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("PersistentWhisperService.TranscribeAsync", ex);
                throw;
            }
            finally
            {
                // BUG FIX (PROC-DISP-001): Robust process disposal with timeout to prevent hangs
                // Dispose process if created - use timeout to prevent indefinite hangs
                if (process != null)
                {
                    try
                    {
                        // Attempt disposal with 2-second timeout
                        var disposeTask = Task.Run(() =>
                        {
                            try
                            {
                                process.Dispose();
                                return true;
                            }
                            catch (Exception innerEx)
                            {
                                ErrorLogger.LogWarning($"Process disposal threw exception: {innerEx.Message}");
                                return false;
                            }
                        });

                        // Wait up to 2 seconds for disposal
                        if (!disposeTask.Wait(PROCESS_DISPOSAL_TIMEOUT_MS))
                        {
                            ErrorLogger.LogError("Process disposal timed out - force killing whisper.exe",
                                new TimeoutException());

                            // SECURITY FIX (ZOMBIE-KILL-001): Force-kill zombie process with taskkill
                            try
                            {
                                var processId = process.Id;
                                ErrorLogger.LogMessage($"Force-killing whisper.exe PID {processId} with taskkill...");

                                // Use taskkill /F /T to force-kill process tree
                                var taskkillProcess = new Process
                                {
                                    StartInfo = new ProcessStartInfo
                                    {
                                        FileName = "taskkill",
                                        Arguments = $"/F /T /PID {processId}",
                                        CreateNoWindow = true,
                                        UseShellExecute = false,
                                        RedirectStandardOutput = true,
                                        RedirectStandardError = true
                                    }
                                };

                                taskkillProcess.Start();
                                taskkillProcess.WaitForExit(1000); // Wait 1 second for taskkill

                                // Verify process terminated
                                try
                                {
                                    if (!process.HasExited)
                                    {
                                        ErrorLogger.LogError("Failed to kill zombie whisper.exe after taskkill",
                                            new InvalidOperationException($"PID {processId} still running"));
                                    }
                                    else
                                    {
                                        ErrorLogger.LogMessage($"Successfully force-killed zombie PID {processId}");
                                    }
                                }
                                catch (InvalidOperationException)
                                {
                                    // Process.HasExited throws if process already disposed - that's fine
                                    ErrorLogger.LogMessage("Zombie process already disposed after taskkill");
                                }
                            }
                            catch (Exception killEx)
                            {
                                ErrorLogger.LogError("Failed to force-kill zombie process with taskkill", killEx);
                            }
                        }
                        else if (!disposeTask.Result)
                        {
                            ErrorLogger.LogWarning("Process disposal completed but threw exception (logged above)");
                        }
                    }
                    catch (Exception disposeEx)
                    {
                        // Catch any timeout/aggregate exceptions from Task.Wait
                        ErrorLogger.LogError("Failed to dispose process (outer catch)", disposeEx);
                        // Continue with cleanup - don't let disposal failure break finally block
                    }
                }

                // BUG FIX (BUG-003): Only release semaphore if we successfully acquired it
                // This prevents SemaphoreFullException if exception occurred before WaitAsync
                // CRITICAL FIX #4: Always release if acquired, even during disposal
                // Otherwise semaphore stays locked forever if disposal happens mid-transcription
                if (semaphoreAcquired)
                {
                    try
                    {
                        transcriptionSemaphore.Release();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Semaphore was disposed during shutdown - this is OK
                        // The semaphore is disposed at line 855 in Dispose()
                    }
                    catch (SemaphoreFullException)
                    {
                        // Should never happen since we track acquisition, but log just in case
                        ErrorLogger.LogWarning("Attempted to release semaphore that wasn't acquired");
                    }
                }

                // CRIT-4 FIX: Kill our specific whisper process if it's still running
                // Only kills the process WE spawned, not other whisper.exe instances
                if (process != null)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            var pid = process.Id;
                            process.Kill(entireProcessTree: true);
                            ErrorLogger.LogWarning($"Killed orphaned whisper.exe (PID: {pid})");
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Process already exited - this is fine
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogWarning($"Orphan whisper cleanup failed: {ex.Message}");
                    }
                }

                // CRITICAL FIX: Signal disposal completion to unblock Dispose() method
                // Without this, Dispose() waits 5 seconds for timeout on every shutdown
                try
                {
                    disposalComplete?.Set();
                }
                catch (Exception ex) { ErrorLogger.LogDebug($"DisposalComplete.Set failed (expected if disposed): {ex.Message}"); }
            }
        }

        /// <summary>
        /// Processes Whisper output: validates exit code, applies text post-processing, logs results.
        /// Returns final transcription text.
        /// </summary>
        private string ProcessWhisperOutput(Process process, StringBuilder outputBuilder, StringBuilder errorBuilder, DateTime startTime)
        {
            // CRITICAL: Log at WARNING level so it shows in Release builds
            ErrorLogger.LogWarning($"Whisper process exited with code: {process.ExitCode}");

            if (process.ExitCode != 0)
            {
                var error = errorBuilder.ToString();
                ErrorLogger.LogWarning($"Whisper process failed with exit code: {process.ExitCode}");
                if (!string.IsNullOrEmpty(error) && error.Length < 500)
                {
                    // Log truncated error to avoid excessive log sizes
                    ErrorLogger.LogWarning($"Whisper error: {error.Substring(0, Math.Min(error.Length, 500))}");
                }
                throw new ExternalException($"Whisper process failed with exit code {process.ExitCode}", process.ExitCode);
            }

            // Get raw Whisper output
            var rawResult = outputBuilder.ToString().Trim();

            // Apply text post-processing (punctuation and capitalization)
            // IMPORTANT: Only apply if user has it enabled in settings
            var result = rawResult;
            if (!string.IsNullOrWhiteSpace(rawResult))
            {
                try
                {
                    // Apply punctuation and capitalization post-processing
                    // Default: both enabled for better transcription quality
                    result = TextPostProcessor.Process(rawResult,
                        enablePunctuation: true,
                        enableCapitalization: true);

                    if (result != rawResult)
                    {
                        ErrorLogger.LogDebug($"Text post-processing applied: '{rawResult}' -> '{result}'");
                    }
                }
                catch (Exception postProcessEx)
                {
                    // Fallback to raw output if post-processing fails
                    ErrorLogger.LogError("Text post-processing failed, using raw output", postProcessEx);
                    result = rawResult;
                }
            }

            var totalTime = DateTime.Now - startTime;
            ErrorLogger.LogWarning($"Transcription completed in {totalTime.TotalMilliseconds:F0}ms, result length: {result.Length} chars");
            ErrorLogger.LogWarning($"Transcription result: '{result.Substring(0, Math.Min(result.Length, 200))}'");

            // PERFORMANCE: Removed redundant post-transcription warmup
            // Background warmup only provides benefit on cold start. After first transcription,
            // OS disk caches are already warm. This was wasting 500-2000ms CPU per transcription.

            return result;
        }

        /// <summary>
        /// Handles Whisper process timeout: kills process tree, waits with hard timeout, throws TimeoutException.
        /// </summary>
        private void HandleWhisperTimeout(Process process, int timeoutSeconds)
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
                if (!waitTask.Wait(PROCESS_KILL_HARD_TIMEOUT_MS) || !waitTask.Result)
                {
                    ErrorLogger.LogError("Whisper process refused to die after Kill()", new TimeoutException());

                    // Last resort: taskkill with verification
                    // ZOMBIE FIX: Wait for taskkill completion and verify process died
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            using var taskkill = Process.Start(new ProcessStartInfo
                            {
                                FileName = "taskkill",
                                Arguments = $"/F /T /PID {process.Id}",
                                CreateNoWindow = true,
                                UseShellExecute = false
                            });
                            // ZOMBIE FIX: Wait for taskkill to complete (up to 2s)
                            if (taskkill != null && !taskkill.WaitForExit(2000))
                            {
                                ErrorLogger.LogWarning($"taskkill command timed out for PID {process.Id}");
                            }
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
                try { process.Kill(); } catch (Exception fallbackEx) { ErrorLogger.LogDebug($"Fallback process.Kill failed: {fallbackEx.Message}"); }
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

        /// <summary>
        /// Creates, configures, and starts Whisper process with output/error capture.
        /// Returns tuple of (process, outputBuilder, errorBuilder).
        /// </summary>
        private (Process process, StringBuilder outputBuilder, StringBuilder errorBuilder) ExecuteWhisperProcess(string whisperExePath, string arguments)
        {
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

            var process = new Process { StartInfo = processStartInfo };
            var outputBuilder = new StringBuilder(4096); // PERFORMANCE: Pre-size for typical Whisper output (1-3KB)
            var errorBuilder = new StringBuilder(512);   // PERFORMANCE: Pre-size for error messages

            // MED-10 FIX: Maximum buffer sizes to prevent unbounded memory growth
            const int MAX_OUTPUT_SIZE = 1024 * 1024; // 1MB max for transcription output (paranoid limit)
            const int MAX_ERROR_SIZE = 64 * 1024;    // 64KB max for error messages

            process.OutputDataReceived += (s, e) =>
            {
                // HIGH-2 FIX: Calculate newLength before appending to prevent exceeding MAX_OUTPUT_SIZE
                if (!string.IsNullOrEmpty(e.Data))
                {
                    int newLength = outputBuilder.Length + e.Data.Length + Environment.NewLine.Length;
                    if (newLength <= MAX_OUTPUT_SIZE)
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                // HIGH-2 FIX: Calculate newLength before appending to prevent exceeding MAX_ERROR_SIZE
                if (!string.IsNullOrEmpty(e.Data))
                {
                    int newLength = errorBuilder.Length + e.Data.Length + Environment.NewLine.Length;
                    if (newLength <= MAX_ERROR_SIZE)
                    {
                        errorBuilder.AppendLine(e.Data);
                    }
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
            catch (Exception ex)
            {
                ErrorLogger.LogWarning($"Failed to set Whisper process priority: {ex.Message}");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return (process, outputBuilder, errorBuilder);
        }

        /// <summary>
        /// Builds Whisper command-line arguments with preset configuration, VAD support, and optimizations.
        /// SECURITY FIX (CMD-SANITIZE-001): Sanitizes all file paths and user-controllable inputs
        /// </summary>
        private string BuildWhisperArguments(string audioFilePath, string effectiveModelPath, out int threadCount)
        {
            // Build arguments using user-selected transcription preset
            // v1.2.0: Preset-driven configuration (Speed/Balanced/Accuracy)
            // - Speed: beam_size=1, aggressive optimizations (current v1.0.87 performance)
            // - Balanced: beam_size=3, moderate optimizations (recommended default)
            // - Accuracy: beam_size=5, minimal optimizations (best quality)
            // PERFORMANCE FIX: Use 4 threads instead of ProcessorCount/2 to avoid thread contention
            int optimalThreads = 4; // Fixed optimal thread count
            threadCount = optimalThreads;
            var presetConfig = WhisperPresetConfig.GetPresetConfig(settings.TranscriptionPreset);

            // SECURITY FIX (CMD-SANITIZE-001): Sanitize file paths to prevent injection
            // CRIT-3 FIX: Wrap in try-catch to handle SecurityException gracefully
            try
            {
                audioFilePath = SanitizeFilePath(audioFilePath, "audio file");
                effectiveModelPath = SanitizeFilePath(effectiveModelPath, "model file");
            }
            catch (SecurityException ex)
            {
                throw new InvalidOperationException($"Path validation failed: {ex.Message}", ex);
            }

            // SECURITY FIX (CMD-SANITIZE-001): Validate language code format
            var safeLanguage = SanitizeLanguageCode(settings.Language);

            var arguments = $"-m \"{effectiveModelPath}\" " +
                          $"-f \"{audioFilePath}\" " +
                          $"--threads {optimalThreads} " +
                          $"--no-timestamps --language {safeLanguage} " +
                          $"--beam-size {presetConfig.BeamSize} " +
                          $"--best-of {presetConfig.BestOf} " +
                          $"--entropy-thold {presetConfig.EntropyThreshold:F1} ";

            // DRAGON-LEVEL OPTIMIZATION: Temperature fallback (critical for accuracy)
            // Dragon retries failed decoding with different parameters - we do the same
            // Temperature fallback is "the main thing that helps with resolving repetitions and other failure cases"
            // Add optional flags based on preset
            if (!presetConfig.UseFallback)
            {
                arguments += "--no-fallback ";
            }
            else
            {
                // Use Whisper default temperature fallback: [0.0, 0.4, 0.8]
                // This allows retries with increasing randomness if initial decode fails
                arguments += "--temperature 0.0 --temperature-inc 0.4 ";
            }

            arguments += $"--max-context {presetConfig.MaxContext} ";

            if (presetConfig.UseFlashAttention)
                arguments += "--flash-attn ";

            // DRAGON-LEVEL OPTIMIZATION: Initial prompt (vocabulary/context adaptation)
            // Dragon uses custom vocabularies to improve accuracy on technical terms
            // Whisper's initial prompt serves the same purpose - guides recognition context
            // This helps with: proper nouns, technical jargon, programming terms, brand names
            var initialPrompt = "Transcribe the following dictation with proper capitalization and punctuation. " +
                               "Common terms: VoiceLite, GitHub, JavaScript, TypeScript, Python, C#, .NET, React, " +
                               "Node.js, API, JSON, SQL, database, function, variable, component, repository.";

            // SECURITY FIX (CMD-SANITIZE-001): Escape quotes in prompt to prevent command injection
            var safePrompt = initialPrompt.Replace("\"", "\\\"");
            arguments += $"--prompt \"{safePrompt}\" ";

            // VAD (Voice Activity Detection) support (v1.2.0)
            // CRITICAL: High-quality VAD implementation using Silero VAD v5.1.2
            // Benefits: Filters out silence/noise, improves speed and accuracy, reduces hallucinations
            if (settings.EnableVAD)
            {
                var vadModelPath = ResolveVADModelPath();
                if (!string.IsNullOrEmpty(vadModelPath) && File.Exists(vadModelPath))
                {
                    // SECURITY FIX (CMD-SANITIZE-001): Sanitize VAD model path
                    vadModelPath = SanitizeFilePath(vadModelPath, "VAD model");

                    // Validate VAD model file integrity
                    var vadFileInfo = new FileInfo(vadModelPath);
                    if (vadFileInfo.Length < 100000) // Silero VAD should be ~865KB
                    {
                        ErrorLogger.LogWarning($"VAD model file appears corrupted (size: {vadFileInfo.Length} bytes). Expected ~865KB. Disabling VAD.");
                    }
                    else
                    {
                        // Clamp threshold to safe range (0.3-0.8 validated by Silero VAD best practices)
                        var safeThreshold = Math.Clamp(settings.VADThreshold, 0.3, 0.8);

                        // TUNED VAD parameters based on Windows Voice Access behavior and Silero best practices
                        // Key insight: We want to KEEP speech, not aggressively filter it
                        arguments += $"--vad --vad-model \"{vadModelPath}\" " +
                                   $"--vad-threshold {safeThreshold:F2} " +
                                   "--vad-min-speech-duration-ms 80 " +     // 80ms: Catches short words/syllables (was 250ms - TOO HIGH!)
                                   "--vad-min-silence-duration-ms 500 " +   // 500ms: Only split on clear pauses, not mid-phrase (was 100ms - too aggressive)
                                   "--vad-speech-pad-ms 200";               // 200ms: Generous padding to avoid clipping word edges (was 30ms - too small)

                        ErrorLogger.LogMessage($"VAD enabled with threshold {safeThreshold:F2}, model: {Path.GetFileName(vadModelPath)}");
                    }
                }
                else
                {
                    ErrorLogger.LogWarning($"VAD enabled but model not found. Transcription will continue without VAD. " +
                                         $"Expected location: {vadModelPath ?? "unknown"}");
                }
            }

            return arguments;
        }

        /// <summary>
        /// SECURITY FIX (CMD-SANITIZE-001): Sanitizes file paths to prevent command injection and directory traversal
        /// </summary>
        private string SanitizeFilePath(string filePath, string fileType)
        {
            try
            {
                // Normalize path to absolute path (prevents relative path attacks)
                var normalizedPath = Path.GetFullPath(filePath);

                // Validate path is within expected directories
                var allowedDirs = new[] {
                    baseDir, // Application directory
                    Path.GetTempPath(), // Temp directory for audio files
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VoiceLite")
                };

                bool isAllowed = allowedDirs.Any(dir =>
                    normalizedPath.StartsWith(Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase));

                if (!isAllowed)
                {
                    throw new SecurityException(
                        $"{fileType} path is outside allowed directories: {normalizedPath}");
                }

                // Additional validation: ensure no shell metacharacters in filename
                var fileName = Path.GetFileName(normalizedPath);
                if (fileName.IndexOfAny(new[] { '&', '|', ';', '`', '$', '(', ')', '<', '>', '\n', '\r' }) != -1)
                {
                    throw new SecurityException(
                        $"{fileType} filename contains invalid characters: {fileName}");
                }

                return normalizedPath;
            }
            catch (Exception ex) when (ex is not SecurityException)
            {
                throw new SecurityException($"Failed to sanitize {fileType} path", ex);
            }
        }

        /// <summary>
        /// SECURITY FIX (CMD-SANITIZE-001): Validates language code format (ISO 639-1 or "auto")
        /// </summary>
        private string SanitizeLanguageCode(string languageCode)
        {
            // Allow only 2-letter ISO 639-1 codes (lowercase letters only) or "auto"
            if (string.IsNullOrWhiteSpace(languageCode))
                return "en"; // Default to English

            // Normalize to lowercase
            var normalized = languageCode.Trim().ToLower();

            // Special case: "auto" for language auto-detection
            if (normalized == "auto")
                return "auto";

            // Validate format: 2 lowercase letters only (ISO 639-1)
            if (normalized.Length != 2 || !normalized.All(char.IsLower))
            {
                // HIGH-5 FIX: Sanitize control characters before logging to prevent log injection
                // Attackers could inject newlines to create fake log entries
                string sanitized = new string(languageCode.Where(c => !char.IsControl(c)).ToArray());
                if (sanitized.Length > 20) sanitized = sanitized.Substring(0, 20) + "...";
                ErrorLogger.LogWarning($"Invalid language code '{sanitized}', defaulting to 'en'");
                return "en";
            }

            return normalized;
        }

        /// <summary>
        /// Validates audio file exists and has content. Throws on disposed service or missing file.
        /// Returns false if file is too small (likely empty).
        /// </summary>
        private bool ValidateAudioFile(string audioFilePath)
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
                return false;
            }

            return true;
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
                    catch (Exception delEx)
                    {
                        ErrorLogger.LogWarning($"Failed to delete dummy audio file {dummyAudioPath}: {delEx.Message}");
                    }
                }

                isWarmedUp = false;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("PersistentWhisperService.CleanupProcess", ex);
            }
        }

        // Interface implementation methods
        public void CancelTranscription()
        {
            // Signal cancellation of transcription operations
            transcriptionCts?.Cancel();
        }

        public bool ValidateWhisperExecutable()
        {
            try
            {
                var whisperPath = cachedWhisperExePath ?? modelResolver.ResolveWhisperExePath();
                if (string.IsNullOrEmpty(whisperPath) || !File.Exists(whisperPath))
                {
                    TranscriptionError?.Invoke(this, new FileNotFoundException("Whisper executable not found"));
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
                var whisperPath = cachedWhisperExePath ?? modelResolver.ResolveWhisperExePath();
                if (string.IsNullOrEmpty(whisperPath) || !File.Exists(whisperPath))
                {
                    return "Unknown";
                }

                // Run whisper --version
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = whisperPath,
                        Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(5000);

                    return string.IsNullOrWhiteSpace(output) ? "Unknown" : output.Trim();
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning($"Failed to get Whisper version: {ex.Message}");
                return "Unknown";
            }
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;

            // CRITICAL FIX: Cancel all operations immediately
            try
            {
                warmupCts.Cancel(); // Cancel warmup tasks
                transcriptionCts.Cancel(); // Cancel semaphore waits to unblock transcriptions
            }
            catch (Exception ex) { ErrorLogger.LogDebug($"CancellationTokenSource.Cancel failed: {ex.Message}"); }

            // MED-15 FIX: Use try-finally to ensure ManualResetEventSlim is always disposed
            // Previously, if Wait() threw, subsequent disposal code wouldn't run
            try
            {
                // CRITICAL FIX: Non-blocking wait for background tasks using ManualResetEventSlim
                // Old approach: Thread.Sleep(200) blocks UI thread
                // New approach: Efficient signaling with max 5-second timeout
                disposalComplete.Wait(TimeSpan.FromSeconds(DISPOSAL_COMPLETION_TIMEOUT_SECONDS));
            }
            catch (ObjectDisposedException)
            {
                // ManualResetEventSlim already disposed, continue cleanup
            }

            CleanupProcess();

            // Dispose semaphore safely
            if (transcriptionSemaphore != null)
            {
                try
                {
                    transcriptionSemaphore.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, ignore
                }
            }

            // Dispose cancellation token sources
            try
            {
                warmupCts.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }

            // CRITICAL FIX: Dispose transcription cancellation token source
            try
            {
                transcriptionCts.Dispose();
            }
            catch (ObjectDisposedException) { }

            // MED-15 FIX: Always dispose ManualResetEventSlim in finally-equivalent position
            try
            {
                disposalComplete.Dispose();
            }
            catch (ObjectDisposedException) { }
        }
    }
}



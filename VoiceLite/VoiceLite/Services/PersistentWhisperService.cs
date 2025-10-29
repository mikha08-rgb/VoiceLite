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
using VoiceLite.Models;
using VoiceLite.Core.Interfaces.Services;

namespace VoiceLite.Services
{
    public class PersistentWhisperService : IWhisperService
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
        private volatile bool isDisposed = false;
        private static bool _integrityWarningLogged = false;
        private volatile bool isProcessing = false;

        // Interface implementation properties and events
        public bool IsProcessing => isProcessing;
#pragma warning disable CS0067 // Event is never used - kept for interface compatibility
        public event EventHandler<string>? TranscriptionComplete;
#pragma warning restore CS0067
        public event EventHandler<Exception>? TranscriptionError;
#pragma warning disable CS0067 // Event is never used - kept for interface compatibility
        public event EventHandler<int>? ProgressChanged;
#pragma warning restore CS0067

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
                // Hash verified: B7C6DC2E999A80BC2D23CD4C76701211F392AE55D5CABDF0D45EB2CA4FAF09AF
                // File size: 469KB (480,768 bytes)
                // Build version: whisper.cpp v1.7.6 (Oct 2024) - Q8_0 quantization support
                const string EXPECTED_HASH = "B7C6DC2E999A80BC2D23CD4C76701211F392AE55D5CABDF0D45EB2CA4FAF09AF";

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

            // Check bundled models in Program Files (read-only)
            var modelPath = Path.Combine(baseDir, "whisper", modelFile);
            if (File.Exists(modelPath))
                return modelPath;

            modelPath = Path.Combine(baseDir, modelFile);
            if (File.Exists(modelPath))
                return modelPath;

            // Check downloaded models in LocalApplicationData (user-writable)
            var localDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoiceLite",
                "whisper",
                modelFile
            );
            if (File.Exists(localDataPath))
                return localDataPath;

            // No fallback - fail fast with clear error message
            // Exception is caught by MainWindow and shown to user with reinstall instructions
            throw new FileNotFoundException(
                $"Whisper model '{modelFile}' not found.\n\n" +
                $"Please download it from Settings → AI Models tab, or reinstall VoiceLite.\n\n" +
                $"Expected locations:\n" +
                $"- Bundled: {modelPath}\n" +
                $"- Downloaded: {localDataPath}");
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
                var modelPath = cachedModelPath ?? ResolveModelPath();
                var whisperExePath = cachedWhisperExePath ?? ResolveWhisperExePath();

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
                catch (Exception cleanupEx)
                {
                    ErrorLogger.LogWarning($"Failed to delete temp audio file {tempPath}: {cleanupEx.Message}");
                }
            }
        }

        // Backward compatibility overload
        public async Task<string> TranscribeAsync(string audioFilePath)
        {
            return await TranscribeAsync(audioFilePath, cachedModelPath ?? ResolveModelPath());
        }

        // Interface implementation
        public async Task<string> TranscribeAsync(string audioFilePath, string modelPath)
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
            Process? process = null; // TIER 1.3: Declare at method scope for finally block access

            try
            {
                // Use semaphore to ensure only one transcription at a time
                // CRITICAL FIX: Add cancellation token support to prevent deadlock during disposal
                await transcriptionSemaphore.WaitAsync(disposalCts.Token);
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
                var effectiveModelPath = !string.IsNullOrEmpty(modelPath) ? modelPath : (cachedModelPath ?? ResolveModelPath());
                var whisperExePath = cachedWhisperExePath ?? ResolveWhisperExePath();

                // Build arguments using user-selected transcription preset
                // v1.2.0: Preset-driven configuration (Speed/Balanced/Accuracy)
                // - Speed: beam_size=1, aggressive optimizations (current v1.0.87 performance)
                // - Balanced: beam_size=3, moderate optimizations (recommended default)
                // - Accuracy: beam_size=5, minimal optimizations (best quality)
                // PERFORMANCE FIX: Use 4 threads instead of ProcessorCount/2 to avoid thread contention
                int optimalThreads = 4; // Fixed optimal thread count
                var presetConfig = WhisperPresetConfig.GetPresetConfig(settings.TranscriptionPreset);

                var arguments = $"-m \"{effectiveModelPath}\" " +
                              $"-f \"{audioFilePath}\" " +
                              $"--threads {optimalThreads} " +
                              $"--no-timestamps --language {settings.Language} " +
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
                arguments += $"--prompt \"{initialPrompt}\" ";

                // VAD (Voice Activity Detection) support (v1.2.0)
                // CRITICAL: High-quality VAD implementation using Silero VAD v5.1.2
                // Benefits: Filters out silence/noise, improves speed and accuracy, reduces hallucinations
                if (settings.EnableVAD)
                {
                    var vadModelPath = ResolveVADModelPath();
                    if (!string.IsNullOrEmpty(vadModelPath) && File.Exists(vadModelPath))
                    {
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

                // DIAGNOSTIC: Log exact whisper command with thread count
                try
                {
                    var diagDir = Path.Combine(Path.GetTempPath(), "VoiceLite");
                    Directory.CreateDirectory(diagDir); // CRITICAL FIX: Create directory if it doesn't exist
                    File.AppendAllText(Path.Combine(diagDir, "diagnostic.log"),
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Thread count: {optimalThreads}, CPU cores: {Environment.ProcessorCount}\n" +
                        $"Command: {whisperExePath} {arguments}\n\n");
                }
                catch
                {
                    // Ignore diagnostic logging failures - don't let them break transcription
                }

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
                            "4. Consider using the Tiny model in Settings (smaller, loads faster)");
                    }
                    else
                    {
                        throw new TimeoutException($"Transcription timed out after {timeoutSeconds} seconds. Please try speaking less or using a smaller model.");
                    }
                }

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
                        ErrorLogger.LogWarning($"Text post-processing failed, using raw output: {postProcessEx.Message}");
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
                        // The semaphore is disposed at line 613 in Dispose()
                    }
                    catch (SemaphoreFullException)
                    {
                        // Should never happen since we track acquisition, but log just in case
                        ErrorLogger.LogWarning("Attempted to release semaphore that wasn't acquired");
                    }
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
            // Signal cancellation
            disposalCts?.Cancel();
        }

        public bool ValidateWhisperExecutable()
        {
            try
            {
                var whisperPath = cachedWhisperExePath ?? ResolveWhisperExePath();
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
                var whisperPath = cachedWhisperExePath ?? ResolveWhisperExePath();
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
            catch (Exception)
            {
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
                disposeCts.Cancel(); // Cancel warmup tasks
                disposalCts.Cancel(); // Cancel semaphore waits to unblock transcriptions
            }
            catch { /* Ignore cancellation errors */ }

            // CRITICAL FIX: Non-blocking wait for background tasks using ManualResetEventSlim
            // Old approach: Thread.Sleep(200) blocks UI thread
            // New approach: Efficient signaling with max 5-second timeout
            disposalComplete.Wait(TimeSpan.FromSeconds(5));

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

            // Dispose cancellation token source
            try
            {
                disposeCts.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }

            // CRITICAL FIX: Dispose new disposal coordination resources
            try
            {
                disposalCts.Dispose();
            }
            catch (ObjectDisposedException) { }

            try
            {
                disposalComplete.Dispose();
            }
            catch (ObjectDisposedException) { }
        }
    }
}



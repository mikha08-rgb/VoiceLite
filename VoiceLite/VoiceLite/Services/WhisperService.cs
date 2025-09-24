using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using VoiceLite.Interfaces;
using VoiceLite.Models;
using System.Collections.Generic;
using System.Linq;

namespace VoiceLite.Services
{
    /// <summary>
    /// DEPRECATED: This service is NOT used. MainWindow uses PersistentWhisperService instead.
    /// Consider removing this file and WhisperProcessPool.cs to reduce confusion.
    /// </summary>
    [Obsolete("Use PersistentWhisperService instead. This class is not used in the application.", false)]
    public class WhisperService : ITranscriber, IDisposable
    {
        private readonly Settings settings;
        private readonly string baseDir;
        private readonly int threadCount;
        private string? cachedWhisperExePath;
        private string? cachedModelPath;
        private readonly Queue<string> recentTranscriptions = new Queue<string>(10); // For context prompt
        private WhisperProcessPool? processPool;
        private readonly object processLock = new object();
        private readonly HashSet<int> activeProcessIds = new HashSet<int>(); // Track active process IDs for cleanup
        private bool? gpuAvailable = null; // Cache GPU availability status
        private int selectedGpuId = 0; // Default to first GPU

        public WhisperService(Settings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            baseDir = AppDomain.CurrentDomain.BaseDirectory;
            threadCount = Environment.ProcessorCount;

            // Cache paths at initialization to avoid repeated file system checks
            cachedWhisperExePath = ResolveWhisperExePath();
            cachedModelPath = ResolveModelPath();

            // Detect GPU availability on initialization
            DetectGpuAvailability();

            // Initialize process pool for better process management
            try
            {
                if (File.Exists(cachedWhisperExePath) && File.Exists(cachedModelPath))
                {
                    processPool = new WhisperProcessPool(settings, cachedWhisperExePath, cachedModelPath, maxPoolSize: 2);
                    ErrorLogger.LogMessage("WhisperProcessPool initialized successfully");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to initialize WhisperProcessPool", ex);
                // Fall back to traditional process management
                processPool = null;
            }
        }

        public async Task<string> TranscribeFromMemoryAsync(byte[] audioData)
        {
            ErrorLogger.LogMessage($"TranscribeFromMemoryAsync called with {audioData.Length} bytes");

            // Save to temporary file for whisper.cpp (until we implement stdin streaming)
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
            ErrorLogger.LogMessage($"TranscribeAsync called with: {audioFilePath}");

            if (!File.Exists(audioFilePath))
                throw new FileNotFoundException($"Audio file not found: {audioFilePath}");

            AudioPreprocessor.ProcessAudioFile(audioFilePath, settings);

            // Try to use process pool first for better performance and resource management
            if (processPool != null)
            {
                try
                {
                    return await TranscribeUsingPoolAsync(audioFilePath);
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Process pool transcription failed, falling back to direct process", ex);
                    // Fall through to traditional method
                }
            }

            // Use cached paths - much faster!
            var modelPath = cachedModelPath ?? ResolveModelPath();
            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"Whisper model not found at: {modelPath}");

            var whisperExePath = cachedWhisperExePath ?? ResolveWhisperExePath();
            ErrorLogger.LogMessage($"Whisper exe path: {whisperExePath}");
            if (!File.Exists(whisperExePath))
                throw new FileNotFoundException($"Whisper.exe not found at: {whisperExePath}");

            var audioDuration = GetAudioDuration(audioFilePath);
            var timeoutSeconds = CalculateTimeoutSeconds(audioDuration);

            var arguments = BuildArguments(modelPath, audioFilePath);

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

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            try
            {
                ErrorLogger.LogMessage($"Starting whisper process with args: {arguments}");
                process.Start();

                // Track process ID for cleanup
                lock (processLock)
                {
                    activeProcessIds.Add(process.Id);
                    ErrorLogger.LogMessage($"Tracking whisper process ID: {process.Id}");
                }

                // Set high priority for faster execution
                try
                {
                    process.PriorityClass = ProcessPriorityClass.AboveNormal;
                }
                catch { /* Ignore if we can't set priority */ }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(1000); // Wait up to 1 second for graceful exit
                    if (!process.HasExited)
                    {
                        process.Kill(true); // Force kill entire process tree
                    }
                }
                catch { }
                throw new TimeoutException("Transcription timed out. Please try again.");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("WhisperService.TranscribeAsync", ex);
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch { }
                throw new Exception($"Failed to run Whisper: {ex.Message}", ex);
            }
            finally
            {
                // Remove from tracked processes
                lock (processLock)
                {
                    activeProcessIds.Remove(process.Id);
                    ErrorLogger.LogMessage($"Removed whisper process ID from tracking: {process.Id}");
                }
            }

            if (process.ExitCode != 0)
            {
                var error = errorBuilder.ToString();
                if (error.Contains("model", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("Whisper model file appears to be corrupted. Please reinstall or re-download it.");
                }

                throw new Exception("Whisper transcription failed. Please try again.");
            }

            var output = outputBuilder.ToString();
            var transcription = ExtractTranscription(output);
            transcription = TranscriptionPostProcessor.ProcessTranscription(transcription.Trim(), settings.UseEnhancedDictionary);

            // Store for context (before returning)
            if (!string.IsNullOrWhiteSpace(transcription) && settings.UseContextPrompt)
            {
                StoreRecentTranscription(transcription);
            }

            return transcription;
        }

        private void DetectGpuAvailability()
        {
            try
            {
                // Check if clblast.dll exists (OpenCL/GPU support indicator)
                var whisperDir = Path.GetDirectoryName(cachedWhisperExePath ?? ResolveWhisperExePath());
                if (!string.IsNullOrEmpty(whisperDir))
                {
                    var clblastPath = Path.Combine(whisperDir, "clblast.dll");
                    var openblasPath = Path.Combine(whisperDir, "libopenblas.dll");

                    if (File.Exists(clblastPath))
                    {
                        // Test GPU availability by running a quick whisper command
                        TestGpuCapability(whisperDir);
                    }
                    else
                    {
                        gpuAvailable = false;
                        ErrorLogger.LogMessage("GPU libraries not found (clblast.dll missing)");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("GPU detection failed", ex);
                gpuAvailable = false;
            }
        }

        private void TestGpuCapability(string whisperDir)
        {
            try
            {
                var testProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = cachedWhisperExePath ?? Path.Combine(whisperDir, "whisper.exe"),
                        Arguments = "--help", // Quick test command
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                testProcess.Start();
                var output = testProcess.StandardOutput.ReadToEnd();
                var error = testProcess.StandardError.ReadToEnd();
                testProcess.WaitForExit(5000);

                // Check if GPU-related options are available in help output
                if (output.Contains("--gpu-id") || output.Contains("--use-gpu") || output.Contains("opencl"))
                {
                    gpuAvailable = true;
                    ErrorLogger.LogMessage("GPU acceleration is available");

                    // Try to detect available GPU devices
                    DetectGpuDevices();
                }
                else
                {
                    gpuAvailable = false;
                    ErrorLogger.LogMessage("GPU options not found in whisper.exe");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("GPU capability test failed", ex);
                gpuAvailable = false;
            }
        }

        private void DetectGpuDevices()
        {
            try
            {
                // Try to enumerate GPU devices using Windows Management Instrumentation
                using var searcher = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                var gpuCount = 0;

                foreach (System.Management.ManagementObject obj in searcher.Get())
                {
                    var name = obj["Name"]?.ToString();
                    if (!string.IsNullOrEmpty(name) && (name.Contains("NVIDIA") || name.Contains("AMD") || name.Contains("Intel")))
                    {
                        ErrorLogger.LogMessage($"Found GPU {gpuCount}: {name}");
                        gpuCount++;
                    }
                }

                if (gpuCount == 0)
                {
                    gpuAvailable = false;
                    ErrorLogger.LogMessage("No compatible GPU found");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogMessage($"GPU enumeration failed: {ex.Message}");
                // Don't set gpuAvailable to false here - GPU might still work
            }
        }

        public bool IsGpuAvailable() => gpuAvailable == true;

        public void SetGpuId(int gpuId)
        {
            selectedGpuId = Math.Max(0, gpuId);
            ErrorLogger.LogMessage($"GPU ID set to: {selectedGpuId}");
        }

        private string ResolveWhisperExePath()
        {
            // Return cached path if it exists
            if (!string.IsNullOrEmpty(cachedWhisperExePath) && File.Exists(cachedWhisperExePath))
            {
                return cachedWhisperExePath;
            }

            // Search for whisper.exe in multiple possible locations
            var searchPaths = new List<string>();

            // First, check the standard whisper subfolder in the executable directory
            searchPaths.Add(Path.Combine(baseDir, "whisper", "whisper.exe"));

            // Then walk up the directory tree looking for whisper folders
            var currentDir = baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            while (!string.IsNullOrEmpty(currentDir))
            {
                searchPaths.Add(Path.Combine(currentDir, "whisper", "whisper.exe"));

                // Also check if we're in a bin\Debug or bin\Release folder
                if (currentDir.Contains("bin", StringComparison.OrdinalIgnoreCase))
                {
                    var parent = Directory.GetParent(currentDir);
                    if (parent != null)
                    {
                        searchPaths.Add(Path.Combine(parent.FullName, "whisper", "whisper.exe"));
                    }
                }

                var nextParent = Directory.GetParent(currentDir);
                var next = nextParent?.FullName;
                if (string.Equals(next, currentDir, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                currentDir = next ?? string.Empty;
            }

            // Remove duplicates and check each path
            foreach (var path in searchPaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (File.Exists(path))
                {
                    cachedWhisperExePath = path;
                    return path;
                }
            }

            // Default fallback
            return Path.Combine(baseDir, "whisper", "whisper.exe");
        }

        private string ResolveModelPath()
        {
            var requestedModel = string.IsNullOrWhiteSpace(settings.WhisperModel)
                ? "ggml-small.bin"
                : settings.WhisperModel.Trim();

            // Check if we need to update cached path (model changed)
            if (!string.IsNullOrEmpty(cachedModelPath) &&
                File.Exists(cachedModelPath) &&
                Path.GetFileName(cachedModelPath).Equals(requestedModel, StringComparison.OrdinalIgnoreCase))
            {
                // Validate model file integrity
                if (ValidateModelFile(cachedModelPath))
                {
                    return cachedModelPath;
                }
                else
                {
                    cachedModelPath = null; // Invalidate corrupted cache
                    ErrorLogger.LogMessage($"Model file validation failed for cached path: {cachedModelPath}");
                }
            }

            // First, try to find the model in the same directory as whisper.exe
            var whisperExePath = ResolveWhisperExePath();
            var whisperDir = Path.GetDirectoryName(whisperExePath);

            if (!string.IsNullOrEmpty(whisperDir))
            {
                var modelInWhisperDir = Path.Combine(whisperDir, requestedModel);
                if (File.Exists(modelInWhisperDir) && ValidateModelFile(modelInWhisperDir))
                {
                    cachedModelPath = modelInWhisperDir;
                    return modelInWhisperDir;
                }

                // Try fallback models in the whisper directory
                var fallbacks = new[] { "ggml-medium.bin", "ggml-small.bin", "ggml-large-v3.bin" };
                foreach (var fallback in fallbacks)
                {
                    var fallbackPath = Path.Combine(whisperDir, fallback);
                    if (File.Exists(fallbackPath) && ValidateModelFile(fallbackPath))
                    {
                        cachedModelPath = fallbackPath;
                        return fallbackPath;
                    }
                }
            }

            // Fallback to walking directory tree if needed
            var searchRoots = new List<string>();
            var currentDir = baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            while (!string.IsNullOrEmpty(currentDir))
            {
                searchRoots.Add(Path.Combine(currentDir, "whisper"));
                var parent = Directory.GetParent(currentDir);
                var next = parent?.FullName;
                if (string.Equals(next, currentDir, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                currentDir = next ?? string.Empty;
            }

            foreach (var root in searchRoots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(root))
                    continue;

                var candidatePath = Path.Combine(root, requestedModel);
                if (File.Exists(candidatePath) && ValidateModelFile(candidatePath))
                {
                    cachedModelPath = candidatePath;
                    return candidatePath;
                }
            }

            return Path.Combine(baseDir, "whisper", requestedModel);
        }

        private static TimeSpan GetAudioDuration(string audioFilePath)
        {
            try
            {
                using var reader = new AudioFileReader(audioFilePath);
                return reader.TotalTime;
            }
            catch
            {
                return TimeSpan.FromSeconds(0);
            }
        }

        private double CalculateTimeoutSeconds(TimeSpan duration)
        {
            var multiplier = settings.WhisperTimeoutMultiplier > 0.5 ? settings.WhisperTimeoutMultiplier : 2.0;
            var dynamicTimeout = duration.TotalSeconds * multiplier;
            return Math.Max(10, Math.Max(dynamicTimeout, 30));
        }

        private string BuildArguments(string modelPath, string audioFilePath)
        {
            var builder = new StringBuilder();
            builder.Append($"-m \"{modelPath}\" -f \"{audioFilePath}\" --no-timestamps");

            var language = settings.Language?.Trim();
            if (!string.IsNullOrWhiteSpace(language))
            {
                builder.Append($" --language {language}");
            }

            // GPU acceleration if available
            if (gpuAvailable == true)
            {
                builder.Append($" --gpu-id {selectedGpuId}");
                // Use optimal thread count for GPU (usually fewer threads when using GPU)
                builder.Append(" --threads 4");
                ErrorLogger.LogMessage($"Using GPU acceleration with GPU ID: {selectedGpuId}");
            }
            else
            {
                // CPU mode - use all available threads
                builder.Append($" --threads {threadCount}");
                if (gpuAvailable == false)
                {
                    ErrorLogger.LogMessage("GPU not available, using CPU mode");
                }
            }

            if (settings.BeamSize > 0)
            {
                builder.Append($" --beam-size {settings.BeamSize}");
            }

            if (settings.BestOf > 0)
            {
                builder.Append($" --best-of {settings.BestOf}");
            }

            // Enhanced accuracy parameters
            builder.Append(" --entropy-thold 2.2"); // Lower threshold for better word detection
            builder.Append(" --logprob-thold -1.0");
            builder.Append(" --no-context"); // Disable context carry-over between segments
            builder.Append(" --word-thold 0.01"); // Lower word threshold for better detection

            // Max context and length penalties for better accuracy
            builder.Append(" --max-context 224"); // Increase context window
            builder.Append(" --length-penalty 0.1"); // Slight penalty for overly long outputs

            // Compression ratio threshold
            builder.Append(" --compression-ratio-threshold 2.0"); // Lower threshold for better quality

            // Add temperature optimization for better accuracy
            if (settings.UseTemperatureOptimization)
            {
                // Lower temperature = more deterministic = better accuracy
                builder.Append($" --temperature {settings.WhisperTemperature:F2}");
                builder.Append(" --temperature-inc 0.2"); // Increase temperature if decoding fails
            }

            // Add initial prompt for context (helps with technical terms)
            if (settings.UseContextPrompt)
            {
                var contextPrompt = GetContextPrompt();
                if (!string.IsNullOrWhiteSpace(contextPrompt))
                {
                    // Escape quotes and limit length
                    contextPrompt = contextPrompt.Replace("\"", "\\\"").Replace("\n", " ");
                    if (contextPrompt.Length > 200) contextPrompt = contextPrompt.Substring(0, 200);
                    builder.Append($" --initial-prompt \"{contextPrompt}\"");
                }
            }

            return builder.ToString();
        }

        private string GetContextPrompt()
        {
            // Combine recent transcriptions with common technical terms
            var recent = string.Join(" ", recentTranscriptions.TakeLast(3));
            var technicalContext = "git commit push pull useState useEffect async await npm yarn " +
                                 "GitHub JSON API REST GraphQL Docker Kubernetes React Vue Angular " +
                                 "TypeScript JavaScript C# Python forEach console.log useState";

            // Combine and limit length
            var combined = $"{recent} {technicalContext}".Trim();
            return combined.Length > 200 ? combined.Substring(0, 200) : combined;
        }

        private void StoreRecentTranscription(string transcription)
        {
            // Keep only last 10 transcriptions for context
            lock (recentTranscriptions)
            {
                if (recentTranscriptions.Count >= 10)
                {
                    recentTranscriptions.Dequeue();
                }
                recentTranscriptions.Enqueue(transcription);
            }
        }

        private string ExtractTranscription(string whisperOutput)
        {
            var lines = whisperOutput.Split('\n');
            var transcriptionBuilder = new StringBuilder();
            bool foundTranscription = false;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (string.IsNullOrEmpty(trimmedLine))
                    continue;

                if (trimmedLine.StartsWith("[") && trimmedLine.Contains("]"))
                    continue;

                if (trimmedLine.Contains("whisper_", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.Contains("system_info", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.Contains("model size", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.Contains("processing", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.Contains("thread", StringComparison.OrdinalIgnoreCase))
                    continue;

                foundTranscription = true;
                transcriptionBuilder.Append(trimmedLine + " ");
            }

            return foundTranscription ? transcriptionBuilder.ToString() : string.Empty;
        }

        private bool ValidateModelFile(string modelPath)
        {
            try
            {
                var fileInfo = new FileInfo(modelPath);

                // Check minimum size - whisper models are at least 39MB (tiny model)
                if (fileInfo.Length < 39 * 1024 * 1024)
                {
                    ErrorLogger.LogMessage($"Model file too small: {modelPath} ({fileInfo.Length} bytes)");
                    return false;
                }

                // Check if file is readable by attempting to read the header
                using (var fs = new FileStream(modelPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var buffer = new byte[4];
                    if (fs.Read(buffer, 0, 4) != 4)
                    {
                        ErrorLogger.LogMessage($"Cannot read model file header: {modelPath}");
                        return false;
                    }

                    // Whisper GGML models should start with specific magic bytes
                    // GGML format typically starts with 0x67676d6c ("ggml" in hex)
                    if (buffer[0] != 0x67 || buffer[1] != 0x67 || buffer[2] != 0x6d)
                    {
                        ErrorLogger.LogMessage($"Invalid model file format: {modelPath}");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError($"ValidateModelFile failed for {modelPath}", ex);
                return false;
            }
        }

        private async Task<string> TranscribeUsingPoolAsync(string audioFilePath)
        {
            if (processPool == null)
                throw new InvalidOperationException("Process pool not initialized");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await processPool.ExecuteTranscriptionAsync(audioFilePath, cts.Token);

            if (!string.IsNullOrEmpty(result.Error))
            {
                ErrorLogger.LogMessage($"Whisper error output: {result.Error}");
            }

            var transcription = ExtractTranscription(result.Output);
            transcription = TranscriptionPostProcessor.ProcessTranscription(transcription.Trim(), settings.UseEnhancedDictionary);

            // Store for context
            if (!string.IsNullOrWhiteSpace(transcription) && settings.UseContextPrompt)
            {
                StoreRecentTranscription(transcription);
            }

            return transcription;
        }

        private void CleanupZombieProcesses()
        {
            // Process pool handles its own cleanup now
            if (processPool == null)
            {
                // Legacy cleanup for when pool is not available
                var whisperProcesses = Process.GetProcessesByName("whisper");
                foreach (var process in whisperProcesses)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            ErrorLogger.LogMessage($"Killing zombie whisper process: {process.Id}");
                            process.Kill(true);
                            process.WaitForExit(1000);
                        }
                    }
                    catch { /* Ignore */ }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
        }

        public void Dispose()
        {
            // Dispose process pool
            processPool?.Dispose();
            processPool = null;

            // Clean up any remaining processes
            CleanupZombieProcesses();
        }
    }
}

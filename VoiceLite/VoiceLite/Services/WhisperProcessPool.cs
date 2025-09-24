using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    /// <summary>
    /// DEPRECATED: This class is NOT used. It's only instantiated by WhisperService, which itself is not used.
    /// MainWindow uses PersistentWhisperService instead.
    /// WARNING: This class has a critical bug - Dispose() method doesn't properly clean up processes.
    /// Consider removing this file entirely to reduce confusion and potential issues.
    /// </summary>
    [Obsolete("Not used in the application. WhisperService (which uses this) is also deprecated.", false)]
    public class WhisperProcessPool : IDisposable
    {
        private readonly ConcurrentQueue<WhisperProcess> availableProcesses = new();
        private readonly HashSet<int> activeProcessIds = new();
        private readonly object processLock = new object();
        private readonly Settings settings;
        private readonly string whisperExePath;
        private readonly string modelPath;
        private readonly int maxPoolSize;
        private readonly int maxProcessReuses;
        private readonly Timer healthCheckTimer;
        private readonly Timer memoryMonitorTimer;
        private volatile bool isDisposed = false;
        private long totalMemoryUsage = 0;
        private readonly ConcurrentDictionary<int, ProcessMemoryInfo> processMemoryTracking = new();

        public WhisperProcessPool(Settings settings, string whisperExePath, string modelPath, int maxPoolSize = 3)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.whisperExePath = whisperExePath ?? throw new ArgumentNullException(nameof(whisperExePath));
            this.modelPath = modelPath ?? throw new ArgumentNullException(nameof(modelPath));
            this.maxPoolSize = maxPoolSize;
            this.maxProcessReuses = 10; // Recycle process after 10 uses to prevent degradation

            // Start health monitoring
            healthCheckTimer = new Timer(HealthCheckCallback, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            memoryMonitorTimer = new Timer(MemoryMonitorCallback, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

            // Pre-warm the pool with one process
            _ = Task.Run(() => WarmupPool(1));
        }

        private class WhisperProcess
        {
            public Process Process { get; set; }
            public int UsageCount { get; set; }
            public DateTime LastUsed { get; set; }
            public bool IsHealthy { get; set; } = true;
            public long PeakMemoryUsage { get; set; }

            public WhisperProcess(Process process)
            {
                Process = process;
                UsageCount = 0;
                LastUsed = DateTime.Now;
            }
        }

        private class ProcessMemoryInfo
        {
            public long WorkingSet { get; set; }
            public long PeakWorkingSet { get; set; }
            public DateTime LastChecked { get; set; }
        }

        private async Task WarmupPool(int count)
        {
            try
            {
                for (int i = 0; i < count && availableProcesses.Count < maxPoolSize; i++)
                {
                    var process = await CreateWhisperProcess();
                    if (process != null)
                    {
                        var whisperProcess = new WhisperProcess(process);
                        availableProcesses.Enqueue(whisperProcess);
                        ErrorLogger.LogMessage($"Pre-warmed whisper process {process.Id} added to pool");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("WhisperProcessPool.WarmupPool", ex);
            }
        }

        public async Task<ProcessExecutionResult> ExecuteTranscriptionAsync(string audioFilePath, CancellationToken cancellationToken)
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(WhisperProcessPool));

            WhisperProcess? whisperProcess = null;
            Process? process = null;
            var shouldReturnToPool = true;

            try
            {
                // Get or create a process
                whisperProcess = await GetOrCreateProcess();
                process = whisperProcess.Process;

                // Track active process
                lock (processLock)
                {
                    activeProcessIds.Add(process.Id);
                }

                // Build arguments for transcription
                var arguments = BuildTranscriptionArguments(audioFilePath);

                // Execute transcription
                var result = await ExecuteProcessAsync(process, arguments, audioFilePath, cancellationToken);

                // Update usage statistics
                whisperProcess.UsageCount++;
                whisperProcess.LastUsed = DateTime.Now;

                // Check if process should be retired
                if (whisperProcess.UsageCount >= maxProcessReuses || !IsProcessHealthy(process))
                {
                    shouldReturnToPool = false;
                    ErrorLogger.LogMessage($"Retiring whisper process {process.Id} after {whisperProcess.UsageCount} uses");
                }

                return result;
            }
            catch (Exception)
            {
                shouldReturnToPool = false; // Don't return unhealthy process to pool
                throw;
            }
            finally
            {
                if (process != null)
                {
                    lock (processLock)
                    {
                        activeProcessIds.Remove(process.Id);
                    }

                    if (shouldReturnToPool && whisperProcess != null && !isDisposed)
                    {
                        // Return healthy process to pool
                        availableProcesses.Enqueue(whisperProcess);
                    }
                    else if (process != null)
                    {
                        // Dispose unhealthy or retired process
                        TerminateProcess(process);
                    }
                }
            }
        }

        private async Task<WhisperProcess> GetOrCreateProcess()
        {
            // Try to get an available process from the pool
            if (availableProcesses.TryDequeue(out var whisperProcess))
            {
                if (IsProcessHealthy(whisperProcess.Process))
                {
                    ErrorLogger.LogMessage($"Reusing whisper process {whisperProcess.Process.Id} from pool");
                    return whisperProcess;
                }
                else
                {
                    // Process is unhealthy, dispose it
                    TerminateProcess(whisperProcess.Process);
                }
            }

            // Create a new process if pool is not at max size
            lock (processLock)
            {
                if (activeProcessIds.Count >= maxPoolSize)
                {
                    throw new InvalidOperationException($"Process pool limit reached ({maxPoolSize} processes)");
                }
            }

            var newProcess = await CreateWhisperProcess();
            if (newProcess == null)
            {
                throw new InvalidOperationException("Failed to create whisper process");
            }

            return new WhisperProcess(newProcess);
        }

        private async Task<Process?> CreateWhisperProcess()
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = whisperExePath,
                    Arguments = $"-m \"{modelPath}\" --interactive", // Interactive mode for reuse
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                var process = new Process { StartInfo = processStartInfo };
                process.Start();

                // Set process priority
                try
                {
                    process.PriorityClass = ProcessPriorityClass.AboveNormal;
                }
                catch { /* Ignore if we can't set priority */ }

                // Track memory from the start
                TrackProcessMemory(process);

                ErrorLogger.LogMessage($"Created new whisper process {process.Id}");
                return process;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("WhisperProcessPool.CreateWhisperProcess", ex);
                return null;
            }
        }

        private string BuildTranscriptionArguments(string audioFilePath)
        {
            var builder = new StringBuilder();
            builder.Append($"-f \"{audioFilePath}\" --no-timestamps");

            var language = settings.Language?.Trim();
            if (!string.IsNullOrWhiteSpace(language))
            {
                builder.Append($" --language {language}");
            }

            builder.Append($" --threads {Environment.ProcessorCount}");

            if (settings.BeamSize > 0)
            {
                builder.Append($" --beam-size {settings.BeamSize}");
            }

            if (settings.BestOf > 0)
            {
                builder.Append($" --best-of {settings.BestOf}");
            }

            // Add accuracy parameters
            builder.Append(" --entropy-thold 2.2");
            builder.Append(" --logprob-thold -1.0");

            return builder.ToString();
        }

        private async Task<ProcessExecutionResult> ExecuteProcessAsync(
            Process process,
            string arguments,
            string audioFilePath,
            CancellationToken cancellationToken)
        {
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();
            var tcs = new TaskCompletionSource<ProcessExecutionResult>();

            // For interactive mode, write command to stdin
            await process.StandardInput.WriteLineAsync(arguments);
            await process.StandardInput.FlushAsync();

            // Read output with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            try
            {
                var outputTask = ReadStreamAsync(process.StandardOutput, outputBuilder, cts.Token);
                var errorTask = ReadStreamAsync(process.StandardError, errorBuilder, cts.Token);

                await Task.WhenAll(outputTask, errorTask);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("Transcription timed out");
            }

            return new ProcessExecutionResult
            {
                Output = outputBuilder.ToString(),
                Error = errorBuilder.ToString(),
                ExitCode = 0 // Process remains running in interactive mode
            };
        }

        private async Task ReadStreamAsync(StreamReader reader, StringBuilder builder, CancellationToken cancellationToken)
        {
            var buffer = new char[4096];
            while (!cancellationToken.IsCancellationRequested)
            {
                var readTask = reader.ReadAsync(buffer, 0, buffer.Length);
                var completedTask = await Task.WhenAny(readTask, Task.Delay(100, cancellationToken));

                if (completedTask == readTask)
                {
                    var count = await readTask;
                    if (count == 0) break;
                    builder.Append(buffer, 0, count);

                    // Check for completion marker
                    var current = builder.ToString();
                    if (current.Contains("[TRANSCRIPTION_COMPLETE]") || current.Contains("whisper_print_timings"))
                    {
                        break;
                    }
                }
            }
        }

        private bool IsProcessHealthy(Process process)
        {
            try
            {
                if (process.HasExited)
                    return false;

                // Check memory usage
                process.Refresh();
                var workingSet = process.WorkingSet64;
                var peakWorkingSet = process.PeakWorkingSet64;

                // If memory usage is too high (>500MB), consider unhealthy
                if (workingSet > 500 * 1024 * 1024)
                {
                    ErrorLogger.LogMessage($"Process {process.Id} unhealthy: High memory usage {workingSet / 1024 / 1024}MB");
                    return false;
                }

                // Check if process is responsive (try to get process time)
                var totalTime = process.TotalProcessorTime;
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError($"IsProcessHealthy check failed for process {process.Id}", ex);
                return false;
            }
        }

        private void TerminateProcess(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true); // Kill entire process tree
                    process.WaitForExit(1000);
                }
                process.Dispose();

                // Remove from memory tracking
                processMemoryTracking.TryRemove(process.Id, out _);

                ErrorLogger.LogMessage($"Terminated whisper process {process.Id}");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError($"Failed to terminate process {process.Id}", ex);
            }
        }

        private void TrackProcessMemory(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    var memInfo = new ProcessMemoryInfo
                    {
                        WorkingSet = process.WorkingSet64,
                        PeakWorkingSet = process.PeakWorkingSet64,
                        LastChecked = DateTime.Now
                    };
                    processMemoryTracking.AddOrUpdate(process.Id, memInfo, (k, v) => memInfo);
                }
            }
            catch { /* Ignore tracking errors */ }
        }

        private void HealthCheckCallback(object? state)
        {
            if (isDisposed) return;

            try
            {
                var unhealthyProcesses = new List<WhisperProcess>();

                // Check all available processes
                while (availableProcesses.TryDequeue(out var whisperProcess))
                {
                    if (IsProcessHealthy(whisperProcess.Process) &&
                        (DateTime.Now - whisperProcess.LastUsed).TotalMinutes < 5)
                    {
                        availableProcesses.Enqueue(whisperProcess);
                    }
                    else
                    {
                        unhealthyProcesses.Add(whisperProcess);
                    }
                }

                // Terminate unhealthy processes
                foreach (var unhealthy in unhealthyProcesses)
                {
                    TerminateProcess(unhealthy.Process);
                }

                // Clean up orphaned processes
                CleanupOrphanedProcesses();

                if (unhealthyProcesses.Count > 0)
                {
                    ErrorLogger.LogMessage($"Health check: Removed {unhealthyProcesses.Count} unhealthy processes");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("WhisperProcessPool.HealthCheckCallback", ex);
            }
        }

        private void MemoryMonitorCallback(object? state)
        {
            if (isDisposed) return;

            try
            {
                long totalMemory = 0;
                var processesToCheck = new List<int>();

                lock (processLock)
                {
                    processesToCheck.AddRange(activeProcessIds);
                }

                foreach (var pid in processesToCheck)
                {
                    try
                    {
                        var process = Process.GetProcessById(pid);
                        if (!process.HasExited)
                        {
                            process.Refresh();
                            var memory = process.WorkingSet64;
                            totalMemory += memory;
                            TrackProcessMemory(process);
                        }
                    }
                    catch { /* Process may have exited */ }
                }

                totalMemoryUsage = totalMemory;

                if (totalMemory > 1024 * 1024 * 1024) // > 1GB total
                {
                    ErrorLogger.LogMessage($"WARNING: High memory usage detected: {totalMemory / 1024 / 1024}MB");

                    // Force cleanup of idle processes
                    while (availableProcesses.TryDequeue(out var process))
                    {
                        TerminateProcess(process.Process);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("WhisperProcessPool.MemoryMonitorCallback", ex);
            }
        }

        private void CleanupOrphanedProcesses()
        {
            try
            {
                var whisperProcesses = Process.GetProcessesByName("whisper");
                foreach (var process in whisperProcesses)
                {
                    try
                    {
                        bool isOurProcess;
                        lock (processLock)
                        {
                            isOurProcess = activeProcessIds.Contains(process.Id);
                        }

                        if (!isOurProcess && !process.HasExited)
                        {
                            ErrorLogger.LogMessage($"Killing orphaned whisper process: {process.Id}");
                            process.Kill(true);
                            process.WaitForExit(1000);
                        }
                    }
                    catch { /* Ignore individual process errors */ }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("WhisperProcessPool.CleanupOrphanedProcesses", ex);
            }
        }

        public long GetTotalMemoryUsage() => totalMemoryUsage;

        public int GetActiveProcessCount()
        {
            lock (processLock)
            {
                return activeProcessIds.Count;
            }
        }

        public int GetAvailableProcessCount() => availableProcesses.Count;

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            // Stop timers
            healthCheckTimer?.Dispose();
            memoryMonitorTimer?.Dispose();

            // Terminate all processes
            while (availableProcesses.TryDequeue(out var whisperProcess))
            {
                TerminateProcess(whisperProcess.Process);
            }

            lock (processLock)
            {
                foreach (var pid in activeProcessIds.ToList())
                {
                    try
                    {
                        var process = Process.GetProcessById(pid);
                        TerminateProcess(process);
                    }
                    catch { /* Process may have already exited */ }
                }
                activeProcessIds.Clear();
            }

            // Final cleanup of any remaining orphaned processes
            CleanupOrphanedProcesses();

            ErrorLogger.LogMessage("WhisperProcessPool disposed");
        }

        public class ProcessExecutionResult
        {
            public string Output { get; set; } = string.Empty;
            public string Error { get; set; } = string.Empty;
            public int ExitCode { get; set; }
        }
    }
}
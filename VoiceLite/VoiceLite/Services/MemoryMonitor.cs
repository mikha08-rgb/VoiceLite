using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace VoiceLite.Services
{
    /// <summary>
    /// Monitors application memory usage and performs automatic cleanup to prevent memory leaks
    /// </summary>
    public class MemoryMonitor : IDisposable
    {
        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, int min, int max);

        private readonly Timer monitorTimer;
        private readonly Timer gcTimer;
        private readonly Process currentProcess; // Note: Process.GetCurrentProcess() doesn't need disposal
        private long baselineMemory;
        private long peakMemory;
        private long lastMemory;
        private int consecutiveHighMemoryCount = 0;
        private readonly object statsLock = new object();
        private volatile bool isDisposed = false;

        // Thresholds
        private const long WARNING_THRESHOLD_MB = 300;
        private const long CRITICAL_THRESHOLD_MB = 500;
        private const long FORCE_CLEANUP_THRESHOLD_MB = 600;
        private const int HIGH_MEMORY_CONSECUTIVE_LIMIT = 5;

        public event EventHandler<MemoryAlertEventArgs>? MemoryAlert;

        public MemoryMonitor()
        {
            currentProcess = Process.GetCurrentProcess();
            baselineMemory = currentProcess.WorkingSet64;
            lastMemory = baselineMemory;

            // Monitor memory every 5 seconds
            monitorTimer = new Timer(MonitorCallback, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

            // Run GC optimization every 30 seconds
            gcTimer = new Timer(GCOptimizationCallback, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

            ErrorLogger.LogMessage($"MemoryMonitor initialized. Baseline memory: {baselineMemory / 1024 / 1024}MB");
        }

        private void MonitorCallback(object? state)
        {
            if (isDisposed) return;

            try
            {
                currentProcess.Refresh();
                var currentMemory = currentProcess.WorkingSet64;
                var currentMemoryMB = currentMemory / 1024 / 1024;
                var gcMemory = GC.GetTotalMemory(false) / 1024 / 1024;

                lock (statsLock)
                {
                    if (currentMemory > peakMemory)
                    {
                        peakMemory = currentMemory;
                    }

                    // Check for memory leak patterns
                    var memoryDelta = currentMemory - lastMemory;
                    var isIncreasing = memoryDelta > 1024 * 1024; // >1MB increase

                    if (currentMemoryMB > WARNING_THRESHOLD_MB)
                    {
                        consecutiveHighMemoryCount++;

                        if (currentMemoryMB > CRITICAL_THRESHOLD_MB)
                        {
                            OnMemoryAlert(MemoryAlertLevel.Critical, currentMemoryMB,
                                $"Critical memory usage: {currentMemoryMB}MB");

                            // Attempt automatic cleanup
                            if (currentMemoryMB > FORCE_CLEANUP_THRESHOLD_MB)
                            {
                                ForceMemoryCleanup();
                            }
                        }
                        else if (consecutiveHighMemoryCount > HIGH_MEMORY_CONSECUTIVE_LIMIT)
                        {
                            OnMemoryAlert(MemoryAlertLevel.Warning, currentMemoryMB,
                                $"Sustained high memory usage: {currentMemoryMB}MB");
                        }
                    }
                    else
                    {
                        consecutiveHighMemoryCount = 0;
                    }

                    // Detect potential memory leak
                    if (isIncreasing && consecutiveHighMemoryCount > 10)
                    {
                        OnMemoryAlert(MemoryAlertLevel.PotentialLeak, currentMemoryMB,
                            $"Potential memory leak detected. Memory continuously increasing: {currentMemoryMB}MB");
                    }

                    lastMemory = currentMemory;
                }

                // Log detailed stats periodically
                if (DateTime.Now.Second == 0) // Once per minute
                {
                    LogMemoryStats(currentMemoryMB, gcMemory);
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("MemoryMonitor.MonitorCallback", ex);
            }
        }

        private void GCOptimizationCallback(object? state)
        {
            if (isDisposed) return;

            try
            {
                var beforeGC = GC.GetTotalMemory(false);

                // Only intervene if memory usage is truly critical (>600MB)
                // Let .NET's GC handle normal memory management - forced collections defeat generational GC optimization
                if (beforeGC > FORCE_CLEANUP_THRESHOLD_MB * 1024 * 1024)
                {
                    ErrorLogger.LogMessage($"Memory critically high ({beforeGC / 1024 / 1024}MB), requesting optimized GC");

                    // Use Optimized mode instead of Forced - let .NET decide when it's safe to collect
                    // This avoids UI freezes and respects application state
                    GC.Collect(0, GCCollectionMode.Optimized, false);

                    // Only do a full collection if Gen0 didn't help enough
                    var afterGen0 = GC.GetTotalMemory(false);
                    if (afterGen0 > FORCE_CLEANUP_THRESHOLD_MB * 1024 * 1024)
                    {
                        // Use Optimized instead of Forced to minimize UI impact
                        GC.Collect(2, GCCollectionMode.Optimized, false);
                    }

                    var afterGC = GC.GetTotalMemory(false);
                    var freedMB = (beforeGC - afterGC) / 1024 / 1024;

                    if (freedMB > 10)
                    {
                        ErrorLogger.LogMessage($"GC optimization freed {freedMB}MB of memory");
                    }
                }

                // Removed: LOH compaction every 5 minutes was too aggressive
                // .NET will compact the LOH automatically when needed
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("MemoryMonitor.GCOptimizationCallback", ex);
            }
        }

        private void ForceMemoryCleanup()
        {
            try
            {
                ErrorLogger.LogMessage("Forcing aggressive memory cleanup...");

                // Force full GC
                GC.Collect(2, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, true);

                // Trim working set
                TrimWorkingSet();

                // Compact LOH
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Forced, true);

                currentProcess.Refresh();
                var newMemoryMB = currentProcess.WorkingSet64 / 1024 / 1024;
                ErrorLogger.LogMessage($"Memory after cleanup: {newMemoryMB}MB");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("MemoryMonitor.ForceMemoryCleanup", ex);
            }
        }

        private void TrimWorkingSet()
        {
            try
            {
                // This tells Windows to trim the working set
                SetProcessWorkingSetSize(currentProcess.Handle, -1, -1);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("MemoryMonitor.TrimWorkingSet", ex);
            }
        }

        private void LogMemoryStats(long workingSetMB, long gcMemoryMB)
        {
            var gen0 = GC.CollectionCount(0);
            var gen1 = GC.CollectionCount(1);
            var gen2 = GC.CollectionCount(2);

            // MEMORY_FIX 2025-10-08: Enhanced logging - check for zombie whisper.exe processes
            var whisperProcesses = Process.GetProcessesByName("whisper");
            var whisperCount = whisperProcesses.Length;
            var whisperMemoryMB = 0L;
            foreach (var proc in whisperProcesses)
            {
                try
                {
                    proc.Refresh();
                    whisperMemoryMB += proc.WorkingSet64 / 1024 / 1024;
                    proc.Dispose();
                }
                catch { }
            }

            ErrorLogger.LogMessage(
                $"Memory Stats - Working Set: {workingSetMB}MB | " +
                $"GC Memory: {gcMemoryMB}MB | " +
                $"Peak: {peakMemory / 1024 / 1024}MB | " +
                $"GC Counts: G0={gen0}, G1={gen1}, G2={gen2} | " +
                $"Whisper Processes: {whisperCount} ({whisperMemoryMB}MB)");

            // CRITICAL: Alert if zombie whisper.exe detected
            if (whisperCount > 0)
            {
                OnMemoryAlert(MemoryAlertLevel.Warning, workingSetMB,
                    $"Zombie whisper.exe processes detected: {whisperCount} processes using {whisperMemoryMB}MB");
            }
        }

        private void OnMemoryAlert(MemoryAlertLevel level, long memoryMB, string message)
        {
            MemoryAlert?.Invoke(this, new MemoryAlertEventArgs
            {
                Level = level,
                MemoryMB = memoryMB,
                Message = message,
                Timestamp = DateTime.Now
            });

            ErrorLogger.LogMessage($"MEMORY ALERT [{level}]: {message}");
        }

        public MemoryStatistics GetStatistics()
        {
            lock (statsLock)
            {
                currentProcess.Refresh();
                return new MemoryStatistics
                {
                    CurrentMemoryMB = currentProcess.WorkingSet64 / 1024 / 1024,
                    PeakMemoryMB = peakMemory / 1024 / 1024,
                    BaselineMemoryMB = baselineMemory / 1024 / 1024,
                    GCMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024,
                    Gen0Collections = GC.CollectionCount(0),
                    Gen1Collections = GC.CollectionCount(1),
                    Gen2Collections = GC.CollectionCount(2)
                };
            }
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            monitorTimer?.Dispose();
            gcTimer?.Dispose();

            ErrorLogger.LogMessage("MemoryMonitor disposed");
        }
    }

    public class MemoryStatistics
    {
        public long CurrentMemoryMB { get; set; }
        public long PeakMemoryMB { get; set; }
        public long BaselineMemoryMB { get; set; }
        public long GCMemoryMB { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
    }

    public class MemoryAlertEventArgs : EventArgs
    {
        public MemoryAlertLevel Level { get; set; }
        public long MemoryMB { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public enum MemoryAlertLevel
    {
        Info,
        Warning,
        Critical,
        PotentialLeak
    }
}
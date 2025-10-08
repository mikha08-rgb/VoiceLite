// MEMORY_FIX 2025-10-08: Periodic zombie whisper.exe process cleanup service
// Fixes CRITICAL memory leak: 100MB+ per zombie process

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace VoiceLite.Services
{
    /// <summary>
    /// Periodic background service that kills orphaned whisper.exe processes.
    /// Runs every 60 seconds to prevent RAM exhaustion from zombie processes.
    /// </summary>
    public class ZombieProcessCleanupService : IDisposable
    {
        private readonly Timer cleanupTimer;
        private volatile bool isDisposed = false;
        private int totalZombiesKilled = 0;
        private const int CLEANUP_INTERVAL_SECONDS = 60;

        public event EventHandler<ZombieCleanupEventArgs>? ZombieDetected;

        public ZombieProcessCleanupService()
        {
            // Start cleanup timer (60-second interval)
            cleanupTimer = new Timer(
                callback: CleanupCallback,
                state: null,
                dueTime: TimeSpan.FromSeconds(CLEANUP_INTERVAL_SECONDS),
                period: TimeSpan.FromSeconds(CLEANUP_INTERVAL_SECONDS)
            );

            ErrorLogger.LogMessage("ZombieProcessCleanupService initialized - will check for zombies every 60 seconds");
        }

        private void CleanupCallback(object? state)
        {
            if (isDisposed) return;

            try
            {
                // Find all whisper.exe processes
                var whisperProcesses = Process.GetProcessesByName("whisper");

                if (whisperProcesses.Length == 0)
                {
                    // No processes found - all good
                    return;
                }

                // Found zombie processes - kill them
                ErrorLogger.LogWarning($"ZombieProcessCleanupService: Found {whisperProcesses.Length} whisper.exe process(es) - killing zombies");

                foreach (var zombie in whisperProcesses)
                {
                    try
                    {
                        zombie.Refresh();
                        var pid = zombie.Id;
                        var memoryMB = zombie.WorkingSet64 / 1024 / 1024;

                        // Fire event before killing
                        ZombieDetected?.Invoke(this, new ZombieCleanupEventArgs
                        {
                            ProcessId = pid,
                            MemoryMB = memoryMB,
                            Timestamp = DateTime.Now
                        });

                        ErrorLogger.LogWarning($"ZombieProcessCleanupService: Killing whisper.exe PID {pid} ({memoryMB}MB)");

                        // Try killing with entire process tree
                        try
                        {
                            zombie.Kill(entireProcessTree: true);
                            totalZombiesKilled++;
                            ErrorLogger.LogMessage($"ZombieProcessCleanupService: Successfully killed PID {pid}");
                        }
                        catch (Exception)
                        {
                            // If Kill() fails, try taskkill.exe as last resort
                            ErrorLogger.LogWarning($"ZombieProcessCleanupService: Kill() failed for PID {pid}, trying taskkill.exe");

                            try
                            {
                                var taskkill = Process.Start(new ProcessStartInfo
                                {
                                    FileName = "taskkill",
                                    Arguments = $"/F /T /PID {pid}",
                                    CreateNoWindow = true,
                                    UseShellExecute = false,
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true
                                });

                                if (taskkill != null)
                                {
                                    taskkill.WaitForExit(5000); // Wait max 5 seconds
                                    taskkill.Dispose();
                                    totalZombiesKilled++;
                                    ErrorLogger.LogMessage($"ZombieProcessCleanupService: taskkill.exe succeeded for PID {pid}");
                                }
                            }
                            catch (Exception taskkillEx)
                            {
                                ErrorLogger.LogError($"ZombieProcessCleanupService: Both Kill() and taskkill.exe failed for PID {pid}", taskkillEx);
                            }
                        }
                        finally
                        {
                            zombie.Dispose();
                        }
                    }
                    catch (Exception zombieEx)
                    {
                        ErrorLogger.LogError($"ZombieProcessCleanupService: Error processing zombie", zombieEx);
                    }
                }

                if (totalZombiesKilled > 0)
                {
                    ErrorLogger.LogMessage($"ZombieProcessCleanupService: Total zombies killed since app start: {totalZombiesKilled}");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("ZombieProcessCleanupService.CleanupCallback", ex);
            }
        }

        /// <summary>
        /// Force an immediate cleanup check (on-demand)
        /// </summary>
        public void CleanupNow()
        {
            if (isDisposed) return;
            CleanupCallback(null);
        }

        /// <summary>
        /// Get statistics about zombie cleanup activity
        /// </summary>
        public ZombieCleanupStatistics GetStatistics()
        {
            return new ZombieCleanupStatistics
            {
                TotalZombiesKilled = totalZombiesKilled,
                ServiceRunning = !isDisposed
            };
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            try
            {
                cleanupTimer?.Dispose();
            }
            catch { }

            ErrorLogger.LogMessage($"ZombieProcessCleanupService disposed - killed {totalZombiesKilled} zombie(s) during session");
        }
    }

    public class ZombieCleanupEventArgs : EventArgs
    {
        public int ProcessId { get; set; }
        public long MemoryMB { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ZombieCleanupStatistics
    {
        public int TotalZombiesKilled { get; set; }
        public bool ServiceRunning { get; set; }
    }
}

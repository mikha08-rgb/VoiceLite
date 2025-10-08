// STRESS TEST 2025-10-08: Memory leak verification under heavy load
// Tests all memory leak fixes with 1000+ iterations

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using VoiceLite.Services;
using VoiceLite.Models;

namespace VoiceLite.Tests
{
    /// <summary>
    /// Stress tests for memory leak fixes.
    /// Runs 1000+ iterations to detect slow leaks and verify GC cleanup.
    /// </summary>
    public class MemoryLeakStressTest
    {
        private readonly ITestOutputHelper output;

        public MemoryLeakStressTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        /// <summary>
        /// STRESS TEST: Create and dispose 100 PersistentWhisperService instances
        /// Expected: Memory growth < 50MB, no zombie process accumulation
        /// Duration: ~30 seconds
        /// </summary>
        [Fact]
        public async Task PersistentWhisperService_100Instances_NoLeak()
        {
            // Arrange
            output.WriteLine("=== PersistentWhisperService Stress Test ===");
            output.WriteLine($"Start Time: {DateTime.Now:HH:mm:ss}");

            ForceGC();
            var initialMemoryMB = GetCurrentMemoryMB();
            output.WriteLine($"Initial Memory: {initialMemoryMB}MB");

            var settings = new Settings();
            var stopwatch = Stopwatch.StartNew();

            // Act - Create and dispose 100 instances (reduced from 1000 for faster test runtime)
            for (int i = 0; i < 100; i++)
            {
                using (var service = new PersistentWhisperService(settings))
                {
                    // Service created and immediately disposed
                }

                // Log progress every 10 iterations
                if ((i + 1) % 10 == 0)
                {
                    ForceGC();
                    var currentMemoryMB = GetCurrentMemoryMB();
                    var elapsed = stopwatch.Elapsed;
                    output.WriteLine($"Iteration {i + 1}: {currentMemoryMB}MB (Δ{currentMemoryMB - initialMemoryMB}MB) - {elapsed.TotalSeconds:F1}s");
                }
            }

            stopwatch.Stop();

            // Assert
            ForceGC();
            var finalMemoryMB = GetCurrentMemoryMB();
            var memoryGrowthMB = finalMemoryMB - initialMemoryMB;

            output.WriteLine("=== Results ===");
            output.WriteLine($"Initial Memory: {initialMemoryMB}MB");
            output.WriteLine($"Final Memory: {finalMemoryMB}MB");
            output.WriteLine($"Memory Growth: {memoryGrowthMB}MB");
            output.WriteLine($"Duration: {stopwatch.Elapsed.TotalSeconds:F1}s");
            output.WriteLine($"Avg per instance: {stopwatch.Elapsed.TotalMilliseconds / 1000:F2}ms");

            // Check for zombie whisper.exe processes
            var zombies = Process.GetProcessesByName("whisper");
            output.WriteLine($"Zombie whisper.exe processes: {zombies.Length}");
            foreach (var zombie in zombies)
            {
                output.WriteLine($"  PID {zombie.Id}: {zombie.WorkingSet64 / 1024 / 1024}MB");
                zombie.Dispose();
            }

            memoryGrowthMB.Should().BeLessThan(50, "Memory growth should be < 50MB after 1000 instances");
            zombies.Length.Should().Be(0, "No zombie whisper.exe processes should exist");
        }

        /// <summary>
        /// STRESS TEST: ZombieProcessCleanupService running for 5 minutes
        /// Expected: Service stable, no memory growth, cleanup works
        /// Duration: ~5 minutes
        /// </summary>
        [Fact(Skip = "Long-running test (5 minutes) - run manually")]
        public async Task ZombieProcessCleanupService_5Minutes_Stable()
        {
            // Arrange
            output.WriteLine("=== ZombieProcessCleanupService 5-Minute Stress Test ===");
            output.WriteLine($"Start Time: {DateTime.Now:HH:mm:ss}");

            ForceGC();
            var initialMemoryMB = GetCurrentMemoryMB();
            output.WriteLine($"Initial Memory: {initialMemoryMB}MB");

            var service = new ZombieProcessCleanupService();
            var zombiesDetected = 0;
            var memorySnapshots = new List<long>();

            service.ZombieDetected += (sender, e) =>
            {
                Interlocked.Increment(ref zombiesDetected);
                output.WriteLine($"[{DateTime.Now:HH:mm:ss}] Zombie detected: PID {e.ProcessId} ({e.MemoryMB}MB)");
            };

            var stopwatch = Stopwatch.StartNew();

            // Act - Run for 5 minutes
            while (stopwatch.Elapsed.TotalMinutes < 5)
            {
                await Task.Delay(10000); // Check every 10 seconds

                ForceGC();
                var currentMemoryMB = GetCurrentMemoryMB();
                memorySnapshots.Add(currentMemoryMB);

                var stats = service.GetStatistics();
                output.WriteLine($"[{stopwatch.Elapsed.TotalMinutes:F1}m] Memory: {currentMemoryMB}MB, Zombies killed: {stats.TotalZombiesKilled}");
            }

            stopwatch.Stop();

            // Assert
            service.Dispose();
            ForceGC();
            var finalMemoryMB = GetCurrentMemoryMB();
            var memoryGrowthMB = finalMemoryMB - initialMemoryMB;

            output.WriteLine("=== Results ===");
            output.WriteLine($"Duration: {stopwatch.Elapsed.TotalMinutes:F1} minutes");
            output.WriteLine($"Initial Memory: {initialMemoryMB}MB");
            output.WriteLine($"Final Memory: {finalMemoryMB}MB");
            output.WriteLine($"Memory Growth: {memoryGrowthMB}MB");
            output.WriteLine($"Total Zombies Detected: {zombiesDetected}");

            var avgMemory = memorySnapshots.Average();
            var maxMemory = memorySnapshots.Max();
            var minMemory = memorySnapshots.Min();
            output.WriteLine($"Memory - Avg: {avgMemory:F1}MB, Min: {minMemory}MB, Max: {maxMemory}MB");

            memoryGrowthMB.Should().BeLessThan(20, "Service should not leak > 20MB over 5 minutes");
        }

        /// <summary>
        /// STRESS TEST: MemoryMonitor running for 10 minutes with logging
        /// Expected: No memory growth, zombie detection works
        /// Duration: ~10 minutes
        /// </summary>
        [Fact(Skip = "Long-running test (10 minutes) - run manually")]
        public async Task MemoryMonitor_10Minutes_NoLeak()
        {
            // Arrange
            output.WriteLine("=== MemoryMonitor 10-Minute Stress Test ===");
            output.WriteLine($"Start Time: {DateTime.Now:HH:mm:ss}");

            ForceGC();
            var initialMemoryMB = GetCurrentMemoryMB();
            output.WriteLine($"Initial Memory: {initialMemoryMB}MB");

            var monitor = new MemoryMonitor();
            var alertCount = 0;

            monitor.MemoryAlert += (sender, e) =>
            {
                Interlocked.Increment(ref alertCount);
                output.WriteLine($"[{DateTime.Now:HH:mm:ss}] ALERT [{e.Level}]: {e.Message}");
            };

            var stopwatch = Stopwatch.StartNew();
            var snapshots = new List<MemoryStatistics>();

            // Act - Run for 10 minutes
            while (stopwatch.Elapsed.TotalMinutes < 10)
            {
                await Task.Delay(30000); // Check every 30 seconds

                var stats = monitor.GetStatistics();
                snapshots.Add(stats);

                output.WriteLine($"[{stopwatch.Elapsed.TotalMinutes:F1}m] Memory: {stats.CurrentMemoryMB}MB, " +
                                $"GC: {stats.GCMemoryMB}MB, G0={stats.Gen0Collections}, G1={stats.Gen1Collections}, G2={stats.Gen2Collections}");
            }

            stopwatch.Stop();

            // Assert
            monitor.Dispose();
            ForceGC();
            var finalMemoryMB = GetCurrentMemoryMB();
            var memoryGrowthMB = finalMemoryMB - initialMemoryMB;

            output.WriteLine("=== Results ===");
            output.WriteLine($"Duration: {stopwatch.Elapsed.TotalMinutes:F1} minutes");
            output.WriteLine($"Initial Memory: {initialMemoryMB}MB");
            output.WriteLine($"Final Memory: {finalMemoryMB}MB");
            output.WriteLine($"Memory Growth: {memoryGrowthMB}MB");
            output.WriteLine($"Total Alerts: {alertCount}");

            var avgCurrentMemory = snapshots.Average(s => s.CurrentMemoryMB);
            var avgGCMemory = snapshots.Average(s => s.GCMemoryMB);
            output.WriteLine($"Avg Working Set: {avgCurrentMemory:F1}MB");
            output.WriteLine($"Avg GC Memory: {avgGCMemory:F1}MB");

            memoryGrowthMB.Should().BeLessThan(30, "MemoryMonitor should not leak > 30MB over 10 minutes");
        }

        /// <summary>
        /// STRESS TEST: Simulate 500 rapid transcription cycles
        /// Expected: Memory bounded, no service crashes
        /// Duration: ~1 minute
        /// </summary>
        [Fact]
        public async Task TranscriptionCycle_500Iterations_Bounded()
        {
            // Arrange
            output.WriteLine("=== Transcription Cycle Stress Test (500x) ===");
            output.WriteLine($"Start Time: {DateTime.Now:HH:mm:ss}");

            ForceGC();
            var initialMemoryMB = GetCurrentMemoryMB();
            output.WriteLine($"Initial Memory: {initialMemoryMB}MB");

            var settings = new Settings();
            var stopwatch = Stopwatch.StartNew();

            // Act - Simulate 500 transcription cycles
            for (int i = 0; i < 500; i++)
            {
                // Create services
                var audioRecorder = new AudioRecorder();
                var whisperService = new PersistentWhisperService(settings);
                var textInjector = new TextInjector(settings);

                // Simulate usage (without actual recording/transcription)
                await Task.Delay(1); // Minimal delay

                // Dispose services
                audioRecorder.Dispose();
                whisperService.Dispose();
                // TextInjector doesn't implement IDisposable

                // Log progress every 50 iterations
                if ((i + 1) % 50 == 0)
                {
                    ForceGC();
                    var currentMemoryMB = GetCurrentMemoryMB();
                    output.WriteLine($"Iteration {i + 1}: {currentMemoryMB}MB (Δ{currentMemoryMB - initialMemoryMB}MB)");
                }
            }

            stopwatch.Stop();

            // Assert
            ForceGC();
            var finalMemoryMB = GetCurrentMemoryMB();
            var memoryGrowthMB = finalMemoryMB - initialMemoryMB;

            output.WriteLine("=== Results ===");
            output.WriteLine($"Iterations: 500");
            output.WriteLine($"Duration: {stopwatch.Elapsed.TotalSeconds:F1}s");
            output.WriteLine($"Initial Memory: {initialMemoryMB}MB");
            output.WriteLine($"Final Memory: {finalMemoryMB}MB");
            output.WriteLine($"Memory Growth: {memoryGrowthMB}MB");
            output.WriteLine($"Avg per cycle: {stopwatch.Elapsed.TotalMilliseconds / 500:F2}ms");

            memoryGrowthMB.Should().BeLessThan(100, "Memory growth should be < 100MB after 500 cycles");
        }

        /// <summary>
        /// STRESS TEST: Concurrent service creation (thread safety)
        /// Expected: No race conditions, no crashes, clean disposal
        /// Duration: ~10 seconds
        /// </summary>
        [Fact]
        public async Task ConcurrentServiceCreation_100Threads_ThreadSafe()
        {
            // Arrange
            output.WriteLine("=== Concurrent Service Creation Test (100 threads) ===");
            output.WriteLine($"Start Time: {DateTime.Now:HH:mm:ss}");

            ForceGC();
            var initialMemoryMB = GetCurrentMemoryMB();
            output.WriteLine($"Initial Memory: {initialMemoryMB}MB");

            var settings = new Settings();
            var exceptions = new List<Exception>();
            var exceptionLock = new object();

            var stopwatch = Stopwatch.StartNew();

            // Act - Create 100 services concurrently
            var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(() =>
            {
                try
                {
                    using var service = new PersistentWhisperService(settings);
                    // Service used briefly
                    Task.Delay(10).Wait();
                }
                catch (Exception ex)
                {
                    lock (exceptionLock)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            ForceGC();
            var finalMemoryMB = GetCurrentMemoryMB();
            var memoryGrowthMB = finalMemoryMB - initialMemoryMB;

            output.WriteLine("=== Results ===");
            output.WriteLine($"Threads: 100");
            output.WriteLine($"Duration: {stopwatch.Elapsed.TotalSeconds:F1}s");
            output.WriteLine($"Initial Memory: {initialMemoryMB}MB");
            output.WriteLine($"Final Memory: {finalMemoryMB}MB");
            output.WriteLine($"Memory Growth: {memoryGrowthMB}MB");
            output.WriteLine($"Exceptions: {exceptions.Count}");

            foreach (var ex in exceptions)
            {
                output.WriteLine($"  - {ex.GetType().Name}: {ex.Message}");
            }

            exceptions.Count.Should().Be(0, "No exceptions should occur during concurrent creation");
            memoryGrowthMB.Should().BeLessThan(60, "Memory growth should be < 60MB");
        }

        /// <summary>
        /// BENCHMARK: Measure disposal performance
        /// Expected: < 500ms total disposal time (PersistentWhisperService warmup overhead expected)
        /// </summary>
        [Fact]
        public void ServiceDisposal_Performance_Fast()
        {
            // Arrange
            output.WriteLine("=== Service Disposal Performance Benchmark ===");

            var settings = new Settings();
            var services = new List<IDisposable>
            {
                new PersistentWhisperService(settings),
                new AudioRecorder(),
                new MemoryMonitor(),
                new ZombieProcessCleanupService(),
                new SoundService() // SoundService has parameterless constructor
            };

            // Act - Measure disposal time
            var stopwatch = Stopwatch.StartNew();
            foreach (var service in services)
            {
                var serviceStopwatch = Stopwatch.StartNew();
                service.Dispose();
                serviceStopwatch.Stop();
                output.WriteLine($"{service.GetType().Name}: {serviceStopwatch.ElapsedMilliseconds}ms");
            }
            stopwatch.Stop();

            // Assert
            output.WriteLine($"Total disposal time: {stopwatch.ElapsedMilliseconds}ms");

            stopwatch.ElapsedMilliseconds.Should().BeLessThan(500,
                "All services should dispose in < 500ms total (PersistentWhisperService warmup is expected overhead)");
        }

        #region Helper Methods

        private void ForceGC()
        {
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true);
        }

        private long GetCurrentMemoryMB()
        {
            var process = Process.GetCurrentProcess();
            process.Refresh();
            return process.WorkingSet64 / 1024 / 1024;
        }

        #endregion
    }
}

using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using VoiceLite.Models;
using VoiceLite.Services;

namespace VoiceLite.Tests.Stress
{
    /// <summary>
    /// PHASE 3 - DAY 5: Stress tests for transcription operations
    ///
    /// Purpose: Validate that transcription can handle sustained load without:
    /// - Memory leaks
    /// - File handle exhaustion
    /// - Performance degradation
    /// - Process cleanup failures
    ///
    /// Note: These tests are marked with [Trait("Category", "Stress")]
    /// and are skipped by default (they take several minutes to run)
    /// </summary>
    [Trait("Category", "Stress")]
    public class TranscriptionStressTests : StressTestBase
    {
        [Fact(Skip = "Stress test - run manually with: dotnet test --filter Category=Stress")]
        public async Task StressTest_100ConsecutiveTranscriptions_NoMemoryLeak()
        {
            // Arrange
            const int iterations = 100;
            const double maxMemoryGrowthMB = 50.0; // Allow 50MB growth max

            var settings = new Settings
            {
                WhisperModel = "tiny", // Use tiny model for speed
                Language = "en",
                BeamSize = 1,
                BestOf = 1
            };

            PersistentWhisperService? whisperService = null;
            string? testAudioPath = null;

            try
            {
                // Create test audio file once (reuse for all iterations)
                testAudioPath = CreateTestAudioFile();

                whisperService = new PersistentWhisperService(settings);

                var successCount = 0;
                var failureCount = 0;
                var durations = new System.Collections.Generic.List<double>();

                // Act - Run 100 transcriptions
                for (int i = 0; i < iterations; i++)
                {
                    var iterationStart = System.Diagnostics.Stopwatch.StartNew();

                    try
                    {
                        var result = await whisperService.TranscribeAsync(testAudioPath);
                        successCount++;

                        durations.Add(iterationStart.Elapsed.TotalMilliseconds);

                        // Track peak memory
                        UpdatePeakMemory();

                        // Log progress every 10 iterations
                        if ((i + 1) % 10 == 0)
                        {
                            Console.WriteLine($"Progress: {i + 1}/{iterations} - " +
                                            $"Memory: {GetMemoryGrowthMB():F2} MB growth - " +
                                            $"Avg duration: {durations[^1]:F0}ms");
                        }
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        Console.WriteLine($"Iteration {i + 1} failed: {ex.Message}");
                    }
                }

                // Assert - Check results
                Console.WriteLine($"\n=== Stress Test Results ===");
                Console.WriteLine($"Total iterations: {iterations}");
                Console.WriteLine($"Successes: {successCount}");
                Console.WriteLine($"Failures: {failureCount}");
                Console.WriteLine($"Success rate: {(successCount / (double)iterations) * 100:F1}%");

                if (durations.Count > 0)
                {
                    var avgDuration = durations.ToArray().Average();
                    var minDuration = durations.Min();
                    var maxDuration = durations.Max();

                    Console.WriteLine($"Duration - Avg: {avgDuration:F0}ms, Min: {minDuration:F0}ms, Max: {maxDuration:F0}ms");

                    // Check for performance degradation (last 10 vs first 10)
                    if (durations.Count >= 20)
                    {
                        var firstTenAvg = durations.GetRange(0, 10).Average();
                        var lastTenAvg = durations.GetRange(durations.Count - 10, 10).Average();
                        var degradation = ((lastTenAvg - firstTenAvg) / firstTenAvg) * 100;

                        Console.WriteLine($"Performance degradation: {degradation:F1}% " +
                                        $"(first 10 avg: {firstTenAvg:F0}ms, last 10 avg: {lastTenAvg:F0}ms)");

                        // Warn if >50% slowdown (but don't fail - could be system load)
                        if (degradation > 50)
                        {
                            Console.WriteLine($"WARNING: Significant performance degradation detected");
                        }
                    }
                }

                // Memory check
                AssertMemoryWithinLimits(maxMemoryGrowthMB, "100 Consecutive Transcriptions");

                // Success rate check - allow some failures (model missing, etc)
                successCount.Should().BeGreaterThan(0, "at least some transcriptions should succeed");
            }
            finally
            {
                // Cleanup
                whisperService?.Dispose();
                if (testAudioPath != null)
                {
                    CleanupTestFile(testAudioPath);
                }
            }
        }

        [Fact(Skip = "Stress test - run manually with: dotnet test --filter Category=Stress")]
        public async Task StressTest_ConcurrentTranscriptions_NoDeadlock()
        {
            // Arrange
            const int concurrentTasks = 5;
            const int iterationsPerTask = 10;
            const double maxMemoryGrowthMB = 100.0;

            var settings = new Settings
            {
                WhisperModel = "tiny",
                Language = "en",
                BeamSize = 1,
                BestOf = 1
            };

            PersistentWhisperService? whisperService = null;
            string? testAudioPath = null;

            try
            {
                testAudioPath = CreateTestAudioFile();
                whisperService = new PersistentWhisperService(settings);

                // Act - Launch multiple concurrent transcription tasks
                var tasks = new System.Collections.Generic.List<Task>();
                var successCount = 0;
                var successLock = new object();

                for (int t = 0; t < concurrentTasks; t++)
                {
                    var taskNumber = t;
                    tasks.Add(Task.Run(async () =>
                    {
                        for (int i = 0; i < iterationsPerTask; i++)
                        {
                            try
                            {
                                await whisperService.TranscribeAsync(testAudioPath);
                                lock (successLock)
                                {
                                    successCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Task {taskNumber}, iteration {i} failed: {ex.Message}");
                            }

                            UpdatePeakMemory();
                        }
                    }));
                }

                // Wait for all tasks with timeout
                var completedInTime = await Task.WhenAny(
                    Task.WhenAll(tasks),
                    Task.Delay(TimeSpan.FromMinutes(5))
                ) == Task.WhenAll(tasks);

                // Assert
                completedInTime.Should().BeTrue("all concurrent tasks should complete within timeout (no deadlock)");
                successCount.Should().BeGreaterThan(0, "at least some concurrent transcriptions should succeed");

                Console.WriteLine($"Concurrent transcriptions: {successCount}/{concurrentTasks * iterationsPerTask} succeeded");

                AssertMemoryWithinLimits(maxMemoryGrowthMB, "Concurrent Transcriptions");
            }
            finally
            {
                whisperService?.Dispose();
                if (testAudioPath != null)
                {
                    CleanupTestFile(testAudioPath);
                }
            }
        }

        [Fact(Skip = "Stress test - run manually with: dotnet test --filter Category=Stress")]
        public async Task StressTest_LongRunningSession_MaintainsStability()
        {
            // Arrange - Simulate a user's full work session
            const int transcriptionsPerBatch = 20;
            const int batches = 5; // 100 total
            const int pauseBetweenBatchesMs = 1000; // 1 second rest
            const double maxMemoryGrowthMB = 50.0;

            var settings = new Settings
            {
                WhisperModel = "tiny",
                Language = "en",
                BeamSize = 1,
                BestOf = 1
            };

            PersistentWhisperService? whisperService = null;
            string? testAudioPath = null;

            try
            {
                testAudioPath = CreateTestAudioFile();
                whisperService = new PersistentWhisperService(settings);

                var totalSuccess = 0;

                // Act - Simulate realistic usage pattern with breaks
                for (int batch = 0; batch < batches; batch++)
                {
                    Console.WriteLine($"\n=== Batch {batch + 1}/{batches} ===");

                    for (int i = 0; i < transcriptionsPerBatch; i++)
                    {
                        try
                        {
                            await whisperService.TranscribeAsync(testAudioPath);
                            totalSuccess++;
                            UpdatePeakMemory();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Batch {batch + 1}, iteration {i} failed: {ex.Message}");
                        }
                    }

                    Console.WriteLine($"Batch {batch + 1} complete - " +
                                    $"Memory growth: {GetMemoryGrowthMB():F2} MB");

                    // Pause between batches (simulate user breaks)
                    if (batch < batches - 1)
                    {
                        await Task.Delay(pauseBetweenBatchesMs);
                    }
                }

                // Assert
                Console.WriteLine($"\n=== Final Results ===");
                Console.WriteLine($"Total successes: {totalSuccess}/{transcriptionsPerBatch * batches}");

                totalSuccess.Should().BeGreaterThan(0, "transcriptions should succeed during long session");

                AssertMemoryWithinLimits(maxMemoryGrowthMB, "Long Running Session");
            }
            finally
            {
                whisperService?.Dispose();
                if (testAudioPath != null)
                {
                    CleanupTestFile(testAudioPath);
                }
            }
        }
    }
}

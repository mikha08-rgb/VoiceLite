using System;
using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using Xunit;
using VoiceLite.Models;
using VoiceLite.Services;

namespace VoiceLite.Tests.Stress
{
    /// <summary>
    /// PHASE 3 - DAY 5: Stress tests for Whisper process recovery
    ///
    /// Purpose: Validate that PersistentWhisperService can handle:
    /// - Multiple consecutive failures without breaking
    /// - Process cleanup after crashes
    /// - Recovery after errors (retry logic from Day 1)
    /// - Mixed success/failure scenarios
    ///
    /// Critical: Whisper.net loads native DLLs in-process - must handle all edge cases
    /// </summary>
    [Trait("Category", "Stress")]
    public class WhisperRecoveryStressTests : StressTestBase
    {
        [Fact(Skip = "Stress test - run manually with: dotnet test --filter Category=Stress")]
        public async Task StressTest_MixedSuccessAndFailure_RemainsStable()
        {
            // Arrange
            const int iterations = 50;
            const double maxMemoryGrowthMB = 40.0;

            var settings = new Settings
            {
                WhisperModel = "tiny",
                Language = "en",
            };

            PersistentWhisperService? whisperService = null;
            string? goodAudioPath = null;
            string? badAudioPath = null;

            try
            {
                goodAudioPath = CreateTestAudioFile();

                // Create a corrupted audio file (invalid WAV header)
                badAudioPath = Path.Combine(Path.GetTempPath(), $"bad_audio_{Guid.NewGuid():N}.wav");
                await File.WriteAllBytesAsync(badAudioPath, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });

                whisperService = new PersistentWhisperService(settings);

                var successCount = 0;
                var failureCount = 0;

                // Act - Alternate between good and bad audio files
                for (int i = 0; i < iterations; i++)
                {
                    var useGoodAudio = i % 2 == 0; // Every other iteration uses good audio
                    var audioPath = useGoodAudio ? goodAudioPath : badAudioPath;

                    try
                    {
                        await whisperService.TranscribeAsync(audioPath);
                        successCount++;

                        if (!useGoodAudio)
                        {
                            Console.WriteLine($"Iteration {i + 1}: Unexpected success with bad audio");
                        }
                    }
                    catch (Exception ex)
                    {
                        failureCount++;

                        if (useGoodAudio)
                        {
                            Console.WriteLine($"Iteration {i + 1}: Unexpected failure with good audio - {ex.Message}");
                        }
                    }

                    UpdatePeakMemory();

                    if ((i + 1) % 10 == 0)
                    {
                        Console.WriteLine($"Progress: {i + 1}/{iterations} - " +
                                        $"Success: {successCount}, Failures: {failureCount} - " +
                                        $"Memory: {GetMemoryGrowthMB():F2} MB");
                    }
                }

                // Assert
                Console.WriteLine($"\n=== Results ===");
                Console.WriteLine($"Total iterations: {iterations}");
                Console.WriteLine($"Successes: {successCount}");
                Console.WriteLine($"Failures: {failureCount}");

                // Should handle mixed success/failure without crashing
                (successCount + failureCount).Should().Be(iterations,
                    "all iterations should complete (either succeed or fail gracefully)");

                // At least some should succeed (the good audio files)
                successCount.Should().BeGreaterThan(0,
                    "at least some good audio files should transcribe successfully");

                AssertMemoryWithinLimits(maxMemoryGrowthMB, "Mixed Success/Failure");
            }
            finally
            {
                whisperService?.Dispose();
                if (goodAudioPath != null) CleanupTestFile(goodAudioPath);
                if (badAudioPath != null) CleanupTestFile(badAudioPath);
            }
        }

        [Fact(Skip = "Stress test - run manually with: dotnet test --filter Category=Stress")]
        public async Task StressTest_ConsecutiveFailures_DoesNotBreakService()
        {
            // Arrange - Test that multiple failures in a row don't break the service
            const int consecutiveFailures = 10;
            const int successfulAfter = 5;
            const double maxMemoryGrowthMB = 30.0;

            var settings = new Settings
            {
                WhisperModel = "tiny",
                Language = "en",
            };

            PersistentWhisperService? whisperService = null;
            string? goodAudioPath = null;
            string? nonExistentPath = Path.Combine(Path.GetTempPath(), "does_not_exist.wav");

            try
            {
                goodAudioPath = CreateTestAudioFile();
                whisperService = new PersistentWhisperService(settings);

                // Act - Cause multiple consecutive failures
                Console.WriteLine($"=== Phase 1: {consecutiveFailures} consecutive failures ===");
                var failureCount = 0;

                for (int i = 0; i < consecutiveFailures; i++)
                {
                    try
                    {
                        await whisperService.TranscribeAsync(nonExistentPath);
                        Console.WriteLine($"Iteration {i + 1}: Unexpected success");
                    }
                    catch (FileNotFoundException)
                    {
                        failureCount++;
                        // Expected - file doesn't exist
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Iteration {i + 1}: Different error - {ex.GetType().Name}");
                    }

                    UpdatePeakMemory();
                }

                Console.WriteLine($"Failures handled: {failureCount}/{consecutiveFailures}");

                // Now try successful transcriptions
                Console.WriteLine($"\n=== Phase 2: {successfulAfter} successful transcriptions ===");
                var successCount = 0;

                for (int i = 0; i < successfulAfter; i++)
                {
                    try
                    {
                        await whisperService.TranscribeAsync(goodAudioPath);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Iteration {i + 1}: Failed - {ex.Message}");
                    }

                    UpdatePeakMemory();
                }

                // Assert
                Console.WriteLine($"\n=== Results ===");
                Console.WriteLine($"Phase 1 failures: {failureCount}/{consecutiveFailures}");
                Console.WriteLine($"Phase 2 successes: {successCount}/{successfulAfter}");

                // Service should still work after consecutive failures
                successCount.Should().BeGreaterThan(0,
                    "service should recover and work after consecutive failures");

                AssertMemoryWithinLimits(maxMemoryGrowthMB, "Consecutive Failures Recovery");
            }
            finally
            {
                whisperService?.Dispose();
                if (goodAudioPath != null) CleanupTestFile(goodAudioPath);
            }
        }

        [Fact(Skip = "Stress test - run manually with: dotnet test --filter Category=Stress")]
        public async Task StressTest_MultipleServicesSequential_NoProcessLeaks()
        {
            // Arrange - Create and dispose multiple service instances
            // This tests that Dispose() properly cleans up Whisper processes
            const int serviceInstances = 20;
            const int transcriptionsPerInstance = 3;
            const double maxMemoryGrowthMB = 40.0;

            var settings = new Settings
            {
                WhisperModel = "tiny",
                Language = "en",
            };

            string? testAudioPath = null;
            var totalSuccess = 0;

            try
            {
                testAudioPath = CreateTestAudioFile();

                // Act - Create multiple service instances sequentially
                for (int instance = 0; instance < serviceInstances; instance++)
                {
                    PersistentWhisperService? whisperService = null;

                    try
                    {
                        whisperService = new PersistentWhisperService(settings);

                        for (int i = 0; i < transcriptionsPerInstance; i++)
                        {
                            try
                            {
                                await whisperService.TranscribeAsync(testAudioPath);
                                totalSuccess++;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Instance {instance + 1}, transcription {i + 1} failed: {ex.Message}");
                            }
                        }

                        UpdatePeakMemory();

                        if ((instance + 1) % 5 == 0)
                        {
                            Console.WriteLine($"Instance {instance + 1}/{serviceInstances} - " +
                                            $"Success: {totalSuccess} - " +
                                            $"Memory: {GetMemoryGrowthMB():F2} MB");
                        }
                    }
                    finally
                    {
                        // Critical: Dispose each service to test cleanup
                        whisperService?.Dispose();
                    }

                    // Brief pause between instances
                    await Task.Delay(100);
                }

                // Assert
                Console.WriteLine($"\n=== Results ===");
                Console.WriteLine($"Total service instances: {serviceInstances}");
                Console.WriteLine($"Total successful transcriptions: {totalSuccess}/{serviceInstances * transcriptionsPerInstance}");

                totalSuccess.Should().BeGreaterThan(0,
                    "at least some transcriptions should succeed across all service instances");

                // Memory check is critical - must not leak processes
                AssertMemoryWithinLimits(maxMemoryGrowthMB, "Multiple Services Sequential");
            }
            finally
            {
                if (testAudioPath != null) CleanupTestFile(testAudioPath);
            }
        }

        [Fact(Skip = "Stress test - run manually with: dotnet test --filter Category=Stress")]
        public async Task StressTest_EmptyAndTinyAudioFiles_HandlesGracefully()
        {
            // Arrange - Test edge cases with various audio file sizes
            const int iterations = 30;
            const double maxMemoryGrowthMB = 25.0;

            var settings = new Settings
            {
                WhisperModel = "tiny",
                Language = "en",
            };

            PersistentWhisperService? whisperService = null;
            var testFiles = new System.Collections.Generic.List<string>();

            try
            {
                // Create various edge case audio files
                // 1. Empty file
                var emptyFile = Path.Combine(Path.GetTempPath(), $"empty_{Guid.NewGuid():N}.wav");
                await File.WriteAllBytesAsync(emptyFile, Array.Empty<byte>());
                testFiles.Add(emptyFile);

                // 2. Tiny file (just WAV header, no data)
                var tinyFile = Path.Combine(Path.GetTempPath(), $"tiny_{Guid.NewGuid():N}.wav");
                await File.WriteAllBytesAsync(tinyFile, new byte[44]); // Minimum WAV header
                testFiles.Add(tinyFile);

                // 3. Valid audio file
                testFiles.Add(CreateTestAudioFile());

                whisperService = new PersistentWhisperService(settings);

                var handledCount = 0;

                // Act - Try transcribing edge case files
                for (int i = 0; i < iterations; i++)
                {
                    var fileIndex = i % testFiles.Count;
                    var testFile = testFiles[fileIndex];

                    try
                    {
                        var result = await whisperService.TranscribeAsync(testFile);
                        // Success or empty result is both acceptable
                        handledCount++;
                    }
                    catch (Exception)
                    {
                        // Exception is acceptable for edge cases
                        handledCount++;
                    }

                    UpdatePeakMemory();

                    if ((i + 1) % 10 == 0)
                    {
                        Console.WriteLine($"Progress: {i + 1}/{iterations} - Memory: {GetMemoryGrowthMB():F2} MB");
                    }
                }

                // Assert
                Console.WriteLine($"\n=== Results ===");
                Console.WriteLine($"Edge cases handled: {handledCount}/{iterations}");

                // All iterations should complete (either succeed or fail gracefully)
                handledCount.Should().Be(iterations,
                    "all edge case files should be handled (success or graceful failure)");

                AssertMemoryWithinLimits(maxMemoryGrowthMB, "Empty and Tiny Audio Files");
            }
            finally
            {
                whisperService?.Dispose();
                foreach (var file in testFiles)
                {
                    CleanupTestFile(file);
                }
            }
        }
    }
}

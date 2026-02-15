using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using Xunit;
using VoiceLite.Services;

namespace VoiceLite.Tests.Stress
{
    /// <summary>
    /// PHASE 3 - DAY 5: Stress tests for audio recording operations
    ///
    /// Purpose: Validate that AudioRecorder can handle:
    /// - Rapid start/stop cycles without leaks
    /// - Multiple instances without resource exhaustion
    /// - Disposal is properly cleanup resources
    ///
    /// Critical: These tests verify Phase 1 resource leak fixes hold up under load
    /// </summary>
    [Trait("Category", "Stress")]
    public class RecordingStressTests : StressTestBase
    {
        [Fact(Skip = "Stress test - run manually with: dotnet test --filter Category=Stress")]
        public async Task StressTest_RapidStartStopCycles_NoLeaks()
        {
            // Arrange
            const int cycles = 100;
            const int recordDurationMs = 100; // Very short recordings
            const double maxMemoryGrowthMB = 30.0; // AudioRecorder should be lightweight

            var successCount = 0;
            var failureCount = 0;

            // Act - Rapidly start and stop recording
            for (int i = 0; i < cycles; i++)
            {
                AudioRecorder? recorder = null;

                try
                {
                    recorder = new AudioRecorder();

                    // Start recording
                    recorder.StartRecording();

                    // Record for short duration
                    await Task.Delay(recordDurationMs);

                    // Stop recording
                    recorder.StopRecording();

                    successCount++;

                    // No need to verify audio data - we're testing for leaks/crashes, not functionality

                    UpdatePeakMemory();

                    // Log progress every 10 cycles
                    if ((i + 1) % 10 == 0)
                    {
                        Console.WriteLine($"Progress: {i + 1}/{cycles} - Memory growth: {GetMemoryGrowthMB():F2} MB");
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    Console.WriteLine($"Cycle {i + 1} failed: {ex.Message}");
                }
                finally
                {
                    // Critical: Dispose after each cycle to test cleanup
                    recorder?.Dispose();
                }

                // Small delay to allow cleanup
                await Task.Delay(10);
            }

            // Assert
            Console.WriteLine($"\n=== Stress Test Results ===");
            Console.WriteLine($"Total cycles: {cycles}");
            Console.WriteLine($"Successes: {successCount}");
            Console.WriteLine($"Failures: {failureCount}");
            Console.WriteLine($"Success rate: {(successCount / (double)cycles) * 100:F1}%");

            // Memory check - this is critical for AudioRecorder
            AssertMemoryWithinLimits(maxMemoryGrowthMB, "Rapid Start/Stop Cycles");

            // At least 90% should succeed
            successCount.Should().BeGreaterThanOrEqualTo((int)(cycles * 0.9),
                "at least 90% of start/stop cycles should succeed");
        }

        [Fact(Skip = "Stress test - run manually with: dotnet test --filter Category=Stress")]
        public async Task StressTest_MultipleRecorderInstances_NoResourceExhaustion()
        {
            // Arrange
            const int instances = 50; // Create many recorders sequentially
            const int recordDurationMs = 200;
            const double maxMemoryGrowthMB = 50.0;

            var successCount = 0;

            // Act - Create and dispose many recorder instances
            for (int i = 0; i < instances; i++)
            {
                AudioRecorder? recorder = null;

                try
                {
                    recorder = new AudioRecorder();

                    recorder.StartRecording();
                    await Task.Delay(recordDurationMs);
                    recorder.StopRecording();

                    successCount++;

                    UpdatePeakMemory();

                    if ((i + 1) % 10 == 0)
                    {
                        Console.WriteLine($"Instance {i + 1}/{instances} - Memory: {GetMemoryGrowthMB():F2} MB");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Instance {i + 1} failed: {ex.Message}");
                }
                finally
                {
                    recorder?.Dispose();
                }

                await Task.Delay(50); // Brief pause between instances
            }

            // Assert
            Console.WriteLine($"\n=== Results ===");
            Console.WriteLine($"Successful instances: {successCount}/{instances}");

            AssertMemoryWithinLimits(maxMemoryGrowthMB, "Multiple Recorder Instances");

            successCount.Should().BeGreaterThanOrEqualTo((int)(instances * 0.9),
                "at least 90% of recorder instances should work");
        }

        [Fact(Skip = "Stress test - run manually with: dotnet test --filter Category=Stress")]
        public void StressTest_StartStopWithoutRecording_NoLeaks()
        {
            // Arrange - Test edge case: start immediately followed by stop (no recording time)
            const int cycles = 100;
            const double maxMemoryGrowthMB = 20.0;

            var successCount = 0;

            // Act
            for (int i = 0; i < cycles; i++)
            {
                AudioRecorder? recorder = null;

                try
                {
                    recorder = new AudioRecorder();

                    recorder.StartRecording();
                    // Immediately stop (no delay)
                    recorder.StopRecording();

                    // Empty audio is acceptable for this edge case
                    successCount++;

                    UpdatePeakMemory();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Cycle {i + 1} failed: {ex.Message}");
                }
                finally
                {
                    recorder?.Dispose();
                }
            }

            // Assert
            Console.WriteLine($"Start/Stop without recording: {successCount}/{cycles} succeeded");

            AssertMemoryWithinLimits(maxMemoryGrowthMB, "Start/Stop Without Recording");

            // This edge case should succeed most of the time
            successCount.Should().BeGreaterThanOrEqualTo((int)(cycles * 0.8),
                "at least 80% should handle immediate start/stop");
        }

        [Fact(Skip = "Stress test - run manually with: dotnet test --filter Category=Stress")]
        public async Task StressTest_DisposeWithoutStoppingRecording_Cleanup()
        {
            // Arrange - Test cleanup when user disposes without stopping
            const int cycles = 50;
            const double maxMemoryGrowthMB = 25.0;

            // Act
            for (int i = 0; i < cycles; i++)
            {
                AudioRecorder? recorder = null;

                try
                {
                    recorder = new AudioRecorder();
                    recorder.StartRecording();
                    await Task.Delay(100);

                    // Dispose WITHOUT calling StopRecording
                    // This tests that Dispose() properly cleans up active recording
                    recorder.Dispose();
                    recorder = null;

                    UpdatePeakMemory();

                    if ((i + 1) % 10 == 0)
                    {
                        Console.WriteLine($"Cycle {i + 1}/{cycles} - Memory: {GetMemoryGrowthMB():F2} MB");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Cycle {i + 1} failed: {ex.Message}");
                }
                finally
                {
                    // Ensure cleanup even if already disposed
                    recorder?.Dispose();
                }

                await Task.Delay(50);
            }

            // Assert
            Console.WriteLine($"\n=== Results ===");
            Console.WriteLine($"Completed {cycles} dispose-during-recording cycles");

            // Memory is critical - disposing during recording must not leak
            AssertMemoryWithinLimits(maxMemoryGrowthMB, "Dispose During Recording");
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace VoiceLite.Tests.Stress
{
    /// <summary>
    /// PHASE 3 - DAY 5: Base class for stress tests
    ///
    /// Purpose: Provides common infrastructure for long-running stress tests
    /// - Memory measurement utilities
    /// - Performance tracking
    /// - Test resource cleanup
    ///
    /// Usage: Inherit from this class for all stress tests
    /// </summary>
    public abstract class StressTestBase : IDisposable
    {
        protected readonly Stopwatch Stopwatch;
        protected long InitialMemoryBytes;
        protected long PeakMemoryBytes;

        protected StressTestBase()
        {
            // Force garbage collection to get accurate baseline
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true);

            InitialMemoryBytes = GC.GetTotalMemory(false);
            PeakMemoryBytes = InitialMemoryBytes;
            Stopwatch = Stopwatch.StartNew();
        }

        /// <summary>
        /// Updates peak memory if current usage is higher
        /// Call this periodically during stress test iterations
        /// </summary>
        protected void UpdatePeakMemory()
        {
            var currentMemory = GC.GetTotalMemory(false);
            if (currentMemory > PeakMemoryBytes)
            {
                PeakMemoryBytes = currentMemory;
            }
        }

        /// <summary>
        /// Gets current memory growth since test started
        /// </summary>
        protected long GetMemoryGrowthBytes()
        {
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true);

            return GC.GetTotalMemory(false) - InitialMemoryBytes;
        }

        /// <summary>
        /// Gets memory growth in megabytes for readable output
        /// </summary>
        protected double GetMemoryGrowthMB()
        {
            return GetMemoryGrowthBytes() / 1024.0 / 1024.0;
        }

        /// <summary>
        /// Gets peak memory usage in megabytes
        /// </summary>
        protected double GetPeakMemoryMB()
        {
            return PeakMemoryBytes / 1024.0 / 1024.0;
        }

        /// <summary>
        /// Asserts that memory growth is within acceptable limits
        /// </summary>
        /// <param name="maxGrowthMB">Maximum allowed memory growth in MB</param>
        /// <param name="testName">Name of test for error message</param>
        protected void AssertMemoryWithinLimits(double maxGrowthMB, string testName)
        {
            var growthMB = GetMemoryGrowthMB();
            var peakMB = GetPeakMemoryMB();

            var message = $"{testName} memory check:\n" +
                         $"  Initial: {InitialMemoryBytes / 1024.0 / 1024.0:F2} MB\n" +
                         $"  Final: {GC.GetTotalMemory(false) / 1024.0 / 1024.0:F2} MB\n" +
                         $"  Growth: {growthMB:F2} MB\n" +
                         $"  Peak: {peakMB:F2} MB\n" +
                         $"  Limit: {maxGrowthMB:F2} MB\n" +
                         $"  Duration: {Stopwatch.Elapsed.TotalSeconds:F1}s";

            // Log results even if passing (useful for performance tracking)
            Console.WriteLine(message);

            Assert.True(growthMB <= maxGrowthMB,
                $"Memory leak detected! {message}");
        }

        /// <summary>
        /// Creates a temporary test audio file for stress testing
        /// Returns path to the file
        /// </summary>
        protected string CreateTestAudioFile()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"stress_test_{Guid.NewGuid():N}.wav");

            // Create a minimal valid WAV file (1 second of silence at 16kHz)
            // This is faster than using NAudio for thousands of iterations
            using var fs = new FileStream(tempPath, FileMode.Create);
            using var writer = new BinaryWriter(fs);

            // WAV header
            var sampleRate = 16000;
            var bitsPerSample = 16;
            var channels = 1;
            var byteRate = sampleRate * channels * (bitsPerSample / 8);
            var blockAlign = (short)(channels * (bitsPerSample / 8));
            var dataSize = sampleRate * channels * (bitsPerSample / 8); // 1 second

            // RIFF header
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataSize);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });

            // fmt chunk
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16); // chunk size
            writer.Write((short)1); // PCM
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write((short)bitsPerSample);

            // data chunk
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataSize);

            // Write silence (zeros)
            var silenceBuffer = new byte[dataSize];
            writer.Write(silenceBuffer);

            return tempPath;
        }

        /// <summary>
        /// Cleans up temporary test files
        /// </summary>
        protected void CleanupTestFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch
            {
                // Best effort cleanup - don't fail test if file is locked
            }
        }

        public virtual void Dispose()
        {
            Stopwatch?.Stop();
        }
    }
}

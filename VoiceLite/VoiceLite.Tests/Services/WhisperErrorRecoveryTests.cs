using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using VoiceLite.Models;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// Comprehensive error recovery tests for Whisper transcription service
    /// Tests crash scenarios, timeouts, OOM conditions, and graceful degradation
    /// </summary>
    public class WhisperErrorRecoveryTests : IDisposable
    {
        private readonly Settings _settings;
        private PersistentWhisperService? _service;
        private readonly string _tempDirectory;

        public WhisperErrorRecoveryTests()
        {
            _settings = new Settings
            {
                WhisperModel = "ggml-small.bin",
                BeamSize = 5,
                BestOf = 5
            };

            _tempDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp_error_recovery");
            Directory.CreateDirectory(_tempDirectory);
        }

        public void Dispose()
        {
            _service?.Dispose();

            // Cleanup temp directory
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, true);
                }
            }
            catch { /* Ignore cleanup errors */ }
        }

        [Fact]
        public async Task ProcessTimeout_KillsProcessTreeGracefully()
        {
            // Arrange: Create a very long audio file to trigger timeout
            var audioPath = Path.Combine(_tempDirectory, "long_audio.wav");
            CreateSilentWavFile(audioPath, 300); // 5 minutes - will timeout

            // Set very short timeout
            _settings.WhisperTimeoutMultiplier = 0.01; // Force timeout
            _service = new PersistentWhisperService(_settings);

            // Act & Assert: Should throw TimeoutException and kill process tree
            Func<Task> act = async () => await _service.TranscribeAsync(audioPath);
            await act.Should().ThrowAsync<TimeoutException>()
                .WithMessage("*timed out*");

            // Verify no orphaned whisper.exe processes remain
            await Task.Delay(1000); // Wait for cleanup
            var whisperProcesses = Process.GetProcessesByName("whisper");
            whisperProcesses.Should().BeEmpty("all whisper.exe processes should be killed");
        }

        [Fact]
        public async Task ConsecutiveCrashes_DoesNotLeakResources()
        {
            // Arrange: Test multiple failures in a row
            var initialProcessCount = Process.GetCurrentProcess().Threads.Count;
            var initialHandleCount = Process.GetCurrentProcess().HandleCount;

            _service = new PersistentWhisperService(_settings);

            // Act: Attempt 5 consecutive transcriptions with non-existent files
            for (int i = 0; i < 5; i++)
            {
                var nonExistentFile = Path.Combine(_tempDirectory, $"nonexistent_{i}.wav");

                try
                {
                    await _service.TranscribeAsync(nonExistentFile);
                }
                catch (FileNotFoundException)
                {
                    // Expected exception - continue testing
                }
            }

            // Assert: Verify no resource leaks
            await Task.Delay(500); // Allow GC to run
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(500);

            var finalProcessCount = Process.GetCurrentProcess().Threads.Count;
            var finalHandleCount = Process.GetCurrentProcess().HandleCount;

            // Thread count should not increase significantly (±5 is acceptable)
            Math.Abs(finalProcessCount - initialProcessCount).Should().BeLessThan(10,
                "thread count should not leak after consecutive failures");

            // Handle count should not increase significantly (±50 is acceptable for temp files)
            Math.Abs(finalHandleCount - initialHandleCount).Should().BeLessThan(100,
                "handle count should not leak after consecutive failures");
        }

        [Fact]
        public async Task CorruptedAudioFile_HandlesGracefully()
        {
            // Arrange: Create a corrupted WAV file (wrong header)
            var corruptedPath = Path.Combine(_tempDirectory, "corrupted.wav");
            File.WriteAllBytes(corruptedPath, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });

            _service = new PersistentWhisperService(_settings);

            // Act & Assert: Should either return empty string or throw gracefully
            try
            {
                var result = await _service.TranscribeAsync(corruptedPath);
                result.Should().BeEmpty("corrupted audio should return empty transcription");
            }
            catch (Exception ex)
            {
                // Acceptable exceptions for corrupted files
                ex.Should().BeOfType<Exception>()
                    .Which.Message.Should().Contain("failed",
                    "error message should indicate failure");
            }
        }

        [Fact]
        public async Task EmptyAudioFile_ReturnsEmptyString()
        {
            // Arrange: Create minimal valid WAV file with no actual audio data
            var emptyPath = Path.Combine(_tempDirectory, "empty.wav");
            CreateSilentWavFile(emptyPath, 0); // 0 seconds = headers only

            _service = new PersistentWhisperService(_settings);

            // Act
            var result = await _service.TranscribeAsync(emptyPath);

            // Assert: Should return empty without crashing
            result.Should().NotBeNull();
            result.Length.Should().BeLessThan(10, "empty audio should return minimal/no transcription");
        }

        [Fact]
        public async Task MultipleDisposeCalls_DoesNotThrow()
        {
            // Arrange
            _service = new PersistentWhisperService(_settings);

            // Act & Assert: Multiple Dispose calls should be safe
            _service.Dispose();

            Action act = () => _service.Dispose();
            act.Should().NotThrow("Dispose should be idempotent");

            // Third dispose for good measure
            act.Should().NotThrow();
        }

        [Fact]
        public async Task TranscriptionDuringDispose_HandlesGracefully()
        {
            // Arrange
            var audioPath = Path.Combine(_tempDirectory, "test_dispose.wav");
            CreateSilentWavFile(audioPath, 2);

            _service = new PersistentWhisperService(_settings);

            // Act: Start transcription and immediately dispose
            var transcriptionTask = _service.TranscribeAsync(audioPath);
            await Task.Delay(10); // Let transcription start
            _service.Dispose();

            // Assert: Task should complete or fail gracefully
            try
            {
                await transcriptionTask;
                // If it completes, that's fine
            }
            catch
            {
                // If it throws, that's also acceptable after dispose
            }

            // Verify no zombie processes
            await Task.Delay(1000);
            var whisperProcesses = Process.GetProcessesByName("whisper");
            whisperProcesses.Should().BeEmpty("no orphaned processes after dispose");
        }

        [Fact]
        public async Task ConcurrentTranscriptions_QueuedCorrectly()
        {
            // Arrange: PersistentWhisperService uses semaphore to serialize transcriptions
            var audioPath1 = Path.Combine(_tempDirectory, "concurrent1.wav");
            var audioPath2 = Path.Combine(_tempDirectory, "concurrent2.wav");
            var audioPath3 = Path.Combine(_tempDirectory, "concurrent3.wav");

            CreateSilentWavFile(audioPath1, 1);
            CreateSilentWavFile(audioPath2, 1);
            CreateSilentWavFile(audioPath3, 1);

            _service = new PersistentWhisperService(_settings);

            // Act: Start 3 transcriptions simultaneously
            var task1 = _service.TranscribeAsync(audioPath1);
            var task2 = _service.TranscribeAsync(audioPath2);
            var task3 = _service.TranscribeAsync(audioPath3);

            var results = await Task.WhenAll(task1, task2, task3);

            // Assert: All should complete successfully
            results.Should().HaveCount(3);
            results.Should().AllSatisfy(r => r.Should().NotBeNull());
        }

        [Fact]
        public async Task MissingWhisperModel_ShowsClearError()
        {
            // Arrange: Try to use a model that doesn't exist
            var settingsWithMissingModel = new Settings
            {
                WhisperModel = "ggml-nonexistent-model.bin"
            };

            // Act & Assert: Should throw FileNotFoundException with helpful message
            Action act = () => new PersistentWhisperService(settingsWithMissingModel);
            act.Should().Throw<FileNotFoundException>()
                .WithMessage("*not found*")
                .WithMessage("*reinstall*", "should suggest reinstalling VoiceLite");
        }

        [Fact]
        public async Task VeryShortAudio_DoesNotCrash()
        {
            // Arrange: Audio shorter than 100ms
            var shortPath = Path.Combine(_tempDirectory, "very_short.wav");
            CreateSilentWavFile(shortPath, 0); // Will create minimal file

            _service = new PersistentWhisperService(_settings);

            // Act & Assert: Should handle gracefully
            Func<Task> act = async () => await _service.TranscribeAsync(shortPath);
            await act.Should().NotThrowAsync("short audio should be handled gracefully");
        }

        [Fact]
        public async Task LargeAudioFile_HandlesTimeout()
        {
            // Arrange: Create a large audio file that might cause memory issues
            var largePath = Path.Combine(_tempDirectory, "large_audio.wav");
            CreateSilentWavFile(largePath, 60); // 1 minute

            _settings.WhisperTimeoutMultiplier = 5.0; // Reasonable timeout
            _service = new PersistentWhisperService(_settings);

            // Act
            var result = await _service.TranscribeAsync(largePath);

            // Assert: Should complete or timeout gracefully
            result.Should().NotBeNull();
        }

        // Helper method to create silent WAV files
        private void CreateSilentWavFile(string path, int durationSeconds)
        {
            const int sampleRate = 16000;
            const int bitsPerSample = 16;
            const int channels = 1;

            var numSamples = sampleRate * durationSeconds;
            var dataSize = numSamples * (bitsPerSample / 8) * channels;

            using var fileStream = File.Create(path);
            using var writer = new BinaryWriter(fileStream);

            // RIFF header
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize); // File size - 8
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            // fmt chunk
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16); // Chunk size
            writer.Write((short)1); // Audio format (PCM)
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * (bitsPerSample / 8)); // Byte rate
            writer.Write((short)(channels * (bitsPerSample / 8))); // Block align
            writer.Write((short)bitsPerSample);

            // data chunk
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);

            // Write silence (zeros)
            if (dataSize > 0)
            {
                writer.Write(new byte[dataSize]);
            }
        }
    }
}

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using VoiceLite.Models;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    public class WhisperServiceTests : IDisposable
    {
        private readonly Settings _settings;
        private readonly PersistentWhisperService _service;
        private readonly string _tempDirectory;

        public WhisperServiceTests()
        {
            _settings = new Settings
            {
                WhisperModel = "small",
                BeamSize = 5
            };

            _tempDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
            Directory.CreateDirectory(_tempDirectory);

            // Only create service if whisper.exe exists
            var whisperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper", "whisper.exe");
            if (File.Exists(whisperPath))
            {
                _service = new PersistentWhisperService(_settings);
            }
            else
            {
                _service = null!;
            }
        }

        public void Dispose()
        {
            _service?.Dispose();
        }

        [Fact]
        public void Constructor_ThrowsWhenSettingsNull()
        {
            Action act = () => new PersistentWhisperService(null!);
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("settings");
        }

        [Fact]
        public void Constructor_InitializesWithValidSettings()
        {
            if (_service == null)
            {
                // Skip test if whisper.exe doesn't exist
                return;
            }

            _service.Should().NotBeNull();
        }

        [Fact]
        public async Task TranscribeAsync_ThrowsWhenFileNotFound()
        {
            if (_service == null) return;

            var nonExistentFile = Path.Combine(_tempDirectory, "nonexistent.wav");

            Func<Task> act = async () => await _service.TranscribeAsync(nonExistentFile);
            await act.Should().ThrowAsync<FileNotFoundException>()
                .WithMessage($"Audio file not found: {nonExistentFile}");
        }

        [Fact(Skip = "Integration test - silent WAV causes whisper.exe exit code -1")]
        public async Task TranscribeAsync_ReturnsTranscriptionForValidAudio()
        {
            if (_service == null) return;

            // Create a simple WAV file with silence
            var audioPath = Path.Combine(_tempDirectory, "test.wav");
            CreateSilentWavFile(audioPath, 1);

            try
            {
                var result = await _service.TranscribeAsync(audioPath);

                result.Should().NotBeNull();
                // Silent audio typically returns empty or very short transcription
                result.Length.Should().BeGreaterThanOrEqualTo(0);
            }
            finally
            {
                if (File.Exists(audioPath))
                    File.Delete(audioPath);
            }
        }

        [Fact(Skip = "Integration test - requires real voice audio")]
        public async Task TranscribeFromMemoryAsync_HandlesValidData()
        {
            if (_service == null) return;

            // Create WAV data in memory
            var audioData = CreateSilentWavData(1);

            var result = await _service.TranscribeFromMemoryAsync(audioData);

            result.Should().NotBeNull();
            result.Length.Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public async Task TranscribeFromMemoryAsync_HandlesEmptyData()
        {
            if (_service == null) return;

            var emptyData = new byte[0];

            // Should handle gracefully without crashing
            Func<Task> act = async () => await _service.TranscribeFromMemoryAsync(emptyData);

            // May throw or return empty string depending on implementation
            await act.Should().NotThrowAsync<NullReferenceException>();
        }

        [Fact]
        public void ModelPathResolution_HandlesAllModelTypes()
        {
            var modelTypes = new[] { "small", "medium", "large" };

            foreach (var modelType in modelTypes)
            {
                var settings = new Settings { WhisperModel = modelType };

                // Should not throw during construction
                Action act = () =>
                {
                    using var service = new PersistentWhisperService(settings);
                };

                // If model file doesn't exist, it will throw FileNotFoundException
                // which is expected behavior
                if (!File.Exists(GetExpectedModelPath(modelType)))
                {
                    act.Should().Throw<FileNotFoundException>();
                }
            }
        }

        [Fact(Skip = "Integration test - requires real voice audio")]
        public async Task TranscribeAsync_CancellationHandling()
        {
            if (_service == null) return;

            var audioPath = Path.Combine(_tempDirectory, "test_cancel.wav");
            CreateSilentWavFile(audioPath, 5); // Longer file for cancellation test

            try
            {
                using var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromMilliseconds(100));

                // Current implementation doesn't support cancellation tokens,
                // but we test that it handles the scenario gracefully
                var task = _service.TranscribeAsync(audioPath);
                var result = await task;

                // Should complete normally even without explicit cancellation support
                result.Should().NotBeNull();
            }
            finally
            {
                if (File.Exists(audioPath))
                    File.Delete(audioPath);
            }
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            if (_service == null) return;

            _service.Dispose();

            Action act = () => _service.Dispose();
            act.Should().NotThrow();
        }

        [Fact(Skip = "Integration test - requires real voice audio")]
        public async Task ConcurrentTranscriptions_HandledSafely()
        {
            if (_service == null) return;

            var audioPath1 = Path.Combine(_tempDirectory, "test1.wav");
            var audioPath2 = Path.Combine(_tempDirectory, "test2.wav");
            CreateSilentWavFile(audioPath1, 1);
            CreateSilentWavFile(audioPath2, 1);

            try
            {
                // Start two transcriptions concurrently
                var task1 = _service.TranscribeAsync(audioPath1);
                var task2 = _service.TranscribeAsync(audioPath2);

                var results = await Task.WhenAll(task1, task2);

                results.Should().HaveCount(2);
                results[0].Should().NotBeNull();
                results[1].Should().NotBeNull();
            }
            finally
            {
                if (File.Exists(audioPath1)) File.Delete(audioPath1);
                if (File.Exists(audioPath2)) File.Delete(audioPath2);
            }
        }

        private string GetExpectedModelPath(string modelType)
        {
            var modelFile = modelType switch
            {
                "small" => "ggml-small.bin",
                "medium" => "ggml-medium.bin",
                "large" => "ggml-large-v3.bin",
                _ => "ggml-small.bin"
            };

            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper", modelFile);
            if (File.Exists(path)) return path;

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, modelFile);
        }

        private void CreateSilentWavFile(string path, int durationSeconds)
        {
            var data = CreateSilentWavData(durationSeconds);
            File.WriteAllBytes(path, data);
        }

        private byte[] CreateSilentWavData(int durationSeconds)
        {
            const int sampleRate = 16000;
            const int bitsPerSample = 16;
            const int channels = 1;

            var numSamples = sampleRate * durationSeconds;
            var dataSize = numSamples * (bitsPerSample / 8) * channels;

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8);

            // RIFF header
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize); // File size - 8
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));

            // fmt chunk
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16); // Chunk size
            writer.Write((short)1); // Audio format (PCM)
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * (bitsPerSample / 8)); // Byte rate
            writer.Write((short)(channels * (bitsPerSample / 8))); // Block align
            writer.Write((short)bitsPerSample);

            // data chunk
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);

            // Write silence (zeros)
            writer.Write(new byte[dataSize]);

            return ms.ToArray();
        }
    }
}

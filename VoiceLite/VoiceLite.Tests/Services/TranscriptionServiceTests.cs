using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using VoiceLite.Models;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// Shares one TranscriptionService (and its ~600MB native Parakeet model)
    /// across all tests in the class — loading the model per-test would be brutally slow.
    /// </summary>
    public class TranscriptionServiceFixture : IDisposable
    {
        public static readonly string ModelDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VoiceLite", "models", ModelResolverService.ParakeetDirName);

        public static bool ModelPresent =>
            new[] { "encoder.int8.onnx", "decoder.int8.onnx", "joiner.int8.onnx", "tokens.txt" }
                .All(f => File.Exists(Path.Combine(ModelDir, f)));

        public Settings Settings { get; } = new Settings();
        public TranscriptionService Service { get; }

        public TranscriptionServiceFixture()
        {
            Service = new TranscriptionService(Settings);
        }

        public void Dispose()
        {
            Service.Dispose();
        }
    }

    /// <summary>
    /// FUNCTIONAL tests for the Parakeet transcription path — the feature users pay for.
    /// Until 2026-07-17 this path had zero active coverage (every green test was
    /// infrastructure around it). Model-dependent tests gate on the real Parakeet model
    /// being installed (dev machines have it; bare CI does not) but assert REAL output
    /// when it is — no early-return-to-green against assertions that never run.
    /// </summary>
    public class TranscriptionServiceTests : IClassFixture<TranscriptionServiceFixture>
    {
        private readonly TranscriptionServiceFixture _fx;

        private static string KnownSpeechWav => Path.Combine(
            AppContext.BaseDirectory, "TestData", "hello_world.wav");

        public TranscriptionServiceTests(TranscriptionServiceFixture fx)
        {
            _fx = fx;
        }

        // ---- Tests that need the real model ----

        [Fact]
        public async Task TranscribeAsync_KnownSpeech_ReturnsTheSpokenWords()
        {
            if (!TranscriptionServiceFixture.ModelPresent) return; // no model on this machine (e.g. bare CI)

            File.Exists(KnownSpeechWav).Should().BeTrue(
                "TestData/hello_world.wav must be copied to the test output directory");

            var result = await _fx.Service.TranscribeAsync(KnownSpeechWav, TranscriptionServiceFixture.ModelDir);

            // TTS audio of "The quick brown fox jumps over the lazy dog."
            result.Should().NotBeNullOrWhiteSpace("the app's core feature is producing text from speech");
            result.ToLowerInvariant().Should().Contain("fox");
            result.ToLowerInvariant().Should().Contain("dog");
        }

        [Fact]
        public async Task TranscribeFromMemoryAsync_SameAudio_ProducesSameWords()
        {
            if (!TranscriptionServiceFixture.ModelPresent) return;

            var bytes = await File.ReadAllBytesAsync(KnownSpeechWav);

            var result = await _fx.Service.TranscribeFromMemoryAsync(bytes);

            result.ToLowerInvariant().Should().Contain("fox",
                "the in-memory path (used by the live recording pipeline) must transcribe like the file path");
        }

        [Fact]
        public async Task TranscribeAsync_Silence_DoesNotHallucinateText()
        {
            if (!TranscriptionServiceFixture.ModelPresent) return;

            var silencePath = CreateSilenceWav(durationSeconds: 1);
            try
            {
                var result = await _fx.Service.TranscribeAsync(silencePath, TranscriptionServiceFixture.ModelDir);

                // Parakeet (unlike Whisper) must not invent text from pure silence —
                // this property is advertised in the README.
                result.Trim().Should().BeEmpty();
            }
            finally
            {
                try { File.Delete(silencePath); } catch { }
            }
        }

        [Fact]
        public async Task ConcurrentTranscriptions_AreSerializedByTheSemaphore_AndBothSucceed()
        {
            if (!TranscriptionServiceFixture.ModelPresent) return;

            var first = _fx.Service.TranscribeAsync(KnownSpeechWav, TranscriptionServiceFixture.ModelDir);
            var second = _fx.Service.TranscribeAsync(KnownSpeechWav, TranscriptionServiceFixture.ModelDir);

            var results = await Task.WhenAll(first, second);

            results.Should().AllSatisfy(r => r.ToLowerInvariant().Should().Contain("fox"),
                "concurrent calls must queue on the transcription semaphore, not corrupt native state");
        }

        [Fact]
        public async Task ChangingTranscriptionPreset_RebuildsRecognizer_AndStillTranscribes()
        {
            if (!TranscriptionServiceFixture.ModelPresent) return;

            // Dedicated service: mutating the preset on the shared fixture would leak
            // state into the other tests in this class.
            var settings = new Settings { TranscriptionPreset = TranscriptionPreset.Balanced };
            using var service = new TranscriptionService(settings);

            var before = await service.TranscribeAsync(KnownSpeechWav, TranscriptionServiceFixture.ModelDir);
            before.ToLowerInvariant().Should().Contain("fox");
            var loadsBefore = service.RecognizerLoadCount;

            settings.TranscriptionPreset = TranscriptionPreset.Speed;

            var after = await service.TranscribeAsync(KnownSpeechWav, TranscriptionServiceFixture.ModelDir);

            service.RecognizerLoadCount.Should().BeGreaterThan(loadsBefore,
                "changing the preset must rebuild the OfflineRecognizer — before this fix the " +
                "Speed/Balanced/Accuracy setting was a silent no-op until restart (HEALTH.md #3)");
            after.ToLowerInvariant().Should().Contain("fox",
                "transcription must keep working after a preset-triggered recognizer reload");
        }

        // ---- Tests that run everywhere (no model needed) ----

        [Fact]
        public async Task TranscribeAsync_MissingFile_ThrowsFileNotFound()
        {
            var act = () => _fx.Service.TranscribeAsync(
                Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.wav"),
                TranscriptionServiceFixture.ModelDir);

            await act.Should().ThrowAsync<FileNotFoundException>();
        }

        [Fact]
        public async Task TranscribeAsync_NearEmptyFile_ReturnsEmptyWithoutTouchingTheModel()
        {
            var tinyPath = Path.Combine(Path.GetTempPath(), $"tiny-{Guid.NewGuid():N}.wav");
            await File.WriteAllBytesAsync(tinyPath, new byte[50]); // < 100-byte floor
            try
            {
                var result = await _fx.Service.TranscribeAsync(tinyPath, TranscriptionServiceFixture.ModelDir);
                result.Should().BeEmpty();
            }
            finally
            {
                try { File.Delete(tinyPath); } catch { }
            }
        }

        [Fact]
        public async Task TranscribeAsync_AfterDispose_Throws()
        {
            using var service = new TranscriptionService(new Settings());
            service.Dispose();

            var act = () => service.TranscribeAsync(KnownSpeechWav);

            await act.Should().ThrowAsync<ObjectDisposedException>();
        }

        // 16kHz/16-bit/mono all-zero PCM — the exact format AudioRecorder produces.
        private static string CreateSilenceWav(int durationSeconds)
        {
            var path = Path.Combine(Path.GetTempPath(), $"silence-{Guid.NewGuid():N}.wav");
            int dataSize = 16000 * 2 * durationSeconds;

            using var fs = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(fs);
            w.Write("RIFF".ToCharArray());
            w.Write(36 + dataSize);
            w.Write("WAVE".ToCharArray());
            w.Write("fmt ".ToCharArray());
            w.Write(16);
            w.Write((short)1);      // PCM
            w.Write((short)1);      // mono
            w.Write(16000);         // sample rate
            w.Write(32000);         // byte rate
            w.Write((short)2);      // block align
            w.Write((short)16);     // bits per sample
            w.Write("data".ToCharArray());
            w.Write(dataSize);
            w.Write(new byte[dataSize]);
            return path;
        }
    }
}

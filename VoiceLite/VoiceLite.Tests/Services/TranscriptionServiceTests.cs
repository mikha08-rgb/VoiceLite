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
            var service = new TranscriptionService(new Settings());
            service.Dispose();

            // Pass an explicit modelDir: the single-arg overload runs the model RESOLVER
            // before the disposed check, so on a machine without the model (bare CI) it
            // used to throw FileNotFoundException instead of ObjectDisposedException.
            var fileAct = () => service.TranscribeAsync(KnownSpeechWav, TranscriptionServiceFixture.ModelDir);
            await fileAct.Should().ThrowAsync<ObjectDisposedException>();

            // The in-memory path (live recording pipeline) must fail the same way.
            var memoryAct = () => service.TranscribeFromMemoryAsync(new byte[200]);
            await memoryAct.Should().ThrowAsync<ObjectDisposedException>();

            // Double-dispose must be idempotent (convention of the disposal suites).
            var secondDispose = () => service.Dispose();
            secondDispose.Should().NotThrow();
        }

        [Fact]
        public void Constructor_NullSettings_ThrowsArgumentNullException()
        {
            var act = () => new TranscriptionService(null!);

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public async Task TranscribeFromMemoryAsync_TinyBuffer_ReturnsEmptyWithoutTouchingTheModel()
        {
            // < 100-byte floor — rejected before any model or WAV parsing happens.
            var result = await _fx.Service.TranscribeFromMemoryAsync(new byte[50]);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task TranscribeAsync_ZeroByteFile_ReturnsEmpty()
        {
            var emptyPath = Path.Combine(Path.GetTempPath(), $"empty-{Guid.NewGuid():N}.wav");
            await File.WriteAllBytesAsync(emptyPath, Array.Empty<byte>());
            try
            {
                var result = await _fx.Service.TranscribeAsync(emptyPath, TranscriptionServiceFixture.ModelDir);

                result.Should().BeEmpty("a zero-byte recording must be skipped, not fed to the recognizer");
            }
            finally
            {
                try { File.Delete(emptyPath); } catch { }
            }
        }

        [Fact]
        public async Task TranscribeAsync_ModelDirMissingAFile_ThrowsFileNotFoundNamingTheMissingFile()
        {
            // Incomplete install: 3 of 4 model files present. The service must fail loudly
            // (naming the missing file) BEFORE handing anything to the native loader.
            // Placeholder files are never opened — the pre-check throws first.
            var incompleteDir = Path.Combine(Path.GetTempPath(), $"parakeet-incomplete-{Guid.NewGuid():N}");
            Directory.CreateDirectory(incompleteDir);
            var wavPath = CreateSilenceWav(durationSeconds: 1);
            using var service = new TranscriptionService(new Settings());
            try
            {
                foreach (var f in new[] { "encoder.int8.onnx", "decoder.int8.onnx", "joiner.int8.onnx" })
                    File.WriteAllBytes(Path.Combine(incompleteDir, f), new byte[10]);
                // tokens.txt deliberately missing

                var act = () => service.TranscribeAsync(wavPath, incompleteDir);

                var ex = await act.Should().ThrowAsync<FileNotFoundException>();
                ex.Which.Message.Should().Contain("tokens.txt");
            }
            finally
            {
                try { Directory.Delete(incompleteDir, true); } catch { }
                try { File.Delete(wavPath); } catch { }
            }
        }

        [Fact]
        public async Task CancelTranscription_WithNothingInFlight_DoesNotBreakSubsequentTranscriptions()
        {
            if (!TranscriptionServiceFixture.ModelPresent) return; // no model on this machine (e.g. bare CI)

            // CancelTranscription disposes and replaces the shared CTS — a stale token
            // must not poison the next call (Esc-cancel then immediately re-record).
            _fx.Service.CancelTranscription();

            var result = await _fx.Service.TranscribeAsync(KnownSpeechWav, TranscriptionServiceFixture.ModelDir);

            result.ToLowerInvariant().Should().Contain("fox");
        }

        [Fact]
        public async Task SequentialTranscriptions_ReuseTheLoadedRecognizer()
        {
            if (!TranscriptionServiceFixture.ModelPresent) return;

            var first = await _fx.Service.TranscribeAsync(KnownSpeechWav, TranscriptionServiceFixture.ModelDir);
            var loadsAfterFirst = _fx.Service.RecognizerLoadCount;

            var second = await _fx.Service.TranscribeAsync(KnownSpeechWav, TranscriptionServiceFixture.ModelDir);

            _fx.Service.RecognizerLoadCount.Should().Be(loadsAfterFirst,
                "same model dir + same preset must NOT rebuild the recognizer per call");
            first.ToLowerInvariant().Should().Contain("fox");
            second.ToLowerInvariant().Should().Contain("fox");
            _fx.Service.IsProcessing.Should().BeFalse("the processing flag must reset after completion");
        }

        [Fact]
        public async Task TranscribeAsync_WhiteNoise_ReturnsShortOrEmptyTextWithoutThrowing()
        {
            if (!TranscriptionServiceFixture.ModelPresent) return;

            var noisePath = CreateNoiseWav(durationSeconds: 1);
            try
            {
                var result = await _fx.Service.TranscribeAsync(noisePath, TranscriptionServiceFixture.ModelDir);

                // Non-speech must not crash the decoder or produce paragraphs of hallucination.
                result.Length.Should().BeLessThan(100);
            }
            finally
            {
                try { File.Delete(noisePath); } catch { }
            }
        }

        [Fact]
        public async Task TranscribeAsync_CorruptWav_Throws_AndTheServiceRemainsUsable()
        {
            if (!TranscriptionServiceFixture.ModelPresent) return;

            // ≥ 100 bytes (passes the size floor) but not a RIFF file at all.
            var corruptPath = Path.Combine(Path.GetTempPath(), $"corrupt-{Guid.NewGuid():N}.wav");
            var garbage = new byte[4096];
            Array.Fill(garbage, (byte)0xAB);
            await File.WriteAllBytesAsync(corruptPath, garbage);
            try
            {
                var act = () => _fx.Service.TranscribeAsync(corruptPath, TranscriptionServiceFixture.ModelDir);
                await act.Should().ThrowAsync<FormatException>("NAudio rejects non-RIFF input");

                // The crown-jewel property: a failed call must release the semaphore and
                // leave the recognizer intact — the next dictation must still work.
                var recovery = await _fx.Service.TranscribeAsync(KnownSpeechWav, TranscriptionServiceFixture.ModelDir);
                recovery.ToLowerInvariant().Should().Contain("fox");
            }
            finally
            {
                try { File.Delete(corruptPath); } catch { }
            }
        }

        // 16kHz/16-bit/mono all-zero PCM — the exact format AudioRecorder produces.
        private static string CreateSilenceWav(int durationSeconds)
            => CreatePcmWav(durationSeconds, $"silence-{Guid.NewGuid():N}.wav", _ => 0);

        // Same format, low-amplitude white noise — non-speech audio that is NOT silence.
        private static string CreateNoiseWav(int durationSeconds)
        {
            var rng = new Random(42); // deterministic: same "noise" every run
            return CreatePcmWav(durationSeconds, $"noise-{Guid.NewGuid():N}.wav",
                _ => (short)rng.Next(-3000, 3001));
        }

        private static string CreatePcmWav(int durationSeconds, string fileName, Func<int, short> sampleAt)
        {
            var path = Path.Combine(Path.GetTempPath(), fileName);
            int sampleCount = 16000 * durationSeconds;
            int dataSize = sampleCount * 2;

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
            for (int i = 0; i < sampleCount; i++)
            {
                w.Write(sampleAt(i));
            }
            return path;
        }
    }
}

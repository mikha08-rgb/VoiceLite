using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using VoiceLite.Models;
using VoiceLite.Services;
using VoiceLite.Services.Audio;
using Xunit;

namespace VoiceLite.Tests.Services
{
    public class SileroVadServiceTests : IDisposable
    {
        private readonly string? modelPath;
        private readonly SileroVadService? service;

        public SileroVadServiceTests()
        {
            modelPath = SileroVadService.FindModelPath();
            if (modelPath != null)
                service = new SileroVadService(modelPath);
        }

        public void Dispose()
        {
            service?.Dispose();
        }

        private bool ModelAvailable => service != null;

        private static byte[] CreateWav(float[] samples)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            int dataSize = samples.Length * 2;
            int fileSize = 44 + dataSize - 8;

            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(fileSize);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(16000);
            writer.Write(16000 * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataSize);

            for (int i = 0; i < samples.Length; i++)
            {
                float clamped = Math.Clamp(samples[i], -1f, 1f);
                writer.Write((short)(clamped * 32767f));
            }

            writer.Flush();
            return ms.ToArray();
        }

        private static float[] GenerateSilence(int sampleCount)
        {
            return new float[sampleCount];
        }

        private static float[] GenerateSineWave(int sampleCount, float frequency = 440f, float amplitude = 0.8f)
        {
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                samples[i] = amplitude * MathF.Sin(2 * MathF.PI * frequency * i / 16000f);
            return samples;
        }

        [Fact]
        public void Constructor_ThrowsIfModelNotFound()
        {
            var act = () => new SileroVadService("nonexistent_model.onnx");
            act.Should().Throw<FileNotFoundException>();
        }

        [Fact]
        public void FindModelPath_ReturnsNullOrValidPath()
        {
            var path = SileroVadService.FindModelPath();
            if (path != null)
                File.Exists(path).Should().BeTrue();
        }

        [Fact]
        public void ProcessAudio_SilentWav_ReturnsOriginal()
        {
            if (!ModelAvailable) return;

            var silence = GenerateSilence(32000);
            var wavData = CreateWav(silence);

            var result = service!.ProcessAudio(wavData, 0.5f);

            // All silence -> should return original audio (safety fallback)
            result.Length.Should().Be(wavData.Length);
        }

        [Fact]
        public void ProcessAudio_SineWave_ReturnsSimilarLength()
        {
            if (!ModelAvailable) return;

            var tone = GenerateSineWave(32000);
            var wavData = CreateWav(tone);

            var result = service!.ProcessAudio(wavData, 0.5f);

            // Tone should be mostly preserved (allow some trimming at edges)
            result.Length.Should().BeGreaterThan(wavData.Length / 2);
        }

        [Fact]
        public void ProcessAudio_MixedToneAndSilence_ProcessesWithoutError()
        {
            if (!ModelAvailable) return;

            // 1s silence + 1s tone + 2s silence + 1s tone + 1s silence = 6s
            // Note: Silero VAD is trained on human speech, not sine waves.
            // Pure tones may not trigger speech detection, which is correct behavior.
            var silence1s = GenerateSilence(16000);
            var tone1s = GenerateSineWave(16000);
            var silence2s = GenerateSilence(32000);

            var combined = silence1s.Concat(tone1s).Concat(silence2s).Concat(tone1s).Concat(silence1s).ToArray();
            var wavData = CreateWav(combined);

            var result = service!.ProcessAudio(wavData, 0.5f);

            // Either trimmed (if VAD detected tone as speech-like) or original (safety fallback)
            result.Length.Should().BeLessThanOrEqualTo(wavData.Length);
            result.Length.Should().BeGreaterThan(100);
        }

        [Fact]
        public void ProcessAudio_HigherThreshold_TrimsMore()
        {
            if (!ModelAvailable) return;

            var quietTone = GenerateSineWave(32000, amplitude: 0.1f);
            var silence = GenerateSilence(32000);
            var combined = quietTone.Concat(silence).ToArray();
            var wavData = CreateWav(combined);

            var resultLow = service!.ProcessAudio(wavData, 0.2f);
            var resultHigh = service!.ProcessAudio(wavData, 0.9f);

            // Higher threshold should be more aggressive (shorter or same)
            resultHigh.Length.Should().BeLessThanOrEqualTo(resultLow.Length);
        }

        [Fact]
        public void ProcessAudio_ShortAudio_PassesThrough()
        {
            if (!ModelAvailable) return;

            // Less than 512 samples -- too short for VAD
            var shortSamples = GenerateSineWave(256);
            var wavData = CreateWav(shortSamples);

            var result = service!.ProcessAudio(wavData, 0.5f);

            result.Should().BeEquivalentTo(wavData);
        }

        [Fact]
        public void Disposal_DisposesCleanly()
        {
            if (modelPath == null) return;

            var disposableService = new SileroVadService(modelPath);

            // Force session creation
            var silence = CreateWav(GenerateSilence(32000));
            disposableService.ProcessAudio(silence, 0.5f);

            // Dispose should not throw
            disposableService.Dispose();

            // Double dispose should not throw
            disposableService.Dispose();
        }

        [Fact]
        public void Disposal_ThrowsOnUseAfterDispose()
        {
            if (modelPath == null) return;

            var disposableService = new SileroVadService(modelPath);
            disposableService.Dispose();

            var silence = CreateWav(GenerateSilence(32000));
            var act = () => disposableService.ProcessAudio(silence, 0.5f);
            act.Should().Throw<ObjectDisposedException>();
        }

        [Fact]
        public void AudioRecorder_VadSkippedWhenDisabled()
        {
            var recorder = new AudioRecorder();
            var testSettings = new Settings { EnableVAD = false };

            // Should not throw even with null service when VAD is disabled
            recorder.SetVadService(null, testSettings);
            recorder.Dispose();
        }

        [Fact]
        public void ExtractSamples_ValidWav_ReturnsCorrectCount()
        {
            if (!ModelAvailable) return;

            var samples = GenerateSineWave(1000);
            var wav = CreateWav(samples);

            var extracted = service!.ExtractSamples(wav);

            extracted.Length.Should().Be(1000);
        }

        [Fact]
        public void EncodeWav_RoundTrips()
        {
            if (!ModelAvailable) return;

            var original = GenerateSineWave(1000);
            var encoded = service!.EncodeWav(original);

            // Should be a valid WAV
            encoded.Length.Should().Be(44 + 1000 * 2);

            // Round-trip extract
            var roundTripped = service.ExtractSamples(encoded);
            roundTripped.Length.Should().Be(1000);

            // Values should be approximately equal (16-bit quantization)
            for (int i = 0; i < original.Length; i++)
                Math.Abs(original[i] - roundTripped[i]).Should().BeLessThan(0.001f);
        }

        [Fact]
        public void DetectSpeechSegments_AllAboveThreshold_SingleSegment()
        {
            if (!ModelAvailable) return;

            var probs = new float[] { 0.9f, 0.8f, 0.95f, 0.85f, 0.9f };

            var segments = service!.DetectSpeechSegments(probs, 0.5f, 200f, 500f, 5 * 512);

            segments.Count.Should().Be(1);
            segments[0].Start.Should().Be(0); // padded to 0
        }

        [Fact]
        public void DetectSpeechSegments_AllBelowThreshold_NoSegments()
        {
            if (!ModelAvailable) return;

            var probs = new float[] { 0.1f, 0.2f, 0.15f, 0.05f };

            var segments = service!.DetectSpeechSegments(probs, 0.5f, 200f, 500f, 4 * 512);

            segments.Count.Should().Be(0);
        }
    }
}

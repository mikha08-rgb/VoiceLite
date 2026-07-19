using System;
using AwesomeAssertions;
using NAudio.Wave;
using VoiceLite.Services.Audio;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// Guards the O(1) incremental running-sum optimization in AutomaticGainControl and
    /// SimpleNoiseGate: for a known input, the optimized filters must produce output
    /// matching a naive per-sample full-window recompute (the pre-optimization
    /// algorithm, re-implemented verbatim inside these tests) within a small epsilon.
    /// </summary>
    public class DspWindowSumTests
    {
        private const int SampleRate = 16000;

        // Tolerances. The optimized code carries the window sum in a double and
        // periodically re-syncs, so differences vs. the float-summed naive reference
        // are pure floating-point accumulation-order noise (~1e-7 on the RMS). The
        // per-sample max is looser than the mean because an epsilon-scale RMS
        // difference exactly at a gate/gain threshold can flip one branch decision
        // for a single sample, producing a transient ~1e-3-scale blip that decays.
        // A genuine semantic change (different window/formula) produces diffs on the
        // order of the signal itself (0.01–1.0) and fails both bounds.
        private const float MaxAbsEpsilon = 5e-3f;
        private const float MeanAbsEpsilon = 1e-4f;

        // ------------------------------------------------------------------
        // Test infrastructure
        // ------------------------------------------------------------------

        private sealed class ArraySampleProvider : ISampleProvider
        {
            private readonly float[] data;
            private int position;

            public ArraySampleProvider(float[] data)
            {
                this.data = data;
                WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 1);
            }

            public WaveFormat WaveFormat { get; }

            public int Read(float[] buffer, int offset, int count)
            {
                int n = Math.Min(count, data.Length - position);
                Array.Copy(data, position, buffer, offset, n);
                position += n;
                return n;
            }
        }

        /// <summary>Runs a filter to completion using the given read chunk size.</summary>
        private static float[] RunFilter(ISampleProvider filter, int totalSamples, int chunkSize)
        {
            var output = new float[totalSamples];
            var buffer = new float[chunkSize];
            int written = 0;

            while (written < totalSamples)
            {
                int read = filter.Read(buffer, 0, chunkSize);
                if (read == 0) break;
                Array.Copy(buffer, 0, output, written, read);
                written += read;
            }

            written.Should().Be(totalSamples);
            return output;
        }

        /// <summary>
        /// Deterministic speech-like test signal: loud sine burst, near-silence,
        /// medium noise, quiet sine — exercises both attack and release paths.
        /// </summary>
        private static float[] BuildTestSignal(int totalSamples)
        {
            var rng = new Random(42);
            var signal = new float[totalSamples];
            int quarter = totalSamples / 4;

            for (int i = 0; i < totalSamples; i++)
            {
                if (i < quarter)
                    signal[i] = 0.8f * MathF.Sin(2 * MathF.PI * 440f * i / SampleRate);
                else if (i < 2 * quarter)
                    signal[i] = 0.0005f * MathF.Sin(2 * MathF.PI * 200f * i / SampleRate);
                else if (i < 3 * quarter)
                    signal[i] = 0.2f * (float)(rng.NextDouble() * 2 - 1);
                else
                    signal[i] = 0.05f * MathF.Sin(2 * MathF.PI * 1000f * i / SampleRate);
            }

            return signal;
        }

        // ------------------------------------------------------------------
        // Naive reference implementations (the exact pre-optimization algorithm:
        // full float re-sum of the RMS window for every sample, mono).
        // ------------------------------------------------------------------

        private static float[] NaiveAgcReference(
            float[] input, float targetLevel = 0.1f, float attackTimeMs = 10.0f, float releaseTimeMs = 500.0f)
        {
            const float MIN_GAIN = 0.1f;
            const float MAX_GAIN = 10.0f;

            targetLevel = Math.Clamp(targetLevel, 0.01f, 0.5f);
            float attackCoefficient = (float)Math.Exp(-1.0 / (SampleRate * attackTimeMs / 1000.0));
            float releaseCoefficient = (float)Math.Exp(-1.0 / (SampleRate * releaseTimeMs / 1000.0));
            int rmsWindowSize = (int)(SampleRate * 0.05f);
            var rmsBuffer = new float[rmsWindowSize];
            int rmsBufferIndex = 0;
            float currentGain = 1.0f;

            var output = new float[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                float sample = input[i];
                rmsBuffer[rmsBufferIndex] = sample * sample;
                rmsBufferIndex = (rmsBufferIndex + 1) % rmsWindowSize;

                // mono: gain recalculated for every sample
                float sumSquares = 0;
                for (int j = 0; j < rmsWindowSize; j++)
                    sumSquares += rmsBuffer[j];
                float rms = (float)Math.Sqrt(sumSquares / rmsWindowSize);

                float desiredGain = rms > 0.0001f ? targetLevel / rms : 1.0f;
                desiredGain = Math.Clamp(desiredGain, MIN_GAIN, MAX_GAIN);

                float coefficient = desiredGain < currentGain ? attackCoefficient : releaseCoefficient;
                currentGain = coefficient * currentGain + (1.0f - coefficient) * desiredGain;

                output[i] = sample * currentGain;
            }

            return output;
        }

        private static float[] NaiveNoiseGateReference(
            float[] input, float threshold = 0.01f, float reductionFactor = 0.5f,
            float attackTimeMs = 10.0f, float releaseTimeMs = 100.0f)
        {
            threshold = Math.Clamp(threshold, 0.001f, 0.1f);
            reductionFactor = Math.Clamp(reductionFactor, 0.1f, 0.9f);
            float attackCoefficient = (float)Math.Exp(-1.0 / (SampleRate * attackTimeMs / 1000.0));
            float releaseCoefficient = (float)Math.Exp(-1.0 / (SampleRate * releaseTimeMs / 1000.0));
            int rmsWindowSize = (int)(SampleRate * 0.1f);
            var rmsBuffer = new float[rmsWindowSize];
            int rmsBufferIndex = 0;
            float currentGain = 1.0f;

            var output = new float[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                float sample = input[i];
                rmsBuffer[rmsBufferIndex] = sample * sample;
                rmsBufferIndex = (rmsBufferIndex + 1) % rmsWindowSize;

                // mono: gain recalculated for every sample
                float sumSquares = 0;
                for (int j = 0; j < rmsWindowSize; j++)
                    sumSquares += rmsBuffer[j];
                float rms = (float)Math.Sqrt(sumSquares / rmsWindowSize);

                float targetGain = rms > threshold ? 1.0f : reductionFactor;

                float coefficient = targetGain < currentGain ? attackCoefficient : releaseCoefficient;
                currentGain = coefficient * currentGain + (1.0f - coefficient) * targetGain;

                output[i] = sample * currentGain;
            }

            return output;
        }

        private static void AssertMatches(float[] actual, float[] expected)
        {
            actual.Length.Should().Be(expected.Length);

            double sumAbsDiff = 0;
            for (int i = 0; i < expected.Length; i++)
            {
                float diff = Math.Abs(actual[i] - expected[i]);
                sumAbsDiff += diff;
                if (diff > MaxAbsEpsilon)
                {
                    throw new Xunit.Sdk.XunitException(
                        $"Sample {i} diverged from naive reference: actual={actual[i]}, expected={expected[i]}, diff={diff}");
                }
            }

            double meanAbsDiff = sumAbsDiff / expected.Length;
            meanAbsDiff.Should().BeLessThan(MeanAbsEpsilon,
                "the optimized filter must track the naive reference except for isolated float-noise blips");
        }

        // ------------------------------------------------------------------
        // AutomaticGainControl
        // ------------------------------------------------------------------

        [Fact]
        public void Agc_MatchesNaiveReference_MixedSignal()
        {
            int totalSamples = 32000; // 2s
            var signal = BuildTestSignal(totalSamples);

            var agc = new AutomaticGainControl(new ArraySampleProvider(signal));
            var actual = RunFilter(agc, totalSamples, 512);

            AssertMatches(actual, NaiveAgcReference(signal));
        }

        [Fact]
        public void Agc_MatchesNaiveReference_OddChunkSizes()
        {
            // Chunk size not aligned to the RMS window: window state must carry
            // correctly across Read() boundaries.
            int totalSamples = 16000;
            var signal = BuildTestSignal(totalSamples);

            var agc = new AutomaticGainControl(new ArraySampleProvider(signal));
            var actual = RunFilter(agc, totalSamples, 333);

            AssertMatches(actual, NaiveAgcReference(signal));
        }

        [Fact]
        public void Agc_MatchesNaiveReference_PastResyncInterval()
        {
            // Longer than the periodic exact re-sum interval (65536 samples), so the
            // drift-guard resync path executes and must not change the output.
            int totalSamples = 80000; // 5s
            var signal = BuildTestSignal(totalSamples);

            var agc = new AutomaticGainControl(new ArraySampleProvider(signal));
            var actual = RunFilter(agc, totalSamples, 4096);

            AssertMatches(actual, NaiveAgcReference(signal));
        }

        // ------------------------------------------------------------------
        // SimpleNoiseGate
        // ------------------------------------------------------------------

        [Fact]
        public void NoiseGate_MatchesNaiveReference_MixedSignal()
        {
            int totalSamples = 32000; // 2s
            var signal = BuildTestSignal(totalSamples);

            var gate = new SimpleNoiseGate(new ArraySampleProvider(signal));
            var actual = RunFilter(gate, totalSamples, 512);

            AssertMatches(actual, NaiveNoiseGateReference(signal));
        }

        [Fact]
        public void NoiseGate_MatchesNaiveReference_OddChunkSizes()
        {
            int totalSamples = 16000;
            var signal = BuildTestSignal(totalSamples);

            var gate = new SimpleNoiseGate(new ArraySampleProvider(signal));
            var actual = RunFilter(gate, totalSamples, 333);

            AssertMatches(actual, NaiveNoiseGateReference(signal));
        }

        [Fact]
        public void NoiseGate_MatchesNaiveReference_PastResyncInterval()
        {
            int totalSamples = 80000; // 5s
            var signal = BuildTestSignal(totalSamples);

            var gate = new SimpleNoiseGate(new ArraySampleProvider(signal));
            var actual = RunFilter(gate, totalSamples, 4096);

            AssertMatches(actual, NaiveNoiseGateReference(signal));
        }

        [Fact]
        public void NoiseGate_MatchesNaiveReference_NonDefaultParameters()
        {
            int totalSamples = 16000;
            var signal = BuildTestSignal(totalSamples);

            var gate = new SimpleNoiseGate(new ArraySampleProvider(signal),
                threshold: 0.02f, reductionFactor: 0.3f, attackTimeMs: 5f, releaseTimeMs: 200f);
            var actual = RunFilter(gate, totalSamples, 1024);

            AssertMatches(actual, NaiveNoiseGateReference(
                signal, threshold: 0.02f, reductionFactor: 0.3f, attackTimeMs: 5f, releaseTimeMs: 200f));
        }

        [Fact]
        public void Agc_MatchesNaiveReference_NonDefaultParameters()
        {
            int totalSamples = 16000;
            var signal = BuildTestSignal(totalSamples);

            var agc = new AutomaticGainControl(new ArraySampleProvider(signal),
                targetLevel: 0.2f, attackTimeMs: 5f, releaseTimeMs: 250f);
            var actual = RunFilter(agc, totalSamples, 1024);

            AssertMatches(actual, NaiveAgcReference(
                signal, targetLevel: 0.2f, attackTimeMs: 5f, releaseTimeMs: 250f));
        }
    }
}

using System;
using NAudio.Wave;

namespace VoiceLite.Services.Audio
{
    /// <summary>
    /// Simple noise gate to reduce background hum and noise during silence.
    /// Uses RMS-based detection with smooth gain transitions to avoid clicks/pops.
    /// </summary>
    public class SimpleNoiseGate : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly int channels;

        // Noise gate parameters
        private readonly float threshold;
        private readonly float attackTime;
        private readonly float releaseTime;
        private readonly float reductionFactor;

        // RMS calculation window (100ms as specified)
        private readonly float[] rmsBuffer;
        private int rmsBufferIndex = 0;
        private readonly int rmsWindowSize;

        // Incremental running sum of rmsBuffer (see Read). double, not float, so the
        // add/subtract updates don't accumulate rounding error sample-over-sample.
        private double rmsRunningSum = 0.0;

        // Periodically recompute the exact sum to bound any residual floating-point
        // drift on very long runs. ~every 4s of audio at 16kHz — cost is negligible.
        private int samplesSinceResync = 0;
        private const int RESYNC_INTERVAL_SAMPLES = 65536;

        // Current gate state
        private float currentGain = 1.0f;
        private readonly float attackCoefficient;
        private readonly float releaseCoefficient;

        public WaveFormat WaveFormat => source.WaveFormat;

        /// <summary>
        /// Creates a simple noise gate with smooth gain reduction.
        /// </summary>
        /// <param name="source">Input audio source</param>
        /// <param name="threshold">RMS threshold below which gate activates (default: 0.01 = -40 dBFS)</param>
        /// <param name="reductionFactor">How much to reduce gain when gate is active (default: 0.5 = -6 dB)</param>
        /// <param name="attackTimeMs">Attack time in milliseconds (default: 10ms)</param>
        /// <param name="releaseTimeMs">Release time in milliseconds (default: 100ms)</param>
        public SimpleNoiseGate(ISampleProvider source,
                              float threshold = 0.01f,
                              float reductionFactor = 0.5f,
                              float attackTimeMs = 10.0f,
                              float releaseTimeMs = 100.0f)
        {
            this.source = source;
            this.channels = source.WaveFormat.Channels;
            this.threshold = Math.Clamp(threshold, 0.001f, 0.1f);
            this.reductionFactor = Math.Clamp(reductionFactor, 0.1f, 0.9f);

            // Calculate attack/release coefficients for smooth transitions
            float sampleRate = source.WaveFormat.SampleRate;
            this.attackTime = attackTimeMs;
            this.releaseTime = releaseTimeMs;
            attackCoefficient = (float)Math.Exp(-1.0 / (sampleRate * attackTimeMs / 1000.0));
            releaseCoefficient = (float)Math.Exp(-1.0 / (sampleRate * releaseTimeMs / 1000.0));

            // RMS window size: 100ms as specified in plan
            rmsWindowSize = (int)(sampleRate * 0.1f);
            rmsBuffer = new float[rmsWindowSize];
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = source.Read(buffer, offset, count);

            for (int i = 0; i < samplesRead; i++)
            {
                float sample = buffer[offset + i];

                // Update RMS buffer with an incremental running sum: subtract the
                // squared sample leaving the window, add the one entering. This is a
                // pure performance optimization (O(1) per sample instead of re-summing
                // the whole window per sample — the loop runs on the capture path);
                // same window, same formula, output identical to the naive per-sample
                // recompute up to floating-point accumulation order.
                float squared = sample * sample;
                rmsRunningSum += (double)squared - rmsBuffer[rmsBufferIndex];
                rmsBuffer[rmsBufferIndex] = squared;
                rmsBufferIndex = (rmsBufferIndex + 1) % rmsWindowSize;

                // Periodic exact re-sum guards against long-run drift of the running sum.
                if (++samplesSinceResync >= RESYNC_INTERVAL_SAMPLES)
                {
                    samplesSinceResync = 0;
                    double exactSum = 0.0;
                    for (int j = 0; j < rmsWindowSize; j++)
                        exactSum += rmsBuffer[j];
                    rmsRunningSum = exactSum;
                }

                // Per-frame gain update (for mono — the only real capture format —
                // this branch is taken every sample; kept for multi-channel safety).
                if (i % channels == 0)
                {
                    // Current RMS over the window (clamp: drift could dip epsilon below 0)
                    float rms = (float)Math.Sqrt(Math.Max(rmsRunningSum, 0.0) / rmsWindowSize);

                    // Determine target gain: full gain if above threshold, reduced if below
                    float targetGain = rms > threshold ? 1.0f : reductionFactor;

                    // Smooth gain transition with attack/release
                    // Fast attack (close gate quickly), slower release (open gate gradually)
                    float coefficient = targetGain < currentGain ? attackCoefficient : releaseCoefficient;
                    currentGain = coefficient * currentGain + (1.0f - coefficient) * targetGain;
                }

                // Apply gain
                buffer[offset + i] = sample * currentGain;
            }

            return samplesRead;
        }
    }
}

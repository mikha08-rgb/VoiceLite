using System;
using NAudio.Wave;

namespace VoiceLite.Services.Audio
{
    /// <summary>
    /// Automatic Gain Control (AGC) for normalizing audio volume levels in real-time.
    /// Uses RMS-based envelope tracking with attack/release times for smooth gain adjustments.
    /// </summary>
    public class AutomaticGainControl : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly int channels;

        // Target RMS level (approximately -20 dBFS, industry standard)
        private readonly float targetLevel;

        // Current gain multiplier (starts at 1.0)
        private float currentGain = 1.0f;

        // Attack/release coefficients for smooth gain changes
        private readonly float attackCoefficient;
        private readonly float releaseCoefficient;

        // RMS calculation window
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

        // Maximum gain limits to prevent extreme amplification
        private const float MIN_GAIN = 0.1f;  // -20 dB
        private const float MAX_GAIN = 10.0f; // +20 dB

        public WaveFormat WaveFormat => source.WaveFormat;

        /// <summary>
        /// Creates an Automatic Gain Control processor.
        /// </summary>
        /// <param name="source">Input audio source</param>
        /// <param name="targetLevel">Target RMS level (0.0-1.0, default 0.1 = -20 dBFS)</param>
        /// <param name="attackTimeMs">Attack time in milliseconds (how fast to reduce gain, default 10ms)</param>
        /// <param name="releaseTimeMs">Release time in milliseconds (how fast to increase gain, default 500ms)</param>
        public AutomaticGainControl(ISampleProvider source,
                                    float targetLevel = 0.1f,
                                    float attackTimeMs = 10.0f,
                                    float releaseTimeMs = 500.0f)
        {
            this.source = source;
            this.channels = source.WaveFormat.Channels;
            this.targetLevel = Math.Clamp(targetLevel, 0.01f, 0.5f);

            // Calculate attack/release coefficients
            float sampleRate = source.WaveFormat.SampleRate;
            attackCoefficient = (float)Math.Exp(-1.0 / (sampleRate * attackTimeMs / 1000.0));
            releaseCoefficient = (float)Math.Exp(-1.0 / (sampleRate * releaseTimeMs / 1000.0));

            // RMS window size: 50ms
            rmsWindowSize = (int)(sampleRate * 0.05f);
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

                    // Calculate desired gain
                    float desiredGain = rms > 0.0001f ? targetLevel / rms : 1.0f;
                    desiredGain = Math.Clamp(desiredGain, MIN_GAIN, MAX_GAIN);

                    // Smooth gain adjustment with attack/release
                    float coefficient = desiredGain < currentGain ? attackCoefficient : releaseCoefficient;
                    currentGain = coefficient * currentGain + (1.0f - coefficient) * desiredGain;
                }

                // Apply gain
                buffer[offset + i] = sample * currentGain;
            }

            return samplesRead;
        }
    }
}

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

                // Update RMS buffer
                rmsBuffer[rmsBufferIndex] = sample * sample;
                rmsBufferIndex = (rmsBufferIndex + 1) % rmsWindowSize;

                // Calculate RMS every N samples (optimization)
                if (i % channels == 0)
                {
                    // Calculate current RMS level
                    float sumSquares = 0;
                    for (int j = 0; j < rmsWindowSize; j++)
                    {
                        sumSquares += rmsBuffer[j];
                    }
                    float rms = (float)Math.Sqrt(sumSquares / rmsWindowSize);

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

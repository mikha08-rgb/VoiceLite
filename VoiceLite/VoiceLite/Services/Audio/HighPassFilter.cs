using System;
using NAudio.Wave;

namespace VoiceLite.Services.Audio
{
    /// <summary>
    /// High-pass filter to remove low-frequency rumble and wind noise below specified cutoff frequency.
    /// Uses a biquad IIR filter for efficient real-time processing.
    /// </summary>
    public class HighPassFilter : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly int channels;
        private readonly float[] z1; // Previous input samples (per channel)
        private readonly float[] z2; // Previous previous input samples
        private readonly float[] oz1; // Previous output samples
        private readonly float[] oz2; // Previous previous output samples

        // Biquad filter coefficients
        private readonly float a0, a1, a2, b1, b2;

        public WaveFormat WaveFormat => source.WaveFormat;

        /// <summary>
        /// Creates a high-pass filter with specified cutoff frequency.
        /// </summary>
        /// <param name="source">Input audio source</param>
        /// <param name="cutoffFrequency">Cutoff frequency in Hz (default: 80 Hz for rumble removal)</param>
        public HighPassFilter(ISampleProvider source, float cutoffFrequency = 80.0f)
        {
            this.source = source;
            this.channels = source.WaveFormat.Channels;

            // Initialize state arrays per channel
            z1 = new float[channels];
            z2 = new float[channels];
            oz1 = new float[channels];
            oz2 = new float[channels];

            // Calculate biquad coefficients for high-pass filter
            float sampleRate = source.WaveFormat.SampleRate;
            float c = (float)Math.Tan(Math.PI * cutoffFrequency / sampleRate);
            float csq = c * c;
            float sqrt2c = (float)(Math.Sqrt(2) * c);
            float d = csq + sqrt2c + 1;

            // High-pass filter coefficients
            a0 = 1.0f / d;
            a1 = -2.0f * a0;
            a2 = a0;
            b1 = 2.0f * (csq - 1) / d;
            b2 = (csq - sqrt2c + 1) / d;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = source.Read(buffer, offset, count);

            // Process each sample with biquad filter
            for (int i = 0; i < samplesRead; i++)
            {
                int channel = i % channels;
                float input = buffer[offset + i];

                // Biquad difference equation: y[n] = a0*x[n] + a1*x[n-1] + a2*x[n-2] - b1*y[n-1] - b2*y[n-2]
                float output = a0 * input + a1 * z1[channel] + a2 * z2[channel]
                             - b1 * oz1[channel] - b2 * oz2[channel];

                // Update state
                z2[channel] = z1[channel];
                z1[channel] = input;
                oz2[channel] = oz1[channel];
                oz1[channel] = output;

                buffer[offset + i] = output;
            }

            return samplesRead;
        }
    }
}

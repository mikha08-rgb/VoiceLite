using System;
using System.IO;
using NAudio.Wave;

namespace VoiceLite.Services.Audio
{
    /// <summary>
    /// Orchestrates audio preprocessing pipeline: High-pass filter → Noise gate → AGC.
    /// Improves transcription quality by removing low-frequency noise, reducing background hum,
    /// and normalizing volume (industry-standard order to avoid amplifying noise).
    /// </summary>
    public class AudioPreprocessor : ISampleProvider
    {
        private readonly ISampleProvider processedSource;

        public WaveFormat WaveFormat { get; }

        /// <summary>
        /// Creates an audio preprocessing pipeline with default settings optimized for speech.
        /// </summary>
        /// <param name="source">Input audio source</param>
        public AudioPreprocessor(ISampleProvider source)
            : this(source, new AudioPreprocessingSettings())
        {
        }

        /// <summary>
        /// Creates an audio preprocessing pipeline with custom settings.
        /// </summary>
        /// <param name="source">Input audio source</param>
        /// <param name="settings">Preprocessing configuration</param>
        public AudioPreprocessor(ISampleProvider source, AudioPreprocessingSettings settings)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            WaveFormat = source.WaveFormat;

            // Build processing chain based on enabled features
            // Order: HighPass → NoiseGate → AGC (industry standard to avoid amplifying noise)
            ISampleProvider current = source;

            // Stage 1: High-pass filter (removes low-frequency rumble/wind noise)
            if (settings.EnableHighPassFilter)
            {
                current = new HighPassFilter(current, settings.HighPassCutoffHz);
            }

            // Stage 2: Noise gate (reduces background noise during silence)
            // Applied BEFORE AGC to avoid amplifying background noise
            if (settings.EnableNoiseGate)
            {
                current = new SimpleNoiseGate(
                    current,
                    threshold: settings.NoiseGateThreshold,
                    reductionFactor: settings.NoiseGateReduction,
                    attackTimeMs: settings.NoiseGateAttackMs,
                    releaseTimeMs: settings.NoiseGateReleaseMs);
            }

            // Stage 3: Automatic Gain Control (normalizes volume levels)
            // Applied LAST to normalize the clean signal without amplifying noise
            if (settings.EnableAGC)
            {
                current = new AutomaticGainControl(
                    current,
                    targetLevel: settings.AGCTargetLevel,
                    attackTimeMs: settings.AGCAttackTimeMs,
                    releaseTimeMs: settings.AGCReleaseTimeMs);
            }

            processedSource = current;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            return processedSource.Read(buffer, offset, count);
        }

        /// <summary>
        /// Processes an audio file with preprocessing and saves the result to a new file.
        /// </summary>
        /// <param name="inputPath">Input WAV file path</param>
        /// <param name="outputPath">Output WAV file path</param>
        /// <param name="settings">Preprocessing settings (null for defaults)</param>
        public static void ProcessAudioFile(string inputPath, string outputPath, AudioPreprocessingSettings? settings = null)
        {
            settings ??= new AudioPreprocessingSettings();

            using (var reader = new WaveFileReader(inputPath))
            {
                // Convert to ISampleProvider (handles any bit depth)
                var sampleProvider = reader.ToSampleProvider();

                // Apply preprocessing
                var preprocessed = new AudioPreprocessor(sampleProvider, settings);

                // Write to output file
                WaveFileWriter.CreateWaveFile16(outputPath, preprocessed);
            }
        }

        /// <summary>
        /// Processes audio data in memory and returns the processed WAV file as a byte array.
        /// </summary>
        /// <param name="audioData">Input WAV file data</param>
        /// <param name="settings">Preprocessing settings (null for defaults)</param>
        /// <returns>Processed WAV file as byte array</returns>
        public static byte[] ProcessAudioData(byte[] audioData, AudioPreprocessingSettings? settings = null)
        {
            settings ??= new AudioPreprocessingSettings();

            using (var inputStream = new MemoryStream(audioData))
            using (var reader = new WaveFileReader(inputStream))
            {
                // Convert to ISampleProvider
                var sampleProvider = reader.ToSampleProvider();

                // Apply preprocessing
                var preprocessed = new AudioPreprocessor(sampleProvider, settings);

                // Write to memory stream
                using (var outputStream = new MemoryStream())
                using (var writer = new WaveFileWriter(outputStream, reader.WaveFormat))
                {
                    // Read and write samples in chunks
                    float[] buffer = new float[reader.WaveFormat.SampleRate]; // 1 second buffer
                    int samplesRead;
                    while ((samplesRead = preprocessed.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        writer.WriteSamples(buffer, 0, samplesRead);
                    }

                    writer.Flush();
                    return outputStream.ToArray();
                }
            }
        }
    }

    /// <summary>
    /// Configuration settings for audio preprocessing pipeline.
    /// Defaults are optimized for speech transcription quality.
    /// </summary>
    public class AudioPreprocessingSettings
    {
        // High-pass filter settings
        public bool EnableHighPassFilter { get; set; } = true;
        public float HighPassCutoffHz { get; set; } = 80.0f; // Remove frequencies below 80 Hz (rumble/wind)

        // AGC settings
        public bool EnableAGC { get; set; } = true;
        public float AGCTargetLevel { get; set; } = 0.1f;    // -20 dBFS (industry standard)
        public float AGCAttackTimeMs { get; set; } = 10.0f;  // Fast response to loud signals
        public float AGCReleaseTimeMs { get; set; } = 500.0f; // Slow recovery to quiet

        // Noise gate settings
        public bool EnableNoiseGate { get; set; } = true;
        public float NoiseGateThreshold { get; set; } = 0.01f; // -40 dBFS
        public float NoiseGateReduction { get; set; } = 0.5f;  // Reduce by 50% (not mute)
        public float NoiseGateAttackMs { get; set; } = 10.0f;  // Fast gate close
        public float NoiseGateReleaseMs { get; set; } = 100.0f; // Smooth gate open

        /// <summary>
        /// Creates default settings optimized for speech transcription.
        /// </summary>
        public AudioPreprocessingSettings()
        {
            // All defaults set via property initializers above
        }
    }
}

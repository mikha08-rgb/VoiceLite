using System;
using System.IO;
using NAudio.Wave;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    public static class AudioPreprocessor
    {
        private const float StaticBoost = 1.2f; // Slightly higher for better clarity
        private const int NoiseProfileSamples = 4410; // 100ms at 44.1kHz for noise profiling

        public static void ProcessAudioFile(string audioFilePath, Settings settings)
        {
            // Always apply some preprocessing for optimal accuracy
            // Retry logic to handle file locking issues
            const int maxRetries = 5;
            const int retryDelayMs = 100;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    // Apply VAD first if enabled (trim silence)
                    if (settings.UseVAD)
                    {
                        TrimSilenceWithVAD(audioFilePath, settings);
                    }

                    using var reader = new AudioFileReader(audioFilePath);
                var waveFormat = reader.WaveFormat;
                var tempFile = audioFilePath + ".tmp";

                // Read entire audio for better processing
                var totalSamples = (int)(reader.Length / sizeof(float));
                var allSamples = new float[totalSamples];
                reader.Read(allSamples, 0, totalSamples);

                // Apply adaptive noise reduction
                ApplyAdaptiveNoiseReduction(allSamples, waveFormat.SampleRate, settings);

                // Apply spectral subtraction for cleaner audio
                if (settings.EnableNoiseSuppression)
                {
                    ApplySpectralGating(allSamples, waveFormat.SampleRate);
                }

                using (var writer = new WaveFileWriter(tempFile, waveFormat))
                {
                    var buffer = new float[waveFormat.SampleRate * waveFormat.Channels];
                    int position = 0;

                    while (position < allSamples.Length)
                    {
                        int toRead = Math.Min(buffer.Length, allSamples.Length - position);
                        Array.Copy(allSamples, position, buffer, 0, toRead);

                        ApplyNoiseGate(buffer, toRead, settings);
                        ApplyEnhancedAutomaticGain(buffer, toRead, settings);

                        if (!settings.EnableAutomaticGain)
                        {
                            ApplyStaticBoost(buffer, toRead);
                        }

                        // Apply final peak normalization
                        NormalizePeaks(buffer, toRead);

                        writer.WriteSamples(buffer, 0, toRead);
                        position += toRead;
                    }
                }

                    File.Delete(audioFilePath);
                    File.Move(tempFile, audioFilePath);

                    // Success - exit retry loop
                    return;
                }
                catch (IOException ioEx) when (attempt < maxRetries - 1)
                {
                    // File is locked - wait and retry
                    ErrorLogger.LogMessage($"AudioPreprocessor: File locked on attempt {attempt + 1}/{maxRetries}, retrying in {retryDelayMs}ms... ({ioEx.Message})");
                    System.Threading.Thread.Sleep(retryDelayMs);
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError($"AudioPreprocessor (attempt {attempt + 1}/{maxRetries})", ex);

                    // On last attempt or non-IO error, clean up and give up
                    if (attempt == maxRetries - 1)
                    {
                        try
                        {
                            if (File.Exists(audioFilePath + ".tmp"))
                            {
                                File.Delete(audioFilePath + ".tmp");
                            }
                        }
                        catch (Exception cleanupEx)
                        {
                            ErrorLogger.LogError("AudioPreprocessor cleanup", cleanupEx);
                        }

                        // Re-throw on last attempt so caller knows preprocessing failed
                        throw;
                    }
                }
            }
        }

        private static void ApplyNoiseGate(float[] buffer, int count, Settings settings)
        {
            if (!settings.EnableNoiseSuppression)
                return;

            float threshold = (float)Math.Clamp(settings.NoiseGateThreshold, 0.001, 0.2);
            for (int i = 0; i < count; i++)
            {
                if (Math.Abs(buffer[i]) < threshold)
                {
                    buffer[i] = 0f;
                }
            }
        }

        private static void ApplyAdaptiveNoiseReduction(float[] samples, int sampleRate, Settings settings)
        {
            if (!settings.EnableNoiseSuppression || samples.Length < NoiseProfileSamples)
                return;

            // Estimate noise from first 100ms (usually silence before speech)
            float noiseLevel = 0;
            for (int i = 0; i < Math.Min(NoiseProfileSamples, samples.Length); i++)
            {
                noiseLevel += Math.Abs(samples[i]);
            }
            noiseLevel /= NoiseProfileSamples;
            noiseLevel *= 1.5f; // Add margin

            // Apply adaptive noise reduction
            for (int i = 0; i < samples.Length; i++)
            {
                if (Math.Abs(samples[i]) < noiseLevel)
                {
                    samples[i] *= 0.1f; // Strongly attenuate noise
                }
            }
        }

        private static void ApplySpectralGating(float[] samples, int sampleRate)
        {
            // Simple spectral gating - removes consistent background noise
            const int frameSize = 2048;
            if (samples.Length < frameSize) return;

            for (int i = 0; i < samples.Length - frameSize; i += frameSize / 2)
            {
                float frameEnergy = 0;
                for (int j = 0; j < frameSize && i + j < samples.Length; j++)
                {
                    frameEnergy += samples[i + j] * samples[i + j];
                }
                frameEnergy /= frameSize;

                // Gate frames with very low energy
                if (frameEnergy < 0.0001f)
                {
                    for (int j = 0; j < frameSize && i + j < samples.Length; j++)
                    {
                        samples[i + j] *= 0.05f;
                    }
                }
            }
        }

        private static void NormalizePeaks(float[] buffer, int count)
        {
            float maxPeak = 0;
            for (int i = 0; i < count; i++)
            {
                float abs = Math.Abs(buffer[i]);
                if (abs > maxPeak) maxPeak = abs;
            }

            if (maxPeak > 0.95f)
            {
                float scale = 0.95f / maxPeak;
                for (int i = 0; i < count; i++)
                {
                    buffer[i] *= scale;
                }
            }
        }

        private static void ApplyEnhancedAutomaticGain(float[] buffer, int count, Settings settings)
        {
            if (!settings.EnableAutomaticGain)
                return;

            // Calculate RMS with better accuracy
            double sumSquares = 0;
            float maxAbs = 0f;
            int nonSilentSamples = 0;

            for (int i = 0; i < count; i++)
            {
                float sample = buffer[i];
                float abs = Math.Abs(sample);
                if (abs > 0.001f) // Count only non-silent samples
                {
                    sumSquares += sample * sample;
                    nonSilentSamples++;
                }
                if (abs > maxAbs)
                {
                    maxAbs = abs;
                }
            }

            if (nonSilentSamples == 0)
                return;

            float rms = (float)Math.Sqrt(sumSquares / nonSilentSamples);
            if (rms < 1e-6f)
                return;

            // More aggressive gain for quiet audio
            float target = Math.Clamp(settings.TargetRmsLevel * 1.2f, 0.1f, 0.9f);
            float gain = target / rms;

            // Prevent clipping
            if (maxAbs > 0f)
            {
                gain = Math.Min(gain, 0.95f / maxAbs);
            }

            // Apply smooth gain with soft limiting
            gain = Math.Clamp(gain, 0.5f, 5.0f); // Wider gain range

            for (int i = 0; i < count; i++)
            {
                buffer[i] *= gain;
                // Soft limiting to prevent harsh clipping
                if (buffer[i] > 0.95f) buffer[i] = 0.95f + (buffer[i] - 0.95f) * 0.1f;
                if (buffer[i] < -0.95f) buffer[i] = -0.95f + (buffer[i] + 0.95f) * 0.1f;
            }
        }

        private static void ApplyAutomaticGain(float[] buffer, int count, Settings settings)
        {
            if (!settings.EnableAutomaticGain)
                return;

            double sumSquares = 0;
            float maxAbs = 0f;
            for (int i = 0; i < count; i++)
            {
                float sample = buffer[i];
                float abs = Math.Abs(sample);
                if (abs > maxAbs)
                {
                    maxAbs = abs;
                }
                sumSquares += sample * sample;
            }

            if (count == 0)
                return;

            float rms = (float)Math.Sqrt(sumSquares / count);
            if (rms < 1e-6f)
                return;

            float target = Math.Clamp(settings.TargetRmsLevel, 0.05f, 0.9f);
            float gain = target / rms;

            if (maxAbs > 0f)
            {
                gain = Math.Min(gain, 0.99f / maxAbs);
            }

            if (Math.Abs(gain - 1f) < 0.05f)
                return;

            for (int i = 0; i < count; i++)
            {
                buffer[i] *= gain;
            }
        }

        private static void ApplyStaticBoost(float[] buffer, int count)
        {
            float maxAbs = 0f;
            for (int i = 0; i < count; i++)
            {
                float abs = Math.Abs(buffer[i]);
                if (abs > maxAbs)
                {
                    maxAbs = abs;
                }
            }

            float boost = StaticBoost;
            if (maxAbs > 0f && boost * maxAbs > 0.99f)
            {
                boost = 0.99f / maxAbs;
            }

            for (int i = 0; i < count; i++)
            {
                buffer[i] *= boost;
            }
        }

        private static void TrimSilenceWithVAD(string audioFilePath, Settings settings)
        {
            // Simple VAD implementation - trim silence from start and end
            try
            {
                using var reader = new AudioFileReader(audioFilePath);
                var waveFormat = reader.WaveFormat;

                // Read entire audio into memory (reasonable for short recordings)
                var totalSamples = (int)(reader.Length / sizeof(float));
                var audioData = new float[totalSamples];
                reader.Read(audioData, 0, totalSamples);

                // Find speech boundaries using energy-based VAD
                var silenceThreshold = 0.01f; // Energy threshold for silence detection
                var windowSize = waveFormat.SampleRate / 50; // 20ms windows
                var speechStart = 0;
                var speechEnd = totalSamples - 1;

                // Find start of speech (scan forward)
                for (int i = 0; i < totalSamples - windowSize; i += windowSize / 2)
                {
                    var energy = CalculateEnergy(audioData, i, Math.Min(windowSize, totalSamples - i));
                    if (energy > silenceThreshold)
                    {
                        // Found speech, back up a bit to avoid cutting off start
                        speechStart = Math.Max(0, i - windowSize);
                        break;
                    }
                }

                // Find end of speech (scan backward)
                for (int i = totalSamples - windowSize; i >= speechStart; i -= windowSize / 2)
                {
                    var energy = CalculateEnergy(audioData, i, Math.Min(windowSize, totalSamples - i));
                    if (energy > silenceThreshold)
                    {
                        // Found speech, add a bit of buffer to avoid cutting off end
                        speechEnd = Math.Min(totalSamples - 1, i + windowSize * 2);
                        break;
                    }
                }

                // Only rewrite if we actually trimmed something significant (>100ms)
                var trimmedSamples = speechStart + (totalSamples - 1 - speechEnd);
                if (trimmedSamples > waveFormat.SampleRate / 10) // More than 100ms trimmed
                {
                    var tempFile = audioFilePath + ".vad.tmp";
                    using (var writer = new WaveFileWriter(tempFile, waveFormat))
                    {
                        var speechLength = speechEnd - speechStart + 1;
                        writer.WriteSamples(audioData, speechStart, speechLength);
                    }

                    File.Delete(audioFilePath);
                    File.Move(tempFile, audioFilePath);

                    // Log for metrics if enabled
                    if (settings.EnableMetrics)
                    {
                        var trimmedMs = (trimmedSamples * 1000) / waveFormat.SampleRate;
                        ErrorLogger.LogMessage($"VAD trimmed {trimmedMs}ms of silence");
                    }
                }
            }
            catch (Exception ex)
            {
                // VAD is optional - don't fail if it doesn't work
                ErrorLogger.LogError("VAD processing (non-critical)", ex);
            }
        }

        private static float CalculateEnergy(float[] buffer, int start, int length)
        {
            float sum = 0;
            var end = Math.Min(start + length, buffer.Length);
            for (int i = start; i < end; i++)
            {
                sum += buffer[i] * buffer[i];
            }
            return sum / length;
        }
    }
}

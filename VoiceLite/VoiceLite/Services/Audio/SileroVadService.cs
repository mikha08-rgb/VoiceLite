using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace VoiceLite.Services.Audio
{
    /// <summary>
    /// Silero VAD v5 preprocessing: detects speech segments in audio and trims silence.
    /// Runs ONNX inference on 512-sample windows to classify speech vs silence,
    /// then concatenates speech segments with small gaps between them.
    /// Pipeline: HPF → NoiseGate → AGC → Silero VAD trim → Whisper
    /// </summary>
    public class SileroVadService : IDisposable
    {
        private const int SAMPLE_RATE = 16000;
        private const int WINDOW_SIZE = 512;
        private const int CONTEXT_SIZE = 64;
        private const int STATE_DIM = 128;
        private const int WAV_HEADER_SIZE = 44;

        private readonly string modelPath;
        private InferenceSession? session;
        private readonly object sessionLock = new();
        private volatile bool isDisposed;

        public SileroVadService(string modelPath)
        {
            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"Silero VAD model not found: {modelPath}");

            this.modelPath = modelPath;
        }

        private InferenceSession EnsureSession()
        {
            if (session != null)
                return session;

            lock (sessionLock)
            {
                if (session != null)
                    return session;

                var options = new SessionOptions();
                options.InterOpNumThreads = 1;
                options.IntraOpNumThreads = 1;
                session = new InferenceSession(modelPath, options);
                ErrorLogger.LogMessage("Silero VAD: ONNX session loaded");
                return session;
            }
        }

        /// <summary>
        /// Detects speech segments and returns audio with silence trimmed.
        /// </summary>
        /// <param name="wavData">Raw WAV file bytes (16kHz mono 16-bit)</param>
        /// <param name="threshold">Speech probability threshold (0-1). Lower = more permissive.</param>
        /// <param name="speechPadMs">Padding around speech segments in ms</param>
        /// <param name="minSilenceMs">Minimum silence duration to trigger a split</param>
        /// <returns>WAV bytes with only speech segments, or original if all silence</returns>
        public byte[] ProcessAudio(byte[] wavData, float threshold, float speechPadMs = 200f, float minSilenceMs = 500f)
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(SileroVadService));

            var samples = ExtractSamples(wavData);
            if (samples.Length < WINDOW_SIZE)
                return wavData; // Too short for VAD

            var probabilities = RunInference(samples);
            var segments = DetectSpeechSegments(probabilities, threshold, speechPadMs, minSilenceMs, samples.Length);

            if (segments.Count == 0)
                return wavData; // All silence — return original to avoid stripping everything

            var trimmedSamples = ConcatenateSpeechSegments(samples, segments);
            return EncodeWav(trimmedSamples);
        }

        internal float[] ExtractSamples(byte[] wavData)
        {
            // Skip WAV header (44 bytes) and convert 16-bit PCM to float
            int dataStart = WAV_HEADER_SIZE;
            if (wavData.Length <= dataStart)
                return Array.Empty<float>();

            int sampleCount = (wavData.Length - dataStart) / 2; // 16-bit = 2 bytes per sample
            var samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                int offset = dataStart + i * 2;
                if (offset + 1 >= wavData.Length)
                    break;

                short pcm = (short)(wavData[offset] | (wavData[offset + 1] << 8));
                samples[i] = pcm / 32768f;
            }

            return samples;
        }

        internal float[] RunInference(float[] samples)
        {
            var inferenceSession = EnsureSession();
            int totalWindows = (samples.Length - CONTEXT_SIZE) / WINDOW_SIZE;
            if (totalWindows <= 0)
                return Array.Empty<float>();

            var probabilities = new float[totalWindows];

            // Initialize state tensor [2, 1, 128] — zeros
            var state = new DenseTensor<float>(new[] { 2, 1, STATE_DIM });
            var sr = new DenseTensor<long>(new[] { 1 });
            sr[0] = SAMPLE_RATE;

            // Context buffer for overlapping windows
            var context = new float[CONTEXT_SIZE];

            for (int w = 0; w < totalWindows; w++)
            {
                int windowStart = w * WINDOW_SIZE;

                // Build input: [1, WINDOW_SIZE + CONTEXT_SIZE] = [1, 576]
                var input = new DenseTensor<float>(new[] { 1, WINDOW_SIZE + CONTEXT_SIZE });

                // Copy context (first 64 samples)
                for (int i = 0; i < CONTEXT_SIZE; i++)
                    input[0, i] = context[i];

                // Copy window samples (next 512 samples)
                for (int i = 0; i < WINDOW_SIZE; i++)
                {
                    int sampleIdx = windowStart + i;
                    input[0, CONTEXT_SIZE + i] = sampleIdx < samples.Length ? samples[sampleIdx] : 0f;
                }

                // Update context for next iteration (last 64 samples of current window)
                int contextStart = windowStart + WINDOW_SIZE - CONTEXT_SIZE;
                for (int i = 0; i < CONTEXT_SIZE; i++)
                {
                    int sampleIdx = contextStart + i;
                    context[i] = sampleIdx < samples.Length ? samples[sampleIdx] : 0f;
                }

                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input", input),
                    NamedOnnxValue.CreateFromTensor("state", state),
                    NamedOnnxValue.CreateFromTensor("sr", sr)
                };

                using var results = inferenceSession.Run(inputs);

                // Extract speech probability
                var outputTensor = results.First(r => r.Name == "output").AsTensor<float>();
                probabilities[w] = outputTensor.GetValue(0);

                // Update state for next window
                var newState = results.First(r => r.Name == "stateN").AsTensor<float>();
                state = new DenseTensor<float>(new[] { 2, 1, STATE_DIM });
                for (int d = 0; d < 2; d++)
                    for (int s = 0; s < STATE_DIM; s++)
                        state[d, 0, s] = newState[d, 0, s];
            }

            return probabilities;
        }

        internal List<(int Start, int End)> DetectSpeechSegments(
            float[] probabilities, float threshold, float speechPadMs, float minSilenceMs, int totalSamples)
        {
            int padSamples = (int)(speechPadMs / 1000f * SAMPLE_RATE);
            int minSilenceSamples = (int)(minSilenceMs / 1000f * SAMPLE_RATE);

            // Convert probabilities to sample-level speech flags
            var rawSegments = new List<(int Start, int End)>();
            int? segStart = null;

            for (int w = 0; w < probabilities.Length; w++)
            {
                int samplePos = w * WINDOW_SIZE;

                if (probabilities[w] >= threshold)
                {
                    if (segStart == null)
                        segStart = samplePos;
                }
                else
                {
                    if (segStart != null)
                    {
                        rawSegments.Add((segStart.Value, samplePos));
                        segStart = null;
                    }
                }
            }

            // Close any open segment
            if (segStart != null)
                rawSegments.Add((segStart.Value, totalSamples));

            if (rawSegments.Count == 0)
                return rawSegments;

            // Merge segments separated by less than minSilenceMs
            var merged = new List<(int Start, int End)> { rawSegments[0] };
            for (int i = 1; i < rawSegments.Count; i++)
            {
                var prev = merged[^1];
                var curr = rawSegments[i];

                if (curr.Start - prev.End < minSilenceSamples)
                {
                    merged[^1] = (prev.Start, curr.End);
                }
                else
                {
                    merged.Add(curr);
                }
            }

            // Add padding and clamp to bounds
            for (int i = 0; i < merged.Count; i++)
            {
                var seg = merged[i];
                merged[i] = (
                    Math.Max(0, seg.Start - padSamples),
                    Math.Min(totalSamples, seg.End + padSamples)
                );
            }

            return merged;
        }

        private float[] ConcatenateSpeechSegments(float[] samples, List<(int Start, int End)> segments)
        {
            // 50ms silence gap between segments
            int gapSamples = SAMPLE_RATE * 50 / 1000; // 800 samples at 16kHz

            int totalLength = segments.Sum(s => s.End - s.Start) + (segments.Count - 1) * gapSamples;
            var result = new float[totalLength];
            int pos = 0;

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                int segLen = seg.End - seg.Start;

                Array.Copy(samples, seg.Start, result, pos, segLen);
                pos += segLen;

                // Add silence gap between segments (not after last)
                if (i < segments.Count - 1)
                {
                    // Gap is already zero-initialized
                    pos += gapSamples;
                }
            }

            return result;
        }

        internal byte[] EncodeWav(float[] samples)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            int dataSize = samples.Length * 2; // 16-bit PCM
            int fileSize = WAV_HEADER_SIZE + dataSize - 8;

            // WAV header
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(fileSize);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });

            // fmt chunk
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);           // chunk size
            writer.Write((short)1);     // PCM format
            writer.Write((short)1);     // mono
            writer.Write(SAMPLE_RATE);  // sample rate
            writer.Write(SAMPLE_RATE * 2); // byte rate (16-bit mono)
            writer.Write((short)2);     // block align
            writer.Write((short)16);    // bits per sample

            // data chunk
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataSize);

            // Write samples as 16-bit PCM
            for (int i = 0; i < samples.Length; i++)
            {
                float clamped = Math.Clamp(samples[i], -1f, 1f);
                short pcm = (short)(clamped * 32767f);
                writer.Write(pcm);
            }

            writer.Flush();
            return ms.ToArray();
        }

        /// <summary>
        /// Resolves the VAD model path by searching standard locations.
        /// </summary>
        public static string? FindModelPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // Check bundled location (output/whisper/)
            var bundledPath = Path.Combine(baseDir, "whisper", "silero_vad_v5.onnx");
            if (File.Exists(bundledPath))
                return bundledPath;

            // Check base directory
            var basePath = Path.Combine(baseDir, "silero_vad_v5.onnx");
            if (File.Exists(basePath))
                return basePath;

            // Check LocalAppData
            var localDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoiceLite", "whisper", "silero_vad_v5.onnx");
            if (File.Exists(localDataPath))
                return localDataPath;

            ErrorLogger.LogWarning("Silero VAD model not found. Checked:\n" +
                                   $"1. {bundledPath}\n" +
                                   $"2. {basePath}\n" +
                                   $"3. {localDataPath}");
            return null;
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;

            lock (sessionLock)
            {
                session?.Dispose();
                session = null;
            }
        }
    }
}

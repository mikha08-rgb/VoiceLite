using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using NAudio.Wave;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    public class ModelBenchmarkService
    {
        private readonly string tempPath;
        private readonly string whisperPath;
        private Stopwatch stopwatch;

        public class BenchmarkResult
        {
            public string ModelName { get; set; } = string.Empty;
            public double TranscriptionTime { get; set; } // in seconds
            public double AudioDuration { get; set; } // in seconds
            public double ProcessingRatio { get; set; } // transcription time / audio duration
            public long PeakMemoryUsage { get; set; } // in bytes
            public string TranscribedText { get; set; } = string.Empty;
            public bool Success { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
        }

        public ModelBenchmarkService()
        {
            tempPath = Path.Combine(Path.GetTempPath(), "VoiceLite");
            if (!Directory.Exists(tempPath))
                Directory.CreateDirectory(tempPath);

            whisperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper");
            stopwatch = new Stopwatch();
        }

        public async Task<BenchmarkResult> BenchmarkModelAsync(WhisperModelInfo model, string? audioFilePath = null)
        {
            var result = new BenchmarkResult
            {
                ModelName = model.DisplayName,
                Success = false
            };

            try
            {
                // Use provided audio or create test audio
                string testAudioPath;
                if (!string.IsNullOrEmpty(audioFilePath) && File.Exists(audioFilePath))
                {
                    testAudioPath = audioFilePath;
                }
                else
                {
                    testAudioPath = await CreateTestAudioAsync();
                }

                // Get audio duration
                result.AudioDuration = GetAudioDuration(testAudioPath);

                // Prepare whisper command
                var whisperExePath = Path.Combine(whisperPath, "whisper.exe");
                var modelPath = Path.Combine(whisperPath, model.FileName);

                if (!File.Exists(whisperExePath))
                {
                    result.ErrorMessage = "Whisper executable not found";
                    return result;
                }

                if (!File.Exists(modelPath))
                {
                    result.ErrorMessage = $"Model file not found: {model.FileName}";
                    return result;
                }

                // Start benchmark
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = whisperExePath,
                    Arguments = $"-m \"{modelPath}\" -f \"{testAudioPath}\" --no-timestamps --language en -t {Environment.ProcessorCount}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = whisperPath
                };

                stopwatch.Restart();
                long peakMemory = 0;

                using (var process = new Process { StartInfo = processStartInfo })
                {
                    process.Start();

                    // Monitor memory usage
                    var memoryTimer = new Timer(100); // Check every 100ms
                    memoryTimer.Elapsed += (s, e) =>
                    {
                        try
                        {
                            if (!process.HasExited)
                            {
                                process.Refresh();
                                var currentMemory = process.WorkingSet64;
                                if (currentMemory > peakMemory)
                                    peakMemory = currentMemory;
                            }
                        }
                        catch { }
                    };
                    memoryTimer.Start();

                    // Read output
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();

                    // Wait for process with timeout
                    bool completed = await Task.Run(() => process.WaitForExit((int)(result.AudioDuration * 1000 * 10))); // 10x audio duration as timeout

                    memoryTimer.Stop();
                    stopwatch.Stop();

                    if (!completed)
                    {
                        process.Kill();
                        result.ErrorMessage = "Transcription timeout";
                        return result;
                    }

                    if (process.ExitCode == 0)
                    {
                        result.Success = true;
                        result.TranscribedText = CleanTranscriptionOutput(output);
                        result.TranscriptionTime = stopwatch.Elapsed.TotalSeconds;
                        result.ProcessingRatio = result.TranscriptionTime / result.AudioDuration;
                        result.PeakMemoryUsage = peakMemory;
                    }
                    else
                    {
                        result.ErrorMessage = $"Whisper error: {error}";
                    }
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Benchmark failed: {ex.Message}";
            }

            return result;
        }

        public async Task<BenchmarkResult[]> CompareModelsAsync(WhisperModelInfo[] models, string? audioFilePath = null)
        {
            var results = new BenchmarkResult[models.Length];

            for (int i = 0; i < models.Length; i++)
            {
                if (models[i].IsInstalled)
                {
                    results[i] = await BenchmarkModelAsync(models[i], audioFilePath);
                }
                else
                {
                    results[i] = new BenchmarkResult
                    {
                        ModelName = models[i].DisplayName,
                        Success = false,
                        ErrorMessage = "Model not installed"
                    };
                }
            }

            return results;
        }

        private async Task<string> CreateTestAudioAsync()
        {
            // Create a 5-second test audio file with generated speech or silence
            var testAudioPath = Path.Combine(tempPath, $"test_audio_{Guid.NewGuid()}.wav");

            await Task.Run(() =>
            {
                var format = new WaveFormat(16000, 16, 1); // 16kHz, 16-bit, mono
                using (var writer = new WaveFileWriter(testAudioPath, format))
                {
                    // Generate 5 seconds of test audio (could be silence or test tone)
                    var sampleRate = format.SampleRate;
                    var seconds = 5;
                    var samples = sampleRate * seconds;

                    // Generate a simple test pattern (alternating tones)
                    for (int i = 0; i < samples; i++)
                    {
                        // Create a simple sine wave pattern
                        double frequency = (i / (double)sampleRate) % 2 < 1 ? 440 : 880; // Alternate between A4 and A5
                        double amplitude = 0.1; // Low volume
                        double sample = amplitude * Math.Sin(2 * Math.PI * frequency * i / sampleRate);
                        writer.WriteSample((float)sample);
                    }
                }
            });

            return testAudioPath;
        }

        private double GetAudioDuration(string audioPath)
        {
            try
            {
                using (var reader = new AudioFileReader(audioPath))
                {
                    return reader.TotalTime.TotalSeconds;
                }
            }
            catch
            {
                return 5.0; // Default to 5 seconds if unable to read
            }
        }

        private string CleanTranscriptionOutput(string output)
        {
            // Extract actual transcription from whisper output
            // Whisper output format includes timestamps and metadata we need to strip
            var lines = output.Split('\n');
            var transcription = "";

            bool inTranscription = false;
            foreach (var line in lines)
            {
                if (line.Contains("[") && line.Contains("]") && line.Contains("-->"))
                {
                    // This is a timestamp line, skip it
                    continue;
                }

                if (line.Trim().StartsWith("Detected language:") ||
                    line.Trim().StartsWith("[") ||
                    string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                // Check if we've reached the actual transcription
                if (!inTranscription && !string.IsNullOrWhiteSpace(line) && !line.StartsWith(" "))
                {
                    inTranscription = true;
                }

                if (inTranscription)
                {
                    transcription += line.Trim() + " ";
                }
            }

            return transcription.Trim();
        }

        public string FormatBenchmarkResults(BenchmarkResult result)
        {
            if (!result.Success)
            {
                return $"❌ {result.ModelName}: {result.ErrorMessage}";
            }

            return $"✅ {result.ModelName}:\n" +
                   $"   ⏱️ Time: {result.TranscriptionTime:F2}s ({result.ProcessingRatio:F1}x realtime)\n" +
                   $"   💾 Peak RAM: {FormatBytes(result.PeakMemoryUsage)}\n" +
                   $"   📝 Text: {result.TranscribedText?.Substring(0, Math.Min(100, result.TranscribedText?.Length ?? 0))}...";
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.#} {sizes[order]}";
        }

        public void Cleanup()
        {
            // Clean up temporary files
            try
            {
                if (Directory.Exists(tempPath))
                {
                    foreach (var file in Directory.GetFiles(tempPath, "test_audio_*.wav"))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
            catch { }
        }
    }
}
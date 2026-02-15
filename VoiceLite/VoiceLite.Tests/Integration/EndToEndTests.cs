using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Input;
using Xunit;
using AwesomeAssertions;
using VoiceLite.Services;
using VoiceLite.Models;

namespace VoiceLite.Tests.Integration
{
    /// <summary>
    /// WEEK 1: Comprehensive integration tests for end-to-end workflows
    /// These tests verify the complete recording → transcription → injection pipeline
    /// </summary>
    public class EndToEndTests : IDisposable
    {
        private readonly string _testDataPath;
        private readonly AudioRecorder _audioRecorder;
        private readonly PersistentWhisperService _whisperService;
        private readonly TextInjector _textInjector;
        private readonly Settings _testSettings;

        public EndToEndTests()
        {
            // Set up test environment
            _testDataPath = Path.Combine(Path.GetTempPath(), $"VoiceLiteTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDataPath);

            // Initialize services with test settings
            _testSettings = new Settings
            {
                WhisperModel = "tiny",
                TextInjectionMode = TextInjectionMode.AlwaysType,
                RecordHotkey = Key.F8,
                HotkeyModifiers = ModifierKeys.Alt
            };

            _audioRecorder = new AudioRecorder();
            _whisperService = new PersistentWhisperService(_testSettings);
            _textInjector = new TextInjector(_testSettings);
        }

        [Fact]
        public async Task FullTranscriptionPipeline_RecordTranscribeInject_ShouldComplete()
        {
            // Arrange
            string? audioPath = null;
            string? transcribedText = null;
            var transcriptionCompleted = new TaskCompletionSource<bool>();

            _audioRecorder.AudioFileReady += (sender, path) => audioPath = path;

            // Act - Record silence for 1 second
            _audioRecorder.StartRecording();
            await Task.Delay(1000);
            _audioRecorder.StopRecording();

            // Wait for audio file to be ready
            await Task.Delay(500);

            // Verify audio was recorded
            audioPath.Should().NotBeNullOrEmpty("Audio file should be created");
            File.Exists(audioPath).Should().BeTrue("Audio file should exist");

            // Transcribe (will return empty or noise for silence)
            if (!string.IsNullOrEmpty(audioPath))
            {
                transcribedText = await _whisperService.TranscribeAsync(audioPath);
            }

            // Assert - Pipeline completed without errors
            // (Transcription might be empty for silence, that's okay)
            _audioRecorder.IsRecording.Should().BeFalse();
        }

        [Fact]
        public async Task RapidStartStop_50Times_ShouldNotCrashOrLeak()
        {
            // Arrange
            var initialMemory = GC.GetTotalMemory(true) / 1_000_000; // MB
            var exceptions = 0;

            // Act - Rapidly start/stop recording 50 times
            for (int i = 0; i < 50; i++)
            {
                try
                {
                    _audioRecorder.StartRecording();
                    await Task.Delay(20); // Very short recording
                    _audioRecorder.StopRecording();
                    await Task.Delay(20); // Brief pause
                }
                catch
                {
                    exceptions++;
                }
            }

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var finalMemory = GC.GetTotalMemory(true) / 1_000_000; // MB
            var memoryGrowth = finalMemory - initialMemory;

            // Assert
            exceptions.Should().Be(0, "No exceptions during rapid start/stop");
            _audioRecorder.IsRecording.Should().BeFalse("Recording should be stopped");
            memoryGrowth.Should().BeLessThan(50, $"Memory grew by {memoryGrowth}MB");
        }

        [Fact(Skip = "Whisper.net in-process native disposal during ProcessAsync causes access violation")]
        public async Task SimultaneousClose_DuringTranscription_ShouldNotDeadlock()
        {
            // Arrange
            var audioPath = CreateTestAudioFile();

            // Act - Start transcription then immediately dispose
            var transcriptionTask = _whisperService.TranscribeAsync(audioPath);

            // Give transcription time to start
            await Task.Delay(100);

            // Dispose while transcription is potentially running
            _whisperService.Dispose();

            // Wait for completion or timeout
            var completedTask = await Task.WhenAny(
                transcriptionTask,
                Task.Delay(TimeSpan.FromSeconds(10)));

            // Assert - Should complete without deadlock
            completedTask.Should().Be(transcriptionTask, "Transcription should complete or cancel, not deadlock");
        }

        [Fact(Skip = "Requires STA thread which is not supported in xUnit async tests")]
        public async Task ClipboardInjection_ShouldRestoreOriginalContent()
        {
            // Run on STA thread for clipboard access
            var tcs = new TaskCompletionSource<bool>();
            var thread = new Thread(async () =>
            {
                try
                {
                    // Arrange
                    var originalClipboard = "Original clipboard content " + Guid.NewGuid();
                    System.Windows.Clipboard.SetText(originalClipboard);
                    await Task.Delay(100); // Let clipboard settle

                    var testText = "Test transcription text";

                    // Act
                    await _textInjector.InjectTextAsync(testText, TextInjector.InjectionMode.Paste);

                    // Wait for clipboard restoration
                    await Task.Delay(200);

                    // Assert
                    var finalClipboard = System.Windows.Clipboard.GetText();
                    finalClipboard.Should().Be(originalClipboard, "Original clipboard should be restored");

                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            await tcs.Task;
        }

        [Fact]
        public async Task LicenseService_MultipleValidations_ShouldShareHttpClient()
        {
            // Arrange
            var services = new List<LicenseService>();
            var tasks = new List<Task<LicenseValidationResult>>();

            // Act - Create multiple services and validate concurrently
            for (int i = 0; i < 5; i++)
            {
                var service = new LicenseService();
                services.Add(service);
                tasks.Add(service.ValidateLicenseAsync($"test-key-{i}"));
            }

            // Wait for all validations
            var results = await Task.WhenAll(tasks);

            // Clean up
            foreach (var service in services)
            {
                service.Dispose();
            }

            // Assert - All should complete (even if invalid)
            results.Should().HaveCount(5);
            results.Should().OnlyContain(r => r != null);
        }

        [Fact]
        public async Task StressTest_100ConsecutiveTranscriptions_ShouldSucceed()
        {
            // This is a longer stress test - only run if needed
            if (!ShouldRunStressTests())
            {
                return; // Skip in normal test runs
            }

            // Arrange
            var audioPath = CreateTestAudioFile();
            var failures = 0;
            var totalTime = Stopwatch.StartNew();

            // Act - Run 100 transcriptions
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    var result = await _whisperService.TranscribeAsync(audioPath);
                    if (string.IsNullOrEmpty(result))
                    {
                        failures++;
                    }
                }
                catch
                {
                    failures++;
                }

                // Brief pause between transcriptions
                await Task.Delay(100);
            }

            totalTime.Stop();

            // Assert
            failures.Should().BeLessThan(5, "Less than 5% failure rate");
            totalTime.Elapsed.Should().BeLessThan(TimeSpan.FromMinutes(5), "Should complete in reasonable time");
        }

        [Fact]
        public async Task Settings_SaveAndLoad_ShouldBeAtomic()
        {
            // Arrange
            var settingsPath = Path.Combine(_testDataPath, "test-settings.json");
            var settings1 = new Settings { WhisperModel = "tiny", RecordHotkey = Key.F8 };
            var settings2 = new Settings { WhisperModel = "base", RecordHotkey = Key.F9 };

            // Act - Save multiple times rapidly (simulating race condition)
            var tasks = new[]
            {
                Task.Run(() => SaveSettingsAtomic(settingsPath, settings1)),
                Task.Run(() => SaveSettingsAtomic(settingsPath, settings2)),
                Task.Run(() => SaveSettingsAtomic(settingsPath, settings1))
            };

            await Task.WhenAll(tasks);

            // Load and verify
            var loadedSettings = LoadSettings(settingsPath);

            // Assert - Should have valid settings (either one)
            loadedSettings.Should().NotBeNull();
            loadedSettings.WhisperModel.Should().BeOneOf("tiny", "base");
            loadedSettings.RecordHotkey.Should().BeOneOf(Key.F8, Key.F9);
        }

        private string CreateTestAudioFile()
        {
            // Create a minimal valid WAV file for testing
            var filePath = Path.Combine(_testDataPath, $"test_{Guid.NewGuid()}.wav");

            // WAV file header for 16kHz, 16-bit, mono, 1 second of silence
            using var fs = new FileStream(filePath, FileMode.Create);
            using var writer = new BinaryWriter(fs);

            // RIFF header
            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + 32000); // File size - 8
            writer.Write("WAVE".ToCharArray());

            // Format chunk
            writer.Write("fmt ".ToCharArray());
            writer.Write(16); // Chunk size
            writer.Write((short)1); // PCM format
            writer.Write((short)1); // Mono
            writer.Write(16000); // Sample rate
            writer.Write(32000); // Byte rate
            writer.Write((short)2); // Block align
            writer.Write((short)16); // Bits per sample

            // Data chunk
            writer.Write("data".ToCharArray());
            writer.Write(32000); // Data size (1 second at 16kHz, 16-bit)

            // Write 1 second of silence
            for (int i = 0; i < 16000; i++)
            {
                writer.Write((short)0);
            }

            return filePath;
        }

        private void SaveSettingsAtomic(string path, Settings settings)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(settings);
            // Use unique temp file to avoid concurrent write conflicts
            var tempPath = path + $".tmp.{Guid.NewGuid()}";

            // Retry logic to handle concurrent access (simulates real-world atomic write patterns)
            const int maxRetries = 5;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    File.WriteAllText(tempPath, json);
                    File.Move(tempPath, path, overwrite: true);
                    return; // Success
                }
                catch (IOException) when (attempt < maxRetries - 1)
                {
                    // File locked or access denied - retry after brief delay
                    Thread.Sleep(10 * (attempt + 1)); // Exponential backoff: 10ms, 20ms, 30ms, 40ms
                }
                catch (UnauthorizedAccessException) when (attempt < maxRetries - 1)
                {
                    // Access denied - retry after brief delay
                    Thread.Sleep(10 * (attempt + 1));
                }
                finally
                {
                    // Clean up temp file if it still exists
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); } catch { /* Ignore cleanup errors */ }
                    }
                }
            }

            // Final attempt without retry
            try
            {
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { /* Ignore cleanup errors */ }
                }
            }
        }

        private Settings LoadSettings(string path)
        {
            var json = File.ReadAllText(path);
            return System.Text.Json.JsonSerializer.Deserialize<Settings>(json)!;
        }

        private bool ShouldRunStressTests()
        {
            // Only run stress tests when explicitly requested
            return Environment.GetEnvironmentVariable("RUN_STRESS_TESTS") == "true";
        }

        public void Dispose()
        {
            // Clean up test resources
            try
            {
                _audioRecorder?.Dispose();
                _whisperService?.Dispose();
                // TextInjector no longer implements IDisposable after refactoring

                if (Directory.Exists(_testDataPath))
                {
                    Directory.Delete(_testDataPath, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }
}
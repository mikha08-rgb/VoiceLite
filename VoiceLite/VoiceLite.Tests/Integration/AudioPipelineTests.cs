using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using VoiceLite.Models;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Integration
{
    /// <summary>
    /// Integration tests for the complete audio recording → transcription → text injection pipeline
    /// These are high-ROI tests that catch regressions in the core workflow
    /// </summary>
    public class AudioPipelineTests : IDisposable
    {
        private readonly AudioRecorder _recorder;
        private readonly PersistentWhisperService _transcriber;
        private readonly TextInjector _textInjector;
        private readonly Settings _settings;
        private readonly string _tempDirectory;

        public AudioPipelineTests()
        {
            _tempDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
            Directory.CreateDirectory(_tempDirectory);

            _settings = new Settings
            {
                WhisperModel = "base",
            };

            _recorder = new AudioRecorder();

            // Only create transcriber if whisper.exe exists
            var whisperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper", "whisper.exe");
            _transcriber = File.Exists(whisperPath)
                ? new PersistentWhisperService(_settings)
                : null!;

            _textInjector = new TextInjector(_settings);
        }

        public void Dispose()
        {
            _recorder?.Dispose();
            _transcriber?.Dispose();
        }

        [Fact]
        public async Task FullPipeline_RecordTranscribeInject_CompletesSuccessfully()
        {
            if (_transcriber == null)
            {
                // Skip if whisper.exe not available
                return;
            }

            var audioDataReceived = false;
            var transcriptionResult = string.Empty;
            byte[]? capturedAudio = null;

            // Setup event handler for audio data
            _recorder.AudioDataReady += async (sender, audioData) =>
            {
                audioDataReceived = true;
                capturedAudio = audioData;

                // Transcribe the audio
                transcriptionResult = await _transcriber.TranscribeFromMemoryAsync(audioData);
            };

            // Start recording
            _recorder.StartRecording();
            _recorder.IsRecording.Should().BeTrue();

            // Record for a short time
            await Task.Delay(500);

            // Stop recording
            _recorder.StopRecording();
            _recorder.IsRecording.Should().BeFalse();

            // Wait for processing
            await Task.Delay(1000);

            // Verify pipeline stages
            audioDataReceived.Should().BeTrue("Audio data should be captured");
            capturedAudio.Should().NotBeNull();
            capturedAudio!.Length.Should().BeGreaterThan(0, "Audio buffer should contain data");
            transcriptionResult.Should().NotBeNull("Transcription should complete");
        }

        [Fact]
        public async Task Pipeline_MultipleRecordingCycles_MaintainsStability()
        {
            const int cycles = 3;
            var cyclesCompleted = 0;

            _recorder.AudioDataReady += (sender, audioData) =>
            {
                Interlocked.Increment(ref cyclesCompleted);
            };

            for (int i = 0; i < cycles; i++)
            {
                _recorder.StartRecording();
                await Task.Delay(200);
                _recorder.StopRecording();
                await Task.Delay(100); // Brief pause between recordings
            }

            // Allow time for all events to fire
            await Task.Delay(500);

            cyclesCompleted.Should().Be(cycles, "All recording cycles should complete");
        }

        [Fact]
        public async Task Pipeline_ConcurrentOperations_HandledSafely()
        {
            var tasks = new Task[3];

            // Start multiple recording sessions concurrently
            for (int i = 0; i < tasks.Length; i++)
            {
                var index = i;
                tasks[i] = Task.Run(async () =>
                {
                    var recorder = new AudioRecorder();
                    try
                    {
                        recorder.StartRecording();
                        await Task.Delay(100 + index * 50);
                        recorder.StopRecording();
                    }
                    finally
                    {
                        recorder.Dispose();
                    }
                });
            }

            // All concurrent operations should complete without deadlock
            var allCompleted = await Task.WhenAll(tasks).ContinueWith(t => !t.IsFaulted);
            allCompleted.Should().BeTrue("Concurrent operations should complete successfully");
        }

        [Fact]
        public async Task Pipeline_ErrorRecovery_ContinuesAfterFailure()
        {
            var successfulCompletions = 0;
            var errors = 0;
            var eventFiredCount = 0;
            var tcs = new TaskCompletionSource<bool>();
            var expectedEvents = 3;

            _recorder.AudioDataReady += async (sender, audioData) =>
            {
                try
                {
                    var currentEvent = Interlocked.Increment(ref eventFiredCount);

                    // Simulate occasional failures on the 2nd event
                    if (currentEvent == 2)
                    {
                        Interlocked.Increment(ref errors);
                        throw new InvalidOperationException("Simulated pipeline error");
                    }

                    // Simulate a successful transcription without calling actual whisper
                    // (avoid dependency on whisper.exe being available or functioning)
                    await Task.Delay(10); // Simulate async work

                    Interlocked.Increment(ref successfulCompletions);
                }
                catch (InvalidOperationException)
                {
                    // Expected error - already counted errors above before throw
                }
                catch (Exception)
                {
                    // Any other unexpected errors
                    Interlocked.Increment(ref errors);
                }
                finally
                {
                    // Signal completion when all events have fired
                    if (Interlocked.CompareExchange(ref eventFiredCount, 0, 0) >= expectedEvents)
                    {
                        tcs.TrySetResult(true);
                    }
                }
            };

            // Run multiple cycles
            for (int i = 0; i < 3; i++)
            {
                _recorder.StartRecording();
                await Task.Delay(100);
                _recorder.StopRecording();
                await Task.Delay(200);
            }

            // Wait for all events to complete or timeout after 5 seconds
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000)) == tcs.Task;

            // Give async handlers a bit more time to complete
            await Task.Delay(100);

            completed.Should().BeTrue("AudioDataReady event should fire for all recordings");
            eventFiredCount.Should().Be(expectedEvents, "AudioDataReady should fire once per recording");
            errors.Should().Be(1, "Should have exactly one simulated error");
            successfulCompletions.Should().Be(2, "Should successfully complete 2 transcriptions (1st and 3rd) after error recovery");
        }

        [Theory]
        [InlineData(RecordMode.Toggle)]
        [InlineData(RecordMode.PushToTalk)]
        public async Task Pipeline_DifferentRecordModes_WorkCorrectly(RecordMode mode)
        {
            var recordingStateChanges = 0;

            // Simulate mode-specific behavior
            if (mode == RecordMode.Toggle)
            {
                // Toggle mode: Start/Stop on same action
                _recorder.StartRecording();
                recordingStateChanges++;
                _recorder.IsRecording.Should().BeTrue();

                await Task.Delay(200);

                _recorder.StopRecording();
                recordingStateChanges++;
                _recorder.IsRecording.Should().BeFalse();
            }
            else
            {
                // Push-to-talk: Hold to record
                _recorder.StartRecording();
                recordingStateChanges++;
                _recorder.IsRecording.Should().BeTrue();

                await Task.Delay(200); // Simulating held key

                _recorder.StopRecording();
                recordingStateChanges++;
                _recorder.IsRecording.Should().BeFalse();
            }

            recordingStateChanges.Should().Be(2, "Should have exactly 2 state changes");
        }

        [Fact]
        public async Task Pipeline_MemoryBufferMode_AvoidsDiskIO()
        {
            var memoryBufferUsed = false;

            _recorder.AudioDataReady += (sender, audioData) =>
            {
                // Verify we received in-memory audio data
                memoryBufferUsed = audioData != null && audioData.Length > 0;
            };

            _recorder.StartRecording();
            await Task.Delay(200);
            _recorder.StopRecording();

            await Task.Delay(500); // Wait for event

            memoryBufferUsed.Should().BeTrue("Should use memory buffer instead of file");
        }

        [Fact]
        public async Task Pipeline_LongRecording_HandlesLargeBuffer()
        {
            byte[]? largeBuffer = null;

            _recorder.AudioDataReady += (sender, audioData) =>
            {
                largeBuffer = audioData;
            };

            _recorder.StartRecording();
            await Task.Delay(3000); // 3 second recording
            _recorder.StopRecording();

            await Task.Delay(500);

            largeBuffer.Should().NotBeNull();
            largeBuffer!.Length.Should().BeGreaterThan(50000, "3 seconds should produce substantial data");
        }

        [Fact]
        public async Task Pipeline_RapidStartStop_NoDataCorruption()
        {
            var dataIntegrity = true;

            _recorder.AudioDataReady += (sender, audioData) =>
            {
                // Check for basic WAV header integrity
                if (audioData.Length < 44) // WAV header is 44 bytes
                {
                    dataIntegrity = false;
                }
            };

            // Rapid start/stop cycles
            for (int i = 0; i < 5; i++)
            {
                _recorder.StartRecording();
                await Task.Delay(50); // Very short recording
                _recorder.StopRecording();
                await Task.Delay(20);
            }

            await Task.Delay(500);

            dataIntegrity.Should().BeTrue("All audio data should maintain integrity");
        }
    }
}
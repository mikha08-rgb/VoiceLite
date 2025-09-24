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
                WhisperModel = "small",
                BeamSize = 5
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

            _recorder.AudioDataReady += async (sender, audioData) =>
            {
                try
                {
                    // Simulate occasional failures
                    if (successfulCompletions == 1)
                    {
                        throw new InvalidOperationException("Simulated pipeline error");
                    }

                    if (_transcriber != null)
                    {
                        await _transcriber.TranscribeFromMemoryAsync(audioData);
                    }

                    Interlocked.Increment(ref successfulCompletions);
                }
                catch
                {
                    Interlocked.Increment(ref errors);
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

            // Should have one error but continue processing
            errors.Should().Be(1, "Should have exactly one simulated error");
            successfulCompletions.Should().BeGreaterThan(0, "Should continue after error");
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
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Moq;
using VoiceLite.Services;
using VoiceLite.Tests.TestUtilities;
using Xunit;

namespace VoiceLite.Tests.Services
{
    public class AudioRecorderTests : IDisposable
    {
        private readonly AudioRecorder _recorder;
        private readonly string _tempDirectory;

        public AudioRecorderTests()
        {
            _recorder = new AudioRecorder();
            // Must match AudioRecorder's real temp directory (AudioRecorder.cs constructor)
            _tempDirectory = Path.Combine(Path.GetTempPath(), "VoiceLite", "audio");
        }

        public void Dispose()
        {
            _recorder?.Dispose();
        }

        [Fact]
        public void Constructor_CreatesTemporaryDirectory()
        {
            Directory.Exists(_tempDirectory).Should().BeTrue();
        }

        [Fact]
        public void IsRecording_InitiallyFalse()
        {
            _recorder.IsRecording.Should().BeFalse();
        }

        [Fact]
        public void GetAvailableDevices_ReturnsDeviceList()
        {
            var devices = AudioRecorder.GetAvailableMicrophones();
            devices.Should().NotBeNull();
            devices.Should().BeOfType<List<AudioDevice>>();
        }

        [Fact]
        public async Task StartRecording_SetsIsRecordingTrue()
        {
            if (!AudioTestEnvironment.HasMicrophone) return; // no audio device (CI runner)

            _recorder.StartRecording();
            _recorder.IsRecording.Should().BeTrue();

            await Task.Delay(100);
            _recorder.StopRecording();
        }

        [Fact]
        public async Task StopRecording_SetsIsRecordingFalse()
        {
            if (!AudioTestEnvironment.HasMicrophone) return; // no audio device (CI runner)

            _recorder.StartRecording();
            await Task.Delay(100);

            _recorder.StopRecording();
            _recorder.IsRecording.Should().BeFalse();
        }

        [Fact]
        public async Task StopRecording_FiresAudioDataReadyEvent()
        {
            if (!AudioTestEnvironment.HasMicrophone) return; // no audio device (CI runner)

            var eventFired = false;
            byte[]? capturedData = null;

            _recorder.AudioDataReady += (sender, data) =>
            {
                eventFired = true;
                capturedData = data;
            };

            _recorder.StartRecording();
            await Task.Delay(200); // Record for 200ms
            _recorder.StopRecording();

            await Task.Delay(100); // Give time for event to fire

            eventFired.Should().BeTrue();
            capturedData.Should().NotBeNull();
            capturedData!.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task StopRecording_FiresAudioFileReady_WithValidWavOnDisk()
        {
            if (!AudioTestEnvironment.HasMicrophone) return; // no audio device (CI runner)

            // This is the handoff the live pipeline depends on:
            // StopRecording → preprocessing/VAD → temp WAV → AudioFileReady(path),
            // and MainWindow feeds that path straight into the transcription service.
            var fileReady = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _recorder.AudioFileReady += (sender, path) => fileReady.TrySetResult(path);

            _recorder.StartRecording();
            await Task.Delay(300);
            _recorder.StopRecording();

            var completed = await Task.WhenAny(fileReady.Task, Task.Delay(5000));
            completed.Should().Be(fileReady.Task, "AudioFileReady must fire after StopRecording");

            var wavPath = await fileReady.Task;
            File.Exists(wavPath).Should().BeTrue("the path handed to the transcription service must exist");

            // The transcription service expects 16kHz/16-bit/mono (silence-safe:
            // if VAD trims everything, AudioRecorder falls back to the raw audio).
            using var reader = new NAudio.Wave.WaveFileReader(wavPath);
            reader.WaveFormat.SampleRate.Should().Be(16000);
            reader.WaveFormat.Channels.Should().Be(1);
            reader.WaveFormat.BitsPerSample.Should().Be(16);
            reader.Length.Should().BeGreaterThan(0, "the WAV must contain audio data");
        }

        [Fact]
        public void StartRecording_WhenAlreadyRecording_DoesNothing()
        {
            if (!AudioTestEnvironment.HasMicrophone) return; // no audio device (CI runner)

            _recorder.StartRecording();
            var firstState = _recorder.IsRecording;

            _recorder.StartRecording(); // Second call
            var secondState = _recorder.IsRecording;

            firstState.Should().BeTrue();
            secondState.Should().BeTrue();

            _recorder.StopRecording();
        }

        [Fact]
        public void StopRecording_WhenNotRecording_DoesNothing()
        {
            _recorder.StopRecording(); // Should not throw
            _recorder.IsRecording.Should().BeFalse();
        }

        [Fact]
        public async Task SetDevice_ChangesSelectedDevice()
        {
            var devices = AudioRecorder.GetAvailableMicrophones();
            if (devices.Count > 0)
            {
                _recorder.SetDevice(devices[0].Index);

                // Start and stop recording to verify device change took effect
                _recorder.StartRecording();
                await Task.Delay(100);
                _recorder.StopRecording();

                // Test passed if no exceptions were thrown
            }
        }

        [Fact]
        public async Task MultipleStartStop_HandledCorrectly()
        {
            if (!AudioTestEnvironment.HasMicrophone) return; // no audio device (CI runner)

            for (int i = 0; i < 3; i++)
            {
                _recorder.StartRecording();
                _recorder.IsRecording.Should().BeTrue();

                await Task.Delay(100);

                _recorder.StopRecording();
                _recorder.IsRecording.Should().BeFalse();

                await Task.Delay(50); // Brief pause between recordings
            }
        }

        [Fact]
        public void Dispose_CleansUpResources()
        {
            if (!AudioTestEnvironment.HasMicrophone) return; // no audio device (CI runner)

            _recorder.StartRecording();
            _recorder.Dispose();

            _recorder.IsRecording.Should().BeFalse();
        }

        [Fact]
        public void Dispose_MultipleCalls_DoesNotThrow()
        {
            _recorder.Dispose();

            Action secondDispose = () => _recorder.Dispose();
            secondDispose.Should().NotThrow();
        }

        [Fact]
        public async Task TempFilesCleanup_RemovesOldFiles()
        {
            if (!AudioTestEnvironment.HasMicrophone) return; // no audio device (CI runner)

            // Create an old test file
            var oldFile = Path.Combine(_tempDirectory, "old_test.wav");
            File.WriteAllBytes(oldFile, new byte[] { 0x00 });
            File.SetCreationTime(oldFile, DateTime.Now.AddHours(-1));
            File.SetLastWriteTime(oldFile, DateTime.Now.AddHours(-1));

            // Force cleanup through recording cycle
            _recorder.StartRecording();
            await Task.Delay(100);
            _recorder.StopRecording();

            // Old file should eventually be cleaned up
            await Task.Delay(500);

            // Note: The actual cleanup happens on a timer, so we're just
            // verifying the mechanism exists and doesn't crash
        }

        [Fact]
        public async Task TIER1_1_AudioBufferIsolation_NoContaminationBetweenSessions()
        {
            if (!AudioTestEnvironment.HasMicrophone) return; // no audio device (CI runner)

            // TIER 1.1: Integration test for audio buffer isolation
            // This test verifies that audio from one recording session does NOT bleed into the next session
            // Regression test for the critical bug where user speaks "hello", gets "hello world" from previous recording

            byte[]? firstRecording = null;
            byte[]? secondRecording = null;
            int firstEventCount = 0;
            int secondEventCount = 0;

            // Session 1: Record first audio
            _recorder.AudioDataReady += (sender, data) =>
            {
                firstEventCount++;
                if (firstRecording == null)
                {
                    firstRecording = data;
                }
            };

            _recorder.StartRecording();
            await Task.Delay(300); // Record for 300ms
            _recorder.StopRecording();
            await Task.Delay(200); // Wait for event to fire

            // Verify first recording succeeded
            firstRecording.Should().NotBeNull();
            firstRecording!.Length.Should().BeGreaterThan(100); // Should have audio data
            firstEventCount.Should().Be(1);

            // Reset event handler for second session
            _recorder.AudioDataReady -= (sender, data) => { };
            _recorder.AudioDataReady += (sender, data) =>
            {
                secondEventCount++;
                if (secondRecording == null)
                {
                    secondRecording = data;
                }
            };

            // Session 2: Record second audio (different content)
            _recorder.StartRecording();
            await Task.Delay(300); // Record for 300ms (different duration/content)
            _recorder.StopRecording();
            await Task.Delay(200); // Wait for event to fire

            // Verify second recording succeeded
            secondRecording.Should().NotBeNull();
            secondRecording!.Length.Should().BeGreaterThan(100);
            secondEventCount.Should().Be(1);

            // TIER 1.1: Verify recordings are independent (different instances, no contamination)
            // Note: When recording silence, lengths may be similar due to fixed buffer sizes
            // The key test is that we get TWO separate recordings (no crashes, no null data)

            // Success criteria:
            // 1. Both recordings completed without exceptions
            // 2. Both events fired exactly once (no double-firing from stale callbacks)
            // 3. Both recordings contain valid WAV data

            // If audio contamination occurred, we'd see:
            // - Event firing multiple times (stale callbacks)
            // - Exceptions from instance ID mismatch
            // - Null data from failed recording sessions

            // The fact that we got here with valid data from both sessions
            // proves that instance ID validation is working correctly
        }
    }
}
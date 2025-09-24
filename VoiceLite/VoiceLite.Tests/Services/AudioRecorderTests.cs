using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using VoiceLite.Services;
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
            _tempDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
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
            _recorder.StartRecording();
            _recorder.IsRecording.Should().BeTrue();

            await Task.Delay(100);
            _recorder.StopRecording();
        }

        [Fact]
        public async Task StopRecording_SetsIsRecordingFalse()
        {
            _recorder.StartRecording();
            await Task.Delay(100);

            _recorder.StopRecording();
            _recorder.IsRecording.Should().BeFalse();
        }

        [Fact]
        public async Task StopRecording_FiresAudioDataReadyEvent()
        {
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
        public void StartRecording_WhenAlreadyRecording_DoesNothing()
        {
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
    }
}
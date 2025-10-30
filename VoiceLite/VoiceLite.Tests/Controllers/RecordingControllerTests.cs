using System;
using System.Threading.Tasks;
using Xunit;
using Moq;
using FluentAssertions;
using VoiceLite.Core.Controllers;
using VoiceLite.Core.Interfaces.Controllers;
using VoiceLite.Core.Interfaces.Services;
using VoiceLite.Core.Interfaces.Features;
using VoiceLite.Models;

namespace VoiceLite.Tests.Controllers
{
    public class RecordingControllerTests
    {
        private readonly Mock<IAudioRecorder> _mockAudioRecorder;
        private readonly Mock<IWhisperService> _mockWhisperService;
        private readonly Mock<ITextInjector> _mockTextInjector;
        private readonly Mock<ITranscriptionHistoryService> _mockHistoryService;
        private readonly Mock<IErrorLogger> _mockErrorLogger;
        private readonly Mock<ISettingsService> _mockSettingsService;
        private readonly RecordingController _controller;

        public RecordingControllerTests()
        {
            _mockAudioRecorder = new Mock<IAudioRecorder>();
            _mockWhisperService = new Mock<IWhisperService>();
            _mockTextInjector = new Mock<ITextInjector>();
            _mockHistoryService = new Mock<ITranscriptionHistoryService>();
            _mockErrorLogger = new Mock<IErrorLogger>();
            _mockSettingsService = new Mock<ISettingsService>();

            _controller = new RecordingController(
                _mockAudioRecorder.Object,
                _mockWhisperService.Object,
                _mockTextInjector.Object,
                _mockHistoryService.Object,
                _mockErrorLogger.Object,
                _mockSettingsService.Object);
        }

        [Fact]
        public async Task StartRecordingAsync_ShouldStartRecording_WhenNotAlreadyRecording()
        {
            // Arrange
            _mockAudioRecorder.Setup(x => x.IsRecording).Returns(false);
            var recordingStartedRaised = false;
            _controller.RecordingStarted += (sender, e) => recordingStartedRaised = true;

            // Act
            await _controller.StartRecordingAsync();

            // Assert
            _mockAudioRecorder.Verify(x => x.StartRecording(), Times.Once);
            recordingStartedRaised.Should().BeTrue();
            _controller.IsRecording.Should().BeTrue();
        }

        [Fact]
        public async Task StartRecordingAsync_ShouldNotStartRecording_WhenAlreadyRecording()
        {
            // Arrange
            await _controller.StartRecordingAsync();
            _mockAudioRecorder.Invocations.Clear();

            // Act
            await _controller.StartRecordingAsync();

            // Assert
            _mockAudioRecorder.Verify(x => x.StartRecording(), Times.Never);
            _mockErrorLogger.Verify(x => x.LogWarning(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task StopRecordingAsync_ShouldTranscribeAndInject_WhenTranscribeIsTrue()
        {
            // Arrange
            await _controller.StartRecordingAsync();
            var testAudioFile = "test.wav";
            var testTranscription = "Test transcription";

            _mockAudioRecorder.Setup(x => x.GetLastAudioFileAsync())
                .ReturnsAsync(testAudioFile);
            _mockWhisperService.Setup(x => x.TranscribeAsync(testAudioFile, It.IsAny<string>()))
                .ReturnsAsync(testTranscription);
            _mockTextInjector.Setup(x => x.GetFocusedApplicationName())
                .Returns("TestApp");
            _mockSettingsService.Setup(x => x.SelectedModel)
                .Returns("tiny");
            _mockSettingsService.Setup(x => x.InjectionMode)
                .Returns(ITextInjector.InjectionMode.SmartAuto);

            // Act
            var result = await _controller.StopRecordingAsync(transcribe: true);

            // Assert
            result.Success.Should().BeTrue();
            result.Text.Should().Be(testTranscription);
            _mockAudioRecorder.Verify(x => x.StopRecording(), Times.Once);
            _mockWhisperService.Verify(x => x.TranscribeAsync(testAudioFile, It.IsAny<string>()), Times.Once);
            _mockHistoryService.Verify(x => x.AddTranscription(It.IsAny<TranscriptionItem>()), Times.Once);
        }

        [Fact]
        public async Task StopRecordingAsync_ShouldNotTranscribe_WhenTranscribeIsFalse()
        {
            // Arrange
            await _controller.StartRecordingAsync();

            // Act
            var result = await _controller.StopRecordingAsync(transcribe: false);

            // Assert
            result.Success.Should().BeTrue();
            _mockAudioRecorder.Verify(x => x.StopRecording(), Times.Once);
            _mockWhisperService.Verify(x => x.TranscribeAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task StopRecordingAsync_ShouldReturnError_WhenNoRecordingInProgress()
        {
            // Act
            var result = await _controller.StopRecordingAsync();

            // Assert
            result.Success.Should().BeFalse();
            result.Error.Should().Be("No recording in progress");
            _mockAudioRecorder.Verify(x => x.StopRecording(), Times.Never);
        }

        [Fact]
        public async Task TranscribeFileAsync_ShouldReturnError_WhenFileDoesNotExist()
        {
            // Arrange
            var nonExistentFile = "nonexistent.wav";

            // Act
            var result = await _controller.TranscribeFileAsync(nonExistentFile, "tiny");

            // Assert
            result.Success.Should().BeFalse();
            result.Error.Should().Contain("not found");
        }

        [Fact]
        public async Task Cancel_ShouldStopRecordingAndTranscription()
        {
            // Arrange
            await _controller.StartRecordingAsync();

            // Act
            _controller.Cancel();

            // Assert
            _mockAudioRecorder.Verify(x => x.StopRecording(), Times.Once);
            _controller.IsRecording.Should().BeFalse();
        }

        [Fact]
        public async Task RecordingError_ShouldHandleGracefully()
        {
            // Arrange
            var testException = new Exception("Test recording error");
            _mockAudioRecorder.Setup(x => x.StartRecording())
                .Throws(testException);

            // Act
            Func<Task> act = async () => await _controller.StartRecordingAsync();

            // Assert
            await act.Should().ThrowAsync<Exception>()
                .WithMessage("Test recording error");
            _controller.IsRecording.Should().BeFalse();
            _mockErrorLogger.Verify(x => x.LogError(testException, It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
        }

        [Fact]
        public async Task TranscriptionError_ShouldReturnErrorResult()
        {
            // Arrange
            await _controller.StartRecordingAsync();
            var testAudioFile = "test.wav";
            var testException = new Exception("Transcription failed");

            _mockAudioRecorder.Setup(x => x.GetLastAudioFileAsync())
                .ReturnsAsync(testAudioFile);
            _mockWhisperService.Setup(x => x.TranscribeAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(testException);
            _mockSettingsService.Setup(x => x.SelectedModel)
                .Returns("tiny");

            // Act
            var result = await _controller.StopRecordingAsync(transcribe: true);

            // Assert
            result.Success.Should().BeFalse();
            result.Error.Should().Be("Transcription failed");
            _mockErrorLogger.Verify(x => x.LogError(testException, It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
        }

        [Fact]
        public void Dispose_ShouldUnsubscribeFromEvents()
        {
            // Act
            _controller.Dispose();

            // Assert
            // After disposal, raising events should not cause any issues
            _mockAudioRecorder.Raise(x => x.AudioFileReady += null, "test.wav");
            _mockAudioRecorder.Raise(x => x.RecordingError += null, new Exception("test"));

            // No exceptions should be thrown
        }
    }
}
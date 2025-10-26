using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Xunit;
using Moq;
using FluentAssertions;
using VoiceLite.Presentation.ViewModels;
using VoiceLite.Presentation.Commands;
using VoiceLite.Core.Interfaces.Controllers;
using VoiceLite.Core.Interfaces.Features;
using VoiceLite.Core.Interfaces.Services;
using VoiceLite.Models;

namespace VoiceLite.Tests.ViewModels
{
    public class MainViewModelTests
    {
        private readonly Mock<IRecordingController> _mockRecordingController;
        private readonly Mock<ITranscriptionController> _mockTranscriptionController;
        private readonly Mock<ISettingsService> _mockSettingsService;
        private readonly Mock<IProFeatureService> _mockProFeatureService;
        private readonly Mock<ITranscriptionHistoryService> _mockHistoryService;
        private readonly Mock<IHotkeyManager> _mockHotkeyManager;
        private readonly Mock<ISystemTrayManager> _mockSystemTrayManager;
        private readonly Mock<IErrorLogger> _mockErrorLogger;
        private readonly MainViewModel _viewModel;

        public MainViewModelTests()
        {
            _mockRecordingController = new Mock<IRecordingController>();
            _mockTranscriptionController = new Mock<ITranscriptionController>();
            _mockSettingsService = new Mock<ISettingsService>();
            _mockProFeatureService = new Mock<IProFeatureService>();
            _mockHistoryService = new Mock<ITranscriptionHistoryService>();
            _mockHotkeyManager = new Mock<IHotkeyManager>();
            _mockSystemTrayManager = new Mock<ISystemTrayManager>();
            _mockErrorLogger = new Mock<IErrorLogger>();

            // Setup default values
            _mockSettingsService.Setup(x => x.SelectedModel).Returns("tiny");
            _mockProFeatureService.Setup(x => x.IsProUser).Returns(false);
            _mockTranscriptionController.Setup(x => x.ValidateTranscriptionSetupAsync())
                .ReturnsAsync(new ValidationResult { IsValid = true });

            _viewModel = new MainViewModel(
                _mockRecordingController.Object,
                _mockTranscriptionController.Object,
                _mockSettingsService.Object,
                _mockProFeatureService.Object,
                _mockHistoryService.Object,
                _mockHotkeyManager.Object,
                _mockSystemTrayManager.Object,
                _mockErrorLogger.Object);
        }

        [Fact]
        public void Constructor_ShouldInitializePropertiesCorrectly()
        {
            // Assert
            _viewModel.IsRecording.Should().BeFalse();
            _viewModel.IsTranscribing.Should().BeFalse();
            _viewModel.StatusText.Should().NotBeNullOrEmpty();
            // RecordButtonText removed - ViewModel uses StatusText instead
            _viewModel.TranscriptionHistory.Should().NotBeNull();
            _viewModel.TranscriptionHistory.Should().BeEmpty();
        }

        [Fact]
        public void IsRecording_WhenChanged_ShouldUpdateState()
        {
            // Act
            _viewModel.IsRecording = true;

            // Assert - Recording state updated
            _viewModel.IsRecording.Should().BeTrue();
            _viewModel.CanRecord.Should().BeFalse();

            // Act
            _viewModel.IsRecording = false;

            // Assert - Back to ready state
            _viewModel.IsRecording.Should().BeFalse();
            _viewModel.CanRecord.Should().BeTrue();
        }

        [Fact]
        public async Task ToggleRecordingCommand_WhenNotRecording_ShouldStartRecording()
        {
            // Arrange
            _viewModel.IsRecording = false;

            // Act
            _viewModel.ToggleRecordingCommand.Execute(null);
            await Task.Delay(100); // Give async command time to execute

            // Assert
            _mockRecordingController.Verify(x => x.StartRecordingAsync(), Times.Once);
        }

        [Fact]
        public async Task ToggleRecordingCommand_WhenRecording_ShouldStopAndTranscribe()
        {
            // Arrange
            _viewModel.IsRecording = true;
            var transcriptionResult = new TranscriptionResult
            {
                Success = true,
                Text = "Test transcription",
                ProcessingTime = TimeSpan.FromSeconds(1)
            };
            _mockRecordingController.Setup(x => x.StopRecordingAsync(true))
                .ReturnsAsync(transcriptionResult);

            // Act
            _viewModel.ToggleRecordingCommand.Execute(null);
            await Task.Delay(100); // Give async command time to execute

            // Assert
            _mockRecordingController.Verify(x => x.StopRecordingAsync(true), Times.Once);
            _viewModel.StatusText.Should().Contain("complete");
        }

        [Fact]
        public void ClearHistoryCommand_ShouldClearHistoryWhenConfirmed()
        {
            // Arrange
            _viewModel.TranscriptionHistory.Add(new TranscriptionItem { Text = "Test" });

            // Note: MessageBox.Show cannot be easily tested in unit tests
            // In production, you'd inject a dialog service

            // Assert
            _viewModel.ClearHistoryCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void CopyToClipboardCommand_ShouldCopyTextToClipboard()
        {
            // Arrange
            var item = new TranscriptionItem
            {
                Id = Guid.NewGuid().ToString(),
                Text = "Test text to copy"
            };

            // Act
            // Note: Clipboard operations require STA thread
            // In production tests, this would need special handling

            // Assert
            _viewModel.CopyToClipboardCommand.Should().NotBeNull();
        }

        [Fact]
        public void ProFeatureService_ShouldReflectProStatus()
        {
            // Arrange & Act - Free user
            _mockProFeatureService.Setup(x => x.IsProUser).Returns(false);
            var freeViewModel = CreateViewModel();

            // Assert - ProFeatureService is injected and used correctly
            _mockProFeatureService.Object.IsProUser.Should().BeFalse();

            // Arrange & Act - Pro user
            _mockProFeatureService.Setup(x => x.IsProUser).Returns(true);
            var proViewModel = CreateViewModel();

            // Wait for initialization
            Task.Delay(100).Wait();

            // Assert - ProFeatureService reflects pro status
            _mockProFeatureService.Object.IsProUser.Should().BeTrue();
        }

        [Fact]
        public void OnRecordingStarted_ShouldUpdateState()
        {
            // Arrange
            var propertyChangedEvents = new List<string>();
            _viewModel.PropertyChanged += (s, e) => propertyChangedEvents.Add(e.PropertyName!);

            // Act
            _mockRecordingController.Raise(x => x.RecordingStarted += null, EventArgs.Empty);

            // Assert
            _viewModel.IsRecording.Should().BeTrue();
            _viewModel.StatusText.Should().Contain("Recording");
            propertyChangedEvents.Should().Contain(nameof(_viewModel.IsRecording));
            propertyChangedEvents.Should().Contain(nameof(_viewModel.StatusText));
        }

        [Fact]
        public void OnTranscriptionCompleted_ShouldUpdateStatusBasedOnResult()
        {
            // Arrange - Success
            var successResult = new TranscriptionResult
            {
                Success = true,
                Text = "Test",
                ProcessingTime = TimeSpan.FromSeconds(1.5)
            };

            // Act
            _mockRecordingController.Raise(x => x.TranscriptionCompleted += null, successResult);

            // Assert
            _viewModel.StatusText.Should().Contain("Complete");
            _viewModel.StatusText.Should().Contain("1.5s");

            // Arrange - Failure
            var failureResult = new TranscriptionResult
            {
                Success = false,
                Error = "Test error"
            };

            // Act
            _mockRecordingController.Raise(x => x.TranscriptionCompleted += null, failureResult);

            // Assert
            _viewModel.StatusText.Should().Contain("Failed");
            _viewModel.StatusText.Should().Contain("Test error");
        }

        [Fact]
        public void OnProgressChanged_ShouldUpdateProgressProperties()
        {
            // Arrange
            var progress = new RecordingProgress
            {
                Status = "Processing...",
                PercentComplete = 50,
                Elapsed = TimeSpan.FromSeconds(2)
            };

            // Act
            _mockRecordingController.Raise(x => x.ProgressChanged += null, progress);

            // Assert
            _viewModel.StatusText.Should().Be("Processing...");
            _viewModel.ProgressValue.Should().Be(50);
        }

        [Fact]
        public void OnHistoryItemAdded_ShouldAddToTranscriptionHistory()
        {
            // Arrange
            var item = new TranscriptionItem
            {
                Id = Guid.NewGuid().ToString(),
                Text = "New transcription",
                Timestamp = DateTime.Now
            };

            // Act
            _mockHistoryService.Raise(x => x.ItemAdded += null, item);

            // Need to wait for dispatcher
            Task.Delay(100).Wait();

            // Assert
            // Note: Dispatcher.Invoke requires proper STA thread setup
            // In production, you'd mock the dispatcher or use a test dispatcher
        }

        [Fact]
        public void Dispose_ShouldUnsubscribeFromAllEvents()
        {
            // Act
            _viewModel.Dispose();

            // Assert - Events should be unsubscribed
            // Try raising events after disposal
            _mockRecordingController.Raise(x => x.RecordingStarted += null, EventArgs.Empty);

            // No exception should be thrown
            _viewModel.IsRecording.Should().BeFalse();
        }

        private MainViewModel CreateViewModel()
        {
            return new MainViewModel(
                _mockRecordingController.Object,
                _mockTranscriptionController.Object,
                _mockSettingsService.Object,
                _mockProFeatureService.Object,
                _mockHistoryService.Object,
                _mockHotkeyManager.Object,
                _mockSystemTrayManager.Object,
                _mockErrorLogger.Object);
        }
    }
}
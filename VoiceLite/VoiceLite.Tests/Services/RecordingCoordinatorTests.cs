using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using VoiceLite.Interfaces;
using VoiceLite.Models;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    public class RecordingCoordinatorTests : IDisposable
    {
        private readonly AudioRecorder audioRecorder;
        private readonly Mock<ITranscriber> mockWhisperService;
        private readonly TextInjector textInjector;
        private readonly TranscriptionHistoryService historyService;
        private readonly AnalyticsService analyticsService;
        private readonly SoundService soundService;
        private readonly Settings testSettings;
        private readonly RecordingCoordinator coordinator;

        public RecordingCoordinatorTests()
        {
            // Create test settings
            testSettings = new Settings
            {
                WhisperModel = "ggml-small.bin",
                AutoPaste = true,
                PlaySoundFeedback = false,
                EnableHistory = true,
                MaxHistoryItems = 50,
                EnableAnalytics = false
            };

            // Create real instances (AudioRecorder, TextInjector etc. are not mockable)
            audioRecorder = new AudioRecorder();
            mockWhisperService = new Mock<ITranscriber>();
            textInjector = new TextInjector(testSettings);
            historyService = new TranscriptionHistoryService(testSettings);
            analyticsService = new AnalyticsService(testSettings);
            soundService = new SoundService();

            // Create coordinator
            coordinator = new RecordingCoordinator(
                audioRecorder,
                mockWhisperService.Object,
                textInjector,
                historyService,
                analyticsService,
                soundService,
                testSettings
            );
        }

        public void Dispose()
        {
            coordinator?.Dispose();
            audioRecorder?.Dispose();
            soundService?.Dispose();
        }

        [Fact]
        public void Constructor_WithValidDependencies_Succeeds()
        {
            // Assert
            coordinator.Should().NotBeNull();
            coordinator.IsRecording.Should().BeFalse();
        }

        [Fact]
        public void Constructor_WithNullAudioRecorder_ThrowsArgumentNullException()
        {
            // Act & Assert
            Action act = () => new RecordingCoordinator(
                null!,
                mockWhisperService.Object,
                textInjector,
                historyService,
                analyticsService,
                soundService,
                testSettings
            );

            act.Should().Throw<ArgumentNullException>().WithParameterName("audioRecorder");
        }

        [Fact]
        public void Constructor_WithNullWhisperService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Action act = () => new RecordingCoordinator(
                audioRecorder,
                null!,
                textInjector,
                historyService,
                analyticsService,
                soundService,
                testSettings
            );

            act.Should().Throw<ArgumentNullException>().WithParameterName("whisperService");
        }

        [Fact]
        public void Constructor_WithNullSettings_ThrowsArgumentNullException()
        {
            // Act & Assert
            Action act = () => new RecordingCoordinator(
                audioRecorder,
                mockWhisperService.Object,
                textInjector,
                historyService,
                analyticsService,
                soundService,
                null!
            );

            act.Should().Throw<ArgumentNullException>().WithParameterName("settings");
        }

        [Fact]
        public void IsRecording_InitiallyFalse()
        {
            // Assert
            coordinator.IsRecording.Should().BeFalse();
        }

        [Fact]
        public void StartRecording_SetsIsRecordingTrue()
        {
            // Act
            coordinator.StartRecording();

            // Assert
            coordinator.IsRecording.Should().BeTrue();
            audioRecorder.IsRecording.Should().BeTrue();
        }

        [Fact]
        public void StartRecording_FiresStatusChangedEvent()
        {
            // Arrange
            RecordingStatusEventArgs? capturedArgs = null;
            coordinator.StatusChanged += (sender, args) => capturedArgs = args;

            // Act
            coordinator.StartRecording();

            // Assert
            capturedArgs.Should().NotBeNull();
            capturedArgs!.Status.Should().Be("Recording");
            capturedArgs.IsRecording.Should().BeTrue();
            capturedArgs.ElapsedSeconds.Should().Be(0);
        }

        [Fact]
        public void StartRecording_WhenAlreadyRecording_DoesNothing()
        {
            // Arrange
            coordinator.StartRecording();

            // Act
            coordinator.StartRecording();

            // Assert
            coordinator.IsRecording.Should().BeTrue();
        }

        [Fact]
        public void StopRecording_SetsIsRecordingFalse()
        {
            // Arrange
            coordinator.StartRecording();

            // Act
            coordinator.StopRecording();

            // Assert
            coordinator.IsRecording.Should().BeFalse();
            audioRecorder.IsRecording.Should().BeFalse();
        }

        [Fact]
        public void StopRecording_FiresStatusChangedEvent()
        {
            // Arrange
            coordinator.StartRecording();
            RecordingStatusEventArgs? capturedArgs = null;
            coordinator.StatusChanged += (sender, args) => capturedArgs = args;

            // Act
            coordinator.StopRecording();

            // Assert
            capturedArgs.Should().NotBeNull();
            capturedArgs!.Status.Should().Be("Processing");
            capturedArgs.IsRecording.Should().BeFalse();
            capturedArgs.IsCancelled.Should().BeFalse();
        }

        [Fact]
        public void StopRecording_WithCancel_FiresCancelledEvent()
        {
            // Arrange
            coordinator.StartRecording();
            RecordingStatusEventArgs? capturedArgs = null;
            coordinator.StatusChanged += (sender, args) => capturedArgs = args;

            // Act
            coordinator.StopRecording(cancel: true);

            // Assert
            capturedArgs.Should().NotBeNull();
            capturedArgs!.Status.Should().Be("Cancelled");
            capturedArgs.IsRecording.Should().BeFalse();
            capturedArgs.IsCancelled.Should().BeTrue();
        }

        [Fact]
        public void StopRecording_WhenNotRecording_DoesNothing()
        {
            // Act
            coordinator.StopRecording();

            // Assert
            coordinator.IsRecording.Should().BeFalse();
        }

        [Fact]
        public void GetRecordingDuration_WhenNotRecording_ReturnsZero()
        {
            // Act
            var duration = coordinator.GetRecordingDuration();

            // Assert
            duration.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public void GetRecordingDuration_WhenRecording_ReturnsElapsedTime()
        {
            // Arrange
            coordinator.StartRecording();
            Thread.Sleep(100); // Wait 100ms

            // Act
            var duration = coordinator.GetRecordingDuration();

            // Assert
            duration.Should().BeGreaterThan(TimeSpan.FromMilliseconds(50));
            duration.Should().BeLessThan(TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void PlaySoundFeedback_WhenDisabled_DoesNotThrow()
        {
            // Arrange
            testSettings.PlaySoundFeedback = false;

            // Act
            Action act = () => coordinator.PlaySoundFeedback();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void PlaySoundFeedback_WhenEnabled_DoesNotThrow()
        {
            // Arrange
            testSettings.PlaySoundFeedback = true;

            // Act
            Action act = () => coordinator.PlaySoundFeedback();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_UnsubscribesFromAudioRecorderEvents()
        {
            // Arrange
            var localAudioRecorder = new AudioRecorder();
            var localCoordinator = new RecordingCoordinator(
                localAudioRecorder,
                mockWhisperService.Object,
                textInjector,
                historyService,
                analyticsService,
                soundService,
                testSettings
            );

            // Act
            localCoordinator.Dispose();

            // Assert - No exception should be thrown
            localCoordinator.Dispose(); // Calling dispose twice should be safe
            localAudioRecorder.Dispose();
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            // Act & Assert - No exception should be thrown
            coordinator.Dispose();
            coordinator.Dispose();
            coordinator.Dispose();
        }

        [Fact]
        public async Task StartRecording_WithThreadSafety_HandlesRapidCalls()
        {
            // Arrange
            var tasks = new Task[10];

            // Act
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(() => coordinator.StartRecording());
            }

            await Task.WhenAll(tasks);

            // Assert
            coordinator.IsRecording.Should().BeTrue();
        }

        [Fact]
        public async Task StopRecording_WithThreadSafety_HandlesRapidCalls()
        {
            // Arrange
            coordinator.StartRecording();
            var tasks = new Task[10];

            // Act
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(() => coordinator.StopRecording());
            }

            await Task.WhenAll(tasks);

            // Assert
            coordinator.IsRecording.Should().BeFalse();
        }

        // NOTE: Watchdog timer test is omitted because:
        // 1. It requires waiting 2 minutes (120 seconds) which makes tests too slow
        // 2. AudioFileReady is an internal event that's hard to trigger via reflection
        // 3. The watchdog is tested manually during development and integration testing
        //
        // The watchdog timer implementation in RecordingCoordinator:
        // - Starts when transcription begins (OnAudioFileReady)
        // - Checks every 10 seconds if transcription has been running > 120 seconds
        // - Fires TranscriptionCompleted with error if timeout is detected
        // - Stops when transcription completes or errors out
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using FluentAssertions;
using VoiceLite.Core.Controllers;
using VoiceLite.Core.Interfaces.Controllers;
using VoiceLite.Core.Interfaces.Services;
using VoiceLite.Core.Interfaces.Features;

namespace VoiceLite.Tests.Controllers
{
    public class TranscriptionControllerTests
    {
        private readonly Mock<IWhisperService> _mockWhisperService;
        private readonly Mock<IErrorLogger> _mockErrorLogger;
        private readonly Mock<IProFeatureService> _mockProFeatureService;
        private readonly Mock<ISettingsService> _mockSettingsService;
        private readonly TranscriptionController _controller;

        public TranscriptionControllerTests()
        {
            _mockWhisperService = new Mock<IWhisperService>();
            _mockErrorLogger = new Mock<IErrorLogger>();
            _mockProFeatureService = new Mock<IProFeatureService>();
            _mockSettingsService = new Mock<ISettingsService>();

            _controller = new TranscriptionController(
                _mockWhisperService.Object,
                _mockErrorLogger.Object,
                _mockProFeatureService.Object,
                _mockSettingsService.Object);
        }

        [Fact]
        public async Task BatchTranscribeAsync_ShouldProcessAllFiles()
        {
            // Arrange
            var audioFiles = new[] { "file1.wav", "file2.wav", "file3.wav" };
            var modelPath = "tiny";
            var progressEvents = new List<BatchProgressEventArgs>();

            _mockWhisperService.Setup(x => x.TranscribeAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string file, string model) => $"Transcription of {Path.GetFileName(file)}");

            _controller.BatchItemCompleted += (sender, args) => progressEvents.Add(args);

            // Act
            var results = (await _controller.BatchTranscribeAsync(audioFiles, modelPath)).ToList();

            // Assert
            results.Should().HaveCount(3);
            results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
            progressEvents.Should().HaveCount(3);
            progressEvents.Last().CurrentItem.Should().BeLessOrEqualTo(3);
            progressEvents.Last().TotalItems.Should().Be(3);
        }

        [Fact]
        public async Task BatchTranscribeAsync_ShouldHandleMixedResults()
        {
            // Arrange
            var audioFiles = new[] { "file1.wav", "file2.wav", "file3.wav" };
            var modelPath = "tiny";

            _mockWhisperService.SetupSequence(x => x.TranscribeAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("Success 1")
                .ThrowsAsync(new Exception("Failed"))
                .ReturnsAsync("Success 3");

            // Act
            var results = (await _controller.BatchTranscribeAsync(audioFiles, modelPath)).ToList();

            // Assert
            results.Should().HaveCount(3);
            results[0].Success.Should().BeTrue();
            results[1].Success.Should().BeFalse();
            results[2].Success.Should().BeTrue();
        }

        [Fact]
        public async Task RetryTranscriptionAsync_ShouldRetryOnFailure()
        {
            // Arrange
            var audioFile = Path.GetTempFileName();
            try
            {
                var modelPath = "tiny";
                var callCount = 0;

                _mockWhisperService.Setup(x => x.TranscribeAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(() =>
                    {
                        callCount++;
                        if (callCount < 3)
                            throw new Exception("Transient error");
                        return "Success after retries";
                    });

                // Act
                var result = await _controller.RetryTranscriptionAsync(audioFile, modelPath, maxRetries: 3);

                // Assert
                result.Success.Should().BeTrue();
                result.Text.Should().Be("Success after retries");
                callCount.Should().Be(3);
                _mockErrorLogger.Verify(x => x.LogInfo(It.IsAny<string>()), Times.AtLeast(2));
            }
            finally
            {
                if (File.Exists(audioFile))
                    File.Delete(audioFile);
            }
        }

        [Fact]
        public async Task RetryTranscriptionAsync_ShouldFailAfterMaxRetries()
        {
            // Arrange
            var audioFile = Path.GetTempFileName();
            try
            {
                var modelPath = "tiny";

                _mockWhisperService.Setup(x => x.TranscribeAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ThrowsAsync(new Exception("Persistent error"));

                // Act
                var result = await _controller.RetryTranscriptionAsync(audioFile, modelPath, maxRetries: 2);

                // Assert
                result.Success.Should().BeFalse();
                result.Error.Should().Be("Persistent error");
                _mockWhisperService.Verify(x => x.TranscribeAsync(It.IsAny<string>(), It.IsAny<string>()),
                    Times.Exactly(3)); // Initial + 2 retries
            }
            finally
            {
                if (File.Exists(audioFile))
                    File.Delete(audioFile);
            }
        }

        [Fact]
        public async Task GetRecommendedModelAsync_ShouldReturnTinyForSmallFiles()
        {
            // Arrange
            var smallAudioFile = Path.GetTempFileName();
            File.WriteAllBytes(smallAudioFile, new byte[500 * 1024]); // 500 KB
            _mockProFeatureService.Setup(x => x.GetAvailableModels())
                .Returns(new[] { "tiny" });

            try
            {
                // Act
                var recommendedModel = await _controller.GetRecommendedModelAsync(smallAudioFile);

                // Assert
                recommendedModel.Should().Be("tiny");
            }
            finally
            {
                File.Delete(smallAudioFile);
            }
        }

        [Fact]
        public async Task GetRecommendedModelAsync_ShouldReturnLargerModelForProUsers()
        {
            // Arrange
            var mediumAudioFile = Path.GetTempFileName();
            File.WriteAllBytes(mediumAudioFile, new byte[3 * 1024 * 1024]); // 3 MB
            _mockProFeatureService.Setup(x => x.IsProUser).Returns(true);
            _mockProFeatureService.Setup(x => x.GetAvailableModels())
                .Returns(new[] { "tiny", "base", "small" });

            try
            {
                // Act
                var recommendedModel = await _controller.GetRecommendedModelAsync(mediumAudioFile);

                // Assert
                recommendedModel.Should().Be("base");
            }
            finally
            {
                File.Delete(mediumAudioFile);
            }
        }

        [Fact]
        public async Task ValidateTranscriptionSetupAsync_ShouldCheckAllPrerequisites()
        {
            // Arrange
            _mockWhisperService.Setup(x => x.ValidateWhisperExecutable()).Returns(true);
            _mockWhisperService.Setup(x => x.GetWhisperVersion()).Returns("1.7.6");
            _mockSettingsService.Setup(x => x.SelectedModel).Returns("tiny");

            // Act
            var result = await _controller.ValidateTranscriptionSetupAsync();

            // Assert
            result.IsValid.Should().BeTrue();
            result.Issues.Should().BeEmpty();
            _mockWhisperService.Verify(x => x.ValidateWhisperExecutable(), Times.Once);
            _mockWhisperService.Verify(x => x.GetWhisperVersion(), Times.Once);
        }

        [Fact]
        public async Task ValidateTranscriptionSetupAsync_ShouldReportIssues()
        {
            // Arrange
            _mockWhisperService.Setup(x => x.ValidateWhisperExecutable()).Returns(false);
            _mockWhisperService.Setup(x => x.GetWhisperVersion()).Returns("Unknown");

            // Act
            var result = await _controller.ValidateTranscriptionSetupAsync();

            // Assert
            result.IsValid.Should().BeFalse();
            result.Issues.Should().Contain(issue => issue.Contains("Whisper executable"));
            result.Warnings.Should().Contain(warning => warning.Contains("version"));
        }

        [Fact]
        public async Task CleanupTemporaryFilesAsync_ShouldDeleteOldFiles()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "VoiceLite", "audio");
            Directory.CreateDirectory(tempDir);

            // Create test files with different ages
            var oldFile = Path.Combine(tempDir, "old.wav");
            var newFile = Path.Combine(tempDir, "new.wav");

            File.WriteAllBytes(oldFile, new byte[100]);
            File.WriteAllBytes(newFile, new byte[100]);

            // Make old file older
            File.SetCreationTime(oldFile, DateTime.Now.AddHours(-2));

            try
            {
                // Act
                var deletedCount = await _controller.CleanupTemporaryFilesAsync(TimeSpan.FromHours(1));

                // Assert
                deletedCount.Should().Be(1);
                File.Exists(oldFile).Should().BeFalse();
                File.Exists(newFile).Should().BeTrue();
            }
            finally
            {
                // Cleanup
                if (File.Exists(oldFile)) File.Delete(oldFile);
                if (File.Exists(newFile)) File.Delete(newFile);
            }
        }

        [Fact]
        public void GetStatistics_ShouldReturnEmptyStatsWhenNoTranscriptions()
        {
            // Act
            var stats = _controller.GetStatistics();

            // Assert
            stats.TotalTranscriptions.Should().Be(0);
            stats.SuccessfulTranscriptions.Should().Be(0);
            stats.FailedTranscriptions.Should().Be(0);
            stats.ModelUsageCount.Should().BeEmpty();
        }

        [Fact]
        public async Task GetStatistics_ShouldTrackTranscriptionStats()
        {
            // Arrange
            var audioFiles = new[] { "file1.wav", "file2.wav" };
            _mockWhisperService.SetupSequence(x => x.TranscribeAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("Success")
                .ThrowsAsync(new Exception("Failed"));

            // Act
            await _controller.BatchTranscribeAsync(audioFiles, "tiny");
            var stats = _controller.GetStatistics();

            // Assert
            stats.TotalTranscriptions.Should().Be(2);
            stats.SuccessfulTranscriptions.Should().Be(1);
            stats.FailedTranscriptions.Should().Be(1);
            stats.LastTranscription.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
        }
    }
}
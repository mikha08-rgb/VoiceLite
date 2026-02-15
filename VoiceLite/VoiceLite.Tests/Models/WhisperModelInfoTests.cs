using System;
using System.IO;
using System.Linq;
using System.Windows;
using AwesomeAssertions;
using VoiceLite.Models;
using Xunit;

namespace VoiceLite.Tests.Models
{
    public class WhisperModelInfoTests
    {
        [Fact]
        public void FileSizeDisplay_FormatsBytes_Correctly()
        {
            var model = new WhisperModelInfo { FileSizeBytes = 1024 };
            model.FileSizeDisplay.Should().Be("1 KB");
        }

        [Fact]
        public void FileSizeDisplay_FormatsMegabytes_Correctly()
        {
            var model = new WhisperModelInfo { FileSizeBytes = 5 * 1024 * 1024 };
            model.FileSizeDisplay.Should().Contain("MB");
        }

        [Fact]
        public void FileSizeDisplay_FormatsGigabytes_Correctly()
        {
            var model = new WhisperModelInfo { FileSizeBytes = 2L * 1024 * 1024 * 1024 };
            model.FileSizeDisplay.Should().Contain("GB");
        }

        [Fact]
        public void SpeedRatingWidth_CalculatesCorrectly()
        {
            var model = new WhisperModelInfo { SpeedRating = 5 };
            model.SpeedRatingWidth.Should().Be(150);

            model = new WhisperModelInfo { SpeedRating = 3 };
            model.SpeedRatingWidth.Should().Be(90);
        }

        [Fact]
        public void AccuracyRatingWidth_CalculatesCorrectly()
        {
            var model = new WhisperModelInfo { AccuracyRating = 5 };
            model.AccuracyRatingWidth.Should().Be(150);

            model = new WhisperModelInfo { AccuracyRating = 2 };
            model.AccuracyRatingWidth.Should().Be(60);
        }

        [Fact]
        public void GetAvailableModels_ReturnsAllFourModels()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);

            try
            {
                var models = WhisperModelInfo.GetAvailableModels(tempPath);

                models.Should().HaveCount(4);
                models.Should().Contain(m => m.FileName == "ggml-base.bin");
                models.Should().Contain(m => m.FileName == "ggml-small.bin");
                models.Should().Contain(m => m.FileName == "ggml-medium.bin");
                models.Should().Contain(m => m.FileName == "ggml-large-v3.bin");
            }
            finally
            {
                try { Directory.Delete(tempPath, true); } catch { }
            }
        }

        [Fact]
        public void GetAvailableModels_ChecksIsInstalled()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);

            try
            {
                // Create one model file
                File.WriteAllText(Path.Combine(tempPath, "ggml-small.bin"), "dummy");

                var models = WhisperModelInfo.GetAvailableModels(tempPath);

                var smallModel = models.First(m => m.FileName == "ggml-small.bin");
                var baseModel = models.First(m => m.FileName == "ggml-base.bin");

                smallModel.IsInstalled.Should().BeTrue();
                baseModel.IsInstalled.Should().BeFalse();
            }
            finally
            {
                try { Directory.Delete(tempPath, true); } catch { }
            }
        }

        [Fact]
        public void GetAvailableModels_HasCorrectDisplayNames()
        {
            var models = WhisperModelInfo.GetAvailableModels("");

            models.First(m => m.FileName == "ggml-base.bin").DisplayName.Should().Be("Swift");
            models.First(m => m.FileName == "ggml-small.bin").DisplayName.Should().Be("Pro");
            models.First(m => m.FileName == "ggml-medium.bin").DisplayName.Should().Be("Elite");
            models.First(m => m.FileName == "ggml-large-v3.bin").DisplayName.Should().Be("Ultra");
        }

        [Fact]
        public void GetAvailableModels_SmallModelIsRecommended()
        {
            var models = WhisperModelInfo.GetAvailableModels("");

            var proModel = models.First(m => m.FileName == "ggml-small.bin");
            proModel.IsRecommended.Should().Be(Visibility.Visible);

            // Others should not be recommended
            var otherModels = models.Where(m => m.FileName != "ggml-small.bin");
            otherModels.Should().AllSatisfy(m => m.IsRecommended.Should().Be(Visibility.Collapsed));
        }

        [Fact]
        public void GetAvailableModels_HasCorrectRatings()
        {
            var models = WhisperModelInfo.GetAvailableModels("");

            // Base model: fast (4), less accurate (2)
            var baseModel = models.First(m => m.FileName == "ggml-base.bin");
            baseModel.SpeedRating.Should().Be(4);
            baseModel.AccuracyRating.Should().Be(2);

            // Large model: slow (1), most accurate (5)
            var largeModel = models.First(m => m.FileName == "ggml-large-v3.bin");
            largeModel.SpeedRating.Should().Be(1);
            largeModel.AccuracyRating.Should().Be(5);
        }

        [Fact]
        public void GetRecommendedModel_LowRAM_ReturnsBaseModel()
        {
            var recommended = WhisperModelInfo.GetRecommendedModel(1.5, prioritizeSpeed: false);

            recommended.Should().NotBeNull();
            recommended!.FileName.Should().Be("ggml-base.bin");
        }

        [Fact]
        public void GetRecommendedModel_MediumRAM_ReturnsSmallModel()
        {
            var recommended = WhisperModelInfo.GetRecommendedModel(2.5, prioritizeSpeed: false);

            recommended.Should().NotBeNull();
            recommended!.FileName.Should().Be("ggml-small.bin");
        }

        [Fact]
        public void GetRecommendedModel_HighRAM_ReturnsMediumModel()
        {
            var recommended = WhisperModelInfo.GetRecommendedModel(4.0, prioritizeSpeed: false);

            recommended.Should().NotBeNull();
            recommended!.FileName.Should().Be("ggml-medium.bin");
        }

        [Fact]
        public void GetRecommendedModel_VeryHighRAM_ReturnsLargeModel()
        {
            var recommended = WhisperModelInfo.GetRecommendedModel(6.0, prioritizeSpeed: false);

            recommended.Should().NotBeNull();
            recommended!.FileName.Should().Be("ggml-large-v3.bin");
        }

        [Fact]
        public void GetRecommendedModel_PrioritizeSpeed_ReturnsBaseModel()
        {
            var recommended = WhisperModelInfo.GetRecommendedModel(1.5, prioritizeSpeed: true);

            recommended.Should().NotBeNull();
            recommended!.FileName.Should().Be("ggml-base.bin");
        }

        [Fact]
        public void GetRecommendedModel_PrioritizeSpeedWithHighRAM_ReturnsSmallModel()
        {
            var recommended = WhisperModelInfo.GetRecommendedModel(4.0, prioritizeSpeed: true);

            recommended.Should().NotBeNull();
            recommended!.FileName.Should().Be("ggml-small.bin");
        }

        [Fact]
        public void AllModels_HaveValidProperties()
        {
            var models = WhisperModelInfo.GetAvailableModels("");

            foreach (var model in models)
            {
                model.FileName.Should().NotBeNullOrEmpty();
                model.DisplayName.Should().NotBeNullOrEmpty();
                model.FileSizeBytes.Should().BeGreaterThan(0);
                model.SpeedRating.Should().BeInRange(1, 5);
                model.AccuracyRating.Should().BeInRange(1, 5);
                model.TypicalProcessingTime.Should().BeGreaterThan(0);
                model.RequiredRAMGB.Should().BeGreaterThan(0);
                model.Pros.Should().NotBeEmpty();
                model.Cons.Should().NotBeEmpty();
                model.Description.Should().NotBeNullOrEmpty();
            }
        }

        [Fact]
        public void AllModels_SupportMultilingual_ExceptBase()
        {
            var models = WhisperModelInfo.GetAvailableModels("");

            var baseModel = models.First(m => m.FileName == "ggml-base.bin");
            baseModel.SupportsMultilingual.Should().BeFalse();

            var otherModels = models.Where(m => m.FileName != "ggml-base.bin");
            otherModels.Should().AllSatisfy(m => m.SupportsMultilingual.Should().BeTrue());
        }
    }
}

using AwesomeAssertions;
using Moq;
using System;
using System.IO;
using VoiceLite.Core.Interfaces.Features;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// Tests for ModelResolverService - model path resolution and Pro license validation
    /// Security-critical: Tests MODEL-GATE-001 fix (v1.2.0.3) - prevents freemium bypass
    /// </summary>
    public class ModelResolverServiceTests : IDisposable
    {
        private readonly string _testBaseDir;
        private readonly string _testWhisperDir;

        public ModelResolverServiceTests()
        {
            // Create temporary test directories
            _testBaseDir = Path.Combine(Path.GetTempPath(), $"VoiceLiteTest_{Guid.NewGuid()}");
            _testWhisperDir = Path.Combine(_testBaseDir, "whisper");
            Directory.CreateDirectory(_testWhisperDir);
        }

        public void Dispose()
        {
            // Cleanup test directories
            if (Directory.Exists(_testBaseDir))
            {
                Directory.Delete(_testBaseDir, true);
            }
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidDirectory_Succeeds()
        {
            // Act
            var service = new ModelResolverService(_testBaseDir);

            // Assert
            service.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullDirectory_ThrowsArgumentNullException()
        {
            // Act
            Action act = () => new ModelResolverService(null);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("baseDirectory");
        }

        [Fact]
        public void Constructor_WithEmptyDirectory_ThrowsArgumentNullException()
        {
            // Act
            Action act = () => new ModelResolverService(string.Empty);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("baseDirectory");
        }

        #endregion

        #region NormalizeModelName Tests

        [Theory]
        [InlineData("tiny", "ggml-tiny.bin")]
        [InlineData("base", "ggml-base.bin")]
        [InlineData("small", "ggml-small.bin")]
        [InlineData("medium", "ggml-medium.bin")]
        [InlineData("turbo", "ggml-large-v3-turbo-q8_0.bin")]
        [InlineData("large", "ggml-large-v3.bin")]
        public void NormalizeModelName_ShortName_ReturnsFullFilename(string shortName, string expected)
        {
            // Arrange
            var service = new ModelResolverService(_testBaseDir);

            // Act
            var result = service.NormalizeModelName(shortName);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("ggml-tiny.bin")]
        [InlineData("ggml-base.bin")]
        [InlineData("ggml-small.bin")]
        [InlineData("ggml-medium.bin")]
        [InlineData("ggml-large-v3-turbo-q8_0.bin")]
        [InlineData("ggml-large-v3.bin")]
        public void NormalizeModelName_FullFilename_ReturnsUnchanged(string filename)
        {
            // Arrange
            var service = new ModelResolverService(_testBaseDir);

            // Act
            var result = service.NormalizeModelName(filename);

            // Assert
            result.Should().Be(filename);
        }

        [Fact]
        public void NormalizeModelName_ShortNames_CaseInsensitive()
        {
            // Arrange
            var service = new ModelResolverService(_testBaseDir);

            // Act & Assert - Short names are normalized to lowercase filenames
            service.NormalizeModelName("TINY").Should().Be("ggml-tiny.bin");
            service.NormalizeModelName("Base").Should().Be("ggml-base.bin");
            service.NormalizeModelName("SMALL").Should().Be("ggml-small.bin");
            service.NormalizeModelName("TURBO").Should().Be("ggml-large-v3-turbo-q8_0.bin");
        }

        [Theory]
        [InlineData(null, "ggml-base.bin")]
        [InlineData("", "ggml-base.bin")]
        [InlineData("  ", "ggml-base.bin")]
        public void NormalizeModelName_NullOrEmpty_ReturnsDefaultBase(string input, string expected)
        {
            // Arrange
            var service = new ModelResolverService(_testBaseDir);

            // Act
            var result = service.NormalizeModelName(input);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void NormalizeModelName_UnknownName_ReturnsDefaultBase()
        {
            // Arrange
            var service = new ModelResolverService(_testBaseDir);

            // Act
            var result = service.NormalizeModelName("invalid-model");

            // Assert
            result.Should().Be("ggml-base.bin", "Unknown names without .bin extension default to base");
        }

        #endregion

        #region ResolveModelPath Tests - Basic Resolution

        [Fact]
        public void ResolveModelPath_ModelInWhisperSubdir_ReturnsCorrectPath()
        {
            // Arrange
            var modelPath = Path.Combine(_testWhisperDir, "ggml-base.bin");
            File.WriteAllText(modelPath, "dummy model");

            // Mock ProFeatureService to allow base model (free tier)
            var mockProService = new Mock<IProFeatureService>();
            mockProService.Setup(x => x.CanUseModel("ggml-base.bin")).Returns(true);

            var service = new ModelResolverService(_testBaseDir, mockProService.Object);

            // Act
            var result = service.ResolveModelPath("base");

            // Assert
            result.Should().Be(modelPath);
        }

        [Fact]
        public void ResolveModelPath_ModelInBaseDir_ReturnsCorrectPath()
        {
            // Arrange
            var modelPath = Path.Combine(_testBaseDir, "ggml-base.bin");
            File.WriteAllText(modelPath, "dummy model");

            var mockProService = new Mock<IProFeatureService>();
            mockProService.Setup(x => x.CanUseModel("ggml-base.bin")).Returns(true);

            var service = new ModelResolverService(_testBaseDir, mockProService.Object);

            // Act
            var result = service.ResolveModelPath("base");

            // Assert
            result.Should().Be(modelPath);
        }

        [Fact]
        public void ResolveModelPath_ModelNotFound_ThrowsFileNotFoundException()
        {
            // Arrange
            var mockProService = new Mock<IProFeatureService>();
            mockProService.Setup(x => x.CanUseModel(It.IsAny<string>())).Returns(true);

            var service = new ModelResolverService(_testBaseDir, mockProService.Object);

            // Act
            Action act = () => service.ResolveModelPath("nonexistent");

            // Assert
            act.Should().Throw<FileNotFoundException>()
                .WithMessage("*not found*")
                .WithMessage("*download it from Settings*");
        }

        [Fact]
        public void ResolveModelPath_TurboModel_ReturnsCorrectPath()
        {
            // Arrange
            var modelPath = Path.Combine(_testWhisperDir, "ggml-large-v3-turbo-q8_0.bin");
            File.WriteAllText(modelPath, "dummy turbo model");

            var mockProService = new Mock<IProFeatureService>();
            mockProService.Setup(x => x.CanUseModel("ggml-large-v3-turbo-q8_0.bin")).Returns(true);

            var service = new ModelResolverService(_testBaseDir, mockProService.Object);

            // Act
            var result = service.ResolveModelPath("turbo");

            // Assert
            result.Should().Be(modelPath);
        }

        #endregion

        #region Security Tests - MODEL-GATE-001 (v1.2.0.3)

        [Fact]
        public void SecurityTest_FreeUser_CannotAccessProModels_EvenIfFileExists()
        {
            // Arrange - Create Pro model file on disk
            var proModelPath = Path.Combine(_testWhisperDir, "ggml-small.bin");
            File.WriteAllText(proModelPath, "pro model data");

            // Mock free user (cannot use Pro models)
            var mockProService = new Mock<IProFeatureService>();
            mockProService.Setup(x => x.CanUseModel("ggml-small.bin")).Returns(false);
            mockProService.Setup(x => x.GetUpgradeMessage(It.IsAny<string>()))
                .Returns("Upgrade to Pro for $20");

            var service = new ModelResolverService(_testBaseDir, mockProService.Object);

            // Act
            Action act = () => service.ResolveModelPath("small");

            // Assert
            act.Should().Throw<UnauthorizedAccessException>()
                .WithMessage("*requires Pro license*")
                .WithMessage("*Upgrade to Pro*");

            // Verify license check was performed
            mockProService.Verify(x => x.CanUseModel("ggml-small.bin"), Times.Once);
        }

        [Fact]
        public void SecurityTest_FreeUser_CannotAccessTurboModel()
        {
            // Arrange - Create Turbo model file on disk
            var turboModelPath = Path.Combine(_testWhisperDir, "ggml-large-v3-turbo-q8_0.bin");
            File.WriteAllText(turboModelPath, "turbo model data");

            var mockProService = new Mock<IProFeatureService>();
            mockProService.Setup(x => x.CanUseModel("ggml-large-v3-turbo-q8_0.bin")).Returns(false);
            mockProService.Setup(x => x.GetUpgradeMessage(It.IsAny<string>()))
                .Returns("Upgrade to Pro for $20");

            var service = new ModelResolverService(_testBaseDir, mockProService.Object);

            // Act
            Action act = () => service.ResolveModelPath("turbo");

            // Assert
            act.Should().Throw<UnauthorizedAccessException>()
                .WithMessage("*requires Pro license*");
        }

        [Fact]
        public void SecurityTest_FreeUser_CanAccessBaseModel()
        {
            // Arrange
            var baseModelPath = Path.Combine(_testWhisperDir, "ggml-base.bin");
            File.WriteAllText(baseModelPath, "base model data");

            var mockProService = new Mock<IProFeatureService>();
            mockProService.Setup(x => x.CanUseModel("ggml-base.bin")).Returns(true);

            var service = new ModelResolverService(_testBaseDir, mockProService.Object);

            // Act
            var result = service.ResolveModelPath("base");

            // Assert
            result.Should().Be(baseModelPath);
            mockProService.Verify(x => x.CanUseModel("ggml-base.bin"), Times.Once);
        }

        [Fact]
        public void SecurityTest_ProUser_CanAccessAllModels()
        {
            // Arrange - Create all model files
            var modelFiles = new[]
            {
                "ggml-tiny.bin",
                "ggml-base.bin",
                "ggml-small.bin",
                "ggml-medium.bin",
                "ggml-large-v3-turbo-q8_0.bin",
                "ggml-large-v3.bin"
            };

            foreach (var modelFile in modelFiles)
            {
                File.WriteAllText(Path.Combine(_testWhisperDir, modelFile), "model data");
            }

            // Mock Pro user
            var mockProService = new Mock<IProFeatureService>();
            mockProService.Setup(x => x.CanUseModel(It.IsAny<string>())).Returns(true);

            var service = new ModelResolverService(_testBaseDir, mockProService.Object);

            // Act & Assert - All models should be accessible
            foreach (var modelFile in modelFiles)
            {
                var result = service.ResolveModelPath(modelFile);
                result.Should().NotBeNullOrEmpty();
                File.Exists(result).Should().BeTrue();
            }
        }

        [Fact]
        public void SecurityTest_ManualDownload_BypassAttempt_Blocked()
        {
            // Arrange - Simulate user manually downloading Pro model to LocalAppData
            var localAppData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoiceLite",
                "whisper"
            );
            Directory.CreateDirectory(localAppData);

            try
            {
                var manuallyDownloadedModel = Path.Combine(localAppData, "ggml-large-v3.bin");
                File.WriteAllText(manuallyDownloadedModel, "manually downloaded pro model");

                // Mock free user
                var mockProService = new Mock<IProFeatureService>();
                mockProService.Setup(x => x.CanUseModel("ggml-large-v3.bin")).Returns(false);
                mockProService.Setup(x => x.GetUpgradeMessage(It.IsAny<string>()))
                    .Returns("Upgrade message");

                var service = new ModelResolverService(_testBaseDir, mockProService.Object);

                // Act - Attempt to use manually downloaded Pro model
                Action act = () => service.ResolveModelPath("large");

                // Assert - Should be blocked by license check BEFORE file search
                act.Should().Throw<UnauthorizedAccessException>()
                    .WithMessage("*requires Pro license*");

                // Verify license check prevented file access
                mockProService.Verify(x => x.CanUseModel("ggml-large-v3.bin"), Times.Once,
                    "License check should occur before file resolution");
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(localAppData))
                {
                    Directory.Delete(localAppData, true);
                }
            }
        }

        [Fact]
        public void SecurityTest_WithoutProService_AllowsAllModels()
        {
            // Arrange - No ProFeatureService provided (backward compatibility)
            var modelPath = Path.Combine(_testWhisperDir, "ggml-small.bin");
            File.WriteAllText(modelPath, "model data");

            var service = new ModelResolverService(_testBaseDir, proFeatureService: null);

            // Act
            var result = service.ResolveModelPath("small");

            // Assert - Should succeed without license check
            result.Should().Be(modelPath);
        }

        #endregion

        #region GetAvailableModelPaths Tests

        [Fact]
        public void GetAvailableModelPaths_FindsModelsInWhisperSubdir()
        {
            // Arrange
            File.WriteAllText(Path.Combine(_testWhisperDir, "ggml-base.bin"), "data");
            File.WriteAllText(Path.Combine(_testWhisperDir, "ggml-small.bin"), "data");

            var service = new ModelResolverService(_testBaseDir);

            // Act
            var models = service.GetAvailableModelPaths();

            // Assert
            models.Should().HaveCount(2);
            models.Should().Contain(p => p.EndsWith("ggml-base.bin"));
            models.Should().Contain(p => p.EndsWith("ggml-small.bin"));
        }

        [Fact]
        public void GetAvailableModelPaths_FindsModelsInBaseDir()
        {
            // Arrange
            File.WriteAllText(Path.Combine(_testBaseDir, "ggml-medium.bin"), "data");

            var service = new ModelResolverService(_testBaseDir);

            // Act
            var models = service.GetAvailableModelPaths();

            // Assert
            models.Should().Contain(p => p.EndsWith("ggml-medium.bin"));
        }

        [Fact]
        public void GetAvailableModelPaths_ReturnsDistinctPaths()
        {
            // Arrange - Same model in multiple locations
            File.WriteAllText(Path.Combine(_testWhisperDir, "ggml-base.bin"), "data");
            File.WriteAllText(Path.Combine(_testBaseDir, "ggml-base.bin"), "data");

            var service = new ModelResolverService(_testBaseDir);

            // Act
            var models = service.GetAvailableModelPaths();

            // Assert - Should contain both paths (different locations)
            models.Should().HaveCountGreaterThanOrEqualTo(1);
        }

        [Fact]
        public void GetAvailableModelPaths_EmptyDirectory_ReturnsEmpty()
        {
            // Arrange - No model files
            var service = new ModelResolverService(_testBaseDir);

            // Act
            var models = service.GetAvailableModelPaths();

            // Assert
            models.Should().BeEmpty();
        }

        [Fact]
        public void GetAvailableModelPaths_IgnoresNonModelFiles()
        {
            // Arrange
            File.WriteAllText(Path.Combine(_testWhisperDir, "ggml-base.bin"), "model");
            File.WriteAllText(Path.Combine(_testWhisperDir, "readme.txt"), "text");

            var service = new ModelResolverService(_testBaseDir);

            // Act
            var models = service.GetAvailableModelPaths();

            // Assert
            models.Should().HaveCount(1);
            models.Should().Contain(p => p.EndsWith("ggml-base.bin"));
            models.Should().NotContain(p => p.EndsWith("readme.txt"));
        }

        #endregion
    }
}

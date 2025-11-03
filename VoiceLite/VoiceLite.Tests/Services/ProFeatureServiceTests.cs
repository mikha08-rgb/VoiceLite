using FluentAssertions;
using Moq;
using System.Windows;
using VoiceLite.Models;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// Tests for ProFeatureService - centralized Pro feature gating
    /// Security-critical: Prevents freemium bypass vulnerabilities (v1.2.0.3 fix)
    /// </summary>
    public class ProFeatureServiceTests
    {
        #region IsProUser Tests

        [Fact]
        public void IsProUser_WhenLicenseIsActivated_ReturnsTrue()
        {
            // Arrange
            var settings = new Settings { IsProLicense = true };
            var service = new ProFeatureService(settings);

            // Act
            var result = service.IsProUser;

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsProUser_WhenLicenseNotActivated_ReturnsFalse()
        {
            // Arrange
            var settings = new Settings { IsProLicense = false };
            var service = new ProFeatureService(settings);

            // Act
            var result = service.IsProUser;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void Constructor_WithNullSettings_ThrowsArgumentNullException()
        {
            // Act
            Action act = () => new ProFeatureService(null);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("settings");
        }

        #endregion

        #region CanUseModel Tests (Security Critical)

        [Theory]
        [InlineData("ggml-tiny.bin", true)]
        [InlineData("ggml-base.bin", true)]
        [InlineData("ggml-small.bin", true)]
        [InlineData("ggml-medium.bin", true)]
        [InlineData("ggml-large-v3.bin", true)]
        public void CanUseModel_ProUser_CanUseAllModels(string modelFileName, bool expected)
        {
            // Arrange
            var settings = new Settings { IsProLicense = true };
            var service = new ProFeatureService(settings);

            // Act
            var result = service.CanUseModel(modelFileName);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void CanUseModel_FreeUser_CanOnlyUseBaseModel()
        {
            // Arrange
            var settings = new Settings { IsProLicense = false };
            var service = new ProFeatureService(settings);

            // Act & Assert
            service.CanUseModel("ggml-base.bin").Should().BeTrue("Free users can use Base model");
            service.CanUseModel("ggml-tiny.bin").Should().BeFalse("Tiny is Pro-only");
            service.CanUseModel("ggml-small.bin").Should().BeFalse("Small is Pro-only");
            service.CanUseModel("ggml-medium.bin").Should().BeFalse("Medium is Pro-only");
            service.CanUseModel("ggml-large-v3.bin").Should().BeFalse("Large is Pro-only");
        }

        [Fact]
        public void CanUseModel_CaseInsensitive_WorksCorrectly()
        {
            // Arrange
            var settings = new Settings { IsProLicense = false };
            var service = new ProFeatureService(settings);

            // Act & Assert
            service.CanUseModel("GGML-BASE.BIN").Should().BeTrue("Should be case-insensitive");
            service.CanUseModel("ggml-BASE.bin").Should().BeTrue("Should be case-insensitive");
            service.CanUseModel("GGML-SMALL.BIN").Should().BeFalse("Small is Pro-only");
        }

        [Fact]
        public void CanUseModel_NullModelName_ReturnsFalse()
        {
            // Arrange
            var settings = new Settings { IsProLicense = false };
            var service = new ProFeatureService(settings);

            // Act
            var result = service.CanUseModel(null);

            // Assert
            result.Should().BeFalse("Null model name should not be allowed");
        }

        [Fact]
        public void CanUseModel_EmptyModelName_ReturnsFalse()
        {
            // Arrange
            var settings = new Settings { IsProLicense = false };
            var service = new ProFeatureService(settings);

            // Act
            var result = service.CanUseModel(string.Empty);

            // Assert
            result.Should().BeFalse("Empty model name should not be allowed");
        }

        #endregion

        #region Visibility Tests

        [Fact]
        public void AIModelsTabVisibility_AlwaysVisible_ForAllUsers()
        {
            // Arrange
            var freeSettings = new Settings { IsProLicense = false };
            var proSettings = new Settings { IsProLicense = true };

            var freeService = new ProFeatureService(freeSettings);
            var proService = new ProFeatureService(proSettings);

            // Act & Assert
            freeService.AIModelsTabVisibility.Should().Be(Visibility.Visible, "AI Models tab visible for free users");
            proService.AIModelsTabVisibility.Should().Be(Visibility.Visible, "AI Models tab visible for Pro users");
        }

        [Fact]
        public void VoiceShortcutsTabVisibility_OnlyVisibleForProUsers()
        {
            // Arrange
            var freeSettings = new Settings { IsProLicense = false };
            var proSettings = new Settings { IsProLicense = true };

            var freeService = new ProFeatureService(freeSettings);
            var proService = new ProFeatureService(proSettings);

            // Act & Assert
            freeService.VoiceShortcutsTabVisibility.Should().Be(Visibility.Collapsed, "Hidden for free users");
            proService.VoiceShortcutsTabVisibility.Should().Be(Visibility.Visible, "Visible for Pro users");
        }

        [Fact]
        public void ExportHistoryButtonVisibility_OnlyVisibleForProUsers()
        {
            // Arrange
            var freeSettings = new Settings { IsProLicense = false };
            var proSettings = new Settings { IsProLicense = true };

            var freeService = new ProFeatureService(freeSettings);
            var proService = new ProFeatureService(proSettings);

            // Act & Assert
            freeService.ExportHistoryButtonVisibility.Should().Be(Visibility.Collapsed, "Hidden for free users");
            proService.ExportHistoryButtonVisibility.Should().Be(Visibility.Visible, "Visible for Pro users");
        }

        [Fact]
        public void CustomDictionaryTabVisibility_OnlyVisibleForProUsers()
        {
            // Arrange
            var freeSettings = new Settings { IsProLicense = false };
            var proSettings = new Settings { IsProLicense = true };

            var freeService = new ProFeatureService(freeSettings);
            var proService = new ProFeatureService(proSettings);

            // Act & Assert
            freeService.CustomDictionaryTabVisibility.Should().Be(Visibility.Collapsed, "Hidden for free users");
            proService.CustomDictionaryTabVisibility.Should().Be(Visibility.Visible, "Visible for Pro users");
        }

        [Fact]
        public void AdvancedSettingsVisibility_OnlyVisibleForProUsers()
        {
            // Arrange
            var freeSettings = new Settings { IsProLicense = false };
            var proSettings = new Settings { IsProLicense = true };

            var freeService = new ProFeatureService(freeSettings);
            var proService = new ProFeatureService(proSettings);

            // Act & Assert
            freeService.AdvancedSettingsVisibility.Should().Be(Visibility.Collapsed, "Hidden for free users");
            proService.AdvancedSettingsVisibility.Should().Be(Visibility.Visible, "Visible for Pro users");
        }

        #endregion

        #region IsModelAvailable Tests

        [Theory]
        [InlineData("tiny")]
        [InlineData("ggml-tiny.bin")]
        [InlineData("small")]
        [InlineData("ggml-small.bin")]
        [InlineData("medium")]
        [InlineData("large")]
        public void IsModelAvailable_ProUser_AllModelsAvailable(string modelName)
        {
            // Arrange
            var settings = new Settings { IsProLicense = true };
            var service = new ProFeatureService(settings);

            // Act
            var result = service.IsModelAvailable(modelName);

            // Assert
            result.Should().BeTrue($"Pro users should have access to {modelName}");
        }

        [Fact]
        public void IsModelAvailable_FreeUser_OnlyBaseModelAvailable()
        {
            // Arrange
            var settings = new Settings { IsProLicense = false };
            var service = new ProFeatureService(settings);

            // Act & Assert
            service.IsModelAvailable("base").Should().BeTrue("Base model available for free");
            service.IsModelAvailable("ggml-base.bin").Should().BeTrue("Base model available for free");
            service.IsModelAvailable("tiny").Should().BeFalse("Tiny is Pro-only");
            service.IsModelAvailable("small").Should().BeFalse("Small is Pro-only");
            service.IsModelAvailable("medium").Should().BeFalse("Medium is Pro-only");
            service.IsModelAvailable("large").Should().BeFalse("Large is Pro-only");
        }

        [Fact]
        public void IsModelAvailable_NullModelName_ReturnsFalse()
        {
            // Arrange
            var settings = new Settings { IsProLicense = false };
            var service = new ProFeatureService(settings);

            // Act
            var result = service.IsModelAvailable(null);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region GetAvailableModels Tests

        [Fact]
        public void GetAvailableModels_ProUser_ReturnsAll5Models()
        {
            // Arrange
            var settings = new Settings { IsProLicense = true };
            var service = new ProFeatureService(settings);

            // Act
            var models = service.GetAvailableModels();

            // Assert
            models.Should().HaveCount(5);
            models.Should().Contain("ggml-tiny.bin");
            models.Should().Contain("ggml-base.bin");
            models.Should().Contain("ggml-small.bin");
            models.Should().Contain("ggml-medium.bin");
            models.Should().Contain("ggml-large-v3.bin");
        }

        [Fact]
        public void GetAvailableModels_FreeUser_ReturnsOnlyBaseModel()
        {
            // Arrange
            var settings = new Settings { IsProLicense = false };
            var service = new ProFeatureService(settings);

            // Act
            var models = service.GetAvailableModels();

            // Assert
            models.Should().HaveCount(1);
            models.Should().Contain("ggml-base.bin");
            models.Should().NotContain("ggml-tiny.bin");
            models.Should().NotContain("ggml-small.bin");
            models.Should().NotContain("ggml-medium.bin");
            models.Should().NotContain("ggml-large-v3.bin");
        }

        #endregion

        #region Tier Display Tests

        [Fact]
        public void TierName_ProUser_ReturnsPro()
        {
            // Arrange
            var settings = new Settings { IsProLicense = true };
            var service = new ProFeatureService(settings);

            // Act
            var tierName = service.TierName;

            // Assert
            tierName.Should().Be("Pro ⭐");
        }

        [Fact]
        public void TierName_FreeUser_ReturnsFree()
        {
            // Arrange
            var settings = new Settings { IsProLicense = false };
            var service = new ProFeatureService(settings);

            // Act
            var tierName = service.TierName;

            // Assert
            tierName.Should().Be("Free");
        }

        [Fact]
        public void TierDescription_ProUser_ShowsProBenefits()
        {
            // Arrange
            var settings = new Settings { IsProLicense = true };
            var service = new ProFeatureService(settings);

            // Act
            var description = service.TierDescription;

            // Assert
            description.Should().Contain("Pro tier unlocked");
            description.Should().Contain("5 AI models");
        }

        [Fact]
        public void TierDescription_FreeUser_ShowsUpgradeBenefits()
        {
            // Arrange
            var settings = new Settings { IsProLicense = false };
            var service = new ProFeatureService(settings);

            // Act
            var description = service.TierDescription;

            // Assert
            description.Should().Contain("Free tier");
            description.Should().Contain("Base model");
            description.Should().Contain("Upgrade to Pro");
        }

        #endregion

        #region GetUpgradeMessage Tests

        [Fact]
        public void GetUpgradeMessage_ReturnsFormattedMessage()
        {
            // Arrange
            var settings = new Settings { IsProLicense = false };
            var service = new ProFeatureService(settings);

            // Act
            var message = service.GetUpgradeMessage("Voice Shortcuts");

            // Assert
            message.Should().Contain("Voice Shortcuts");
            message.Should().Contain("Pro feature");
            message.Should().Contain("$20");
            message.Should().Contain("one-time payment");
        }

        #endregion

        #region Security Regression Tests (v1.2.0.3)

        [Fact]
        public void SecurityTest_FreeUserCannotBypassProCheck_ByManuallyDownloadingModel()
        {
            // Arrange - Simulate user manually downloading Pro model file
            var settings = new Settings { IsProLicense = false };
            var service = new ProFeatureService(settings);

            // Act - User tries to use manually downloaded Pro model
            var canUseSmall = service.CanUseModel("ggml-small.bin");
            var canUseLarge = service.CanUseModel("ggml-large-v3.bin");

            // Assert - Should be blocked even if file exists on disk
            canUseSmall.Should().BeFalse("Free users cannot bypass Pro check by manually downloading models");
            canUseLarge.Should().BeFalse("Free users cannot bypass Pro check by manually downloading models");
        }

        [Fact]
        public void SecurityTest_FreeUserCannotAccessProFeatures_ViaUIHiding()
        {
            // Arrange
            var settings = new Settings { IsProLicense = false };
            var service = new ProFeatureService(settings);

            // Act - Check all Pro feature UI visibility
            var voiceShortcutsVisible = service.VoiceShortcutsTabVisibility;
            var exportHistoryVisible = service.ExportHistoryButtonVisibility;
            var customDictVisible = service.CustomDictionaryTabVisibility;
            var advancedVisible = service.AdvancedSettingsVisibility;

            // Assert - All Pro features should be hidden
            voiceShortcutsVisible.Should().Be(Visibility.Collapsed);
            exportHistoryVisible.Should().Be(Visibility.Collapsed);
            customDictVisible.Should().Be(Visibility.Collapsed);
            advancedVisible.Should().Be(Visibility.Collapsed);
        }

        [Fact]
        public void SecurityTest_LicenseStatusChange_ReflectsImmediately()
        {
            // Arrange - Start as free user
            var settings = new Settings { IsProLicense = false };
            var service = new ProFeatureService(settings);

            service.IsProUser.Should().BeFalse("Initial state: free user");
            service.CanUseModel("ggml-small.bin").Should().BeFalse("Cannot use Pro models initially");

            // Act - Activate Pro license
            settings.IsProLicense = true;

            // Assert - Pro features should be immediately available
            service.IsProUser.Should().BeTrue("License activated");
            service.CanUseModel("ggml-small.bin").Should().BeTrue("Can now use Pro models");
            service.VoiceShortcutsTabVisibility.Should().Be(Visibility.Visible, "Pro features now visible");
        }

        #endregion
    }
}

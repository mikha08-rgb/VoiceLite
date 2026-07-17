using AwesomeAssertions;
using System.Windows;
using VoiceLite.Models;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// Tests for ProFeatureService - centralized Pro feature gating.
    /// Post-Parakeet (v2.0) the model-gating methods are intentional no-ops (single model),
    /// so coverage here targets what Pro actually gates today: UI visibility and tier display.
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

        #region Security Regression Tests

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
            // Settings is shared by reference; LicenseService mutates IsProLicense directly
            // on activation, so the service must reflect the new state on the next read.
            var settings = new Settings { IsProLicense = false };
            var service = new ProFeatureService(settings);

            service.IsProUser.Should().BeFalse("Initial state: free user");
            service.VoiceShortcutsTabVisibility.Should().Be(Visibility.Collapsed, "Pro features hidden initially");

            // Act - Activate Pro license
            settings.IsProLicense = true;

            // Assert - Pro features should be immediately available
            service.IsProUser.Should().BeTrue("License activated");
            service.VoiceShortcutsTabVisibility.Should().Be(Visibility.Visible, "Pro features now visible");
        }

        #endregion
    }
}

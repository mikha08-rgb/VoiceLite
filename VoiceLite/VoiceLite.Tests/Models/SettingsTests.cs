using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows.Input;
using FluentAssertions;
using VoiceLite.Models;
using Xunit;

namespace VoiceLite.Tests.Models
{
    [Trait("Category", "Unit")]
    public class SettingsTests
    {
        #region Constructor and Default Values

        [Fact]
        public void Constructor_SetsDefaultValues()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.Mode.Should().Be(RecordMode.PushToTalk);
            settings.TextInjectionMode.Should().Be(TextInjectionMode.SmartAuto);
            settings.RecordHotkey.Should().Be(Key.LeftAlt);
            settings.HotkeyModifiers.Should().Be(ModifierKeys.None);
            settings.WhisperModel.Should().Be("ggml-base.bin"); // UPDATED: Changed default to Base model
            settings.BeamSize.Should().Be(1);
            settings.BestOf.Should().Be(1);
            settings.WhisperTimeoutMultiplier.Should().Be(2.0);
        }

        [Fact]
        public void SyncRoot_IsNotNull()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.SyncRoot.Should().NotBeNull();
        }

        #endregion

        #region RecordMode Tests

        [Fact]
        public void RecordMode_DefaultValue_IsPushToTalk()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.Mode.Should().Be(RecordMode.PushToTalk);
        }

        [Fact]
        public void RecordMode_SetToToggle_Updates()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.Mode = RecordMode.Toggle;

            // Assert
            settings.Mode.Should().Be(RecordMode.Toggle);
        }

        [Fact]
        public void RecordMode_InvalidValue_DefaultsToPushToTalk()
        {
            // Arrange
            var settings = new Settings();

            // Act - Set to invalid enum value (cast from int)
            settings.Mode = (RecordMode)999;

            // Assert
            settings.Mode.Should().Be(RecordMode.PushToTalk);
        }

        #endregion

        #region TextInjectionMode Tests

        [Fact]
        public void TextInjectionMode_DefaultValue_IsSmartAuto()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.TextInjectionMode.Should().Be(TextInjectionMode.SmartAuto);
        }

        [Fact]
        public void TextInjectionMode_AllEnumValues_Supported()
        {
            // Arrange
            var settings = new Settings();

            // Act & Assert
            settings.TextInjectionMode = TextInjectionMode.AlwaysType;
            settings.TextInjectionMode.Should().Be(TextInjectionMode.AlwaysType);

            settings.TextInjectionMode = TextInjectionMode.AlwaysPaste;
            settings.TextInjectionMode.Should().Be(TextInjectionMode.AlwaysPaste);

            settings.TextInjectionMode = TextInjectionMode.PreferType;
            settings.TextInjectionMode.Should().Be(TextInjectionMode.PreferType);

            settings.TextInjectionMode = TextInjectionMode.PreferPaste;
            settings.TextInjectionMode.Should().Be(TextInjectionMode.PreferPaste);
        }

        [Fact]
        public void TextInjectionMode_InvalidValue_DefaultsToSmartAuto()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.TextInjectionMode = (TextInjectionMode)999;

            // Assert
            settings.TextInjectionMode.Should().Be(TextInjectionMode.SmartAuto);
        }

        #endregion

        #region UIPreset Tests

        [Fact]
        public void UIPreset_DefaultValue_IsCompact()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.UIPreset.Should().Be(UIPreset.Compact);
        }

        [Fact]
        public void UIPreset_IsReadOnly()
        {
            // Arrange
            var settings = new Settings();

            // Act & Assert - UIPreset is readonly (get-only property)
            settings.UIPreset.Should().Be(UIPreset.Compact);
        }

        #endregion

        #region Hotkey Tests

        [Fact]
        public void RecordHotkey_DefaultValue_IsLeftAlt()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.RecordHotkey.Should().Be(Key.LeftAlt);
        }

        [Fact]
        public void RecordHotkey_SetToCustomKey_Updates()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.RecordHotkey = Key.F1;

            // Assert
            settings.RecordHotkey.Should().Be(Key.F1);
        }

        [Fact]
        public void RecordHotkey_InvalidValue_DefaultsToLeftAlt()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.RecordHotkey = (Key)99999;

            // Assert
            settings.RecordHotkey.Should().Be(Key.LeftAlt);
        }

        [Fact]
        public void HotkeyModifiers_DefaultValue_IsNone()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.HotkeyModifiers.Should().Be(ModifierKeys.None);
        }

        [Fact]
        public void HotkeyModifiers_SetToControl_Updates()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.HotkeyModifiers = ModifierKeys.Control;

            // Assert
            settings.HotkeyModifiers.Should().Be(ModifierKeys.Control);
        }

        #endregion

        #region Whisper Model Tests

        [Fact]
        public void WhisperModel_DefaultValue_IsBase()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.WhisperModel.Should().Be("ggml-base.bin"); // UPDATED: Changed default to Base model
        }

        [Fact]
        public void WhisperModel_SetToOtherModel_Updates()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.WhisperModel = "ggml-medium.bin";

            // Assert
            settings.WhisperModel.Should().Be("ggml-medium.bin");
        }

        [Fact]
        public void WhisperModel_EmptyString_DefaultsToBase()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.WhisperModel = "";

            // Assert
            settings.WhisperModel.Should().Be("ggml-base.bin"); // UPDATED: Changed default to Base model
        }

        [Fact]
        public void WhisperModel_WhitespaceString_DefaultsToBase()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.WhisperModel = "   ";

            // Assert
            settings.WhisperModel.Should().Be("ggml-base.bin"); // UPDATED: Changed default to Base model
        }

        #endregion

        #region BeamSize and BestOf Tests

        [Fact]
        public void BeamSize_DefaultValue_Is1()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.BeamSize.Should().Be(1);
        }

        [Fact]
        public void BeamSize_SetToValidValue_Updates()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.BeamSize = 5;

            // Assert
            settings.BeamSize.Should().Be(5);
        }

        [Fact]
        public void BeamSize_BelowMinimum_ClampsTo1()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.BeamSize = 0;

            // Assert
            settings.BeamSize.Should().Be(1);
        }

        [Fact]
        public void BeamSize_AboveMaximum_ClampsTo10()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.BeamSize = 15;

            // Assert
            settings.BeamSize.Should().Be(10);
        }

        [Fact]
        public void BestOf_DefaultValue_Is1()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.BestOf.Should().Be(1);
        }

        [Fact]
        public void BestOf_SetToValidValue_Updates()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.BestOf = 3;

            // Assert
            settings.BestOf.Should().Be(3);
        }

        [Fact]
        public void BestOf_BelowMinimum_ClampsTo1()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.BestOf = -5;

            // Assert
            settings.BestOf.Should().Be(1);
        }

        [Fact]
        public void BestOf_AboveMaximum_ClampsTo10()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.BestOf = 20;

            // Assert
            settings.BestOf.Should().Be(10);
        }

        #endregion

        #region WhisperTimeoutMultiplier Tests

        [Fact]
        public void WhisperTimeoutMultiplier_DefaultValue_Is2_0()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.WhisperTimeoutMultiplier.Should().Be(2.0);
        }

        [Fact]
        public void WhisperTimeoutMultiplier_SetToValidValue_Updates()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.WhisperTimeoutMultiplier = 5.0;

            // Assert
            settings.WhisperTimeoutMultiplier.Should().Be(5.0);
        }

        [Fact]
        public void WhisperTimeoutMultiplier_BelowMinimum_ClampsTo0_5()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.WhisperTimeoutMultiplier = 0.1;

            // Assert
            settings.WhisperTimeoutMultiplier.Should().Be(0.5);
        }

        [Fact]
        public void WhisperTimeoutMultiplier_AboveMaximum_ClampsTo10_0()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.WhisperTimeoutMultiplier = 15.0;

            // Assert
            settings.WhisperTimeoutMultiplier.Should().Be(10.0);
        }

        #endregion

        #region Boolean Properties Tests

        [Fact]
        public void AutoPaste_DefaultValue_IsTrue()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.AutoPaste.Should().BeTrue();
        }

        [Fact]
        public void PlaySoundFeedback_DefaultValue_IsTrue()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.PlaySoundFeedback.Should().BeTrue(); // UPDATED: Enabled by default for better UX
        }

        [Fact]
        public void EnableNoiseSuppression_DefaultValue_IsFalse()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.EnableNoiseSuppression.Should().BeFalse();
        }

        [Fact]
        public void EnableAutomaticGain_DefaultValue_IsFalse()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.EnableAutomaticGain.Should().BeFalse();
        }

        [Fact]
        public void StartWithWindows_DefaultValue_IsFalse()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.StartWithWindows.Should().BeFalse();
        }

        [Fact]
        public void ShowTrayIcon_DefaultValue_IsTrue()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.ShowTrayIcon.Should().BeTrue();
        }

        [Fact]
        public void MinimizeToTray_DefaultValue_IsTrue()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.MinimizeToTray.Should().BeTrue();
        }

        [Fact]
        public void EnableHistory_DefaultValue_IsTrue()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.EnableHistory.Should().BeTrue();
        }

        [Fact]
        public void ShowHistoryPanel_DefaultValue_IsTrue()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.ShowHistoryPanel.Should().BeTrue();
        }

        [Fact]
        public void UseTemperatureOptimization_DefaultValue_IsTrue()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.UseTemperatureOptimization.Should().BeTrue();
        }

        [Fact]
        public void UseVAD_DefaultValue_IsTrue()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.UseVAD.Should().BeTrue();
        }

        #endregion

        #region String and Int Properties Tests

        [Fact]
        public void Language_DefaultValue_IsEnglish()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.Language.Should().Be("en");
        }

        [Fact]
        public void SelectedMicrophoneIndex_DefaultValue_IsMinus1()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.SelectedMicrophoneIndex.Should().Be(-1);
        }

        [Fact]
        public void MaxHistoryItems_DefaultValue_Is50()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.MaxHistoryItems.Should().Be(50);
        }

        [Fact]
        public void MaxHistoryItems_SetToValidValue_Updates()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.MaxHistoryItems = 100;

            // Assert
            settings.MaxHistoryItems.Should().Be(100);
        }

        [Fact]
        public void MaxHistoryItems_BelowMinimum_ClampsTo1()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.MaxHistoryItems = 0;

            // Assert
            settings.MaxHistoryItems.Should().Be(1);
        }

        [Fact]
        public void MaxHistoryItems_AboveMaximum_ClampsTo250()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.MaxHistoryItems = 1000;

            // Assert
            settings.MaxHistoryItems.Should().Be(250);
        }

        [Fact]
        public void HistoryPanelWidth_DefaultValue_Is280()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.HistoryPanelWidth.Should().Be(280);
        }

        [Fact]
        public void TranscriptionHistory_DefaultValue_IsEmptyList()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.TranscriptionHistory.Should().NotBeNull();
            settings.TranscriptionHistory.Should().BeEmpty();
        }

        #endregion

        #region Clamped Float/Double Properties Tests

        [Fact]
        public void TargetRmsLevel_DefaultValue_Is0_2()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.TargetRmsLevel.Should().Be(0.2f);
        }

        [Fact]
        public void TargetRmsLevel_BelowMinimum_ClampsTo0_05()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.TargetRmsLevel = 0.01f;

            // Assert
            settings.TargetRmsLevel.Should().Be(0.05f);
        }

        [Fact]
        public void TargetRmsLevel_AboveMaximum_ClampsTo0_95()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.TargetRmsLevel = 1.0f;

            // Assert
            settings.TargetRmsLevel.Should().Be(0.95f);
        }

        [Fact]
        public void NoiseGateThreshold_DefaultValue_Is0_005()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.NoiseGateThreshold.Should().Be(0.005);
        }

        [Fact]
        public void NoiseGateThreshold_BelowMinimum_ClampsTo0_001()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.NoiseGateThreshold = 0.0001;

            // Assert
            settings.NoiseGateThreshold.Should().Be(0.001);
        }

        [Fact]
        public void NoiseGateThreshold_AboveMaximum_ClampsTo0_5()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.NoiseGateThreshold = 1.0;

            // Assert
            settings.NoiseGateThreshold.Should().Be(0.5);
        }

        [Fact]
        public void WhisperTemperature_DefaultValue_Is0_2()
        {
            // Arrange & Act
            var settings = new Settings();

            // Assert
            settings.WhisperTemperature.Should().Be(0.2f);
        }

        [Fact]
        public void WhisperTemperature_BelowMinimum_ClampsTo0_0()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.WhisperTemperature = -0.5f;

            // Assert
            settings.WhisperTemperature.Should().Be(0.0f);
        }

        [Fact]
        public void WhisperTemperature_AboveMaximum_ClampsTo2_0()
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.WhisperTemperature = 3.0f;

            // Assert
            settings.WhisperTemperature.Should().Be(2.0f);
        }

        #endregion

        #region JSON Serialization Tests

        [Fact]
        public void Settings_SerializeToJson_DeserializesCorrectly()
        {
            // Arrange
            var originalSettings = new Settings
            {
                Mode = RecordMode.Toggle,
                TextInjectionMode = TextInjectionMode.AlwaysPaste,
                RecordHotkey = Key.F5,
                WhisperModel = "ggml-medium.bin",
                BeamSize = 3,
                AutoPaste = false,
                Language = "es"
            };

            // Act
            var json = JsonSerializer.Serialize(originalSettings);
            var deserializedSettings = JsonSerializer.Deserialize<Settings>(json);

            // Assert
            deserializedSettings.Should().NotBeNull();
            deserializedSettings!.Mode.Should().Be(RecordMode.Toggle);
            deserializedSettings.TextInjectionMode.Should().Be(TextInjectionMode.AlwaysPaste);
            deserializedSettings.RecordHotkey.Should().Be(Key.F5);
            deserializedSettings.WhisperModel.Should().Be("ggml-medium.bin");
            deserializedSettings.BeamSize.Should().Be(3);
            deserializedSettings.AutoPaste.Should().BeFalse();
            deserializedSettings.Language.Should().Be("es");
        }

        [Fact]
        public void Settings_WithAllPropertiesSet_RoundTripsCorrectly()
        {
            // Arrange
            var settings = new Settings
            {
                Mode = RecordMode.Toggle,
                TextInjectionMode = TextInjectionMode.PreferType,
                RecordHotkey = Key.F1,
                HotkeyModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                WhisperModel = "ggml-large-v3.bin",
                BeamSize = 5,
                BestOf = 5,
                WhisperTimeoutMultiplier = 3.0,
                EnableNoiseSuppression = true,
                EnableAutomaticGain = true,
                StartWithWindows = true,
                PlaySoundFeedback = true,
                Language = "fr",
                SelectedMicrophoneIndex = 2,
                AutoPaste = false,
                MaxHistoryItems = 200,
                EnableHistory = false
            };

            // Act
            var json = JsonSerializer.Serialize(settings);
            var restored = JsonSerializer.Deserialize<Settings>(json);

            // Assert
            restored.Should().NotBeNull();
            restored!.BeamSize.Should().Be(5);
            restored.EnableNoiseSuppression.Should().BeTrue();
            restored.Language.Should().Be("fr");
            restored.MaxHistoryItems.Should().Be(200);
        }

        #endregion

        #region SettingsValidator Tests

        [Fact]
        public void SettingsValidator_NullSettings_ReturnsNewInstance()
        {
            // Arrange & Act
            var result = SettingsValidator.ValidateAndRepair(null);

            // Assert
            result.Should().NotBeNull();
            result.Language.Should().Be("en");
        }

        [Fact]
        public void SettingsValidator_InvalidLanguage_FixesToEnglish()
        {
            // Arrange
            var settings = new Settings { Language = "invalid-lang" };

            // Act
            var result = SettingsValidator.ValidateAndRepair(settings);

            // Assert
            result.Language.Should().Be("en");
        }

        [Fact]
        public void SettingsValidator_ValidLanguage_Preserved()
        {
            // Arrange
            var settings = new Settings { Language = "es" };

            // Act
            var result = SettingsValidator.ValidateAndRepair(settings);

            // Assert
            result.Language.Should().Be("es");
        }

        [Fact]
        public void SettingsValidator_NegativeMicrophoneIndex_FixesToMinus1()
        {
            // Arrange
            var settings = new Settings { SelectedMicrophoneIndex = -10 };

            // Act
            var result = SettingsValidator.ValidateAndRepair(settings);

            // Assert
            result.SelectedMicrophoneIndex.Should().Be(-1);
        }

        [Fact]
        public void SettingsValidator_ValidMicrophoneIndex_Preserved()
        {
            // Arrange
            var settings = new Settings { SelectedMicrophoneIndex = 3 };

            // Act
            var result = SettingsValidator.ValidateAndRepair(settings);

            // Assert
            result.SelectedMicrophoneIndex.Should().Be(3);
        }

        [Fact]
        public void SettingsValidator_RevalidatesClampedProperties()
        {
            // Arrange
            var settings = new Settings
            {
                BeamSize = 15,  // Should clamp to 10
                BestOf = -5,    // Should clamp to 1
                WhisperTimeoutMultiplier = 0.1  // Should clamp to 0.5
            };

            // Act
            var result = SettingsValidator.ValidateAndRepair(settings);

            // Assert
            result.BeamSize.Should().Be(10);
            result.BestOf.Should().Be(1);
            result.WhisperTimeoutMultiplier.Should().Be(0.5);
        }

        #endregion

        #region License Properties Tests (Phase 2: TEST-001 Coverage)

        // TODO: These tests are disabled - Settings model no longer contains license properties
        // License data moved to SimpleLicenseStorage in v1.0.68 refactor
        // Need to create new tests for SimpleLicenseStorage class
        // See VoiceLite/Services/SimpleLicenseStorage.cs

        // [Fact]
        // public void LicenseKey_DefaultValue_IsNull() - DISABLED (property removed from Settings)

        // [Fact]
        // public void LicenseIsValid_DefaultValue_IsFalse() - DISABLED (property removed from Settings)

        // [Fact]
        // public void LicenseValidatedAt_DefaultValue_IsNull() - DISABLED (property removed from Settings)

        // [Fact]
        // public void LicenseKey_SetToValidFormat_Updates() - DISABLED (property removed from Settings)

        // [Fact]
        // public void LicenseProperties_CanBeSetTogether() - DISABLED (property removed from Settings)

        // [Fact]
        // public void LicenseKey_Serialization_PreservesValue() - DISABLED (property removed from Settings)

        // [Fact]
        // public void LicenseValidatedAt_Serialization_PreservesTimestamp() - DISABLED (property removed from Settings)

        // [Fact]
        // public void LicenseKey_NullValue_SerializesAndDeserializesCorrectly() - DISABLED (property removed from Settings)

        // [Fact]
        // public void Settings_BackwardCompatibility_LoadsWithoutLicenseFields() - DISABLED (property removed from Settings)

        // [Fact]
        // public void SettingsValidator_LicenseProperties_ArePreserved() - DISABLED (property removed from Settings)

        #endregion
    }
}

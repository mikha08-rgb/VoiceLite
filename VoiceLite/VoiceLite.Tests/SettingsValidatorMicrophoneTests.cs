using AwesomeAssertions;
using VoiceLite.Models;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// v2.5.2 hardening (F1c): v2.4.1 saved SelectedMicrophoneName = "Default Microphone"
    /// together with index -1 on every Save. The v2.5.x recorder resolves by name first,
    /// finds no such device, records from the default (correct) and logs a Release-visible
    /// "saved microphone 'Default Microphone' not found" warning on EVERY recording, burying
    /// genuine mic-not-found reports. Repaired on load, not in the recorder.
    /// </summary>
    public class SettingsValidatorMicrophoneTests
    {
        [Fact]
        public void DefaultDevice_WithLegacyPlaceholderName_LosesTheName()
        {
            var repaired = SettingsValidator.ValidateAndRepair(new Settings
            {
                SelectedMicrophoneIndex = -1,
                SelectedMicrophoneName = "Default Microphone",
            });

            repaired.SelectedMicrophoneIndex.Should().Be(-1);
            repaired.SelectedMicrophoneName.Should().BeNull(
                "index -1 means 'default device'; a name there only makes the recorder warn on every recording");
        }

        [Fact]
        public void RealDevice_KeepsIndexAndName()
        {
            var repaired = SettingsValidator.ValidateAndRepair(new Settings
            {
                SelectedMicrophoneIndex = 2,
                SelectedMicrophoneName = "USB Mic",
            });

            repaired.SelectedMicrophoneIndex.Should().Be(2);
            repaired.SelectedMicrophoneName.Should().Be("USB Mic");
        }

        [Fact]
        public void LegacyIndexOnly_IsUntouched()
        {
            var repaired = SettingsValidator.ValidateAndRepair(new Settings { SelectedMicrophoneIndex = 1 });

            repaired.SelectedMicrophoneIndex.Should().Be(1);
            repaired.SelectedMicrophoneName.Should().BeNull();
        }

        [Fact]
        public void CorruptNegativeIndex_BecomesTheDefaultDevice_AndLosesTheName()
        {
            // Intentional: a hand-edited/corrupt index below -1 is clamped to "default", and
            // default never carries a name. The Settings window already shows "Default
            // microphone" for this shape, so the recorder now agrees with what the UI shows.
            var repaired = SettingsValidator.ValidateAndRepair(new Settings
            {
                SelectedMicrophoneIndex = -5,
                SelectedMicrophoneName = "USB Mic",
            });

            repaired.SelectedMicrophoneIndex.Should().Be(-1);
            repaired.SelectedMicrophoneName.Should().BeNull();
        }
    }
}

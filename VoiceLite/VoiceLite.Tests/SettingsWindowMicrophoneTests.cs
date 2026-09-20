using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using VoiceLite.Models;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// v2.5.2 hardening (F1a): the Settings window must never persist a microphone the user
    /// did not choose. It used to fall back from the saved NAME to the saved INDEX, so with
    /// the saved USB mic unplugged it pre-selected whatever now sat at the stale index
    /// (a webcam, a dead line-in) and the next Save of ANY setting persisted that device by
    /// name - the "empty transcriptions" failure class the recorder's name-first resolution
    /// was written to stop. These statics are the window's production mic logic.
    /// </summary>
    public class SettingsWindowMicrophoneTests
    {
        private static List<AudioDevice> Devices(params string[] names) =>
            names.Select((n, i) => new AudioDevice { Index = i, Name = n }).ToList();

        // ---- PickSavedMicrophone: same resolution the recorder uses ----

        [Fact]
        public void PickSavedMicrophone_NameGone_ReturnsNull_NotWhateverSitsAtTheStaleIndex()
        {
            // Saved: index 1 "USB Mic". USB unplugged -> the webcam slid into index 1.
            var devices = Devices("Realtek", "Webcam");

            SettingsWindow.PickSavedMicrophone(devices, 1, "USB Mic").Should().BeNull(
                "a saved name that is gone must never resolve to the device now at the stale index");
        }

        [Fact]
        public void PickSavedMicrophone_NameFound_IgnoresTheStaleIndex()
        {
            var devices = Devices("Realtek", "Webcam", "USB Mic");

            SettingsWindow.PickSavedMicrophone(devices, 1, "USB Mic")!.Index.Should().Be(2);
        }

        [Fact]
        public void PickSavedMicrophone_LegacyIndexOnlySettings_UseTheIndex()
        {
            var devices = Devices("Realtek", "Webcam");

            SettingsWindow.PickSavedMicrophone(devices, 1, null)!.Name.Should().Be("Webcam");
            SettingsWindow.PickSavedMicrophone(devices, 1, "")!.Name.Should().Be("Webcam");
            SettingsWindow.PickSavedMicrophone(devices, 7, null).Should().BeNull("out of range");
        }

        [Fact]
        public void PickSavedMicrophone_DefaultDevice_ReturnsNull()
        {
            SettingsWindow.PickSavedMicrophone(Devices("Realtek"), -1, null).Should().BeNull();
        }

        [Theory]
        [InlineData(1, "USB Mic")]   // name gone, a device sits at the stale index
        [InlineData(5, "USB Mic")]   // name gone, index out of range
        [InlineData(0, "Webcam")]    // name found at a different index
        [InlineData(1, "Webcam")]    // name found at the saved index
        [InlineData(1, null)]        // legacy index-only
        [InlineData(9, null)]        // legacy index-only, out of range
        [InlineData(-1, null)]       // default
        public void PickSavedMicrophone_AgreesWithTheRecorder(int savedIndex, string? savedName)
        {
            // Locks the window to AudioRecorder.ResolveDeviceIndex: "not found" in the window
            // means "Default microphone", which the recorder records as device 0.
            // (Index -1 WITH a name is excluded: SettingsValidator clears that shape.)
            var devices = Devices("Realtek", "Webcam");

            var picked = SettingsWindow.PickSavedMicrophone(devices, savedIndex, savedName);

            (picked?.Index ?? 0).Should().Be(AudioRecorder.ResolveDeviceIndex(devices, savedIndex, savedName));
        }

        // ---- BuildMicrophoneChoices: what the combo shows ----

        [Fact]
        public void SavedMicUnplugged_ShowsANotConnectedPlaceholder_NotADifferentDevice()
        {
            var choices = SettingsWindow.BuildMicrophoneChoices(
                Devices("Realtek", "Webcam"), 1, "USB Mic", out var selected, out var missing);

            missing.Should().NotBeNull("the saved mic is not connected and the UI must say so");
            selected.Should().BeSameAs(missing);
            selected.Name.Should().Contain("USB Mic").And.Contain("not connected");
            choices.Should().Contain(missing!);
            choices[0].Index.Should().Be(-1, "'Default microphone' stays first and stays selectable");
            choices.Where(d => !ReferenceEquals(d, missing)).Select(d => d.Name)
                .Should().Equal("Default microphone", "Realtek", "Webcam");
        }

        [Fact]
        public void SavedMicConnected_IsSelected_AndThereIsNoPlaceholder()
        {
            SettingsWindow.BuildMicrophoneChoices(
                Devices("Realtek", "USB Mic"), 5, "USB Mic", out var selected, out var missing);

            missing.Should().BeNull();
            selected.Index.Should().Be(1);
            selected.Name.Should().Be("USB Mic");
        }

        [Fact]
        public void DefaultSaved_SelectsDefault_AndThereIsNoPlaceholder()
        {
            var choices = SettingsWindow.BuildMicrophoneChoices(
                Devices("Realtek"), -1, null, out var selected, out var missing);

            missing.Should().BeNull();
            selected.Should().BeSameAs(choices[0]);
            selected.Index.Should().Be(-1);
        }

        [Fact]
        public void RefreshAfterUnplug_TurnsTheSelectedMicIntoThePlaceholder()
        {
            // Second entry point into the same bug: Settings open with the USB mic selected,
            // user unplugs it and clicks Refresh. The handler writes the selection to the
            // draft, then rebuilds the list from the draft.
            var draft = new Settings();
            SettingsWindow.WriteMicrophoneToDraft(draft, new AudioDevice { Index = 1, Name = "USB Mic" }, null);

            SettingsWindow.BuildMicrophoneChoices(
                Devices("Realtek", "Webcam"), draft.SelectedMicrophoneIndex, draft.SelectedMicrophoneName,
                out var selected, out var missing);

            selected.Should().BeSameAs(missing, "the webcam now at index 1 must not be selected");
        }

        // ---- WriteMicrophoneToDraft: what Save/Apply persists ----

        [Fact]
        public void SavingWithThePlaceholderSelected_LeavesTheSavedMicUntouched()
        {
            // Mic unplugged, user opens Settings to change something unrelated and saves:
            // the preference must survive so the mic is used again when it is plugged back in.
            var draft = new Settings { SelectedMicrophoneIndex = 2, SelectedMicrophoneName = "USB Mic" };
            SettingsWindow.BuildMicrophoneChoices(
                Devices("Realtek", "Webcam", "Line In"), 2, "USB Mic", out var selected, out var missing);

            SettingsWindow.WriteMicrophoneToDraft(draft, selected, missing);

            draft.SelectedMicrophoneIndex.Should().Be(2);
            draft.SelectedMicrophoneName.Should().Be("USB Mic");
        }

        [Fact]
        public void ExplicitlyChoosingDefault_WhileTheSavedMicIsUnplugged_IsPersisted()
        {
            var draft = new Settings { SelectedMicrophoneIndex = 2, SelectedMicrophoneName = "USB Mic" };
            var choices = SettingsWindow.BuildMicrophoneChoices(
                Devices("Realtek"), 2, "USB Mic", out _, out var missing);

            SettingsWindow.WriteMicrophoneToDraft(draft, choices[0], missing);

            draft.SelectedMicrophoneIndex.Should().Be(-1);
            draft.SelectedMicrophoneName.Should().BeNull();
        }

        [Fact]
        public void ChoosingARealDevice_PersistsIndexAndName()
        {
            var draft = new Settings();

            SettingsWindow.WriteMicrophoneToDraft(draft, new AudioDevice { Index = 1, Name = "Webcam" }, null);

            draft.SelectedMicrophoneIndex.Should().Be(1);
            draft.SelectedMicrophoneName.Should().Be("Webcam");
        }
    }
}

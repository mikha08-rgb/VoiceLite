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

        // F1(b): WaveIn names are truncated to 31 chars, so two mics can share one name. The
        // saved index breaks the tie - but ONLY among devices that already match the name.
        [Theory]
        [InlineData(2, "Twin", 2)]     // second twin saved -> second twin (was: always the first)
        [InlineData(0, "Twin", 0)]     // first twin saved
        [InlineData(1, "Twin", 0)]     // index points at a different-named device -> first by name
        [InlineData(5, "Twin", 0)]     // index out of range -> first by name
        [InlineData(2, "Webcam", 1)]   // unique name, index points at a twin -> the name wins
        public void PickSavedMicrophone_SameNamedTwins_IndexOnlyBreaksTheTie_AndAgreesWithTheRecorder(
            int savedIndex, string savedName, int expectedIndex)
        {
            var devices = Devices("Twin", "Webcam", "Twin");

            var picked = SettingsWindow.PickSavedMicrophone(devices, savedIndex, savedName);

            picked!.Index.Should().Be(expectedIndex);
            AudioRecorder.ResolveDeviceIndex(devices, savedIndex, savedName).Should().Be(expectedIndex,
                "Settings (and its level meter) must show the same twin the recorder records from");
        }

        [Theory]
        [InlineData(0)]   // stale index now points at the webcam
        [InlineData(2)]   // stale index now out of range
        public void PickSavedMicrophone_OneTwinUnplugged_PicksTheRemainingTwinByName(int savedIndex)
        {
            var devices = Devices("Webcam", "Twin");

            SettingsWindow.PickSavedMicrophone(devices, savedIndex, "Twin")!.Index.Should().Be(1);
            AudioRecorder.ResolveDeviceIndex(devices, savedIndex, "Twin").Should().Be(1);
        }

        [Fact]
        public void PickSavedMicrophone_TwinsRenumbered_KnownLimitation_TheSavedIndexStillBreaksTheTie()
        {
            // ACCEPTED LIMITATION, pinned so nobody "fixes" one resolver only: saved (1, first
            // twin) out of [Webcam, TwinA, TwinB]; the webcam is unplugged -> [TwinA, TwinB].
            // Name + index 1 now means TwinB. WaveIn gives same-named devices no stable
            // identity (that needs MMDevice endpoint IDs), so both resolvers answer 1.
            var devices = Devices("Twin", "Twin");

            SettingsWindow.PickSavedMicrophone(devices, 1, "Twin")!.Index.Should().Be(1);
            AudioRecorder.ResolveDeviceIndex(devices, 1, "Twin").Should().Be(1);
        }

        [Theory]
        [InlineData(2, 2)]   // second twin saved -> second twin pre-selected (Save keeps it)
        [InlineData(0, 0)]
        [InlineData(1, 0)]   // index points at the webcam -> first twin by name
        public void SameNamedTwins_ThroughTheComboList_PreselectTheTwinTheRecorderUses(int savedIndex, int expectedIndex)
        {
            // Through BuildMicrophoneChoices, which prepends "Default microphone" (Index -1).
            var devices = Devices("Twin", "Webcam", "Twin");

            SettingsWindow.BuildMicrophoneChoices(devices, savedIndex, "Twin", out var selected, out var missing);

            missing.Should().BeNull("a twin is connected");
            selected.Index.Should().Be(expectedIndex);
            selected.Index.Should().Be(AudioRecorder.ResolveDeviceIndex(devices, savedIndex, "Twin"));
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

        [Theory]
        [InlineData(false)] // another real device applied in between
        [InlineData(true)]  // "Default microphone" applied in between
        public void ReselectingThePlaceholder_AfterAnApplyOfAnotherDevice_PersistsTheNotConnectedMicAgain(bool applyDefault)
        {
            // Apply does not rebuild the combo, so the placeholder stays selectable after the
            // draft has moved on. Whatever is visibly selected at Save is what must be saved.
            var live = new Settings { SelectedMicrophoneIndex = 2, SelectedMicrophoneName = "USB Mic" };
            var draft = SettingsWindow.CloneForDraft(live);
            var choices = SettingsWindow.BuildMicrophoneChoices(
                Devices("Realtek", "Webcam"), 2, "USB Mic", out _, out var missing);

            var other = applyDefault ? choices[0] : choices.Single(d => d.Name == "Webcam");
            SettingsWindow.WriteMicrophoneToDraft(draft, other, missing);
            SettingsWindow.CommitDraft(draft, live); // Apply
            live.SelectedMicrophoneName.Should().Be(applyDefault ? null : "Webcam");

            SettingsWindow.WriteMicrophoneToDraft(draft, missing!, missing); // change of mind, Save
            SettingsWindow.CommitDraft(draft, live);

            live.SelectedMicrophoneIndex.Should().Be(2);
            live.SelectedMicrophoneName.Should().Be("USB Mic", "the UI says it will be used again when plugged back in");
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

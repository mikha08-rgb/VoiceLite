using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using VoiceLite.Models;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// v2.5.2 review #14: saved mic "USB Mic" is unplugged (placeholder shown), the user tries
    /// "Webcam" and clicks Refresh. Refresh writes the current selection into the draft and
    /// rebuilds the list from the draft alone, so the "USB Mic (not connected)" item vanished
    /// for the rest of the dialog session and the real preference could not be re-selected.
    /// The window now remembers the unconnected saved microphone and keeps offering it.
    /// </summary>
    public class SettingsWindowMissingMicRefreshTests
    {
        private static List<AudioDevice> Devices(params string[] names) =>
            names.Select((n, i) => new AudioDevice { Index = i, Name = n }).ToList();

        [Fact]
        public void Refresh_AfterTryingAnotherDevice_KeepsThePlaceholder_WithoutSelectingIt()
        {
            // Draft = Webcam (connected); the dialog remembers USB Mic (absent) from when it opened.
            var choices = SettingsWindow.BuildMicrophoneChoices(Devices("Realtek", "Webcam"),
                savedIndex: 1, savedName: "Webcam", keepMissingIndex: 2, keepMissingName: "USB Mic",
                out var selected, out var placeholder);

            selected.Name.Should().Be("Webcam", "what the user picked stays selected");
            placeholder.Should().NotBeNull();
            choices.Select(d => d.Name).Should().Equal("Default microphone", "USB Mic (not connected)", "Realtek", "Webcam");
            choices[1].Should().BeSameAs(placeholder);

            // Re-selecting it restores the real preference (what is visibly selected is what gets saved).
            var draft = new Settings { SelectedMicrophoneIndex = 1, SelectedMicrophoneName = "Webcam" };
            SettingsWindow.WriteMicrophoneToDraft(draft, placeholder!, placeholder);
            draft.SelectedMicrophoneIndex.Should().Be(2);
            draft.SelectedMicrophoneName.Should().Be("USB Mic");
        }

        [Fact]
        public void Refresh_WithDefaultSelected_KeepsThePlaceholder()
        {
            var choices = SettingsWindow.BuildMicrophoneChoices(Devices("Realtek"),
                savedIndex: -1, savedName: null, keepMissingIndex: 2, keepMissingName: "USB Mic",
                out var selected, out var placeholder);

            selected.Index.Should().Be(-1);
            placeholder!.Name.Should().Be("USB Mic (not connected)");
            choices.Should().Contain(placeholder);
        }

        [Fact]
        public void Refresh_AfterTheMicIsPluggedBackIn_OffersTheRealDevice_NotAPlaceholder()
        {
            var choices = SettingsWindow.BuildMicrophoneChoices(Devices("Realtek", "Webcam", "USB Mic"),
                savedIndex: 1, savedName: "Webcam", keepMissingIndex: 2, keepMissingName: "USB Mic",
                out var selected, out var placeholder);

            placeholder.Should().BeNull();
            selected.Name.Should().Be("Webcam");
            choices.Count(d => d.Name.StartsWith("USB Mic")).Should().Be(1);
        }

        [Fact]
        public void DraftStillOnTheMissingMic_GetsExactlyOnePlaceholder_AndItIsSelected()
        {
            var choices = SettingsWindow.BuildMicrophoneChoices(Devices("Realtek", "Webcam"),
                savedIndex: 2, savedName: "USB Mic", keepMissingIndex: 2, keepMissingName: "USB Mic",
                out var selected, out var placeholder);

            choices.Count(d => d.Name == "USB Mic (not connected)").Should().Be(1);
            selected.Should().BeSameAs(placeholder);
        }

        [Fact]
        public void NothingRemembered_BehavesExactlyLikeTheOriginalOverload()
        {
            var choices = SettingsWindow.BuildMicrophoneChoices(Devices("Realtek", "Webcam"),
                savedIndex: 1, savedName: "Webcam", keepMissingIndex: -1, keepMissingName: null,
                out var selected, out var placeholder);

            placeholder.Should().BeNull();
            selected.Name.Should().Be("Webcam");
            choices.Should().HaveCount(3);
        }

        [Fact]
        public void Wiring_LoadMicrophonesRemembersTheMissingMic_AndPassesItBackOnRefresh()
        {
            var code = SettingsWindowHotkeyTests.ReadAppSource("SettingsWindow.xaml.cs");

            Assert.True(code.Contains("keptMissingMicIndex, keptMissingMicName,"),
                "LoadMicrophones must hand the remembered unconnected microphone to BuildMicrophoneChoices");
            Assert.True(code.Contains("keptMissingMicName = missingMic.Name[..^NotConnectedSuffix.Length];"),
                "LoadMicrophones must remember the saved microphone a placeholder stands for");
        }
    }
}

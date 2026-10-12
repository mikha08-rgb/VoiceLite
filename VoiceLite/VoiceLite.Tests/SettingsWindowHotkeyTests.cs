using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using AwesomeAssertions;
using VoiceLite.Models;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// v2.5.2 hardening for the Settings hotkey box.
    /// F2: every key-down writes straight into the draft (a bare modifier is a deliberate,
    ///     supported hotkey since v2.4.1), and Esc only cleared focus - so "press Ctrl, change
    ///     your mind, press Esc as the hint says, Save" made bare Left Ctrl the global hotkey.
    /// F3: the availability probe exempted the saved combination on the assumption the app
    ///     owns it, which is false after a failed startup registration.
    /// The window cannot be constructed in a unit test (its ctor reads the real license
    /// store), so the logic lives in internal statics and the wiring is pinned by source text.
    /// </summary>
    public class SettingsWindowHotkeyTests
    {
        private static Settings Live() => new Settings { RecordHotkey = Key.F1, HotkeyModifiers = ModifierKeys.Control };

        // ---- F2: Esc restores the hotkey in effect ----

        [Fact]
        public void Esc_AfterABareModifierWasCaptured_RestoresTheLiveHotkey()
        {
            var live = Live();
            var draft = SettingsWindow.CloneForDraft(live);
            // First key-down of an intended Ctrl+... combo (or an Alt+Tab away from the box).
            draft.RecordHotkey = Key.LeftCtrl;
            draft.HotkeyModifiers = ModifierKeys.None;

            SettingsWindow.RevertHotkeyToLive(draft, live);

            draft.RecordHotkey.Should().Be(Key.F1);
            draft.HotkeyModifiers.Should().Be(ModifierKeys.Control);

            SettingsWindow.CommitDraft(draft, live);
            live.RecordHotkey.Should().Be(Key.F1, "Save after Esc must leave the hotkey unchanged");
            live.HotkeyModifiers.Should().Be(ModifierKeys.Control);
        }

        [Fact]
        public void Esc_AfterAnApply_RestoresTheAppliedHotkey_NotTheDialogOpenValue()
        {
            var live = Live();
            var draft = SettingsWindow.CloneForDraft(live);
            draft.RecordHotkey = Key.F9;
            draft.HotkeyModifiers = ModifierKeys.Alt;
            SettingsWindow.CommitDraft(draft, live); // Apply

            draft.RecordHotkey = Key.LeftShift;
            draft.HotkeyModifiers = ModifierKeys.None;
            SettingsWindow.RevertHotkeyToLive(draft, live);

            draft.RecordHotkey.Should().Be(Key.F9, "'current hotkey' = the last committed one");
            draft.HotkeyModifiers.Should().Be(ModifierKeys.Alt);
        }

        [Fact]
        public void Esc_TouchesNothingButTheHotkey()
        {
            var live = Live();
            var draft = SettingsWindow.CloneForDraft(live);
            draft.Mode = RecordMode.Toggle;
            draft.AutoPaste = false;
            draft.RecordHotkey = Key.LeftAlt;
            draft.HotkeyModifiers = ModifierKeys.None;

            SettingsWindow.RevertHotkeyToLive(draft, live);

            draft.Mode.Should().Be(RecordMode.Toggle);
            draft.AutoPaste.Should().BeFalse();
        }

        [Fact]
        public void Wiring_EscBranchReverts_AndTheHintSaysSo()
        {
            var code = ReadAppSource("SettingsWindow.xaml.cs");

            Regex.IsMatch(code, @"e\.Key == Key\.Escape\)\s*\{[^}]*RevertHotkeyToLive\(settings, liveSettings\)")
                .Should().BeTrue("the Esc branch of HotkeyBox_PreviewKeyDown must restore the live hotkey");

            // The hint's promise, which the Esc branch above now keeps ("current" = the live,
            // last committed hotkey). If the wording changes, re-check it against the code.
            var xaml = ReadAppSource("SettingsWindow.xaml")!;
            Assert.True(xaml.Contains("Esc keeps the current hotkey."),
                "the capture hint changed - make sure it still describes what Esc really does");
        }

        // ---- F3: the probe exemption only applies to a hotkey the app really owns ----

        [Fact]
        public void Probe_IsSkipped_ForTheCombinationTheAppOwns()
        {
            SettingsWindow.ProbeIsExempt(Key.Space, ModifierKeys.Control, Key.Space, ModifierKeys.Control, appOwnsRegisteredHotkey: true)
                .Should().BeTrue("Windows rejects a second registration of a combination this process already holds");
        }

        [Fact]
        public void Probe_IsNotSkipped_WhenStartupRegistrationFailed()
        {
            SettingsWindow.ProbeIsExempt(Key.Space, ModifierKeys.Control, Key.Space, ModifierKeys.Control, appOwnsRegisteredHotkey: false)
                .Should().BeFalse("the app does not own the saved combination, so the probe result is the truth");
        }

        [Fact]
        public void Probe_IsNotSkipped_ForADifferentCombination()
        {
            SettingsWindow.ProbeIsExempt(Key.F9, ModifierKeys.Control, Key.Space, ModifierKeys.Control, true).Should().BeFalse();
            SettingsWindow.ProbeIsExempt(Key.Space, ModifierKeys.Alt, Key.Space, ModifierKeys.Control, true).Should().BeFalse();
        }

        [Fact]
        public void SavedHotkeyThatFailedToRegister_IsRecognisedAsDead_SoItWarnsWithoutBlockingOtherSettings()
        {
            // Startup offered "change the hotkey in Settings, or use the manual buttons": a user
            // living with the conflict must still be able to save e.g. a microphone change.
            SettingsWindow.IsSavedButDeadHotkey(Key.Space, ModifierKeys.Control, Key.Space, ModifierKeys.Control, appOwnsRegisteredHotkey: false)
                .Should().BeTrue();
        }

        [Fact]
        public void ANewlyCapturedTakenHotkey_IsNotTheDeadSavedOne_SoItStillBlocksTheSave()
        {
            SettingsWindow.IsSavedButDeadHotkey(Key.F9, ModifierKeys.Control, Key.Space, ModifierKeys.Control, false).Should().BeFalse();
            SettingsWindow.IsSavedButDeadHotkey(Key.Space, ModifierKeys.Control, Key.Space, ModifierKeys.Control, true).Should().BeFalse(
                "a hotkey the app owns is not dead");
        }

        [Fact]
        public void Wiring_MainWindowTellsSettingsWhetherTheHotkeyIsRegistered()
        {
            var code = ReadAppSource("MainWindow.xaml.cs");

            // A delegate, not a bool: Settings can be opened while startup is still initializing
            // services, and the startup registration then succeeds BEHIND the open dialog. A
            // snapshot taken at dialog-open would call the working hotkey "not active".
            // (The second argument is the Apply callback - RecordingIndicatorSettingTests owns that one.)
            Regex.IsMatch(code, @"new SettingsWindow\(settings, .*, \(\) => hotkeyManager\?\.IsRegistered == true\);")
                .Should().BeTrue("without this the window assumes the saved hotkey is registered and skips the probe");
        }

        [Fact]
        public void Wiring_RegistrationStateIsReadWhenItIsNeeded_NotSnapshottedWhenTheDialogOpens()
        {
            var code = ReadAppSource("SettingsWindow.xaml.cs");

            Assert.False(Regex.IsMatch(code, @"\bbool appOwnsRegisteredHotkey;"),
                "a bool field is a dialog-open snapshot; it goes stale when startup registration completes behind the dialog");
            Assert.True(Regex.IsMatch(code, @"bool AppOwnsRegisteredHotkey\s*=>\s*isHotkeyRegistered\(\)"),
                "the probe exemption and the dead-hotkey warning must ask MainWindow's HotkeyManager each time");
        }

        /// <summary>App source text. A missing file FAILS the calling tripwire (SourceTripwirePolicyTests).</summary>
        internal static string ReadAppSource(string fileName) => RepoSourceFile.ReadApp(fileName);
    }
}

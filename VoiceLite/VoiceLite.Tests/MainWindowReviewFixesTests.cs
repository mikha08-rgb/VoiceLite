using System;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// v2.5.2 review fixes that live in MainWindow. MainWindow cannot be constructed in a unit
    /// test (it news up audio, hotkeys, tray, the recognizer and writes the real settings), so
    /// each decision is a static helper tested here, and the few lines that wire it in are
    /// pinned at source level (TRIPWIRES - they prove the wiring exists, not that it works).
    /// </summary>
    public class MainWindowReviewFixesTests
    {
        private static string Source()
        {
            var code = SettingsWindowHotkeyTests.ReadAppSource("MainWindow.xaml.cs");
            Assert.True(code != null, "MainWindow.xaml.cs not found next to the test project - the tripwires must fail, not pass unchecked");
            return code!;
        }

        private static string MethodBody(string source, string signature)
        {
            var start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.True(start >= 0, $"MainWindow must still declare '{signature}'");
            var end = source.IndexOf("\n        }", start, StringComparison.Ordinal);
            return source[start..end];
        }

        // ------------------------------------------------------------------ #3 tray during the install wait

        [Theory]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]   // the bug: hidden + "Press <hotkey> to dictate" while the hotkey is blocked
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        public void ShouldMinimizeToTray_NeverWhileAnUpdateInstallIsPending(bool requested, bool updateInstalling, bool expected)
        {
            MainWindow.ShouldMinimizeToTray(requested, updateInstalling).Should().Be(expected);
        }

        [Fact]
        public void Wiring_XClick_DuringTheInstallWait_KeepsTheWindowAndReleasesTheCloseFlag()
        {
            var body = MethodBody(Source(), "protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)");

            var guard = body.IndexOf("if (!ShouldMinimizeToTray(true, updateInstallInProgress))", StringComparison.Ordinal);
            var tray = body.IndexOf("systemTrayManager?.MinimizeToTray();", StringComparison.Ordinal);
            Assert.True(guard >= 0, "OnClosing's tray branch must ask ShouldMinimizeToTray");
            Assert.True(tray > guard, "the install-wait guard has to come before MinimizeToTray()");

            var guarded = body[guard..tray];
            Assert.True(guarded.Contains("updateWindow?.Activate();"), "the user is shown why the window stays: the update dialog");
            Assert.True(guarded.Contains("Interlocked.Exchange(ref isClosingHandled, 0);"),
                "without releasing the one-shot flag every later X-click is refused");
            Assert.True(guarded.Contains("return;"), "the guard must not fall through into MinimizeToTray()");
        }

        [Fact]
        public void Wiring_MinimizeButton_DuringTheInstallWait_StaysAnOrdinaryTaskbarMinimize()
        {
            var body = MethodBody(Source(), "private void Window_StateChanged(object sender, EventArgs e)");

            Assert.True(body.Contains("ShouldMinimizeToTray(WindowState == WindowState.Minimized, updateInstallInProgress)"),
                "minimizing must not hide the window + announce 'Press <hotkey> to dictate' while the hotkey is blocked");
        }

        // ------------------------------------------------------------------ #9 deferred install banner

        [Fact]
        public void DescribeUpdateReady_TellsTheUserWhatToDo()
        {
            MainWindow.DescribeUpdateReady("2.5.3").Should().Be("Update 2.5.3 ready - click to install");
        }

        [Fact]
        public void Wiring_InstallDeferred_MovesTheBannerOffDownloading100Percent()
        {
            var body = MethodBody(Source(), "private void OpenUpdateWindow()");

            Assert.True(Regex.IsMatch(body, @"window\.InstallDeferred \+= \(\) =>\s*\{[^}]*UpdateBanner\.Text = DescribeUpdateReady\(info\.Version\);"),
                "a deferred install must leave the banner on a 'ready - click to install' state");
            Assert.True(body.Contains("if (!IsVisible) systemTrayManager?.ShowUpdateReady(info.Version);"),
                "the banner is invisible while the window is hidden in the tray; the balloon is the only surface there");
        }

        // ------------------------------------------------------------------ #11 dead-hotkey retry feedback

        [Fact]
        public void HotkeyOutcome_UnchangedHotkeyStillTaken_SaysItIsNotActive()
        {
            var text = MainWindow.DescribeHotkeyOutcomeAfterSettings(hotkeyChanged: false, registered: false, "Ctrl+Alt+Space");

            text.Should().Be("Hotkey Ctrl+Alt+Space is not active - another app may be using it");
        }

        [Fact]
        public void HotkeyOutcome_UnchangedHotkeyRetrySucceeded_ConfirmsIt()
        {
            MainWindow.DescribeHotkeyOutcomeAfterSettings(hotkeyChanged: false, registered: true, "Ctrl+Alt+Space")
                .Should().Be("Hotkey Ctrl+Alt+Space is active");
        }

        [Fact]
        public void HotkeyOutcome_ChangedHotkeyThatFailed_AlsoStaysVisibleAfterTheMessageBox()
        {
            MainWindow.DescribeHotkeyOutcomeAfterSettings(hotkeyChanged: true, registered: false, "F9")
                .Should().Be("Hotkey F9 is not active - another app may be using it");
        }

        [Fact]
        public void HotkeyOutcome_ChangedHotkeyThatRegistered_ReplacesAnEarlierStickyFailure()
        {
            // F9 was taken -> sticky red "Hotkey F9 is not active". The user then picks F10 and it
            // registers: null here left that red line standing although F10 was live.
            MainWindow.DescribeHotkeyOutcomeAfterSettings(hotkeyChanged: true, registered: true, "F10")
                .Should().Be("Hotkey F10 is active");
        }

        [Fact]
        public void Wiring_HotkeyOutcome_SuccessRevertsToReady_FailureStays()
        {
            var body = MethodBody(Source(), "private void ShowHotkeyOutcome(string? outcome, bool registered)");

            Assert.True(body.Contains("CancelReadyReset();") && body.Contains("if (registered) ResetToReadyLater();"),
                "a successful registration is a transient status; only the failure is sticky");
        }

        [Fact]
        public void Wiring_HotkeyOutcome_IsShownAfterTheUiRefresh_AndStillWithoutAModalNag()
        {
            var body = MethodBody(Source(), "private void SettingsButton_Click(object sender, RoutedEventArgs e)");

            var refresh = body.IndexOf("UpdateConfigDisplay();", StringComparison.Ordinal);
            var status = body.IndexOf("ShowHotkeyOutcome(", StringComparison.Ordinal);
            Assert.True(refresh >= 0 && status > refresh,
                "the outcome status must be set after UpdateUIForCurrentMode/UpdateConfigDisplay so they cannot overwrite it");
            Assert.True(body.Contains("hotkeyAttempted ? DescribeHotkeyOutcomeAfterSettings(hotkeyChanged, hotkeyActive, GetHotkeyDisplayString()) : null"),
                "only a save that actually (re)tried the registration reports an outcome - unrelated saves stay quiet");
            // The deliberate v2.5.2 decision stands: the MessageBox is for a CHANGED hotkey only.
            Assert.True(Regex.IsMatch(body, @"if \(hotkeyChanged\)\s*\{\s*MessageBox\.Show\(\$""Failed to apply new hotkey"),
                "an unchanged hotkey that is still taken must not pop a modal dialog on every Settings save");
        }

        // ------------------------------------------------------------------ #10 Minimize-to-tray reverted by the debounced save

        [Fact]
        public void Wiring_Saves_NeverTakeMinimizeToTrayFromTheHiddenCheckbox()
        {
            // The collapsed MainWindow checkbox is only synced AFTER the Settings dialog closes.
            // A save that fires while the dialog is open (Apply's own 500 ms debounce, a
            // dictation adding history, the exit flush) copied its stale value over the value
            // Apply had just committed - in memory and in settings.json.
            var source = Source();

            Assert.False(Regex.IsMatch(source, @"settings\.MinimizeToTray\s*=\s*(minimizeToTray|MinimizeCheckBox)"),
                "settings.MinimizeToTray is written by the Settings dialog only; no save path may overwrite it from the UI mirror");
            Assert.False(source.Contains("MinimizeCheckBox.IsChecked == true"),
                "nothing may READ the hidden checkbox as the source of truth (OnClosing included)");
            MethodBody(source, "protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)")
                .Should().Contain("if (settings.MinimizeToTray)");
        }

        // ------------------------------------------------------------------ #13 mic change saved during a recording

        [Fact]
        public void Wiring_ControllerPushesTheSavedMicrophone_BeforeEveryRecording()
        {
            var source = Source();

            Assert.True(source.Contains("prepareRecorder: ApplySelectedMicrophone"),
                "DictationController must be given the hook that re-applies the saved microphone before each recording");
            var body = MethodBody(source, "private void ApplySelectedMicrophone(Settings current)");
            Assert.True(body.Contains("audioRecorder?.SetDevice(index, name);"), "the hook must push the device into the recorder");
            Assert.False(source.Contains("ErrorLogger.LogMessage(\"Skipping microphone change"),
                "LogMessage is Debug-only; the skipped device change must be visible in Release logs");
        }
    }
}

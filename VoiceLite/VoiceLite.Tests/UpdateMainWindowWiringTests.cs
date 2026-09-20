using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// TRIPWIRE, not a behaviour test. MainWindow can't be constructed in a unit test (no DI; it
    /// news up audio, hotkeys, tray, the recognizer...), so the few lines that wire the updater
    /// into it are pinned at source level. What the hooks DO is covered by UpdateWindowFlowTests
    /// (block before install, unblock on failure, Settings gate) and SystemTrayUpdateBalloonTests.
    /// Still manual: a real hotkey press during a pending UAC prompt starts no recording.
    /// </summary>
    public class UpdateMainWindowWiringTests
    {
        private static string? MainWindowSource([CallerFilePath] string thisFile = "")
        {
            var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "VoiceLite", "MainWindow.xaml.cs"));
            return File.Exists(path) ? File.ReadAllText(path) : null; // binaries copied away from the source tree
        }

        private static string MethodBody(string source, string signature)
        {
            var start = source.IndexOf(signature, System.StringComparison.Ordinal);
            start.Should().BeGreaterThanOrEqualTo(0, $"MainWindow must still declare '{signature}'");
            var end = source.IndexOf("\n        }", start, System.StringComparison.Ordinal);
            return source[start..end];
        }

        [Theory]
        [InlineData("private void OnHotkeyPressed(object? sender, EventArgs e)", "dictation?.HotkeyPressed()")]
        [InlineData("private void OnHotkeyReleased(object? sender, EventArgs e)", "dictation?.HotkeyReleased()")]
        public void HotkeyForwarding_IsGatedWhileAnUpdateInstallIsInProgress(string signature, string forward)
        {
            var source = MainWindowSource();
            if (source == null) return;

            var body = MethodBody(source, signature);
            var gate = body.IndexOf("if (updateInstallInProgress) return;", System.StringComparison.Ordinal);
            var call = body.IndexOf(forward, System.StringComparison.Ordinal);

            gate.Should().BeGreaterThanOrEqualTo(0, "a hotkey press between 'begin install' and shutdown must not start a recording");
            call.Should().BeGreaterThan(gate, "the gate has to come before the call into DictationController");
        }

        [Fact]
        public void UpdateWindow_IsGivenTheHotkeyGate_AndTheSettingsCheck()
        {
            var source = MainWindowSource();
            if (source == null) return;

            source.Should().Contain("blockHotkeys: () => updateInstallInProgress = true");
            source.Should().Contain("unblockHotkeys: () => updateInstallInProgress = false",
                "a failed install must give the hotkey back");
            source.Should().Contain("isSettingsOpen: () => currentSettingsWindow?.IsVisible == true",
                "'currentSettingsWindow != null' would block every install after Settings was opened once (the field is never cleared)");
        }

        [Fact]
        public void TrayBalloon_OpensTheInAppUpdater_AndGetsNoUrl()
        {
            var source = MainWindowSource();
            if (source == null) return;

            source.Should().Contain("UpdateBalloonClicked += OnUpdateBalloonClicked");
            Regex.IsMatch(source, @"ShowUpdateAvailable\([^)]*Url").Should().BeFalse(
                "the tray manager must never be handed the raw installer URL again");
            MethodBody(source, "private void OnUpdateBalloonClicked()").Should().Contain("OpenUpdateWindow();");
        }
    }
}

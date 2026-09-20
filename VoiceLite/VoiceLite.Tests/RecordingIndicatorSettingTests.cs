using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using VoiceLite.Models;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// Settings.ShowRecordingIndicator (the bottom-centre pill) and the MainWindow wiring
    /// that the pill's "can never break dictation / can never keep the app alive" promises
    /// depend on. The Settings-window checkbox round trip is in WindowConstructionTests.
    /// </summary>
    public class RecordingIndicatorSettingTests
    {
        [Fact]
        public void DefaultsToOn()
        {
            new Settings().ShowRecordingIndicator.Should().BeTrue();
        }

        [Fact]
        public void SettingsJsonFromAnOlderVersion_WithoutTheKey_LoadsAsOn()
        {
            // What v2.5.1 wrote: no ShowRecordingIndicator key at all.
            const string json = "{\"Mode\":1,\"WhisperModel\":\"parakeet-tdt-0.6b-v3-int8\",\"AutoPaste\":false,\"MinimizeToTray\":false}";

            var loaded = SettingsValidator.ValidateAndRepair(JsonSerializer.Deserialize<Settings>(json));

            loaded.AutoPaste.Should().BeFalse("sanity: the file was really read");
            loaded.ShowRecordingIndicator.Should().BeTrue();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RoundTripsThroughJson(bool value)
        {
            var json = JsonSerializer.Serialize(new Settings { ShowRecordingIndicator = value });

            json.Should().Contain("\"ShowRecordingIndicator\":" + (value ? "true" : "false"));
            JsonSerializer.Deserialize<Settings>(json)!.ShowRecordingIndicator.Should().Be(value);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CommitDraft_CopiesItToLive(bool value)
        {
            var live = new Settings { ShowRecordingIndicator = !value };
            var draft = SettingsWindow.CloneForDraft(live);
            draft.ShowRecordingIndicator.Should().Be(!value, "the draft starts from the live value");

            draft.ShowRecordingIndicator = value;
            live.ShowRecordingIndicator.Should().Be(!value, "Cancel contract: an uncommitted edit never reaches live");

            SettingsWindow.CommitDraft(draft, live);
            live.ShowRecordingIndicator.Should().Be(value);
        }

        // ---- source tripwires (a missing source tree FAILS them: SourceTripwirePolicyTests) ----

        private static string MainWindowCode() => RepoSourceFile.ReadApp("MainWindow.xaml.cs");

        [Fact]
        public void MainWindow_FeedsEveryDictationNoticeToTheIndicator_BeforeItsOwnHandling()
        {
            var code = MainWindowCode();
            var method = Regex.Match(code, @"private void ShowDictationNotice\(DictationEvent ev\)\s*\{(?<body>[\s\S]*?)\n        \}");
            method.Success.Should().BeTrue("ShowDictationNotice was renamed - update this tripwire");
            var body = method.Groups["body"].Value;

            int pill = body.IndexOf("recordingIndicator?.OnDictationEvent(ev.Kind)", System.StringComparison.Ordinal);
            int handling = body.IndexOf("switch (ev.Kind)", System.StringComparison.Ordinal);
            pill.Should().BeGreaterThanOrEqualTo(0, "every notice kind must reach the pill, not a hand-picked subset of switch cases");
            pill.Should().BeLessThan(handling,
                "the Fault case blocks in a MessageBox and TranscriptionCompleted pastes: the pill must already be updated by then");
        }

        [Fact]
        public void MainWindow_ClosesTheIndicatorOnExit()
        {
            // ShutdownMode is OnLastWindowClose and a hidden window still counts: a pill that
            // is not closed with MainWindow would keep VoiceLite.exe alive with no UI at all.
            var code = MainWindowCode();
            var onClosed = code.Substring(code.IndexOf("protected override void OnClosed(EventArgs e)", System.StringComparison.Ordinal));
            onClosed.Should().Contain("recordingIndicator?.Close()");
            onClosed.Should().Contain("LevelAvailable -= ");

            // ... and FIRST: OnClosed is a long run of unguarded cleanup. A throw anywhere before
            // the pill is closed is swallowed by the dispatcher's unhandled-exception handler
            // ("application will continue"), and the hidden pill then keeps a UI-less VoiceLite.exe
            // holding the single-instance mutex. Close() never throws, so nothing is lost by going first.
            int close = onClosed.IndexOf("recordingIndicator?.Close()", System.StringComparison.Ordinal);
            int firstCleanup = onClosed.IndexOf("FlushSettingsOnExit()", System.StringComparison.Ordinal);
            firstCleanup.Should().BeGreaterThanOrEqualTo(0, "OnClosed no longer starts with FlushSettingsOnExit - update this tripwire");
            close.Should().BeLessThan(firstCleanup, "the pill must be closed before any cleanup that could throw");
        }

        [Fact]
        public void MainWindow_SettingsApply_RefreshesTheIndicator()
        {
            // Apply keeps the Settings dialog open: without a Refresh in the Apply callback, unticking
            // the box while recording leaves the pill up until the next notice or until the dialog closes.
            var code = MainWindowCode();
            var ctor = Regex.Match(code, @"new SettingsWindow\(settings,\s*\(\)\s*=>\s*(?<apply>\{[^}]*\}|[^,]*),");
            ctor.Success.Should().BeTrue("the SettingsWindow construction was reshaped - update this tripwire");
            ctor.Groups["apply"].Value.Should().Contain("SaveSettings()").And.Contain("recordingIndicator?.Refresh()");
        }

        [Fact]
        public void MainWindow_GatesTheIndicatorOnTheLiveSetting_AndRefreshesAfterSettings()
        {
            var code = MainWindowCode();
            code.Should().Contain("RecordingIndicator.ForWindow(() => settings.ShowRecordingIndicator",
                "the closure must read the settings FIELD each time: MainWindow replaces the instance after a Settings save");
            code.Should().Contain("recordingIndicator?.Refresh()");
        }

        [Fact]
        public void IndicatorWindow_NeverActivates()
        {
            var code = RepoSourceFile.ReadApp("RecordingIndicatorWindow.xaml.cs");
            Regex.IsMatch(code, @"\bActivate\s*\(").Should().BeFalse("the pill must never take activation: text is pasted into the foreground window");
            Regex.IsMatch(code, @"\bFocus\s*\(").Should().BeFalse();
            Regex.IsMatch(code, @"Dispatcher\.Invoke\s*\(").Should().BeFalse("cross-thread work is polled or posted, never a blocking Invoke");
        }
    }
}

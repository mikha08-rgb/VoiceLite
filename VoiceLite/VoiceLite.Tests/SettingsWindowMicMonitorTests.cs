using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// v2.5.2 hardening (S7b): opening Settings opened the microphone and held it for the
    /// life of the dialog even though the level meter lives on the second tab - mic-in-use
    /// indicator lit (and Bluetooth headsets dropped to hands-free) just to change a hotkey.
    /// The meter now follows the visibility of its own track element.
    /// SettingsWindow itself cannot be constructed here (its ctor reads the real license
    /// store and the fix opens real audio hardware), so these are tripwires on the wiring:
    /// they match source text, so a reformat of those lines fails them without a behaviour
    /// change. The WPF behaviour the wiring relies on (content of an unselected tab reports
    /// IsVisible=false) was verified once with a throwaway test that showed a real Window; it
    /// tested WPF rather than the fix, so it is not kept in the release-gating suite.
    /// The end-to-end check is manual.
    /// </summary>
    public class SettingsWindowMicMonitorTests
    {
        [Fact]
        public void Wiring_MeterFollowsLevelTrackVisibility_NotWindowLoaded()
        {
            var code = SettingsWindowHotkeyTests.ReadAppSource("SettingsWindow.xaml.cs");

            // Assert.True/False on Contains: a Should().Contain failure would dump the whole file.
            Assert.False(code.Contains("Loaded += (_, _) => StartMicMonitor();"),
                "that opened the microphone on every Settings open, whatever tab was showing");
            Assert.True(code.Contains("LevelTrack.IsVisibleChanged +="),
                "the meter must start/stop with the visibility of its track element");
            Assert.True(code.Contains("if (!LevelTrack.IsVisible)"),
                "StartMicMonitor must refuse to open the device while the meter is not on screen");
        }

        [Fact]
        public void Wiring_QueuedStart_DoesNotReopenADeviceThatIsAlreadyBeingMetered()
        {
            // Mic tab -> other tab -> Mic tab before the dispatcher idles queues TWO starts.
            // StartMicMonitor always stops first, so the second one would close and re-open the
            // device back-to-back on the UI thread (slow, and a Bluetooth profile flip).
            var code = SettingsWindowHotkeyTests.ReadAppSource("SettingsWindow.xaml.cs");

            Assert.True(System.Text.RegularExpressions.Regex.IsMatch(code,
                    @"if \(LevelTrack\.IsVisible && micMonitor == null\)\s*StartMicMonitor\(\);"),
                "the queued start must be a no-op when a monitor is already running");
        }
    }
}

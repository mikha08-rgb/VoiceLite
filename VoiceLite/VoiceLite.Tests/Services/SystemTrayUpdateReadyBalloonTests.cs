using AwesomeAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// v2.5.2 review #9: an install deferred while VoiceLite sits in the tray (the normal way it
    /// runs - the user is dictating) has no visible surface at all. The "update ready" balloon
    /// is the way back in: clicking it opens the same in-app updater, where "Install now" waits.
    /// No tray icon / window / STA thread needed: every trayIcon use is null-conditional.
    /// </summary>
    public class SystemTrayUpdateReadyBalloonTests
    {
        [Fact]
        public void UpdateReadyBalloonClick_OpensTheUpdater_ExactlyOnce()
        {
            using var tray = new SystemTrayManager();
            var raised = 0;
            tray.UpdateBalloonClicked += () => raised++;

            tray.ShowUpdateReady("2.5.3");
            tray.HandleBalloonClicked();
            tray.HandleBalloonClicked(); // stale second click

            raised.Should().Be(1);
        }

        [Fact]
        public void UnrelatedBalloon_AfterUpdateReady_DoesNotOpenTheUpdater()
        {
            using var tray = new SystemTrayManager();
            var raised = 0;
            tray.UpdateBalloonClicked += () => raised++;

            tray.ShowUpdateReady("2.5.3");
            tray.ShowBalloonTip("Recording auto-stopped", "Stopped after 5 minutes."); // replaces it
            tray.HandleBalloonClicked();

            raised.Should().Be(0);
        }

        // The MainWindow wiring ("if (!IsVisible) ... ShowUpdateReady") is pinned in MainWindowReviewFixesTests.
    }
}

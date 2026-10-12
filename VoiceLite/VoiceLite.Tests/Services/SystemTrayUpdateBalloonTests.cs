using System;
using System.Linq;
using AwesomeAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// S1: clicking the "update available" tray balloon must route into the in-app updater
    /// (SHA-256 verified download, dictation-aware install) instead of ShellExecuting the raw
    /// installer URL in the browser. SystemTrayManager needs no tray icon / window / STA thread
    /// for this: every trayIcon use is null-conditional before Initialize().
    /// </summary>
    public class SystemTrayUpdateBalloonTests
    {
        [Fact]
        public void UpdateBalloonClick_RaisesUpdateBalloonClicked_ExactlyOnce()
        {
            using var tray = new SystemTrayManager();
            var raised = 0;
            tray.UpdateBalloonClicked += () => raised++;

            tray.ShowUpdateAvailable("2.5.2");
            tray.HandleBalloonClicked();
            tray.HandleBalloonClicked(); // stale second click

            raised.Should().Be(1);
        }

        [Fact]
        public void UnrelatedBalloon_AfterUpdateBalloon_DoesNotOpenTheUpdater()
        {
            using var tray = new SystemTrayManager();
            var raised = 0;
            tray.UpdateBalloonClicked += () => raised++;

            tray.ShowUpdateAvailable("2.5.2");
            tray.ShowBalloonTip("VoiceLite", "Running in background."); // replaces the update balloon
            tray.HandleBalloonClicked();

            raised.Should().Be(0);
        }

        [Fact]
        public void BalloonClick_WithNoUpdatePending_DoesNothing()
        {
            using var tray = new SystemTrayManager();
            var raised = 0;
            tray.UpdateBalloonClicked += () => raised++;

            tray.HandleBalloonClicked();

            raised.Should().Be(0);
        }

        [Fact]
        public void ThrowingSubscriber_DoesNotEscapeTheTrayCallback()
        {
            using var tray = new SystemTrayManager();
            tray.UpdateBalloonClicked += () => throw new InvalidOperationException("boom");
            tray.ShowUpdateAvailable("2.5.2");

            var act = () => tray.HandleBalloonClicked();

            act.Should().NotThrow();
        }

        [Fact]
        public void ShowUpdateAvailable_TakesNoUrl()
        {
            // The tray manager must have nothing it could hand to the browser.
            var overloads = typeof(SystemTrayManager).GetMethods()
                .Where(m => m.Name == nameof(SystemTrayManager.ShowUpdateAvailable))
                .ToList();

            overloads.Should().ContainSingle()
                .Which.GetParameters().Select(p => p.Name).Should().Equal("version");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using AwesomeAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// UpdateWindow download -> install flow (F4 window side, F5 window side, F6 a-d) driven
    /// through fake download/install delegates. No network, no process, no Application: each
    /// test runs on its own STA thread with a DispatcherSynchronizationContext (so Progress&lt;T&gt;
    /// and await continuations land on that thread) and the window is never shown.
    /// </summary>
    public class UpdateWindowFlowTests
    {
        private static readonly UpdateInfo Info = new()
        {
            Version = "9.9.9",
            DownloadUrl = "https://example.invalid/VoiceLite-Setup-9.9.9.exe",
            ReleasePageUrl = "https://example.invalid/releases/v9.9.9",
            AssetName = "VoiceLite-Setup-9.9.9.exe",
            Sha256 = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF",
            Sha256Source = "digest",
        };

        private sealed class Harness
        {
            public bool DictationBusy;
            public bool SettingsOpen;
            public readonly List<string> Calls = new();
            public TaskCompletionSource<string> Download = new();
            public TaskCompletionSource Install = new();
            public IProgress<double>? Progress;
            public int AttemptFailed;
            public int InstallStarted;
            public bool Closed;
            public readonly List<double> ForwardedProgress = new();
            public UpdateWindow Window = null!;

            public int InstallCalls => Calls.FindAll(c => c == "install").Count;

            public void Create()
            {
                Window = new UpdateWindow(
                    Info,
                    isDictationBusy: () => DictationBusy,
                    isSettingsOpen: () => SettingsOpen,
                    blockHotkeys: () => Calls.Add("block"),
                    unblockHotkeys: () => Calls.Add("unblock"),
                    download: (_, progress, _) => { Progress = progress; Calls.Add("download"); return Download.Task; },
                    install: _ => { Calls.Add("install"); return Install.Task; });
                Window.DownloadProgressChanged += p => ForwardedProgress.Add(p);
                Window.AttemptFailed += () => AttemptFailed++;
                Window.InstallStarted += () => InstallStarted++;
                Window.Closed += (_, _) => Closed = true;
            }

            public void Click(ButtonBase button)
            {
                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Pump();
            }

            public Button InstallButton => (Button)Window.FindName("InstallButton");
            public Button LaterButton => (Button)Window.FindName("LaterButton");
            public Button SkipButton => (Button)Window.FindName("SkipButton");
            public FrameworkElement ErrorPanel => (FrameworkElement)Window.FindName("ErrorPanel");
            public TextBlock ErrorText => (TextBlock)Window.FindName("ErrorText");
            public Button DownloadPageButton => (Button)Window.FindName("OpenDownloadPageButton");
        }

        private static void Pump()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        private static void RunOnSta(Action<Harness> body)
        {
            ExceptionDispatchInfo? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                    var h = new Harness();
                    h.Create();
                    body(h);
                    if (!h.Closed)
                    {
                        // Let a window that is mid-"install" close: finish the fake install as a failure.
                        h.Install.TrySetException(new InvalidOperationException("test teardown"));
                        Pump();
                        h.Window.Close();
                    }
                }
                catch (Exception ex)
                {
                    failure = ExceptionDispatchInfo.Capture(ex);
                }
                finally
                {
                    // This thread's dispatcher only - never Application shutdown.
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            thread.Join(TimeSpan.FromSeconds(30)).Should().BeTrue("the STA test thread must not hang");
            failure?.Throw();
        }

        // ------------------------------------------------------------------ F6 (a)

        [Fact]
        public void LaterClicked_ThenDownloadCompletes_DoesNotInstall() => RunOnSta(h =>
        {
            h.Click(h.InstallButton);
            h.Click(h.LaterButton);
            h.Closed.Should().BeTrue();

            // The cancel landed after the download's last token check: it completes anyway.
            h.Download.SetResult(@"C:\fake\VoiceLite-Setup-9.9.9.exe");
            Pump();

            h.InstallCalls.Should().Be(0, "the user said Later");
            h.Calls.Should().NotContain("block");
            h.Window.Result.Should().Be(UpdateWindowResult.Later);
        });

        [Fact]
        public void ClosedViaX_ThenDownloadCompletes_DoesNotInstall() => RunOnSta(h =>
        {
            h.Click(h.InstallButton);
            h.Window.Close();
            h.Download.SetResult(@"C:\fake\VoiceLite-Setup-9.9.9.exe");
            Pump();

            h.InstallCalls.Should().Be(0);
            h.Window.Result.Should().Be(UpdateWindowResult.Later);
        });

        [Fact]
        public void DownloadInFlight_SkipIsDisabled_LaterIsEnabled() => RunOnSta(h =>
        {
            // The invariant that makes "Skip raced the install" impossible.
            h.Click(h.InstallButton);

            h.SkipButton.IsEnabled.Should().BeFalse();
            h.InstallButton.IsEnabled.Should().BeFalse();
            h.LaterButton.IsEnabled.Should().BeTrue("Later is how the user cancels a download");
        });

        // ------------------------------------------------------------------ F6 (b)

        [Fact]
        public void ProgressReportedAfterClose_IsNotForwarded() => RunOnSta(h =>
        {
            h.Click(h.InstallButton);
            h.Progress!.Report(0.25);
            Pump();
            h.ForwardedProgress.Should().Equal(0.25);

            h.Click(h.LaterButton);
            h.Progress.Report(0.42); // a write that was already in flight when Later was clicked
            Pump();

            h.ForwardedProgress.Should().Equal(new[] { 0.25 },
                "a late report would put the main-window banner back on 'Downloading update... 42%'");
        });

        // ------------------------------------------------------------------ F6 (c) + F4 window side

        [Fact]
        public void DownloadCompletes_BlocksHotkeys_ThenInstalls_WithEverythingDisabled() => RunOnSta(h =>
        {
            h.Click(h.InstallButton);
            h.Download.SetResult(@"C:\fake\VoiceLite-Setup-9.9.9.exe");
            Pump();

            h.Calls.Should().Equal("download", "block", "install");
            h.InstallStarted.Should().Be(1);
            h.Window.Result.Should().Be(UpdateWindowResult.Installing);
            h.LaterButton.IsEnabled.Should().BeFalse("Later must not be clickable while the installer is starting");
            h.InstallButton.IsEnabled.Should().BeFalse();
            h.SkipButton.IsEnabled.Should().BeFalse();
        });

        [Fact]
        public void WhileWaitingForInstaller_WindowCannotBeClosedByTheUser() => RunOnSta(h =>
        {
            h.Click(h.InstallButton);
            h.Download.SetResult(@"C:\fake\VoiceLite-Setup-9.9.9.exe");
            Pump();

            h.Window.Close(); // X / Alt+F4 while a UAC prompt is pending
            Pump();

            h.Closed.Should().BeFalse("a failure must still have a window to be shown in");
            h.Window.Result.Should().Be(UpdateWindowResult.Installing);
        });

        [Fact]
        public void InstallerExitsEarly_ShowsExitCode_ReenablesButtons_UnblocksHotkeys() => RunOnSta(h =>
        {
            h.Click(h.InstallButton);
            h.Download.SetResult(@"C:\fake\VoiceLite-Setup-9.9.9.exe");
            Pump();

            h.Install.SetException(new UpdateInstallFailedException(5));
            Pump();

            h.Calls.Should().Equal("download", "block", "install", "unblock");
            h.AttemptFailed.Should().Be(1, "MainWindow clears the 'Installing update...' banner on this event");
            h.Window.Result.Should().Be(UpdateWindowResult.None);
            h.ErrorPanel.Visibility.Should().Be(Visibility.Visible);
            h.ErrorText.Text.Should().Contain("exit code 5");
            h.InstallButton.IsEnabled.Should().BeTrue();
            h.LaterButton.IsEnabled.Should().BeTrue();
            h.SkipButton.IsEnabled.Should().BeTrue();

            // Retry re-uses the verified download and goes through the guard again.
            h.Install = new TaskCompletionSource();
            h.Click(h.InstallButton);
            h.Calls.Should().Equal("download", "block", "install", "unblock", "block", "install");
        });

        [Fact]
        public void InstallerCannotBeStarted_UnblocksHotkeys_AndShowsError() => RunOnSta(h =>
        {
            h.Click(h.InstallButton);
            h.Install.SetException(new FileNotFoundException("Installer not found"));
            h.Download.SetResult(@"C:\fake\VoiceLite-Setup-9.9.9.exe");
            Pump();

            h.Calls.Should().Equal("download", "block", "install", "unblock");
            h.ErrorPanel.Visibility.Should().Be(Visibility.Visible);
            h.LaterButton.IsEnabled.Should().BeTrue();
        });

        [Fact]
        public void DictationBusy_DefersInstall_WithoutBlockingHotkeys() => RunOnSta(h =>
        {
            h.DictationBusy = true;
            h.Click(h.InstallButton);
            h.Download.SetResult(@"C:\fake\VoiceLite-Setup-9.9.9.exe");
            Pump();

            h.Calls.Should().Equal("download");
            h.InstallButton.Content.Should().Be("Install now");
            h.InstallButton.IsEnabled.Should().BeTrue();
            h.ErrorText.Text.Should().Contain("recording or transcribing");
            h.DownloadPageButton.Visibility.Should().Be(Visibility.Collapsed, "nothing failed - this is only a 'not yet' notice");
        });

        // ------------------------------------------------------------------ F6 (d)

        [Fact]
        public void SettingsOpen_DoesNotAutoInstall_UntilTheUserClicksInstallNow() => RunOnSta(h =>
        {
            h.SettingsOpen = true;
            h.Click(h.InstallButton);
            h.Download.SetResult(@"C:\fake\VoiceLite-Setup-9.9.9.exe");
            Pump();

            h.Calls.Should().Equal("download");
            h.InstallButton.Content.Should().Be("Install now");
            h.InstallButton.IsEnabled.Should().BeTrue();
            h.ErrorText.Text.Should().Contain("Settings");

            h.SettingsOpen = false;
            h.Click(h.InstallButton);

            h.Calls.Should().Equal("download", "block", "install");
        });

        // ------------------------------------------------------------------ F5 window side

        [Fact]
        public void DownloadTimeout_IsNotTreatedAsUserCancel_ShowsErrorWithRetry() => RunOnSta(h =>
        {
            h.Click(h.InstallButton);

            // HttpClient.Timeout shape: cancellation exception, but the user cancelled nothing.
            h.Download.SetException(new TaskCanceledException("timeout", new TimeoutException()));
            Pump();

            h.ErrorPanel.Visibility.Should().Be(Visibility.Visible, "silently returning leaves the dialog stuck on 'Downloading...'");
            h.InstallButton.Content.Should().Be("Try again");
            h.InstallButton.IsEnabled.Should().BeTrue();
            h.SkipButton.IsEnabled.Should().BeTrue();
            h.AttemptFailed.Should().Be(1);
            h.InstallCalls.Should().Be(0);
        });

        [Fact]
        public void DownloadStall_ShowsErrorWithRetry_AndRetryDownloadsAgain() => RunOnSta(h =>
        {
            h.Click(h.InstallButton);
            h.Download.SetException(new IOException("The download stalled or timed out."));
            Pump();

            h.ErrorText.Text.Should().Contain("stalled");
            h.DownloadPageButton.Visibility.Should().Be(Visibility.Visible);
            h.AttemptFailed.Should().Be(1);

            h.Download = new TaskCompletionSource<string>();
            h.Click(h.InstallButton);
            h.Calls.Should().Equal("download", "download");
        });

        [Fact]
        public void UserCancel_StaysSilent() => RunOnSta(h =>
        {
            h.Click(h.InstallButton);
            h.Click(h.LaterButton);
            h.Download.SetCanceled();
            Pump();

            h.AttemptFailed.Should().Be(0);
            h.ErrorPanel.Visibility.Should().Be(Visibility.Collapsed);
        });

        // ------------------------------------------------------------------ S1 fallback

        [Fact]
        public void ErrorState_OffersTheDownloadPage() => RunOnSta(h =>
        {
            // The tray balloon no longer opens the browser, so a failed in-app update needs its
            // own way to the release page. (Not clicked here: it would open a real browser.)
            var button = h.Window.FindName("OpenDownloadPageButton") as Button;

            button.Should().NotBeNull();
            var parent = button!.Parent as FrameworkElement;
            while (parent != null && !ReferenceEquals(parent, h.ErrorPanel))
                parent = parent.Parent as FrameworkElement;
            parent.Should().BeSameAs(h.ErrorPanel, "the link belongs to the error state");
        });
    }
}

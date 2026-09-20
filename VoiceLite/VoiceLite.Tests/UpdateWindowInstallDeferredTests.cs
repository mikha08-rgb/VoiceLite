using System;
using System.Collections.Generic;
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
    /// v2.5.2 review #9: when the download finishes while Settings is open or a dictation is
    /// running, the install is deferred ("click Install now"). Neither InstallStarted nor
    /// AttemptFailed is raised on that path, so MainWindow's banner stayed on
    /// "Downloading update... 100%" - a download that never seems to complete. The window now
    /// raises InstallDeferred, and no progress report may land after it.
    /// Same harness style as UpdateWindowFlowTests: own STA thread, fake download/install, the
    /// window is never shown, no Application, no network, no process.
    /// </summary>
    public class UpdateWindowInstallDeferredTests
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
            /// <summary>Everything MainWindow's banner reacts to, in the order it arrived.</summary>
            public readonly List<string> BannerEvents = new();
            public Func<IProgress<double>, Task<string>> Download = _ => new TaskCompletionSource<string>().Task;
            public TaskCompletionSource Install = new();
            public UpdateWindow Window = null!;

            public void Create()
            {
                Window = new UpdateWindow(
                    Info,
                    isDictationBusy: () => DictationBusy,
                    isSettingsOpen: () => SettingsOpen,
                    blockHotkeys: () => Calls.Add("block"),
                    unblockHotkeys: () => Calls.Add("unblock"),
                    download: (_, progress, _) => { Calls.Add("download"); return Download(progress); },
                    install: _ => { Calls.Add("install"); return Install.Task; },
                    installWaitHintDelay: TimeSpan.FromMilliseconds(100));
                Window.DownloadProgressChanged += p => BannerEvents.Add($"progress:{p * 100:0}");
                Window.InstallStarted += () => BannerEvents.Add("started");
                Window.AttemptFailed += () => BannerEvents.Add("failed");
                Window.InstallDeferred += () => BannerEvents.Add("deferred");
            }

            public void ClickInstall()
            {
                ((Button)Window.FindName("InstallButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Pump();
            }
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
                    body(h);
                    // Let a window that is mid-"install" close: finish the fake install as a failure.
                    h.Install.TrySetException(new InvalidOperationException("test teardown"));
                    Pump();
                    h.Window.Close();
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

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void DownloadFinishes_WhileSettingsOpenOrDictating_RaisesInstallDeferred_Once(bool settingsOpen, bool dictationBusy) => RunOnSta(h =>
        {
            h.SettingsOpen = settingsOpen;
            h.DictationBusy = dictationBusy;
            var download = new TaskCompletionSource<string>();
            h.Download = progress => { progress.Report(1.0); return download.Task; };
            h.Create();

            h.ClickInstall();
            download.SetResult(@"C:\fake\VoiceLite-Setup-9.9.9.exe");
            Pump();

            h.Calls.Should().Equal(new[] { "download" }, "the install is deferred, and the hotkey is not blocked");
            h.BannerEvents.Should().Equal("progress:100", "deferred");
        });

        [Fact]
        public void VerifiedFileReused_ProgressReportNeverLandsAfterInstallDeferred() => RunOnSta(h =>
        {
            // UpdateInstaller's "already downloaded and verified" path: Report(1.0) and an
            // already-completed task. Progress<T> POSTS its callback, the await continues inline,
            // so the 100% report is delivered after the deferral - and must not reach the banner.
            h.SettingsOpen = true;
            h.Download = progress =>
            {
                progress.Report(1.0);
                return Task.FromResult(@"C:\fake\VoiceLite-Setup-9.9.9.exe");
            };
            h.Create();

            h.ClickInstall();
            Pump();

            h.BannerEvents.Should().NotBeEmpty();
            h.BannerEvents[^1].Should().Be("deferred",
                "whatever arrives last is what the banner shows; 'Downloading update... 100%' must not win");
        });

        [Fact]
        public void InstallNow_AfterTheBlockerIsGone_StartsTheInstall() => RunOnSta(h =>
        {
            h.DictationBusy = true;
            h.Download = _ => Task.FromResult(@"C:\fake\VoiceLite-Setup-9.9.9.exe");
            h.Create();
            h.ClickInstall();

            h.ClickInstall(); // still dictating: deferred again, still no install
            h.DictationBusy = false;
            h.ClickInstall();

            h.BannerEvents.Should().Equal("deferred", "deferred", "started");
            h.Calls.Should().Equal("download", "block", "install");
        });

        [Fact]
        public void NormalInstall_NeverRaisesInstallDeferred() => RunOnSta(h =>
        {
            h.Download = _ => Task.FromResult(@"C:\fake\VoiceLite-Setup-9.9.9.exe");
            h.Create();

            h.ClickInstall();

            h.BannerEvents.Should().Equal("started");
        });
    }
}

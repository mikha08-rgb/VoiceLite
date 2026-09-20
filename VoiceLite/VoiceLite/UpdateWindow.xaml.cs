using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using VoiceLite.Services;

namespace VoiceLite
{
    public enum UpdateWindowResult
    {
        /// <summary>Window still open, or closed without a decision (treated as Later).</summary>
        None,
        Later,
        /// <summary>User chose "Skip this version" — caller records settings.SkippedUpdateVersion.</summary>
        Skipped,
        /// <summary>Installer launched; the app is shutting down.</summary>
        Installing,
    }

    /// <summary>
    /// "VoiceLite x.y.z is available" dialog: download + verify + silent install + relaunch.
    /// Non-modal (MainWindow shows it with Show()) so the user can keep dictating while the
    /// installer downloads. Owned by MainWindow.UpdateBanner_Click.
    /// </summary>
    public partial class UpdateWindow : Window
    {
        private readonly UpdateInfo info;
        private readonly Func<bool> isDictationBusy;
        private readonly Func<bool> isSettingsOpen;
        private readonly Action blockHotkeys;
        private readonly Action unblockHotkeys;
        private readonly Func<UpdateInfo, IProgress<double>, CancellationToken, Task<string>> download;
        private readonly Func<string, Task> install;
        private readonly TimeSpan installWaitHintDelay;
        private CancellationTokenSource? downloadCts;
        private string? downloadedPath;
        private bool installing;
        private bool closed;

        public UpdateWindowResult Result { get; private set; } = UpdateWindowResult.None;

        /// <summary>Download progress 0..1, raised on the UI thread. MainWindow mirrors it in the banner.</summary>
        public event Action<double>? DownloadProgressChanged;

        /// <summary>The installer is being launched; hotkeys are blocked until it succeeds (shutdown) or fails.</summary>
        public event Action? InstallStarted;

        /// <summary>
        /// A download or install attempt failed and the dialog is back in its retry state.
        /// MainWindow resets the banner from "Downloading/Installing..." to "Update x available".
        /// </summary>
        public event Action? AttemptFailed;

        /// <summary>
        /// The installer is downloaded and verified, but the install was not started because
        /// Settings is open or a dictation is running; the dialog now offers "Install now".
        /// Neither InstallStarted nor AttemptFailed fires on that path, so without this
        /// MainWindow's banner stayed on "Downloading update... 100%".
        /// </summary>
        public event Action? InstallDeferred;

        /// <param name="isDictationBusy">
        /// Returns true while a recording or transcription is in progress; the install is
        /// never started in that state (the installer kills VoiceLite.exe).
        /// </param>
        public UpdateWindow(UpdateInfo info, Func<bool> isDictationBusy)
            : this(info, isDictationBusy, () => false, () => { }, () => { })
        {
        }

        /// <param name="isSettingsOpen">
        /// True while the Settings dialog is open: the install is not started under it (it would
        /// drop the user's draft, or interrupt a license activation). The user clicks Install now.
        /// </param>
        /// <param name="blockHotkeys">
        /// Called on the UI thread in the same step as the "dictation idle?" check, right before
        /// the installer is launched: from here until shutdown the hotkey must not start a
        /// recording that the update would throw away.
        /// </param>
        /// <param name="unblockHotkeys">Called when the install failed and VoiceLite keeps running.</param>
        public UpdateWindow(
            UpdateInfo info,
            Func<bool> isDictationBusy,
            Func<bool> isSettingsOpen,
            Action blockHotkeys,
            Action unblockHotkeys)
            : this(info, isDictationBusy, isSettingsOpen, blockHotkeys, unblockHotkeys,
                   (i, p, ct) => UpdateInstaller.DownloadAsync(i, p, ct), UpdateInstaller.InstallAsync)
        {
        }

        /// <summary>Test seam: download and install are injected so no network/process is touched.</summary>
        internal UpdateWindow(
            UpdateInfo info,
            Func<bool> isDictationBusy,
            Func<bool> isSettingsOpen,
            Action blockHotkeys,
            Action unblockHotkeys,
            Func<UpdateInfo, IProgress<double>, CancellationToken, Task<string>> download,
            Func<string, Task> install,
            TimeSpan? installWaitHintDelay = null)
        {
            InitializeComponent();
            this.info = info;
            this.isDictationBusy = isDictationBusy;
            this.isSettingsOpen = isSettingsOpen;
            this.blockHotkeys = blockHotkeys;
            this.unblockHotkeys = unblockHotkeys;
            this.download = download;
            this.install = install;
            this.installWaitHintDelay = installWaitHintDelay ?? TimeSpan.FromSeconds(30);

            var current = UpdateCheckService.GetCurrentVersion()?.ToString(3) ?? "?";
            HeadlineText.Text = $"VoiceLite {info.Version} is available";

            if (info.CanAutoInstall)
            {
                DetailText.Text =
                    $"You have {current}. The installer downloads in the background, then VoiceLite " +
                    "closes, installs the update, and reopens. Your settings, history and license are kept.";
            }
            else
            {
                // No published hash → we refuse to run an unverifiable installer silently.
                DetailText.Text =
                    $"You have {current}. This release can't be verified automatically, so the update " +
                    "opens on the download page instead.";
                InstallButton.Content = "Open download page";
            }
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (!info.CanAutoInstall)
            {
                OpenReleasePage();
                Result = UpdateWindowResult.Later;
                Close();
                return;
            }

            if (downloadedPath != null)
            {
                await TryInstallAsync();
                return;
            }

            await DownloadAsync();
        }

        private async Task DownloadAsync()
        {
            HideError();
            SetBusy(true);
            ProgressPanel.Visibility = Visibility.Visible;
            DownloadProgress.IsIndeterminate = true;
            StatusText.Text = $"Downloading VoiceLite {info.Version}...";
            ErrorLogger.LogWarning($"UpdateWindow: user started download of {info.Version}");

            // Local copy: the field is replaced on every attempt, and "did the USER cancel?"
            // must be answered for this attempt's token.
            var cts = new CancellationTokenSource();
            downloadCts = cts;
            // Progress<T> captures the UI SynchronizationContext, so reports land on the UI thread.
            var progress = new Progress<double>(p =>
            {
                // A report queued before Later/X would flip MainWindow's banner back to
                // "Downloading update... N%" after the Closed handler already reset it.
                if (closed) return;
                // Progress<T> posts its callbacks: the final 100% report can arrive after the
                // download's await has already continued (always, when a verified file is reused)
                // and would put the banner back on "Downloading..." over "ready"/"Installing".
                if (downloadedPath != null) return;
                DownloadProgress.IsIndeterminate = false;
                DownloadProgress.Value = p * 100;
                StatusText.Text = $"Downloading VoiceLite {info.Version}... {p * 100:0}%";
                DownloadProgressChanged?.Invoke(p);
            });

            try
            {
                downloadedPath = await download(info, progress, cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // Only a real user cancel (Later / X) is silent. A timeout or stall is also
                // cancellation-shaped and must reach the generic catch below (error + retry).
                ErrorLogger.LogWarning("UpdateWindow: download cancelled");
                return;
            }
            catch (UpdateVerificationException ex)
            {
                ErrorLogger.LogError("UpdateWindow: installer verification failed", ex);
                ShowError(ex.Message);
                ResetToIdle();
                AttemptFailed?.Invoke();
                return;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("UpdateWindow: download failed", ex);
                ShowError($"Download failed: {ex.Message}\n\nCheck your connection and try again, or use the download page.");
                ResetToIdle();
                AttemptFailed?.Invoke();
                return;
            }

            // Later / X can land after the download's last cancellation check. The verified
            // file stays on disk and is reused next time; the user's "not now" wins.
            if (closed || cts.IsCancellationRequested) return;

            // A reused verified file's only progress report arrives after this point and is
            // dropped (downloadedPath is set), so the marquee has to be stopped here.
            DownloadProgress.IsIndeterminate = false;
            DownloadProgress.Value = 100;
            StatusText.Text = "Download verified.";
            await TryInstallAsync();
        }

        private async Task TryInstallAsync()
        {
            if (downloadedPath == null || closed || installing) return;

            if (isSettingsOpen())
            {
                ErrorLogger.LogWarning("UpdateWindow: install deferred - Settings is open");
                ShowError("The update is ready, but Settings is open. Close Settings, then click Install now.", offerDownloadPage: false);
                InstallButton.Content = "Install now";
                SetBusy(false);
                InstallDeferred?.Invoke();
                return;
            }

            if (isDictationBusy())
            {
                ErrorLogger.LogWarning("UpdateWindow: install deferred - recording/transcription in progress");
                ShowError("VoiceLite is still recording or transcribing. Finish dictating, then click Install now.", offerDownloadPage: false);
                InstallButton.Content = "Install now";
                SetBusy(false);
                InstallDeferred?.Invoke();
                return;
            }

            // Same UI-thread step as the idle check above (no await in between): from here until
            // shutdown - which can be minutes away if a UAC prompt is up - the hotkey is ignored.
            blockHotkeys();
            try
            {
                HideError();
                StatusText.Text = "Installing... VoiceLite will close and reopen.";
                DownloadProgress.IsIndeterminate = true;
                installing = true; // before SetBusy: it is what disables Later
                SetBusy(true);
                Result = UpdateWindowResult.Installing;
                InstallStarted?.Invoke();

                // Returns once the installer signalled ready and shutdown is queued; throws when
                // the installer could not be started or exited first (VoiceLite keeps running).
                var installTask = install(downloadedPath);

                // No timeout on the wait itself (a UAC prompt can sit for minutes), but the user
                // must be told why the window won't close and the hotkey does nothing.
                if (await Task.WhenAny(installTask, Task.Delay(installWaitHintDelay)) != installTask)
                {
                    ErrorLogger.LogWarning("UpdateWindow: still waiting for the installer to signal ready; dictation stays paused");
                    StatusText.Text =
                        "Waiting for the installer - check for a Windows permission prompt. " +
                        "Dictation is paused until the update finishes.";
                }
                await installTask;
            }
            catch (Exception ex)
            {
                installing = false;
                Result = UpdateWindowResult.None;
                unblockHotkeys();
                ErrorLogger.LogError("UpdateWindow: update install failed", ex);
                ShowError(ex switch
                {
                    // Finished without the handshake: it may well have installed, so no
                    // "failed" / "not changed" claim and no nudge to run it again.
                    UpdateInstallFailedException { ExitCode: 0 } =>
                        "The installer finished, but VoiceLite was not restarted. " +
                        "Please exit and restart VoiceLite to finish the update.",
                    UpdateInstallFailedException failed =>
                        $"Update failed (exit code {failed.ExitCode}). VoiceLite was not changed and is still running. " +
                        "Try again, or use the download page.",
                    _ => $"Couldn't start the installer: {ex.Message}",
                });
                DownloadProgress.IsIndeterminate = false;
                DownloadProgress.Value = 100;
                StatusText.Text = "Download verified.";
                InstallButton.Content = "Install now";
                SetBusy(false);
                AttemptFailed?.Invoke();
            }
        }

        private void LaterButton_Click(object sender, RoutedEventArgs e)
        {
            Result = UpdateWindowResult.Later;
            Close();
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorLogger.LogWarning($"UpdateWindow: user skipped version {info.Version}");
            Result = UpdateWindowResult.Skipped;
            Close();
        }

        private void OpenDownloadPageButton_Click(object sender, RoutedEventArgs e) => OpenReleasePage();

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (installing)
            {
                // Waiting for the installer (possibly on a UAC prompt). Stay open so a failure
                // has somewhere to be shown. Application.Shutdown ignores this Cancel, so the
                // update itself and tray Exit are never held up.
                e.Cancel = true;
                return;
            }
            if (Result == UpdateWindowResult.None) Result = UpdateWindowResult.Later;
            try { downloadCts?.Cancel(); } catch { /* already disposed */ }
        }

        protected override void OnClosed(EventArgs e)
        {
            closed = true; // before base, which raises Closed (MainWindow resets the banner there)
            base.OnClosed(e);
        }

        private void OpenReleasePage()
        {
            try
            {
                ErrorLogger.LogWarning($"UpdateWindow: opening release page {info.ReleasePageUrl}");
                Process.Start(new ProcessStartInfo { FileName = info.ReleasePageUrl, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("UpdateWindow: failed to open release page", ex);
                ShowError($"Couldn't open the browser. Download manually from:\n{info.ReleasePageUrl}");
            }
        }

        private void SetBusy(bool busy)
        {
            InstallButton.IsEnabled = !busy;
            SkipButton.IsEnabled = !busy;
            // "Later" stays enabled during download so the user can cancel.
            LaterButton.IsEnabled = !installing;
        }

        private void ResetToIdle()
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
            DownloadProgress.IsIndeterminate = false;
            DownloadProgress.Value = 0;
            InstallButton.Content = "Try again";
            SetBusy(false);
        }

        private void ShowError(string message, bool offerDownloadPage = true)
        {
            ErrorText.Text = message;
            // Real failures get the manual way out; "busy, click Install now" notices don't.
            OpenDownloadPageButton.Visibility = offerDownloadPage ? Visibility.Visible : Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            ErrorPanel.Visibility = Visibility.Collapsed;
        }
    }
}

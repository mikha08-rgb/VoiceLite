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
        private CancellationTokenSource? downloadCts;
        private string? downloadedPath;
        private bool installing;

        public UpdateWindowResult Result { get; private set; } = UpdateWindowResult.None;

        /// <summary>Download progress 0..1, raised on the UI thread. MainWindow mirrors it in the banner.</summary>
        public event Action<double>? DownloadProgressChanged;

        /// <param name="isDictationBusy">
        /// Returns true while a recording or transcription is in progress; the install is
        /// never started in that state (the installer kills VoiceLite.exe).
        /// </param>
        public UpdateWindow(UpdateInfo info, Func<bool> isDictationBusy)
        {
            InitializeComponent();
            this.info = info;
            this.isDictationBusy = isDictationBusy;

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
                TryInstall();
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

            downloadCts = new CancellationTokenSource();
            // Progress<T> captures the UI SynchronizationContext, so reports land on the UI thread.
            var progress = new Progress<double>(p =>
            {
                DownloadProgress.IsIndeterminate = false;
                DownloadProgress.Value = p * 100;
                StatusText.Text = $"Downloading VoiceLite {info.Version}... {p * 100:0}%";
                DownloadProgressChanged?.Invoke(p);
            });

            try
            {
                downloadedPath = await UpdateInstaller.DownloadAsync(info, progress, downloadCts.Token);
            }
            catch (OperationCanceledException)
            {
                ErrorLogger.LogWarning("UpdateWindow: download cancelled");
                return;
            }
            catch (UpdateVerificationException ex)
            {
                ErrorLogger.LogError("UpdateWindow: installer verification failed", ex);
                ShowError(ex.Message);
                ResetToIdle();
                return;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("UpdateWindow: download failed", ex);
                ShowError($"Download failed: {ex.Message}\n\nCheck your connection and try again, or use the download page.");
                ResetToIdle();
                return;
            }

            DownloadProgress.Value = 100;
            StatusText.Text = "Download verified.";
            TryInstall();
        }

        private void TryInstall()
        {
            if (downloadedPath == null) return;

            if (isDictationBusy())
            {
                ErrorLogger.LogWarning("UpdateWindow: install deferred - recording/transcription in progress");
                ShowError("VoiceLite is still recording or transcribing. Finish dictating, then click Install now.");
                InstallButton.Content = "Install now";
                SetBusy(false);
                return;
            }

            try
            {
                HideError();
                StatusText.Text = "Installing... VoiceLite will close and reopen.";
                SetBusy(true);
                installing = true;
                Result = UpdateWindowResult.Installing;
                UpdateInstaller.Install(downloadedPath);
            }
            catch (Exception ex)
            {
                installing = false;
                Result = UpdateWindowResult.None;
                ErrorLogger.LogError("UpdateWindow: failed to launch installer", ex);
                ShowError($"Couldn't start the installer: {ex.Message}");
                InstallButton.Content = "Install now";
                SetBusy(false);
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

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (installing) return; // app is shutting down; nothing to cancel
            if (Result == UpdateWindowResult.None) Result = UpdateWindowResult.Later;
            try { downloadCts?.Cancel(); } catch { /* already disposed */ }
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

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorPanel.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            ErrorPanel.Visibility = Visibility.Collapsed;
        }
    }
}

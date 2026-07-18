using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;

namespace VoiceLite.Services
{
    public class SystemTrayManager : IDisposable
    {
        private TaskbarIcon? trayIcon;
        private System.Drawing.Icon? customIcon;
        private Window? mainWindow;
        private string? pendingUpdateUrl;
        // Configured hotkey display string (e.g. "Ctrl+Alt+Space"), passed in by
        // MainWindow so tooltips/balloons never claim a hardcoded hotkey.
        private string hotkeyDisplay = "the hotkey";

        public SystemTrayManager()
        {
            // Window will be set via Initialize method
        }

        private void InitializeTrayIcon()
        {
            // Load custom icon from the application directory
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VoiceLite.ico");
            if (File.Exists(iconPath))
            {
                customIcon = new System.Drawing.Icon(iconPath);
            }

            trayIcon = new TaskbarIcon
            {
                Icon = customIcon ?? System.Drawing.SystemIcons.Application,
                ToolTipText = IdleTooltip
            };

            trayIcon.TrayMouseDoubleClick += (s, e) =>
            {
                ShowMainWindow();
            };

            // When an update balloon is showing, clicking it opens the download URL.
            // Pending URL is captured per-notification so the handler stays generic.
            trayIcon.TrayBalloonTipClicked += (s, e) =>
            {
                var url = pendingUpdateUrl;
                if (string.IsNullOrEmpty(url)) return;
                pendingUpdateUrl = null;
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Failed to open update download URL", ex);
                }
            };

            var contextMenu = new System.Windows.Controls.ContextMenu();

            var showItem = new System.Windows.Controls.MenuItem
            {
                Header = "Show VoiceLite"
            };
            showItem.Click += (s, e) => ShowMainWindow();

            var separator = new System.Windows.Controls.Separator();

            var exitItem = new System.Windows.Controls.MenuItem
            {
                Header = "Exit"
            };
            exitItem.Click += (s, e) =>
            {
                Application.Current.Shutdown();
            };

            contextMenu.Items.Add(showItem);
            contextMenu.Items.Add(separator);
            contextMenu.Items.Add(exitItem);

            trayIcon.ContextMenu = contextMenu;
        }

        private string IdleTooltip => $"VoiceLite - Press {hotkeyDisplay} to dictate";

        public void ShowBalloonTip(string title, string message)
        {
            // A non-update balloon replaces any pending update balloon; clear the stashed
            // URL so clicking this unrelated balloon doesn't open the update download.
            pendingUpdateUrl = null;
            trayIcon?.ShowBalloonTip(title, message, BalloonIcon.Info);
        }

        public void ShowUpdateAvailable(string version, string downloadUrl)
        {
            // Stash URL for the balloon-click handler. Stays set until clicked or replaced.
            pendingUpdateUrl = downloadUrl;
            trayIcon?.ShowBalloonTip(
                "VoiceLite update available",
                $"Version {version} is available. Click to download.",
                BalloonIcon.Info);
        }

        private void ShowMainWindow()
        {
            mainWindow?.Show();
            if (mainWindow != null)
            {
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
            }
        }

        public void MinimizeToTray()
        {
            mainWindow?.Hide();
            ShowBalloonTip("VoiceLite", $"Running in background. Press {hotkeyDisplay} to dictate.");
        }

        public void Initialize(Window window, string hotkeyDisplayText)
        {
            mainWindow = window;
            if (!string.IsNullOrWhiteSpace(hotkeyDisplayText))
            {
                hotkeyDisplay = hotkeyDisplayText;
            }
            InitializeTrayIcon();
        }

        /// <summary>
        /// Call when the user changes the hotkey so tooltips stay accurate.
        /// </summary>
        public void UpdateHotkeyDisplay(string hotkeyDisplayText)
        {
            if (string.IsNullOrWhiteSpace(hotkeyDisplayText))
                return;

            hotkeyDisplay = hotkeyDisplayText;
            if (trayIcon != null)
            {
                trayIcon.ToolTipText = IdleTooltip;
            }
        }

        public void Dispose()
        {
            trayIcon?.Dispose();
            customIcon?.Dispose();
        }
    }
}

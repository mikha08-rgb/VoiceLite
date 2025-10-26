using System;
using System.IO;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using VoiceLite.Core.Interfaces.Services;

namespace VoiceLite.Services
{
    public class SystemTrayManager : ISystemTrayManager
    {
        private TaskbarIcon? trayIcon;
        private System.Drawing.Icon? customIcon;
        private Window? mainWindow;

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
                ToolTipText = "VoiceLite - Hold Alt to dictate"
            };

            trayIcon.TrayMouseDoubleClick += (s, e) =>
            {
                ShowMainWindow();
            };

            var contextMenu = new System.Windows.Controls.ContextMenu();

            var showItem = new System.Windows.Controls.MenuItem
            {
                Header = "Show VoiceLite"
            };
            showItem.Click += (s, e) => ShowMainWindow();

            var settingsItem = new System.Windows.Controls.MenuItem
            {
                Header = "Settings",
                IsEnabled = false
            };

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
            contextMenu.Items.Add(settingsItem);
            contextMenu.Items.Add(separator);
            contextMenu.Items.Add(exitItem);

            trayIcon.ContextMenu = contextMenu;
        }

        public void ShowBalloonTip(string title, string message)
        {
            trayIcon?.ShowBalloonTip(title, message, BalloonIcon.Info);
        }

        private void ShowMainWindow()
        {
            mainWindow.Show();
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Activate();
        }

        public void MinimizeToTray()
        {
            mainWindow.Hide();
            ShowBalloonTip("VoiceLite", "Running in background. Hold Alt to dictate.");
        }

        #region ISystemTrayManager Implementation

        public void Initialize(Window window)
        {
            mainWindow = window;
            InitializeTrayIcon();
        }

        public void SetTrayIconVisible(bool visible)
        {
            if (trayIcon != null)
            {
                trayIcon.Visibility = visible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
        }

        public void RestoreFromTray()
        {
            ShowMainWindow();
        }

        public void UpdateTooltip(string text)
        {
            if (trayIcon != null)
            {
                trayIcon.ToolTipText = text;
            }
        }

        public void ShowNotification(string title, string message, int durationMs = 3000)
        {
            ShowBalloonTip(title, message);
        }

        public void SetRecordingStatus(bool isRecording)
        {
            if (trayIcon != null)
            {
                trayIcon.ToolTipText = isRecording
                    ? "VoiceLite - Recording..."
                    : "VoiceLite - Hold Alt to dictate";

                // Could also change icon color/state here if we had multiple icons
            }
        }

        #endregion

        public void ShowBalloonTip(string title, string message, int durationMs = 3000)
        {
            // Alias for ShowNotification to maintain compatibility
            ShowNotification(title, message, durationMs);
        }

        public void Dispose()
        {
            trayIcon?.Dispose();
            customIcon?.Dispose();
        }
    }
}

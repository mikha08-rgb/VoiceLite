using System;
using System.IO;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;

namespace VoiceLite.Services
{
    public class SystemTrayManager : IDisposable
    {
        private TaskbarIcon? trayIcon;
        private System.Drawing.Icon? customIcon;
        private readonly Window mainWindow;
        private System.Windows.Controls.MenuItem? accountMenuItem;

        public event EventHandler? AccountMenuClicked;

        public SystemTrayManager(Window window)
        {
            mainWindow = window;
            InitializeTrayIcon();
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

            accountMenuItem = new System.Windows.Controls.MenuItem
            {
                Header = "Sign In"
            };
            accountMenuItem.Click += (s, e) => AccountMenuClicked?.Invoke(this, EventArgs.Empty);

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
            contextMenu.Items.Add(accountMenuItem);
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

        public void UpdateAccountMenuText(string text)
        {
            if (accountMenuItem != null)
            {
                accountMenuItem.Header = text;
            }
        }

        public void Dispose()
        {
            trayIcon?.Dispose();
            customIcon?.Dispose();
        }
    }
}

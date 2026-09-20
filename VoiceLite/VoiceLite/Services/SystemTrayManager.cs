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
        private Window? mainWindow;
        private bool updateBalloonPending;
        // Configured hotkey display string (e.g. "Ctrl+Alt+Space"), passed in by
        // MainWindow so tooltips/balloons never claim a hardcoded hotkey.
        private string hotkeyDisplay = "the hotkey";

        /// <summary>
        /// The "update available" balloon was clicked. Raised on the UI thread (the thread that
        /// created the tray icon). MainWindow opens the same UpdateWindow the banner opens.
        /// </summary>
        public event Action? UpdateBalloonClicked;

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

            // Clicking the update balloon opens the in-app updater (verified download), never
            // the raw installer URL in a browser.
            trayIcon.TrayBalloonTipClicked += (s, e) => HandleBalloonClicked();

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
            // A non-update balloon replaces any pending update balloon; clear the flag so
            // clicking this unrelated balloon doesn't open the updater.
            updateBalloonPending = false;
            trayIcon?.ShowBalloonTip(title, message, BalloonIcon.Info);
        }

        public void ShowUpdateAvailable(string version)
        {
            // Stays set until clicked or replaced by another balloon.
            updateBalloonPending = true;
            trayIcon?.ShowBalloonTip(
                "VoiceLite update available",
                $"Version {version} is available. Click to update.",
                BalloonIcon.Info);
        }

        /// <summary>
        /// The installer is downloaded and verified but the install was deferred (Settings open /
        /// dictating) while the window is hidden. Clicking opens the same in-app updater.
        /// </summary>
        public void ShowUpdateReady(string version)
        {
            updateBalloonPending = true;
            trayIcon?.ShowBalloonTip(
                "VoiceLite update ready",
                $"Version {version} is downloaded. Click to install.",
                BalloonIcon.Info);
        }

        /// <summary>
        /// Balloon-click handler body (internal so it is testable without a tray icon).
        /// One-shot: only a click on the update balloon itself raises the event.
        /// </summary>
        internal void HandleBalloonClicked()
        {
            if (!updateBalloonPending) return;
            updateBalloonPending = false;
            try
            {
                UpdateBalloonClicked?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to open the update window from the tray balloon", ex);
            }
        }

        /// <summary>Restores the main window from the tray and brings it to the foreground.</summary>
        public void ShowMainWindow()
        {
            mainWindow?.Show();
            if (mainWindow != null)
            {
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
                // Same Topmost flip as App.xaml.cs' second-instance restore: Activate() alone
                // doesn't always surface the window, and a balloon click that appears to do
                // nothing would be worse than the browser download it replaces.
                mainWindow.Topmost = true;
                mainWindow.Topmost = false;
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

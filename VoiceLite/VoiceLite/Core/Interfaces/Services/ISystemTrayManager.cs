using System;
using System.Windows;

namespace VoiceLite.Core.Interfaces.Services
{
    /// <summary>
    /// Interface for system tray functionality
    /// </summary>
    public interface ISystemTrayManager : IDisposable
    {
        /// <summary>
        /// Initializes the system tray icon
        /// </summary>
        /// <param name="mainWindow">The main application window</param>
        void Initialize(Window mainWindow);

        /// <summary>
        /// Shows or hides the system tray icon
        /// </summary>
        void SetTrayIconVisible(bool visible);

        /// <summary>
        /// Minimizes the application to the system tray
        /// </summary>
        void MinimizeToTray();

        /// <summary>
        /// Restores the application from the system tray
        /// </summary>
        void RestoreFromTray();

        /// <summary>
        /// Updates the tray icon tooltip
        /// </summary>
        void UpdateTooltip(string text);

        /// <summary>
        /// Shows a balloon notification
        /// </summary>
        void ShowNotification(string title, string message, int durationMs = 3000);

        /// <summary>
        /// Updates the tray icon to indicate recording status
        /// </summary>
        void SetRecordingStatus(bool isRecording);
    }
}
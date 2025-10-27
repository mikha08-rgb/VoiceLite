using System;
using System.Threading.Tasks;
using VoiceLite.Core.Interfaces.Services;

namespace VoiceLite.Core.Interfaces.Features
{
    /// <summary>
    /// Interface for application settings management
    /// </summary>
    public interface ISettingsService
    {
        // General Settings
        /// <summary>
        /// Gets or sets whether to minimize to tray
        /// </summary>
        bool MinimizeToTray { get; set; }

        /// <summary>
        /// Gets or sets whether to start minimized
        /// </summary>
        bool StartMinimized { get; set; }

        /// <summary>
        /// Gets or sets whether to start with Windows
        /// </summary>
        bool StartWithWindows { get; set; }

        /// <summary>
        /// Gets or sets whether to close to tray instead of exiting
        /// </summary>
        bool CloseToTray { get; set; }

        /// <summary>
        /// Gets or sets whether to check for updates
        /// </summary>
        bool CheckForUpdates { get; set; }

        // AI Model Settings
        /// <summary>
        /// Gets or sets the selected Whisper model
        /// </summary>
        string SelectedModel { get; set; }

        /// <summary>
        /// Gets or sets the language for transcription
        /// </summary>
        string TranscriptionLanguage { get; set; }

        /// <summary>
        /// Gets or sets whether to use GPU acceleration
        /// </summary>
        bool UseGpuAcceleration { get; set; }

        // Text Injection Settings
        /// <summary>
        /// Gets or sets the text injection mode
        /// </summary>
        ITextInjector.InjectionMode InjectionMode { get; set; }

        /// <summary>
        /// Gets or sets the typing delay in milliseconds
        /// </summary>
        int TypingDelayMs { get; set; }

        /// <summary>
        /// Gets or sets whether to add space after injection
        /// </summary>
        bool AddSpaceAfterInjection { get; set; }

        // Hotkey Settings
        /// <summary>
        /// Gets or sets the global hotkey
        /// </summary>
        string GlobalHotkey { get; set; }

        /// <summary>
        /// Gets or sets the hotkey key
        /// </summary>
        System.Windows.Input.Key HotkeyKey { get; set; }

        /// <summary>
        /// Gets or sets the hotkey modifiers
        /// </summary>
        System.Windows.Input.ModifierKeys HotkeyModifiers { get; set; }

        /// <summary>
        /// Gets or sets whether hotkeys are enabled
        /// </summary>
        bool HotkeysEnabled { get; set; }

        // Audio Settings
        /// <summary>
        /// Gets or sets the audio input device
        /// </summary>
        string AudioInputDevice { get; set; }

        /// <summary>
        /// Gets or sets the audio sample rate
        /// </summary>
        int AudioSampleRate { get; set; }

        /// <summary>
        /// Gets or sets whether to play sound on recording start/stop
        /// </summary>
        bool PlaySoundFeedback { get; set; }

        // History Settings
        /// <summary>
        /// Gets or sets the maximum number of history items
        /// </summary>
        int MaxHistoryItems { get; set; }

        /// <summary>
        /// Gets or sets whether to save history
        /// </summary>
        bool SaveHistory { get; set; }

        // Methods
        /// <summary>
        /// Loads settings from disk
        /// </summary>
        Task LoadSettingsAsync();

        /// <summary>
        /// Saves settings to disk
        /// </summary>
        Task SaveSettingsAsync();

        /// <summary>
        /// Resets all settings to defaults
        /// </summary>
        void ResetToDefaults();

        /// <summary>
        /// Gets the full path to the selected model file
        /// </summary>
        string GetModelPath();

        /// <summary>
        /// Exports settings to a file
        /// </summary>
        Task ExportSettingsAsync(string filePath);

        /// <summary>
        /// Imports settings from a file
        /// </summary>
        Task ImportSettingsAsync(string filePath);

        /// <summary>
        /// Raised when any setting changes
        /// </summary>
        event EventHandler<SettingChangedEventArgs> SettingChanged;
    }

    /// <summary>
    /// Event arguments for setting changes
    /// </summary>
    public class SettingChangedEventArgs : EventArgs
    {
        public string SettingName { get; set; } = string.Empty;
        public object OldValue { get; set; } = new();
        public object NewValue { get; set; } = new();
    }
}
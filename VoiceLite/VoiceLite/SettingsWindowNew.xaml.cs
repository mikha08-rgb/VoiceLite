using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VoiceLite.Models;
using VoiceLite.Services;
using VoiceLite.Utilities;

namespace VoiceLite
{
    public partial class SettingsWindowNew : Window
    {
        private Settings settings;
        private bool isCapturingHotkey = false;
        private Key capturedKey = Key.None;
        private ModifierKeys capturedModifiers = ModifierKeys.None;
        private Action? testRecordingCallback;
        private Action? saveSettingsCallback; // CRITICAL FIX: Callback to persist settings to disk
        private string? originalModel;

        public Settings Settings => settings;

        public SettingsWindowNew(Settings currentSettings, Action? onTestRecording = null, Action? onSaveSettings = null)
        {
            InitializeComponent();
            settings = currentSettings ?? new Settings();
            testRecordingCallback = onTestRecording;
            saveSettingsCallback = onSaveSettings; // Store save callback
            originalModel = settings.WhisperModel;

            LoadSettings();
            LoadVersionInfo();
        }

        private bool isInitialized = false;

        private void LoadSettings()
        {
            // Hotkey Settings
            UpdateHotkeyDisplay(settings.RecordHotkey, settings.HotkeyModifiers);

            // Recording Mode
            if (settings.Mode == RecordMode.PushToTalk)
                PushToTalkRadio.IsChecked = true;
            else
                ToggleRadio.IsChecked = true;

            // System Settings
            // StartWithWindowsCheckBox.IsChecked = settings.StartWithWindows; // Hidden - not implemented
            MinimizeToTrayCheckBox.IsChecked = settings.MinimizeToTray;
            // ShowTrayIconCheckBox.IsChecked = settings.ShowTrayIcon; // Hidden - always enabled

            // Audio Settings
            LoadMicrophones();
            PlaySoundFeedbackCheckBox.IsChecked = settings.PlaySoundFeedback;
            AutoPasteCheckBox.IsChecked = settings.AutoPaste;

            // Advanced settings removed from UI (still configurable via settings.json)

            // Audio Enhancement - sync UI from settings
            SyncAudioUIFromSettings();

            // License Status (Read-Only)
            UpdateLicenseStatusDisplay();

            isInitialized = true;
        }



        private void LoadMicrophones()
        {
            var devices = AudioRecorder.GetAvailableMicrophones();
            var defaultDevice = new AudioDevice { Index = -1, Name = "Default Microphone" };
            devices.Insert(0, defaultDevice);

            MicrophoneComboBox.ItemsSource = devices;

            if (settings.SelectedMicrophoneIndex == -1)
            {
                MicrophoneComboBox.SelectedIndex = 0;
            }
            else
            {
                var selected = devices.FirstOrDefault(d =>
                    d.Index == settings.SelectedMicrophoneIndex ||
                    d.Name == settings.SelectedMicrophoneName);
                if (selected != null)
                {
                    MicrophoneComboBox.SelectedItem = selected;
                }
                else
                {
                    MicrophoneComboBox.SelectedIndex = 0;
                }
            }
        }

        private void UpdateHotkeyDisplay(Key key, ModifierKeys modifiers)
        {
            HotkeyTextBox.Text = HotkeyDisplayHelper.Format(key, modifiers);
        }

        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!isCapturingHotkey)
                return;

            e.Handled = true;

            if (e.Key == Key.Escape)
            {
                UpdateHotkeyDisplay(settings.RecordHotkey, settings.HotkeyModifiers);
                HotkeyTextBox.Background = System.Windows.Media.Brushes.LightGray;
                isCapturingHotkey = false;
                HotkeyInstructionText.Visibility = Visibility.Collapsed;
                return;
            }

            var key = (e.Key == Key.System) ? e.SystemKey : e.Key;

            // CRITICAL FIX: Allow standalone modifier keys to be selected as hotkeys
            // Remove the blocking code that prevented Left Ctrl, Shift, etc. from being selected

            // Special handling for standalone modifier keys and special keys
            if (key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LWin || key == Key.RWin ||
                key == Key.CapsLock) // ADDED: Support for CapsLock key
            {
                // FIXED: Allow these keys to be captured as standalone hotkeys
                capturedKey = key;
                capturedModifiers = ModifierKeys.None; // No additional modifiers for standalone keys
                UpdateHotkeyDisplay(capturedKey, capturedModifiers);
                return;
            }

            // For non-modifier keys, capture with any active modifiers
            capturedKey = key;
            capturedModifiers = Keyboard.Modifiers;
            UpdateHotkeyDisplay(capturedKey, capturedModifiers);
        }

        private void HotkeyTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            isCapturingHotkey = true;
            HotkeyTextBox.Background = System.Windows.Media.Brushes.LightYellow;
            HotkeyInstructionText.Visibility = Visibility.Visible;
        }

        private void HotkeyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            isCapturingHotkey = false;
            HotkeyTextBox.Background = System.Windows.Media.Brushes.LightGray;
            HotkeyInstructionText.Visibility = Visibility.Collapsed;

            if (capturedKey != Key.None)
            {
                settings.RecordHotkey = capturedKey;
                settings.HotkeyModifiers = capturedModifiers;
            }
        }

        private void ClearHotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            capturedKey = Key.LeftAlt;
            capturedModifiers = ModifierKeys.None;
            UpdateHotkeyDisplay(capturedKey, capturedModifiers);
            settings.RecordHotkey = capturedKey;
            settings.HotkeyModifiers = capturedModifiers;
        }

        private void RefreshMicrophonesButton_Click(object sender, RoutedEventArgs e)
        {
            LoadMicrophones();
            StatusText.Text = "Microphone list refreshed";
        }

        private void TestMicrophoneButton_Click(object sender, RoutedEventArgs e)
        {
            // Invoke the test recording callback if provided
            testRecordingCallback?.Invoke();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            saveSettingsCallback?.Invoke(); // CRITICAL FIX: Persist settings to disk immediately
            StatusText.Text = "Settings applied successfully";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            saveSettingsCallback?.Invoke(); // CRITICAL FIX: Persist settings to disk immediately

            // Track analytics if enabled
            _ = TrackAnalyticsChangesAsync();

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SaveSettings()
        {
            // Recording Mode
            settings.Mode = (PushToTalkRadio.IsChecked == true) ? RecordMode.PushToTalk : RecordMode.Toggle;

            // System Settings
            // settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked ?? false; // Hidden - not implemented
            settings.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked ?? true;
            // settings.ShowTrayIcon = ShowTrayIconCheckBox.IsChecked ?? true; // Hidden - always enabled (hardcoded true)

            // Audio Settings
            settings.PlaySoundFeedback = PlaySoundFeedbackCheckBox.IsChecked ?? true;
            settings.AutoPaste = AutoPasteCheckBox.IsChecked ?? true;

            if (MicrophoneComboBox.SelectedItem is AudioDevice selectedDevice)
            {
                settings.SelectedMicrophoneIndex = selectedDevice.Index;
                settings.SelectedMicrophoneName = selectedDevice.Name;
            }

            // Hotkey (already saved on change)
        }

        private async Task TrackAnalyticsChangesAsync() { await Task.CompletedTask; }
        private void LoadVersionInfo() { try { var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version; if (version != null && VersionText != null) { VersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}"; } } catch { } }
        private void SyncAudioUIFromSettings() { }

        // License Status Display (Read-Only)
        private void UpdateLicenseStatusDisplay()
        {
            if (SimpleLicenseStorage.HasValidLicense(out var storedLicense))
            {
                LicenseStatusText.Text = $"✓ Pro License Active";
                LicenseStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
            }
            else
            {
                LicenseStatusText.Text = "Free Version";
                LicenseStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }

        // Event handlers for UI elements
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) { }
        private void DownloadModelsButton_Click(object sender, RoutedEventArgs e) { }
        private void StudioPreset_Click(object sender, RoutedEventArgs e) { }
        private void OfficePreset_Click(object sender, RoutedEventArgs e) { }
        private void DefaultPreset_Click(object sender, RoutedEventArgs e) { }
        private void AudioSetting_Changed(object sender, RoutedEventArgs e) { }
        private void VolumeBoost_Changed(object sender, RoutedEventArgs e) { }
        private void NoiseGate_Changed(object sender, RoutedEventArgs e) { }
        private void TestAudio_Click(object sender, RoutedEventArgs e) { }
        private void AdvancedAudioSetting_Changed(object sender, RoutedEventArgs e) { }
    }
}

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
        private LicenseService? licenseService;
        private ProFeatureService? proFeatureService;

        public Settings Settings => settings;

        public SettingsWindowNew(Settings currentSettings, Action? onTestRecording = null, Action? onSaveSettings = null)
        {
            InitializeComponent();
            settings = currentSettings ?? new Settings();
            testRecordingCallback = onTestRecording;
            saveSettingsCallback = onSaveSettings; // Store save callback
            originalModel = settings.WhisperModel;
            licenseService = new LicenseService();
            proFeatureService = new ProFeatureService(settings);

            LoadSettings();
            LoadVersionInfo();
        }

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
            MinimizeToTrayCheckBox.IsChecked = settings.MinimizeToTray;

            // Audio Settings
            LoadMicrophones();
            AutoPasteCheckBox.IsChecked = settings.AutoPaste;

            // Advanced settings removed from UI (still configurable via settings.json)

            // Audio Enhancement - sync UI from settings
            SyncAudioUIFromSettings();

            // Custom Dictionary - REMOVED (feature simplified away)
            // UpdateDictionaryCount(); // Dead code

            // Current Model is set in SetupModelComparison

            // License Settings
            LoadLicenseStatus();

            // Pro Features - Control visibility of AI Models tab
            UpdateProFeatureVisibility();

            // Initialize Model Download Control (Pro users only)
            if (proFeatureService?.IsProUser == true)
            {
                ModelDownloadControl.Initialize(settings, () => saveSettingsCallback?.Invoke());
            }
        }

        private void LoadLicenseStatus()
        {
            if (settings.IsProLicense)
            {
                LicenseTierText.Text = "Pro ⭐";
                LicenseTierText.Foreground = System.Windows.Media.Brushes.Green;
                LicenseKeyInput.Text = settings.LicenseKey;
                LicenseKeyInput.IsEnabled = false;
                ActivateLicenseButton.IsEnabled = false;
                LicenseStatusText.Text = "✓ License activated";
                LicenseStatusText.Foreground = System.Windows.Media.Brushes.Green;
                LicenseDescriptionText.Text = "Pro tier unlocked! You have access to all features and AI models.";
            }
            else
            {
                LicenseTierText.Text = "Free";
                LicenseTierText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235)); // #2563EB
                LicenseKeyInput.IsEnabled = true;
                ActivateLicenseButton.IsEnabled = true;
                LicenseStatusText.Text = "";
            }
        }

        /// <summary>
        /// Controls visibility of Pro-only features (AI Models tab, etc.)
        /// Free users: AI Models tab hidden
        /// Pro users: AI Models tab visible
        /// </summary>
        private void UpdateProFeatureVisibility()
        {
            if (proFeatureService == null) return;

            // Control AI Models tab visibility
            AIModelsTab.Visibility = proFeatureService.AIModelsTabVisibility;

            // Future: Add more Pro feature visibility controls here
            // Example:
            // VoiceShortcutsTab.Visibility = proFeatureService.VoiceShortcutsTabVisibility;
            // ExportHistoryButton.Visibility = proFeatureService.ExportHistoryButtonVisibility;
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
            settings.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked ?? true;

            // Audio Settings
            settings.AutoPaste = AutoPasteCheckBox.IsChecked ?? true;

            if (MicrophoneComboBox.SelectedItem is AudioDevice selectedDevice)
            {
                settings.SelectedMicrophoneIndex = selectedDevice.Index;
                settings.SelectedMicrophoneName = selectedDevice.Name;
            }

            // Custom Dictionary - REMOVED (feature simplified away)

            // Whisper Model is already saved when selected

            // Advanced settings removed from UI (still work via settings.json)

            // Audio Enhancement - already saved via event handlers, no need to duplicate

            // Hotkey (already saved on change)
        }

        private async Task TrackAnalyticsChangesAsync() { await Task.CompletedTask; }
        private void LoadVersionInfo() { try { var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version; if (version != null && VersionText != null) { VersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}"; } } catch { } }
        private void SyncAudioUIFromSettings() { }

        // License Management Event Handlers
        private async void ActivateLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            var key = LicenseKeyInput.Text.Trim();

            if (string.IsNullOrEmpty(key))
            {
                LicenseStatusText.Text = "Please enter a license key";
                LicenseStatusText.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            // Disable button and show loading state
            ActivateLicenseButton.IsEnabled = false;
            ActivateLicenseButton.Content = "Validating...";
            LicenseStatusText.Text = "Validating license key...";
            LicenseStatusText.Foreground = System.Windows.Media.Brushes.Gray;

            try
            {
                var result = await licenseService!.ValidateLicenseAsync(key);

                if (result.IsValid && result.Tier == "pro")
                {
                    // Activate license
                    settings.LicenseKey = key;
                    settings.IsProLicense = true;

                    // Save settings immediately
                    saveSettingsCallback?.Invoke();

                    // Update UI
                    LoadLicenseStatus();

                    // Show Pro features (AI Models tab)
                    UpdateProFeatureVisibility();

                    // Initialize Model Download Control
                    ModelDownloadControl.Initialize(settings, () => saveSettingsCallback?.Invoke());

                    // Show success message
                    MessageBox.Show(
                        "🎉 License activated successfully!\n\n" +
                        "You now have access to all Pro features and AI models.\n\n" +
                        "⚠️ IMPORTANT: Please close and restart VoiceLite for the changes to take full effect.\n\n" +
                        "After restarting, you can download and select different models in the 'AI Models' tab.",
                        "License Activated - Restart Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    LicenseStatusText.Text = "✓ License activated successfully!";
                    LicenseStatusText.Foreground = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    LicenseStatusText.Text = result.ErrorMessage ?? "✗ Invalid license key";
                    LicenseStatusText.Foreground = System.Windows.Media.Brushes.Red;
                    ActivateLicenseButton.IsEnabled = true;
                    ActivateLicenseButton.Content = "Activate License";
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("License activation failed", ex);
                LicenseStatusText.Text = "✗ Connection error. Please check your internet connection.";
                LicenseStatusText.Foreground = System.Windows.Media.Brushes.Red;
                ActivateLicenseButton.IsEnabled = true;
                ActivateLicenseButton.Content = "Activate License";
            }
        }

        private void BuyProButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open browser to checkout page
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://voicelite.app/#pricing",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to open browser", ex);
                MessageBox.Show(
                    "Could not open browser. Please visit: https://voicelite.app/#pricing",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        // Stub event handlers for removed/unimplemented features
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

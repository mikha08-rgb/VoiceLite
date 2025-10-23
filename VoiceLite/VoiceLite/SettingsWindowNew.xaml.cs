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

            // Custom Dictionary - REMOVED (feature simplified away)
            // UpdateDictionaryCount(); // Dead code

            // Current Model is set in SetupModelComparison

            // License Tab - Initialize
            InitializeLicenseTab();

            // Models Tab - Initialize
            RefreshModelsTab();

            isInitialized = true;
        }

        private void InitializeLicenseTab()
        {
            var featureService = new FeatureService(settings);

            // Update license status text
            LicenseStatusText.Text = "Status: " + featureService.GetLicenseStatusText();

            // Populate license key if exists
            if (!string.IsNullOrEmpty(settings.LicenseKey))
            {
                LicenseKeyInput.Text = settings.LicenseKey;
            }
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

            // Custom Dictionary - REMOVED (feature simplified away)

            // Whisper Model is already saved when selected

            // Advanced settings removed from UI (still work via settings.json)

            // Audio Enhancement - already saved via event handlers, no need to duplicate

            // Hotkey (already saved on change)
        }

        private async Task TrackAnalyticsChangesAsync() { await Task.CompletedTask; }
        private void LoadVersionInfo() { try { var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version; if (version != null && VersionText != null) { VersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}"; } } catch { } }
        private void SyncAudioUIFromSettings() { }

        // === LICENSE TAB EVENT HANDLERS ===

        private void LicenseKeyInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Enable activate button only if key looks valid (basic check)
            var key = LicenseKeyInput.Text.Trim();
            ActivateLicenseButton.IsEnabled = !string.IsNullOrEmpty(key) && key.Length >= 10;
        }

        private async void ActivateLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            var key = LicenseKeyInput.Text.Trim();

            // Disable button during validation
            ActivateLicenseButton.IsEnabled = false;
            LicenseStatusMessage.Text = "Validating license...";
            LicenseStatusMessage.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);

            try
            {
                // Call API to validate license
                var license = await LicenseService.ValidateLicenseAsync(key);

                if (license.Valid)
                {
                    // Save license to settings
                    settings.LicenseKey = key;
                    settings.LicenseFeatures = license.Features;
                    settings.LicenseStatus = license.Status;
                    settings.LicenseLastValidated = DateTime.UtcNow;

                    // Update UI
                    var featureService = new FeatureService(settings);
                    LicenseStatusText.Text = "Status: " + featureService.GetLicenseStatusText();
                    LicenseStatusMessage.Text = "✅ License activated successfully! All Pro features unlocked.";
                    LicenseStatusMessage.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);

                    // Refresh Models tab to show unlocked models
                    RefreshModelsTab();

                    // Save settings immediately
                    saveSettingsCallback?.Invoke();
                }
                else
                {
                    // Invalid license
                    LicenseStatusMessage.Text = $"❌ Invalid license key. {license.Error ?? "Please check and try again."}";
                    LicenseStatusMessage.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                }
            }
            catch (Exception ex)
            {
                // Network error or API down
                LicenseStatusMessage.Text = $"❌ Validation failed: {ex.Message}. Please check your internet connection.";
                LicenseStatusMessage.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            }
            finally
            {
                // Re-enable button
                ActivateLicenseButton.IsEnabled = !string.IsNullOrEmpty(LicenseKeyInput.Text.Trim());
            }
        }

        private void UpgradeLink_Click(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch { }
        }

        // === MODELS TAB EVENT HANDLERS ===

        private void RefreshModelsTab()
        {
            var featureService = new FeatureService(settings);
            bool isPro = featureService.IsPro;

            // Update Pro badges visibility
            SwiftProBadge.Text = isPro ? " ✓ PRO" : " 🔒 PRO";
            ProProBadge.Text = isPro ? " ✓ PRO" : " 🔒 PRO";
            EliteProBadge.Text = isPro ? " ✓ PRO" : " 🔒 PRO";
            UltraProBadge.Text = isPro ? " ✓ PRO" : " 🔒 PRO";

            SwiftProBadge.Foreground = isPro ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
            ProProBadge.Foreground = isPro ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
            EliteProBadge.Foreground = isPro ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
            UltraProBadge.Foreground = isPro ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);

            // Check which models are downloaded
            var whisperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper");

            UpdateModelStatus(SwiftModelStatus, DownloadSwiftButton, Path.Combine(whisperPath, "ggml-base.bin"));
            UpdateModelStatus(ProModelStatus, DownloadProButton, Path.Combine(whisperPath, "ggml-small.bin"));
            UpdateModelStatus(EliteModelStatus, DownloadEliteButton, Path.Combine(whisperPath, "ggml-medium.bin"));
            UpdateModelStatus(UltraModelStatus, DownloadUltraButton, Path.Combine(whisperPath, "ggml-large-v3.bin"));

            // Disable download buttons for free users
            DownloadSwiftButton.IsEnabled = isPro;
            DownloadProButton.IsEnabled = isPro;
            DownloadEliteButton.IsEnabled = isPro;
            DownloadUltraButton.IsEnabled = isPro;

            if (!isPro)
            {
                DownloadSwiftButton.ToolTip = "Upgrade to Pro to download this model";
                DownloadProButton.ToolTip = "Upgrade to Pro to download this model";
                DownloadEliteButton.ToolTip = "Upgrade to Pro to download this model";
                DownloadUltraButton.ToolTip = "Upgrade to Pro to download this model";
            }
            else
            {
                DownloadSwiftButton.ToolTip = null;
                DownloadProButton.ToolTip = null;
                DownloadEliteButton.ToolTip = null;
                DownloadUltraButton.ToolTip = null;
            }
        }

        private void UpdateModelStatus(TextBlock statusText, Button downloadButton, string modelPath)
        {
            bool exists = File.Exists(modelPath);

            if (exists)
            {
                statusText.Text = "Downloaded";
                statusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                downloadButton.Content = "Delete";
            }
            else
            {
                statusText.Text = "Not downloaded";
                statusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
                downloadButton.Content = "Download";
            }
        }

        private async void DownloadSwiftButton_Click(object sender, RoutedEventArgs e)
        {
            await DownloadOrDeleteModel("ggml-base.bin", "Swift", DownloadSwiftButton, SwiftModelStatus);
        }

        private async void DownloadProButton_Click(object sender, RoutedEventArgs e)
        {
            await DownloadOrDeleteModel("ggml-small.bin", "Pro", DownloadProButton, ProModelStatus);
        }

        private async void DownloadEliteButton_Click(object sender, RoutedEventArgs e)
        {
            await DownloadOrDeleteModel("ggml-medium.bin", "Elite", DownloadEliteButton, EliteModelStatus);
        }

        private async void DownloadUltraButton_Click(object sender, RoutedEventArgs e)
        {
            await DownloadOrDeleteModel("ggml-large-v3.bin", "Ultra", DownloadUltraButton, UltraModelStatus);
        }

        private async Task DownloadOrDeleteModel(string modelFileName, string modelName, Button button, TextBlock statusText)
        {
            var whisperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper");
            var modelPath = Path.Combine(whisperPath, modelFileName);

            if (File.Exists(modelPath))
            {
                // Delete model
                try
                {
                    var result = MessageBox.Show(
                        $"Are you sure you want to delete the {modelName} model? You can always download it again later.",
                        "Delete Model",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        File.Delete(modelPath);
                        statusText.Text = "Not downloaded";
                        statusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
                        button.Content = "Download";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to delete model: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                // Download model
                MessageBox.Show(
                    $"Model downloads coming soon!\n\nFor now, please download models manually from:\nhttps://github.com/ggerganov/whisper.cpp/releases\n\nPlace {modelFileName} in the 'whisper' folder.",
                    "Download Model",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            await Task.CompletedTask;
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

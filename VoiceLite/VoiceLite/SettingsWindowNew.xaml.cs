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
            try
            {
                // Hotkey Settings
                UpdateHotkeyDisplay(settings.RecordHotkey, settings.HotkeyModifiers);

                // Recording Mode
                if (PushToTalkRadio != null && ToggleRadio != null)
                {
                    if (settings.Mode == RecordMode.PushToTalk)
                        PushToTalkRadio.IsChecked = true;
                    else
                        ToggleRadio.IsChecked = true;
                }

                // System Settings
                if (MinimizeToTrayCheckBox != null)
                    MinimizeToTrayCheckBox.IsChecked = settings.MinimizeToTray;

                // Transcription Preset
                LoadTranscriptionPreset();

                // Audio Settings
                LoadMicrophones();
                if (AutoPasteCheckBox != null)
                    AutoPasteCheckBox.IsChecked = settings.AutoPaste;

                // Clipboard Delay Settings
                if (ClipboardDelaySlider != null)
                {
                    ClipboardDelaySlider.Value = settings.ClipboardRestorationDelayMs;
                    UpdateClipboardDelayLabel();
                }

                // Audio Enhancement - sync UI from settings
                SyncAudioUIFromSettings();

                // Current Model is set in SetupModelComparison

                // License Settings
                LoadLicenseStatus();

                // Pro Features - Control visibility of AI Models tab
                UpdateProFeatureVisibility();

                // Initialize Model Download Control (applies Pro gating for both free and Pro users)
                // CRITICAL FIX: Must initialize for ALL users to apply visibility gating
                ModelDownloadControl?.Initialize(settings, () => saveSettingsCallback?.Invoke());

                // Load Custom Shortcuts
                LoadShortcuts();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("LoadSettings failed", ex);
                MessageBox.Show($"Error loading settings: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                    "Settings Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        private void LoadTranscriptionPreset()
        {
            // Select the appropriate preset based on settings
            if (TranscriptionPresetComboBox != null)
            {
                string presetTag = settings.TranscriptionPreset.ToString();
                foreach (ComboBoxItem item in TranscriptionPresetComboBox.Items)
                {
                    if (item.Tag?.ToString() == presetTag)
                    {
                        TranscriptionPresetComboBox.SelectedItem = item;
                        break;
                    }
                }
            }

            // Update description
            UpdatePresetDescription(settings.TranscriptionPreset);

            // Load Language selection
            LoadLanguages();

            // Load VAD settings (with null checks for safety)
            if (EnableVADCheckBox != null)
                EnableVADCheckBox.IsChecked = settings.EnableVAD;

            if (VADThresholdSlider != null)
                VADThresholdSlider.Value = settings.VADThreshold;

            if (VADThresholdText != null)
                VADThresholdText.Text = settings.VADThreshold.ToString("F2");

            if (VADSettingsPanel != null)
                VADSettingsPanel.IsEnabled = settings.EnableVAD;
        }

        private void LoadLanguages()
        {
            if (LanguageComboBox == null) return;

            var languages = LanguageInfo.GetSupportedLanguages();
            LanguageComboBox.ItemsSource = languages;

            // Select current language from settings
            var currentLanguage = languages.FirstOrDefault(l => l.Code == settings.Language)
                                ?? languages.FirstOrDefault(l => l.Code == "en");

            LanguageComboBox.SelectedItem = currentLanguage;

            // Update warnings based on current selection
            UpdateLanguageWarnings(settings.Language);
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox?.SelectedItem is LanguageInfo selectedLanguage)
            {
                settings.Language = selectedLanguage.Code;
                UpdateLanguageWarnings(selectedLanguage.Code);
            }
        }

        private void UpdateLanguageWarnings(string languageCode)
        {
            // Show auto-detect latency warning
            if (LanguageDescriptionText != null)
            {
                LanguageDescriptionText.Visibility = languageCode == "auto"
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            // Show base model warning for non-English languages
            if (BaseModelWarningText != null)
            {
                bool isBaseModel = settings.WhisperModel == "ggml-base.bin";
                bool isNonEnglish = languageCode != "en" && languageCode != "auto";
                BaseModelWarningText.Visibility = (isBaseModel && isNonEnglish)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void UpdatePresetDescription(TranscriptionPreset preset)
        {
            if (PresetDescriptionText != null)
            {
                var config = WhisperPresetConfig.GetPresetConfig(preset);
                PresetDescriptionText.Text = config.Description;
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
            settings.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked ?? true;

            // Transcription Preset
            if (TranscriptionPresetComboBox.SelectedItem is ComboBoxItem selectedPreset)
            {
                // MED-6 FIX: Validate parsed enum is actually defined (TryParse accepts undefined int values)
                if (Enum.TryParse<TranscriptionPreset>(selectedPreset.Tag?.ToString(), out var preset) &&
                    Enum.IsDefined(typeof(TranscriptionPreset), preset))
                {
                    settings.TranscriptionPreset = preset;
                }
            }

            // VAD Settings
            settings.EnableVAD = EnableVADCheckBox.IsChecked ?? true;
            settings.VADThreshold = VADThresholdSlider.Value;

            // Audio Settings
            settings.AutoPaste = AutoPasteCheckBox.IsChecked ?? true;
            settings.ClipboardRestorationDelayMs = (int)ClipboardDelaySlider.Value;

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

        private void TranscriptionPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TranscriptionPresetComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                // MED-6 FIX: Validate parsed enum is actually defined
                if (Enum.TryParse<TranscriptionPreset>(selectedItem.Tag?.ToString(), out var preset) &&
                    Enum.IsDefined(typeof(TranscriptionPreset), preset))
                {
                    UpdatePresetDescription(preset);
                }
            }
        }

        private void EnableVADCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (VADSettingsPanel != null)
            {
                VADSettingsPanel.IsEnabled = EnableVADCheckBox.IsChecked ?? false;
            }
        }

        private void VADThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (VADThresholdText != null)
            {
                VADThresholdText.Text = e.NewValue.ToString("F2");
            }
        }

        private void ClipboardDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateClipboardDelayLabel();
        }

        private void UpdateClipboardDelayLabel()
        {
            if (ClipboardDelayLabel != null && ClipboardDelaySlider != null)
            {
                ClipboardDelayLabel.Text = $"{(int)ClipboardDelaySlider.Value}ms";
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

        // ========== Custom Shortcuts Management ==========

        private void LoadShortcuts()
        {
            try
            {
                if (ShortcutsListView != null)
                {
                    RefreshShortcutsList();
                    ShortcutsListView.SelectionChanged += ShortcutsListView_SelectionChanged;
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("LoadShortcuts failed", ex);
            }
        }

        private void RefreshShortcutsList()
        {
            if (ShortcutsListView == null) return;

            ShortcutsListView.ItemsSource = null;
            ShortcutsListView.ItemsSource = settings.CustomShortcuts;
        }

        private void ShortcutsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var hasSelection = ShortcutsListView.SelectedItem != null;
            if (EditShortcutButton != null)
                EditShortcutButton.IsEnabled = hasSelection;
            if (DeleteShortcutButton != null)
                DeleteShortcutButton.IsEnabled = hasSelection;
        }

        private void AddShortcutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Thread-safe: lock while reading shortcuts list
                lock (settings.SyncRoot)
                {
                    var dialog = new ShortcutEditDialog(settings.CustomShortcuts);
                    if (dialog.ShowDialog() == true)
                    {
                        settings.CustomShortcuts.Add(dialog.Shortcut);
                        RefreshShortcutsList();
                        saveSettingsCallback?.Invoke(); // Persist to disk
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("AddShortcutButton_Click failed", ex);
                MessageBox.Show($"Error adding shortcut: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditShortcutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ShortcutsListView.SelectedItem is CustomShortcut selectedShortcut)
                {
                    // Thread-safe: lock while reading shortcuts list
                    lock (settings.SyncRoot)
                    {
                        var dialog = new ShortcutEditDialog(selectedShortcut, settings.CustomShortcuts);
                        if (dialog.ShowDialog() == true)
                        {
                            RefreshShortcutsList();
                            saveSettingsCallback?.Invoke(); // Persist to disk
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("EditShortcutButton_Click failed", ex);
                MessageBox.Show($"Error editing shortcut: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteShortcutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ShortcutsListView.SelectedItem is CustomShortcut selectedShortcut)
                {
                    var result = MessageBox.Show(
                        $"Are you sure you want to delete this shortcut?\n\n{selectedShortcut.Trigger} → {selectedShortcut.Replacement}",
                        "Confirm Delete",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Thread-safe: lock while modifying shortcuts list
                        lock (settings.SyncRoot)
                        {
                            settings.CustomShortcuts.Remove(selectedShortcut);
                            RefreshShortcutsList();
                            saveSettingsCallback?.Invoke(); // Persist to disk
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("DeleteShortcutButton_Click failed", ex);
                MessageBox.Show($"Error deleting shortcut: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShortcutsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ShortcutsListView.SelectedItem != null)
            {
                EditShortcutButton_Click(sender, e);
            }
        }
    }
}

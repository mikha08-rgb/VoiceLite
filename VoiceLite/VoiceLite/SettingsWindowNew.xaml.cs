using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

        public Settings Settings => settings;

        public SettingsWindowNew(Settings currentSettings, Action? onTestRecording = null)
        {
            InitializeComponent();
            settings = currentSettings ?? new Settings();
            testRecordingCallback = onTestRecording;

            DownloadModelsButton.Visibility = Visibility.Visible;
            UpdateModelDownloadButton();

            LoadSettings();
            SetupModelComparison();
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
            StartWithWindowsCheckBox.IsChecked = settings.StartWithWindows;
            MinimizeToTrayCheckBox.IsChecked = settings.MinimizeToTray;
            ShowTrayIconCheckBox.IsChecked = settings.ShowTrayIcon;

            // Audio Settings
            LoadMicrophones();
            PlaySoundFeedbackCheckBox.IsChecked = settings.PlaySoundFeedback;
            AutoPasteCheckBox.IsChecked = settings.AutoPaste;

            // Whisper Parameters
            BeamSizeTextBox.Text = settings.BeamSize.ToString(CultureInfo.InvariantCulture);
            BestOfTextBox.Text = settings.BestOf.ToString(CultureInfo.InvariantCulture);
            TimeoutMultiplierTextBox.Text = settings.WhisperTimeoutMultiplier.ToString("0.##", CultureInfo.InvariantCulture);

            // Audio Preprocessing
            NoiseSuppressionCheckBox.IsChecked = settings.EnableNoiseSuppression;
            AutoGainCheckBox.IsChecked = settings.EnableAutomaticGain;
            TargetRmsTextBox.Text = settings.TargetRmsLevel.ToString("0.###", CultureInfo.InvariantCulture);
            NoiseThresholdTextBox.Text = settings.NoiseGateThreshold.ToString("0.###", CultureInfo.InvariantCulture);

            // Custom Dictionary
            EnableCustomDictionaryCheckBox.IsChecked = settings.EnableCustomDictionary;
            UpdateDictionaryCount();

            // Current Model is set in SetupModelComparison
        }

        private void UpdateDictionaryCount()
        {
            var count = settings.CustomDictionaryEntries?.Count ?? 0;
            var enabledCount = settings.CustomDictionaryEntries?.Count(e => e.IsEnabled) ?? 0;
            DictionaryCountText.Text = count == 0
                ? "No entries loaded"
                : $"{enabledCount} of {count} entries enabled";
        }

        private void SetupModelComparison()
        {
            if (!IsModelInstalled(settings.WhisperModel))
            {
                settings.WhisperModel = "ggml-tiny.bin";
            }

            SimpleModelSelector.SelectedModel = settings.WhisperModel;
        }

        private void UpdateModelDownloadButton()
        {
            var mediumInstalled = IsModelInstalled("ggml-medium.bin");

            if (mediumInstalled)
            {
                DownloadModelsButton.Content = "Medium model installed";
                DownloadModelsButton.IsEnabled = false;
            }
            else
            {
                DownloadModelsButton.Content = "Download Medium Model (1.5GB)";
                DownloadModelsButton.IsEnabled = true;
            }
        }

        private static bool IsModelInstalled(string fileName)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper", fileName);
            return File.Exists(path) || File.Exists(path + ".enc");
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

        private void SimpleModelSelector_ModelSelected(object sender, string modelFileName)
        {
            settings.WhisperModel = modelFileName;
            StatusText.Text = $"Model changed to: {GetModelDisplayName(modelFileName)}";
        }

        private string GetModelDisplayName(string fileName)
        {
            switch (fileName)
            {
                case "ggml-tiny.bin": return "Fastest";
                case "ggml-base.bin": return "Fast";
                case "ggml-small.bin": return "Balanced";
                case "ggml-medium.bin": return "Accurate";
                case "ggml-large-v3.bin": return "Maximum";
                default: return fileName;
            }
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            StatusText.Text = "Settings applied successfully";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
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
            settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked ?? false;
            settings.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked ?? true;
            settings.ShowTrayIcon = ShowTrayIconCheckBox.IsChecked ?? true;

            // Audio Settings
            settings.PlaySoundFeedback = PlaySoundFeedbackCheckBox.IsChecked ?? true;
            settings.AutoPaste = AutoPasteCheckBox.IsChecked ?? true;

            if (MicrophoneComboBox.SelectedItem is AudioDevice selectedDevice)
            {
                settings.SelectedMicrophoneIndex = selectedDevice.Index;
                settings.SelectedMicrophoneName = selectedDevice.Name;
            }

            // Custom Dictionary
            settings.EnableCustomDictionary = EnableCustomDictionaryCheckBox.IsChecked ?? true;

            // Whisper Model is already saved when selected

            // Whisper Parameters
            if (int.TryParse(BeamSizeTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int beamSize))
                settings.BeamSize = Math.Max(1, Math.Min(10, beamSize));

            if (int.TryParse(BestOfTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bestOf))
                settings.BestOf = Math.Max(1, Math.Min(10, bestOf));

            if (double.TryParse(TimeoutMultiplierTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double timeout))
                settings.WhisperTimeoutMultiplier = Math.Max(0.5, Math.Min(10.0, timeout));

            // Audio Preprocessing
            settings.EnableNoiseSuppression = NoiseSuppressionCheckBox.IsChecked ?? false;
            settings.EnableAutomaticGain = AutoGainCheckBox.IsChecked ?? false;

            if (float.TryParse(TargetRmsTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float targetRms))
                settings.TargetRmsLevel = Math.Max(0.01f, Math.Min(1.0f, targetRms));

            if (double.TryParse(NoiseThresholdTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double noiseThreshold))
                settings.NoiseGateThreshold = Math.Max(0.0, Math.Min(0.5, noiseThreshold));

            // Hotkey (already saved on change)
        }

        private void ManageDictionaryButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DictionaryManagerWindow(settings);
            if (dialog.ShowDialog() == true)
            {
                // Refresh the count display
                UpdateDictionaryCount();
                StatusText.Text = "Dictionary updated";
            }
        }

        private void LoadMedicalTemplate_Click(object sender, RoutedEventArgs e)
        {
            LoadTemplate(Models.CustomDictionaryTemplates.GetMedicalTemplate(), "Medical");
        }

        private void LoadLegalTemplate_Click(object sender, RoutedEventArgs e)
        {
            LoadTemplate(Models.CustomDictionaryTemplates.GetLegalTemplate(), "Legal");
        }

        private void LoadTechTemplate_Click(object sender, RoutedEventArgs e)
        {
            LoadTemplate(Models.CustomDictionaryTemplates.GetTechTemplate(), "Tech");
        }

        private void LoadTemplate(List<Models.DictionaryEntry> template, string templateName)
        {
            var result = MessageBox.Show(
                $"Load {template.Count} {templateName} entries?\n\nThis will add new entries without removing existing ones.",
                "Load Template",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                int added = 0;
                foreach (var entry in template)
                {
                    // Avoid duplicates
                    if (!settings.CustomDictionaryEntries.Any(e => e.Pattern.Equals(entry.Pattern, StringComparison.OrdinalIgnoreCase)))
                    {
                        settings.CustomDictionaryEntries.Add(entry);
                        added++;
                    }
                }
                UpdateDictionaryCount();
                StatusText.Text = $"Loaded {added} {templateName} entries";
            }
        }

        private async void DownloadModelsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            if (IsModelInstalled("ggml-medium.bin"))
            {
                UpdateModelDownloadButton();
                StatusText.Text = "Medium model is already available";
                return;
            }

            button.IsEnabled = false;
            button.Content = "Downloading... (this may take several minutes)";

            try
            {
                var models = new[]
                {
                    ("ggml-medium.bin", "https://github.com/mikha08-rgb/VoiceLite/releases/download/v1.0.0/ggml-medium.bin")
                    // Large model (2.9GB) exceeds GitHub's 2GB limit - will be added in future update
                };

                var whisperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper");
                Directory.CreateDirectory(whisperPath);

                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(30);

                    foreach (var (name, url) in models)
                    {
                        var targetPath = Path.Combine(whisperPath, name);

                        if (File.Exists(targetPath))
                        {
                            button.Content = $"{name} already exists, skipping...";
                            await Task.Delay(500);
                            continue;
                        }

                        button.Content = $"Downloading {name}...";
                        using (var response = await client.GetAsync(url, System.Net.Http.HttpCompletionOption.ResponseHeadersRead))
                        {
                            response.EnsureSuccessStatusCode();
                            await using var sourceStream = await response.Content.ReadAsStreamAsync();
                            await using var targetStream = File.Create(targetPath);
                            await sourceStream.CopyToAsync(targetStream);
                        }
                    }
                }

                button.Content = "Download Complete!";
                // Models will be available after restart

                MessageBox.Show("Model downloaded successfully! Restart the application to see it in the model list.",
                               "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                UpdateModelDownloadButton();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Download failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                button.Content = "Download Medium Model (1.5GB)";
                button.IsEnabled = true;
                UpdateModelDownloadButton();
            }
        }
    }
}



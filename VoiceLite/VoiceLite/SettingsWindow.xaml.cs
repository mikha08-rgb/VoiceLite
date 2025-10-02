using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using VoiceLite.Models;
using VoiceLite.Services;
using VoiceLite.Utilities;

namespace VoiceLite
{
    public partial class SettingsWindow : Window
    {
        private Settings settings;
        private bool isCapturingHotkey = false;
        private Key capturedKey = Key.None;
        private ModifierKeys capturedModifiers = ModifierKeys.None;
        private Action? testRecordingCallback;
        private static readonly string[] DefaultModelCandidates = new[]
        {
            "ggml-small.bin",
            "ggml-small.en.bin",
            "ggml-medium.bin",
            "ggml-medium.en.bin",
            "ggml-large-v3.bin"
        };

        public Settings Settings => settings;

        public SettingsWindow(Settings currentSettings, Action? onTestRecording = null)
        {
            InitializeComponent();
            settings = currentSettings ?? new Settings();
            testRecordingCallback = onTestRecording;
            LoadSettings();
        }

        private void LoadSettings()
        {
            UpdateHotkeyDisplay(settings.RecordHotkey, settings.HotkeyModifiers);

            if (settings.Mode == RecordMode.PushToTalk)
                PushToTalkRadio.IsChecked = true;
            else
                ToggleRadio.IsChecked = true;

            StartWithWindowsCheckBox.IsChecked = settings.StartWithWindows;
            MinimizeToTrayCheckBox.IsChecked = settings.MinimizeToTray;
            ShowTrayIconCheckBox.IsChecked = settings.ShowTrayIcon;

            LoadMicrophones();
            LoadWhisperModels();

            WhisperModelComboBox.Text = string.IsNullOrWhiteSpace(settings.WhisperModel)
                ? "ggml-small.bin" // Free tier default (matches Settings.cs)
                : settings.WhisperModel;

            BeamSizeTextBox.Text = settings.BeamSize.ToString(CultureInfo.InvariantCulture);
            BestOfTextBox.Text = settings.BestOf.ToString(CultureInfo.InvariantCulture);
            TimeoutMultiplierTextBox.Text = settings.WhisperTimeoutMultiplier.ToString("0.##", CultureInfo.InvariantCulture);

            NoiseSuppressionCheckBox.IsChecked = settings.EnableNoiseSuppression;
            AutoGainCheckBox.IsChecked = settings.EnableAutomaticGain;
            PlaySoundFeedbackCheckBox.IsChecked = settings.PlaySoundFeedback;
            AutoPasteCheckBox.IsChecked = settings.AutoPaste;
            TargetRmsTextBox.Text = settings.TargetRmsLevel.ToString("0.###", CultureInfo.InvariantCulture);
            NoiseThresholdTextBox.Text = settings.NoiseGateThreshold.ToString("0.###", CultureInfo.InvariantCulture);

            // Load Text Injection Mode
            TextInjectionModeComboBox.SelectedIndex = (int)settings.TextInjectionMode;

            // Load Custom Dictionary settings
            EnableCustomDictionaryCheckBox.IsChecked = settings.EnableCustomDictionary;
            UpdateDictionaryCount();
        }

        private void UpdateDictionaryCount()
        {
            var count = settings.CustomDictionaryEntries?.Count ?? 0;
            var enabledCount = settings.CustomDictionaryEntries?.Count(e => e.IsEnabled) ?? 0;
            DictionaryCountText.Text = count == 0
                ? "No entries loaded"
                : $"{enabledCount} of {count} entries enabled";
        }

        private void LoadMicrophones()
        {
            var devices = AudioRecorder.GetAvailableMicrophones();
            var defaultDevice = new AudioDevice { Index = -1, Name = "Default Microphone" };
            devices.Insert(0, defaultDevice);

            MicrophoneComboBox.ItemsSource = devices;

            if (settings.SelectedMicrophoneIndex == -1)
            {
                MicrophoneComboBox.SelectedItem = defaultDevice;
            }
            else
            {
                var selectedDevice = devices.FirstOrDefault(d => d.Index == settings.SelectedMicrophoneIndex);
                MicrophoneComboBox.SelectedItem = selectedDevice ?? defaultDevice;
            }

            if (devices.Count == 1)
            {
                MicrophoneComboBox.IsEnabled = false;
            }
        }

        private void LoadWhisperModels()
        {
            var models = new List<string>();
            try
            {
                var whisperDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper");
                if (Directory.Exists(whisperDir))
                {
                    models = Directory.GetFiles(whisperDir, "*.bin")
                        .Select(Path.GetFileName)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Select(name => name!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
            }
            catch
            {
                // ignore discovery errors
            }

            foreach (var candidate in DefaultModelCandidates)
            {
                if (!models.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                {
                    models.Add(candidate);
                }
            }

            models = models
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            WhisperModelComboBox.ItemsSource = models;
        }

        private void UpdateHotkeyDisplay(Key key, ModifierKeys modifiers)
        {
            HotkeyTextBox.Text = HotkeyDisplayHelper.Format(key, modifiers);
        }

        private void HotkeyTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            isCapturingHotkey = true;
            HotkeyTextBox.Text = "Press a key...";
            HotkeyInstructionText.Visibility = Visibility.Visible;
        }

        private void HotkeyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            isCapturingHotkey = false;
            HotkeyInstructionText.Visibility = Visibility.Collapsed;

            if (capturedKey == Key.None)
            {
                UpdateHotkeyDisplay(settings.RecordHotkey, settings.HotkeyModifiers);
            }
        }

        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!isCapturingHotkey)
                return;

            e.Handled = true;
            Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;

            // Allow Escape to cancel the hotkey capture
            if (key == Key.None || key == Key.Escape)
            {
                return;
            }

            // Allow all keys including modifiers as standalone hotkeys
            capturedKey = key;

            // If the key is a modifier key, don't capture additional modifiers
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                capturedModifiers = ModifierKeys.None;
            }
            else
            {
                capturedModifiers = Keyboard.Modifiers;
            }

            UpdateHotkeyDisplay(capturedKey, capturedModifiers);

            FocusManager.SetFocusedElement(this, null);
            Keyboard.ClearFocus();
        }

        private void ClearHotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            capturedKey = Key.LeftAlt;
            capturedModifiers = ModifierKeys.None;
            UpdateHotkeyDisplay(capturedKey, capturedModifiers);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (capturedKey != Key.None)
            {
                settings.RecordHotkey = capturedKey;
                settings.HotkeyModifiers = capturedModifiers;
            }

            settings.Mode = (PushToTalkRadio.IsChecked == true) ? RecordMode.PushToTalk : RecordMode.Toggle;

            if (MicrophoneComboBox.SelectedItem is AudioDevice device)
            {
                settings.SelectedMicrophoneIndex = device.Index;
                settings.SelectedMicrophoneName = device.Name;
            }

            settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
            settings.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked == true;
            settings.ShowTrayIcon = ShowTrayIconCheckBox.IsChecked == true;

            if (!string.IsNullOrWhiteSpace(WhisperModelComboBox.Text))
            {
                settings.WhisperModel = WhisperModelComboBox.Text.Trim();
            }

            settings.BeamSize = Clamp(ParseIntOrDefault(BeamSizeTextBox.Text, settings.BeamSize), 1, 10);
            settings.BestOf = Clamp(ParseIntOrDefault(BestOfTextBox.Text, settings.BestOf), 1, 10);
            settings.WhisperTimeoutMultiplier = Math.Max(0.5, ParseDoubleOrDefault(TimeoutMultiplierTextBox.Text, settings.WhisperTimeoutMultiplier));
            settings.EnableNoiseSuppression = NoiseSuppressionCheckBox.IsChecked == true;
            settings.EnableAutomaticGain = AutoGainCheckBox.IsChecked == true;
            settings.PlaySoundFeedback = PlaySoundFeedbackCheckBox.IsChecked == true;
            settings.AutoPaste = AutoPasteCheckBox.IsChecked == true;
            settings.TargetRmsLevel = (float)Math.Clamp(ParseDoubleOrDefault(TargetRmsTextBox.Text, settings.TargetRmsLevel), 0.05, 0.9);
            settings.NoiseGateThreshold = Math.Clamp(ParseDoubleOrDefault(NoiseThresholdTextBox.Text, settings.NoiseGateThreshold), 0.001, 0.2);
            settings.Language = "en";

            // Save Custom Dictionary settings
            settings.EnableCustomDictionary = EnableCustomDictionaryCheckBox.IsChecked == true;

            // Save Text Injection Mode
            settings.TextInjectionMode = (TextInjectionMode)TextInjectionModeComboBox.SelectedIndex;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void RefreshMicrophonesButton_Click(object sender, RoutedEventArgs e)
        {
            LoadMicrophones();
        }

        private void TestMicrophoneButton_Click(object sender, RoutedEventArgs e)
        {
            // Invoke the test recording callback if provided
            testRecordingCallback?.Invoke();
        }

        private void ManageDictionaryButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DictionaryManagerWindow(settings);
            if (dialog.ShowDialog() == true)
            {
                // Refresh the count display
                UpdateDictionaryCount();
            }
        }

        private void SendFeedbackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get app version
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

                // Get OS version
                var osVersion = $"{Environment.OSVersion.Platform} {Environment.OSVersion.Version}";

                // Build feedback URL with pre-filled data
                var feedbackUrl = $"https://voicelite.app/feedback?source=desktop&version={Uri.EscapeDataString(version)}&os={Uri.EscapeDataString(osVersion)}";

                // Open URL in default browser
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = feedbackUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to open feedback page: {ex.Message}\n\nPlease visit: https://voicelite.app/feedback",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private static int ParseIntOrDefault(string? value, int fallback)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
        }

        private static double ParseDoubleOrDefault(string? value, double fallback)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}

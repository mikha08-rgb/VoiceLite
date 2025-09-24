using System;
using System.Globalization;
using System.IO;
using System.Linq;
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

        public Settings Settings => settings;

        public SettingsWindowNew(Settings currentSettings)
        {
            InitializeComponent();
            settings = currentSettings ?? new Settings();
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

            // Current Model is set in SetupModelComparison
        }

        private void SetupModelComparison()
        {
            // Set the current model in the simple selector
            SimpleModelSelector.SelectedModel = settings.WhisperModel;
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
            capturedKey = Key.F1;
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
    }
}
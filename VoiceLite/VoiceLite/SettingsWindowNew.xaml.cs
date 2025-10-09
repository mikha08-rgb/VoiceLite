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

        public SettingsWindowNew(Settings currentSettings, AnalyticsService? analytics = null, Action? onTestRecording = null, Action? onSaveSettings = null)
        {
            InitializeComponent();
            settings = currentSettings ?? new Settings();
            testRecordingCallback = onTestRecording;
            saveSettingsCallback = onSaveSettings; // Store save callback
            originalModel = settings.WhisperModel;

            DownloadModelsButton.Visibility = Visibility.Visible;
            UpdateModelDownloadButton();

            LoadSettings();
            SetupModelComparison();
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

            // Whisper Parameters
            BeamSizeTextBox.Text = settings.BeamSize.ToString(CultureInfo.InvariantCulture);
            BestOfTextBox.Text = settings.BestOf.ToString(CultureInfo.InvariantCulture);
            TimeoutMultiplierTextBox.Text = settings.WhisperTimeoutMultiplier.ToString("0.##", CultureInfo.InvariantCulture);

            // Audio Enhancement - sync UI from settings
            SyncAudioUIFromSettings();

            // Custom Dictionary
            EnableCustomDictionaryCheckBox.IsChecked = settings.EnableCustomDictionary;
            UpdateDictionaryCount();

            // Current Model is set in SetupModelComparison

            isInitialized = true;
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
            var tinyInstalled = IsModelInstalled("ggml-tiny.bin");
            var mediumInstalled = IsModelInstalled("ggml-medium.bin");

            if (tinyInstalled && mediumInstalled)
            {
                DownloadModelsButton.Content = "All models installed";
                DownloadModelsButton.IsEnabled = false;
            }
            else if (!tinyInstalled && !mediumInstalled)
            {
                DownloadModelsButton.Content = "Download Tiny + Medium Models (1.6GB)";
                DownloadModelsButton.IsEnabled = true;
            }
            else if (!tinyInstalled)
            {
                DownloadModelsButton.Content = "Download Tiny Model (75MB)";
                DownloadModelsButton.IsEnabled = true;
            }
            else // !mediumInstalled
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

            // Audio Enhancement - already saved via event handlers, no need to duplicate

            // Hotkey (already saved on change)
        }

        private void ManageDictionaryButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DictionaryManagerWindow(settings);
            if (dialog.ShowDialog() == true)
            {
                // Refresh the count display
                UpdateDictionaryCount();
                saveSettingsCallback?.Invoke(); // CRITICAL FIX: Persist dictionary changes immediately
                StatusText.Text = "Dictionary updated and saved";
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
                saveSettingsCallback?.Invoke(); // CRITICAL FIX: Persist template entries immediately
                StatusText.Text = $"Loaded {added} {templateName} entries and saved";
            }
        }

        private async void DownloadModelsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            var tinyInstalled = IsModelInstalled("ggml-tiny.bin");
            var mediumInstalled = IsModelInstalled("ggml-medium.bin");

            if (tinyInstalled && mediumInstalled)
            {
                UpdateModelDownloadButton();
                StatusText.Text = "All models are already installed";
                return;
            }

            button.IsEnabled = false;
            button.Content = "Downloading... (this may take several minutes)";

            try
            {
                // Build list of models to download based on what's missing
                var modelsToDownload = new List<(string, string)>();

                if (!tinyInstalled)
                {
                    modelsToDownload.Add(("ggml-tiny.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin"));
                }

                if (!mediumInstalled)
                {
                    modelsToDownload.Add(("ggml-medium.bin", "https://github.com/mikha08-rgb/VoiceLite/releases/download/v1.0.0/ggml-medium.bin"));
                }

                var models = modelsToDownload.ToArray();
                // Large model (2.9GB) exceeds GitHub's 2GB limit - will be added in future update

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

        private void EnableAnalyticsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // Analytics removed - no action needed
        }

        private async Task TrackAnalyticsChangesAsync()
        {
            // Analytics removed - no action needed
            await Task.CompletedTask;
        }

        private void LoadVersionInfo()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"v{version?.Major}.{version?.Minor}.{version?.Build}";
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+S to save and close
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                SaveButton_Click(this, new RoutedEventArgs());
            }
            // Escape to cancel and close
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CancelButton_Click(this, new RoutedEventArgs());
            }
        }

        #region Audio Enhancement

        private void SyncAudioUIFromSettings()
        {
            // Sync checkboxes
            NoiseSuppressionCheckBox.IsChecked = settings.EnableNoiseSuppression;
            AutoGainCheckBox.IsChecked = settings.EnableAutomaticGain;
            VADCheckBox.IsChecked = settings.UseVAD;

            // Sync sliders
            VolumeBoostSlider.Value = settings.TargetRmsLevel;
            NoiseGateSlider.Value = settings.NoiseGateThreshold;

            // Sync textboxes
            TargetRmsTextBox.Text = settings.TargetRmsLevel.ToString("0.##", CultureInfo.InvariantCulture);
            NoiseThresholdTextBox.Text = settings.NoiseGateThreshold.ToString("0.###", CultureInfo.InvariantCulture);

            // Update labels
            VolumeBoostLabel.Text = GetVolumeBoostLabel(settings.TargetRmsLevel);
            NoiseGateLabel.Text = GetNoiseGateLabel(settings.NoiseGateThreshold);

            // Highlight active preset button
            HighlightPresetButton(settings.CurrentAudioPreset);
        }

        private void HighlightPresetButton(AudioPreset preset)
        {
            // Reset all buttons to normal weight
            StudioPresetButton.FontWeight = FontWeights.Normal;
            OfficePresetButton.FontWeight = FontWeights.Normal;
            DefaultPresetButton.FontWeight = FontWeights.Normal;

            // Highlight active button
            switch (preset)
            {
                case AudioPreset.StudioQuality:
                    StudioPresetButton.FontWeight = FontWeights.Bold;
                    break;
                case AudioPreset.OfficeNoisy:
                    OfficePresetButton.FontWeight = FontWeights.Bold;
                    break;
                case AudioPreset.Default:
                    DefaultPresetButton.FontWeight = FontWeights.Bold;
                    break;
                case AudioPreset.Custom:
                    // No button highlighted for custom
                    break;
            }
        }

        private void StudioPreset_Click(object sender, RoutedEventArgs e)
        {
            settings.ApplyAudioPreset(AudioPreset.StudioQuality);
            SyncAudioUIFromSettings();
            StatusText.Text = "Studio Quality preset applied";
        }

        private void OfficePreset_Click(object sender, RoutedEventArgs e)
        {
            settings.ApplyAudioPreset(AudioPreset.OfficeNoisy);
            SyncAudioUIFromSettings();
            StatusText.Text = "Office/Noisy preset applied";
        }

        private void DefaultPreset_Click(object sender, RoutedEventArgs e)
        {
            settings.ApplyAudioPreset(AudioPreset.Default);
            SyncAudioUIFromSettings();
            StatusText.Text = "Default preset applied";
        }

        private void VolumeBoost_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (VolumeBoostLabel == null || !isInitialized) return; // Not initialized

            var value = (float)VolumeBoostSlider.Value;
            settings.TargetRmsLevel = value; // Automatically clamped
            settings.CurrentAudioPreset = AudioPreset.Custom;

            // Update label text based on value ranges
            VolumeBoostLabel.Text = GetVolumeBoostLabel(value);

            // Sync advanced textbox
            TargetRmsTextBox.Text = value.ToString("F2", CultureInfo.InvariantCulture);

            // Update preset button highlighting
            HighlightPresetButton(AudioPreset.Custom);
        }

        private string GetVolumeBoostLabel(float value)
        {
            if (value < 0.15f) return "Very Light";
            if (value < 0.25f) return "Light";
            if (value < 0.35f) return "Medium";
            if (value < 0.50f) return "Strong";
            return "Maximum";
        }

        private void NoiseGate_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (NoiseGateLabel == null || !isInitialized) return; // Not initialized

            var value = NoiseGateSlider.Value;
            settings.NoiseGateThreshold = value; // Automatically clamped
            settings.CurrentAudioPreset = AudioPreset.Custom;

            // Update label text based on value ranges
            NoiseGateLabel.Text = GetNoiseGateLabel(value);

            // Sync advanced textbox
            NoiseThresholdTextBox.Text = value.ToString("F3", CultureInfo.InvariantCulture);

            // Update preset button highlighting
            HighlightPresetButton(AudioPreset.Custom);
        }

        private string GetNoiseGateLabel(double value)
        {
            if (value < 0.01) return "Silent";
            if (value < 0.03) return "Light";
            if (value < 0.06) return "Medium";
            if (value < 0.10) return "Aggressive";
            return "Maximum";
        }

        private void AudioSetting_Changed(object sender, RoutedEventArgs e)
        {
            if (settings == null || !isInitialized) return;

            settings.EnableAutomaticGain = AutoGainCheckBox.IsChecked ?? false;
            settings.EnableNoiseSuppression = NoiseSuppressionCheckBox.IsChecked ?? false;
            settings.UseVAD = VADCheckBox.IsChecked ?? true;
            settings.CurrentAudioPreset = AudioPreset.Custom;

            // Update preset button highlighting
            HighlightPresetButton(AudioPreset.Custom);
        }

        private async void TestAudio_Click(object sender, RoutedEventArgs e)
        {
            TestAudioButton.IsEnabled = false;
            TestResultText.Visibility = Visibility.Visible;
            TestResultText.Text = "Recording 3 seconds... Speak now!";

            try
            {
                var tempFile = Path.Combine(Path.GetTempPath(), $"voicelite_test_{Guid.NewGuid()}.wav");

                // Record 3 seconds
                using (var recorder = new AudioRecorder())
                {
                    recorder.StartRecording();
                    await Task.Delay(3000);
                    recorder.StopRecording();
                    await Task.Delay(500); // Wait for file write
                }

                // Process with current settings (if file exists)
                if (File.Exists(tempFile))
                {
                    var stats = AudioPreprocessor.ProcessAudioFileWithStats(tempFile, settings);

                    // Display results
                    var result = "✅ Test complete!\n";
                    result += $"Duration: {stats.OriginalDurationSeconds:F2}s";
                    if (stats.TrimmedSilenceMs > 0)
                        result += $" → {stats.ProcessedDurationSeconds:F2}s (trimmed {stats.TrimmedSilenceMs:F0}ms)";
                    result += $"\nPeak: {stats.OriginalPeakLevel:F2}";
                    if (stats.AutoGainApplied)
                        result += $" → {stats.ProcessedPeakLevel:F2}";
                    if (stats.NoiseSuppressionApplied)
                        result += "\nNoise reduction: ✅ Applied";

                    TestResultText.Text = result;

                    // Cleanup
                    try { File.Delete(tempFile); } catch { }
                }
                else
                {
                    TestResultText.Text = "❌ No audio file created";
                }
            }
            catch (Exception ex)
            {
                TestResultText.Text = $"❌ Test failed: {ex.Message}";
                ErrorLogger.LogError("AudioTest", ex);
            }
            finally
            {
                TestAudioButton.IsEnabled = true;
            }
        }

        private void AdvancedAudioSetting_Changed(object sender, TextChangedEventArgs e)
        {
            if (settings == null || !isInitialized) return;

            // Parse and clamp, sync with sliders
            if (float.TryParse(TargetRmsTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float rms))
            {
                settings.TargetRmsLevel = rms; // Auto-clamped by Settings.cs
                VolumeBoostSlider.Value = settings.TargetRmsLevel;
            }

            if (double.TryParse(NoiseThresholdTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double threshold))
            {
                settings.NoiseGateThreshold = threshold; // Auto-clamped
                NoiseGateSlider.Value = settings.NoiseGateThreshold;
            }

            settings.CurrentAudioPreset = AudioPreset.Custom;
            HighlightPresetButton(AudioPreset.Custom);
        }

        #endregion
    }
}



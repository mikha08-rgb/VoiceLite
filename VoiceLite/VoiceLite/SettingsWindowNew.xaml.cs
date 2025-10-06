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
        private AnalyticsService? analyticsService;
        private string? originalModel;
        private bool originalUseWhisperServer;

        public Settings Settings => settings;

        public SettingsWindowNew(Settings currentSettings, AnalyticsService? analytics = null, Action? onTestRecording = null, Action? onSaveSettings = null)
        {
            InitializeComponent();
            settings = currentSettings ?? new Settings();
            analyticsService = analytics;
            testRecordingCallback = onTestRecording;
            saveSettingsCallback = onSaveSettings; // Store save callback
            originalModel = settings.WhisperModel;
            originalUseWhisperServer = settings.UseWhisperServer;

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

            // Analytics (Privacy)
            EnableAnalyticsCheckBox.IsChecked = settings.EnableAnalytics ?? false;

            // Performance Settings
            UseWhisperServerCheckBox.IsChecked = settings.UseWhisperServer;

            // Text Formatting (Post-Processing)
            LoadTextFormattingSettings();

            // UI Preset (Appearance)
            LoadUIPresetSettings();

            // Current Model is set in SetupModelComparison

            isInitialized = true;
        }

        private void LoadUIPresetSettings()
        {
            // Set radio button based on current UI preset
            switch (settings.UIPreset)
            {
                case UIPreset.Default:
                    PresetDefaultRadio.IsChecked = true;
                    break;
                case UIPreset.Compact:
                    PresetCompactRadio.IsChecked = true;
                    break;
                default:
                    PresetDefaultRadio.IsChecked = true;
                    break;
            }
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
            saveSettingsCallback?.Invoke(); // CRITICAL FIX: Persist settings to disk immediately
            StatusText.Text = "Settings applied successfully";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            saveSettingsCallback?.Invoke(); // CRITICAL FIX: Persist settings to disk immediately

            // Check if UseWhisperServer changed - requires restart
            if (settings.UseWhisperServer != originalUseWhisperServer)
            {
                MessageBox.Show(
                    "Whisper Server mode change requires an app restart to take effect.",
                    "Restart Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

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

            // Analytics (Privacy)
            settings.EnableAnalytics = EnableAnalyticsCheckBox.IsChecked;
            if (settings.EnableAnalytics.HasValue && settings.AnalyticsConsentDate == null)
            {
                settings.AnalyticsConsentDate = DateTime.UtcNow;
            }

            // Text Formatting (Post-Processing)
            SaveTextFormattingSettings();

            // Whisper Model is already saved when selected

            // Whisper Parameters
            if (int.TryParse(BeamSizeTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int beamSize))
                settings.BeamSize = Math.Max(1, Math.Min(10, beamSize));

            if (int.TryParse(BestOfTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bestOf))
                settings.BestOf = Math.Max(1, Math.Min(10, bestOf));

            if (double.TryParse(TimeoutMultiplierTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double timeout))
                settings.WhisperTimeoutMultiplier = Math.Max(0.5, Math.Min(10.0, timeout));

            // Audio Enhancement - already saved via event handlers, no need to duplicate

            // Performance Settings
            settings.UseWhisperServer = UseWhisperServerCheckBox.IsChecked ?? false;

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

        private void EnableAnalyticsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // This event handler allows users to toggle analytics from settings
            // The actual save happens in SaveSettings() when they click Save/Apply
        }

        private async Task TrackAnalyticsChangesAsync()
        {
            if (analyticsService == null)
                return;

            // Track model changes
            if (originalModel != null && originalModel != settings.WhisperModel)
            {
                await analyticsService.TrackModelChangeAsync(originalModel, settings.WhisperModel);
            }

            // Track settings save
            await analyticsService.TrackSettingsChangeAsync("settings_saved");
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

        #region Text Formatting Tab

        private void LoadTextFormattingSettings()
        {
            var postProc = settings.PostProcessing;

            // Capitalization
            EnableCapitalizationCheckBox.IsChecked = postProc.EnableCapitalization;
            CapFirstLetterCheckBox.IsChecked = postProc.CapitalizeFirstLetter;
            CapAfterPeriodCheckBox.IsChecked = postProc.CapitalizeAfterPeriod;
            CapAfterQuestionCheckBox.IsChecked = postProc.CapitalizeAfterQuestionExclamation;

            // Ending Punctuation
            EnableEndingPunctuationCheckBox.IsChecked = postProc.EnableEndingPunctuation;
            SmartPunctuationCheckBox.IsChecked = postProc.UseSmartPunctuation;

            switch (postProc.DefaultPunctuation)
            {
                case EndingPunctuationType.Period:
                    PeriodRadio.IsChecked = true;
                    break;
                case EndingPunctuationType.Question:
                    QuestionRadio.IsChecked = true;
                    break;
                case EndingPunctuationType.Exclamation:
                    ExclamationRadio.IsChecked = true;
                    break;
            }

            // Filler Words
            FillerIntensitySlider.Value = (int)postProc.FillerRemovalIntensity;
            UpdateFillerIntensityLabel();

            HesitationsCheckBox.IsChecked = postProc.EnabledLists.Hesitations;
            VerbalTicsCheckBox.IsChecked = postProc.EnabledLists.VerbalTics;
            QualifiersCheckBox.IsChecked = postProc.EnabledLists.Qualifiers;
            IntensifiersCheckBox.IsChecked = postProc.EnabledLists.Intensifiers;
            TransitionsCheckBox.IsChecked = postProc.EnabledLists.Transitions;

            // Contractions
            switch (postProc.ContractionHandling)
            {
                case ContractionMode.LeaveAsIs:
                    LeaveAsIsRadio.IsChecked = true;
                    break;
                case ContractionMode.Expand:
                    ExpandRadio.IsChecked = true;
                    break;
                case ContractionMode.Contract:
                    ContractRadio.IsChecked = true;
                    break;
            }

            // Grammar
            FixHomophonesCheckBox.IsChecked = postProc.FixHomophones;
            FixDoubleNegativesCheckBox.IsChecked = postProc.FixDoubleNegatives;
            FixSubjectVerbCheckBox.IsChecked = postProc.FixSubjectVerbAgreement;

            // Initialize preview
            UpdatePreview(null, null);
        }

        private void SaveTextFormattingSettings()
        {
            var postProc = settings.PostProcessing;

            // Capitalization
            postProc.EnableCapitalization = EnableCapitalizationCheckBox.IsChecked == true;
            postProc.CapitalizeFirstLetter = CapFirstLetterCheckBox.IsChecked == true;
            postProc.CapitalizeAfterPeriod = CapAfterPeriodCheckBox.IsChecked == true;
            postProc.CapitalizeAfterQuestionExclamation = CapAfterQuestionCheckBox.IsChecked == true;

            // Ending Punctuation
            postProc.EnableEndingPunctuation = EnableEndingPunctuationCheckBox.IsChecked == true;
            postProc.UseSmartPunctuation = SmartPunctuationCheckBox.IsChecked == true;

            if (PeriodRadio.IsChecked == true)
                postProc.DefaultPunctuation = EndingPunctuationType.Period;
            else if (QuestionRadio.IsChecked == true)
                postProc.DefaultPunctuation = EndingPunctuationType.Question;
            else if (ExclamationRadio.IsChecked == true)
                postProc.DefaultPunctuation = EndingPunctuationType.Exclamation;

            // Filler Words
            postProc.FillerRemovalIntensity = (FillerWordRemovalLevel)(int)FillerIntensitySlider.Value;
            postProc.EnabledLists.Hesitations = HesitationsCheckBox.IsChecked == true;
            postProc.EnabledLists.VerbalTics = VerbalTicsCheckBox.IsChecked == true;
            postProc.EnabledLists.Qualifiers = QualifiersCheckBox.IsChecked == true;
            postProc.EnabledLists.Intensifiers = IntensifiersCheckBox.IsChecked == true;
            postProc.EnabledLists.Transitions = TransitionsCheckBox.IsChecked == true;

            // Contractions
            if (LeaveAsIsRadio.IsChecked == true)
                postProc.ContractionHandling = ContractionMode.LeaveAsIs;
            else if (ExpandRadio.IsChecked == true)
                postProc.ContractionHandling = ContractionMode.Expand;
            else if (ContractRadio.IsChecked == true)
                postProc.ContractionHandling = ContractionMode.Contract;

            // Grammar
            postProc.FixHomophones = FixHomophonesCheckBox.IsChecked == true;
            postProc.FixDoubleNegatives = FixDoubleNegativesCheckBox.IsChecked == true;
            postProc.FixSubjectVerbAgreement = FixSubjectVerbCheckBox.IsChecked == true;
        }

        private void UpdatePreview(object? sender, RoutedEventArgs? e)
        {
            if (PreviewInputTextBox == null || PreviewBeforeText == null || PreviewAfterText == null)
                return;

            var input = PreviewInputTextBox.Text;
            PreviewBeforeText.Text = input;

            // Build PostProcessingSettings from current UI state
            var previewSettings = new PostProcessingSettings
            {
                EnableCapitalization = EnableCapitalizationCheckBox?.IsChecked == true,
                CapitalizeFirstLetter = CapFirstLetterCheckBox?.IsChecked == true,
                CapitalizeAfterPeriod = CapAfterPeriodCheckBox?.IsChecked == true,
                CapitalizeAfterQuestionExclamation = CapAfterQuestionCheckBox?.IsChecked == true,
                EnableEndingPunctuation = EnableEndingPunctuationCheckBox?.IsChecked == true,
                UseSmartPunctuation = SmartPunctuationCheckBox?.IsChecked == true,
                DefaultPunctuation = GetSelectedPunctuationType(),
                FillerRemovalIntensity = GetSelectedFillerLevel(),
                EnabledLists = new FillerWordLists
                {
                    Hesitations = HesitationsCheckBox?.IsChecked == true,
                    VerbalTics = VerbalTicsCheckBox?.IsChecked == true,
                    Qualifiers = QualifiersCheckBox?.IsChecked == true,
                    Intensifiers = IntensifiersCheckBox?.IsChecked == true,
                    Transitions = TransitionsCheckBox?.IsChecked == true,
                    // Copy word lists from settings
                    HesitationWords = settings.PostProcessing.EnabledLists.HesitationWords,
                    VerbalTicWords = settings.PostProcessing.EnabledLists.VerbalTicWords,
                    QualifierWords = settings.PostProcessing.EnabledLists.QualifierWords,
                    IntensifierWords = settings.PostProcessing.EnabledLists.IntensifierWords,
                    TransitionWords = settings.PostProcessing.EnabledLists.TransitionWords
                },
                CustomFillerWords = settings.PostProcessing.CustomFillerWords,
                ContractionHandling = GetSelectedContractionMode(),
                FixHomophones = FixHomophonesCheckBox?.IsChecked == true,
                FixDoubleNegatives = FixDoubleNegativesCheckBox?.IsChecked == true,
                FixSubjectVerbAgreement = FixSubjectVerbCheckBox?.IsChecked == true
            };

            // Process and display result
            var processed = Services.TranscriptionPostProcessor.ProcessTranscription(
                input,
                useEnhancedDictionary: false,
                customDictionary: null,
                postProcessingSettings: previewSettings
            );

            PreviewAfterText.Text = processed;
        }

        private EndingPunctuationType GetSelectedPunctuationType()
        {
            if (PeriodRadio?.IsChecked == true) return EndingPunctuationType.Period;
            if (QuestionRadio?.IsChecked == true) return EndingPunctuationType.Question;
            if (ExclamationRadio?.IsChecked == true) return EndingPunctuationType.Exclamation;
            return EndingPunctuationType.Period;
        }

        private FillerWordRemovalLevel GetSelectedFillerLevel()
        {
            if (FillerIntensitySlider == null) return FillerWordRemovalLevel.None;
            return (FillerWordRemovalLevel)(int)FillerIntensitySlider.Value;
        }

        private ContractionMode GetSelectedContractionMode()
        {
            if (LeaveAsIsRadio?.IsChecked == true) return ContractionMode.LeaveAsIs;
            if (ExpandRadio?.IsChecked == true) return ContractionMode.Expand;
            if (ContractRadio?.IsChecked == true) return ContractionMode.Contract;
            return ContractionMode.LeaveAsIs;
        }

        private void FillerIntensity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateFillerIntensityLabel();

            // Auto-enable/disable checkboxes based on intensity level
            if (FillerIntensitySlider.Value == 0) // None
            {
                // Disable all checkboxes
                if (HesitationsCheckBox != null) HesitationsCheckBox.IsChecked = false;
                if (VerbalTicsCheckBox != null) VerbalTicsCheckBox.IsChecked = false;
                if (QualifiersCheckBox != null) QualifiersCheckBox.IsChecked = false;
                if (IntensifiersCheckBox != null) IntensifiersCheckBox.IsChecked = false;
                if (TransitionsCheckBox != null) TransitionsCheckBox.IsChecked = false;
            }
            else if (FillerIntensitySlider.Value == 1) // Light
            {
                if (HesitationsCheckBox != null) HesitationsCheckBox.IsChecked = true;
            }
            else if (FillerIntensitySlider.Value == 2) // Moderate
            {
                if (HesitationsCheckBox != null) HesitationsCheckBox.IsChecked = true;
                if (VerbalTicsCheckBox != null) VerbalTicsCheckBox.IsChecked = true;
            }
            else if (FillerIntensitySlider.Value == 3) // Aggressive
            {
                if (HesitationsCheckBox != null) HesitationsCheckBox.IsChecked = true;
                if (VerbalTicsCheckBox != null) VerbalTicsCheckBox.IsChecked = true;
                if (QualifiersCheckBox != null) QualifiersCheckBox.IsChecked = true;
                if (IntensifiersCheckBox != null) IntensifiersCheckBox.IsChecked = true;
                if (TransitionsCheckBox != null) TransitionsCheckBox.IsChecked = true;
            }
            // Custom mode (4) - user manages checkboxes manually

            UpdatePreview(null, null);
        }

        private void UpdateFillerIntensityLabel()
        {
            if (FillerIntensityLabel == null || FillerIntensitySlider == null) return;

            FillerIntensityLabel.Text = (int)FillerIntensitySlider.Value switch
            {
                0 => "None",
                1 => "Light",
                2 => "Moderate",
                3 => "Aggressive",
                4 => "Custom",
                _ => "None"
            };
        }

        private void ProfessionalPreset_Click(object sender, RoutedEventArgs e)
        {
            // Professional: Remove all fillers, expand contractions, fix grammar
            EnableCapitalizationCheckBox.IsChecked = true;
            CapFirstLetterCheckBox.IsChecked = true;
            CapAfterPeriodCheckBox.IsChecked = true;
            CapAfterQuestionCheckBox.IsChecked = true;
            EnableEndingPunctuationCheckBox.IsChecked = true;
            PeriodRadio.IsChecked = true;
            FillerIntensitySlider.Value = 3; // Aggressive
            ExpandRadio.IsChecked = true;
            FixHomophonesCheckBox.IsChecked = true;
            FixDoubleNegativesCheckBox.IsChecked = true;
            FixSubjectVerbCheckBox.IsChecked = true;

            UpdatePreview(null, null);
        }

        private void CodePreset_Click(object sender, RoutedEventArgs e)
        {
            // Code: Preserve casing, no punctuation, no filler removal
            EnableCapitalizationCheckBox.IsChecked = false;
            EnableEndingPunctuationCheckBox.IsChecked = false;
            FillerIntensitySlider.Value = 0; // None
            LeaveAsIsRadio.IsChecked = true;
            FixHomophonesCheckBox.IsChecked = false;
            FixDoubleNegativesCheckBox.IsChecked = false;
            FixSubjectVerbCheckBox.IsChecked = false;

            UpdatePreview(null, null);
        }

        private void CasualPreset_Click(object sender, RoutedEventArgs e)
        {
            // Casual: Light filler removal, keep contractions
            EnableCapitalizationCheckBox.IsChecked = true;
            CapFirstLetterCheckBox.IsChecked = true;
            CapAfterPeriodCheckBox.IsChecked = true;
            CapAfterQuestionCheckBox.IsChecked = false;
            EnableEndingPunctuationCheckBox.IsChecked = true;
            PeriodRadio.IsChecked = true;
            FillerIntensitySlider.Value = 1; // Light
            LeaveAsIsRadio.IsChecked = true;
            FixHomophonesCheckBox.IsChecked = false;
            FixDoubleNegativesCheckBox.IsChecked = false;
            FixSubjectVerbCheckBox.IsChecked = false;

            UpdatePreview(null, null);
        }

        // Feature hidden - not yet implemented
        // private void ManageCustomFillerWords_Click(object sender, RoutedEventArgs e)
        // {
        //     MessageBox.Show("Custom filler words management coming soon!", "Feature In Development", MessageBoxButton.OK, MessageBoxImage.Information);
        // }

        private void UIPreset_Changed(object sender, RoutedEventArgs e)
        {
            if (!isInitialized) return;

            // Update settings based on selected radio button
            if (PresetDefaultRadio.IsChecked == true)
            {
                settings.UIPreset = UIPreset.Default;
            }
            else if (PresetCompactRadio.IsChecked == true)
            {
                settings.UIPreset = UIPreset.Compact;
            }
        }

        #endregion
    }
}



using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using VoiceLite.Models;
using VoiceLite.Services;

namespace VoiceLite.Controls
{
    public partial class SimpleModelSelector : UserControl
    {
        public event EventHandler<string>? ModelSelected;
        private string selectedModel = "ggml-tiny.bin"; // Default to Tiny (free tier)
        private ProFeatureService? proFeatureService;
        private Settings? settings;

        public string SelectedModel
        {
            get => selectedModel;
            set
            {
                selectedModel = value;
                UpdateSelection();
            }
        }

        public SimpleModelSelector()
        {
            InitializeComponent();
            CheckModelAvailability();
        }

        /// <summary>
        /// Initialize with settings and Pro feature service for permission checking
        /// </summary>
        public void Initialize(Settings currentSettings, ProFeatureService? proService = null)
        {
            settings = currentSettings;
            proFeatureService = proService;
            ApplyProFeatureGating();
        }

        /// <summary>
        /// Hide/disable Pro models for free users
        /// </summary>
        private void ApplyProFeatureGating()
        {
            if (proFeatureService == null) return;

            bool isProUser = proFeatureService.IsProUser;

            // Base, Small, Medium, Large are Pro-only
            // Free users: Only Tiny visible
            BaseRadio.Visibility = isProUser ? Visibility.Visible : Visibility.Collapsed;
            SmallRadio.Visibility = isProUser ? Visibility.Visible : Visibility.Collapsed;
            MediumRadio.Visibility = isProUser ? Visibility.Visible : Visibility.Collapsed;
            LargeRadio.Visibility = isProUser ? Visibility.Visible : Visibility.Collapsed;

            // If free user somehow has a Pro model selected, revert to Tiny
            if (!isProUser && selectedModel != "ggml-tiny.bin")
            {
                selectedModel = "ggml-tiny.bin";
                if (settings != null)
                {
                    settings.WhisperModel = "ggml-tiny.bin";
                }
                UpdateSelection();
            }
        }

        private void CheckModelAvailability()
        {
            var whisperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper");

            CheckAndUpdateRadio(BaseRadio, Path.Combine(whisperPath, "ggml-base.bin"));
            CheckAndUpdateRadio(SmallRadio, Path.Combine(whisperPath, "ggml-small.bin"));
            CheckAndUpdateRadio(MediumRadio, Path.Combine(whisperPath, "ggml-medium.bin"));
            CheckAndUpdateRadio(LargeRadio, Path.Combine(whisperPath, "ggml-large-v3.bin"));
        }

        private void CheckAndUpdateRadio(RadioButton radio, string modelPath)
        {
            // Also check downloaded models in LocalAppData
            var localDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoiceLite",
                "whisper",
                Path.GetFileName(modelPath)
            );

            var encryptedPath = modelPath + ".enc";
            var modelExists = File.Exists(modelPath) || File.Exists(encryptedPath) || File.Exists(localDataPath);

            if (!modelExists)
            {
                radio.IsEnabled = false;
                radio.Opacity = 0.5;
                radio.ToolTip = "Download this model from Settings → AI Models tab";
            }
            else
            {
                radio.IsEnabled = true;
                radio.Opacity = 1.0;
                radio.ToolTip = null;
            }
        }

        private void ModelRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radio && radio.Tag is string modelFile)
            {
                // CRITICAL FIX: Check Pro permission before allowing selection
                if (proFeatureService != null && !proFeatureService.CanUseModel(modelFile))
                {
                    MessageBox.Show(
                        proFeatureService.GetUpgradeMessage("This AI model"),
                        "Pro Feature",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );

                    // Revert to Tiny model
                    selectedModel = "ggml-tiny.bin";
                    if (settings != null)
                    {
                        settings.WhisperModel = "ggml-tiny.bin";
                    }
                    UpdateSelection();
                    return;
                }

                selectedModel = modelFile;
                UpdateTip(modelFile);
                ModelSelected?.Invoke(this, modelFile);
            }
        }

        private void UpdateTip(string modelFile)
        {
            switch (modelFile)
            {
                case "ggml-tiny.bin":
                    TipText.Text = "Fast and lightweight - included for free";
                    break;
                case "ggml-base.bin":
                    TipText.Text = "Good for simple dictation and casual use (Pro)";
                    break;
                case "ggml-small.bin":
                    TipText.Text = "'Pro' offers the best experience for most users";
                    break;
                case "ggml-medium.bin":
                    TipText.Text = "Great for professional use when accuracy matters (Pro)";
                    break;
                case "ggml-large-v3.bin":
                    TipText.Text = "Maximum accuracy but requires a powerful computer (Pro)";
                    break;
            }
        }

        private void UpdateSelection()
        {
            switch (selectedModel)
            {
                case "ggml-tiny.bin":
                    // Tiny model not shown in this control (only shows Pro models)
                    break;
                case "ggml-base.bin":
                    BaseRadio.IsChecked = true;
                    break;
                case "ggml-small.bin":
                    SmallRadio.IsChecked = true;
                    break;
                case "ggml-medium.bin":
                    MediumRadio.IsChecked = true;
                    break;
                case "ggml-large-v3.bin":
                    LargeRadio.IsChecked = true;
                    break;
            }
        }
    }
}

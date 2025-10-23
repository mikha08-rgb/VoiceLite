using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VoiceLite.Models;
using VoiceLite.Services;

namespace VoiceLite.Controls
{
    public partial class SimpleModelSelector : UserControl
    {
        public event EventHandler<string>? ModelSelected;
        private string selectedModel = "ggml-tiny.bin"; // Free tier default
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
        /// Apply license restrictions based on user's license features.
        /// </summary>
        public void ApplyLicenseRestrictions(Settings userSettings)
        {
            settings = userSettings;
            var featureService = new FeatureService(userSettings);
            bool isPro = featureService.IsPro;

            // Tiny is always enabled (free tier)
            TinyRadio.IsEnabled = true;
            TinyRadio.Opacity = 1.0;

            // Pro models require license
            if (!isPro)
            {
                // Disable Pro models for free users
                BaseRadio.IsEnabled = false;
                SmallRadio.IsEnabled = false;
                MediumRadio.IsEnabled = false;
                LargeRadio.IsEnabled = false;

                BaseRadio.Opacity = 0.5;
                SmallRadio.Opacity = 0.5;
                MediumRadio.Opacity = 0.5;
                LargeRadio.Opacity = 0.5;

                BaseRadio.ToolTip = "🔒 Pro tier required - Upgrade to unlock";
                SmallRadio.ToolTip = "🔒 Pro tier required - Upgrade to unlock";
                MediumRadio.ToolTip = "🔒 Pro tier required - Upgrade to unlock";
                LargeRadio.ToolTip = "🔒 Pro tier required - Upgrade to unlock";

                // Force select Tiny if user had Pro model selected
                if (selectedModel != "ggml-tiny.bin")
                {
                    selectedModel = "ggml-tiny.bin";
                    userSettings.WhisperModel = "ggml-tiny.bin";
                    TinyRadio.IsChecked = true;
                    UpdateTip("ggml-tiny.bin");
                }
            }
            else
            {
                // Enable Pro models for Pro users (if downloaded)
                CheckModelAvailability();
            }
        }

        private void CheckModelAvailability()
        {
            var whisperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper");

            CheckAndUpdateRadio(TinyRadio, Path.Combine(whisperPath, "ggml-tiny.bin"));
            CheckAndUpdateRadio(BaseRadio, Path.Combine(whisperPath, "ggml-base.bin"));
            CheckAndUpdateRadio(SmallRadio, Path.Combine(whisperPath, "ggml-small.bin"));
            CheckAndUpdateRadio(MediumRadio, Path.Combine(whisperPath, "ggml-medium.bin"));
            CheckAndUpdateRadio(LargeRadio, Path.Combine(whisperPath, "ggml-large-v3.bin"));
        }

        private void CheckAndUpdateRadio(RadioButton radio, string modelPath)
        {
            var encryptedPath = modelPath + ".enc";
            var modelExists = File.Exists(modelPath) || File.Exists(encryptedPath);

            if (!modelExists)
            {
                radio.IsEnabled = false;
                radio.Opacity = 0.5;
                radio.ToolTip = "Download this model from the settings page.";
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
                    TipText.Text = "Free forever - basic transcription for everyone";
                    break;
                case "ggml-base.bin":
                    TipText.Text = "Good for simple dictation and casual use";
                    break;
                case "ggml-small.bin":
                    TipText.Text = "'Pro' offers the best experience for most users";
                    break;
                case "ggml-medium.bin":
                    TipText.Text = "Great for professional use when accuracy matters";
                    break;
                case "ggml-large-v3.bin":
                    TipText.Text = "Maximum accuracy but requires a powerful computer";
                    break;
            }
        }

        private void UpdateSelection()
        {
            switch (selectedModel)
            {
                case "ggml-tiny.bin":
                    TinyRadio.IsChecked = true;
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

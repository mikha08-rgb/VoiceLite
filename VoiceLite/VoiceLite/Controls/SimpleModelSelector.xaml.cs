using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VoiceLite.Models;
using VoiceLite.Services;

namespace VoiceLite.Controls
{
    public partial class SimpleModelSelector : UserControl
    {
        public event EventHandler<string>? ModelSelected;
        private string selectedModel = "ggml-tiny.bin"; // Default to Tiny model (free tier, pre-installed)
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
        }

        public void Initialize(Settings currentSettings)
        {
            settings = currentSettings;
            CheckModelAvailability();
            CheckLicenseGating();
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

        /// <summary>
        /// Public method to refresh license gating after license status changes.
        /// Called when a Pro license is activated to immediately unlock Pro models.
        /// </summary>
        public void RefreshLicenseGating()
        {
            CheckLicenseGating();
        }

        private void CheckLicenseGating()
        {
            // Pro models (Base, Small, Medium, Large) require a valid license
            bool hasValidLicense = SimpleLicenseStorage.HasValidLicense(out _);

            if (!hasValidLicense)
            {
                // Disable ALL Pro models for free users (Tiny only)
                string tooltip = "Requires Pro license. Get it for $20 at voicelite.app";

                BaseRadio.IsEnabled = false;
                BaseRadio.Opacity = 0.5;
                BaseRadio.ToolTip = tooltip;

                SmallRadio.IsEnabled = false;
                SmallRadio.Opacity = 0.5;
                SmallRadio.ToolTip = tooltip;

                MediumRadio.IsEnabled = false;
                MediumRadio.Opacity = 0.5;
                MediumRadio.ToolTip = tooltip;

                LargeRadio.IsEnabled = false;
                LargeRadio.Opacity = 0.5;
                LargeRadio.ToolTip = tooltip;
            }
            else
            {
                // Re-enable all Pro models when license is active
                // But check if they're downloaded first
                CheckAndUpdateRadio(BaseRadio, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper", "ggml-base.bin"));
                CheckAndUpdateRadio(SmallRadio, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper", "ggml-small.bin"));
                CheckAndUpdateRadio(MediumRadio, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper", "ggml-medium.bin"));
                CheckAndUpdateRadio(LargeRadio, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper", "ggml-large-v3.bin"));
            }
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
                // Check if trying to select Pro model without license
                var proModels = new[] { "ggml-base.bin", "ggml-small.bin", "ggml-medium.bin", "ggml-large-v3.bin" };
                if (proModels.Contains(modelFile))
                {
                    bool hasValidLicense = SimpleLicenseStorage.HasValidLicense(out _);

                    if (!hasValidLicense)
                    {
                        MessageBox.Show(
                            "This AI model requires a Pro license.\n\n" +
                            "Free tier includes:\n" +
                            "• Tiny model only (80-85% accuracy)\n\n" +
                            "Pro tier unlocks:\n" +
                            "• Base model (90% accuracy)\n" +
                            "• Small model (92% accuracy)\n" +
                            "• Medium model (95% accuracy)\n" +
                            "• Large model (98% accuracy)\n\n" +
                            "Get Pro for $20 at voicelite.app",
                            "Pro License Required",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        // Revert to Tiny model (free tier)
                        TinyRadio.IsChecked = true;
                        return;
                    }
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
                    TipText.Text = "Free tier model - fast, good for quick notes (80-85% accuracy)";
                    break;
                case "ggml-base.bin":
                    TipText.Text = "Pro model - good balance of speed and accuracy (90% accuracy)";
                    break;
                case "ggml-small.bin":
                    TipText.Text = "'Small' offers even better accuracy than Base";
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

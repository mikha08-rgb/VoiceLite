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
        private string selectedModel = "ggml-base.bin"; // Default to Base model for better first impression (still free, better accuracy)
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

        private void CheckLicenseGating()
        {
            // Pro model (ggml-small.bin) requires a valid license
            bool hasValidLicense = SimpleLicenseStorage.HasValidLicense(out _);

            if (!hasValidLicense)
            {
                // Disable Pro model if no valid license
                SmallRadio.IsEnabled = false;
                SmallRadio.Opacity = 0.5;
                SmallRadio.ToolTip = "Requires Pro license. Get it for $20 at voicelite.app";
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
                if (modelFile == "ggml-small.bin")
                {
                    bool hasValidLicense = SimpleLicenseStorage.HasValidLicense(out _);

                    if (!hasValidLicense)
                    {
                        MessageBox.Show(
                            "Pro model requires a valid license key.\n\n" +
                            "Get Pro for $20 at voicelite.app\n" +
                            "Then restart the app to activate your license.",
                            "Pro License Required",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        // Revert to Base model (better quality than Tiny)
                        BaseRadio.IsChecked = true;
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
                    TipText.Text = "Fastest model - lower accuracy, good for quick notes";
                    break;
                case "ggml-base.bin":
                    TipText.Text = "Free default - good balance of speed and accuracy";
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

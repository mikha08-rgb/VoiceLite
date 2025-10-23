using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace VoiceLite.Controls
{
    public partial class SimpleModelSelector : UserControl
    {
        public event EventHandler<string>? ModelSelected;
        private string selectedModel = "ggml-small.bin";

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

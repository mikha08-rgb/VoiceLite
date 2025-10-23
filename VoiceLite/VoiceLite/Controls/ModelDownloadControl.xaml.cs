using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VoiceLite.Models;

namespace VoiceLite.Controls
{
    public partial class ModelDownloadControl : UserControl
    {
        public event EventHandler<string>? ModelSelected;

        private readonly ObservableCollection<ModelInfo> models;
        private readonly string whisperPath;
        private Settings? settings;

        public ModelDownloadControl()
        {
            InitializeComponent();
            whisperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper");
            models = new ObservableCollection<ModelInfo>();
            ModelList.ItemsSource = models;

            InitializeModels();
        }

        public void Initialize(Settings currentSettings)
        {
            settings = currentSettings;
            RefreshModelStates();
        }

        private void InitializeModels()
        {
            models.Add(new ModelInfo
            {
                FileName = "ggml-tiny.bin",
                DisplayName = "Tiny (Lite)",
                Description = "Fast and lightweight - 75MB, 80-85% accuracy, <1 second processing",
                FileSizeMB = 75,
                DownloadUrl = null // Bundled with installer
            });

            models.Add(new ModelInfo
            {
                FileName = "ggml-base.bin",
                DisplayName = "Base (Swift)",
                Description = "Good balance - 142MB, 85-90% accuracy, ~2 seconds processing",
                FileSizeMB = 142,
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin"
            });

            models.Add(new ModelInfo
            {
                FileName = "ggml-small.bin",
                DisplayName = "Small (Pro)",
                Description = "Recommended for most users - 466MB, 90-93% accuracy, ~5 seconds processing",
                FileSizeMB = 466,
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin"
            });

            models.Add(new ModelInfo
            {
                FileName = "ggml-medium.bin",
                DisplayName = "Medium (Elite)",
                Description = "Professional accuracy - 1.5GB, 95-97% accuracy, ~12 seconds processing",
                FileSizeMB = 1500,
                DownloadUrl = "https://github.com/mikha08-rgb/VoiceLite/releases/download/v1.0.0/ggml-medium.bin"
            });

            models.Add(new ModelInfo
            {
                FileName = "ggml-large-v3.bin",
                DisplayName = "Large (Ultra)",
                Description = "Maximum accuracy - 2.9GB, 97-98% accuracy, ~25 seconds processing (requires 8GB+ RAM)",
                FileSizeMB = 2900,
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3.bin"
            });
        }

        private void RefreshModelStates()
        {
            foreach (var model in models)
            {
                var modelPath = Path.Combine(whisperPath, model.FileName);
                model.IsInstalled = File.Exists(modelPath);

                if (settings != null)
                {
                    model.IsSelected = settings.WhisperModel == model.FileName;
                }
            }
        }

        private void ModelRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radio && radio.DataContext is ModelInfo model)
            {
                if (settings != null)
                {
                    settings.WhisperModel = model.FileName;
                }

                UpdateTip(model);
                ModelSelected?.Invoke(this, model.FileName);
            }
        }

        private void UpdateTip(ModelInfo model)
        {
            TipText.Text = $"{model.DisplayName}: {model.Description}";
        }

        private async void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ModelInfo model)
            {
                if (!model.IsInstalled && model.DownloadUrl != null)
                {
                    await DownloadModel(model);
                }
            }
        }

        private async Task DownloadModel(ModelInfo model)
        {
            try
            {
                model.IsDownloading = true;
                model.DownloadProgress = 0;

                var destinationPath = Path.Combine(whisperPath, model.FileName);

                using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
                using var response = await httpClient.GetAsync(model.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var buffer = new byte[8192];
                var totalRead = 0L;

                Directory.CreateDirectory(whisperPath);

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        var progress = (double)totalRead / totalBytes * 100;
                        model.DownloadProgress = (int)progress;
                    }
                }

                model.IsDownloading = false;
                model.IsInstalled = true;
                model.DownloadProgress = 100;

                MessageBox.Show($"{model.DisplayName} downloaded successfully!", "Download Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                model.IsDownloading = false;
                MessageBox.Show($"Failed to download {model.DisplayName}:\n{ex.Message}", "Download Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class ModelInfo : INotifyPropertyChanged
    {
        private bool isInstalled;
        private bool isSelected;
        private bool isDownloading;
        private int downloadProgress;

        public string FileName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int FileSizeMB { get; set; }
        public string? DownloadUrl { get; set; }

        public bool IsInstalled
        {
            get => isInstalled;
            set { isInstalled = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusBadge)); OnPropertyChanged(nameof(ActionButtonText)); OnPropertyChanged(nameof(ActionButtonVisibility)); }
        }

        public bool IsSelected
        {
            get => isSelected;
            set { isSelected = value; OnPropertyChanged(); }
        }

        public bool IsDownloading
        {
            get => isDownloading;
            set { isDownloading = value; OnPropertyChanged(); OnPropertyChanged(nameof(DownloadProgressVisibility)); OnPropertyChanged(nameof(ActionButtonEnabled)); }
        }

        public int DownloadProgress
        {
            get => downloadProgress;
            set { downloadProgress = value; OnPropertyChanged(); OnPropertyChanged(nameof(DownloadProgressText)); }
        }

        public string StatusBadge => IsInstalled ? "✓ Installed" : "";
        public Brush StatusColor => new SolidColorBrush(Colors.Green);

        public string ActionButtonText
        {
            get
            {
                if (IsInstalled) return "Installed";
                if (DownloadUrl == null) return "Bundled";
                return $"Download ({FileSizeMB}MB)";
            }
        }

        public Visibility ActionButtonVisibility => (DownloadUrl != null && !IsInstalled) ? Visibility.Visible : Visibility.Collapsed;
        public bool ActionButtonEnabled => !IsDownloading;
        public Visibility DownloadProgressVisibility => IsDownloading ? Visibility.Visible : Visibility.Collapsed;
        public string DownloadProgressText => $"{DownloadProgress}%";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

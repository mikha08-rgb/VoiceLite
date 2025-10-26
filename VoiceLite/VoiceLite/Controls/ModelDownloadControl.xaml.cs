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
using VoiceLite.Services;

namespace VoiceLite.Controls
{
    public partial class ModelDownloadControl : UserControl
    {
        public event EventHandler<string>? ModelSelected;

        // WEEK 1 FIX: Static HttpClient for downloads prevents socket exhaustion
        // Separate client for downloads with longer timeout
        private static readonly HttpClient _downloadHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30),
            DefaultRequestHeaders =
            {
                { "User-Agent", "VoiceLite-Desktop/1.0" }
            }
        };

        private readonly ObservableCollection<ModelInfo> models;
        private readonly string whisperPath;
        private Settings? settings;
        private Action? saveSettingsCallback;
        private ProFeatureService? proFeatureService;

        public ModelDownloadControl()
        {
            InitializeComponent();
            // Use LocalApplicationData for downloads (user-writable) instead of Program Files
            var localDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoiceLite",
                "whisper"
            );
            whisperPath = localDataPath;
            models = new ObservableCollection<ModelInfo>();
            ModelList.ItemsSource = models;

            InitializeModels();
        }

        public void Initialize(Settings currentSettings, Action? onSaveSettings = null)
        {
            settings = currentSettings;
            saveSettingsCallback = onSaveSettings;
            proFeatureService = new ProFeatureService(currentSettings);

            // Apply Pro feature gating after initialization
            ApplyProFeatureGating();
            RefreshModelStates();
        }

        private void InitializeModels()
        {
            models.Add(new ModelInfo
            {
                FileName = "ggml-tiny.bin",
                DisplayName = "Tiny (Lite)",
                Description = "Fast and lightweight - 42MB Q8_0, 80-85% accuracy, <1 second processing",
                FileSizeMB = 42,
                DownloadUrl = null, // Bundled with installer
                IsProOnly = false // Free tier model
            });

            models.Add(new ModelInfo
            {
                FileName = "ggml-base.bin",
                DisplayName = "Base (Swift) 🔒",
                Description = "Good balance - 78MB Q8_0, 85-90% accuracy, ~2 seconds processing",
                FileSizeMB = 78,
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base-q8_0.bin",
                IsProOnly = true // Pro tier model
            });

            models.Add(new ModelInfo
            {
                FileName = "ggml-small.bin",
                DisplayName = "Small (Pro) 🔒",
                Description = "Recommended for most users - 253MB Q8_0, 90-93% accuracy, ~3 seconds processing",
                FileSizeMB = 253,
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small-q8_0.bin",
                IsProOnly = true // Pro tier model
            });

            models.Add(new ModelInfo
            {
                FileName = "ggml-medium.bin",
                DisplayName = "Medium (Elite) 🔒",
                Description = "Professional accuracy - 823MB Q8_0, 95-97% accuracy, ~12 seconds processing",
                FileSizeMB = 823,
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium-q8_0.bin",
                IsProOnly = true // Pro tier model
            });

            models.Add(new ModelInfo
            {
                FileName = "ggml-large-v3.bin",
                DisplayName = "Large (Ultra) 🔒",
                Description = "Maximum accuracy - 1.6GB Q8_0, 97-98% accuracy, ~8 seconds processing",
                FileSizeMB = 1600,
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-q8_0.bin",
                IsProOnly = true // Pro tier model
            });
        }

        private void ApplyProFeatureGating()
        {
            if (proFeatureService == null) return;

            foreach (var model in models)
            {
                // Free users can only see Tiny model
                model.IsVisible = !model.IsProOnly || proFeatureService.IsProUser;

                // Update display name for locked models (free users)
                if (model.IsProOnly && !proFeatureService.IsProUser)
                {
                    model.IsLocked = true;
                }
            }
        }

        private void RefreshModelStates()
        {
            foreach (var model in models)
            {
                // Check both bundled location (Program Files) and download location (LocalAppData)
                var bundledPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper", model.FileName);
                var downloadedPath = Path.Combine(whisperPath, model.FileName);

                model.IsInstalled = File.Exists(bundledPath) || File.Exists(downloadedPath);

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
                // Check if user has permission to use this model
                if (proFeatureService != null && !proFeatureService.CanUseModel(model.FileName))
                {
                    MessageBox.Show(
                        proFeatureService.GetUpgradeMessage($"{model.DisplayName} model"),
                        "Pro Feature",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );

                    // Revert to Tiny model
                    if (settings != null)
                    {
                        settings.WhisperModel = "ggml-tiny.bin";
                        RefreshModelStates();
                    }
                    return;
                }

                if (settings != null)
                {
                    settings.WhisperModel = model.FileName;
                    // Save settings immediately when model changes
                    saveSettingsCallback?.Invoke();
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
            // CRITICAL FIX #5: Wrap async void event handler in try-catch
            try
            {
                if (sender is Button button && button.Tag is ModelInfo model)
                {
                    // Check Pro permission before download
                    if (proFeatureService != null && !proFeatureService.CanUseModel(model.FileName))
                    {
                        MessageBox.Show(
                            proFeatureService.GetUpgradeMessage($"{model.DisplayName} model"),
                            "Pro Feature",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information
                        );
                        return;
                    }

                    if (!model.IsInstalled && model.DownloadUrl != null)
                    {
                        await DownloadModel(model);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Model download button click failed", ex);
                MessageBox.Show(
                    $"An error occurred: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private async Task DownloadModel(ModelInfo model)
        {
            try
            {
                model.IsDownloading = true;
                model.DownloadProgress = 0;

                var destinationPath = Path.Combine(whisperPath, model.FileName);

                // WEEK 1 FIX: Use static HttpClient instead of creating new instance
                using var response = await _downloadHttpClient.GetAsync(model.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
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
        private bool isVisible = true;
        private bool isLocked = false;

        public string FileName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int FileSizeMB { get; set; }
        public string? DownloadUrl { get; set; }
        public bool IsProOnly { get; set; } = false;

        public bool IsVisible
        {
            get => isVisible;
            set { isVisible = value; OnPropertyChanged(); OnPropertyChanged(nameof(VisibilityState)); }
        }

        public bool IsLocked
        {
            get => isLocked;
            set { isLocked = value; OnPropertyChanged(); OnPropertyChanged(nameof(Opacity)); }
        }

        public Visibility VisibilityState => IsVisible ? Visibility.Visible : Visibility.Collapsed;
        public double Opacity => IsLocked ? 0.5 : 1.0;

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

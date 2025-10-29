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
            // FREE TIER: Base only (bundled with installer)
            // PRO TIER: All 5 models (Tiny, Base, Small, Medium, Large)
            // ORDERING: Tiny, Base, Small, Medium, Large
            models.Add(new ModelInfo
            {
                FileName = "ggml-tiny.bin",
                DisplayName = "Tiny (Lite)",
                Description = "For very slow PCs - 42MB Q8_0, 80-85% accuracy, <0.8s processing",
                FileSizeMB = 42,
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny-q8_0.bin",
                IsProOnly = false // Free tier model (downloadable)
            });

            models.Add(new ModelInfo
            {
                FileName = "ggml-base.bin",
                DisplayName = "Base (Swift) ⭐ Default",
                Description = "Bundled with installer - 78MB Q8_0, 85-90% accuracy, ~1.5s processing - Recommended for most users",
                FileSizeMB = 78,
                DownloadUrl = null, // Bundled with installer (new default)
                IsProOnly = false // Free tier model (bundled, no download needed)
            });

            models.Add(new ModelInfo
            {
                FileName = "ggml-small.bin",
                DisplayName = "Small (Pro) 🔒",
                Description = "Pro users only - 253MB Q8_0, 90-93% accuracy, ~3 seconds processing",
                FileSizeMB = 253,
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small-q8_0.bin",
                IsProOnly = true // Pro tier model
            });

            models.Add(new ModelInfo
            {
                FileName = "ggml-medium.bin",
                DisplayName = "Medium (Elite) 🔒",
                Description = "Pro users only - 823MB Q8_0, 95-97% accuracy, ~12 seconds processing",
                FileSizeMB = 823,
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium-q8_0.bin",
                IsProOnly = true // Pro tier model
            });

            models.Add(new ModelInfo
            {
                FileName = "ggml-large-v3.bin",
                DisplayName = "Large (Ultra) 🔒",
                Description = "Pro users only - 3.1GB F16, 97-98% accuracy, ~15 seconds processing",
                FileSizeMB = 3100,
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3.bin",
                IsProOnly = true // Pro tier model
            });
        }

        private void ApplyProFeatureGating()
        {
            if (proFeatureService == null) return;

            bool isProUser = proFeatureService.IsProUser;

            foreach (var model in models)
            {
                // Free users: Only see Base model
                // Pro users: See all models
                if (isProUser)
                {
                    model.IsVisible = true;
                    model.IsLocked = false;
                }
                else
                {
                    // Free users only see Base model
                    model.IsVisible = model.FileName == "ggml-base.bin";
                    model.IsLocked = false;
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
            set
            {
                isLocked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Opacity));
                OnPropertyChanged(nameof(CanSelect));
            }
        }

        public Visibility VisibilityState => IsVisible ? Visibility.Visible : Visibility.Collapsed;
        public double Opacity => IsLocked ? 0.5 : 1.0;

        // Radio button should be enabled if: (1) model is installed AND (2) not locked (user has permission)
        public bool CanSelect => IsInstalled && !IsLocked;

        public bool IsInstalled
        {
            get => isInstalled;
            set { isInstalled = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusBadge)); OnPropertyChanged(nameof(ActionButtonText)); OnPropertyChanged(nameof(ActionButtonVisibility)); OnPropertyChanged(nameof(CanSelect)); }
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

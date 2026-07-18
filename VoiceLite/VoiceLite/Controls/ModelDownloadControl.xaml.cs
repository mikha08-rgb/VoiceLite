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
using SharpCompress.Archives;
using SharpCompress.Common;
using VoiceLite.Models;
using VoiceLite.Services;

namespace VoiceLite.Controls
{
    public partial class ModelDownloadControl : UserControl
    {
        public event EventHandler<string>? ModelSelected;
        public event EventHandler? InstallCompleted;

        // Static HttpClient prevents socket exhaustion. 30-minute timeout for the
        // ~640MB Parakeet tarball on slower connections.
        private static readonly HttpClient _downloadHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30),
            DefaultRequestHeaders = { { "User-Agent", "VoiceLite-Desktop/1.0" } }
        };

        // ModelResolverService probes this path (alongside bin/models for dev installs).
        private static readonly string ModelDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VoiceLite", "models", "parakeet-v3");

        // The 4 files the Parakeet tarball expands to. TranscriptionService
        // requires all four — partial extraction must not look "installed."
        private static readonly string[] RequiredModelFiles = new[]
        {
            "encoder.int8.onnx",
            "decoder.int8.onnx",
            "joiner.int8.onnx",
            "tokens.txt"
        };

        private readonly ObservableCollection<ModelInfo> models;
        private Settings? settings;
        private Action? saveSettingsCallback;

        public ModelDownloadControl()
        {
            InitializeComponent();
            models = new ObservableCollection<ModelInfo>();
            ModelList.ItemsSource = models;
            InitializeModels();
            RefreshModelStates();
        }

        // First-launch use passes null settings/callback — the control still functions
        // for the download flow because settings.TranscriptionModel already defaults to the
        // Parakeet id in Models/Settings.cs.
        public void Initialize(Settings? currentSettings, Action? onSaveSettings = null)
        {
            settings = currentSettings;
            saveSettingsCallback = onSaveSettings;
            RefreshModelStates();
        }

        public bool IsModelInstalled => AllModelFilesPresent();

        private void InitializeModels()
        {
            models.Add(new ModelInfo
            {
                FileName = "parakeet-tdt-0.6b-v3-int8",
                DisplayName = "Parakeet v3 (int8)",
                Description = "Multilingual (25 EU languages), transducer architecture — no silence hallucinations. ~640MB download.",
                FileSizeMB = 640,
                DownloadUrl = DownloadEndpoints.ParakeetV3Int8
            });
        }

        private void RefreshModelStates()
        {
            foreach (var model in models)
            {
                model.IsInstalled = AllModelFilesPresent();
                if (settings != null)
                {
                    model.IsSelected = settings.TranscriptionModel == model.FileName;
                }
            }
        }

        private static bool AllModelFilesPresent()
        {
            if (!Directory.Exists(ModelDir)) return false;
            return RequiredModelFiles.All(f =>
            {
                var path = Path.Combine(ModelDir, f);
                return File.Exists(path) && new FileInfo(path).Length > 0;
            });
        }

        private void ModelRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radio && radio.DataContext is ModelInfo model)
            {
                if (settings != null)
                {
                    settings.TranscriptionModel = model.FileName;
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
            try
            {
                if (sender is Button button && button.Tag is ModelInfo model)
                {
                    if (!model.IsInstalled && model.DownloadUrl != null)
                    {
                        await DownloadAndExtractModel(model);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Model download button click failed", ex);
                MessageBox.Show($"An error occurred: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DownloadAndExtractModel(ModelInfo model)
        {
            string? tempTarPath = null;
            try
            {
                model.IsDownloading = true;
                model.DownloadProgress = 0;

                Directory.CreateDirectory(ModelDir);
                tempTarPath = Path.Combine(Path.GetTempPath(), $"parakeet-{Guid.NewGuid():N}.tar.bz2");

                using (var response = await _downloadHttpClient.GetAsync(
                    model.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? 0;

                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(
                        tempTarPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);

                    var buffer = new byte[65536];
                    long totalRead = 0;
                    int bytesRead;
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;

                        if (totalBytes > 0)
                        {
                            // Reserve the top 5% for extraction so the bar doesn't sit at 100% while extracting.
                            model.DownloadProgress = (int)((double)totalRead / totalBytes * 95);
                        }
                    }
                }

                await Task.Run(() => ExtractRequiredFiles(tempTarPath, ModelDir));

                if (!AllModelFilesPresent())
                {
                    throw new InvalidOperationException(
                        $"Extraction completed but required files are missing in {ModelDir}");
                }

                model.DownloadProgress = 100;
                model.IsInstalled = true;
                model.IsDownloading = false;

                MessageBox.Show(
                    $"{model.DisplayName} installed successfully.",
                    "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                InstallCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                model.IsDownloading = false;
                ErrorLogger.LogError("Parakeet download/extract failed", ex);
                MessageBox.Show(
                    $"Failed to install {model.DisplayName}:\n{ex.Message}\n\nClick Download to retry.",
                    "Download Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (tempTarPath != null && File.Exists(tempTarPath))
                {
                    try { File.Delete(tempTarPath); }
                    catch (Exception ex) { ErrorLogger.LogDebug($"Temp tarball cleanup failed: {ex.Message}"); }
                }
            }
        }

        // Tarball nests the 4 files under "sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8/".
        // We match by filename only so a folder-naming change upstream doesn't break us.
        private static void ExtractRequiredFiles(string tarBz2Path, string targetDir)
        {
            var wanted = new HashSet<string>(RequiredModelFiles, StringComparer.OrdinalIgnoreCase);

            using var archive = ArchiveFactory.OpenArchive(tarBz2Path, new SharpCompress.Readers.ReaderOptions());
            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory) continue;

                var entryName = Path.GetFileName(entry.Key ?? string.Empty);
                if (string.IsNullOrEmpty(entryName) || !wanted.Contains(entryName)) continue;

                var destPath = Path.Combine(targetDir, entryName);
                entry.WriteToFile(destPath, new ExtractionOptions { Overwrite = true });
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

        // Single-engine post-Parakeet swap — no Pro gating, no lock state.
        public bool CanSelect => IsInstalled;

        public bool IsInstalled
        {
            get => isInstalled;
            set
            {
                isInstalled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusBadge));
                OnPropertyChanged(nameof(ActionButtonText));
                OnPropertyChanged(nameof(ActionButtonVisibility));
                OnPropertyChanged(nameof(CanSelect));
            }
        }

        public bool IsSelected
        {
            get => isSelected;
            set { isSelected = value; OnPropertyChanged(); }
        }

        public bool IsDownloading
        {
            get => isDownloading;
            set
            {
                isDownloading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DownloadProgressVisibility));
                OnPropertyChanged(nameof(ActionButtonEnabled));
            }
        }

        public int DownloadProgress
        {
            get => downloadProgress;
            set { downloadProgress = value; OnPropertyChanged(); OnPropertyChanged(nameof(DownloadProgressText)); }
        }

        public string StatusBadge => IsInstalled ? "✓ Installed" : string.Empty;
        public Brush StatusColor => new SolidColorBrush(Colors.Green);

        public string ActionButtonText =>
            IsInstalled ? "Installed" : $"Download ({FileSizeMB}MB)";

        public Visibility ActionButtonVisibility =>
            (DownloadUrl != null && !IsInstalled) ? Visibility.Visible : Visibility.Collapsed;

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

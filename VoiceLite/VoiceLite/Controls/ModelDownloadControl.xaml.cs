using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
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

        // Static HttpClient prevents socket exhaustion. Timeout is infinite on purpose:
        // the ~640MB Parakeet tarball legitimately takes >30 minutes on slow links, and
        // the old fixed 30-minute Timeout made <3Mbps connections fail at ~100% and
        // restart from zero. Dead connections are caught by the per-read stall detector
        // in DownloadAndExtractModel (no bytes for 60s → clear "connection stalled" error).
        private static readonly HttpClient _downloadHttpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan,
            DefaultRequestHeaders = { { "User-Agent", "VoiceLite-Desktop/1.0" } }
        };

        // Per-read stall timeout: if the socket delivers no bytes for this long the
        // download fails with an actionable message instead of hanging forever.
        private const int StallTimeoutSeconds = 60;

        // Disk-space preflight: tarball (~640MB) and extracted model (~700MB) coexist
        // briefly during install, so require headroom for both plus a safety margin.
        private const long TarballBytesRequired = 700_000_000;
        private const long ExtractedBytesRequired = 800_000_000;

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
        private CancellationTokenSource? downloadCts;

        public ModelDownloadControl()
        {
            InitializeComponent();
            models = new ObservableCollection<ModelInfo>();
            ModelList.ItemsSource = models;
            InitializeModels();
            RefreshModelStates();

            // Sweep artifacts left behind if a previous run was killed mid-download
            // (the finally block never runs on process kill). Fire-and-forget.
            _ = Task.Run(SweepStaleDownloadArtifacts);
        }

        /// <summary>
        /// Cancels any in-flight download. Safe to call from the hosting window's
        /// Closing handler; the same cancellation is also wired automatically to the
        /// hosting window's Closed event when a download starts.
        /// </summary>
        public void CancelDownload() => downloadCts?.Cancel();

        // Deletes stray tarballs in %TEMP% and half-extracted tmp dirs next to the
        // live model dir, left by crashed/killed previous runs. Best-effort only.
        private static void SweepStaleDownloadArtifacts()
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(Path.GetTempPath(), "parakeet-*.tar.bz2"))
                {
                    try { File.Delete(file); }
                    catch { /* locked by a concurrent download — leave it */ }
                }

                var parentDir = Path.GetDirectoryName(ModelDir);
                if (parentDir != null && Directory.Exists(parentDir))
                {
                    foreach (var dir in Directory.EnumerateDirectories(
                                 parentDir, Path.GetFileName(ModelDir) + ".tmp-*"))
                    {
                        try { Directory.Delete(dir, recursive: true); }
                        catch { /* in use — leave it */ }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogDebug($"Stale download artifact sweep failed: {ex.Message}");
            }
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

        private static bool AllModelFilesPresent() => AllModelFilesPresentIn(ModelDir);

        private static bool AllModelFilesPresentIn(string dir)
        {
            if (!Directory.Exists(dir)) return false;
            return RequiredModelFiles.All(f =>
            {
                var path = Path.Combine(dir, f);
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
            string? tempExtractDir = null;

            // Cancellation: cancelled explicitly via CancelDownload(), or automatically
            // when the hosting window closes (first-launch dialog or settings window).
            using var cts = new CancellationTokenSource();
            downloadCts = cts;
            var hostWindow = Window.GetWindow(this);
            EventHandler? hostClosedHandler = null;
            if (hostWindow != null)
            {
                hostClosedHandler = (_, _) => cts.Cancel();
                hostWindow.Closed += hostClosedHandler;
            }

            try
            {
                model.IsDownloading = true;
                model.DownloadProgress = 0;

                var parentDir = Path.GetDirectoryName(ModelDir)!;
                Directory.CreateDirectory(parentDir);
                tempTarPath = Path.Combine(Path.GetTempPath(), $"parakeet-{Guid.NewGuid():N}.tar.bz2");

                EnsureSufficientDiskSpace(tempTarPath, parentDir);

                // Stall detector: stallCts trips if a single connect/read makes no
                // progress for StallTimeoutSeconds. The HttpClient itself has an
                // infinite timeout so slow-but-alive links can finish.
                using var stallCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);

                stallCts.CancelAfter(TimeSpan.FromSeconds(StallTimeoutSeconds));
                using (var response = await WrapStall(
                    _downloadHttpClient.GetAsync(model.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, stallCts.Token), cts))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? 0;

                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(
                        tempTarPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);

                    var buffer = new byte[65536];
                    long totalRead = 0;
                    while (true)
                    {
                        stallCts.CancelAfter(TimeSpan.FromSeconds(StallTimeoutSeconds));
                        int bytesRead = await WrapStall(
                            contentStream.ReadAsync(buffer, 0, buffer.Length, stallCts.Token), cts);
                        if (bytesRead == 0) break;

                        stallCts.CancelAfter(Timeout.InfiniteTimeSpan); // pause timer during disk write
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cts.Token);
                        totalRead += bytesRead;

                        if (totalBytes > 0)
                        {
                            // Reserve the top 5% for extraction so the bar doesn't sit at 100% while extracting.
                            model.DownloadProgress = (int)((double)totalRead / totalBytes * 95);
                        }
                    }
                }

                // Extract into a sibling temp dir, then atomically swap into place.
                // Extracting directly into the live dir poisoned installs on power
                // loss / disk full: partial files made the dir look "installed" and
                // wedged every subsequent launch.
                tempExtractDir = Path.Combine(
                    parentDir, $"{Path.GetFileName(ModelDir)}.tmp-{Guid.NewGuid():N}");
                var extractDir = tempExtractDir;
                await Task.Run(() => ExtractRequiredFiles(tempTarPath, extractDir), cts.Token);
                cts.Token.ThrowIfCancellationRequested();

                if (!AllModelFilesPresentIn(tempExtractDir))
                {
                    throw new InvalidOperationException(
                        $"Extraction completed but required files are missing or empty in {tempExtractDir}");
                }

                // Swap: remove any previous (possibly corrupt) live dir, then move the
                // fully-verified temp dir into place. Same-volume Directory.Move means a
                // crash leaves either the old state or the complete new state, never a mix.
                if (Directory.Exists(ModelDir))
                    Directory.Delete(ModelDir, recursive: true);
                Directory.Move(tempExtractDir, ModelDir);
                tempExtractDir = null; // swapped into place — nothing to clean up

                if (!AllModelFilesPresent())
                {
                    throw new InvalidOperationException(
                        $"Model install verification failed after swap in {ModelDir}");
                }

                model.DownloadProgress = 100;
                model.IsInstalled = true;
                model.IsDownloading = false;

                MessageBox.Show(
                    $"{model.DisplayName} installed successfully.",
                    "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                InstallCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // User closed the window / cancelled: stop cleanly, no error dialog.
                model.IsDownloading = false;
                model.DownloadProgress = 0;
                ErrorLogger.LogDebug("Parakeet download cancelled");
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
                downloadCts = null;
                if (hostWindow != null && hostClosedHandler != null)
                    hostWindow.Closed -= hostClosedHandler;

                if (tempTarPath != null && File.Exists(tempTarPath))
                {
                    try { File.Delete(tempTarPath); }
                    catch (Exception ex) { ErrorLogger.LogDebug($"Temp tarball cleanup failed: {ex.Message}"); }
                }

                if (tempExtractDir != null && Directory.Exists(tempExtractDir))
                {
                    try { Directory.Delete(tempExtractDir, recursive: true); }
                    catch (Exception ex) { ErrorLogger.LogDebug($"Temp extract dir cleanup failed: {ex.Message}"); }
                }
            }
        }

        // Converts a stall-timer cancellation into an actionable error while letting
        // genuine user cancellation (outer cts) propagate as OperationCanceledException.
        private static async Task<T> WrapStall<T>(Task<T> task, CancellationTokenSource userCts)
        {
            try
            {
                return await task;
            }
            catch (OperationCanceledException) when (!userCts.IsCancellationRequested)
            {
                throw new IOException(
                    $"Connection stalled: no data received for {StallTimeoutSeconds} seconds. " +
                    "Check your internet connection, then click Download to resume.");
            }
        }

        // Preflight so a full disk produces a clear message with numbers instead of a
        // raw IOException at 99%. Tarball lands on the temp drive; extraction and the
        // final swap happen on the model drive — usually the same drive (C:).
        private static void EnsureSufficientDiskSpace(string tempFilePath, string targetDir)
        {
            try
            {
                var tempRoot = Path.GetPathRoot(Path.GetFullPath(tempFilePath));
                var targetRoot = Path.GetPathRoot(Path.GetFullPath(targetDir));
                if (string.IsNullOrEmpty(tempRoot) || string.IsNullOrEmpty(targetRoot))
                    return;

                var requirements = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    [tempRoot] = TarballBytesRequired
                };
                if (requirements.ContainsKey(targetRoot))
                    requirements[targetRoot] += ExtractedBytesRequired;
                else
                    requirements[targetRoot] = ExtractedBytesRequired;

                foreach (var (root, requiredBytes) in requirements)
                {
                    long availableBytes;
                    try
                    {
                        availableBytes = new DriveInfo(root).AvailableFreeSpace;
                    }
                    catch (Exception ex)
                    {
                        // Preflight is best-effort (odd drive types, UNC paths): never
                        // block a download because the check itself failed.
                        ErrorLogger.LogDebug($"Disk-space preflight skipped for {root}: {ex.Message}");
                        continue;
                    }

                    if (availableBytes < requiredBytes)
                    {
                        throw new IOException(
                            $"Not enough disk space on drive {root} — " +
                            $"{requiredBytes / 1_000_000_000.0:F1} GB required, " +
                            $"{availableBytes / 1_000_000_000.0:F1} GB available.\n" +
                            "Free up space, then click Download to retry.");
                    }
                }
            }
            catch (IOException)
            {
                throw; // our own clear insufficient-space message above
            }
            catch (Exception ex)
            {
                ErrorLogger.LogDebug($"Disk-space preflight skipped: {ex.Message}");
            }
        }

        // Tarball nests the 4 files under "sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8/".
        // We match by filename only so a folder-naming change upstream doesn't break us.
        private static void ExtractRequiredFiles(string tarBz2Path, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
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
        public Visibility VisibilityState => Visibility.Visible;
        public double Opacity => 1.0;
        public Visibility ProRequiredMessageVisibility => Visibility.Collapsed;

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

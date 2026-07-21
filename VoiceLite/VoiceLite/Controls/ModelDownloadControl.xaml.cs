using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
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
        public event EventHandler? InstallCompleted;

        // ModelResolverService probes this path (alongside bin/models for dev installs).
        private static readonly string ParakeetModelDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VoiceLite", "models", ModelResolverService.ParakeetDirName);

        private readonly ObservableCollection<ModelInfo> models;
        private readonly IReadOnlyDictionary<string, ModelManifest> manifests;
        private readonly ModelInstaller modelInstaller;
        private readonly HashSet<string> recoveredModelIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private Settings? settings;
        private Action? saveSettingsCallback;
        private CancellationTokenSource? downloadCts;
        private bool recoveryCompletionRaised;

        public ModelDownloadControl()
        {
            InitializeComponent();

            var parakeetManifest = DownloadEndpoints.GetManifestForFileName(
                ModelResolverService.ParakeetModelId,
                ParakeetModelDirectory);
            var translationManifest = DownloadEndpoints.GetManifestForFileName(
                TranslationModelResolverService.ModelId,
                TranslationModelResolverService.DefaultModelDirectory);
            manifests = new Dictionary<string, ModelManifest>(StringComparer.OrdinalIgnoreCase)
            {
                [parakeetManifest.ModelId] = parakeetManifest,
                [translationManifest.ModelId] = translationManifest
            };
            modelInstaller = new ModelInstaller();

            // Run recovery synchronously before checking model state. Directory renames
            // are fast, and this ensures a killed live->backup/temp->live swap is repaired
            // before the download UI refreshes its installed state.
            RecoverInterruptedInstalls();

            models = new ObservableCollection<ModelInfo>();
            ModelList.ItemsSource = models;
            InitializeModels();
            RefreshModelStates();

            // A first-launch or translation modal may have opened solely because the
            // process died between swap renames. Once Loaded subscribers are attached,
            // report the recovered install so the host can continue automatically.
            Loaded += ModelDownloadControl_Loaded;

            // Tarballs are not part of swap recovery. Sweep them in the background;
            // locked files belong to an active download and are left untouched.
            _ = Task.Run(() => ModelInstaller.SweepStaleArchiveDownloads(manifests.Values));
        }

        /// <summary>
        /// Cancels any in-flight download. Safe to call from the hosting window's
        /// Closing handler; the same cancellation is also wired automatically to the
        /// hosting window's Closed event when a download starts.
        /// </summary>
        public void CancelDownload() => downloadCts?.Cancel();

        // First-launch use passes null settings/callback. Settings.TranscriptionModel
        // already defaults to the Parakeet id, so the download flow still works.
        public void Initialize(
            Settings? currentSettings,
            Action? onSaveSettings = null,
            bool includeTranslationModel = false)
        {
            settings = currentSettings;
            saveSettingsCallback = onSaveSettings;
            if (includeTranslationModel &&
                models.All(model => model.FileName != TranslationModelResolverService.ModelId))
            {
                models.Add(CreateTranslationModelInfo());
            }

            if (includeTranslationModel)
            {
                EngineHeaderText.Text = "Local Speech Models";
                EngineDescriptionText.Text =
                    "Parakeet handles normal multilingual transcription. The optional Canary add-on translates Spanish, French, or German speech to English. Both run locally after download.";
            }

            RefreshModelStates();
        }

        /// <summary>
        /// Reconfigures the control for the small modal launched by the translation
        /// setting. First-launch setup keeps using the default Parakeet-only view.
        /// </summary>
        public void ShowTranslationModelOnly()
        {
            models.Clear();
            models.Add(CreateTranslationModelInfo());
            RefreshModelStates();
            EngineHeaderText.Text = "English Translation Add-on";
            EngineDescriptionText.Text =
                "Canary translates Spanish, French, or German speech directly to English. The model is stored locally and audio never leaves this computer.";
            TipText.Text =
                "Install this optional model to translate Spanish, French, or German speech to English without sending audio online.";
        }

        public bool IsModelInstalled
        {
            get
            {
                var parakeet = models.FirstOrDefault(
                    model => model.FileName == ModelResolverService.ParakeetModelId);
                return parakeet != null && AllModelFilesPresent(parakeet);
            }
        }

        private void InitializeModels()
        {
            var manifest = manifests[ModelResolverService.ParakeetModelId];
            models.Add(new ModelInfo
            {
                Manifest = manifest,
                DisplayName = "Parakeet v3 (int8)",
                Description =
                    "Multilingual (25 EU languages), transducer architecture — no silence hallucinations. ~487MB download.",
                FileSizeMB = (int)(manifest.ArchiveSizeBytes / 1_000_000),
                IsSelectable = true
            });
        }

        private ModelInfo CreateTranslationModelInfo()
        {
            var manifest = manifests[TranslationModelResolverService.ModelId];
            return new ModelInfo
            {
                Manifest = manifest,
                DisplayName = "English Translation Add-on (Canary int8)",
                Description =
                    "Offline Spanish, French, and German speech → English. Optional ~154MB download.",
                FileSizeMB = (int)(manifest.ArchiveSizeBytes / 1_000_000),
                IsSelectable = false
            };
        }

        private void RecoverInterruptedInstalls()
        {
            foreach (var manifest in manifests.Values)
            {
                try
                {
                    var result = ModelInstaller.RecoverInterruptedInstall(manifest);
                    if (result is ModelRecoveryResult.RestoredBackup or
                        ModelRecoveryResult.PromotedTemporaryDirectory)
                    {
                        recoveredModelIds.Add(manifest.ModelId);
                        ErrorLogger.LogWarning(
                            $"Recovered interrupted {manifest.ModelId} model install ({result}).");
                    }
                }
                catch (Exception ex)
                {
                    // Keep the download UI available for a clean retry, but make this
                    // Release-visible: a recovery failure can otherwise look like a
                    // mysteriously missing model after an update or power loss.
                    ErrorLogger.LogError(
                        $"Interrupted model install recovery failed for {manifest.ModelId}",
                        ex);
                }
            }
        }

        private void ModelDownloadControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (recoveryCompletionRaised)
                return;

            var visibleRecoveredModel = models.FirstOrDefault(model =>
                recoveredModelIds.Contains(model.FileName) && AllModelFilesPresent(model));
            if (visibleRecoveredModel == null)
                return;

            recoveryCompletionRaised = true;
            InstallCompleted?.Invoke(this, EventArgs.Empty);
        }

        private void RefreshModelStates()
        {
            foreach (var model in models)
            {
                model.IsInstalled = AllModelFilesPresent(model);
                if (settings != null && model.IsSelectable)
                    model.IsSelected = settings.TranscriptionModel == model.FileName;
            }
        }

        private static bool AllModelFilesPresent(ModelInfo model) =>
            ModelInstaller.HasRequiredModelFiles(model.Manifest);

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
                if (sender is Button button &&
                    button.Tag is ModelInfo model &&
                    !model.IsInstalled)
                {
                    await DownloadAndInstallModel(model);
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Model download button click failed", ex);
                MessageBox.Show(
                    $"An error occurred: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task DownloadAndInstallModel(ModelInfo model)
        {
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

                var progress = new Progress<ModelInstallProgress>(update =>
                {
                    model.DownloadProgress = update.Percentage;
                });
                await modelInstaller.InstallAsync(model.Manifest, progress, cts.Token);

                model.DownloadProgress = 100;
                model.IsInstalled = true;
                model.IsDownloading = false;

                MessageBox.Show(
                    $"{model.DisplayName} installed successfully.",
                    "Download Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                InstallCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // User closed the window / cancelled: stop cleanly, no error dialog.
                model.IsDownloading = false;
                model.DownloadProgress = 0;
                ErrorLogger.LogDebug($"{model.DisplayName} download cancelled");
            }
            catch (Exception ex)
            {
                model.IsDownloading = false;
                ErrorLogger.LogError($"{model.DisplayName} download/install failed", ex);
                MessageBox.Show(
                    $"Failed to install {model.DisplayName}:\n{ex.Message}\n\nClick Download to retry.",
                    "Download Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                downloadCts = null;
                if (hostWindow != null && hostClosedHandler != null)
                    hostWindow.Closed -= hostClosedHandler;
            }
        }
    }

    public class ModelInfo : INotifyPropertyChanged
    {
        private bool isInstalled;
        private bool isSelected;
        private bool isDownloading;
        private int downloadProgress;

        internal ModelManifest Manifest { get; init; } = null!;
        public string FileName => Manifest.ModelId;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int FileSizeMB { get; set; }
        public bool IsSelectable { get; set; } = true;

        // Single-engine post-Parakeet swap — no Pro gating, no lock state.
        public bool CanSelect => IsSelectable && IsInstalled;
        public Visibility SelectionVisibility =>
            IsSelectable ? Visibility.Visible : Visibility.Collapsed;

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
            set
            {
                isSelected = value;
                OnPropertyChanged();
            }
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
            set
            {
                downloadProgress = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DownloadProgressText));
            }
        }

        public string StatusBadge => IsInstalled ? "✓ Installed" : string.Empty;
        public Brush StatusColor => new SolidColorBrush(Colors.Green);
        public string ActionButtonText =>
            IsInstalled ? "Installed" : $"Download ({FileSizeMB}MB)";
        public Visibility ActionButtonVisibility =>
            IsInstalled ? Visibility.Collapsed : Visibility.Visible;
        public bool ActionButtonEnabled => !IsDownloading;
        public Visibility DownloadProgressVisibility =>
            IsDownloading ? Visibility.Visible : Visibility.Collapsed;
        public string DownloadProgressText => $"{DownloadProgress}%";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

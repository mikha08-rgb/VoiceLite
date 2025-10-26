using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using VoiceLite.Core.Interfaces.Controllers;
using VoiceLite.Core.Interfaces.Features;
using VoiceLite.Core.Interfaces.Services;
using VoiceLite.Presentation.Commands;
using VoiceLite.Helpers;

namespace VoiceLite.Presentation.ViewModels
{
    /// <summary>
    /// ViewModel for the main window
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly IRecordingController _recordingController;
        private readonly ITranscriptionController _transcriptionController;
        private readonly ISettingsService _settingsService;
        private readonly IProFeatureService _proFeatureService;
        private readonly ITranscriptionHistoryService _historyService;
        private readonly IHotkeyManager _hotkeyManager;
        private readonly ISystemTrayManager _systemTrayManager;
        private readonly IErrorLogger _errorLogger;

        // State properties
        private bool _isRecording;
        private bool _isTranscribing;
        private string _statusText = "Ready";
        private string _recordButtonText = "Start Recording";
        private int _progressValue;
        private string _currentModelName = "Tiny";
        private ObservableCollection<TranscriptionItem> _transcriptionHistory;
        private TranscriptionItem? _selectedHistoryItem;
        private bool _isModelDownloading;
        private string _modelDownloadProgress = string.Empty;
        private Visibility _proFeaturesVisibility = Visibility.Collapsed;

        // Properties
        public bool IsRecording
        {
            get => _isRecording;
            set
            {
                if (SetProperty(ref _isRecording, value))
                {
                    RecordButtonText = value ? "Stop Recording" : "Start Recording";
                    UpdateCommands();
                }
            }
        }

        public bool IsTranscribing
        {
            get => _isTranscribing;
            set
            {
                if (SetProperty(ref _isTranscribing, value))
                {
                    UpdateCommands();
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string RecordButtonText
        {
            get => _recordButtonText;
            set => SetProperty(ref _recordButtonText, value);
        }

        public int ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        public string CurrentModelName
        {
            get => _currentModelName;
            set => SetProperty(ref _currentModelName, value);
        }

        public ObservableCollection<TranscriptionItem> TranscriptionHistory
        {
            get => _transcriptionHistory;
            set => SetProperty(ref _transcriptionHistory, value);
        }

        public TranscriptionItem? SelectedHistoryItem
        {
            get => _selectedHistoryItem;
            set
            {
                if (SetProperty(ref _selectedHistoryItem, value))
                {
                    UpdateCommands();
                }
            }
        }

        public bool IsModelDownloading
        {
            get => _isModelDownloading;
            set
            {
                if (SetProperty(ref _isModelDownloading, value))
                {
                    UpdateCommands();
                }
            }
        }

        public string ModelDownloadProgress
        {
            get => _modelDownloadProgress;
            set => SetProperty(ref _modelDownloadProgress, value);
        }

        public Visibility ProFeaturesVisibility
        {
            get => _proFeaturesVisibility;
            set => SetProperty(ref _proFeaturesVisibility, value);
        }

        // Commands
        public ICommand StartStopRecordingCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand ExitApplicationCommand { get; }
        public ICommand ClearHistoryCommand { get; }
        public ICommand CopyTranscriptionCommand { get; }
        public ICommand DeleteHistoryItemCommand { get; }
        public ICommand PinHistoryItemCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ShowAboutCommand { get; }
        public ICommand MinimizeToTrayCommand { get; }

        public MainViewModel(
            IRecordingController recordingController,
            ITranscriptionController transcriptionController,
            ISettingsService settingsService,
            IProFeatureService proFeatureService,
            ITranscriptionHistoryService historyService,
            IHotkeyManager hotkeyManager,
            ISystemTrayManager systemTrayManager,
            IErrorLogger errorLogger)
        {
            // Store dependencies
            _recordingController = recordingController ?? throw new ArgumentNullException(nameof(recordingController));
            _transcriptionController = transcriptionController ?? throw new ArgumentNullException(nameof(transcriptionController));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _proFeatureService = proFeatureService ?? throw new ArgumentNullException(nameof(proFeatureService));
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
            _hotkeyManager = hotkeyManager ?? throw new ArgumentNullException(nameof(hotkeyManager));
            _systemTrayManager = systemTrayManager ?? throw new ArgumentNullException(nameof(systemTrayManager));
            _errorLogger = errorLogger ?? throw new ArgumentNullException(nameof(errorLogger));

            // Initialize collections
            _transcriptionHistory = new ObservableCollection<TranscriptionItem>();

            // Initialize commands
            StartStopRecordingCommand = new AsyncRelayCommand(ExecuteStartStopRecording, CanStartStopRecording);
            OpenSettingsCommand = new RelayCommand(ExecuteOpenSettings);
            ExitApplicationCommand = new RelayCommand(ExecuteExitApplication);
            ClearHistoryCommand = new RelayCommand(ExecuteClearHistory, CanClearHistory);
            CopyTranscriptionCommand = new RelayCommand<TranscriptionItem>(ExecuteCopyTranscription);
            DeleteHistoryItemCommand = new RelayCommand<TranscriptionItem>(ExecuteDeleteHistoryItem);
            PinHistoryItemCommand = new RelayCommand<TranscriptionItem>(ExecutePinHistoryItem);
            RefreshCommand = new AsyncRelayCommand(ExecuteRefresh);
            ShowAboutCommand = new RelayCommand(ExecuteShowAbout);
            MinimizeToTrayCommand = new RelayCommand(ExecuteMinimizeToTray);

            // Subscribe to controller events
            SubscribeToEvents();

            // Initialize
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                // Load settings
                await _settingsService.LoadSettingsAsync();

                // Update model name
                CurrentModelName = _settingsService.SelectedModel.ToUpper();

                // Load history
                LoadTranscriptionHistory();

                // Update Pro features visibility
                ProFeaturesVisibility = _proFeatureService.IsProUser ? Visibility.Visible : Visibility.Collapsed;

                // Validate transcription setup
                var validation = await _transcriptionController.ValidateTranscriptionSetupAsync();
                if (!validation.IsValid)
                {
                    foreach (var issue in validation.Issues)
                    {
                        _errorLogger.LogWarning($"Setup issue: {issue}");
                    }
                }

                StatusText = "Ready";
            }
            catch (Exception ex)
            {
                _errorLogger.LogError(ex, "Failed to initialize MainViewModel");
                StatusText = "Initialization failed";
            }
        }

        private void SubscribeToEvents()
        {
            // Recording controller events
            _recordingController.RecordingStarted += OnRecordingStarted;
            _recordingController.RecordingStopped += OnRecordingStopped;
            _recordingController.TranscriptionStarted += OnTranscriptionStarted;
            _recordingController.TranscriptionCompleted += OnTranscriptionCompleted;
            _recordingController.ProgressChanged += OnProgressChanged;

            // History service events
            _historyService.ItemAdded += OnHistoryItemAdded;

            // Hotkey events
            _hotkeyManager.HotkeyPressed += OnHotkeyPressed;

            // Settings changed events
            _settingsService.SettingChanged += OnSettingChanged;
        }

        private async Task ExecuteStartStopRecording()
        {
            try
            {
                if (!IsRecording)
                {
                    // Start recording
                    StatusText = "Starting recording...";
                    await _recordingController.StartRecordingAsync();
                }
                else
                {
                    // Stop recording and transcribe
                    StatusText = "Stopping recording...";
                    var result = await _recordingController.StopRecordingAsync(transcribe: true);

                    if (result.Success)
                    {
                        StatusText = $"Transcription complete ({result.ProcessingTime.TotalSeconds:F1}s)";
                    }
                    else
                    {
                        StatusText = $"Transcription failed: {result.Error}";
                        _errorLogger.LogError(new Exception(result.Error ?? "Unknown error"), "Transcription failed");
                    }
                }
            }
            catch (Exception ex)
            {
                _errorLogger.LogError(ex, "Failed to start/stop recording");
                StatusText = $"Error: {ex.Message}";
            }
        }

        private bool CanStartStopRecording()
        {
            return !IsTranscribing && !IsModelDownloading;
        }

        private void ExecuteOpenSettings()
        {
            try
            {
                // This will be handled by the View layer
                // For now, we can use a messenger or event
                OnOpenSettingsRequested();
            }
            catch (Exception ex)
            {
                _errorLogger.LogError(ex, "Failed to open settings");
            }
        }

        private void ExecuteExitApplication()
        {
            Application.Current.Shutdown();
        }

        private void ExecuteClearHistory()
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear all transcription history?\nPinned items will be preserved.",
                "Clear History",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _historyService.ClearHistory();
                LoadTranscriptionHistory();
                StatusText = "History cleared";
            }
        }

        private bool CanClearHistory()
        {
            return TranscriptionHistory?.Any() == true;
        }

        private void ExecuteCopyTranscription(TranscriptionItem? item)
        {
            if (item?.Text != null)
            {
                try
                {
                    Clipboard.SetText(item.Text);
                    StatusText = "Copied to clipboard";
                }
                catch (Exception ex)
                {
                    _errorLogger.LogError(ex, "Failed to copy to clipboard");
                    StatusText = "Failed to copy";
                }
            }
        }

        private void ExecuteDeleteHistoryItem(TranscriptionItem? item)
        {
            if (item != null)
            {
                _historyService.RemoveItem(item.Id);
                TranscriptionHistory.Remove(item);
                StatusText = "Item removed";
            }
        }

        private void ExecutePinHistoryItem(TranscriptionItem? item)
        {
            if (item != null)
            {
                _historyService.TogglePin(item.Id);
                item.IsPinned = !item.IsPinned;
                LoadTranscriptionHistory(); // Reload to reorder
                StatusText = item.IsPinned ? "Item pinned" : "Item unpinned";
            }
        }

        private async Task ExecuteRefresh()
        {
            StatusText = "Refreshing...";
            await InitializeAsync();
        }

        private void ExecuteShowAbout()
        {
            MessageBox.Show(
                "VoiceLite - AI-Powered Transcription\n\n" +
                "Version: 1.0.96\n" +
                "Powered by OpenAI Whisper\n\n" +
                "© 2024 VoiceLite",
                "About VoiceLite",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ExecuteMinimizeToTray()
        {
            _systemTrayManager.MinimizeToTray();
        }

        private void LoadTranscriptionHistory()
        {
            try
            {
                var history = _historyService.GetHistory().Take(50);
                TranscriptionHistory.Clear();

                foreach (var item in history)
                {
                    TranscriptionHistory.Add(item);
                }
            }
            catch (Exception ex)
            {
                _errorLogger.LogError(ex, "Failed to load history");
            }
        }

        private void UpdateCommands()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        // Event handlers
        private void OnRecordingStarted(object? sender, EventArgs e)
        {
            IsRecording = true;
            StatusText = "Recording...";
            ProgressValue = 10;
        }

        private void OnRecordingStopped(object? sender, EventArgs e)
        {
            IsRecording = false;
            StatusText = "Processing audio...";
            ProgressValue = 20;
        }

        private void OnTranscriptionStarted(object? sender, EventArgs e)
        {
            IsTranscribing = true;
            StatusText = "Transcribing...";
            ProgressValue = 30;
        }

        private void OnTranscriptionCompleted(object? sender, TranscriptionResult e)
        {
            IsTranscribing = false;
            ProgressValue = 100;

            if (e.Success)
            {
                StatusText = $"Complete! ({e.ProcessingTime.TotalSeconds:F1}s)";
            }
            else
            {
                StatusText = $"Failed: {e.Error}";
            }

            // Reset progress after a delay
            Task.Delay(2000).ContinueWith(_ =>
            {
                ProgressValue = 0;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void OnProgressChanged(object? sender, RecordingProgress e)
        {
            StatusText = e.Status;
            ProgressValue = e.PercentComplete;
        }

        private void OnHistoryItemAdded(object? sender, TranscriptionItem e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                TranscriptionHistory.Insert(0, e);

                // Limit displayed history
                while (TranscriptionHistory.Count > 50)
                {
                    TranscriptionHistory.RemoveAt(TranscriptionHistory.Count - 1);
                }
            });
        }

        private async void OnHotkeyPressed(object? sender, HotkeyEventArgs e)
        {
            // Handle global hotkey
            await ExecuteStartStopRecording();
        }

        private void OnSettingChanged(object? sender, SettingChangedEventArgs e)
        {
            if (e.SettingName == nameof(ISettingsService.SelectedModel))
            {
                CurrentModelName = e.NewValue?.ToString()?.ToUpper() ?? "UNKNOWN";
            }
        }

        // Events for View layer
        public event EventHandler? OpenSettingsRequested;

        protected virtual void OnOpenSettingsRequested()
        {
            OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
        }

        protected override void DisposeCore()
        {
            // Unsubscribe from events
            _recordingController.RecordingStarted -= OnRecordingStarted;
            _recordingController.RecordingStopped -= OnRecordingStopped;
            _recordingController.TranscriptionStarted -= OnTranscriptionStarted;
            _recordingController.TranscriptionCompleted -= OnTranscriptionCompleted;
            _recordingController.ProgressChanged -= OnProgressChanged;

            _historyService.ItemAdded -= OnHistoryItemAdded;
            _hotkeyManager.HotkeyPressed -= OnHotkeyPressed;
            _settingsService.SettingChanged -= OnSettingChanged;

            base.DisposeCore();
        }
    }
}
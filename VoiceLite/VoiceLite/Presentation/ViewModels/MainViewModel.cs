using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using VoiceLite.Core.Interfaces.Controllers;
using VoiceLite.Core.Interfaces.Features;
using VoiceLite.Core.Interfaces.Services;
using VoiceLite.Presentation.Commands;
using VoiceLite.Models;

namespace VoiceLite.Presentation.ViewModels
{
    /// <summary>
    /// Enhanced ViewModel for the main window with complete MVVM implementation
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        #region Dependencies

        private readonly IRecordingController _recordingController;
        private readonly ITranscriptionController _transcriptionController;
        private readonly ISettingsService _settingsService;
        private readonly IProFeatureService _proFeatureService;
        private readonly ITranscriptionHistoryService _historyService;
        private readonly IHotkeyManager _hotkeyManager;
        private readonly ISystemTrayManager _systemTrayManager;
        private readonly IErrorLogger _errorLogger;

        #endregion

        #region State Properties

        private bool _isRecording;
        private bool _isTranscribing;
        private bool _isProcessing;
        private bool _isInitialized;
        private bool _isApplicationExiting;

        private string _statusText = "Ready";
        private Brush _statusTextColor = Brushes.Green;
        private Brush _statusIndicatorColor = Brushes.LightGreen;

        private string _hotkeyDisplayText = "Alt";
        private string _currentModelName = "Tiny";
        private string _progressMessage = "Processing...";
        private int _progressValue;
        private bool _isProgressIndeterminate;

        private ObservableCollection<TranscriptionItem> _transcriptionHistory;
        private ICollectionView _historyView;
        private TranscriptionItem? _selectedHistoryItem;

        private string _searchText = "";
        private bool _isSearchVisible;
        private string _emptyHistoryMessage = "No transcriptions yet. Press hotkey or click 'Start Recording' to begin.";

        #endregion

        #region Public Properties

        public bool IsRecording
        {
            get => _isRecording;
            set
            {
                if (SetProperty(ref _isRecording, value))
                {
                    UpdateCommands();
                    OnPropertyChanged(nameof(CanRecord));
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
                    OnPropertyChanged(nameof(CanRecord));
                }
            }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }

        public bool IsApplicationExiting
        {
            get => _isApplicationExiting;
            set => SetProperty(ref _isApplicationExiting, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public Brush StatusTextColor
        {
            get => _statusTextColor;
            set => SetProperty(ref _statusTextColor, value);
        }

        public Brush StatusIndicatorColor
        {
            get => _statusIndicatorColor;
            set => SetProperty(ref _statusIndicatorColor, value);
        }

        public string HotkeyDisplayText
        {
            get => _hotkeyDisplayText;
            set => SetProperty(ref _hotkeyDisplayText, value);
        }

        public string CurrentModelName
        {
            get => _currentModelName;
            set => SetProperty(ref _currentModelName, value);
        }

        public string ProgressMessage
        {
            get => _progressMessage;
            set => SetProperty(ref _progressMessage, value);
        }

        public int ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }

        public ObservableCollection<TranscriptionItem> TranscriptionHistory
        {
            get => _transcriptionHistory;
            set => SetProperty(ref _transcriptionHistory, value);
        }

        public ICollectionView FilteredTranscriptionHistory => _historyView;

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

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterHistory();
                    OnPropertyChanged(nameof(HasSearchText));
                }
            }
        }

        public bool IsSearchVisible
        {
            get => _isSearchVisible;
            set => SetProperty(ref _isSearchVisible, value);
        }

        public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);

        public string EmptyHistoryMessage
        {
            get => _emptyHistoryMessage;
            set => SetProperty(ref _emptyHistoryMessage, value);
        }

        public bool IsHistoryEmpty => TranscriptionHistory?.Count == 0;
        public bool HasHistory => TranscriptionHistory?.Count > 0;
        public bool CanRecord => !IsRecording && !IsTranscribing && !IsProcessing;

        // Settings properties
        public bool ShowInTaskbar => !_settingsService.MinimizeToTray;
        public bool StartMinimized => _settingsService.StartMinimized;
        public bool MinimizeToTray => _settingsService.MinimizeToTray;
        public bool CloseToTray => _settingsService.CloseToTray;

        #endregion

        #region Commands

        public ICommand ToggleRecordingCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand ClearHistoryCommand { get; }
        public ICommand CopyToClipboardCommand { get; }
        public ICommand DeleteHistoryItemCommand { get; }
        public ICommand TogglePinCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand MinimizeToTrayCommand { get; }

        // Search commands
        public ICommand ToggleSearchCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ClearSearchCommand { get; }

        #endregion

        #region Events

        public event EventHandler? MinimizeToTrayRequested;
        public event EventHandler? ShowSettingsRequested;
        public event EventHandler? CloseRequested;

        #endregion

        #region Constructor

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
            _historyView = CollectionViewSource.GetDefaultView(_transcriptionHistory);
            _historyView.Filter = FilterHistoryItem;

            // Initialize commands
            ToggleRecordingCommand = new AsyncRelayCommand(ExecuteToggleRecording, CanToggleRecording);
            OpenSettingsCommand = new RelayCommand(ExecuteOpenSettings);
            ExitCommand = new RelayCommand(ExecuteExit);
            ClearHistoryCommand = new RelayCommand(ExecuteClearHistory, () => HasHistory);
            CopyToClipboardCommand = new RelayCommand<TranscriptionItem>(ExecuteCopyToClipboard);
            DeleteHistoryItemCommand = new RelayCommand<TranscriptionItem>(ExecuteDeleteHistoryItem);
            TogglePinCommand = new RelayCommand<TranscriptionItem>(ExecuteTogglePin);
            RefreshCommand = new AsyncRelayCommand(ExecuteRefresh);
            MinimizeToTrayCommand = new RelayCommand(ExecuteMinimizeToTray);

            // Search commands
            ToggleSearchCommand = new RelayCommand(ExecuteToggleSearch);
            SearchCommand = new RelayCommand(ExecuteSearch);
            ClearSearchCommand = new RelayCommand(ExecuteClearSearch);

            // Subscribe to events
            SubscribeToEvents();
        }

        #endregion

        #region Initialization

        public async Task InitializeAsync(IntPtr windowHandle)
        {
            if (_isInitialized) return;

            try
            {
                UpdateStatus("Initializing...", Brushes.Orange);

                // Load settings
                await _settingsService.LoadSettingsAsync();
                UpdateHotkeyDisplay();
                CurrentModelName = GetModelDisplayName(_settingsService.SelectedModel);

                // Register hotkeys
                _hotkeyManager.RegisterHotkey(windowHandle);

                // Load history
                await LoadTranscriptionHistoryAsync();

                // Validate setup
                var validation = await _transcriptionController.ValidateTranscriptionSetupAsync();
                if (!validation.IsValid)
                {
                    foreach (var issue in validation.Issues)
                    {
                        _errorLogger.LogWarning($"Setup issue: {issue}");
                    }
                    UpdateStatus("Ready (with warnings)", Brushes.Orange);
                }
                else
                {
                    UpdateStatus("Ready", Brushes.Green);
                }

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                _errorLogger.LogError(ex, "Failed to initialize MainViewModel");
                UpdateStatus("Initialization failed", Brushes.Red);

                MessageBox.Show(
                    $"Failed to initialize: {ex.Message}",
                    "VoiceLite Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public async Task ShutdownAsync()
        {
            IsApplicationExiting = true;

            try
            {
                UpdateStatus("Shutting down...", Brushes.Orange);

                // Stop any ongoing recording
                if (IsRecording)
                {
                    await _recordingController.StopRecordingAsync(transcribe: false);
                }

                // Cancel any ongoing transcription
                if (IsTranscribing)
                {
                    // TODO: Add CancelCurrentTranscriptionAsync to ITranscriptionController
                    // await _transcriptionController.CancelCurrentTranscriptionAsync();
                }

                // Save settings
                await _settingsService.SaveSettingsAsync();

                // Unregister hotkeys
                _hotkeyManager.UnregisterAllHotkeys();

                // Dispose system tray
                _systemTrayManager.Dispose();
            }
            catch (Exception ex)
            {
                _errorLogger.LogError(ex, "Error during shutdown");
            }
        }

        #endregion

        #region Command Implementations

        private async Task ExecuteToggleRecording()
        {
            try
            {
                if (!IsRecording)
                {
                    UpdateStatus("Starting recording...", Brushes.Orange);
                    await _recordingController.StartRecordingAsync();
                    IsRecording = true;
                    UpdateStatus("Recording... (press hotkey to stop)", Brushes.Red);
                }
                else
                {
                    UpdateStatus("Stopping recording...", Brushes.Orange);
                    IsRecording = false;

                    var result = await _recordingController.RecordAndTranscribeAsync(
                        _settingsService.GetModelPath(),
                        _settingsService.InjectionMode);

                    if (result.Success)
                    {
                        UpdateStatus($"Complete! ({result.ProcessingTime.TotalSeconds:F1}s)", Brushes.Green);

                        // Add to history
                        var item = new TranscriptionItem
                        {
                            Id = Guid.NewGuid().ToString(),
                            Text = result.Text ?? "",
                            Timestamp = DateTime.Now,
                            ProcessingTime = result.ProcessingTime.TotalSeconds,
                            ModelUsed = CurrentModelName
                        };

                        _historyService.AddItem(item);
                    }
                    else
                    {
                        UpdateStatus($"Failed: {result.Error}", Brushes.Red);
                    }
                }
            }
            catch (Exception ex)
            {
                _errorLogger.LogError(ex, "Failed to toggle recording");
                UpdateStatus($"Error: {ex.Message}", Brushes.Red);
                IsRecording = false;
            }
        }

        private bool CanToggleRecording()
        {
            return !IsTranscribing && !IsProcessing;
        }

        private void ExecuteOpenSettings()
        {
            ShowSettingsRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ExecuteExit()
        {
            IsApplicationExiting = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
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
                _historyService.ClearHistory(preservePinned: true);
                _ = LoadTranscriptionHistoryAsync();
                UpdateStatus("History cleared", Brushes.Green);
            }
        }

        private void ExecuteCopyToClipboard(TranscriptionItem? item)
        {
            if (item?.Text != null)
            {
                try
                {
                    Clipboard.SetText(item.Text);
                    UpdateStatus("Copied to clipboard", Brushes.Green);
                }
                catch (Exception ex)
                {
                    _errorLogger.LogError(ex, "Failed to copy to clipboard");
                    UpdateStatus("Failed to copy", Brushes.Red);
                }
            }
        }

        private void ExecuteDeleteHistoryItem(TranscriptionItem? item)
        {
            if (item != null)
            {
                _historyService.RemoveItem(item.Id);
                TranscriptionHistory.Remove(item);
                UpdateStatus("Item deleted", Brushes.Green);
                OnPropertyChanged(nameof(IsHistoryEmpty));
                OnPropertyChanged(nameof(HasHistory));
            }
        }

        private void ExecuteTogglePin(TranscriptionItem? item)
        {
            if (item != null)
            {
                _historyService.TogglePin(item.Id);
                item.IsPinned = !item.IsPinned;
                _historyView.Refresh(); // Refresh to re-sort
                UpdateStatus(item.IsPinned ? "Item pinned" : "Item unpinned", Brushes.Green);
            }
        }

        private async Task ExecuteRefresh()
        {
            UpdateStatus("Refreshing...", Brushes.Orange);
            await LoadTranscriptionHistoryAsync();
            UpdateStatus("Ready", Brushes.Green);
        }

        private void ExecuteMinimizeToTray()
        {
            MinimizeToTrayRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ExecuteToggleSearch()
        {
            IsSearchVisible = !IsSearchVisible;
            if (!IsSearchVisible)
            {
                SearchText = "";
            }
        }

        private void ExecuteSearch()
        {
            FilterHistory();
        }

        private void ExecuteClearSearch()
        {
            SearchText = "";
            IsSearchVisible = false;
        }

        #endregion

        #region Helper Methods

        private void UpdateStatus(string text, Brush color)
        {
            StatusText = text;
            StatusTextColor = color;
            StatusIndicatorColor = GetIndicatorColor(color);
        }

        private Brush GetIndicatorColor(Brush statusColor)
        {
            if (statusColor == Brushes.Green) return Brushes.LightGreen;
            if (statusColor == Brushes.Red) return Brushes.LightCoral;
            if (statusColor == Brushes.Orange) return Brushes.LightGoldenrodYellow;
            return Brushes.LightGray;
        }

        private void UpdateHotkeyDisplay()
        {
            var key = _hotkeyManager.CurrentKey;
            var modifiers = _hotkeyManager.CurrentModifiers;

            var display = "";
            if (modifiers.HasFlag(ModifierKeys.Control)) display += "Ctrl+";
            if (modifiers.HasFlag(ModifierKeys.Alt)) display += "Alt+";
            if (modifiers.HasFlag(ModifierKeys.Shift)) display += "Shift+";
            if (modifiers.HasFlag(ModifierKeys.Windows)) display += "Win+";

            display += key.ToString();
            HotkeyDisplayText = display;
        }

        private string GetModelDisplayName(string modelPath)
        {
            if (modelPath.Contains("tiny")) return "Lite (Free)";
            if (modelPath.Contains("base")) return "Swift";
            if (modelPath.Contains("small")) return "Pro ⭐";
            if (modelPath.Contains("medium")) return "Elite";
            if (modelPath.Contains("large")) return "Ultra";
            return "Unknown";
        }

        private async Task LoadTranscriptionHistoryAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    var history = _historyService.GetHistory().Take(100).ToList();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TranscriptionHistory.Clear();
                        foreach (var item in history)
                        {
                            TranscriptionHistory.Add(item);
                        }

                        OnPropertyChanged(nameof(IsHistoryEmpty));
                        OnPropertyChanged(nameof(HasHistory));
                    });
                }
                catch (Exception ex)
                {
                    _errorLogger.LogError(ex, "Failed to load history");
                }
            });
        }

        private void FilterHistory()
        {
            _historyView?.Refresh();
        }

        private bool FilterHistoryItem(object item)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            if (item is TranscriptionItem transcription)
            {
                return transcription.Text?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false;
            }

            return true;
        }

        public void RefreshSettings()
        {
            // Reload settings after settings window closes
            _ = _settingsService.LoadSettingsAsync();
            UpdateHotkeyDisplay();
            CurrentModelName = GetModelDisplayName(_settingsService.SelectedModel);

            OnPropertyChanged(nameof(ShowInTaskbar));
            OnPropertyChanged(nameof(StartMinimized));
            OnPropertyChanged(nameof(MinimizeToTray));
            OnPropertyChanged(nameof(CloseToTray));
        }

        public void ShowTrayNotification(string title, string message)
        {
            _systemTrayManager.ShowBalloonTip(title, message);
        }

        private void UpdateCommands()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        #endregion

        #region Event Handlers

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
            _hotkeyManager.HotkeyReleased += OnHotkeyReleased;

            // Settings changed events
            _settingsService.SettingChanged += OnSettingChanged;
        }

        private void OnRecordingStarted(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsRecording = true;
                UpdateStatus("Recording...", Brushes.Red);
            });
        }

        private void OnRecordingStopped(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsRecording = false;
                UpdateStatus("Processing audio...", Brushes.Orange);
            });
        }

        private void OnTranscriptionStarted(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsTranscribing = true;
                IsProcessing = true;
                ProgressMessage = "Transcribing with " + CurrentModelName + "...";
                IsProgressIndeterminate = true;
                UpdateStatus("Transcribing...", Brushes.Orange);
            });
        }

        private void OnTranscriptionCompleted(object? sender, TranscriptionResult e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsTranscribing = false;
                IsProcessing = false;

                if (e.Success)
                {
                    UpdateStatus($"Complete! ({e.ProcessingTime.TotalSeconds:F1}s)", Brushes.Green);
                }
                else
                {
                    UpdateStatus($"Failed: {e.Error}", Brushes.Red);
                }
            });
        }

        private void OnProgressChanged(object? sender, RecordingProgress e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ProgressMessage = e.Status;
                ProgressValue = e.PercentComplete;
                IsProgressIndeterminate = e.IsIndeterminate;
            });
        }

        private void OnHistoryItemAdded(object? sender, TranscriptionItem e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                TranscriptionHistory.Insert(0, e);

                // Limit displayed history
                while (TranscriptionHistory.Count > 100)
                {
                    TranscriptionHistory.RemoveAt(TranscriptionHistory.Count - 1);
                }

                OnPropertyChanged(nameof(IsHistoryEmpty));
                OnPropertyChanged(nameof(HasHistory));
            });
        }

        private async void OnHotkeyPressed(object? sender, EventArgs e)
        {
            // Toggle mode (default): Press once to start/stop (Dragon-like behavior)
            if (!IsTranscribing)
            {
                await ExecuteToggleRecording();
            }
        }

        private void OnHotkeyReleased(object? sender, EventArgs e)
        {
            // Toggle mode: Do nothing on key release
            // Note: PushToTalk mode can be re-enabled in future by checking RecordMode setting
        }

        private void OnSettingChanged(object? sender, SettingChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                switch (e.SettingName)
                {
                    case nameof(ISettingsService.SelectedModel):
                        CurrentModelName = GetModelDisplayName(e.NewValue?.ToString() ?? "");
                        break;

                    case nameof(ISettingsService.HotkeyKey):
                    case nameof(ISettingsService.HotkeyModifiers):
                        UpdateHotkeyDisplay();
                        break;

                    case nameof(ISettingsService.MinimizeToTray):
                    case nameof(ISettingsService.CloseToTray):
                    case nameof(ISettingsService.StartMinimized):
                        OnPropertyChanged(nameof(ShowInTaskbar));
                        OnPropertyChanged(nameof(StartMinimized));
                        OnPropertyChanged(nameof(MinimizeToTray));
                        OnPropertyChanged(nameof(CloseToTray));
                        break;
                }
            });
        }

        #endregion

        #region Disposal

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
            _hotkeyManager.HotkeyReleased -= OnHotkeyReleased;

            _settingsService.SettingChanged -= OnSettingChanged;

            base.DisposeCore();
        }

        #endregion
    }
}
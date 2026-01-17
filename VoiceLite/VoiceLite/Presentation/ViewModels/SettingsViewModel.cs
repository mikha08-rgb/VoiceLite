using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using NAudio.Wave;
using VoiceLite.Core.Interfaces.Features;
using VoiceLite.Core.Interfaces.Services;
using VoiceLite.Presentation.Commands;
using VoiceLite.Services;

namespace VoiceLite.Presentation.ViewModels
{
    /// <summary>
    /// ViewModel for the settings window
    /// </summary>
    public class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly ILicenseService _licenseService;
        private readonly IProFeatureService _proFeatureService;
        private readonly IErrorLogger _errorLogger;

        // General settings
        private bool _minimizeToTray;
        private bool _startMinimized;
        private bool _startWithWindows;
        private bool _checkForUpdates;

        // AI Model settings
        private string _selectedModel = null!; // Initialized in InitializeCollections()
        private string _transcriptionLanguage = null!; // Initialized in InitializeCollections()
        private ObservableCollection<string> _availableModels = null!; // Initialized in InitializeCollections()
        private ObservableCollection<string> _availableLanguages = null!; // Initialized in InitializeCollections()
        private bool _useGpuAcceleration;

        // Text injection settings
        private string _injectionMode = null!; // Initialized in InitializeCollections()
        private int _typingDelayMs;
        private bool _addSpaceAfterInjection;
        private ObservableCollection<string> _injectionModes = null!; // Initialized in InitializeCollections()

        // Hotkey settings
        private string _globalHotkey = null!; // Initialized in InitializeCollections()
        private bool _hotkeysEnabled;

        // Audio settings
        private string _selectedAudioDevice = null!; // Initialized in InitializeCollections()
        private ObservableCollection<string> _availableAudioDevices = null!; // Initialized in InitializeCollections()
        private bool _playSoundFeedback;

        // History settings
        private int _maxHistoryItems;
        private bool _saveHistory;

        // License settings
        private string _licenseKey = string.Empty;
        private bool _isProUser;
        private string _licenseStatus = "Free Version";
        private Visibility _proTabsVisibility = Visibility.Collapsed;

        // Properties
        public bool MinimizeToTray
        {
            get => _minimizeToTray;
            set
            {
                if (SetProperty(ref _minimizeToTray, value))
                {
                    _settingsService.MinimizeToTray = value;
                }
            }
        }

        public bool StartMinimized
        {
            get => _startMinimized;
            set
            {
                if (SetProperty(ref _startMinimized, value))
                {
                    _settingsService.StartMinimized = value;
                }
            }
        }

        public bool StartWithWindows
        {
            get => _startWithWindows;
            set
            {
                if (SetProperty(ref _startWithWindows, value))
                {
                    _settingsService.StartWithWindows = value;
                    UpdateStartupRegistration(value);
                }
            }
        }

        public bool CheckForUpdates
        {
            get => _checkForUpdates;
            set
            {
                if (SetProperty(ref _checkForUpdates, value))
                {
                    _settingsService.CheckForUpdates = value;
                }
            }
        }

        public string SelectedModel
        {
            get => _selectedModel;
            set
            {
                if (SetProperty(ref _selectedModel, value))
                {
                    _settingsService.SelectedModel = value;
                }
            }
        }

        public string TranscriptionLanguage
        {
            get => _transcriptionLanguage;
            set
            {
                if (SetProperty(ref _transcriptionLanguage, value))
                {
                    _settingsService.TranscriptionLanguage = value;
                }
            }
        }

        public ObservableCollection<string> AvailableModels
        {
            get => _availableModels;
            set => SetProperty(ref _availableModels, value);
        }

        public ObservableCollection<string> AvailableLanguages
        {
            get => _availableLanguages;
            set => SetProperty(ref _availableLanguages, value);
        }

        public bool UseGpuAcceleration
        {
            get => _useGpuAcceleration;
            set
            {
                if (SetProperty(ref _useGpuAcceleration, value))
                {
                    _settingsService.UseGpuAcceleration = value;
                }
            }
        }

        public string InjectionMode
        {
            get => _injectionMode;
            set
            {
                if (SetProperty(ref _injectionMode, value))
                {
                    _settingsService.InjectionMode = ConvertToInjectionMode(value);
                }
            }
        }

        public int TypingDelayMs
        {
            get => _typingDelayMs;
            set
            {
                if (SetProperty(ref _typingDelayMs, value))
                {
                    _settingsService.TypingDelayMs = value;
                }
            }
        }

        public bool AddSpaceAfterInjection
        {
            get => _addSpaceAfterInjection;
            set
            {
                if (SetProperty(ref _addSpaceAfterInjection, value))
                {
                    _settingsService.AddSpaceAfterInjection = value;
                }
            }
        }

        public ObservableCollection<string> InjectionModes
        {
            get => _injectionModes;
            set => SetProperty(ref _injectionModes, value);
        }

        public string GlobalHotkey
        {
            get => _globalHotkey;
            set
            {
                if (SetProperty(ref _globalHotkey, value))
                {
                    _settingsService.GlobalHotkey = value;
                }
            }
        }

        public bool HotkeysEnabled
        {
            get => _hotkeysEnabled;
            set
            {
                if (SetProperty(ref _hotkeysEnabled, value))
                {
                    _settingsService.HotkeysEnabled = value;
                }
            }
        }

        public string SelectedAudioDevice
        {
            get => _selectedAudioDevice;
            set
            {
                if (SetProperty(ref _selectedAudioDevice, value))
                {
                    _settingsService.AudioInputDevice = value;
                }
            }
        }

        public ObservableCollection<string> AvailableAudioDevices
        {
            get => _availableAudioDevices;
            set => SetProperty(ref _availableAudioDevices, value);
        }

        public bool PlaySoundFeedback
        {
            get => _playSoundFeedback;
            set
            {
                if (SetProperty(ref _playSoundFeedback, value))
                {
                    _settingsService.PlaySoundFeedback = value;
                }
            }
        }

        public int MaxHistoryItems
        {
            get => _maxHistoryItems;
            set
            {
                if (SetProperty(ref _maxHistoryItems, value))
                {
                    _settingsService.MaxHistoryItems = value;
                }
            }
        }

        public bool SaveHistory
        {
            get => _saveHistory;
            set
            {
                if (SetProperty(ref _saveHistory, value))
                {
                    _settingsService.SaveHistory = value;
                }
            }
        }

        public string LicenseKey
        {
            get => _licenseKey;
            set => SetProperty(ref _licenseKey, value);
        }

        public bool IsProUser
        {
            get => _isProUser;
            set
            {
                if (SetProperty(ref _isProUser, value))
                {
                    UpdateProStatus();
                }
            }
        }

        public string LicenseStatus
        {
            get => _licenseStatus;
            set => SetProperty(ref _licenseStatus, value);
        }

        public Visibility ProTabsVisibility
        {
            get => _proTabsVisibility;
            set => SetProperty(ref _proTabsVisibility, value);
        }

        // Commands
        public ICommand SaveSettingsCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ResetToDefaultsCommand { get; }
        public ICommand ValidateLicenseCommand { get; }
        public ICommand PurchaseLicenseCommand { get; }
        public ICommand RecordHotkeyCommand { get; }
        public ICommand TestAudioCommand { get; }
        public ICommand ExportSettingsCommand { get; }
        public ICommand ImportSettingsCommand { get; }

        public SettingsViewModel(
            ISettingsService settingsService,
            ILicenseService licenseService,
            IProFeatureService proFeatureService,
            IErrorLogger errorLogger)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _licenseService = licenseService ?? throw new ArgumentNullException(nameof(licenseService));
            _proFeatureService = proFeatureService ?? throw new ArgumentNullException(nameof(proFeatureService));
            _errorLogger = errorLogger ?? throw new ArgumentNullException(nameof(errorLogger));

            // Initialize collections
            InitializeCollections();

            // Initialize commands
            SaveSettingsCommand = new AsyncRelayCommand(ExecuteSaveSettings);
            CancelCommand = new RelayCommand(ExecuteCancel);
            ResetToDefaultsCommand = new RelayCommand(ExecuteResetToDefaults);
            ValidateLicenseCommand = new AsyncRelayCommand(ExecuteValidateLicense, CanValidateLicense);
            PurchaseLicenseCommand = new RelayCommand(ExecutePurchaseLicense);
            RecordHotkeyCommand = new RelayCommand(ExecuteRecordHotkey);
            TestAudioCommand = new AsyncRelayCommand(ExecuteTestAudio);
            ExportSettingsCommand = new AsyncRelayCommand(ExecuteExportSettings);
            ImportSettingsCommand = new AsyncRelayCommand(ExecuteImportSettings);

            // Load current settings
            LoadSettings();
        }

        private void InitializeCollections()
        {
            // Available models (based on Pro status)
            _availableModels = new ObservableCollection<string>();
            UpdateAvailableModels();

            // Available languages
            _availableLanguages = new ObservableCollection<string>
            {
                "en", "es", "fr", "de", "it", "pt", "ru", "zh", "ja", "ko", "ar", "hi"
            };

            // Injection modes
            _injectionModes = new ObservableCollection<string>
            {
                "Smart Auto",
                "Always Type",
                "Always Paste"
            };

            // Audio devices (will be populated dynamically)
            _availableAudioDevices = new ObservableCollection<string>
            {
                "Default"
            };
        }

        private void LoadSettings()
        {
            // Load all settings from service
            _minimizeToTray = _settingsService.MinimizeToTray;
            _startMinimized = _settingsService.StartMinimized;
            _startWithWindows = _settingsService.StartWithWindows;
            _checkForUpdates = _settingsService.CheckForUpdates;

            _selectedModel = _settingsService.SelectedModel;
            _transcriptionLanguage = _settingsService.TranscriptionLanguage;
            _useGpuAcceleration = _settingsService.UseGpuAcceleration;

            _injectionMode = ConvertFromInjectionMode(_settingsService.InjectionMode);
            _typingDelayMs = _settingsService.TypingDelayMs;
            _addSpaceAfterInjection = _settingsService.AddSpaceAfterInjection;

            _globalHotkey = _settingsService.GlobalHotkey;
            _hotkeysEnabled = _settingsService.HotkeysEnabled;

            _selectedAudioDevice = _settingsService.AudioInputDevice;
            _playSoundFeedback = _settingsService.PlaySoundFeedback;

            _maxHistoryItems = _settingsService.MaxHistoryItems;
            _saveHistory = _settingsService.SaveHistory;

            // Load license info
            _licenseKey = _licenseService.GetStoredLicenseKey() ?? string.Empty;
            _isProUser = _licenseService.IsLicenseValid;
            UpdateProStatus();
        }

        private async Task ExecuteSaveSettings()
        {
            try
            {
                await _settingsService.SaveSettingsAsync();
                OnSettingsSaved();
            }
            catch (Exception ex)
            {
                _errorLogger.LogError(ex, "Failed to save settings");
                MessageBox.Show(
                    "Failed to save settings. Please try again.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ExecuteCancel()
        {
            // Reload original settings
            LoadSettings();
            OnCancelled();
        }

        private void ExecuteResetToDefaults()
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset all settings to defaults?",
                "Reset Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _settingsService.ResetToDefaults();
                LoadSettings();
            }
        }

        private async Task ExecuteValidateLicense()
        {
            try
            {
                LicenseStatus = "Validating...";
                var result = await _licenseService.ValidateLicenseAsync(LicenseKey);

                if (result.IsValid)
                {
                    _licenseService.SaveLicenseKey(LicenseKey);
                    IsProUser = true;
                    LicenseStatus = "Pro Version - License Valid";
                    UpdateAvailableModels();

                    MessageBox.Show(
                        "License validated successfully!\nPro features are now unlocked.",
                        "License Valid",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    LicenseStatus = "Invalid License Key";
                    MessageBox.Show(
                        "The license key is invalid.\nPlease check your key and try again.",
                        "Invalid License",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _errorLogger.LogError(ex, "License validation failed");
                LicenseStatus = "Validation failed";
                MessageBox.Show(
                    "Failed to validate license. Please check your internet connection and try again.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool CanValidateLicense()
        {
            return !string.IsNullOrWhiteSpace(LicenseKey);
        }

        private void ExecutePurchaseLicense()
        {
            // Open purchase URL
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://voicelite.app/purchase",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _errorLogger.LogError(ex, "Failed to open purchase URL");
            }
        }

        private void ExecuteRecordHotkey()
        {
            // This will be handled by the View
            OnRecordHotkeyRequested();
        }

        private async Task ExecuteTestAudio()
        {
            try
            {
                // Find device index from name
                int deviceIndex = -1;
                for (int i = 0; i < WaveInEvent.DeviceCount; i++)
                {
                    var caps = WaveInEvent.GetCapabilities(i);
                    if (caps.ProductName == SelectedAudioDevice)
                    {
                        deviceIndex = i;
                        break;
                    }
                }

                if (deviceIndex < 0)
                {
                    MessageBox.Show($"Audio device '{SelectedAudioDevice}' not found.", "Audio Test Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Test microphone by recording briefly and measuring level
                float maxLevel = 0;
                var tcs = new TaskCompletionSource<bool>();

                var waveIn = new WaveInEvent
                {
                    DeviceNumber = deviceIndex,
                    WaveFormat = new WaveFormat(16000, 16, 1)
                };

                try
                {
                    waveIn.DataAvailable += (s, e) =>
                    {
                        // Measure peak level from samples
                        for (int i = 0; i < e.BytesRecorded; i += 2)
                        {
                            short sample = BitConverter.ToInt16(e.Buffer, i);
                            float level = Math.Abs(sample / 32768f);
                            if (level > maxLevel) maxLevel = level;
                        }
                    };

                    waveIn.RecordingStopped += (s, e) => tcs.TrySetResult(true);

                    waveIn.StartRecording();
                    await Task.Delay(500); // Record for 500ms
                    waveIn.StopRecording();

                    // Wait for RecordingStopped with timeout to prevent infinite wait
                    await Task.WhenAny(tcs.Task, Task.Delay(2000));
                }
                finally
                {
                    waveIn?.Dispose();
                }

                // Show result
                string levelDesc = maxLevel switch
                {
                    < 0.01f => "No audio detected - check mic connection",
                    < 0.1f => "Low level - speak louder or check mic settings",
                    < 0.5f => "Good level",
                    _ => "Strong signal"
                };

                MessageBox.Show(
                    $"Device: {SelectedAudioDevice}\n" +
                    $"Peak Level: {maxLevel:P0}\n" +
                    $"Status: {levelDesc}",
                    "Audio Test Complete",
                    MessageBoxButton.OK,
                    maxLevel < 0.01f ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _errorLogger.LogError(ex, "Audio test failed");
                MessageBox.Show($"Audio test failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteExportSettings()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json",
                    DefaultExt = ".json",
                    FileName = $"VoiceLite_Settings_{DateTime.Now:yyyyMMdd}.json"
                };

                if (dialog.ShowDialog() == true)
                {
                    await _settingsService.ExportSettingsAsync(dialog.FileName);
                    MessageBox.Show("Settings exported successfully!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _errorLogger.LogError(ex, "Failed to export settings");
                MessageBox.Show("Failed to export settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteImportSettings()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json",
                    DefaultExt = ".json"
                };

                if (dialog.ShowDialog() == true)
                {
                    await _settingsService.ImportSettingsAsync(dialog.FileName);
                    LoadSettings();
                    MessageBox.Show("Settings imported successfully!", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _errorLogger.LogError(ex, "Failed to import settings");
                MessageBox.Show("Failed to import settings. The file may be corrupted or incompatible.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateProStatus()
        {
            if (_isProUser)
            {
                ProTabsVisibility = Visibility.Visible;
                LicenseStatus = $"Pro Version - {_licenseService.GetActivationCount()}/{_licenseService.GetMaxActivations()} devices";
            }
            else
            {
                ProTabsVisibility = Visibility.Collapsed;
                LicenseStatus = "Free Version";
            }
        }

        private void UpdateAvailableModels()
        {
            AvailableModels.Clear();

            if (_proFeatureService.IsProUser)
            {
                AvailableModels.Add("tiny");
                AvailableModels.Add("base");
                AvailableModels.Add("small");
                AvailableModels.Add("medium");
                AvailableModels.Add("large");
            }
            else
            {
                AvailableModels.Add("tiny");
            }

            // Ensure selected model is available
            if (!AvailableModels.Contains(_selectedModel))
            {
                SelectedModel = "tiny";
            }
        }

        private void UpdateStartupRegistration(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);

                if (key == null)
                {
                    _errorLogger.LogWarning("Could not open registry key for startup registration");
                    RevertStartupSetting(enable);
                    MessageBox.Show(
                        "Unable to modify Windows startup settings. Registry access denied.",
                        "Startup Configuration Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                const string appName = "VoiceLite";
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;

                if (string.IsNullOrEmpty(exePath))
                {
                    _errorLogger.LogWarning("Could not determine executable path for startup registration");
                    RevertStartupSetting(enable);
                    return;
                }

                if (enable)
                {
                    // Add to startup with /startup flag for silent launch
                    key.SetValue(appName, $"\"{exePath}\" /startup");
                    _errorLogger.LogInfo("VoiceLite added to Windows startup");
                }
                else
                {
                    key.DeleteValue(appName, throwOnMissingValue: false);
                    _errorLogger.LogInfo("VoiceLite removed from Windows startup");
                }
            }
            catch (Exception ex)
            {
                _errorLogger.LogError(ex, "Failed to update Windows startup registration");
                RevertStartupSetting(enable);
                MessageBox.Show(
                    $"Failed to update Windows startup: {ex.Message}",
                    "Startup Configuration Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void RevertStartupSetting(bool attemptedValue)
        {
            // Revert to opposite of what was attempted
            _startWithWindows = !attemptedValue;
            _settingsService.StartWithWindows = !attemptedValue;
            OnPropertyChanged(nameof(StartWithWindows));
        }

        private ITextInjector.InjectionMode ConvertToInjectionMode(string mode)
        {
            return mode switch
            {
                "Always Type" => ITextInjector.InjectionMode.Type,
                "Always Paste" => ITextInjector.InjectionMode.Paste,
                _ => ITextInjector.InjectionMode.SmartAuto
            };
        }

        private string ConvertFromInjectionMode(ITextInjector.InjectionMode mode)
        {
            return mode switch
            {
                ITextInjector.InjectionMode.Type => "Always Type",
                ITextInjector.InjectionMode.Paste => "Always Paste",
                _ => "Smart Auto"
            };
        }

        // Events
        public event EventHandler? SettingsSaved;
        public event EventHandler? Cancelled;
        public event EventHandler? RecordHotkeyRequested;

        protected virtual void OnSettingsSaved()
        {
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnCancelled()
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnRecordHotkeyRequested()
        {
            RecordHotkeyRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
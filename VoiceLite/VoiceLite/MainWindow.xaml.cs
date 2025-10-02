using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using VoiceLite.Models;
using VoiceLite.Services;
using VoiceLite.Interfaces;
using VoiceLite.Utilities;
using System.Text.Json;
using VoiceLite.Services.Auth;
using VoiceLite.Services.Licensing;

namespace VoiceLite
{
    public partial class MainWindow : Window
    {
        private AudioRecorder? audioRecorder;
        private ITranscriber? whisperService;
        private HotkeyManager? hotkeyManager;
        private TextInjector? textInjector;
        private SystemTrayManager? systemTrayManager;
        private MemoryMonitor? memoryMonitor;
        private TranscriptionHistoryService? historyService;
        private SoundService? soundService;
        private AnalyticsService? analyticsService;
        private DateTime recordingStartTime;
        private Settings settings = new();
        private AuthenticationService authenticationService = new();
        private AuthenticationCoordinator? authenticationCoordinator;
        private LicenseService licenseService = new();
        private UserSession? currentSession;
        private LicenseStatus currentLicenseStatus = LicenseStatus.Unknown;
        private bool _isRecording = false;
        private bool isRecording
        {
            get => _isRecording;
            set
            {
                if (_isRecording != value)
                {
                    ErrorLogger.LogMessage($"isRecording state change: {_isRecording} -> {value}");
                    _isRecording = value;
                }
            }
        }
        private bool isHotkeyMode = false;
        private readonly object recordingLock = new object();
        private DateTime lastClickTime = DateTime.MinValue;
        private DateTime lastHotkeyPressTime = DateTime.MinValue;
        private System.Timers.Timer? autoTimeoutTimer;
        private bool isCancelled = false; // Track if recording was cancelled
        private System.Windows.Threading.DispatcherTimer? recordingElapsedTimer;
        private System.Windows.Threading.DispatcherTimer? settingsSaveTimer;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();

            // Check dependencies before initializing services
            _ = CheckDependenciesAsync();

            InitializeServices();
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        private async Task CheckDependenciesAsync()
        {
            try
            {
                // First run comprehensive diagnostics
                var diagnostics = await StartupDiagnostics.RunCompleteDiagnosticsAsync();

                if (diagnostics.HasAnyIssues)
                {
                    ErrorLogger.LogMessage($"Startup issues detected: {diagnostics.GetSummary()}");

                    // Try to auto-fix issues
                    var issuesFixed = await StartupDiagnostics.TryAutoFixIssuesAsync(diagnostics);

                    if (issuesFixed)
                    {
                        ErrorLogger.LogMessage("Some issues were automatically fixed");
                        // Re-run diagnostics after fixes
                        diagnostics = await StartupDiagnostics.RunCompleteDiagnosticsAsync();
                    }

                    // Show remaining issues to user
                    if (diagnostics.HasAnyIssues)
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            var message = "VoiceLite detected some issues:\n\n" + diagnostics.GetSummary();

                            if (diagnostics.AntivirusIssues)
                            {
                                message += "\n\nSolution: Add VoiceLite to your antivirus exclusions.";
                            }

                            if (diagnostics.BlockedFilesIssue)
                            {
                                message += "\n\nSolution: Right-click VoiceLite.exe → Properties → Unblock";
                            }

                            if (diagnostics.ProtectedFolderIssue)
                            {
                                message += "\n\nSolution: Move VoiceLite to a different folder (e.g., Desktop or Documents)";
                            }

                            MessageBox.Show(message, "Setup Issues Detected", MessageBoxButton.OK, MessageBoxImage.Warning);
                        });
                    }
                }

                // Now check dependencies
                var result = await DependencyChecker.CheckAndInstallDependenciesAsync();

                if (!result.AllDependenciesMet)
                {
                    ErrorLogger.LogMessage($"Dependency check failed: {result.GetErrorMessage()}");

                    // Show user-friendly error
                    await Dispatcher.InvokeAsync(() =>
                    {
                        StatusText.Text = "Setup Required";

                        var message = result.GetErrorMessage() + "\n\n";

                        if (!result.VCRuntimeInstalled || !result.WhisperCanRun)
                        {
                            message += "Click OK to install required components automatically.";

                            var response = MessageBox.Show(
                                message,
                                "Setup Required",
                                MessageBoxButton.OKCancel,
                                MessageBoxImage.Information);

                            if (response == MessageBoxResult.OK)
                            {
                                // Re-run dependency installer
                                _ = Task.Run(async () =>
                                {
                                    var retry = await DependencyChecker.CheckAndInstallDependenciesAsync();
                                    if (retry.AllDependenciesMet)
                                    {
                                        await Dispatcher.InvokeAsync(() =>
                                        {
                                            StatusText.Text = "Ready";
                                        });
                                    }
                                });
                            }
                        }
                        else
                        {
                            MessageBox.Show(
                                message + "Please check the troubleshooting guide or reinstall.",
                                "Setup Required",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    });
                }
                else
                {
                    ErrorLogger.LogMessage("All dependencies verified successfully");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Dependency check failed", ex);
            }
        }

        private string GetAppDataDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VoiceLite");
        }

        private string GetSettingsPath()
        {
            return Path.Combine(GetAppDataDirectory(), "settings.json");
        }

        private void EnsureAppDataDirectoryExists()
        {
            try
            {
                var appDataDir = GetAppDataDirectory();
                if (!Directory.Exists(appDataDir))
                {
                    Directory.CreateDirectory(appDataDir);
                    ErrorLogger.LogMessage($"Created AppData directory: {appDataDir}");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to create AppData directory", ex);
            }
        }

        private void LoadSettings()
        {
            settings = new Settings();
            try
            {
                // Ensure AppData directory exists first
                EnsureAppDataDirectoryExists();

                string settingsPath = GetSettingsPath();

                // Try to migrate old settings from Program Files if they exist
                string oldSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                if (!File.Exists(settingsPath) && File.Exists(oldSettingsPath))
                {
                    try
                    {
                        File.Copy(oldSettingsPath, settingsPath);
                        ErrorLogger.LogMessage("Migrated settings from Program Files to AppData");
                    }
                    catch (Exception migrationEx)
                    {
                        ErrorLogger.LogError("Failed to migrate old settings", migrationEx);
                    }
                }

                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    var loadedSettings = JsonSerializer.Deserialize<Settings>(json);
                    settings = SettingsValidator.ValidateAndRepair(loadedSettings) ?? new Settings();
                    ErrorLogger.LogMessage($"Settings loaded from: {settingsPath}");

                    // Verify whisper model exists
                    ValidateWhisperModel();
                }
                else
                {
                    ErrorLogger.LogMessage("No existing settings found, using defaults");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("LoadSettings", ex);
                MessageBox.Show($"Failed to load settings: {ex.Message}\n\nUsing default settings.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                settings = new Settings();
            }

            MinimizeCheckBox.IsChecked = settings.MinimizeToTray;
            UpdateConfigDisplay();
        }

        /// <summary>
        /// Debounced settings save - queues save request and executes after 500ms of inactivity
        /// </summary>
        private void SaveSettings()
        {
            // Reset or create timer
            if (settingsSaveTimer == null)
            {
                settingsSaveTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                settingsSaveTimer.Tick += (s, e) =>
                {
                    settingsSaveTimer.Stop();
                    SaveSettingsInternal();
                };
            }

            // Restart timer (debounce)
            settingsSaveTimer.Stop();
            settingsSaveTimer.Start();
        }

        /// <summary>
        /// Internal method that actually saves settings to disk
        /// </summary>
        private void SaveSettingsInternal()
        {
            try
            {
                // Ensure AppData directory exists
                EnsureAppDataDirectoryExists();

                settings.MinimizeToTray = MinimizeCheckBox.IsChecked == true;
                string settingsPath = GetSettingsPath();
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsPath, json);
                ErrorLogger.LogMessage($"Settings saved to: {settingsPath}");
            }
            catch (UnauthorizedAccessException ex)
            {
                ErrorLogger.LogError("SaveSettings - Access Denied", ex);
                MessageBox.Show(
                    $"Cannot save settings due to permission issues.\n\n" +
                    $"Try running VoiceLite as administrator or check folder permissions.\n\n" +
                    $"Settings location: {GetSettingsPath()}",
                    "Settings Save Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("SaveSettings", ex);
                MessageBox.Show($"Failed to save settings: {ex.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void InitializeServices()
        {
            try
            {
                authenticationCoordinator = new AuthenticationCoordinator(authenticationService);

                // Initialize core services
                textInjector = new TextInjector(settings);
                audioRecorder = new AudioRecorder();
                hotkeyManager = new HotkeyManager();

                // Check for microphone first
                if (!AudioRecorder.HasAnyMicrophone())
                {
                    MessageBox.Show("No microphone detected!\n\nPlease connect a microphone and restart the application.",
                        "No Microphone", MessageBoxButton.OK, MessageBoxImage.Warning);
                    StatusText.Text = "No microphone detected";
                    StatusText.Foreground = Brushes.Red;
                }

                // Set the selected microphone if configured
                if (settings.SelectedMicrophoneIndex >= 0)
                {
                    audioRecorder.SetDevice(settings.SelectedMicrophoneIndex);
                }

                audioRecorder.AudioFileReady += OnAudioFileReady;

                // Use persistent Whisper service for better performance
                whisperService = new PersistentWhisperService(settings);

                hotkeyManager.HotkeyPressed += OnHotkeyPressed;
                hotkeyManager.HotkeyReleased += OnHotkeyReleased;

                systemTrayManager = new SystemTrayManager(this);
                systemTrayManager.AccountMenuClicked += OnTrayAccountMenuClicked;

                // Initialize memory monitoring
                memoryMonitor = new MemoryMonitor();
                memoryMonitor.MemoryAlert += OnMemoryAlert;

                // Initialize transcription history service
                historyService = new TranscriptionHistoryService(settings);

                // Initialize sound service
                soundService = new SoundService();

                // Initialize analytics service
                analyticsService = new AnalyticsService(settings);

                // Load and display existing history
                UpdateHistoryUI();

                _ = RestoreAccountAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var helper = new WindowInteropHelper(this);
                hotkeyManager?.RegisterHotkey(helper.Handle, settings.RecordHotkey, settings.HotkeyModifiers);

                string hotkeyDisplay = GetHotkeyDisplayString();
                UpdateUIForCurrentMode();
                UpdateConfigDisplay();

                // Check for analytics consent on first run
                CheckAnalyticsConsentAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to register hotkey: {ex.Message}\n\nThe hotkey may be in use by another application.",
                    "Hotkey Registration Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void CheckAnalyticsConsentAsync()
        {
            try
            {
                // If consent not asked yet (null), show consent dialog
                if (settings.EnableAnalytics == null)
                {
                    // Small delay to let the main window fully load
                    await Task.Delay(1000);

                    var consentWindow = new AnalyticsConsentWindow(settings);
                    consentWindow.Owner = this;
                    var result = consentWindow.ShowDialog();

                    // Save settings after consent decision
                    SaveSettings();

                    // If user consented, track app launch
                    if (settings.EnableAnalytics == true && analyticsService != null)
                    {
                        await analyticsService.TrackAppLaunchAsync();
                    }
                }
                else if (settings.EnableAnalytics == true && analyticsService != null)
                {
                    // User already consented, track app launch
                    await analyticsService.TrackAppLaunchAsync();
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Analytics consent check failed", ex);
                // Fail silently - analytics should never break the app
            }
        }

        private string GetHotkeyDisplayString()
        {
            return HotkeyDisplayHelper.Format(settings.RecordHotkey, settings.HotkeyModifiers);
        }

        private void UpdateConfigDisplay()
        {
            // Update hotkey display
            HotkeyText.Text = GetHotkeyDisplayString();

            // Update microphone display
            if (settings.SelectedMicrophoneIndex == -1)
            {
                MicrophoneText.Text = "Default Microphone";
            }
            else if (!string.IsNullOrEmpty(settings.SelectedMicrophoneName))
            {
                // Truncate long microphone names
                string micName = settings.SelectedMicrophoneName;
                if (micName.Length > 25)
                {
                    micName = micName.Substring(0, 22) + "...";
                }
                MicrophoneText.Text = micName;
            }
            else
            {
                MicrophoneText.Text = "Microphone " + settings.SelectedMicrophoneIndex;
            }

            // Update model display
            ModelText.Text = GetModelDisplayName(settings.WhisperModel);
        }

        private string GetModelDisplayName(string modelPath)
        {
            if (string.IsNullOrEmpty(modelPath))
                return "Tiny (Free)";

            // Extract model name from path
            string modelName = modelPath.ToLower();
            if (modelName.Contains("tiny"))
                return "Tiny (Free)";
            else if (modelName.Contains("base"))
                return "Base";
            else if (modelName.Contains("small"))
                return "Small";
            else if (modelName.Contains("medium"))
                return "Medium";
            else if (modelName.Contains("large"))
                return "Large";
            else
                return "Tiny (Free)"; // Default fallback
        }

        private void UpdateUIForCurrentMode()
        {
            string hotkeyDisplay = GetHotkeyDisplayString();

            if (settings.Mode == Models.RecordMode.PushToTalk)
            {
                TranscriptionText.Text = $"Ready! Press and hold {hotkeyDisplay} to record, release to transcribe and type.";
            }
            else // Toggle mode
            {
                if (isRecording)
                {
                    TranscriptionText.Text = $"Recording... Press {hotkeyDisplay} again to stop and transcribe.";
                }
                else
                {
                    TranscriptionText.Text = $"Ready! Press {hotkeyDisplay} to start recording, press again to stop and transcribe.";
                }
            }
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorLogger.LogDebug($"TestButton_Click: Entry - isRecording={isRecording}, isHotkeyMode={isHotkeyMode}");

            // Prevent rapid clicking (debounce to 300ms)
            var now = DateTime.Now;
            if ((now - lastClickTime).TotalMilliseconds < 300)
            {
                ErrorLogger.LogDebug("TestButton_Click: Debounced - too rapid clicking");
                return;
            }
            lastClickTime = now;

            if (audioRecorder == null)
            {
                ErrorLogger.LogWarning("TestButton_Click: AudioRecorder is null");
                UpdateStatus("Audio recorder not initialized", Brushes.Red);
                return;
            }

            lock (recordingLock)
            {
                if (!isRecording)
                {
                    ErrorLogger.LogInfo("User clicked to start recording");
                    StartRecording();
                }
                else
                {
                    ErrorLogger.LogInfo("User clicked to stop recording");
                    StopRecording();
                }
            }
        }

        private async void AccountButton_Click(object sender, RoutedEventArgs e)
        {
            if (authenticationCoordinator == null)
            {
                MessageBox.Show("Authentication services are not initialized yet.", "VoiceLite", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (currentSession == null)
            {
                var loginWindow = new LoginWindow(authenticationCoordinator)
                {
                    Owner = this,
                };

                if (loginWindow.ShowDialog() == true && loginWindow.Session != null)
                {
                    currentSession = loginWindow.Session;
                    settings.LastSignedInEmail = currentSession.Email;
                    SaveSettings();
                    await UpdateLicenseStatusAsync();
                    systemTrayManager?.UpdateAccountMenuText("Manage Account");
                }

                return;
            }

            // User is signed in - show account management
            var message = $"Signed in as: {currentSession.Email}\n\n";
            message += $"License Status: {currentLicenseStatus}\n\n";
            message += "Would you like to sign out?";

            var result = MessageBox.Show(message, "Account Management", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await authenticationCoordinator.SignOutAsync();
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("SignOutAsync", ex);
                    MessageBox.Show($"Sign out failed: {ex.Message}", "VoiceLite", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                currentSession = null;
                currentLicenseStatus = LicenseStatus.Unlicensed;
                UpdateAccountStatusUI("Not signed in", Brushes.Gray);
                systemTrayManager?.UpdateAccountMenuText("Sign In");
            }
        }

        private void UpdateStatus(string status, Brush color)
        {
            StatusText.Text = status;
            StatusText.Foreground = color;

            // Update the status indicator ellipse color
            if (StatusIndicator != null)
            {
                StatusIndicator.Fill = color;
                StatusIndicator.Opacity = 1.0;
            }
        }

        private void StartRecording()
        {
            ErrorLogger.LogDebug($"StartRecording: Entry - isRecording={isRecording}");

            // Pre-check: if already recording, do nothing
            if (isRecording)
            {
                ErrorLogger.LogDebug("StartRecording: Already recording, returning early");
                return;
            }

            try
            {
                // Set state BEFORE attempting to start (defensive)
                isRecording = true;
                isCancelled = false; // Reset cancel flag when starting
                recordingStartTime = DateTime.Now;

                audioRecorder?.StartRecording();

                // Only update UI if we get here successfully
                UpdateStatus("Recording 0:00", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")));

                // Add red border for visual recording indicator
                this.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
                this.BorderThickness = new Thickness(3);

                // Start elapsed time timer
                recordingElapsedTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                recordingElapsedTimer.Tick += (s, e) =>
                {
                    var elapsed = DateTime.Now - recordingStartTime;
                    UpdateStatus($"Recording {elapsed:m\\:ss}", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")));
                };
                recordingElapsedTimer.Start();

                // Update button and text based on mode
                // Recording started - mode-specific handling in hotkey manager

                UpdateUIForCurrentMode();
                TranscriptionText.Foreground = Brushes.Gray;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("No microphone"))
            {
                ErrorLogger.LogWarning($"StartRecording failed: No microphone - {ex.Message}");
                isRecording = false; // Reset on failure
                UpdateStatus("No microphone!", Brushes.Red);

                // Reset TranscriptionText to ready state
                UpdateUIForCurrentMode();
                TranscriptionText.Foreground = Brushes.Black;

                MessageBox.Show(ex.Message, "No Microphone", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Failed to access"))
            {
                ErrorLogger.LogWarning($"StartRecording failed: Microphone busy - {ex.Message}");
                isRecording = false; // Reset on failure
                UpdateStatus("Microphone busy", Brushes.Red);

                // Reset TranscriptionText to ready state
                UpdateUIForCurrentMode();
                TranscriptionText.Foreground = Brushes.Black;

                MessageBox.Show(ex.Message, "Microphone Access Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("StartRecording failed", ex);
                isRecording = false; // Reset on failure
                UpdateStatus("Error", Brushes.Red);

                // Reset TranscriptionText to ready state
                UpdateUIForCurrentMode();
                TranscriptionText.Foreground = Brushes.Black;

                MessageBox.Show($"Failed to start recording: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopRecording(bool cancel = false)
        {
            ErrorLogger.LogDebug($"StopRecording: Entry - isRecording={isRecording}, cancel={cancel}");

            // Pre-check: if not recording, do nothing
            if (!isRecording)
            {
                ErrorLogger.LogDebug("StopRecording: Not recording, returning early");
                return;
            }

            try
            {
                if (cancel)
                {
                    isCancelled = true;
                    ErrorLogger.LogInfo("Recording cancelled by user");
                }

                audioRecorder?.StopRecording();
                isRecording = false;

                if (cancel)
                {
                    UpdateStatus("Cancelled", Brushes.Gray);
                    UpdateUIForCurrentMode();
                }
                else
                {
                    UpdateStatus("Processing...", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12")));

                    // Reset TranscriptionText to show processing state
                    TranscriptionText.Text = "Processing audio...";
                    TranscriptionText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12"));
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("StopRecording failed", ex);
                // Even if stop fails, reset our state
                isRecording = false;
                UpdateStatus("Error stopping", Brushes.Red);

                // Reset UI to ready state on error
                string hotkeyDisplay = GetHotkeyDisplayString();
                TranscriptionText.Text = $"Ready! Press and hold {hotkeyDisplay} to record, release to transcribe and type.";
                TranscriptionText.Foreground = Brushes.Black;
            }
        }

        private void OnHotkeyPressed(object? sender, EventArgs e)
        {
            ErrorLogger.LogMessage($"OnHotkeyPressed: Entry - Mode={settings.Mode}, isRecording={isRecording}, isHotkeyMode={isHotkeyMode}");

            // Debounce protection - ignore rapid key presses (stronger protection)
            var now = DateTime.Now;
            if ((now - lastHotkeyPressTime).TotalMilliseconds < 250)
            {
                ErrorLogger.LogMessage("OnHotkeyPressed: Debounced - too rapid key press");
                return;
            }
            lastHotkeyPressTime = now;

            lock (recordingLock)
            {
                ErrorLogger.LogMessage($"OnHotkeyPressed: Inside lock - Mode={settings.Mode}, isRecording={isRecording}");

                if (settings.Mode == Models.RecordMode.PushToTalk)
                {
                    HandlePushToTalkPressed();
                }
                else if (settings.Mode == Models.RecordMode.Toggle)
                {
                    HandleToggleModePressed();
                }
            }

            ErrorLogger.LogMessage($"OnHotkeyPressed: Exit - Mode={settings.Mode}, isRecording={isRecording}, isHotkeyMode={isHotkeyMode}");
        }

        private void OnHotkeyReleased(object? sender, EventArgs e)
        {
            ErrorLogger.LogMessage($"OnHotkeyReleased: Entry - Mode={settings.Mode}, isRecording={isRecording}, isHotkeyMode={isHotkeyMode}");

            lock (recordingLock)
            {
                ErrorLogger.LogMessage($"OnHotkeyReleased: Inside lock - Mode={settings.Mode}, isRecording={isRecording}, isHotkeyMode={isHotkeyMode}");

                if (settings.Mode == Models.RecordMode.PushToTalk)
                {
                    HandlePushToTalkReleased();
                }
                // In Toggle mode, do nothing on release - all action happens on press
            }

            ErrorLogger.LogMessage($"OnHotkeyReleased: Exit - Mode={settings.Mode}, isRecording={isRecording}, isHotkeyMode={isHotkeyMode}");
        }

        private void HandlePushToTalkPressed()
        {
            // Validate current state before proceeding - defensive copy to prevent race conditions
            var recorder = audioRecorder; // Local copy to prevent null ref if disposed
            if (recorder == null)
            {
                ErrorLogger.LogMessage("HandlePushToTalkPressed: AudioRecorder is null, ignoring");
                return;
            }

            bool actuallyRecording = recorder.IsRecording;

            if (isRecording != actuallyRecording)
            {
                ErrorLogger.LogMessage($"HandlePushToTalkPressed: State mismatch detected - isRecording={isRecording}, actuallyRecording={actuallyRecording}. Syncing...");
                isRecording = actuallyRecording;
            }

            if (!isRecording)
            {
                ErrorLogger.LogMessage("HandlePushToTalkPressed: Setting isHotkeyMode=true and starting recording");
                isHotkeyMode = true;

                // Audio feedback for push-to-talk start
                if (settings.PlaySoundFeedback)
                {
                    soundService?.PlaySound();
                }

                StartRecording();
            }
            else
            {
                ErrorLogger.LogMessage("HandlePushToTalkPressed: Already recording, ignoring");
            }
        }

        private void HandlePushToTalkReleased()
        {
            // Validate current state before proceeding - defensive copy to prevent race conditions
            var recorder = audioRecorder; // Local copy to prevent null ref if disposed
            if (recorder == null)
            {
                ErrorLogger.LogMessage("HandlePushToTalkReleased: AudioRecorder is null, ignoring");
                return;
            }

            bool actuallyRecording = recorder.IsRecording;

            if (isRecording != actuallyRecording)
            {
                ErrorLogger.LogMessage($"HandlePushToTalkReleased: State mismatch detected - isRecording={isRecording}, actuallyRecording={actuallyRecording}. Syncing...");
                isRecording = actuallyRecording;
            }

            if (isRecording && isHotkeyMode)
            {
                ErrorLogger.LogMessage("HandlePushToTalkReleased: Stopping recording");

                // Audio feedback for push-to-talk stop
                if (settings.PlaySoundFeedback)
                {
                    soundService?.PlaySound();
                }

                StopRecording(false);
                isHotkeyMode = false;
                ErrorLogger.LogMessage("HandlePushToTalkReleased: isHotkeyMode reset to false");
            }
            else
            {
                ErrorLogger.LogMessage($"HandlePushToTalkReleased: Not stopping - isRecording={isRecording}, isHotkeyMode={isHotkeyMode}");
                // Defensive reset - but only if we're not actually recording
                if (isHotkeyMode && !actuallyRecording)
                {
                    isHotkeyMode = false;
                    ErrorLogger.LogMessage("HandlePushToTalkReleased: Defensive reset of isHotkeyMode");
                }
            }
        }

        private void HandleToggleModePressed()
        {
            // Validate current state before proceeding - defensive copy to prevent race conditions
            var recorder = audioRecorder; // Local copy to prevent null ref if disposed
            if (recorder == null)
            {
                ErrorLogger.LogMessage("HandleToggleModePressed: AudioRecorder is null, ignoring");
                return;
            }

            bool actuallyRecording = recorder.IsRecording;

            if (isRecording != actuallyRecording)
            {
                ErrorLogger.LogMessage($"HandleToggleModePressed: State mismatch detected - isRecording={isRecording}, actuallyRecording={actuallyRecording}. Syncing...");
                isRecording = actuallyRecording;
            }

            ErrorLogger.LogMessage($"HandleToggleModePressed: Current state - isRecording={isRecording}, audioRecorder.IsRecording={actuallyRecording}");

            if (!isRecording)
            {
                ErrorLogger.LogMessage("HandleToggleModePressed: TOGGLE ON - Starting continuous recording");

                // Audio feedback for toggle start
                if (settings.PlaySoundFeedback)
                {
                    soundService?.PlaySound();
                }

                StartRecording();
                StartAutoTimeoutTimer();
                ErrorLogger.LogMessage($"HandleToggleModePressed: After start - isRecording={isRecording}");
            }
            else
            {
                ErrorLogger.LogMessage("HandleToggleModePressed: TOGGLE OFF - Stopping recording");

                // Audio feedback for toggle stop
                if (settings.PlaySoundFeedback)
                {
                    soundService?.PlaySound();
                }

                StopRecording(false);
                StopAutoTimeoutTimer();
                ErrorLogger.LogMessage($"HandleToggleModePressed: After stop - isRecording={isRecording}");
            }
        }

        private void StartAutoTimeoutTimer()
        {
            // Auto-timeout for toggle mode to prevent forgotten hot mics
            const int timeoutMinutes = 5; // 5 minute timeout for safety

            StopAutoTimeoutTimer(); // Stop any existing timer

            autoTimeoutTimer = new System.Timers.Timer(timeoutMinutes * 60 * 1000); // Convert to milliseconds
            autoTimeoutTimer.Elapsed += OnAutoTimeout;
            autoTimeoutTimer.AutoReset = false; // Only fire once
            autoTimeoutTimer.Start();

            ErrorLogger.LogMessage($"StartAutoTimeoutTimer: Auto-timeout set for {timeoutMinutes} minutes");
        }

        private void StopAutoTimeoutTimer()
        {
            if (autoTimeoutTimer != null)
            {
                autoTimeoutTimer.Stop();
                autoTimeoutTimer.Dispose();
                autoTimeoutTimer = null;
                ErrorLogger.LogMessage("StopAutoTimeoutTimer: Auto-timeout timer stopped");
            }
        }

        private void OnAutoTimeout(object? sender, System.Timers.ElapsedEventArgs e)
        {
            ErrorLogger.LogMessage("OnAutoTimeout: Auto-timeout triggered - stopping recording for safety");

            Dispatcher.Invoke(() =>
            {
                lock (recordingLock)
                {
                    if (isRecording)
                    {
                        // Show timeout warning
                        UpdateStatus("Auto-timeout - Recording stopped", Brushes.Orange);

                        // Audio feedback for timeout
                        if (settings.PlaySoundFeedback)
                        {
                            soundService?.PlaySound();
                        }

                        StopRecording(false);
                        StopAutoTimeoutTimer();

                        // Show message to user
                        MessageBox.Show("Recording automatically stopped after 5 minutes for safety.\n\nThis prevents forgotten hot microphones.",
                            "Auto-Timeout", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            });
        }

        private async void OnAudioFileReady(object? sender, string audioFilePath)
        {
            ErrorLogger.LogMessage($"OnAudioFileReady: Entry - isRecording={isRecording}, isHotkeyMode={isHotkeyMode}, isCancelled={isCancelled}, file={audioFilePath}");

            // Track usage for free users
            {
                var duration = (DateTime.Now - recordingStartTime).TotalSeconds;

                // Update status to show remaining time
            }

            // If recording was cancelled, just clean up and return
            if (isCancelled)
            {
                ErrorLogger.LogMessage("OnAudioFileReady: Recording was cancelled, skipping transcription");
                isCancelled = false; // Reset flag

                // Clean up the audio file
                // Retry file deletion with delay to handle file locks
                _ = Task.Run(async () =>
                {
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            if (File.Exists(audioFilePath))
                            {
                                File.Delete(audioFilePath);
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            if (i == 2) // Last attempt
                                ErrorLogger.LogError("OnAudioFileReady.DeleteCancelledAudio", ex);
                            await Task.Delay(100); // Wait before retry
                        }
                    }
                });

                return;
            }

            string workingAudioPath = audioFilePath;
            bool createdCopy = false;

            try
            {
                if (File.Exists(audioFilePath))
                {
                    var audioDirectory = Path.GetDirectoryName(audioFilePath);
                    if (!string.IsNullOrEmpty(audioDirectory))
                    {
                        var uniqueFileName = $"audio_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}.wav";
                        var tempCopyPath = Path.Combine(audioDirectory, uniqueFileName);
                        File.Copy(audioFilePath, tempCopyPath);
                        workingAudioPath = tempCopyPath;
                        createdCopy = true;
                        ErrorLogger.LogMessage($"OnAudioFileReady: Using isolated copy {workingAudioPath}");
                    }
                }
            }
            catch (Exception copyEx)
            {
                ErrorLogger.LogError("OnAudioFileReady.CopyAudio", copyEx);
                workingAudioPath = audioFilePath;
            }

            // Stop elapsed time timer
            recordingElapsedTimer?.Stop();
            recordingElapsedTimer = null;

            // Update UI immediately on UI thread
            Dispatcher.Invoke(() => UpdateStatus("Transcribing...", Brushes.Blue));

            try
            {
                if (whisperService != null)
                {
                    // Run transcription on background thread
                    var transcription = await Task.Run(async () =>
                        await whisperService.TranscribeAsync(workingAudioPath));

                    ErrorLogger.LogMessage($"Transcription result: '{transcription?.Substring(0, Math.Min(transcription?.Length ?? 0, 50))}'... (length: {transcription?.Length})");

                    // Track analytics for successful transcription
                    if (!string.IsNullOrWhiteSpace(transcription) && analyticsService != null)
                    {
                        var wordCount = transcription.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
                        _ = analyticsService.TrackTranscriptionAsync(settings.WhisperModel, wordCount);
                    }

                    // Update UI on UI thread after transcription completes
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (!string.IsNullOrWhiteSpace(transcription))
                        {
                            TranscriptionText.Text = transcription;
                            TranscriptionText.Foreground = Brushes.Black;

                            // Add to history
                            var historyItem = new TranscriptionHistoryItem
                            {
                                Timestamp = DateTime.Now,
                                Text = transcription,
                                WordCount = transcription.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length,
                                DurationSeconds = (DateTime.Now - recordingStartTime).TotalSeconds,
                                ModelUsed = settings.WhisperModel
                            };

                            historyService?.AddToHistory(historyItem);
                            UpdateHistoryUI(); // Refresh the history panel
                            SaveSettings(); // Persist history to disk
                        }
                        else
                        {
                            TranscriptionText.Text = "(No speech detected)";
                            TranscriptionText.Foreground = Brushes.Gray;
                        }
                    });

                    // Handle text injection on background thread if we have text
                    if (!string.IsNullOrWhiteSpace(transcription) && textInjector != null)
                    {
                        textInjector.AutoPaste = settings.AutoPaste;

                        if (settings.AutoPaste)
                        {
                            ErrorLogger.LogMessage("Auto-pasting text via Ctrl+V simulation");
                            Dispatcher.Invoke(() => UpdateStatus("Pasting...", Brushes.Purple));
                        }
                        else
                        {
                            ErrorLogger.LogMessage("Copying text to clipboard (manual paste required)");
                            Dispatcher.Invoke(() => UpdateStatus("Copied to clipboard", Brushes.Blue));
                        }

                        // Run text injection on background thread
                        await Task.Run(() => textInjector.InjectText(transcription));
                    }
                    else if (textInjector == null)
                    {
                        ErrorLogger.LogMessage("TextInjector is null, cannot inject text");
                    }

                    // Final UI update with thread-safe state reset
                    Dispatcher.Invoke(() =>
                    {
                        ErrorLogger.LogMessage($"OnAudioFileReady: Final UI update - isRecording={isRecording}, isHotkeyMode={isHotkeyMode}");
                        UpdateStatus("Ready", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")));

                        // Remove window border when done recording
                        this.BorderThickness = new Thickness(0);

                        // Reset TranscriptionText to ready state after successful completion
                        string hotkeyDisplay = GetHotkeyDisplayString();
                        if (!string.IsNullOrWhiteSpace(transcription)) // Only schedule reset if we actually have transcription to show
                        {
                            Task.Delay(3000).ContinueWith(_ => Dispatcher.Invoke(() =>
                            {
                                if (!isRecording) // Only reset if not currently recording again
                                {
                                    UpdateUIForCurrentMode();
                                }
                            }));
                        }

                        // Only reset state if we're not currently in an active recording session
                        lock (recordingLock)
                        {
                            // In push-to-talk mode, only reset if the key is not currently held down
                            // In toggle mode, the recording should already be stopped by user action
                            if (settings.Mode == RecordMode.PushToTalk && isHotkeyMode)
                            {
                                ErrorLogger.LogMessage("OnAudioFileReady: Push-to-talk mode - keeping state (key still held)");
                                // Don't reset state - user is still holding the key
                            }
                            else
                            {
                                ErrorLogger.LogMessage($"OnAudioFileReady: Resetting state - isRecording from {isRecording} to false, isHotkeyMode from {isHotkeyMode} to false");
                                isRecording = false;
                                isHotkeyMode = false;
                                StopAutoTimeoutTimer();
                            }
                        }
                        ErrorLogger.LogMessage($"OnAudioFileReady: After state reset - isRecording={isRecording}, isHotkeyMode={isHotkeyMode}");
                    });
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        TranscriptionText.Text = "Whisper service not initialized";
                        TranscriptionText.Foreground = Brushes.Red;
                        UpdateStatus("Error", Brushes.Red);

                        // Reset to ready state after error
                        Task.Delay(3000).ContinueWith(_ => Dispatcher.Invoke(() =>
                        {
                            if (!isRecording)
                            {
                                UpdateUIForCurrentMode();
                                UpdateStatus("Ready", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7A7A7A")));
                            }
                        }));

                        // Reset state when whisper service is null, but respect push-to-talk mode
                        lock (recordingLock)
                        {
                            if (settings.Mode == RecordMode.PushToTalk && isHotkeyMode)
                            {
                                ErrorLogger.LogMessage("OnAudioFileReady: Whisper null - keeping push-to-talk state (key still held)");
                            }
                            else
                            {
                                ErrorLogger.LogMessage($"OnAudioFileReady: Whisper null - resetting state - isRecording from {isRecording} to false, isHotkeyMode from {isHotkeyMode} to false");
                                isRecording = false;
                                isHotkeyMode = false;
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("OnAudioFileReady", ex);
                ErrorLogger.LogMessage($"OnAudioFileReady: Exception - isRecording={isRecording}, isHotkeyMode={isHotkeyMode}");
                Dispatcher.Invoke(() =>
                {
                    TranscriptionText.Text = $"Transcription error: {ex.Message}";
                    TranscriptionText.Foreground = Brushes.Red;
                    UpdateStatus("Error", Brushes.Red);

                    // Reset to ready state after error
                    Task.Delay(3000).ContinueWith(_ =>
                    {
                        try { Dispatcher.Invoke(() =>
                        {
                            if (!isRecording)
                            {
                                UpdateUIForCurrentMode();
                                UpdateStatus("Ready", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7A7A7A")));
                            }
                        }); } catch { /* Dispatcher might be shut down */ }
                    });

                    // Reset state on error, but respect push-to-talk mode
                    lock (recordingLock)
                    {
                        if (settings.Mode == RecordMode.PushToTalk && isHotkeyMode)
                        {
                            ErrorLogger.LogMessage("OnAudioFileReady: Exception - keeping push-to-talk state (key still held)");
                        }
                        else
                        {
                            ErrorLogger.LogMessage($"OnAudioFileReady: Exception - resetting state - isRecording from {isRecording} to false, isHotkeyMode from {isHotkeyMode} to false");
                            isRecording = false;
                            isHotkeyMode = false;
                        }
                    }
                });
            }
            finally
            {
                if (createdCopy && workingAudioPath != audioFilePath)
                {
                    // Retry file deletion with delay to handle file locks
                    _ = Task.Run(async () =>
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            try
                            {
                                await Task.Delay(50); // Small delay to ensure file is released
                                File.Delete(workingAudioPath);
                                ErrorLogger.LogMessage($"OnAudioFileReady: Deleted isolated copy {workingAudioPath}");
                                break;
                            }
                            catch (Exception deleteEx)
                            {
                                if (i == 2) // Last attempt
                                    ErrorLogger.LogError("OnAudioFileReady.CleanupTempAudio", deleteEx);
                                await Task.Delay(100); // Wait before retry
                            }
                        }
                    });
                }
            }

            ErrorLogger.LogMessage($"OnAudioFileReady: Exit - isRecording={isRecording}, isHotkeyMode={isHotkeyMode}");
        }
        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                systemTrayManager?.MinimizeToTray();
            }
        }

        private void OnMemoryAlert(object? sender, MemoryAlertEventArgs e)
        {
            if (e.Level == MemoryAlertLevel.Critical || e.Level == MemoryAlertLevel.PotentialLeak)
            {
                Dispatcher.Invoke(() =>
                {
                    ErrorLogger.LogMessage($"Memory alert: {e.Message}");
                    // Could show a warning to user if needed
                });
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (MinimizeCheckBox.IsChecked == true)
            {
                e.Cancel = true;
                systemTrayManager?.MinimizeToTray();
            }
        }

        private void DictionaryButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DictionaryManagerWindow(settings);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                SaveSettings();
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var oldMode = settings.Mode;
            var oldHotkey = settings.RecordHotkey;
            var oldModifiers = settings.HotkeyModifiers;

            var settingsWindow = new SettingsWindowNew(settings, analyticsService, () => TestButton_Click(this, new RoutedEventArgs()));
            settingsWindow.Owner = this;

            if (settingsWindow.ShowDialog() == true)
            {
                settings = SettingsValidator.ValidateAndRepair(settingsWindow.Settings);
                MinimizeCheckBox.IsChecked = settings.MinimizeToTray;
                SaveSettings();

                // Log mode changes
                if (oldMode != settings.Mode)
                {
                    ErrorLogger.LogMessage($"SettingsButton_Click: Recording mode changed from {oldMode} to {settings.Mode}");
                }

                // Log hotkey changes
                if (oldHotkey != settings.RecordHotkey || oldModifiers != settings.HotkeyModifiers)
                {
                    ErrorLogger.LogMessage($"SettingsButton_Click: Hotkey changed from {HotkeyDisplayHelper.Format(oldHotkey, oldModifiers)} to {GetHotkeyDisplayString()}");
                }

                // CRITICAL FIX: Preserve current recording state and minimize service recreation
                bool wasRecording = isRecording;
                bool wasHotkeyMode = isHotkeyMode;

                // Only recreate if service is null (never had one)
                // Model switching is handled by the service itself in most cases
                if (whisperService == null)
                {
                    ErrorLogger.LogMessage($"Creating initial Whisper service with model: {settings.WhisperModel}");
                    whisperService = new PersistentWhisperService(settings);
                }
                else
                {
                    ErrorLogger.LogMessage("Whisper service already exists - skipping recreation for performance");
                }

                // CRITICAL FIX: Only change microphone if not currently recording
                if (audioRecorder != null && !isRecording && settings.SelectedMicrophoneIndex >= -1)
                {
                    try
                    {
                        audioRecorder.SetDevice(settings.SelectedMicrophoneIndex);
                        ErrorLogger.LogMessage($"Microphone changed to device index: {settings.SelectedMicrophoneIndex}");
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogMessage($"Failed to change microphone device: {ex.Message}");
                        // Don't throw - this is not fatal
                    }
                }
                else if (isRecording)
                {
                    ErrorLogger.LogMessage("Skipping microphone change - recording in progress");
                }

                // Only re-register hotkey if it actually changed
                if (oldHotkey != settings.RecordHotkey || oldModifiers != settings.HotkeyModifiers)
                {
                    try
                    {
                        ErrorLogger.LogMessage("Re-registering hotkey due to change");
                        hotkeyManager?.Dispose();
                        hotkeyManager = new HotkeyManager();
                        hotkeyManager.HotkeyPressed += OnHotkeyPressed;
                        hotkeyManager.HotkeyReleased += OnHotkeyReleased;
                        var helper = new WindowInteropHelper(this);
                        hotkeyManager.RegisterHotkey(helper.Handle, settings.RecordHotkey, settings.HotkeyModifiers);

                        // CRITICAL FIX: Restore recording state after hotkey recreation
                        isRecording = wasRecording;
                        isHotkeyMode = wasHotkeyMode;

                        ErrorLogger.LogMessage($"Hotkey re-registration successful. State restored: recording={isRecording}, hotkeyMode={isHotkeyMode}");
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogMessage($"Failed to register new hotkey: {ex.Message}");
                        MessageBox.Show($"Failed to apply new hotkey: {ex.Message}\n\nThe old hotkey may still be active.",
                            "Hotkey Registration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        // Don't return - continue with UI updates
                    }
                }
                else
                {
                    ErrorLogger.LogMessage("Hotkey unchanged - skipping re-registration");
                }

                // Always update UI regardless of service recreation success
                UpdateUIForCurrentMode();
                UpdateConfigDisplay();
            }
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Handle Ctrl+D - Open dictionary
            if (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.Control)
            {
                DictionaryButton_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            // Handle Ctrl+F - Toggle search box
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ToggleSearchButton_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            // Handle Ctrl+E - Export history
            if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ExportHistory_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            // Handle Ctrl+Shift+Delete - Clear all history
            if (e.Key == Key.Delete && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                ClearAllHistory_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            // Handle Ctrl+, - Open settings
            if (e.Key == Key.OemComma && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SettingsButton_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            // Handle Esc key to close search box (if open)
            if (e.Key == Key.Escape && SearchBoxRow.Visibility == Visibility.Visible)
            {
                SearchBoxRow.Visibility = Visibility.Collapsed;
                HistorySearchBox.Text = "";
                e.Handled = true;
                return;
            }

            // Handle Esc key to cancel recording
            if (e.Key == Key.Escape && isRecording)
            {
                ErrorLogger.LogMessage($"MainWindow_PreviewKeyDown: Esc pressed while recording - Mode={settings.Mode}");

                lock (recordingLock)
                {
                    if (isRecording)
                    {
                        // Audio feedback for cancel
                        if (settings.PlaySoundFeedback)
                        {
                            soundService?.PlaySound();
                        }

                        // Cancel the recording
                        StopRecording(true);
                        StopAutoTimeoutTimer();
                        isHotkeyMode = false;

                        ErrorLogger.LogMessage("MainWindow_PreviewKeyDown: Recording cancelled via Esc key");
                    }
                }

                e.Handled = true;
            }
        }

        private async Task RestoreAccountAsync()
        {
            if (authenticationCoordinator == null)
            {
                UpdateAccountStatusUI("Not signed in", Brushes.Gray);
                systemTrayManager?.UpdateAccountMenuText("Sign In");
                return;
            }

            try
            {
                var session = await authenticationCoordinator.TryRestoreSessionAsync().ConfigureAwait(false);
                if (session == null)
                {
                    UpdateAccountStatusUI("Not signed in", Brushes.Gray);
                    systemTrayManager?.UpdateAccountMenuText("Sign In");
                    return;
                }

                currentSession = session;
                settings.LastSignedInEmail = session.Email;
                SaveSettings();
                await UpdateLicenseStatusAsync().ConfigureAwait(false);

                // Update tray menu for signed-in user
                await Dispatcher.InvokeAsync(() =>
                {
                    systemTrayManager?.UpdateAccountMenuText("Manage Account");
                });
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("RestoreAccountAsync", ex);
                // Hide account status for errors - don't alarm free users
                UpdateAccountStatusUI("", Brushes.Transparent);
            }
        }

        private async Task UpdateLicenseStatusAsync()
        {
            try
            {
                // First validate local license file if it exists
                var validationResult = await licenseService.ValidateLocalLicenseAsync().ConfigureAwait(false);

                if (validationResult.IsValid)
                {
                    currentLicenseStatus = LicenseStatus.Active;

                    if (validationResult.IsInGracePeriod)
                    {
                        var expiresAt = DateTime.Parse(validationResult.Payload!.ExpiresAt);
                        var daysRemaining = (expiresAt - DateTime.UtcNow).Days;
                        UpdateAccountStatusUI($"Pro license (expires in {daysRemaining} days)", Brushes.Orange);
                        ErrorLogger.LogMessage($"License in grace period, expires in {daysRemaining} days");
                    }
                    else
                    {
                        UpdateAccountStatusUI("Pro license active (offline)", Brushes.SeaGreen);
                    }
                }
                else if (!string.IsNullOrEmpty(validationResult.Reason))
                {
                    ErrorLogger.LogMessage($"Local license validation failed: {validationResult.Reason}");
                }

                // Then check online status (if authenticated)
                if (currentSession != null)
                {
                    var status = await licenseService.GetCurrentStatusAsync().ConfigureAwait(false);
                    currentLicenseStatus = status;

                    switch (status)
                    {
                        case LicenseStatus.Active:
                            UpdateAccountStatusUI("Pro license active", Brushes.SeaGreen);
                            try
                            {
                                // Fetch and save updated license file
                                var fetched = await licenseService.FetchAndSaveLicenseAsync().ConfigureAwait(false);
                                if (fetched)
                                {
                                    ErrorLogger.LogMessage("License file updated from server");
                                }

                                await licenseService.SyncAsync().ConfigureAwait(false);
                            }
                            catch (Exception syncEx)
                            {
                                ErrorLogger.LogError("License sync", syncEx);
                            }
                            break;
                        case LicenseStatus.Expired:
                            UpdateAccountStatusUI("License expired", Brushes.OrangeRed);
                            break;
                        case LicenseStatus.Unlicensed:
                            UpdateAccountStatusUI("Signed in (no license)", Brushes.SlateBlue);
                            break;
                        default:
                            UpdateAccountStatusUI("License status unknown", Brushes.OrangeRed);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("UpdateLicenseStatusAsync", ex);
                // Hide licensing errors - they're logged but don't alarm users
                UpdateAccountStatusUI("", Brushes.Transparent);
            }
        }

        private void UpdateAccountStatusUI(string text, Brush brush)
        {
            Dispatcher.Invoke(() =>
            {
                AccountStatusText.Text = text;
                AccountStatusText.Foreground = brush;
            });
        }

        private void ValidateWhisperModel()
        {
            try
            {
                // Use PersistentWhisperService's logic: support both short names and full filenames
                var modelFile = settings.WhisperModel switch
                {
                    "tiny" => "ggml-tiny.bin",
                    "base" => "ggml-base.bin",
                    "small" => "ggml-small.bin",
                    "medium" => "ggml-medium.bin",
                    "large" => "ggml-large-v3.bin",
                    _ => settings.WhisperModel.EndsWith(".bin") ? settings.WhisperModel : "ggml-tiny.bin"
                };

                var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper", modelFile);
                if (!File.Exists(modelPath))
                {
                    // Fallback to ggml-tiny.bin (free tier default)
                    var fallbackPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper", "ggml-tiny.bin");
                    if (File.Exists(fallbackPath))
                    {
                        settings.WhisperModel = "ggml-tiny.bin";
                        ErrorLogger.LogMessage($"Model {modelFile} not found, falling back to ggml-tiny.bin (free tier)");
                        SaveSettings();
                    }
                    else
                    {
                        MessageBox.Show("Whisper model files not found!\n\nPlease ensure the whisper folder contains ggml-tiny.bin.",
                            "Missing Model Files", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("ValidateWhisperModel", ex);
            }
        }


        protected override void OnClosed(EventArgs e)
        {
            SaveSettings();
            StopAutoTimeoutTimer();
            autoTimeoutTimer = null; // Clear timer reference

            // Clean up with proper disposal order
            try
            {
                // Stop recording FIRST to ensure no active operations
                if (isRecording)
                {
                    StopRecording(true);
                }

                // Dispose services in reverse order of creation
                memoryMonitor?.Dispose();
                memoryMonitor = null;

                systemTrayManager?.Dispose();
                systemTrayManager = null;

                hotkeyManager?.Dispose();
                hotkeyManager = null;

                whisperService?.Dispose();
                whisperService = null;

                audioRecorder?.Dispose();
                audioRecorder = null;

                // Stop security protection
                SecurityService.StopProtection();

                // Note: Removed aggressive GC.Collect() - let .NET handle it
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("OnClosed cleanup", ex);
            }

            base.OnClosed(e);
        }

        private async void OnTrayAccountMenuClicked(object? sender, EventArgs e)
        {
            ShowMainWindow();
            // Call the account button logic directly
            await Dispatcher.InvokeAsync(() => AccountButton_Click(sender ?? this, new RoutedEventArgs()));
        }

        private async Task SignOutAsync()
        {
            try
            {
                await authenticationService.SignOutAsync().ConfigureAwait(false);
                currentSession = null;
                currentLicenseStatus = LicenseStatus.Unknown;
                settings.LastSignedInEmail = string.Empty;
                SaveSettings();

                await Dispatcher.InvokeAsync(() =>
                {
                    UpdateAccountStatusUI("Not signed in", Brushes.Gray);
                    systemTrayManager?.UpdateAccountMenuText("Sign In");
                });
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("SignOutAsync", ex);
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Failed to sign out: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private void ShowMainWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        // ===== TRANSCRIPTION HISTORY METHODS =====

        /// <summary>
        /// Updates the history panel UI with current history items.
        /// Creates visual cards for each transcription.
        /// </summary>
        private void UpdateHistoryUI()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // Update count
                    int count = settings.TranscriptionHistory?.Count ?? 0;
                    HistoryCountText.Text = count == 1 ? "1 item" : $"{count} items";

                    // Clear existing items
                    HistoryItemsPanel.Children.Clear();

                    // Show empty state if no history
                    if (count == 0)
                    {
                        EmptyHistoryMessage.Visibility = Visibility.Visible;
                        // Update empty state with current hotkey
                        var hotkeyDisplay = GetHotkeyDisplayString();
                        var hotkeyHintElement = EmptyHistoryMessage.FindName("EmptyStateHotkeyHint") as TextBlock;
                        if (hotkeyHintElement != null)
                        {
                            hotkeyHintElement.Text = $"Press {hotkeyDisplay} to start recording";
                        }
                        return;
                    }

                    EmptyHistoryMessage.Visibility = Visibility.Collapsed;

                    // Create UI for each history item - instant, no animations
                    foreach (var item in settings.TranscriptionHistory!)
                    {
                        var card = CreateHistoryCard(item);
                        HistoryItemsPanel.Children.Add(card);
                    }
                });
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("UpdateHistoryUI", ex);
            }
        }

        /// <summary>
        /// Creates a visual card for a single history item.
        /// </summary>
        private System.Windows.Controls.Border CreateHistoryCard(TranscriptionHistoryItem item)
        {
            // Main container
            var border = new System.Windows.Controls.Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 10),
                CornerRadius = new CornerRadius(6),
                Cursor = Cursors.Hand,
                Tag = item, // Store the item for event handlers
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 4,
                    ShadowDepth = 1,
                    Opacity = 0.06,
                    Direction = 270,
                    RenderingBias = RenderingBias.Quality
                },
                // Improve rendering clarity
                UseLayoutRounding = true,
                SnapsToDevicePixels = true
            };

            // Improve text rendering for the entire card
            System.Windows.Media.TextOptions.SetTextRenderingMode(border, System.Windows.Media.TextRenderingMode.ClearType);
            System.Windows.Media.TextOptions.SetTextFormattingMode(border, System.Windows.Media.TextFormattingMode.Display);

            // Enhanced hover effects

            // Click to copy
            border.MouseLeftButtonDown += (s, e) =>
            {
                try
                {
                    System.Windows.Clipboard.SetText(item.Text);
                    UpdateStatus("Copied to clipboard", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")));

                    // Revert to "Ready" after 1.5 seconds
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
                    timer.Tick += (ts, te) =>
                    {
                        UpdateStatus("Ready", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")));
                        timer.Stop();
                    };
                    timer.Start();
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Copy history item", ex);
                }
            };

            // Context menu
            var contextMenu = new System.Windows.Controls.ContextMenu();

            var copyMenuItem = new System.Windows.Controls.MenuItem { Header = "📋 Copy" };
            copyMenuItem.Click += (s, e) =>
            {
                try
                {
                    System.Windows.Clipboard.SetText(item.Text);
                    UpdateStatus("Copied to clipboard", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")));

                    // Revert to "Ready" after 1.5 seconds
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
                    timer.Tick += (ts, te) =>
                    {
                        UpdateStatus("Ready", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")));
                        timer.Stop();
                    };
                    timer.Start();
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Copy menu item", ex);
                }
            };
            contextMenu.Items.Add(copyMenuItem);

            var reinjectMenuItem = new System.Windows.Controls.MenuItem { Header = "📤 Re-inject" };
            reinjectMenuItem.Click += (s, e) =>
            {
                try
                {
                    textInjector?.InjectText(item.Text);
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Re-inject menu item", ex);
                }
            };
            contextMenu.Items.Add(reinjectMenuItem);

            contextMenu.Items.Add(new System.Windows.Controls.Separator());

            var pinMenuItem = new System.Windows.Controls.MenuItem { Header = item.IsPinned ? "📌 Unpin" : "📌 Pin" };
            pinMenuItem.Click += (s, e) =>
            {
                historyService?.TogglePin(item.Id);
                UpdateHistoryUI();
                SaveSettings();
            };
            contextMenu.Items.Add(pinMenuItem);

            var deleteMenuItem = new System.Windows.Controls.MenuItem { Header = "🗑️ Delete" };
            deleteMenuItem.Click += (s, e) =>
            {
                historyService?.RemoveFromHistory(item.Id);
                UpdateHistoryUI();
                SaveSettings();
            };
            contextMenu.Items.Add(deleteMenuItem);

            border.ContextMenu = contextMenu;

            // Content grid
            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

            // Timestamp and pin indicator
            var headerGrid = new System.Windows.Controls.Grid();
            System.Windows.Controls.Grid.SetRow(headerGrid, 0);
            headerGrid.Margin = new Thickness(0, 0, 0, 6);

            var relativeConverter = new Utilities.RelativeTimeConverter();
            var timeText = new System.Windows.Controls.TextBlock
            {
                Text = (string)relativeConverter.Convert(item.Timestamp, null!, null!, null!),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = item.Timestamp.ToString("MMM d, yyyy h:mm tt") // Full timestamp on hover
            };
            headerGrid.Children.Add(timeText);

            // Copy button (visible on hover) - NO ANIMATIONS
            var copyButton = new System.Windows.Controls.Button
            {
                Content = "📋 Copy",
                FontSize = 11,
                Padding = new Thickness(8, 3, 8, 3),
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0, // Hidden by default via opacity, NOT visibility
                IsHitTestVisible = false, // Can't be clicked when hidden
                Margin = new Thickness(0, 0, item.IsPinned ? 25 : 0, 0), // Space for pin icon if pinned
                UseLayoutRounding = true,
                SnapsToDevicePixels = true
            };

            // Improve text rendering clarity
            System.Windows.Media.TextOptions.SetTextRenderingMode(copyButton, System.Windows.Media.TextRenderingMode.ClearType);
            System.Windows.Media.TextOptions.SetTextFormattingMode(copyButton, System.Windows.Media.TextFormattingMode.Display);

            copyButton.Click += (s, e) =>
            {
                e.Handled = true; // Prevent card click event
                try
                {
                    System.Windows.Clipboard.SetText(item.Text);
                    UpdateStatus("Copied to clipboard", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")));

                    // Revert to "Ready" after 1.5 seconds
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
                    timer.Tick += (ts, te) =>
                    {
                        UpdateStatus("Ready", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")));
                        timer.Stop();
                    };
                    timer.Start();
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Copy button", ex);
                }
            };
            headerGrid.Children.Add(copyButton);

            if (item.IsPinned)
            {
                var pinIcon = new System.Windows.Controls.TextBlock
                {
                    Text = "📌",
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                headerGrid.Children.Add(pinIcon);
            }

            // Instant hover effects - NO animations
            border.MouseEnter += (s, e) =>
            {
                // Instant background change
                border.Background = new SolidColorBrush(Color.FromRgb(248, 249, 250));

                // Show copy button instantly via opacity (no WPF visibility animations)
                copyButton.Opacity = 1;
                copyButton.IsHitTestVisible = true;
            };

            border.MouseLeave += (s, e) =>
            {
                // Instant background reset
                border.Background = Brushes.White;

                // Hide copy button instantly via opacity
                copyButton.Opacity = 0;
                copyButton.IsHitTestVisible = false;
            };

            grid.Children.Add(headerGrid);

            // Transcription text
            var truncateConverter = new Utilities.TruncateTextConverter();
            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = (string)truncateConverter.Convert(item.Text, null!, null!, null!),
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 60,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 6),
                ToolTip = item.Text // Full text on hover
            };
            System.Windows.Controls.Grid.SetRow(textBlock, 1);
            grid.Children.Add(textBlock);

            // Metadata
            var metadataText = new System.Windows.Controls.TextBlock
            {
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166))
            };

            metadataText.Inlines.Add(new System.Windows.Documents.Run(item.WordCount.ToString()));
            metadataText.Inlines.Add(new System.Windows.Documents.Run(" words • "));
            metadataText.Inlines.Add(new System.Windows.Documents.Run($"{item.DurationSeconds:F1}s"));
            metadataText.Inlines.Add(new System.Windows.Documents.Run(" • "));
            metadataText.Inlines.Add(new System.Windows.Documents.Run(item.ModelUsed));

            System.Windows.Controls.Grid.SetRow(metadataText, 2);
            grid.Children.Add(metadataText);

            border.Child = grid;
            return border;
        }


        /// <summary>
        /// Clears all unpinned history items.
        /// </summary>
        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will remove all unpinned transcriptions from history.\n\nPinned items will be kept.\n\nContinue?",
                "Clear History",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                int cleared = historyService?.ClearHistory() ?? 0;
                UpdateHistoryUI();
                SaveSettings();

                MessageBox.Show(
                    $"Cleared {cleared} items from history.",
                    "History Cleared",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Clears ALL history items including pinned items.
        /// </summary>
        private void ClearAllHistory_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will remove ALL transcriptions from history including pinned items.\n\nThis action cannot be undone.\n\nContinue?",
                "Clear All History",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                int cleared = historyService?.ClearAllHistory() ?? 0;
                UpdateHistoryUI();
                SaveSettings();

                MessageBox.Show(
                    $"Cleared all {cleared} items from history.",
                    "History Cleared",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Handles search text changes for filtering history.
        /// </summary>
        private void HistorySearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = HistorySearchBox.Text?.ToLower() ?? "";

            // Show/hide clear button
            ClearSearchButton.Visibility = string.IsNullOrEmpty(searchText) ? Visibility.Collapsed : Visibility.Visible;

            // Filter history items
            if (string.IsNullOrEmpty(searchText))
            {
                // Show all items
                UpdateHistoryUI();
            }
            else
            {
                // Show only matching items
                FilterHistoryBySearch(searchText);
            }
        }

        /// <summary>
        /// Handles keyboard shortcuts in search box.
        /// </summary>
        private void HistorySearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                // Clear search on Escape
                HistorySearchBox.Text = "";
                e.Handled = true;
            }
        }

        /// <summary>
        /// Toggles search box visibility.
        /// </summary>
        private void ToggleSearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (SearchBoxRow.Visibility == Visibility.Collapsed)
            {
                SearchBoxRow.Visibility = Visibility.Visible;
                HistorySearchBox.Focus();
            }
            else
            {
                SearchBoxRow.Visibility = Visibility.Collapsed;
                HistorySearchBox.Text = ""; // Clear search when hiding
            }
        }

        /// <summary>
        /// Opens history actions menu.
        /// </summary>
        private void HistoryMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        /// <summary>
        /// Clears the search box.
        /// </summary>
        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            HistorySearchBox.Text = "";
            HistorySearchBox.Focus();
        }

        /// <summary>
        /// Filters history items by search text.
        /// </summary>
        private void FilterHistoryBySearch(string searchText)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // Clear existing items
                    HistoryItemsPanel.Children.Clear();

                    if (settings.TranscriptionHistory == null || settings.TranscriptionHistory.Count == 0)
                    {
                        EmptyHistoryMessage.Visibility = Visibility.Visible;
                        return;
                    }

                    // Filter items that match search text
                    var matchingItems = settings.TranscriptionHistory
                        .Where(item => item.Text.ToLower().Contains(searchText))
                        .ToList();

                    // Update count
                    HistoryCountText.Text = matchingItems.Count == 1
                        ? "1 match"
                        : $"{matchingItems.Count} matches";

                    // Show empty state if no matches
                    if (matchingItems.Count == 0)
                    {
                        EmptyHistoryMessage.Visibility = Visibility.Visible;
                        var emptyMessage = EmptyHistoryMessage.Children.OfType<TextBlock>().FirstOrDefault();
                        if (emptyMessage != null)
                        {
                            emptyMessage.Text = $"No results for \"{searchText}\"";
                        }
                        return;
                    }

                    EmptyHistoryMessage.Visibility = Visibility.Collapsed;

                    // Create UI for each matching item
                    foreach (var item in matchingItems)
                    {
                        var card = CreateHistoryCard(item);
                        HistoryItemsPanel.Children.Add(card);
                    }
                });
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("FilterHistoryBySearch", ex);
            }
        }

        /// <summary>
        /// Exports history to a text file.
        /// </summary>
        private void ExportHistory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"VoiceLite_History_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                    Filter = "Text files (*.txt)|*.txt|CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    DefaultExt = ".txt"
                };

                if (dialog.ShowDialog() == true)
                {
                    var content = new System.Text.StringBuilder();
                    content.AppendLine("VoiceLite Transcription History");
                    content.AppendLine($"Exported: {DateTime.Now:F}");
                    content.AppendLine(new string('=', 60));
                    content.AppendLine();

                    foreach (var item in settings.TranscriptionHistory!)
                    {
                        content.AppendLine($"[{item.Timestamp:yyyy-MM-dd HH:mm:ss}] {(item.IsPinned ? "📌 " : "")}");
                        content.AppendLine($"Text: {item.Text}");
                        content.AppendLine($"Words: {item.WordCount} | Duration: {item.DurationSeconds:F1}s | Model: {item.ModelUsed}");
                        content.AppendLine(new string('-', 60));
                        content.AppendLine();
                    }

                    File.WriteAllText(dialog.FileName, content.ToString());

                    MessageBox.Show(
                        $"History exported successfully to:\n{dialog.FileName}",
                        "Export Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("ExportHistory", ex);
                MessageBox.Show(
                    $"Failed to export history:\n{ex.Message}",
                    "Export Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}


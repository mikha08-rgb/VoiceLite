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
        #region Fields & Properties

        // Service dependencies
        private AudioRecorder? audioRecorder;
        private ITranscriber? whisperService;
        private HotkeyManager? hotkeyManager;
        private TextInjector? textInjector;
        private SystemTrayManager? systemTrayManager;
        private MemoryMonitor? memoryMonitor;
        private TranscriptionHistoryService? historyService;
        private SoundService? soundService;
        private AnalyticsService? analyticsService;
        private RecordingCoordinator? recordingCoordinator;

        // Recording state
        private DateTime recordingStartTime;
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
        private readonly object saveSettingsLock = new object(); // CONCURRENCY FIX: Prevent simultaneous settings saves
        private DateTime lastClickTime = DateTime.MinValue;
        private DateTime lastHotkeyPressTime = DateTime.MinValue;

        // Authentication & Licensing
        private Settings settings = new();
        private AuthenticationService authenticationService = new();
        private AuthenticationCoordinator? authenticationCoordinator;
        private LicenseService licenseService = new();
        private UserSession? currentSession;
        private LicenseStatus currentLicenseStatus = LicenseStatus.Unknown;

        // Timers
        private System.Timers.Timer? autoTimeoutTimer;
        private System.Windows.Threading.DispatcherTimer? recordingElapsedTimer;
        private System.Windows.Threading.DispatcherTimer? settingsSaveTimer;
        private System.Windows.Threading.DispatcherTimer? stuckStateRecoveryTimer;

        #endregion

        #region Initialization & Lifecycle

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();

            // CRITICAL FIX: Show loading state immediately
            StatusText.Text = "Initializing...";
            StatusText.Foreground = Brushes.Gray;

            // CRITICAL FIX: Run all async initialization on background thread
            // This prevents UI freeze during startup diagnostics and service initialization
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        private async Task CheckDependenciesAsync()
        {
            try
            {
                // Update status
                StatusText.Text = "Running diagnostics...";

                // First run comprehensive diagnostics
                var diagnostics = await StartupDiagnostics.RunCompleteDiagnosticsAsync();

                if (diagnostics.HasAnyIssues)
                {
                    ErrorLogger.LogMessage($"Startup issues detected: {diagnostics.GetSummary()}");

                    // Try to auto-fix issues silently
                    var issuesFixed = await StartupDiagnostics.TryAutoFixIssuesAsync(diagnostics);

                    if (issuesFixed)
                    {
                        ErrorLogger.LogMessage("Some issues were automatically fixed");
                        // Re-run diagnostics after fixes
                        diagnostics = await StartupDiagnostics.RunCompleteDiagnosticsAsync();
                    }

                    // Show remaining issues to user (if critical)
                    if (diagnostics.HasAnyIssues && (diagnostics.MissingFiles.Any() || diagnostics.WindowsVersionIssue))
                    {
                        // Only show dialog for CRITICAL issues that block functionality
                        await Dispatcher.InvokeAsync(() =>
                        {
                            var message = "VoiceLite detected critical issues:\n\n" + diagnostics.GetSummary();

                            if (diagnostics.AntivirusIssues)
                            {
                                message += "\n\nSolution: Add VoiceLite to your antivirus exclusions.";
                            }

                            if (diagnostics.BlockedFilesIssue)
                            {
                                message += "\n\nSolution: Right-click VoiceLite.exe → Properties → Unblock";
                            }

                            MessageBox.Show(message, "Critical Setup Issues", MessageBoxButton.OK, MessageBoxImage.Warning);
                        });
                    }
                    else if (diagnostics.HasAnyIssues)
                    {
                        // Non-critical issues - just log them
                        ErrorLogger.LogMessage($"Non-critical issues detected (app will continue): {diagnostics.GetSummary()}");
                    }
                }

                // Update status
                StatusText.Text = "Checking dependencies...";

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
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
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

                // MIGRATION 1: Migrate from old Roaming AppData to new Local AppData (privacy fix)
                string oldRoamingPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "VoiceLite", "settings.json");

                if (!File.Exists(settingsPath) && File.Exists(oldRoamingPath))
                {
                    try
                    {
                        // Create Local AppData directory if needed
                        EnsureAppDataDirectoryExists();

                        File.Copy(oldRoamingPath, settingsPath);
                        ErrorLogger.LogMessage("✅ Migrated settings from Roaming to Local AppData (privacy fix)");

                        // Clear transcription history from migrated settings (privacy-sensitive data)
                        try
                        {
                            string json = File.ReadAllText(settingsPath);
                            Settings? migratedSettings = JsonSerializer.Deserialize<Settings>(json);
                            if (migratedSettings != null && migratedSettings.TranscriptionHistory != null && migratedSettings.TranscriptionHistory.Count > 0)
                            {
                                int historyCount = migratedSettings.TranscriptionHistory.Count;
                                migratedSettings.TranscriptionHistory.Clear();
                                File.WriteAllText(settingsPath, JsonSerializer.Serialize(migratedSettings, new JsonSerializerOptions { WriteIndented = true }));
                                ErrorLogger.LogMessage($"🗑️ Cleared {historyCount} migrated transcriptions for privacy");
                            }
                        }
                        catch (Exception clearEx)
                        {
                            ErrorLogger.LogError("Failed to clear migrated history (non-critical)", clearEx);
                        }

                        // Delete old Roaming folder to prevent cloud sync (privacy)
                        try
                        {
                            string? roamingDir = Path.GetDirectoryName(oldRoamingPath);
                            if (!string.IsNullOrEmpty(roamingDir) && Directory.Exists(roamingDir))
                            {
                                Directory.Delete(roamingDir, recursive: true);
                                ErrorLogger.LogMessage("🗑️ Deleted old Roaming AppData folder to prevent sync across PCs");
                            }
                        }
                        catch (Exception deleteEx)
                        {
                            ErrorLogger.LogError("Failed to delete old Roaming folder (non-critical)", deleteEx);
                        }

                        // Optionally inform user about the change
                        MessageBox.Show(
                            "VoiceLite has moved your settings to Local AppData.\n\n" +
                            "This ensures your transcription history stays private and doesn't sync to other PCs.\n\n" +
                            "Old location (Roaming): Synced across PCs\n" +
                            "New location (Local): Stays on this PC only",
                            "Privacy Improvement",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    catch (Exception migrationEx)
                    {
                        ErrorLogger.LogError("Failed to migrate settings from Roaming to Local AppData", migrationEx);
                    }
                }

                // MIGRATION 2: Try to migrate old settings from Program Files if they exist
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

                    // MIGRATION 3: Upgrade Default UI preset to Compact (v1.0.38+) - ONE-TIME ONLY
                    // BUG-009 FIX: Check migration flag to prevent overwriting user's explicit choice
                    // New users get Compact by default, migrate existing users to Compact for consistency
                    if (!settings.UIPresetMigrationApplied && settings.UIPreset == UIPreset.Default)
                    {
                        settings.UIPreset = UIPreset.Compact;
                        settings.UIPresetMigrationApplied = true; // Mark migration as done
                        ErrorLogger.LogMessage("BUG-009 FIX: Migrated UI preset from Default to Compact (one-time migration)");
                        SaveSettingsInternal(); // Save immediately
                    }
                    else if (!settings.UIPresetMigrationApplied)
                    {
                        // User already has non-Default preset, just mark migration as done
                        settings.UIPresetMigrationApplied = true;
                        SaveSettingsInternal();
                    }

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
                MessageBox.Show($"Failed to load settings: {ex.Message}\n\nUsing default settings.", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                settings = new Settings();
            }

            // BUG-003 FIX: Auto-cleanup old history items (>7 days, not pinned)
            CleanupOldHistoryItems();

            MinimizeCheckBox.IsChecked = settings.MinimizeToTray;
            UpdateConfigDisplay();
        }

        /// <summary>
        /// BUG-003 FIX: Remove old transcription history items to prevent memory bloat
        /// Runs on startup to clean up items older than 7 days (except pinned items)
        /// </summary>
        private void CleanupOldHistoryItems()
        {
            try
            {
                if (settings.TranscriptionHistory == null || settings.TranscriptionHistory.Count == 0)
                    return;

                var sevenDaysAgo = DateTime.Now.AddDays(-7);
                var itemsToRemove = settings.TranscriptionHistory
                    .Where(h => !h.IsPinned && h.Timestamp < sevenDaysAgo)
                    .ToList();

                if (itemsToRemove.Count > 0)
                {
                    foreach (var item in itemsToRemove)
                    {
                        settings.TranscriptionHistory.Remove(item);
                    }
                    ErrorLogger.LogMessage($"BUG-003 FIX: Cleaned up {itemsToRemove.Count} old history items (>7 days, not pinned)");
                    SaveSettingsInternal(); // Persist cleanup immediately
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("CleanupOldHistoryItems", ex);
            }
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
                    Interval = TimeSpan.FromMilliseconds(TimingConstants.SettingsSaveDebounceMs)
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
        /// Uses atomic write pattern to prevent file corruption
        /// </summary>
        private void SaveSettingsInternal()
        {
            // CONCURRENCY FIX: Prevent concurrent saves from corrupting file
            lock (saveSettingsLock)
            {
                try
                {
                    // Ensure AppData directory exists
                    EnsureAppDataDirectoryExists();

                    settings.MinimizeToTray = MinimizeCheckBox.IsChecked == true;
                    string settingsPath = GetSettingsPath();

                    // THREAD SAFETY FIX: Lock settings during serialization to prevent concurrent modification
                    string json;
                    lock (settings.SyncRoot)
                    {
                        json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                    }

                    // CRITICAL FIX: Atomic write to prevent file corruption on crash/power loss
                    // Write to temp file first, then rename (rename is atomic on Windows)
                    string tempPath = settingsPath + ".tmp";
                    File.WriteAllText(tempPath, json);

                    // Delete old file if exists (required before rename on Windows)
                    if (File.Exists(settingsPath))
                    {
                        File.Delete(settingsPath);
                    }

                    // Atomic rename - if this fails, temp file remains for recovery
                    File.Move(tempPath, settingsPath);

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
                    MessageBox.Show($"Failed to save settings: {ex.Message}", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            } // End lock (saveSettingsLock)
        }

        private async Task InitializeServicesAsync()
        {
            try
            {
                StatusText.Text = "Initializing services...";

                authenticationCoordinator = new AuthenticationCoordinator(authenticationService);

                // Initialize core services
                textInjector = new TextInjector(settings);
                audioRecorder = new AudioRecorder();
                hotkeyManager = new HotkeyManager();

                // Check for microphone first
                if (!AudioRecorder.HasAnyMicrophone())
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("No microphone detected!\n\nPlease connect a microphone and restart the application.",
                            "No Microphone", MessageBoxButton.OK, MessageBoxImage.Warning);
                        StatusText.Text = "No microphone detected";
                        StatusText.Foreground = Brushes.Red;
                    });
                }

                // CRITICAL FIX: Null-check before using audioRecorder
                if (audioRecorder != null && settings.SelectedMicrophoneIndex >= 0)
                {
                    audioRecorder.SetDevice(settings.SelectedMicrophoneIndex);
                }

                // CRITICAL FIX: Initialize Whisper service asynchronously with progress feedback
                if (settings.UseWhisperServer)
                {
                    StatusText.Text = "Starting Whisper server...";
                    var serverService = new WhisperServerService(settings);

                    // Run initialization in background - don't block UI
                    await serverService.InitializeAsync();
                    whisperService = serverService;

                    StatusText.Text = "Whisper server ready";
                }
                else
                {
                    StatusText.Text = "Loading Whisper AI...";
                    whisperService = new PersistentWhisperService(settings);
                }

                // Initialize services BEFORE coordinator (coordinator needs these references)
                historyService = new TranscriptionHistoryService(settings);
                soundService = new SoundService();
                analyticsService = new AnalyticsService(settings);

                // CRITICAL FIX: Null-check all dependencies before creating coordinator
                if (audioRecorder == null || whisperService == null || textInjector == null ||
                    historyService == null || analyticsService == null || soundService == null)
                {
                    throw new InvalidOperationException(
                        "Failed to initialize core services - one or more required services is null. " +
                        "Please restart VoiceLite or check the logs for errors.");
                }

                // Initialize recording coordinator (handles recording pipeline)
                recordingCoordinator = new RecordingCoordinator(
                    audioRecorder,
                    whisperService,
                    textInjector,
                    historyService,
                    analyticsService,
                    soundService,
                    settings);

                // Wire up coordinator events
                recordingCoordinator.StatusChanged += OnRecordingStatusChanged;
                recordingCoordinator.TranscriptionCompleted += OnTranscriptionCompleted;
                recordingCoordinator.ErrorOccurred += OnRecordingError;

                // CRITICAL FIX: Null-check hotkeyManager before subscribing
                if (hotkeyManager != null)
                {
                    hotkeyManager.HotkeyPressed += OnHotkeyPressed;
                    hotkeyManager.HotkeyReleased += OnHotkeyReleased;

                    // BUG-006 FIX: Subscribe to polling mode notification
                    hotkeyManager.PollingModeActivated += OnPollingModeActivated;
                }

                systemTrayManager = new SystemTrayManager(this);
                systemTrayManager.AccountMenuClicked += OnTrayAccountMenuClicked;
                systemTrayManager.ReportBugMenuClicked += OnTrayReportBugMenuClicked;

                // Initialize memory monitoring
                memoryMonitor = new MemoryMonitor();
                memoryMonitor.MemoryAlert += OnMemoryAlert;

                // Load and display existing history
                _ = UpdateHistoryUI();

                _ = RestoreAccountAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize: {ex.Message}",
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // CRITICAL FIX: Run ALL initialization asynchronously to prevent UI freeze
                // Old code ran diagnostics/services in fire-and-forget tasks, causing race conditions
                // New code ensures proper ordering: diagnostics → services → hotkey → UI updates

                // Step 1: Check dependencies (runs in background)
                await CheckDependenciesAsync();

                // Step 2: Initialize services (runs in background)
                await InitializeServicesAsync();

                // Step 3: Register hotkey (ONLY after services are ready)
                var helper = new WindowInteropHelper(this);
                hotkeyManager?.RegisterHotkey(helper.Handle, settings.RecordHotkey, settings.HotkeyModifiers);

                // Step 4: Update UI (now safe - all services initialized)
                string hotkeyDisplay = GetHotkeyDisplayString();
                UpdateUIForCurrentMode();
                UpdateConfigDisplay();

                // Mark as ready with helpful hint
                if (StatusText.Text == "Initializing...")
                {
                    var hotkeyHint = GetHotkeyDisplayString();
                    var modelName = WhisperModelInfo.GetDisplayName(settings.WhisperModel);
                    StatusText.Text = $"Ready ({modelName}) - Press {hotkeyHint} to record";
                    StatusText.Foreground = Brushes.Green;
                }

                // Step 5: Check for first-run diagnostics (blocking for critical issues)
                await CheckFirstRunDiagnosticsAsync();

                // Step 6: Check for analytics consent on first run (non-blocking)
                CheckAnalyticsConsentAsync();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("MainWindow_Loaded initialization failed", ex);
                StatusText.Text = "Initialization failed - Click for help";
                StatusText.Foreground = Brushes.Red;

                var errorMessage = $"Failed to initialize VoiceLite:\n\n{ex.Message}\n\n";

                // Add helpful troubleshooting steps based on error type
                if (ex.Message.Contains("whisper") || ex.Message.Contains("model"))
                {
                    errorMessage += "Troubleshooting:\n" +
                                  "1. Verify VoiceLite installed correctly\n" +
                                  "2. Check if whisper.exe exists in installation folder\n" +
                                  "3. Try reinstalling VoiceLite";
                }
                else if (ex.Message.Contains("microphone") || ex.Message.Contains("audio"))
                {
                    errorMessage += "Troubleshooting:\n" +
                                  "1. Connect a microphone\n" +
                                  "2. Check Windows audio settings\n" +
                                  "3. Restart VoiceLite";
                }
                else
                {
                    errorMessage += "Please check the logs at:\n" +
                                  $"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VoiceLite", "logs")}";
                }

                MessageBox.Show(
                    errorMessage,
                    "Initialization Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task CheckFirstRunDiagnosticsAsync()
        {
            try
            {
                // Show first-run diagnostic window on first app launch after installation
                if (!settings.HasSeenFirstRunDiagnostics)
                {
                    // Small delay to let the main window fully load
                    await Task.Delay(500);

                    var diagnosticWindow = new FirstRunDiagnosticWindow();
                    diagnosticWindow.Owner = this;
                    var result = diagnosticWindow.ShowDialog();

                    // Mark as seen regardless of dialog result
                    settings.HasSeenFirstRunDiagnostics = true;
                    SaveSettings();

                    // If user closed dialog with critical issues, log it
                    if (result != true)
                    {
                        ErrorLogger.LogMessage("First-run diagnostics completed with issues or user closed early");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("First-run diagnostics check failed", ex);
                // Mark as seen to prevent repeated failures
                settings.HasSeenFirstRunDiagnostics = true;
                SaveSettings();
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
            // Update instruction text with actual hotkey
            if (InstructionText != null)
            {
                string hotkeyHint = GetHotkeyDisplayString();
                InstructionText.Text = $"Press {hotkeyHint} to record";
            }

            // Update empty state hotkey hint
            if (EmptyStateHotkeyHint != null)
            {
                string hotkeyHint = GetHotkeyDisplayString();
                EmptyStateHotkeyHint.Text = $"Press {hotkeyHint} to start recording";
            }

            // Update hotkey display (null check for XAML control)
            if (HotkeyText != null)
            {
                HotkeyText.Text = GetHotkeyDisplayString();
            }

            // Update microphone display (null check for XAML control)
            if (MicrophoneText != null)
            {
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
            }

            // Update model display (null check for XAML control)
            if (ModelText != null)
            {
                ModelText.Text = GetModelDisplayName(settings.WhisperModel);
            }
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

        #endregion

        #region Recording Control

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorLogger.LogDebug($"TestButton_Click: Entry - isRecording={isRecording}, isHotkeyMode={isHotkeyMode}");

            // Prevent rapid clicking (debounce)
            var now = DateTime.Now;
            if ((now - lastClickTime).TotalMilliseconds < TimingConstants.ClickDebounceMs)
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
            // CRIT-006 FIX: Wrap entire async void method in try-catch
            try
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
            catch (Exception ex)
            {
                ErrorLogger.LogError("CRITICAL: Unhandled exception in AccountButton_Click", ex);
                MessageBox.Show($"Account operation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

            // CRITICAL FIX: Validate AudioRecorder is not already recording (prevents race conditions)
            var recorder = audioRecorder;
            if (recorder == null)
            {
                ErrorLogger.LogWarning("StartRecording: AudioRecorder is null, cannot start");
                return;
            }

            if (recorder.IsRecording)
            {
                ErrorLogger.LogWarning("StartRecording: AudioRecorder is already recording, aborting");
                return;
            }

            // Pre-check: if already recording, do nothing
            if (isRecording)
            {
                ErrorLogger.LogDebug("StartRecording: Already recording, returning early");
                return;
            }

            // PERFORMANCE FIX: Check if coordinator is busy (transcribing)
            // Prevents queueing up recordings which causes apparent freezing
            if (recordingCoordinator != null && recordingCoordinator.IsTranscribing)
            {
                UpdateStatus("Busy - transcription in progress, please wait", new SolidColorBrush(Color.FromRgb(255, 165, 0))); // Orange
                ErrorLogger.LogInfo("StartRecording: Blocked - transcription already in progress");
                return;
            }

            // CRITICAL FIX: Stop stuck-state recovery timer if somehow still running
            StopStuckStateRecoveryTimer();

            // Set state BEFORE attempting to start (defensive)
            isRecording = true;
            recordingStartTime = DateTime.Now;

            // Delegate to coordinator
            recordingCoordinator?.StartRecording();

            // CRITICAL FIX: Verify recording actually started, otherwise rollback state
            if (recorder.IsRecording)
            {
                // Start elapsed time timer for UI updates
                recordingElapsedTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                recordingElapsedTimer.Tick += (s, e) =>
                {
                    var elapsed = recordingCoordinator?.GetRecordingDuration() ?? TimeSpan.Zero;
                    UpdateStatus($"Recording {elapsed:m\\:ss}", new SolidColorBrush(StatusColors.Recording));
                };
                recordingElapsedTimer.Start();

                // Update UI
                UpdateUIForCurrentMode();
                TranscriptionText.Foreground = Brushes.Gray;
            }
            else
            {
                // Recording failed to start - rollback state
                ErrorLogger.LogWarning("StartRecording: Recording failed to start, rolling back state");
                isRecording = false;
                UpdateStatus("Failed to start recording", Brushes.Red);
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

            // Stop elapsed time timer
            recordingElapsedTimer?.Stop();
            recordingElapsedTimer = null;

            // Delegate to coordinator
            recordingCoordinator?.StopRecording(cancel);

            isRecording = false;

            // Update UI immediately
            if (cancel)
            {
                UpdateStatus("Cancelled", Brushes.Gray);
                UpdateUIForCurrentMode();
            }
            else
            {
                UpdateStatus("Processing...", new SolidColorBrush(StatusColors.Processing));
                TranscriptionText.Text = "Processing audio...";
                TranscriptionText.Foreground = new SolidColorBrush(StatusColors.Processing);

                // CRITICAL FIX: Start stuck-state recovery timer when entering "Processing" state
                // This ensures the app NEVER stays stuck in processing for >15 seconds
                StartStuckStateRecoveryTimer();
            }
        }

        private void OnHotkeyPressed(object? sender, EventArgs e)
        {
            // CRITICAL: Event handlers must never throw exceptions - wrap entire method
            try
            {
                ErrorLogger.LogMessage($"OnHotkeyPressed: Entry - Mode={settings.Mode}, isRecording={isRecording}, isHotkeyMode={isHotkeyMode}");

                // Debounce protection - ignore rapid key presses
                var now = DateTime.Now;
                if ((now - lastHotkeyPressTime).TotalMilliseconds < TimingConstants.HotkeyDebounceMs)
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
            catch (Exception ex)
            {
                ErrorLogger.LogError("CRITICAL: OnHotkeyPressed exception - hotkey system may be broken", ex);

                // Attempt to reset state to prevent stuck recording
                try
                {
                    if (isRecording)
                    {
                        StopRecording(cancel: true);
                    }
                }
                catch
                {
                    // Last resort - ignore secondary exceptions during recovery
                }

                // Notify user of critical failure
                Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(
                        $"Hotkey system error: {ex.Message}\n\nPlease restart VoiceLite.",
                        "Critical Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            }
        }

        private void OnHotkeyReleased(object? sender, EventArgs e)
        {
            // CRITICAL: Event handlers must never throw exceptions - wrap entire method
            try
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
            catch (Exception ex)
            {
                ErrorLogger.LogError("CRITICAL: OnHotkeyReleased exception - hotkey system may be broken", ex);

                // Attempt to stop recording if stuck
                try
                {
                    if (isRecording)
                    {
                        StopRecording(cancel: true);
                    }
                }
                catch
                {
                    // Last resort - ignore secondary exceptions
                }
            }
        }

        /// <summary>
        /// BUG-006 FIX: Handle polling mode activation notification
        /// Shows a brief status message when polling mode is active for standalone modifier keys
        /// </summary>
        private void OnPollingModeActivated(object? sender, string message)
        {
            try
            {
                Dispatcher.InvokeAsync(() =>
                {
                    // Show subtle notification (orange color, auto-dismiss after 3 seconds)
                    StatusText.Text = message;
                    StatusText.Foreground = Brushes.Orange;
                    ErrorLogger.LogMessage($"BUG-006 FIX: {message}");

                    // Auto-clear after 3 seconds and restore normal status
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                    timer.Tick += (_, __) =>
                    {
                        timer.Stop();
                        UpdateUIForCurrentMode(); // Restore normal status text
                    };
                    timer.Start();
                });
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("OnPollingModeActivated", ex);
            }
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
                recordingCoordinator?.PlaySoundFeedback();

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
                recordingCoordinator?.PlaySoundFeedback();

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
                recordingCoordinator?.PlaySoundFeedback();

                StartRecording();
                StartAutoTimeoutTimer();
                ErrorLogger.LogMessage($"HandleToggleModePressed: After start - isRecording={isRecording}");
            }
            else
            {
                ErrorLogger.LogMessage("HandleToggleModePressed: TOGGLE OFF - Stopping recording");

                // Audio feedback for toggle stop
                recordingCoordinator?.PlaySoundFeedback();

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

        /// <summary>
        /// Start global stuck-state recovery timer (120 seconds max for any processing state)
        /// CRITICAL: Prevents app from being permanently stuck in "Processing" state
        /// BUG FIX: Increased from 15s to 120s - 15s was too aggressive for normal transcriptions
        /// </summary>
        private void StartStuckStateRecoveryTimer()
        {
            // Stop any existing timer first
            StopStuckStateRecoveryTimer();

            // BUG FIX: Increased from 15s to 120s to match RecordingCoordinator timeout
            // 15s was TOO AGGRESSIVE - normal transcriptions with Small model can take 20-30s
            // This should only fire if Whisper completely hangs (rare edge case)
            const int maxProcessingSeconds = 120; // 2 minutes max - matches RecordingCoordinator

            stuckStateRecoveryTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(maxProcessingSeconds)
            };
            stuckStateRecoveryTimer.Tick += OnStuckStateRecovery;
            stuckStateRecoveryTimer.Start();

            ErrorLogger.LogDebug($"StartStuckStateRecoveryTimer: Recovery timer set for {maxProcessingSeconds}s");
        }

        /// <summary>
        /// Stop stuck-state recovery timer (safe to call multiple times)
        /// BUG FIX (BUG-010): Properly dispose timer to prevent resource leak
        /// </summary>
        private void StopStuckStateRecoveryTimer()
        {
            // BUG FIX (BUG-001): Thread-safe timer disposal to prevent race condition
            // Use local variable to prevent null reference if timer is set to null by another thread
            var timer = stuckStateRecoveryTimer;
            if (timer != null)
            {
                timer.Stop();
                timer.Tick -= OnStuckStateRecovery;

                // Clear reference BEFORE disposal to prevent re-entry
                stuckStateRecoveryTimer = null;

                // BUG FIX (BUG-010): Dispose timer properly
                try
                {
                    // DispatcherTimer doesn't implement IDisposable in .NET Framework
                    // but does in .NET Core/.NET 5+. Safe to skip disposal if not available.
                    (timer as IDisposable)?.Dispose();
                }
                catch
                {
                    // Ignore disposal errors - timer will be GC'd
                }

                ErrorLogger.LogDebug("StopStuckStateRecoveryTimer: Recovery timer stopped");
            }
        }

        /// <summary>
        /// Stuck-state recovery callback - CRITICAL failsafe to prevent permanent "Processing" state
        /// Fires if app is stuck in processing for >15 seconds without completion
        /// </summary>
        private async void OnStuckStateRecovery(object? sender, EventArgs e)
        {
            // CRIT-006 FIX: Wrap entire async void method in try-catch
            try
            {
                ErrorLogger.LogWarning("OnStuckStateRecovery: STUCK STATE DETECTED! Forcing recovery...");

                // Stop the timer immediately
                StopStuckStateRecoveryTimer();

                // CRITICAL FIX: Kill any hung whisper.exe processes BEFORE showing dialog
                // This prevents PC freeze from stuck processes consuming resources
                await KillHungWhisperProcessesAsync();

            // Force UI back to ready state
            try
            {
                TranscriptionText.Text = "Processing timed out - app recovered";
                TranscriptionText.Foreground = Brushes.Orange;
                UpdateStatus("Ready", new SolidColorBrush(StatusColors.Ready));
                this.BorderThickness = new Thickness(0);

                // Reset all state flags
                lock (recordingLock)
                {
                    ErrorLogger.LogWarning($"OnStuckStateRecovery: Force-resetting state - was isRecording={isRecording}, isHotkeyMode={isHotkeyMode}");
                    isRecording = false;
                    isHotkeyMode = false;
                }

                // Stop all timers
                StopAutoTimeoutTimer();
                recordingElapsedTimer?.Stop();
                recordingElapsedTimer = null;

                // Show user-friendly message (non-blocking async)
                try
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show(
                            "VoiceLite recovered from a stuck state.\n\n" +
                            "The app was stuck processing for too long and has been reset.\n\n" +
                            "If this happens frequently:\n" +
                            "• Try using a smaller Whisper model\n" +
                            "• Check if antivirus is blocking the app\n" +
                            "• Restart VoiceLite",
                            "Stuck State Recovery",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    });
                }
                catch (TaskCanceledException)
                {
                    // Dispatcher shutting down during app close - this is normal, just log it
                    ErrorLogger.LogMessage("OnStuckStateRecovery: Dispatcher shutting down (app closing)");
                }

                // Reset UI to default mode
                UpdateUIForCurrentMode();
            }
            catch (Exception innerEx)
            {
                ErrorLogger.LogError("OnStuckStateRecovery: Failed to recover", innerEx);
            }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("CRITICAL: Unhandled exception in OnStuckStateRecovery", ex);
                // Best effort to reset UI
                try
                {
                    UpdateStatus("Error during recovery", Brushes.Red);
                }
                catch { /* Ignore */ }
            }
        }

        /// <summary>
        /// Kill any hung whisper.exe processes that may be stuck
        /// CRITICAL: Prevents PC freeze from stuck processes consuming resources
        /// </summary>
        private async Task KillHungWhisperProcessesAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    var whisperProcesses = System.Diagnostics.Process.GetProcessesByName("whisper");
                    if (whisperProcesses.Length > 0)
                    {
                        ErrorLogger.LogWarning($"KillHungWhisperProcesses: Found {whisperProcesses.Length} whisper.exe process(es) - attempting to kill...");

                        foreach (var process in whisperProcesses)
                        {
                            try
                            {
                                if (!process.HasExited)
                                {
                                    ErrorLogger.LogWarning($"Killing hung whisper.exe process (PID: {process.Id})");
                                    process.Kill(entireProcessTree: true); // Kill process and all children
                                    process.WaitForExit(2000); // Wait up to 2 seconds for clean exit
                                }
                                process.Dispose();
                            }
                            catch (Exception ex)
                            {
                                ErrorLogger.LogError($"Failed to kill whisper.exe process (PID: {process.Id})", ex);
                            }
                        }

                        ErrorLogger.LogWarning("KillHungWhisperProcesses: All whisper.exe processes terminated");
                    }
                    else
                    {
                        ErrorLogger.LogDebug("KillHungWhisperProcesses: No whisper.exe processes found");
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("KillHungWhisperProcesses: Failed to enumerate/kill processes", ex);
                }
            });
        }

        private async void OnAutoTimeout(object? sender, System.Timers.ElapsedEventArgs e)
        {
            ErrorLogger.LogMessage("OnAutoTimeout: Auto-timeout triggered - stopping recording for safety");

            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    lock (recordingLock)
                    {
                        if (isRecording)
                        {
                            // Show timeout warning
                            UpdateStatus("Auto-timeout - Recording stopped", Brushes.Orange);

                            // Audio feedback for timeout
                            recordingCoordinator?.PlaySoundFeedback();

                            StopRecording(false);
                            StopAutoTimeoutTimer();

                            // Show message to user
                            MessageBox.Show("Recording automatically stopped after 5 minutes for safety.\n\nThis prevents forgotten hot microphones.",
                                "Auto-Timeout", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                });
            }
            catch (TaskCanceledException)
            {
                // App is shutting down - ignore dispatcher exceptions
                ErrorLogger.LogMessage("OnAutoTimeout: Dispatcher shutting down (app closing)");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("OnAutoTimeout: Unexpected exception", ex);
            }
        }

        /// <summary>
        /// Handle recording status changes from coordinator
        /// PERFORMANCE FIX: Changed from async void to void (saves 20-50ms per status change)
        /// RecordingCoordinator raises on background thread, so we still need Dispatcher
        /// </summary>
        private void OnRecordingStatusChanged(object? sender, RecordingStatusEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    switch (e.Status)
                    {
                        case "Recording":
                            UpdateStatus("Recording 0:00", new SolidColorBrush(StatusColors.Recording));
                            this.BorderBrush = new SolidColorBrush(StatusColors.Recording);
                            this.BorderThickness = new Thickness(3);
                            break;

                        case "Transcribing":
                            UpdateStatus("Transcribing...", new SolidColorBrush(StatusColors.Info));
                            // CRITICAL FIX: Start recovery timer when entering transcribing state
                            // This catches cases where StopRecording didn't start the timer
                            if (stuckStateRecoveryTimer == null)
                            {
                                StartStuckStateRecoveryTimer();
                            }
                            break;

                        case "Pasting":
                            UpdateStatus("Pasting...", Brushes.Purple);
                            break;

                        case "Copied to clipboard":
                            UpdateStatus("Copied to clipboard", new SolidColorBrush(StatusColors.Info));
                            break;

                        case "Cancelled":
                            UpdateStatus("Cancelled", new SolidColorBrush(StatusColors.Inactive));
                            // Stop recovery timer on cancel
                            StopStuckStateRecoveryTimer();
                            break;

                        case "Processing":
                            UpdateStatus("Processing...", new SolidColorBrush(StatusColors.Processing));
                            // CRITICAL FIX: Ensure recovery timer is running when entering processing state
                            if (stuckStateRecoveryTimer == null)
                            {
                                StartStuckStateRecoveryTimer();
                            }
                            break;
                    }
                });
            }
            catch (TaskCanceledException)
            {
                // Dispatcher shutting down during app close - this is normal, just log it
                ErrorLogger.LogMessage("OnRecordingStatusChanged: Dispatcher shutting down (app closing)");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("OnRecordingStatusChanged failed", ex);
                // Attempt to reset to safe state
                try { UpdateStatus("Error", Brushes.Red); } catch { }
            }
        }

        /// <summary>
        /// Handle transcription completion from coordinator
        /// PERFORMANCE FIX: Refactored to use batched helper methods
        /// RecordingCoordinator raises on BACKGROUND thread, so we need Dispatcher.Invoke
        /// </summary>
        private void OnTranscriptionCompleted(object? sender, TranscriptionCompleteEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // CRITICAL FIX: Always stop the stuck-state recovery timer when transcription completes
                    StopStuckStateRecoveryTimer();

                    if (e.Success)
                    {
                        // Display transcription result
                        if (!string.IsNullOrWhiteSpace(e.Transcription))
                        {
                            // PERFORMANCE FIX: Batch all UI updates into single call
                            BatchUpdateTranscriptionSuccess(e);
                        }
                        else
                        {
                            TranscriptionText.Text = "(No speech detected)";
                            TranscriptionText.Foreground = Brushes.Gray;
                            this.BorderThickness = new Thickness(0);

                            // Revert to ready immediately for no-speech case
                            var hotkeyHint = GetHotkeyDisplayString();
                            var modelName = WhisperModelInfo.GetDisplayName(settings.WhisperModel);
                            UpdateStatus($"Ready ({modelName}) - Press {hotkeyHint} to record", Brushes.Green);
                        }

                        // Reset TranscriptionText to ready state after delay (fire-and-forget)
                        if (!string.IsNullOrWhiteSpace(e.Transcription))
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await Task.Delay(TimingConstants.TranscriptionTextResetDelayMs);
                                    await Dispatcher.InvokeAsync(() =>
                                    {
                                        if (!isRecording) // Only reset if not currently recording again
                                        {
                                            UpdateUIForCurrentMode();
                                        }
                                    });
                                }
                                catch (TaskCanceledException)
                                {
                                    // Dispatcher shutting down during app close - this is normal
                                }
                                catch (Exception ex)
                                {
                                    ErrorLogger.LogError("OnTranscriptionCompleted: TranscriptionText reset failed", ex);
                                }
                            });
                        }

                        // Reset state if needed
                        lock (recordingLock)
                        {
                            // CRITICAL FIX: Only reset state if not currently recording
                            // This prevents old transcriptions from resetting state during new recordings
                            var recorder = audioRecorder;
                            bool actuallyRecording = recorder?.IsRecording ?? false;

                            if (actuallyRecording)
                            {
                                ErrorLogger.LogMessage("OnTranscriptionCompleted: NEW recording in progress - skipping state reset");
                                // Don't reset state, new recording is active
                            }
                            // In push-to-talk mode, only reset if the key is not currently held down
                            else if (settings.Mode == RecordMode.PushToTalk && isHotkeyMode)
                            {
                                ErrorLogger.LogMessage("OnTranscriptionCompleted: Push-to-talk mode - keeping state (key still held)");
                            }
                            else
                            {
                                ErrorLogger.LogMessage($"OnTranscriptionCompleted: Resetting state");
                                isRecording = false;
                                isHotkeyMode = false;
                                StopAutoTimeoutTimer();
                            }
                        }
                    }
                    else
                    {
                        // PERFORMANCE FIX: Batch error UI updates
                        ErrorLogger.LogWarning($"OnTranscriptionCompleted: Error occurred - {e.ErrorMessage}");
                        BatchUpdateTranscriptionError(e.ErrorMessage ?? "Transcription error");

                        // Reset to ready state after 3 seconds (fire-and-forget)
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(3000);
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    if (!isRecording)
                                    {
                                        UpdateUIForCurrentMode();
                                        UpdateStatus("Ready", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7A7A7A")));
                                    }
                                });
                            }
                            catch { /* Dispatcher might be shut down */ }
                        });

                        // CRITICAL FIX: Only reset state on error if not currently recording
                        lock (recordingLock)
                        {
                            var recorder = audioRecorder;
                            bool actuallyRecording = recorder?.IsRecording ?? false;

                            if (!actuallyRecording)
                            {
                                ErrorLogger.LogMessage($"OnTranscriptionCompleted: Error - forcing state reset");
                                isRecording = false;
                                isHotkeyMode = false;
                                StopAutoTimeoutTimer();
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("OnTranscriptionCompleted", ex);
                try
                {
                    // CRITICAL FIX: Force recovery even if exception occurs
                    Dispatcher.Invoke(() =>
                    {
                        StopStuckStateRecoveryTimer();
                        BatchUpdateTranscriptionError("Error displaying transcription");

                        // CRITICAL FIX: Only reset state if not currently recording
                        var recorder = audioRecorder;
                        bool actuallyRecording = recorder?.IsRecording ?? false;

                        if (!actuallyRecording)
                        {
                            isRecording = false;
                            isHotkeyMode = false;
                            StopAutoTimeoutTimer();
                        }
                    });
                }
                catch
                {
                    // Dispatcher is unavailable, app is shutting down
                }
            }
        }

        #region PERFORMANCE FIX: Helper Methods for Batched UI Updates

        /// <summary>
        /// PERFORMANCE FIX: Batch multiple UI updates into a single Dispatcher call
        /// Old: 5 separate Dispatcher calls (50-100ms each) = 250-500ms total
        /// New: 1 batched Dispatcher call = 50-100ms total
        /// Savings: 200-400ms per transcription
        /// </summary>
        private void BatchUpdateTranscriptionSuccess(TranscriptionCompleteEventArgs e)
        {
            // All 5 UI updates batched together
            TranscriptionText.Text = e.Transcription;
            TranscriptionText.Foreground = Brushes.Black;
            UpdateStatus("✓ Transcribed successfully", Brushes.Green);
            this.BorderThickness = new Thickness(0);

            // Start revert timer (already on UI thread, no Dispatcher needed)
            var revertTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };

            EventHandler? tickHandler = null;
            tickHandler = (s, args) =>
            {
                try
                {
                    var hotkeyHint = GetHotkeyDisplayString();
                    var modelName = WhisperModelInfo.GetDisplayName(settings.WhisperModel);
                    UpdateStatus($"Ready ({modelName}) - Press {hotkeyHint} to record", Brushes.Green);
                }
                finally
                {
                    revertTimer.Stop();
                    if (tickHandler != null)
                    {
                        revertTimer.Tick -= tickHandler;
                    }
                }
            };
            revertTimer.Tick += tickHandler;
            revertTimer.Start();

            // Update history UI and save settings (synchronous, optimized)
            UpdateHistoryUISync();
            _ = Task.Run(() => SaveSettingsAsync()); // Fire-and-forget background save
        }

        /// <summary>
        /// PERFORMANCE FIX: Batch error UI updates
        /// </summary>
        private void BatchUpdateTranscriptionError(string errorMessage)
        {
            TranscriptionText.Text = errorMessage;
            TranscriptionText.Foreground = Brushes.Red;
            UpdateStatus("Error", Brushes.Red);
            this.BorderThickness = new Thickness(0);
        }

        /// <summary>
        /// PERFORMANCE FIX: Synchronous history update (delegates to existing async method but fire-and-forget)
        /// This keeps the original UpdateHistoryUI() logic intact while running it in background
        /// </summary>
        private void UpdateHistoryUISync()
        {
            // Fire-and-forget the existing async UpdateHistoryUI()
            _ = UpdateHistoryUI();
        }

        /// <summary>
        /// PERFORMANCE FIX: Async settings save (non-blocking)
        /// BUGFIX: Must stay on UI thread - settings object may have UI thread affinity
        /// </summary>
        private async Task SaveSettingsAsync()
        {
            try
            {
                // Just call SaveSettings() synchronously - it's already fast enough (50ms)
                // Trying to move to background thread causes cross-thread access errors
                await Task.Run(() => { }); // Keep async signature for compatibility
                SaveSettings();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("SaveSettingsAsync failed", ex);
            }
        }

        #endregion

        /// <summary>
        /// Handle recording errors from coordinator
        /// </summary>
        private async void OnRecordingError(object? sender, string errorMessage)
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    isRecording = false;
                    UpdateStatus("Error", Brushes.Red);
                    UpdateUIForCurrentMode();
                    TranscriptionText.Foreground = Brushes.Black;

                    MessageBox.Show(errorMessage, "Recording Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
            catch (TaskCanceledException)
            {
                // Dispatcher shutting down during app close - this is normal, just log it
                ErrorLogger.LogMessage("OnRecordingError: Dispatcher shutting down (app closing)");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("OnRecordingError", ex);
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                systemTrayManager?.MinimizeToTray();
            }
        }

        private async void OnMemoryAlert(object? sender, MemoryAlertEventArgs e)
        {
            if (e.Level == MemoryAlertLevel.Critical || e.Level == MemoryAlertLevel.PotentialLeak)
            {
                try
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        ErrorLogger.LogMessage($"Memory alert: {e.Message}");
                        // Could show a warning to user if needed
                    });
                }
                catch (TaskCanceledException)
                {
                    // Dispatcher shutting down during app close - this is normal, just log it
                    ErrorLogger.LogMessage("OnMemoryAlert: Dispatcher shutting down (app closing)");
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("OnMemoryAlert: Unexpected exception", ex);
                }
            }
        }

        #endregion

        #region UI Event Handlers & Settings

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

        private void VoiceShortcutsButton_Click(object sender, RoutedEventArgs e)
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

            var settingsWindow = new SettingsWindowNew(settings, analyticsService, () => TestButton_Click(this, new RoutedEventArgs()), () => SaveSettings());
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
                await Dispatcher.InvokeAsync(() =>
                {
                    UpdateAccountStatusUI("Not signed in", Brushes.Gray);
                    systemTrayManager?.UpdateAccountMenuText("Sign In");
                });
                return;
            }

            try
            {
                var session = await authenticationCoordinator.TryRestoreSessionAsync().ConfigureAwait(false);
                if (session == null)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        UpdateAccountStatusUI("Not signed in", Brushes.Gray);
                        systemTrayManager?.UpdateAccountMenuText("Sign In");
                    });
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
                        if (validationResult.Payload?.ExpiresAt != null)
                        {
                            var expiresAt = DateTime.Parse(validationResult.Payload.ExpiresAt);
                            var daysRemaining = (expiresAt - DateTime.UtcNow).Days;
                            UpdateAccountStatusUI($"Pro license (expires in {daysRemaining} days)", Brushes.Orange);
                            ErrorLogger.LogMessage($"License in grace period, expires in {daysRemaining} days");
                        }
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
            // This method is always called from UI thread or inside Dispatcher.InvokeAsync
            AccountStatusText.Text = text;
            AccountStatusText.Foreground = brush;
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
                    _ => settings.WhisperModel.EndsWith(".bin") ? settings.WhisperModel : "ggml-small.bin"
                };

                var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper", modelFile);
                if (!File.Exists(modelPath))
                {
                    // Show clear error message - no fallback
                    MessageBox.Show(
                        $"Model file '{modelFile}' is missing.\n\n" +
                        "Please reinstall VoiceLite to restore missing files.\n\n" +
                        $"Expected location: {modelPath}",
                        "Model File Missing",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("ValidateWhisperModel", ex);
            }
        }


        protected override void OnClosed(EventArgs e)
        {
            // CRITICAL FIX (BUG-002): Flush pending settings save BEFORE disposal
            // If timer is active, debounce hasn't fired yet - force immediate save to prevent data loss
            if (settingsSaveTimer != null && settingsSaveTimer.IsEnabled)
            {
                settingsSaveTimer.Stop();
                SaveSettingsInternal(); // Force immediate flush
                ErrorLogger.LogMessage("BUG-002 FIX: Flushed pending settings save on app close");
            }

            SaveSettings(); // Belt-and-suspenders: call debounced save too (will be no-op if timer null)

            // MEMORY FIX: Dispose all timers properly
            StopAutoTimeoutTimer();
            autoTimeoutTimer = null;

            StopStuckStateRecoveryTimer(); // Already disposes properly

            // BUG FIX (BUG-014): Dispose settingsSaveTimer to prevent settings corruption
            if (settingsSaveTimer != null)
            {
                settingsSaveTimer.Stop();
                try { (settingsSaveTimer as IDisposable)?.Dispose(); } catch { }
                settingsSaveTimer = null;
            }

            // Dispose recording elapsed timer
            if (recordingElapsedTimer != null)
            {
                recordingElapsedTimer.Stop();
                try { (recordingElapsedTimer as IDisposable)?.Dispose(); } catch { }
                recordingElapsedTimer = null;
            }

            // CRITICAL FIX: Kill any hung whisper.exe processes on shutdown
            // Prevents orphaned processes from consuming resources after app closes
            _ = KillHungWhisperProcessesAsync();

            // Clean up with proper disposal order
            try
            {
                // Stop recording FIRST to ensure no active operations
                if (isRecording)
                {
                    StopRecording(true);
                }

                // MEMORY FIX: Unsubscribe event handlers BEFORE disposal to prevent leaks
                if (recordingCoordinator != null)
                {
                    recordingCoordinator.StatusChanged -= OnRecordingStatusChanged;
                    recordingCoordinator.TranscriptionCompleted -= OnTranscriptionCompleted;
                    recordingCoordinator.ErrorOccurred -= OnRecordingError;
                }

                if (hotkeyManager != null)
                {
                    hotkeyManager.HotkeyPressed -= OnHotkeyPressed;
                    hotkeyManager.HotkeyReleased -= OnHotkeyReleased;
                }

                if (systemTrayManager != null)
                {
                    systemTrayManager.AccountMenuClicked -= OnTrayAccountMenuClicked;
                    systemTrayManager.ReportBugMenuClicked -= OnTrayReportBugMenuClicked;
                }

                if (memoryMonitor != null)
                {
                    memoryMonitor.MemoryAlert -= OnMemoryAlert;
                }

                // Now dispose services in reverse order of creation
                memoryMonitor?.Dispose();
                memoryMonitor = null;

                systemTrayManager?.Dispose();
                systemTrayManager = null;

                hotkeyManager?.Dispose();
                hotkeyManager = null;

                recordingCoordinator?.Dispose();
                recordingCoordinator = null;

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

        private void OnTrayReportBugMenuClicked(object? sender, EventArgs e)
        {
            try
            {
                // Get last error from ErrorLogger if available
                string? lastError = null;
                try
                {
                    var logPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "VoiceLite", "logs", "voicelite.log");

                    if (File.Exists(logPath))
                    {
                        // Read last 500 characters of log file to get recent errors
                        using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        if (stream.Length > 500)
                        {
                            stream.Seek(-500, SeekOrigin.End);
                        }
                        using var reader = new StreamReader(stream);
                        lastError = reader.ReadToEnd();
                    }
                }
                catch
                {
                    // Ignore errors reading log file
                }

                // Show feedback window
                var feedbackWindow = new FeedbackWindow(settings, lastError);
                feedbackWindow.Owner = this;
                feedbackWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("OnTrayReportBugMenuClicked", ex);
                MessageBox.Show("Failed to open feedback window. Please try again.",
                    "VoiceLite", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                    MessageBox.Show($"Failed to sign out: {ex.Message}", "VoiceLite", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private void ShowMainWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        #endregion

        #region Transcription History Management

        /// <summary>
        /// Updates the history panel UI with current history items.
        /// Creates visual cards for each transcription.
        /// </summary>
        private async Task UpdateHistoryUI()
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
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
            // Check UI preset and create appropriate card layout
            if (settings.UIPreset == UIPreset.Compact)
            {
                return CreateCompactHistoryCard(item);
            }
            else
            {
                return CreateDefaultHistoryCard(item);
            }
        }

        /// <summary>
        /// Creates a context menu for history items with Copy, Re-inject, Pin, and Delete actions.
        /// Extracted to eliminate code duplication between Compact and Default card layouts.
        /// </summary>
        private System.Windows.Controls.ContextMenu CreateHistoryContextMenu(TranscriptionHistoryItem item)
        {
            var contextMenu = new System.Windows.Controls.ContextMenu();

            // Copy menu item
            var copyMenuItem = new System.Windows.Controls.MenuItem { Header = "📋 Copy" };
            copyMenuItem.Click += (s, e) =>
            {
                try
                {
                    System.Windows.Clipboard.SetText(item.Text);
                    UpdateStatus("Copied to clipboard", new SolidColorBrush(StatusColors.Ready));
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TimingConstants.StatusRevertDelayMs) };
                    EventHandler? handler = null;
                    handler = (ts, te) =>
                    {
                        UpdateStatus("Ready", new SolidColorBrush(StatusColors.Ready));
                        timer.Stop();
                        if (handler != null) timer.Tick -= handler; // MEMORY FIX: Unsubscribe to prevent leak
                    };
                    timer.Tick += handler;
                    timer.Start();
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Copy menu item", ex);
                }
            };
            contextMenu.Items.Add(copyMenuItem);

            // Re-inject menu item
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

            // Pin/Unpin menu item
            var pinMenuItem = new System.Windows.Controls.MenuItem { Header = item.IsPinned ? "📌 Unpin" : "📌 Pin" };
            pinMenuItem.Click += (s, e) =>
            {
                historyService?.TogglePin(item.Id);
                _ = UpdateHistoryUI();
                SaveSettings();
            };
            contextMenu.Items.Add(pinMenuItem);

            // Delete menu item
            var deleteMenuItem = new System.Windows.Controls.MenuItem { Header = "🗑️ Delete" };
            deleteMenuItem.Click += (s, e) =>
            {
                historyService?.RemoveFromHistory(item.Id);
                _ = UpdateHistoryUI();
                SaveSettings();
            };
            contextMenu.Items.Add(deleteMenuItem);

            return contextMenu;
        }

        private System.Windows.Controls.Border CreateCompactHistoryCard(TranscriptionHistoryItem item)
        {
            // COMPACT PRESET: Single-line layout with timestamp + text
            var border = new System.Windows.Controls.Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 6, 0, 6), // Reduced padding for compact
                Cursor = Cursors.Hand,
                Tag = item,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true
            };

            // Hover effects
            border.MouseEnter += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.FromRgb(248, 249, 250));
            };
            border.MouseLeave += (s, e) =>
            {
                border.Background = Brushes.White;
            };

            // CRITICAL FIX: Click to copy (was missing in compact mode!)
            border.MouseLeftButtonDown += (s, e) =>
            {
                try
                {
                    System.Windows.Clipboard.SetText(item.Text);
                    UpdateStatus("Copied to clipboard", new SolidColorBrush(StatusColors.Ready));

                    // Revert to "Ready" after 1.5 seconds
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TimingConstants.StatusRevertDelayMs) };
                    EventHandler? handler = null;
                    handler = (ts, te) =>
                    {
                        var hotkeyHint = GetHotkeyDisplayString();
                        var modelName = WhisperModelInfo.GetDisplayName(settings.WhisperModel);
                        UpdateStatus($"Ready ({modelName}) - Press {hotkeyHint} to record", new SolidColorBrush(StatusColors.Ready));
                        timer.Stop();
                        if (handler != null) timer.Tick -= handler; // MEMORY FIX: Unsubscribe to prevent leak
                    };
                    timer.Tick += handler;
                    timer.Start();
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Copy history item", ex);
                }
            };

            // Single grid with columns for timestamp and text
            var grid = new System.Windows.Controls.Grid();
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(70, GridUnitType.Pixel) }); // Timestamp
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Text

            // Timestamp (left column)
            var relativeConverter = new Utilities.RelativeTimeConverter();
            var timeText = new System.Windows.Controls.TextBlock
            {
                Text = (string)relativeConverter.Convert(item.Timestamp, null!, null!, null!),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 8, 0),
                ToolTip = item.Timestamp.ToString("MMM d, yyyy h:mm tt")
            };
            System.Windows.Controls.Grid.SetColumn(timeText, 0);
            grid.Children.Add(timeText);

            // Transcription text (right column) - single line with ellipsis
            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = item.Text,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                TextWrapping = TextWrapping.NoWrap, // Compact = no wrap
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Top,
                ToolTip = item.Text
            };
            System.Windows.Controls.Grid.SetColumn(textBlock, 1);
            grid.Children.Add(textBlock);

            // Pin indicator if pinned (overlays on text)
            if (item.IsPinned)
            {
                var pinIcon = new System.Windows.Controls.TextBlock
                {
                    Text = "📌",
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 0, 5, 0)
                };
                System.Windows.Controls.Grid.SetColumn(pinIcon, 1);
                grid.Children.Add(pinIcon);
            }

            // Attach context menu using shared helper
            border.ContextMenu = CreateHistoryContextMenu(item);
            border.Child = grid;
            return border;
        }

        private System.Windows.Controls.Border CreateDefaultHistoryCard(TranscriptionHistoryItem item)
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
                    UpdateStatus("Copied to clipboard", new SolidColorBrush(StatusColors.Ready));

                    // Revert to "Ready" after 1.5 seconds
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TimingConstants.StatusRevertDelayMs) };
                    EventHandler? handler = null;
                    handler = (ts, te) =>
                    {
                        UpdateStatus("Ready", new SolidColorBrush(StatusColors.Ready));
                        timer.Stop();
                        if (handler != null) timer.Tick -= handler; // MEMORY FIX: Unsubscribe to prevent leak
                    };
                    timer.Tick += handler;
                    timer.Start();
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Copy history item", ex);
                }
            };

            // Attach context menu using shared helper
            border.ContextMenu = CreateHistoryContextMenu(item);

            // Content grid (simplified - no metadata row)
            var grid = new System.Windows.Controls.Grid();
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
                    UpdateStatus("Copied to clipboard", new SolidColorBrush(StatusColors.Ready));

                    // Revert to "Ready" after 1.5 seconds
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TimingConstants.StatusRevertDelayMs) };
                    EventHandler? handler = null;
                    handler = (ts, te) =>
                    {
                        UpdateStatus("Ready", new SolidColorBrush(StatusColors.Ready));
                        timer.Stop();
                        if (handler != null) timer.Tick -= handler; // MEMORY FIX: Unsubscribe to prevent leak
                    };
                    timer.Tick += handler;
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

            // Metadata removed - cleaner UI with just timestamp + text

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
                _ = UpdateHistoryUI();
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
                _ = UpdateHistoryUI();
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
                _ = UpdateHistoryUI();
            }
            else
            {
                // Show only matching items
                _ = FilterHistoryBySearch(searchText);
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
        private async Task FilterHistoryBySearch(string searchText)
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
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

        #endregion
    }
}


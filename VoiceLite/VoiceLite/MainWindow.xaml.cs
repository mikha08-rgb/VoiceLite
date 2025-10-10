using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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
        private ZombieProcessCleanupService? zombieCleanupService; // MEMORY_FIX 2025-10-08: Periodic zombie process cleanup
        private TranscriptionHistoryService? historyService;
        // SoundService removed per user request - no audio feedback

        // Recording state
        private DateTime recordingStartTime;
        private bool isRecording = false;
        private bool isTranscribing = false;
        private CancellationTokenSource? recordingCancellation;
        private bool isHotkeyMode = false;
        private readonly object recordingLock = new object();

        // UI BUG FIX: Initialization flag to suppress polling mode warnings during startup
        // More reliable than time-based check (works on slow PCs with 10+ second startup)
        private bool isInitializing = true;
        // TIER 1.4: Replaced lock with SemaphoreSlim for async compatibility
        private readonly SemaphoreSlim saveSettingsSemaphore = new SemaphoreSlim(1, 1);
        private DateTime lastClickTime = DateTime.MinValue;
        private DateTime lastHotkeyPressTime = DateTime.MinValue;

        // P1 OPTIMIZATION: Pre-configured JSON serializer options (5-15ms savings per save)
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultBufferSize = 131072 // 128KB buffer (matches typical settings size with large history)
        };

        private Settings settings = new();

        // Timers
        private System.Timers.Timer? autoTimeoutTimer;
        private System.Windows.Threading.DispatcherTimer? recordingElapsedTimer;
        private System.Windows.Threading.DispatcherTimer? settingsSaveTimer;
        private System.Windows.Threading.DispatcherTimer? stuckStateRecoveryTimer;

        // Child windows (for proper disposal)
        private SettingsWindowNew? currentSettingsWindow;

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
                    var validatedSettings = SettingsValidator.ValidateAndRepair(loadedSettings);

                    // BUG-011 FIX: Only use validated settings if repair succeeded
                    // If validation fails, use defaults WITHOUT running cleanup (prevents data loss)
                    if (validatedSettings != null)
                    {
                        settings = validatedSettings;
                        ErrorLogger.LogMessage($"Settings loaded from: {settingsPath}");

                        // MIGRATION 3: Upgrade Default UI preset to Compact (v1.0.38+) - ONE-TIME ONLY
                        // UI Preset is now hardcoded to Compact - migration code removed

                        // Verify whisper model exists
                        ValidateWhisperModel();

                        // BUG-011 FIX: Only cleanup history if we successfully loaded existing settings
                        // DO NOT cleanup on default settings (prevents accidental data loss)
                        CleanupOldHistoryItems();
                    }
                    else
                    {
                        ErrorLogger.LogMessage("Settings validation failed - using defaults WITHOUT cleanup");
                        settings = new Settings();
                    }
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
                    _ = SaveSettingsInternalAsync(); // TIER 1.4: Fire-and-forget async save
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
                settingsSaveTimer.Tick += async (s, e) =>
                {
                    settingsSaveTimer.Stop();
                    await SaveSettingsInternalAsync(); // TIER 1.4: Await async save
                };
            }

            // Restart timer (debounce)
            settingsSaveTimer.Stop();
            settingsSaveTimer.Start();
        }

        /// <summary>
        /// TIER 1.4: TRUE ASYNC - Internal method that saves settings to disk
        /// Uses atomic write pattern to prevent file corruption
        /// Now uses async I/O to prevent UI thread blocking
        /// </summary>
        private async Task SaveSettingsInternalAsync()
        {
            // v1.0.55 BUG FIX: Capture UI state on UI thread BEFORE going async
            // Prevents "The calling thread cannot access this object" errors
            bool minimizeToTray = false;
            await Dispatcher.InvokeAsync(() =>
            {
                minimizeToTray = MinimizeCheckBox.IsChecked == true;
            });

            // TIER 1.4: Use SemaphoreSlim instead of lock for async compatibility
            await saveSettingsSemaphore.WaitAsync();
            try
            {
                try
                {
                    // Ensure AppData directory exists
                    EnsureAppDataDirectoryExists();

                    string settingsPath = GetSettingsPath();

                    // BUG-002 FIX (CORRECTED): Update settings inside lock, serialize on background thread
                    // Prevents race condition while maintaining async performance
                    lock (settings.SyncRoot)
                    {
                        // Update settings inside lock to prevent concurrent modifications
                        settings.MinimizeToTray = minimizeToTray;
                    }

                    // TIER 1.4: Serialize on background thread to avoid UI blocking
                    // P1 OPTIMIZATION: Use pre-configured JsonSerializerOptions with optimized buffer size
                    // Lock is held only during serialization to prevent concurrent modifications
                    string json = await Task.Run(() =>
                    {
                        lock (settings.SyncRoot)
                        {
                            return JsonSerializer.Serialize(settings, _jsonSerializerOptions);
                        }
                    });

                    // TIER 1.4: Use async file I/O to prevent UI thread blocking (was 50ms)
                    // Write to temp file first, then rename (rename is atomic on Windows)
                    string tempPath = settingsPath + ".tmp";
                    await File.WriteAllTextAsync(tempPath, json);

                    // BUG-010 FIX: Verify temp file is valid JSON before replacing original
                    // Prevents data loss if app crashes during serialization
                    try
                    {
                        var testLoad = JsonSerializer.Deserialize<Settings>(await File.ReadAllTextAsync(tempPath));
                        if (testLoad == null)
                        {
                            throw new InvalidDataException("Settings deserialized to null");
                        }
                    }
                    catch (Exception validationEx)
                    {
                        // Delete corrupt temp file
                        try { File.Delete(tempPath); } catch { }
                        throw new InvalidOperationException("Settings save failed - temp file validation failed", validationEx);
                    }

                    // BUG-008 FIX: Use File.Move with overwrite to handle race conditions
                    // Prevents "file already exists" error if another thread recreated the file
                    File.Move(tempPath, settingsPath, overwrite: true);

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
            }
            finally
            {
                // TIER 1.4: Always release semaphore, even on exception
                saveSettingsSemaphore.Release();
            }
        }

        private async Task InitializeServicesAsync()
        {
            try
            {
                StatusText.Text = "Initializing services...";

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

                // Initialize Whisper service using process mode
                whisperService = new PersistentWhisperService(settings);

                // Initialize services
                historyService = new TranscriptionHistoryService(settings);
                // SoundService removed per user request - no audio feedback

                // CRITICAL FIX: Null-check all dependencies before creating coordinator
                if (audioRecorder == null || whisperService == null || textInjector == null ||
                    historyService == null)
                {
                    throw new InvalidOperationException(
                        "Failed to initialize core services - one or more required services is null. " +
                        "Please restart VoiceLite or check the logs for errors.");
                }

                // Wire up audio recorder events
                audioRecorder.AudioFileReady += OnAudioFileReady;

                // CRITICAL FIX: Null-check hotkeyManager before subscribing
                if (hotkeyManager != null)
                {
                    hotkeyManager.HotkeyPressed += OnHotkeyPressed;
                    hotkeyManager.HotkeyReleased += OnHotkeyReleased;

                    // BUG-006 FIX: Subscribe to polling mode notification
                    hotkeyManager.PollingModeActivated += OnPollingModeActivated;
                }

                systemTrayManager = new SystemTrayManager(this);

                // Initialize memory monitoring
                memoryMonitor = new MemoryMonitor();
                memoryMonitor.MemoryAlert += OnMemoryAlert;

                // MEMORY_FIX 2025-10-08: Start periodic zombie process cleanup service
                zombieCleanupService = new ZombieProcessCleanupService();
                zombieCleanupService.ZombieDetected += OnZombieProcessDetected;

                // Load and display existing history
                _ = UpdateHistoryUI();
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
                // BUG-005 FIX: Wrap hotkey registration in try-catch to show user-friendly error
                var helper = new WindowInteropHelper(this);
                try
                {
                    hotkeyManager?.RegisterHotkey(helper.Handle, settings.RecordHotkey, settings.HotkeyModifiers);
                }
                catch (InvalidOperationException ex)
                {
                    ErrorLogger.LogError("Initial hotkey registration failed", ex);
                    MessageBox.Show(
                        $"Failed to register hotkey: {ex.Message}\n\n" +
                        $"The hotkey may be in use by another application.\n\n" +
                        $"You can change the hotkey in Settings, or the app will use manual buttons.",
                        "Hotkey Registration Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    // App continues - user can still use manual buttons
                }

                // Step 4: Update UI (now safe - all services initialized)
                string hotkeyDisplay = GetHotkeyDisplayString();
                UpdateUIForCurrentMode();
                UpdateConfigDisplay();

                // Mark as ready
                if (StatusText.Text == "Initializing...")
                {
                    StatusText.Text = "Ready";
                    StatusText.Foreground = Brushes.Green;
                }

                // UI BUG FIX: Initialization complete - allow polling mode warnings to show now
                isInitializing = false;

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
            // Analytics removed - no action needed
            await Task.CompletedTask;
        }

        private string GetHotkeyDisplayString()
        {
            return HotkeyDisplayHelper.Format(settings.RecordHotkey, settings.HotkeyModifiers);
        }

        private void UpdateConfigDisplay()
        {
            // Update hotkey display in top bar
            if (HotkeyDisplay != null)
            {
                HotkeyDisplay.Text = GetHotkeyDisplayString();
            }

            // Update hidden hotkey text (for legacy compatibility)
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
            ErrorLogger.LogDebug($"TestButton_Click: Entry - // WEEK1-DAY3: State managed by coordinator - isRecording ={isRecording}, isHotkeyMode={isHotkeyMode}");

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

            lock (recordingLock)
            {
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

                if (isRecording)
                {
                    ErrorLogger.LogDebug("StartRecording: Already recording, returning early");
                    return;
                }

                if (isTranscribing)
                {
                    UpdateStatus("Busy - transcription in progress, please wait", new SolidColorBrush(Color.FromRgb(255, 165, 0)));
                    ErrorLogger.LogInfo("StartRecording: Blocked - transcription already in progress");
                    return;
                }

                StopStuckStateRecoveryTimer();

                recordingStartTime = DateTime.Now;
                isRecording = true;

                try
                {
                    recorder.StartRecording();

                    if (recorder.IsRecording)
                    {
                        recordingElapsedTimer = new System.Windows.Threading.DispatcherTimer
                        {
                            Interval = TimeSpan.FromSeconds(1)
                        };
                        recordingElapsedTimer.Tick += (s, e) =>
                        {
                            var elapsed = DateTime.Now - recordingStartTime;
                            UpdateStatus($"Recording {elapsed:m\\:ss}", new SolidColorBrush(StatusColors.Recording));
                        };
                        recordingElapsedTimer.Start();

                        UpdateUIForCurrentMode();
                        TranscriptionText.Foreground = Brushes.Gray;
                    }
                    else
                    {
                        ErrorLogger.LogWarning("StartRecording: Recording failed to start, rolling back state");
                        isRecording = false;
                        UpdateStatus("Failed to start recording", Brushes.Red);
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("StartRecording", ex);
                    isRecording = false;
                    UpdateStatus("Failed to start recording", Brushes.Red);
                }
            }
        }

        private void StopRecording(bool cancel = false)
        {
            ErrorLogger.LogDebug($"StopRecording: Entry - isRecording={isRecording}, cancel={cancel}");

            if (!isRecording)
            {
                ErrorLogger.LogDebug("StopRecording: Not recording, returning early");
                return;
            }

            recordingElapsedTimer?.Stop();
            recordingElapsedTimer = null;

            isRecording = false;

            try
            {
                audioRecorder?.StopRecording();

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
                    StartStuckStateRecoveryTimer();
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("StopRecording", ex);
                UpdateStatus("Failed to stop recording", Brushes.Red);
            }
        }

        private void OnHotkeyPressed(object? sender, EventArgs e)
        {
            // CRITICAL: Event handlers must never throw exceptions - wrap entire method
            try
            {
                ErrorLogger.LogMessage($"OnHotkeyPressed: Entry - Mode={settings.Mode}, // WEEK1-DAY3: State managed by coordinator - isRecording ={isRecording}, isHotkeyMode={isHotkeyMode}");

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
                    ErrorLogger.LogMessage($"OnHotkeyPressed: Inside lock - Mode={settings.Mode}, // WEEK1-DAY3: State managed by coordinator - isRecording ={isRecording}");

                    if (settings.Mode == Models.RecordMode.PushToTalk)
                    {
                        HandlePushToTalkPressed();
                    }
                    else if (settings.Mode == Models.RecordMode.Toggle)
                    {
                        HandleToggleModePressed();
                    }
                }

                ErrorLogger.LogMessage($"OnHotkeyPressed: Exit - Mode={settings.Mode}, // WEEK1-DAY3: State managed by coordinator - isRecording ={isRecording}, isHotkeyMode={isHotkeyMode}");
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
                ErrorLogger.LogMessage($"OnHotkeyReleased: Entry - Mode={settings.Mode}, // WEEK1-DAY3: State managed by coordinator - isRecording ={isRecording}, isHotkeyMode={isHotkeyMode}");

                lock (recordingLock)
                {
                    ErrorLogger.LogMessage($"OnHotkeyReleased: Inside lock - Mode={settings.Mode}, // WEEK1-DAY3: State managed by coordinator - isRecording ={isRecording}, isHotkeyMode={isHotkeyMode}");

                    if (settings.Mode == Models.RecordMode.PushToTalk)
                    {
                        HandlePushToTalkReleased();
                    }
                    // In Toggle mode, do nothing on release - all action happens on press
                }

                ErrorLogger.LogMessage($"OnHotkeyReleased: Exit - Mode={settings.Mode}, // WEEK1-DAY3: State managed by coordinator - isRecording ={isRecording}, isHotkeyMode={isHotkeyMode}");
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
                // UI BUG FIX: Suppress polling mode notification during initialization
                // Prevents overlapping yellow text bug on launch (even on slow PCs with 10+ second startup)
                // Replaced time-based check with initialization flag for 100% reliability
                if (isInitializing)
                {
                    ErrorLogger.LogDebug($"Suppressing polling mode notification during initialization: {message}");
                    return;
                }

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
                ErrorLogger.LogMessage($"HandlePushToTalkPressed: State mismatch detected - // WEEK1-DAY3: State managed by coordinator - isRecording ={isRecording}, actuallyRecording={actuallyRecording}. Syncing...");
                // WEEK1-DAY3: State managed by coordinator - isRecording = actuallyRecording;
            }

            if (!isRecording)
            {
                ErrorLogger.LogMessage("HandlePushToTalkPressed: Setting isHotkeyMode=true and starting recording");
                isHotkeyMode = true;

                // Sound feedback removed per user request

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
                ErrorLogger.LogMessage($"HandlePushToTalkReleased: State mismatch detected - // WEEK1-DAY3: State managed by coordinator - isRecording ={isRecording}, actuallyRecording={actuallyRecording}. Syncing...");
                // WEEK1-DAY3: State managed by coordinator - isRecording = actuallyRecording;
            }

            if (isRecording && isHotkeyMode)
            {
                ErrorLogger.LogMessage("HandlePushToTalkReleased: Stopping recording");

                // Audio feedback for push-to-talk stop
                // Sound removed per user request

                StopRecording(false);
                isHotkeyMode = false;
                ErrorLogger.LogMessage("HandlePushToTalkReleased: isHotkeyMode reset to false");
            }
            else
            {
                ErrorLogger.LogMessage($"HandlePushToTalkReleased: Not stopping - // WEEK1-DAY3: State managed by coordinator - isRecording ={isRecording}, isHotkeyMode={isHotkeyMode}");
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
                ErrorLogger.LogMessage($"HandleToggleModePressed: State mismatch detected - // WEEK1-DAY3: State managed by coordinator - isRecording ={isRecording}, actuallyRecording={actuallyRecording}. Syncing...");
                // WEEK1-DAY3: State managed by coordinator - isRecording = actuallyRecording;
            }

            ErrorLogger.LogMessage($"HandleToggleModePressed: Current state - // WEEK1-DAY3: State managed by coordinator - isRecording ={isRecording}, audioRecorder.isRecording={actuallyRecording}");

            if (!isRecording)
            {
                ErrorLogger.LogMessage("HandleToggleModePressed: TOGGLE ON - Starting continuous recording");

                // Audio feedback for toggle start
                // Sound removed per user request

                StartRecording();
                StartAutoTimeoutTimer();
                ErrorLogger.LogMessage($"HandleToggleModePressed: After start - // WEEK1-DAY3: State managed by coordinator - isRecording ={isRecording}");
            }
            else
            {
                ErrorLogger.LogMessage("HandleToggleModePressed: TOGGLE OFF - Stopping recording");

                // Audio feedback for toggle stop
                // Sound removed per user request

                StopRecording(false);
                StopAutoTimeoutTimer();
                ErrorLogger.LogMessage($"HandleToggleModePressed: After stop - // WEEK1-DAY3: State managed by coordinator - isRecording ={isRecording}");
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

            // BUG FIX: Increased from 15s to 120s to match transcription timeout
            // 15s was TOO AGGRESSIVE - normal transcriptions with Small model can take 20-30s
            // This should only fire if Whisper completely hangs (rare edge case)
            const int maxProcessingSeconds = 120; // 2 minutes max

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

                // Reset all state flags
                lock (recordingLock)
                {
                    ErrorLogger.LogWarning($"OnStuckStateRecovery: Force-resetting state - was // WEEK1-DAY3: State managed by coordinator - isRecording ={isRecording}, isHotkeyMode={isHotkeyMode}");
                    // WEEK1-DAY3: State managed by coordinator - // WEEK1-DAY3: State managed by coordinator - isRecording = false;
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
                            // Sound removed per user request

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
        /// Handle audio file ready event from AudioRecorder
        /// </summary>
        private async void OnAudioFileReady(object? sender, string audioFilePath)
        {
            if (isTranscribing)
            {
                ErrorLogger.LogWarning("OnAudioFileReady: Already transcribing, ignoring");
                return;
            }

            isTranscribing = true;

            try
            {
                var transcriber = whisperService;
                if (transcriber == null)
                {
                    throw new InvalidOperationException("Whisper service not initialized");
                }

                var transcription = await transcriber.TranscribeAsync(audioFilePath);

                await Dispatcher.InvokeAsync(() =>
                {
                    StopStuckStateRecoveryTimer();

                    if (!string.IsNullOrWhiteSpace(transcription))
                    {
                        TranscriptionText.Text = transcription;
                        TranscriptionText.Foreground = Brushes.Black;
                        UpdateStatus("✓ Transcribed successfully", Brushes.Green);

                        var historyItem = new TranscriptionHistoryItem
                        {
                            Text = transcription,
                            Timestamp = DateTime.Now,
                            ModelUsed = settings.WhisperModel
                        };
                        historyService?.AddToHistory(historyItem);
                        _ = UpdateHistoryUI();
                        _ = Task.Run(() => SaveSettingsAsync());

                        if (settings.AutoPaste)
                        {
                            try
                            {
                                textInjector?.InjectText(transcription);
                            }
                            catch (Exception ex)
                            {
                                ErrorLogger.LogError("Text injection failed", ex);
                            }
                        }

                        var durationMs = (DateTime.UtcNow - recordingStartTime).TotalMilliseconds;
                        var wordCount = transcription.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(TimingConstants.TranscriptionTextResetDelayMs);
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    if (!isRecording)
                                    {
                                        UpdateUIForCurrentMode();
                                        UpdateStatus("Ready", Brushes.Green);
                                    }
                                });
                            }
                            catch { }
                        });
                    }
                    else
                    {
                        TranscriptionText.Text = "(No speech detected)";
                        TranscriptionText.Foreground = Brushes.Gray;
                        UpdateStatus("Ready", Brushes.Green);
                    }

                    lock (recordingLock)
                    {
                        if (!audioRecorder?.IsRecording ?? true)
                        {
                            if (!(settings.Mode == RecordMode.PushToTalk && isHotkeyMode))
                            {
                                isHotkeyMode = false;
                                StopAutoTimeoutTimer();
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("OnAudioFileReady", ex);
                await Dispatcher.InvokeAsync(() =>
                {
                    StopStuckStateRecoveryTimer();
                    TranscriptionText.Text = "Transcription error";
                    TranscriptionText.Foreground = Brushes.Red;
                    UpdateStatus("Error", Brushes.Red);

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
                                    UpdateStatus("Ready", Brushes.Green);
                                }
                            });
                        }
                        catch { }
                    });
                });
            }
            finally
            {
                isTranscribing = false;

                try
                {
                    if (File.Exists(audioFilePath))
                    {
                        File.Delete(audioFilePath);
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError($"Failed to delete temp audio file: {audioFilePath}", ex);
                }
            }
        }

        /// <summary>
        /// TIER 1.4: TRUE ASYNC - Non-blocking settings save
        /// Completed implementation with File.WriteAllTextAsync() and SemaphoreSlim
        /// </summary>
        private async Task SaveSettingsAsync()
        {
            try
            {
                // TIER 1.4: Now using true async implementation (0ms UI thread blocking)
                await SaveSettingsInternalAsync();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("SaveSettingsAsync failed", ex);
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

        // MEMORY_FIX 2025-10-08: Handle zombie whisper.exe process detection
        private void OnZombieProcessDetected(object? sender, ZombieCleanupEventArgs e)
        {
            ErrorLogger.LogWarning($"Zombie whisper.exe detected and killed: PID {e.ProcessId} ({e.MemoryMB}MB)");
            // Could notify user via toast notification if needed
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
            {
                SaveSettings();
            }
        }

        private void VoiceShortcutsButton_Click(object sender, RoutedEventArgs e)
        {
            {
                SaveSettings();
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var oldMode = settings.Mode;
            var oldHotkey = settings.RecordHotkey;
            var oldModifiers = settings.HotkeyModifiers;

            currentSettingsWindow = new SettingsWindowNew(settings, () => TestButton_Click(this, new RoutedEventArgs()), () => SaveSettings());
            currentSettingsWindow.Owner = this;

            if (currentSettingsWindow.ShowDialog() == true)
            {
                settings = SettingsValidator.ValidateAndRepair(currentSettingsWindow.Settings);
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
                        // WEEK1-DAY3: State managed by coordinator - isRecording = wasRecording;
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
                            // Sound removed per user request
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


        // v1.0.56 CRITICAL FIX: Prevent deadlock on app close
        // Old approach: SaveSettingsInternalAsync().Wait() blocks UI thread for 5-30 seconds
        // New approach: Use async OnClosing to await settings save without blocking
        private bool isClosingHandled = false;

        protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!isClosingHandled)
            {
                e.Cancel = true; // Prevent immediate close
                isClosingHandled = true;

                // CRITICAL FIX (BUG-002 + v1.0.56): Flush pending settings save BEFORE disposal
                // NOW ASYNC - no UI thread blocking!
                if (settingsSaveTimer != null && settingsSaveTimer.IsEnabled)
                {
                    settingsSaveTimer.Stop();
                    await SaveSettingsInternalAsync(); // NO .Wait() - async all the way
                    ErrorLogger.LogMessage("v1.0.56 FIX: Flushed pending settings save on app close (async, no blocking)");
                }

                SaveSettings(); // Belt-and-suspenders: call debounced save too (will be no-op if timer null)

                base.OnClosing(e);
                Close(); // Now actually close the window
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Settings save already handled in OnClosing (async pattern)

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
                if (audioRecorder != null)
                {
                    audioRecorder.AudioFileReady -= OnAudioFileReady;
                }

                if (hotkeyManager != null)
                {
                    hotkeyManager.HotkeyPressed -= OnHotkeyPressed;
                    hotkeyManager.HotkeyReleased -= OnHotkeyReleased;
                    hotkeyManager.PollingModeActivated -= OnPollingModeActivated;
                }


                if (memoryMonitor != null)
                {
                    memoryMonitor.MemoryAlert -= OnMemoryAlert;
                }

                // MEMORY_FIX 2025-10-08: Unsubscribe zombie cleanup service
                if (zombieCleanupService != null)
                {
                    zombieCleanupService.ZombieDetected -= OnZombieProcessDetected;
                }

                // Dispose child windows (WPF Window resources)

                try { currentSettingsWindow?.Close(); } catch { }
                currentSettingsWindow = null;


                // Now dispose services in reverse order of creation
                // MEMORY_FIX 2025-10-08: Dispose zombie cleanup service
                zombieCleanupService?.Dispose();
                zombieCleanupService = null;

                memoryMonitor?.Dispose();
                memoryMonitor = null;

                systemTrayManager?.Dispose();
                systemTrayManager = null;

                hotkeyManager?.Dispose();
                hotkeyManager = null;

                // SoundService removed per user request - no disposal needed

                // Dispose semaphore (SemaphoreSlim implements IDisposable)
                try { saveSettingsSemaphore?.Dispose(); } catch { }

                // Dispose cancellation token
                try { recordingCancellation?.Dispose(); } catch { }

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



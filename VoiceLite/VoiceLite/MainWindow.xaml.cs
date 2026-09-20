using System;
using System.Collections.Generic;
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
using VoiceLite.Utilities;
using VoiceLite.Presentation.ViewModels;
using System.Text.Json;

namespace VoiceLite
{
    public partial class MainWindow : Window
    {
        #region Fields & Properties

        // Service dependencies
        private AudioRecorder? audioRecorder;
        private TranscriptionService? transcriptionService;
        private HotkeyManager? hotkeyManager;
        private TextInjector? textInjector;
        private SystemTrayManager? systemTrayManager;
        private TranscriptionHistoryService? historyService;
        private CustomShortcutService? customShortcutService;
        private ProFeatureService? proFeatureService;
        private LicenseService? licenseService;
        private Services.Audio.SileroVadService? vadService;
        // SoundService removed per user request - no audio feedback

        // StatusViewModel drives the status indicator binding in MainWindow.xaml.
        private StatusViewModel? statusViewModel;
        public StatusViewModel StatusViewModel => statusViewModel!;

        // The dictation state machine (hotkey -> recording -> transcription -> result).
        // MainWindow only renders what it reports; see DictationController.
        private DictationController? dictation;

        // Initialization flag to suppress polling mode warnings during startup
        // More reliable than time-based check (works on slow PCs with 10+ second startup)
        private bool isInitializing = true;
        // Replaced lock with SemaphoreSlim for async compatibility
        private readonly SemaphoreSlim saveSettingsSemaphore = new SemaphoreSlim(1, 1);
        // CancellationToken for settings save operations (cancelled on disposal)
        private readonly CancellationTokenSource settingsSaveCts = new CancellationTokenSource();

        // Pre-configured JSON serializer options (5-15ms savings per save)
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultBufferSize = 131072 // 128KB buffer (matches typical settings size with large history)
        };

        private Settings settings = new();

        // Timers
        private System.Threading.Timer? updateCheckTimer;
        private UpdateInfo? pendingUpdate;
        private string? notifiedUpdateVersion;
        private UpdateWindow? updateWindow;
        private System.Windows.Threading.DispatcherTimer? recordingElapsedTimer;
        private System.Windows.Threading.DispatcherTimer? settingsSaveTimer;

        // Improved timer management with Dictionary for proper cleanup
        // Dictionary allows us to track and clean up specific timers by ID
        private readonly HashSet<System.Windows.Threading.DispatcherTimer> activeStatusTimers = new HashSet<System.Windows.Threading.DispatcherTimer>();
        private readonly object timerLock = new object();

        // Child windows (for proper disposal)
        private SettingsWindow? currentSettingsWindow;

        // Timing constants (in milliseconds)
        private const int STATUS_MESSAGE_DISPLAY_DURATION_MS = 3000; // How long to show status messages

        #endregion

        #region Initialization & Lifecycle

        public MainWindow()
        {
            InitializeComponent();

            // LICENSE STARTUP FIX: LicenseService MUST be constructed before LoadSettings().
            // Its ctor migrates any legacy plaintext key from settings.json into DPAPI
            // license.dat and loads the authoritative DPAPI state, so the entitlement check
            // inside LoadSettings() → ValidateTranscriptionModel() sees real license state.
            // When it was constructed later (InitializeServicesAsync), the check ran against
            // a null service on every cold start: Pro users were reset to Free and the
            // legacy plaintext key was wiped before migration could consume it.
            licenseService = new LicenseService();

            LoadSettings();

            statusViewModel = new StatusViewModel();
            statusViewModel.SetReady(); // Start with Ready state

            // Set DataContext AFTER ViewModels initialized
            this.DataContext = this;

            // Run all async initialization on background thread
            // This prevents UI freeze during startup diagnostics and service initialization
            this.Loaded += MainWindow_Loaded;
            // NOTE: close/minimize-to-tray handling lives in the OnClosing override (one place).
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
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

                    // Only use validated settings if repair succeeded
                    // If validation fails, use defaults WITHOUT running cleanup (prevents data loss)
                    if (validatedSettings != null)
                    {
                        settings = validatedSettings;
                        ErrorLogger.LogMessage($"Settings loaded from: {settingsPath}");

                        // MIGRATION 3: Upgrade Default UI preset to Compact (v1.0.38+) - ONE-TIME ONLY
                        // UI Preset is now hardcoded to Compact - migration code removed

                        // Verify transcription model exists
                        ValidateTranscriptionModel();

                        // Only cleanup history if we successfully loaded existing settings
                        // DO NOT cleanup on default settings (prevents accidental data loss)
                        CleanupOldHistoryItems();
                    }
                    else
                    {
                        ErrorLogger.LogMessage("Settings validation failed - attempting history recovery");

                        // Preserve transcription history before resetting
                        // Prevents permanent data loss when settings file is corrupted
                        var preservedHistory = new List<TranscriptionHistoryItem>();
                        try
                        {
                            if (loadedSettings?.TranscriptionHistory != null)
                            {
                                preservedHistory = loadedSettings.TranscriptionHistory.ToList();
                                ErrorLogger.LogMessage($"Preserved {preservedHistory.Count} history items from corrupted settings");
                            }
                        }
                        catch (Exception historyEx)
                        {
                            ErrorLogger.LogWarning($"Failed to preserve history: {historyEx.Message}");
                        }

                        settings = new Settings();
                        settings.TranscriptionHistory = preservedHistory;
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

            // Rewrite legacy Whisper GGML model ids to the Parakeet canonical id.
            // Must run before any service consumes settings.TranscriptionModel.
            if (SettingsMigration.Migrate(settings))
            {
                try { SaveSettings(); }
                catch (Exception ex) { ErrorLogger.LogError("SettingsMigration.SavePostMigration", ex); }
            }

            MinimizeCheckBox.IsChecked = settings.MinimizeToTray;
            UpdateConfigDisplay();
        }

        /// <summary>
        /// Remove old transcription history items to prevent memory bloat
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
                    .Where(h => h.Timestamp < sevenDaysAgo && !h.IsPinned) // pinned items are exempt from the 7-day purge
                    .ToList();

                if (itemsToRemove.Count > 0)
                {
                    foreach (var item in itemsToRemove)
                    {
                        settings.TranscriptionHistory.Remove(item);
                    }
                    ErrorLogger.LogMessage($"Cleaned up {itemsToRemove.Count} old history items (>7 days)");
                    _ = SaveSettingsInternalAsync(); // Fire-and-forget async save
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
                    await SaveSettingsInternalAsync(); // Await async save
                };
            }

            // Restart timer (debounce)
            settingsSaveTimer.Stop();
            settingsSaveTimer.Start();
        }

        /// <summary>
        /// TRUE ASYNC - Internal method that saves settings to disk
        /// Uses atomic write pattern to prevent file corruption
        /// Now uses async I/O to prevent UI thread blocking
        /// </summary>
        private async Task SaveSettingsInternalAsync()
        {
            // V1.0.55 Capture UI state on UI thread BEFORE going async
            // Prevents "The calling thread cannot access this object" errors
            bool minimizeToTray = false;
            await Dispatcher.InvokeAsync(() =>
            {
                minimizeToTray = MinimizeCheckBox.IsChecked == true;
            });

            // Use SemaphoreSlim instead of lock for async compatibility
            // Use cancellation token to allow graceful shutdown
            // ConfigureAwait(false) on every await past the UI-state capture above:
            // the finally block's semaphore Release must not queue behind a busy/blocked
            // dispatcher, or the exit-flush wait on this semaphore can never succeed
            // against an in-flight save.
            await saveSettingsSemaphore.WaitAsync(settingsSaveCts.Token).ConfigureAwait(false);
            try
            {
                try
                {
                    // Ensure AppData directory exists
                    EnsureAppDataDirectoryExists();

                    string settingsPath = GetSettingsPath();

                    // Update settings inside lock, serialize on background thread
                    // Prevents race condition while maintaining async performance
                    lock (settings.SyncRoot)
                    {
                        // Update settings inside lock to prevent concurrent modifications
                        settings.MinimizeToTray = minimizeToTray;
                    }

                    // Serialize on background thread to avoid UI blocking
                    // Use pre-configured JsonSerializerOptions with optimized buffer size
                    // Lock is held only during serialization to prevent concurrent modifications
                    string json = await Task.Run(() =>
                    {
                        lock (settings.SyncRoot)
                        {
                            return JsonSerializer.Serialize(settings, _jsonSerializerOptions);
                        }
                    }).ConfigureAwait(false);

                    // Use async file I/O to prevent UI thread blocking (was 50ms)
                    // Write to temp file first, then rename (rename is atomic on Windows)
                    string tempPath = settingsPath + ".tmp";
                    await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);

                    // Verify temp file is valid JSON before replacing original
                    // Prevents data loss if app crashes during serialization
                    try
                    {
                        var testLoad = JsonSerializer.Deserialize<Settings>(await File.ReadAllTextAsync(tempPath).ConfigureAwait(false));
                        if (testLoad == null)
                        {
                            throw new InvalidDataException("Settings deserialized to null");
                        }
                    }
                    catch (Exception validationEx)
                    {
                        // Delete corrupt temp file
                        try { File.Delete(tempPath); } catch (Exception ex) { ErrorLogger.LogDebug($"Settings temp file cleanup failed: {ex.Message}"); }
                        throw new InvalidOperationException("Settings save failed - temp file validation failed", validationEx);
                    }

                    // Use File.Move with overwrite to handle race conditions
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
                // Always release semaphore, even on exception
                saveSettingsSemaphore.Release();
            }
        }

        private async Task InitializeServicesAsync()
        {
            try
            {
                await Dispatcher.InvokeAsync(() => statusViewModel?.UpdateStatus("Initializing services...", Brushes.Orange));

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
                        statusViewModel?.UpdateStatus("No microphone detected", Brushes.Red);
                    });
                }

                // Null-check before using audioRecorder
                if (audioRecorder != null)
                {
                    audioRecorder.SetDevice(settings.SelectedMicrophoneIndex, settings.SelectedMicrophoneName);
                }

                // Initialize Silero VAD for silence trimming
                var vadModelPath = Services.Audio.SileroVadService.FindModelPath();
                if (vadModelPath != null)
                {
                    try
                    {
                        vadService = new Services.Audio.SileroVadService(vadModelPath);
                        audioRecorder.SetVadService(vadService, settings);
                        ErrorLogger.LogWarning("Silero VAD initialized (threshold=0.35)");
                    }
                    catch (Exception vadEx)
                    {
                        ErrorLogger.LogWarning($"Silero VAD init failed, continuing without VAD: {vadEx.Message}");
                    }
                }
                else
                {
                    ErrorLogger.LogWarning("Silero VAD model not found, silence trimming disabled");
                }

                // Initialize Pro feature service (for license validation)
                // Required for Pro model access control
                proFeatureService = new ProFeatureService(settings);

                // LicenseService is constructed in the MainWindow ctor, BEFORE LoadSettings(),
                // so the DPAPI tamper check in ValidateTranscriptionModel sees real license
                // state during startup. Do not construct a second instance here.

                // Initialize ASR service (in-process via Sherpa-ONNX + Parakeet v3)
                transcriptionService = new TranscriptionService(settings, null, proFeatureService);

                // Initialize services
                historyService = new TranscriptionHistoryService(settings);
                customShortcutService = new CustomShortcutService(settings);
                // SoundService removed per user request - no audio feedback

                // Null-check all dependencies before creating coordinator
                if (audioRecorder == null || transcriptionService == null || textInjector == null ||
                    historyService == null || customShortcutService == null)
                {
                    throw new InvalidOperationException(
                        "Failed to initialize core services - one or more required services is null. " +
                        "Please restart VoiceLite or check the logs for errors.");
                }

                // The controller owns recording/transcription state; timers are routed
                // through the dispatcher so the recorder is always driven from the UI thread.
                dictation = new DictationController(audioRecorder, transcriptionService, () => settings,
                    invoke: a => Dispatcher.InvokeAsync(a));
                dictation.Notice += OnDictationNotice;

                // Null-check hotkeyManager before subscribing
                if (hotkeyManager != null)
                {
                    hotkeyManager.HotkeyPressed += OnHotkeyPressed;
                    hotkeyManager.HotkeyReleased += OnHotkeyReleased;

                    // Subscribe to polling mode notification
                    hotkeyManager.PollingModeActivated += OnPollingModeActivated;
                }

                systemTrayManager = new SystemTrayManager();
                await Dispatcher.InvokeAsync(() => systemTrayManager.Initialize(this, GetHotkeyDisplayString()));

                // Load and display existing history
                await Dispatcher.InvokeAsync(() => _ = UpdateHistoryUI());
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                    MessageBox.Show($"Failed to initialize: {ex.Message}",
                        "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Run ALL initialization asynchronously to prevent UI freeze
                // Old code ran diagnostics/services in fire-and-forget tasks, causing race conditions
                // New code ensures proper ordering: services → hotkey → UI updates

                // Step 1: Initialize services (runs in background)
                await Task.Run(() => InitializeServicesAsync());

                // Step 2: Register hotkey (ONLY after services are ready)
                // Wrap hotkey registration in try-catch to show user-friendly error
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

                // Step 3: Update UI (now safe - all services initialized)
                string hotkeyDisplay = GetHotkeyDisplayString();
                UpdateUIForCurrentMode();
                UpdateConfigDisplay();

                // Initialization complete - allow polling mode warnings to show now
                isInitializing = false;

                // Mark initialization complete - show "Ready" status
                UpdateStatus("Ready", Brushes.Green);

                // Update check: first run 3s after startup so HTTP never touches the
                // startup path, then once a day - the app runs for days at a time and a
                // single launch-time balloon was missed for months (v2.1.2 -> v2.4.1).
                updateCheckTimer = new System.Threading.Timer(
                    _ => _ = RunUpdateCheckAsync(),
                    null,
                    TimeSpan.FromSeconds(3),
                    TimeSpan.FromHours(24));
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("MainWindow_Loaded initialization failed", ex);
                statusViewModel?.UpdateStatus("Initialization failed - Click for help", Brushes.Red);

                var errorMessage = $"Failed to initialize VoiceLite:\n\n{ex.Message}\n\n";

                // Add helpful troubleshooting steps based on error type
                if (ex.Message.Contains("whisper") || ex.Message.Contains("model"))
                {
                    errorMessage += "Troubleshooting:\n" +
                                  "1. Verify VoiceLite installed correctly\n" +
                                  "2. Check if model files exist in installation folder\n" +
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
                ModelText.Text = GetModelDisplayName(settings.TranscriptionModel);
            }
        }

        private string GetModelDisplayName(string modelPath)
        {
            // v2.0: single-model world — the Whisper-era tiny/base/small/medium/large
            // mappings are gone. SettingsMigration rewrites legacy ids to the Parakeet
            // canonical id, so anything else is genuinely unknown.
            if (!string.IsNullOrEmpty(modelPath) &&
                modelPath.Contains("parakeet", StringComparison.OrdinalIgnoreCase))
            {
                return "Parakeet v3";
            }

            return "Unknown";
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
                if (dictation?.IsRecording == true)
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


        private async Task RunUpdateCheckAsync()
        {
            // try/catch around everything so any failure (no network, parse error, etc.)
            // can never block or affect main app flow.
            try
            {
                var info = await UpdateCheckService.CheckAsync(settings).ConfigureAwait(false);
                if (info == null) return;
                await Dispatcher.InvokeAsync(() =>
                {
                    pendingUpdate = info;
                    // Don't clobber "Downloading update... 42%" if a download is in flight.
                    if (UpdateBanner != null && updateWindow == null)
                    {
                        UpdateBanner.Text = $"Update {info.Version} available";
                        UpdateBanner.Visibility = Visibility.Visible;
                    }
                    // Balloon once per version; the banner stays until the user updates.
                    if (notifiedUpdateVersion != info.Version)
                    {
                        notifiedUpdateVersion = info.Version;
                        systemTrayManager?.ShowUpdateAvailable(info.Version, info.DownloadUrl);
                    }
                });
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Update check task failed", ex);
            }
        }

        private void UpdateBanner_Click(object sender, MouseButtonEventArgs e)
        {
            var info = pendingUpdate;
            if (info == null) return;

            // One dialog at a time: re-clicking the banner mid-download just refocuses it.
            if (updateWindow != null)
            {
                updateWindow.Activate();
                return;
            }

            try
            {
                var window = new UpdateWindow(info, () => dictation != null && dictation.State != DictationState.Idle) { Owner = this };
                updateWindow = window;
                window.DownloadProgressChanged += pct =>
                {
                    if (UpdateBanner != null)
                        UpdateBanner.Text = $"Downloading update... {pct * 100:0}%";
                };
                window.Closed += (_, _) =>
                {
                    updateWindow = null;
                    switch (window.Result)
                    {
                        case UpdateWindowResult.Skipped:
                            lock (settings.SyncRoot)
                            {
                                settings.SkippedUpdateVersion = info.Version;
                            }
                            SaveSettings();
                            pendingUpdate = null;
                            if (UpdateBanner != null) UpdateBanner.Visibility = Visibility.Collapsed;
                            break;
                        case UpdateWindowResult.Installing:
                            if (UpdateBanner != null) UpdateBanner.Text = "Installing update...";
                            break;
                        default:
                            if (UpdateBanner != null) UpdateBanner.Text = $"Update {info.Version} available";
                            break;
                    }
                };
                window.Show();
            }
            catch (Exception ex)
            {
                updateWindow = null;
                ErrorLogger.LogError("UpdateBanner_Click: failed to open update window", ex);
            }
        }

        private void UpdateStatus(string status, Brush color)
        {
            // Delegate to StatusViewModel (phase 3 refactor)
            // UI elements will be updated via PropertyChanged handler
            statusViewModel?.UpdateStatus(status, color);
        }

        // Helper method to safely update TranscriptionText with null protection
        private void UpdateTranscriptionText(string text, Brush? foreground = null)
        {
            if (TranscriptionText is not null)
            {
                TranscriptionText.Text = text;
                if (foreground is not null)
                {
                    TranscriptionText.Foreground = foreground;
                }
            }
        }

        // ------------------------------------------------------------------
        // Dictation: DictationController owns the state machine; this window only
        // renders its notices. Never call back into the controller from a notice
        // handler synchronously - always via the dispatcher.
        // ------------------------------------------------------------------

        private void OnHotkeyPressed(object? sender, EventArgs e) => dictation?.HotkeyPressed();
        private void OnHotkeyReleased(object? sender, EventArgs e) => dictation?.HotkeyReleased();

        private void OnDictationNotice(object? sender, DictationEvent ev)
        {
            _ = Dispatcher.InvokeAsync(() => ShowDictationNotice(ev));
        }

        private void ShowDictationNotice(DictationEvent ev)
        {
            try
            {
                switch (ev.Kind)
                {
                    case DictationEventKind.RecordingStarted:
                        UpdateStatus("Recording 0:00", new SolidColorBrush(StatusColors.Recording));
                        UpdateUIForCurrentMode();
                        TranscriptionText.Foreground = Brushes.Gray;
                        StartElapsedTimer();
                        break;

                    case DictationEventKind.RecordingStopped:
                        StopElapsedTimer();
                        UpdateStatus("Processing...", new SolidColorBrush(StatusColors.Processing));
                        UpdateTranscriptionText("Processing audio...", new SolidColorBrush(StatusColors.Processing));
                        break;

                    case DictationEventKind.RecordingCancelled:
                        StopElapsedTimer();
                        UpdateStatus("Cancelled", Brushes.Gray);
                        UpdateUIForCurrentMode();
                        break;

                    case DictationEventKind.RecordingFailed:
                        StopElapsedTimer();
                        UpdateTranscriptionText(ev.Message ?? "Recording failed", Brushes.Red);
                        UpdateStatus(ev.Message ?? "Recording failed", Brushes.Red);
                        break;

                    case DictationEventKind.RecordingTooShort:
                        StopElapsedTimer();
                        UpdateStatus("Too short - hold the hotkey while speaking", Brushes.Gray);
                        UpdateUIForCurrentMode();
                        break;

                    case DictationEventKind.Busy:
                        UpdateStatus("Busy - transcription in progress, please wait", new SolidColorBrush(Color.FromRgb(255, 165, 0)));
                        break;

                    case DictationEventKind.TranscriptionCompleted:
                        DeliverTranscription(ev.Text ?? string.Empty);
                        break;

                    case DictationEventKind.NoSpeech:
                        // TranscriptionText is collapsed in XAML - the status line is what the user sees.
                        if (ev.MicSilent)
                        {
                            UpdateTranscriptionText($"No audio from microphone '{ev.MicName}' - muted, unplugged, or wrong device", Brushes.Red);
                            UpdateStatus($"No audio from '{ev.MicName}' - check mic in Settings", Brushes.Red);
                        }
                        else
                        {
                            UpdateTranscriptionText($"(No speech detected - mic: {ev.MicName})", Brushes.Gray);
                            UpdateStatus($"No speech detected (mic: {ev.MicName})", Brushes.Gray);
                        }
                        break;

                    case DictationEventKind.TranscriptionFailed:
                        UpdateTranscriptionText(ev.Message ?? "Transcription error", Brushes.Red);
                        UpdateStatus("Error", Brushes.Red);
                        ResetToReadyLater();
                        break;

                    case DictationEventKind.AutoStopped:
                        // Non-modal on purpose: a MessageBox here used to block the UI thread
                        // while holding the recording lock and had to be clicked away every time.
                        systemTrayManager?.ShowBalloonTip("Recording auto-stopped",
                            "Stopped after 5 minutes (hot-mic safety). Transcribing what was captured.");
                        break;

                    case DictationEventKind.StuckRecovered:
                        StopElapsedTimer();
                        UpdateTranscriptionText("(Timeout - recovered)", Brushes.Gray);
                        UpdateStatus("Ready", new SolidColorBrush(StatusColors.Ready));
                        UpdateUIForCurrentMode();
                        break;

                    case DictationEventKind.Fault:
                        MessageBox.Show(ev.Message ?? "Hotkey system error. Please restart VoiceLite.",
                            "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError($"ShowDictationNotice({ev.Kind})", ex);
            }
        }

        /// <summary>Show, remember, and paste (or copy) a finished transcription.</summary>
        private void DeliverTranscription(string transcription)
        {
            TranscriptionText.Text = transcription;
            TranscriptionText.Foreground = Brushes.Black;
            UpdateStatus("Ready", Brushes.Green);

            var processedText = customShortcutService?.ProcessShortcuts(transcription) ?? transcription;

            historyService?.AddToHistory(new TranscriptionHistoryItem
            {
                Text = processedText,          // what the user sees
                OriginalText = transcription,  // raw engine output, for re-injection
                Timestamp = DateTime.Now,
                ModelUsed = transcriptionService?.EffectiveTranslateToEnglish == true
                    ? TranslationModelResolverService.ModelId
                    : settings.TranscriptionModel
            });
            _ = UpdateHistoryUI();
            _ = Task.Run(() => SaveSettingsAsync());

            if (settings.AutoPaste)
            {
                try
                {
                    textInjector?.InjectText(processedText);
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Text injection failed", ex);
                    UpdateStatus("Paste failed - copy text from history", Brushes.Red);
                }
            }
            else
            {
                // Manual-paste flow: longer clipboard hold so the user can click into the
                // target field first. Required for clinical workflows (GPHealth pilot).
                try
                {
                    TextInjector.CopyToClipboardForManualPaste(processedText);
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Manual-paste clipboard copy failed", ex);
                    UpdateStatus("Copy failed - copy text from history", Brushes.Red);
                }
            }

            ResetToReadyLater(TimingConstants.TranscriptionTextResetDelayMs);
        }

        private void StartElapsedTimer()
        {
            StopElapsedTimer();
            var startedAt = dictation?.RecordingStartedAt ?? DateTime.Now;
            recordingElapsedTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            recordingElapsedTimer.Tick += (s, e) =>
                UpdateStatus($"Recording {DateTime.Now - startedAt:m\\:ss}", new SolidColorBrush(StatusColors.Recording));
            recordingElapsedTimer.Start();
        }

        private void StopElapsedTimer()
        {
            recordingElapsedTimer?.Stop();
            recordingElapsedTimer = null;
        }

        /// <summary>Put the status line back to Ready unless a new dictation is under way by then.</summary>
        private void ResetToReadyLater(int delayMs = STATUS_MESSAGE_DISPLAY_DURATION_MS)
        {
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                if (dictation == null || dictation.State == DictationState.Idle)
                {
                    UpdateUIForCurrentMode();
                    UpdateStatus("Ready", Brushes.Green);
                }
            };
            timer.Start();
        }

        /// <summary>
        /// Handle polling mode activation notification
        /// Shows a brief status message when polling mode is active for standalone modifier keys
        /// </summary>
        private void OnPollingModeActivated(object? sender, string message)
        {
            try
            {
                // Suppress polling mode notification during initialization
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
                    ErrorLogger.LogMessage($"{message}");

                    // Use new timer management method
                    CreateStatusTimer(message, TimeSpan.FromSeconds(3), Brushes.Orange);
                });
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("OnPollingModeActivated", ex);
            }
        }

        /// <summary>
        /// TRUE ASYNC - Non-blocking settings save
        /// Completed implementation with File.WriteAllTextAsync() and SemaphoreSlim
        /// </summary>
        private async Task SaveSettingsAsync()
        {
            try
            {
                // Now using true async implementation (0ms UI thread blocking)
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

        #endregion

        #region UI Event Handlers & Settings

        // The old MainWindow_Closing handler (tray-cancel via the Closing event)
        // was removed — its minimize-to-tray decision is consolidated into the OnClosing
        // override so the close flow has exactly one owner (no double-cancel confusion).

        /// <summary>
        /// After a Settings commit: register when the hotkey changed, and ALSO when it did not
        /// but the app holds no registration (startup registration failed because another app
        /// had the combination) - Save is the user's way to retry once the combination is free.
        /// Internal static for the regression tests.
        /// </summary>
        internal static bool ShouldRegisterHotkeyAfterSettings(bool hotkeyChanged, bool currentlyRegistered)
            => hotkeyChanged || !currentlyRegistered;

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var oldMode = settings.Mode;
            var oldHotkey = settings.RecordHotkey;
            var oldModifiers = settings.HotkeyModifiers;

            // Local capture: if the app exits while the dialog is open (tray Exit, Windows
            // logoff), OnClosed runs inside ShowDialog's nested pump and nulls the
            // currentSettingsWindow FIELD before ShowDialog returns — re-reading the field
            // after the pump would NRE mid-shutdown.
            var settingsWindow = new SettingsWindow(settings, () => SaveSettings(), hotkeyManager?.IsRegistered == true);
            currentSettingsWindow = settingsWindow;
            settingsWindow.Owner = this;

            // The window edits a cloned draft; live settings change only when
            // Save/Apply commits it. ChangesCommitted covers Apply-then-Cancel/X — those
            // commits are already live and persisted, so hotkey re-registration and UI
            // refresh below must still run. A pure Cancel leaves live settings untouched.
            var dialogSaved = settingsWindow.ShowDialog() == true;
            if (dialogSaved || settingsWindow.ChangesCommitted)
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

                // Only recreate if service is null (never had one)
                // Model switching is handled by the service itself in most cases
                if (transcriptionService == null)
                {
                    // Ensure proFeatureService exists before creating transcriptionService
                    if (proFeatureService == null)
                    {
                        proFeatureService = new ProFeatureService(settings);
                    }

                    ErrorLogger.LogMessage($"Creating initial transcription service with model: {settings.TranscriptionModel}");
                    transcriptionService = new TranscriptionService(settings, null, proFeatureService);
                }
                else
                {
                    // No recreation needed: TranscriptionService reloads the recognizer
                    // lazily on the next transcription when the preset (or model dir) changed.
                    ErrorLogger.LogMessage("Transcription service already exists - preset/model changes apply on next transcription");
                }

                // Only change microphone if not currently recording
                if (audioRecorder != null && dictation?.IsRecording != true && settings.SelectedMicrophoneIndex >= -1)
                {
                    try
                    {
                        audioRecorder.SetDevice(settings.SelectedMicrophoneIndex, settings.SelectedMicrophoneName);
                        ErrorLogger.LogMessage($"Microphone changed to device index: {settings.SelectedMicrophoneIndex} ({settings.SelectedMicrophoneName})");
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogMessage($"Failed to change microphone device: {ex.Message}");
                        // Don't throw - this is not fatal
                    }
                }
                else if (dictation?.IsRecording == true)
                {
                    ErrorLogger.LogMessage("Skipping microphone change - recording in progress");
                }

                // Re-register when the hotkey changed - and also when it did not but the app holds
                // no registration (startup registration failed: another app had the combination).
                bool hotkeyChanged = oldHotkey != settings.RecordHotkey || oldModifiers != settings.HotkeyModifiers;
                if (ShouldRegisterHotkeyAfterSettings(hotkeyChanged, hotkeyManager?.IsRegistered == true))
                {
                    try
                    {
                        ErrorLogger.LogMessage("Re-registering hotkey due to change");
                        // Unsubscribe from the old instance before disposal (mirrors OnClosed)
                        if (hotkeyManager != null)
                        {
                            hotkeyManager.HotkeyPressed -= OnHotkeyPressed;
                            hotkeyManager.HotkeyReleased -= OnHotkeyReleased;
                            hotkeyManager.PollingModeActivated -= OnPollingModeActivated;
                            hotkeyManager.Dispose();
                        }
                        hotkeyManager = new HotkeyManager();
                        hotkeyManager.HotkeyPressed += OnHotkeyPressed;
                        hotkeyManager.HotkeyReleased += OnHotkeyReleased;
                        // Re-subscribe polling mode notification too — it was
                        // silently dropped on every hotkey change (init path subscribes it)
                        hotkeyManager.PollingModeActivated += OnPollingModeActivated;
                        var helper = new WindowInteropHelper(this);
                        hotkeyManager.RegisterHotkey(helper.Handle, settings.RecordHotkey, settings.HotkeyModifiers);

                        ErrorLogger.LogWarning($"Hotkey registered after Settings: {GetHotkeyDisplayString()} (changed={hotkeyChanged})");
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogWarning($"Failed to register hotkey {GetHotkeyDisplayString()} after Settings (changed={hotkeyChanged}) - no hotkey is active: {ex.Message}");
                        // An unchanged hotkey that is still taken: the user was told at startup and
                        // the Settings window says so - no modal nag on every Settings save.
                        if (hotkeyChanged)
                        {
                            MessageBox.Show($"Failed to apply new hotkey: {ex.Message}\n\nNo hotkey is active right now. Pick a different one in Settings - the manual buttons still work.",
                                "Hotkey Registration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        // Don't return - continue with UI updates
                    }
                }
                else
                {
                    ErrorLogger.LogWarning("Hotkey unchanged and registered - skipping re-registration");
                }

                // Always update UI regardless of service recreation success
                UpdateUIForCurrentMode();
                UpdateConfigDisplay();
                systemTrayManager?.UpdateHotkeyDisplay(GetHotkeyDisplayString());
            }
        }


        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
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

            // Esc to close search removed - search feature not needed

            // Esc cancels the current recording
            if (e.Key == Key.Escape && dictation?.IsRecording == true)
            {
                ErrorLogger.LogWarning("Esc pressed - cancelling recording");
                dictation.Cancel();
                e.Handled = true;
            }
        }


        private void ValidateTranscriptionModel()
        {
            try
            {
                // Model file presence is validated by TranscriptionService / ModelResolverService
                // on first transcription. This method retains only the license-tamper checks that
                // have nothing to do with the engine.

                if (ApplyStartupEntitlementCheck(settings, licenseService))
                {
                    _ = SaveSettingsInternalAsync();
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("ValidateTranscriptionModel", ex);
            }
        }

        /// <summary>
        /// Startup entitlement check; returns true when settings changed and need saving.
        /// LICENSE STARTUP FIX: requires a constructed LicenseService — entitlement is never
        /// cleared or saved based on a null (not-yet-constructed) service, because "no service"
        /// is indistinguishable from "no license". internal static so the cold-start regression
        /// suite (EntitlementColdStartTests) exercises the exact production logic.
        /// </summary>
        internal static bool ApplyStartupEntitlementCheck(Settings settings, LicenseService? licenseService)
        {
            if (licenseService == null)
            {
                // Without the service we cannot know the real license state - touch nothing.
                ErrorLogger.LogWarning("Startup entitlement check skipped - LicenseService not constructed");
                return false;
            }

            bool needsSettingsSave = false;

            // SECURITY: Verify settings.IsProLicense is backed by a DPAPI-stored license.
            // DPAPI encrypts the license to the Windows user account, so an attacker who
            // edits settings.json to set IsProLicense=true won't have a matching license.dat.
            if (settings.IsProLicense && string.IsNullOrWhiteSpace(licenseService.GetStoredLicenseKey()))
            {
                ErrorLogger.LogWarning("SECURITY: IsProLicense=true but no DPAPI license found - possible manual edit, resetting to free");
                settings.IsProLicense = false;
                needsSettingsSave = true;
            }

            // Legacy cleanup: older builds wrote the license key plaintext to settings.json.
            // The DPAPI-encrypted license.dat is now the only authoritative store; by this
            // point the LicenseService ctor has already migrated any plaintext key into it.
            if (!string.IsNullOrEmpty(settings.LicenseKey))
            {
                settings.LicenseKey = string.Empty;
                needsSettingsSave = true;
            }

            return needsSettingsSave;
        }


        // V1.0.56 Prevent deadlock on app close
        // Old approach: SaveSettingsInternalAsync().Wait() blocks UI thread for 5-30 seconds
        // New approach: Use async OnClosing to await settings save without blocking
        // #2: Use int for Interlocked operations (thread-safe)
        private int isClosingHandled = 0;

        // Set immediately before the deferred Close() below. While the settings flush is
        // still awaiting, a second X-click re-enters OnClosing with isClosingHandled == 1;
        // without this flag that second entry would be indistinguishable from the deferred
        // Close() and would shut the window mid-flush (losing settings). Volatile: set on
        // a dispatcher continuation, read on the dispatcher thread.
        private volatile bool deferredCloseIssued = false;

        // This override used to call Close() re-entrantly from inside WmClose,
        // which throws InvalidOperationException (Window.VerifyNotClosing) on every X-button
        // close, and it burned the one-shot isClosingHandled flag on minimize-to-tray so the
        // SECOND X-click exited the app. Restructured: the tray decision lives here
        // (consolidated from the old MainWindow_Closing handler), the one-shot flag only
        // burns on a real exit, and the actual Close() is deferred via the dispatcher until
        // the original close callstack has unwound.
        protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (Interlocked.CompareExchange(ref isClosingHandled, 1, 0) != 0)
            {
                if (!deferredCloseIssued)
                {
                    // Second X-click while the settings flush below is still awaiting —
                    // refuse it; the first entry issues the real Close() when done.
                    e.Cancel = true;
                    return;
                }

                // Second entry = the legitimate deferred Close() below: let it proceed.
                base.OnClosing(e);
                return;
            }

            // Minimize-to-tray: hide instead of exiting. Must work on every X-click, so
            // release the one-shot flag before returning. (On tray Exit WPF is inside
            // Application.Shutdown and ignores e.Cancel — the window closes anyway and
            // the synchronous backstop flush in OnClosed saves settings.)
            if (MinimizeCheckBox.IsChecked == true)
            {
                e.Cancel = true;
                systemTrayManager?.MinimizeToTray();
                Interlocked.Exchange(ref isClosingHandled, 0);
                return;
            }

            // Real exit: cancel THIS close, flush settings without blocking the UI thread,
            // then re-issue Close() from outside the WmClose callstack.
            e.Cancel = true;

            try
            {
                // Flush pending settings BEFORE disposal.
                // Flush unconditionally — the old code only flushed when the 500ms debounce
                // timer happened to be running, and its follow-up SaveSettings() call just
                // restarted a timer that could never fire before close (dead code).
                settingsSaveTimer?.Stop();
                await SaveSettingsInternalAsync(); // NO .Wait() - async all the way
                ErrorLogger.LogMessage("OnClosing: Flushed settings on app close (async, no blocking)");
            }
            catch (Exception ex)
            {
                // OnClosed's synchronous backstop flush still runs — don't block the exit.
                ErrorLogger.LogError("OnClosing: settings flush failed", ex);
            }

            try
            {
                // Defer Close() until after OnClosing (and WmClose) has returned — calling
                // it re-entrantly throws InvalidOperationException. On the Application.Shutdown
                // path the window may already be closed by now; the inner catch absorbs that.
                deferredCloseIssued = true;
                await Dispatcher.InvokeAsync(() =>
                {
                    try { Close(); }
                    catch (Exception ex) { ErrorLogger.LogWarning($"OnClosing: deferred Close failed (window may already be closed): {ex.Message}"); }
                });
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("OnClosing: deferred close dispatch failed", ex);
            }
        }

        // Synchronous, dispatcher-free settings flush for exit paths where async
        // continuations may never run — the tray Exit menu calls Application.Current.Shutdown()
        // directly, which ignores e.Cancel and tears the dispatcher down right after the
        // windows close, so OnClosing's awaited flush can be abandoned mid-flight.
        // Idempotent with the async flush (semaphore serializes them, both write the same
        // state); a plain sync write of the settings JSON takes milliseconds — fine at exit.
        private void FlushSettingsOnExit()
        {
            // Short wait only: if an in-flight async save holds the semaphore, its
            // continuation is queued on this (now blocked) UI thread and can't finish.
            if (!saveSettingsSemaphore.Wait(TimeSpan.FromSeconds(2)))
            {
                ErrorLogger.LogWarning("FlushSettingsOnExit: timed out waiting for in-flight save - skipping backstop flush");
                return;
            }
            try
            {
                EnsureAppDataDirectoryExists();
                string settingsPath = GetSettingsPath();

                string json;
                lock (settings.SyncRoot)
                {
                    settings.MinimizeToTray = MinimizeCheckBox.IsChecked == true;
                    json = JsonSerializer.Serialize(settings, _jsonSerializerOptions);
                }

                // Same atomic temp-write + rename pattern as SaveSettingsInternalAsync
                string tempPath = settingsPath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, settingsPath, overwrite: true);

                ErrorLogger.LogMessage($"FlushSettingsOnExit: settings flushed to {settingsPath}");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("FlushSettingsOnExit", ex);
            }
            finally
            {
                saveSettingsSemaphore.Release();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Last-resort settings flush (covers tray Exit / Application.Shutdown,
            // where OnClosing's async flush may never complete). Redundant-but-harmless on the
            // normal X-button exit path where OnClosing already flushed.
            FlushSettingsOnExit();

            try { updateCheckTimer?.Dispose(); } catch (Exception ex) { ErrorLogger.LogWarning($"updateCheckTimer disposal failed: {ex.Message}"); }
            updateCheckTimer = null;

            // Dispose settingsSaveTimer to prevent settings corruption
            if (settingsSaveTimer != null)
            {
                settingsSaveTimer.Stop();
                try { (settingsSaveTimer as IDisposable)?.Dispose(); } catch (Exception ex) { ErrorLogger.LogWarning($"settingsSaveTimer disposal failed: {ex.Message}"); }
                settingsSaveTimer = null;
            }

            StopElapsedTimer();

            // Use new cleanup method for proper timer disposal
            CleanupAllTimers();

            // Clean up with proper disposal order
            try
            {
                // Stop any recording first so nothing is in flight, then detach the controller.
                if (dictation != null)
                {
                    dictation.Cancel();
                    dictation.Notice -= OnDictationNotice;
                    dictation.Dispose();
                    dictation = null;
                }

                if (hotkeyManager != null)
                {
                    hotkeyManager.HotkeyPressed -= OnHotkeyPressed;
                    hotkeyManager.HotkeyReleased -= OnHotkeyReleased;
                    hotkeyManager.PollingModeActivated -= OnPollingModeActivated;
                }

                // Dispose child windows (WPF Window resources)

                try { currentSettingsWindow?.Close(); } catch (Exception ex) { ErrorLogger.LogWarning($"Settings window close failed: {ex.Message}"); }
                currentSettingsWindow = null;


                // Now dispose services in reverse order of creation
                systemTrayManager?.Dispose();
                systemTrayManager = null;

                hotkeyManager?.Dispose();
                hotkeyManager = null;

                // SoundService removed per user request - no disposal needed

                // Cancel pending settings save operations before disposing
                try { settingsSaveCts?.Cancel(); } catch (Exception ex) { ErrorLogger.LogWarning($"settingsSaveCts cancel failed: {ex.Message}"); }
                try { settingsSaveCts?.Dispose(); } catch (Exception ex) { ErrorLogger.LogWarning($"settingsSaveCts disposal failed: {ex.Message}"); }

                // Dispose semaphore (SemaphoreSlim implements IDisposable)
                try { saveSettingsSemaphore?.Dispose(); } catch (Exception ex) { ErrorLogger.LogWarning($"saveSettingsSemaphore disposal failed: {ex.Message}"); }

                transcriptionService?.Dispose();
                transcriptionService = null;

                vadService?.Dispose();
                vadService = null;

                audioRecorder?.Dispose();
                audioRecorder = null;

                // Dispose TextInjector to release clipboard semaphore
                textInjector?.Dispose();
                textInjector = null;

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

                    // Clear ContextMenu references before clearing children to prevent GC leaks
                    foreach (var child in HistoryItemsPanel.Children.OfType<Border>())
                    {
                        child.ContextMenu = null; // Clear reference to break circular dependency
                    }

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
            return CreateCompactHistoryCard(item);
        }

        /// <summary>
        /// Creates a context menu for history items with Copy, Re-inject, and Delete actions.
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
                    // 30s clipboard hold: history copy is the documented recovery path for
                    // failed injection ("copy text from history") — the 2s auto-clear
                    // variant would wipe the text before the user could paste it.
                    TextInjector.CopyToClipboardForManualPaste(item.Text);
                    UpdateStatus("Copied to clipboard", new SolidColorBrush(StatusColors.Ready));
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TimingConstants.StatusRevertDelayMs) };
                    lock (timerLock) { activeStatusTimers.Add(timer); } // TIMER RACE FIX: Use lock for thread safety
                    EventHandler? handler = null;
                    handler = (ts, te) =>
                    {
                        UpdateStatus("Ready", new SolidColorBrush(StatusColors.Ready));
                        timer.Stop();
                        if (handler != null) timer.Tick -= handler; // Unsubscribe to prevent leak
                        lock (timerLock) { activeStatusTimers.Remove(timer); } // TIMER RACE FIX: Use lock for thread safety
                    };
                    timer.Tick += handler;
                    timer.Start();
                }
                catch (Exception ex)
                {
                    // Clipboard copy can fail when another app holds the clipboard open —
                    // surface the failure instead of silently showing nothing.
                    ErrorLogger.LogError("Copy menu item", ex);
                    UpdateStatus("Copy failed - clipboard busy", Brushes.Red);
                }
            };
            contextMenu.Items.Add(copyMenuItem);

            // Re-inject menu item
            var reinjectMenuItem = new System.Windows.Controls.MenuItem { Header = "📤 Re-inject" };
            reinjectMenuItem.Click += (s, e) =>
            {
                try
                {
                    // Use OriginalText if available (before shortcuts), otherwise use Text (already processed)
                    // This prevents double-processing shortcuts on re-injection
                    var textToProcess = item.OriginalText ?? item.Text;
                    var processedText = customShortcutService?.ProcessShortcuts(textToProcess) ?? textToProcess;
                    textInjector?.InjectText(processedText);
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Re-inject menu item", ex);
                    UpdateStatus("Paste failed", Brushes.Red);
                }
            };
            contextMenu.Items.Add(reinjectMenuItem);

            contextMenu.Items.Add(new System.Windows.Controls.Separator());

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

            // Click to copy (was missing in compact mode!)
            border.MouseLeftButtonDown += (s, e) =>
            {
                try
                {
                    // 30s clipboard hold (not 2s auto-clear) — see CreateHistoryContextMenu.
                    TextInjector.CopyToClipboardForManualPaste(item.Text);
                    UpdateStatus("Copied to clipboard", new SolidColorBrush(StatusColors.Ready));

                    // Revert to "Ready" after 1.5 seconds
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TimingConstants.StatusRevertDelayMs) };
                    lock (timerLock) { activeStatusTimers.Add(timer); } // TIMER RACE FIX: Use lock for thread safety
                    EventHandler? handler = null;
                    handler = (ts, te) =>
                    {
                        UpdateStatus("Ready", new SolidColorBrush(StatusColors.Ready));
                        timer.Stop();
                        if (handler != null) timer.Tick -= handler; // Unsubscribe to prevent leak
                        lock (timerLock) { activeStatusTimers.Remove(timer); } // TIMER RACE FIX: Use lock for thread safety
                    };
                    timer.Tick += handler;
                    timer.Start();
                }
                catch (Exception ex)
                {
                    // Clipboard copy can fail when another app holds the clipboard open —
                    // surface the failure instead of lying with "Copied to clipboard".
                    ErrorLogger.LogError("Copy history item", ex);
                    UpdateStatus("Copy failed - clipboard busy", Brushes.Red);
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

            // Attach context menu using shared helper
            border.ContextMenu = CreateHistoryContextMenu(item);
            border.Child = grid;
            return border;
        }


        /// <summary>
        /// Clears all history items.
        /// </summary>
        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will remove all transcriptions from history.\n\nContinue?",
                "Clear History",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                historyService?.ClearHistory();
                _ = UpdateHistoryUI();
                SaveSettings();

                // History cleared successfully
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
                historyService?.ClearHistory();
                _ = UpdateHistoryUI();
                SaveSettings();

                // All history cleared successfully
            }
        }

        // REMOVED: HistorySearchBox_TextChanged - now handled by ViewModel binding

        // REMOVED: HistorySearchBox_KeyDown - search feature not needed

        // REMOVED: ToggleSearchButton_Click - now handled by ViewModel command

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

        // REMOVED: ClearSearchButton_Click - now handled by ViewModel command

        /// <summary>
        /// Filters history items by search text.
        /// </summary>
        private async Task FilterHistoryBySearch(string searchText)
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    // Clear ContextMenu references before clearing children to prevent GC leaks
                    foreach (var child in HistoryItemsPanel.Children.OfType<Border>())
                    {
                        child.ContextMenu = null; // Clear reference to break circular dependency
                    }

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
                        content.AppendLine($"[{item.Timestamp:yyyy-MM-dd HH:mm:ss}]");
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

        #region Timer Management

        /// <summary>
        /// Creates and manages a status timer that will auto-dispose properly
        /// This prevents timer accumulation and memory leaks
        /// </summary>
        private void CreateStatusTimer(string message, TimeSpan duration, Brush? textColor = null, Action? onComplete = null)
        {
            Dispatcher.Invoke(() =>
            {
                // Generate unique timer ID
                var timerId = $"status_{Guid.NewGuid():N}";

                lock (timerLock)
                {
                    // Clean up any existing timers
                    foreach (var existingTimer in activeStatusTimers.ToList())
                    {
                        existingTimer.Stop();
                        activeStatusTimers.Remove(existingTimer);
                    }

                    // Update status message via ViewModel
                    statusViewModel?.UpdateStatus(message, textColor ?? Brushes.Gray);

                    // Create new timer
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = duration,
                        Tag = timerId
                    };

                    timer.Tick += (sender, e) =>
                    {
                        var t = (System.Windows.Threading.DispatcherTimer)sender!;
                        t.Stop();

                        lock (timerLock)
                        {
                            activeStatusTimers.Remove(t);
                        }

                        // Execute completion callback
                        onComplete?.Invoke();

                        // Restore normal status
                        UpdateUIForCurrentMode();
                    };

                    timer.Start();
                    activeStatusTimers.Add(timer);
                }
            });
        }

        /// <summary>
        /// Cleans up all active status timers
        /// Added IDisposable check for proper resource cleanup.
        /// Note: WPF DispatcherTimer doesn't implement IDisposable, but this is
        /// defensive coding for future-proofing if timer type changes.
        /// </summary>
        private void CleanupAllTimers()
        {
            lock (timerLock)
            {
                foreach (var timer in activeStatusTimers.ToList())
                {
                    timer.Stop();
                    // Dispose if timer implements IDisposable
                    // DispatcherTimer doesn't, but this is defensive for future changes
                    if (timer is IDisposable disposable)
                    {
                        try { disposable.Dispose(); } catch (Exception ex) { ErrorLogger.LogWarning($"Timer disposal failed: {ex.Message}"); }
                    }
                }
                activeStatusTimers.Clear();
            }
        }

        #endregion
    }
}



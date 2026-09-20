using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VoiceLite.Models;
using VoiceLite.Services;
using VoiceLite.Utilities;

namespace VoiceLite
{
    public partial class SettingsWindow : Window
    {
        // The window edits a DRAFT clone; the live instance (shared with every running
        // service) changes only when Save/Apply commits the draft, so Cancel really cancels
        // and the registered hotkey never disagrees with the in-memory setting.
        // Explicitly immediate actions bypass the draft on purpose: license activation
        // (the DPAPI key is already written when it succeeds) and translation-model install.
        private readonly Settings liveSettings;
        private readonly Settings settings; // draft
        private readonly Action? saveSettingsCallback;
        private readonly ProFeatureService proFeatureService;
        private LicenseService? licenseService;
        private bool changesCommitted;

        // The hotkey the app had registered when this dialog opened. Windows refuses a
        // second registration of a combination the process already owns, so the probe is
        // skipped for exactly this one - but only if the app really owns it: after a failed
        // startup registration (another app holds it) the saved combination is NOT ours.
        // Asked each time, not snapshotted: Settings can be opened while startup is still
        // initializing services, and the registration then succeeds behind this dialog.
        private readonly Key registeredKey;
        private readonly ModifierKeys registeredModifiers;
        private readonly Func<bool> isHotkeyRegistered;
        private bool AppOwnsRegisteredHotkey => isHotkeyRegistered();

        private const string HotkeyTakenMessage = "That key is already taken by another app. Pick a different one.";
        private const string HotkeyNotActiveMessage =
            "This hotkey is not active: another app is using it. Pick a different one, or close the other app and press Save to try again.";

        private bool isCapturingHotkey;
        private MicLevelMonitor? micMonitor;
        // Combo placeholder for a saved microphone that is not connected right now: the saved
        // index + the saved name with NotConnectedSuffix. Selecting it saves that saved choice.
        private AudioDevice? missingMic;
        private const string NotConnectedSuffix = " (not connected)";
        private ObservableCollection<CustomDictionaryEntry>? customDictionaryView;

        // The live instance — after Save/Apply it carries the committed values, so
        // MainWindow's post-dialog handling keeps working on the same object every service
        // already references.
        public Settings Settings => liveSettings;

        // Lets MainWindow apply committed changes even when the dialog is closed via
        // Cancel/X after an Apply (DialogResult alone would discard them).
        public bool ChangesCommitted => changesCommitted;

        /// <param name="hotkeyRegistered">
        /// Whether MainWindow's HotkeyManager holds the saved hotkey right now. False after a
        /// failed registration: the saved combination is then probed like any other.
        /// </param>
        public SettingsWindow(Settings currentSettings, Action? onSaveSettings = null, Func<bool>? hotkeyRegistered = null)
        {
            InitializeComponent();
            liveSettings = currentSettings ?? new Settings();
            settings = CloneForDraft(liveSettings);
            saveSettingsCallback = onSaveSettings;
            proFeatureService = new ProFeatureService(settings);
            licenseService = new LicenseService();
            registeredKey = settings.RecordHotkey;
            registeredModifiers = settings.HotkeyModifiers;
            isHotkeyRegistered = hotkeyRegistered ?? (() => true);

            LoadSettings();

            // The level meter holds the microphone open (mic-in-use indicator, Bluetooth
            // headsets drop to hands-free), so it runs only while it is on screen: the
            // content of an unselected tab is not visible. ApplicationIdle lets the tab paint
            // before the blocking device open and collapses arrowing past the tab. Showing the
            // tab twice before the queue idles queues two starts: the second must not close and
            // re-open the device the first one just opened (hence micMonitor == null).
            LevelTrack.IsVisibleChanged += (_, e) =>
            {
                if ((bool)e.NewValue)
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                        new Action(() => { if (LevelTrack.IsVisible && micMonitor == null) StartMicMonitor(); }));
                else
                    StopMicMonitor();
            };
        }

        /// <summary>
        /// Snapshot of the live settings for this dialog to edit. JSON round-trip: same
        /// serialization the app persists with, so every saved field is covered and the
        /// clone owns independent collection instances (and its own SyncRoot).
        /// Internal static for the draft-semantics regression tests.
        /// </summary>
        internal static Settings CloneForDraft(Settings source)
        {
            lock (source.SyncRoot)
            {
                var json = JsonSerializer.Serialize(source);
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
        }

        /// <summary>
        /// Copies ONLY the fields this window edits from the draft to the live instance.
        /// A whitelist, not a full overwrite: state mutated outside the dialog while it is
        /// open must survive — TranscriptionHistory grows when the user dictates over the
        /// dialog (global hotkey), IsProLicense/TranscriptionModel are written immediately.
        /// Collections are deep-copied because the draft stays editable after an Apply.
        /// Internal static for the draft-semantics regression tests.
        /// </summary>
        internal static void CommitDraft(Settings draft, Settings live)
        {
            lock (live.SyncRoot)
            {
                live.RecordHotkey = draft.RecordHotkey;
                live.HotkeyModifiers = draft.HotkeyModifiers;
                live.Mode = draft.Mode;
                live.MinimizeToTray = draft.MinimizeToTray;
                live.CheckForUpdates = draft.CheckForUpdates;
                live.TranscriptionPreset = draft.TranscriptionPreset;
                live.EnableVAD = draft.EnableVAD;
                live.TranslateToEnglish = draft.TranslateToEnglish;
                live.TranslationSourceLanguage = draft.TranslationSourceLanguage;
                live.AutoPaste = draft.AutoPaste;
                live.SelectedMicrophoneIndex = draft.SelectedMicrophoneIndex;
                live.SelectedMicrophoneName = draft.SelectedMicrophoneName;
                live.CustomShortcuts = draft.CustomShortcuts
                    .Select(s => new CustomShortcut
                    {
                        Id = s.Id,
                        Trigger = s.Trigger,
                        Replacement = s.Replacement,
                        IsEnabled = s.IsEnabled,
                        CreatedAt = s.CreatedAt,
                    })
                    .ToList();
                // Always a NEW list: TextPostProcessor caches its regex per list instance.
                live.CustomDictionary = draft.CustomDictionary
                    .Select(e => new CustomDictionaryEntry { Spoken = e.Spoken, Written = e.Written })
                    .ToList();
            }
        }

        // ================= Load =================

        private void LoadSettings()
        {
            try
            {
                HotkeyBox.Text = HotkeyDisplayHelper.Format(settings.RecordHotkey, settings.HotkeyModifiers);
                UpdateDeadHotkeyWarning();
                if (settings.Mode == RecordMode.PushToTalk) PushToTalkRadio.IsChecked = true;
                else ToggleRadio.IsChecked = true;

                LoadMicrophones();
                EnableVADCheckBox.IsChecked = settings.EnableVAD;

                switch (settings.TranscriptionPreset)
                {
                    case TranscriptionPreset.Speed: PresetSpeedRadio.IsChecked = true; break;
                    case TranscriptionPreset.Accuracy: PresetAccuracyRadio.IsChecked = true; break;
                    default: PresetBalancedRadio.IsChecked = true; break;
                }
                LoadTranslationSettings();

                AutoPasteCheckBox.IsChecked = settings.AutoPaste;
                RefreshShortcutsList();
                LoadCustomDictionary();

                LoadLicenseStatus();
                UpdateProFeatureVisibility();

                CheckForUpdatesCheckBox.IsChecked = settings.CheckForUpdates;
                MinimizeToTrayCheckBox.IsChecked = settings.MinimizeToTray;
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                if (version != null)
                    AboutVersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("SettingsWindow.LoadSettings failed", ex);
                MessageBox.Show($"Error loading settings: {ex.Message}", "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>Pro gating. Free users never see the dictionary tab or the translation options.</summary>
        private void UpdateProFeatureVisibility()
        {
            CustomDictionaryTab.Visibility = proFeatureService.CustomDictionaryTabVisibility;
            TranslationPanel.Visibility = proFeatureService.SpeechTranslationVisibility;
        }

        // ================= Save =================

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (TryCommit())
                StatusText.Text = "Settings applied.";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryCommit())
                return;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Nothing to undo — the draft is simply discarded. MainWindow checks
            // ChangesCommitted for the Apply-then-Cancel case.
            DialogResult = false;
            Close();
        }

        /// <summary>UI → draft → live (+ persist). False (and nothing committed) when validation fails.</summary>
        private bool TryCommit()
        {
            if (!CanSaveTranslationSettings())
                return false;

            if (!HotkeyIsAvailable())
            {
                // The saved hotkey was already dead when the dialog opened (startup said so and
                // offered the manual buttons): warn, but let the user save their other settings.
                // MainWindow retries the registration after every commit while it is dead.
                bool savedButDead = IsSavedButDeadHotkey(settings.RecordHotkey, settings.HotkeyModifiers,
                    registeredKey, registeredModifiers, AppOwnsRegisteredHotkey);
                ShowHotkeyProblem(savedButDead);
                if (!savedButDead)
                {
                    SettingsTabs.SelectedItem = HotkeyTab;
                    return false;
                }
            }

            ReadUiIntoDraft();
            CommitDraft(settings, liveSettings);
            changesCommitted = true;
            saveSettingsCallback?.Invoke();
            return true;
        }

        private void ReadUiIntoDraft()
        {
            // Hotkey is written to the draft as it is captured (see HotkeyBox_PreviewKeyDown).
            settings.Mode = PushToTalkRadio.IsChecked == true ? RecordMode.PushToTalk : RecordMode.Toggle;

            if (MicrophoneComboBox.SelectedItem is AudioDevice device)
                WriteMicrophoneToDraft(settings, device, missingMic);
            settings.EnableVAD = EnableVADCheckBox.IsChecked ?? true;

            settings.TranscriptionPreset =
                PresetSpeedRadio.IsChecked == true ? TranscriptionPreset.Speed :
                PresetAccuracyRadio.IsChecked == true ? TranscriptionPreset.Accuracy :
                TranscriptionPreset.Balanced;
            settings.TranslateToEnglish = TranslateToEnglishCheckBox.IsChecked ?? false;
            if (TranslationSourceLanguageComboBox.SelectedItem is ComboBoxItem lang && lang.Tag is string code)
                settings.TranslationSourceLanguage = code;

            settings.AutoPaste = AutoPasteCheckBox.IsChecked ?? true;
            // CustomShortcuts: edited in place on the draft by the Add/Edit/Delete handlers.
            CommitCustomDictionaryToDraft();

            settings.CheckForUpdates = CheckForUpdatesCheckBox.IsChecked ?? true;
            settings.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked ?? true;
        }

        // ================= Hotkey =================

        /// <summary>
        /// Probes Windows for the draft hotkey. The combination the app already owns is
        /// exempt (a probe would falsely report it taken); polled keys always pass.
        /// </summary>
        private bool HotkeyIsAvailable()
        {
            if (ProbeIsExempt(settings.RecordHotkey, settings.HotkeyModifiers, registeredKey, registeredModifiers, AppOwnsRegisteredHotkey))
                return true;
            return HotkeyManager.CanRegisterHotkey(settings.RecordHotkey, settings.HotkeyModifiers);
        }

        /// <summary>
        /// The probe is skipped only for the combination this process really holds. When the
        /// startup registration failed the app owns nothing, so the saved combination is
        /// probed too. Internal static for the regression tests.
        /// </summary>
        internal static bool ProbeIsExempt(Key draftKey, ModifierKeys draftModifiers,
            Key registeredKey, ModifierKeys registeredModifiers, bool appOwnsRegisteredHotkey)
        {
            return appOwnsRegisteredHotkey && draftKey == registeredKey && draftModifiers == registeredModifiers;
        }

        /// <summary>
        /// Esc in the hotkey box: put the hotkey that is in effect (the live, last committed
        /// one) back into the draft. Every key-down writes the draft, so without this an
        /// aborted capture - or an Alt+Tab away from the box - left a bare modifier behind for
        /// the next Save. Live rather than a focus-in snapshot: keyboard focus returns to the
        /// box on window re-activation, which would snapshot the already-polluted draft.
        /// Internal static for the regression tests.
        /// </summary>
        internal static void RevertHotkeyToLive(Settings draft, Settings live)
        {
            lock (live.SyncRoot)
            {
                draft.RecordHotkey = live.RecordHotkey;
                draft.HotkeyModifiers = live.HotkeyModifiers;
            }
        }

        /// <summary>
        /// The draft still holds the hotkey that was saved when the dialog opened, and the app
        /// failed to register it. Internal static for the regression tests.
        /// </summary>
        internal static bool IsSavedButDeadHotkey(Key draftKey, ModifierKeys draftModifiers,
            Key registeredKey, ModifierKeys registeredModifiers, bool appOwnsRegisteredHotkey)
        {
            return !appOwnsRegisteredHotkey && draftKey == registeredKey && draftModifiers == registeredModifiers;
        }

        /// <summary>Says so when the hotkey in the box is the saved one and it is not working.</summary>
        private void UpdateDeadHotkeyWarning()
        {
            if (IsSavedButDeadHotkey(settings.RecordHotkey, settings.HotkeyModifiers, registeredKey, registeredModifiers, AppOwnsRegisteredHotkey)
                && !HotkeyManager.CanRegisterHotkey(settings.RecordHotkey, settings.HotkeyModifiers))
                ShowHotkeyProblem(hotkeyIsDead: true);
            else
                HotkeyErrorText.Visibility = Visibility.Collapsed;
        }

        /// <summary>Red text under the hotkey box.</summary>
        private void ShowHotkeyProblem(bool hotkeyIsDead)
        {
            HotkeyErrorText.Text = hotkeyIsDead ? HotkeyNotActiveMessage : HotkeyTakenMessage;
            HotkeyErrorText.Visibility = Visibility.Visible;
        }

        private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e)
        {
            isCapturingHotkey = true;
            HotkeyBox.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xF8, 0xE1));
            HotkeyCaptureHint.Visibility = Visibility.Visible;
        }

        private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e)
        {
            isCapturingHotkey = false;
            HotkeyBox.Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
            HotkeyCaptureHint.Visibility = Visibility.Collapsed;
        }

        private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!isCapturingHotkey)
                return;
            e.Handled = true;

            if (e.Key == Key.Escape)
            {
                RevertHotkeyToLive(settings, liveSettings);
                HotkeyBox.Text = HotkeyDisplayHelper.Format(settings.RecordHotkey, settings.HotkeyModifiers);
                UpdateDeadHotkeyWarning();
                Keyboard.ClearFocus();
                return;
            }

            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            bool standalone = key is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl
                or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.CapsLock;

            // Standalone modifiers and CapsLock are valid hotkeys on their own (polled, not
            // RegisterHotKey'd); anything else is captured together with the held modifiers.
            settings.RecordHotkey = key;
            settings.HotkeyModifiers = standalone ? ModifierKeys.None : Keyboard.Modifiers;
            HotkeyBox.Text = HotkeyDisplayHelper.Format(settings.RecordHotkey, settings.HotkeyModifiers);
            HotkeyErrorText.Visibility = Visibility.Collapsed;
        }

        // ================= Microphone =================

        private void LoadMicrophones()
        {
            var devices = BuildMicrophoneChoices(AudioRecorder.GetAvailableMicrophones(),
                settings.SelectedMicrophoneIndex, settings.SelectedMicrophoneName,
                out var selected, out missingMic);
            MicrophoneComboBox.ItemsSource = devices;
            MicrophoneComboBox.SelectedItem = selected;
        }

        /// <summary>
        /// Finds the saved microphone among the connected devices exactly the way
        /// AudioRecorder.ResolveDeviceIndex does: -1 = default; a saved NAME wins and the index
        /// is then never consulted (Windows renumbers devices, so whatever now sits at the
        /// stale index is a different microphone); the index alone is trusted only for legacy
        /// settings that have no name. Null = not found / default.
        /// Internal static for the regression tests (which also pin parity with the recorder).
        /// </summary>
        internal static AudioDevice? PickSavedMicrophone(System.Collections.Generic.IReadOnlyList<AudioDevice> devices, int savedIndex, string? savedName)
        {
            if (savedIndex == -1)
                return null;
            if (!string.IsNullOrEmpty(savedName))
                return devices.FirstOrDefault(d => d.Index != -1 && d.Name == savedName);
            return devices.FirstOrDefault(d => d.Index == savedIndex);
        }

        /// <summary>
        /// The combo's items and pre-selection. A saved microphone that is not connected gets
        /// an explicit "(not connected)" placeholder instead of silently pre-selecting another
        /// device - the pre-selected item is what Save persists, and that is how an unplugged
        /// USB mic used to turn into "record from the webcam from now on".
        /// Internal static for the regression tests.
        /// </summary>
        internal static System.Collections.Generic.List<AudioDevice> BuildMicrophoneChoices(
            System.Collections.Generic.IReadOnlyList<AudioDevice> available, int savedIndex, string? savedName,
            out AudioDevice selected, out AudioDevice? missingPlaceholder)
        {
            var devices = available.ToList();
            devices.Insert(0, new AudioDevice { Index = -1, Name = "Default microphone" });

            missingPlaceholder = null;
            var saved = PickSavedMicrophone(devices, savedIndex, savedName);
            if (saved == null && savedIndex != -1 && !string.IsNullOrEmpty(savedName))
            {
                missingPlaceholder = new AudioDevice { Index = savedIndex, Name = savedName + NotConnectedSuffix };
                devices.Insert(1, missingPlaceholder);
            }

            selected = saved ?? missingPlaceholder ?? devices[0];
            return devices;
        }

        /// <summary>
        /// Combo selection → draft. The not-connected placeholder writes back the saved choice
        /// it stands for (saved index + the name without the suffix): the preference survives
        /// saving unrelated settings, and re-selecting the placeholder after an Apply of
        /// another device really restores it - what is visibly selected is what gets saved.
        /// Internal static for the regression tests.
        /// </summary>
        internal static void WriteMicrophoneToDraft(Settings draft, AudioDevice selected, AudioDevice? missingPlaceholder)
        {
            if (ReferenceEquals(selected, missingPlaceholder))
            {
                draft.SelectedMicrophoneIndex = selected.Index;
                draft.SelectedMicrophoneName = selected.Name[..^NotConnectedSuffix.Length];
                return;
            }
            draft.SelectedMicrophoneIndex = selected.Index;
            draft.SelectedMicrophoneName = selected.Index == -1 ? null : selected.Name;
        }

        private void RefreshMicrophonesButton_Click(object sender, RoutedEventArgs e)
        {
            // Keep the current choice across a refresh (it may not be persisted yet).
            if (MicrophoneComboBox.SelectedItem is AudioDevice current)
                WriteMicrophoneToDraft(settings, current, missingMic);
            LoadMicrophones();
            StatusText.Text = "Microphone list refreshed.";
        }

        private void MicrophoneComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                StartMicMonitor();
        }

        private void StartMicMonitor()
        {
            StopMicMonitor();
            LevelBar.Width = 0;
            MicStatusText.Visibility = Visibility.Collapsed;

            if (MicrophoneComboBox.SelectedItem is not AudioDevice device)
                return;
            if (!LevelTrack.IsVisible)
                return; // never hold the microphone open while the meter is not on screen

            // Saved mic not connected: the recorder falls back to the default device, so
            // that is what the meter shows (and the text says so).
            bool missing = ReferenceEquals(device, missingMic);
            if (missing)
                ShowMissingMicStatus();

            try
            {
                var monitor = new MicLevelMonitor(missing ? -1 : device.Index);
                monitor.LevelChanged += OnMicLevelChanged;
                monitor.Start();
                micMonitor = monitor;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning($"Mic level monitor could not open device {device.Index} ({device.Name}): {ex.Message}");
                MicStatusText.Text = "Couldn't open this microphone. It may be unplugged or in exclusive use by another app.";
                MicStatusText.Visibility = Visibility.Visible;
            }
        }

        private void StopMicMonitor()
        {
            var monitor = micMonitor;
            micMonitor = null;
            if (monitor == null) return;
            monitor.LevelChanged -= OnMicLevelChanged;
            monitor.Dispose();
        }

        private void OnMicLevelChanged(object? sender, MicLevelEventArgs e)
        {
            if (!ReferenceEquals(sender, micMonitor))
                return; // late event from a monitor that has been replaced

            LevelBar.Width = Math.Round(LevelTrack.ActualWidth * Math.Clamp(e.Peak, 0f, 1f));
            LevelBar.Background = e.Peak > 0.95f ? Brushes.OrangeRed : (Brush)FindResource("OkBrush");

            if (e.NoSignal)
            {
                MicStatusText.Text = "No signal — check that the microphone is plugged in and not muted.";
                MicStatusText.Visibility = Visibility.Visible;
            }
            else if (missingMic != null && ReferenceEquals(MicrophoneComboBox.SelectedItem, missingMic))
            {
                ShowMissingMicStatus();
            }
            else
            {
                MicStatusText.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowMissingMicStatus()
        {
            MicStatusText.Text = "This microphone isn't connected, so VoiceLite is recording from the default microphone. "
                + "It will be used again when you plug it back in, or you can pick another one here.";
            MicStatusText.Visibility = Visibility.Visible;
        }

        // ================= Translation (Pro) =================

        private void LoadTranslationSettings()
        {
            TranslateToEnglishCheckBox.IsChecked = settings.TranslateToEnglish;
            foreach (var item in TranslationSourceLanguageComboBox.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Tag?.ToString(), settings.TranslationSourceLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    TranslationSourceLanguageComboBox.SelectedItem = item;
                    break;
                }
            }
            UpdateTranslationControls();
        }

        private void TranslateToEnglishCheckBox_Changed(object sender, RoutedEventArgs e) => UpdateTranslationControls();

        private void UpdateTranslationControls()
        {
            if (TranslationOptionsPanel == null) return;
            TranslationOptionsPanel.IsEnabled = TranslateToEnglishCheckBox.IsChecked == true;

            bool installed = new TranslationModelResolverService().IsModelInstalled();
            TranslationModelStatusText.Text = installed ? "Model installed" : "Model not installed";
            TranslationModelStatusText.Foreground = installed ? (Brush)FindResource("OkBrush") : Brushes.DarkOrange;
            InstallTranslationModelButton.Visibility = installed ? Visibility.Collapsed : Visibility.Visible;
        }

        private bool CanSaveTranslationSettings()
        {
            // Free tier: the option is hidden and the runtime gate ignores it, so never
            // block saving (a hand-edited settings.json could leave it checked).
            if (!proFeatureService.IsProUser)
                return true;
            if (TranslateToEnglishCheckBox.IsChecked != true || new TranslationModelResolverService().IsModelInstalled())
                return true;

            MessageBox.Show("Install the translation model before enabling Translate to English.",
                "Translation model required", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        private void InstallTranslationModelButton_Click(object sender, RoutedEventArgs e)
        {
            // Immediate-mode: the installer writes model files to disk, nothing in the draft.
            var control = new Controls.ModelDownloadControl();
            control.ShowTranslationModelOnly();

            var window = new Window
            {
                Title = "VoiceLite — Translation Model",
                Width = 720,
                Height = 430,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Content = new ScrollViewer
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Padding = new Thickness(20),
                    Content = control,
                },
            };
            control.InstallCompleted += (_, _) =>
            {
                window.DialogResult = true;
                window.Close();
            };
            window.ShowDialog();
            UpdateTranslationControls();
        }

        // ================= Text shortcuts =================

        private void RefreshShortcutsList()
        {
            ShortcutsListView.ItemsSource = null;
            ShortcutsListView.ItemsSource = settings.CustomShortcuts;
        }

        private void ShortcutsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool hasSelection = ShortcutsListView.SelectedItem != null;
            EditShortcutButton.IsEnabled = hasSelection;
            DeleteShortcutButton.IsEnabled = hasSelection;
        }

        private void ShortcutsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ShortcutsListView.SelectedItem != null)
                EditShortcutButton_Click(sender, e);
        }

        private void AddShortcutButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ShortcutEditDialog(settings.CustomShortcuts) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                settings.CustomShortcuts.Add(dialog.Shortcut); // draft; committed on Save/Apply
                RefreshShortcutsList();
            }
        }

        private void EditShortcutButton_Click(object sender, RoutedEventArgs e)
        {
            if (ShortcutsListView.SelectedItem is not CustomShortcut selected)
                return;
            var dialog = new ShortcutEditDialog(selected, settings.CustomShortcuts) { Owner = this };
            if (dialog.ShowDialog() == true)
                RefreshShortcutsList();
        }

        private void DeleteShortcutButton_Click(object sender, RoutedEventArgs e)
        {
            if (ShortcutsListView.SelectedItem is not CustomShortcut selected)
                return;
            var result = MessageBox.Show($"Delete this shortcut?\n\n{selected.Trigger} → {selected.Replacement}",
                "Delete shortcut", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                settings.CustomShortcuts.Remove(selected);
                RefreshShortcutsList();
            }
        }

        // ================= Custom dictionary (Pro) =================

        private void LoadCustomDictionary()
        {
            // DataGrid needs INotifyCollectionChanged for inline add/remove; project the
            // persisted List into an ObservableCollection and materialize it back on save.
            customDictionaryView = new ObservableCollection<CustomDictionaryEntry>(settings.CustomDictionary);
            CustomDictionaryGrid.ItemsSource = customDictionaryView;
            customDictionaryView.CollectionChanged += (_, _) => UpdateDictionaryEntryCountText();
            UpdateDictionaryEntryCountText();
        }

        private void CommitCustomDictionaryToDraft()
        {
            if (customDictionaryView == null) return;
            // Drop rows with a blank spoken phrase (the DataGrid's placeholder row, accidental
            // empties) and trim so "  github  " behaves like "github".
            settings.CustomDictionary = customDictionaryView
                .Where(e => !string.IsNullOrWhiteSpace(e.Spoken))
                .Select(e => new CustomDictionaryEntry { Spoken = e.Spoken.Trim(), Written = e.Written ?? string.Empty })
                .ToList();
        }

        private void UpdateDictionaryEntryCountText()
        {
            if (customDictionaryView == null) return;
            int count = customDictionaryView.Count(e => !string.IsNullOrWhiteSpace(e.Spoken));
            DictionaryEntryCountText.Text = count == 1 ? "1 entry" : $"{count} entries";
        }

        private void RemoveDictionaryEntryButton_Click(object sender, RoutedEventArgs e)
        {
            if (CustomDictionaryGrid.SelectedItem is CustomDictionaryEntry selected)
                customDictionaryView?.Remove(selected);
        }

        // ================= Account / license =================

        private void LoadLicenseStatus()
        {
            if (settings.IsProLicense)
            {
                LicenseTierText.Text = "Pro";
                LicenseTierText.Foreground = (Brush)FindResource("OkBrush");
                LicenseDescriptionText.Text = "Pro is active on this computer. The custom dictionary and speech translation are unlocked.";
                LicenseKeyInput.Text = settings.LicenseKey;
                LicenseKeyInput.IsEnabled = false;
                ActivateLicenseButton.IsEnabled = false;
                LicenseStatusText.Text = "License activated";
                LicenseStatusText.Foreground = (Brush)FindResource("OkBrush");
            }
            else
            {
                LicenseTierText.Text = "Free";
                LicenseTierText.Foreground = (Brush)FindResource("AccentBrush");
                LicenseKeyInput.IsEnabled = true;
                ActivateLicenseButton.IsEnabled = true;
                LicenseStatusText.Text = string.Empty;
            }
        }

        private async void ActivateLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            var key = LicenseKeyInput.Text.Trim();
            if (string.IsNullOrEmpty(key))
            {
                ShowLicenseStatus("Enter your license key first.", error: true);
                return;
            }
            if (licenseService == null)
            {
                ShowLicenseStatus("License service is not available.", error: true);
                return;
            }

            ActivateLicenseButton.IsEnabled = false;
            ActivateLicenseButton.Content = "Checking…";
            ShowLicenseStatus("Validating license key…", error: false);

            try
            {
                var result = await licenseService.ValidateLicenseAsync(key);
                if (result.IsValid && result.Tier == "pro")
                {
                    // Immediate-mode: the DPAPI license.dat is the authoritative key store and
                    // is already written, so the LIVE settings are updated and persisted now —
                    // activation must survive Cancel. The draft copy keeps this dialog's Pro
                    // gating in sync.
                    licenseService.SaveLicenseKey(key);
                    settings.IsProLicense = true;
                    lock (liveSettings.SyncRoot)
                    {
                        liveSettings.IsProLicense = true;
                    }
                    saveSettingsCallback?.Invoke();

                    LoadLicenseStatus();
                    UpdateProFeatureVisibility();
                    UpdateTranslationControls();
                    MessageBox.Show(
                        "License activated. Pro features are unlocked.\n\nPlease restart VoiceLite for the change to take full effect.",
                        "License activated", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                ShowLicenseStatus(result.ErrorMessage ?? "Invalid license key.", error: true);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("License activation failed", ex);
                ShowLicenseStatus("Connection error. Check your internet connection and try again.", error: true);
            }

            ActivateLicenseButton.IsEnabled = true;
            ActivateLicenseButton.Content = "Activate";
        }

        private void ShowLicenseStatus(string message, bool error)
        {
            LicenseStatusText.Text = message;
            LicenseStatusText.Foreground = error ? (Brush)FindResource("DangerBrush") : (Brush)FindResource("MutedBrush");
        }

        // ================= About =================

        private void OpenLicensesFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var licensesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LICENSES");
                if (!Directory.Exists(licensesDir))
                {
                    MessageBox.Show($"LICENSES folder not found at:\n{licensesDir}", "VoiceLite", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = licensesDir, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("OpenLicensesFolderButton_Click failed", ex);
            }
        }

        private void OpenExternalLink(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = e.Uri.AbsoluteUri, UseShellExecute = true });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError($"OpenExternalLink failed for {e.Uri}", ex);
            }
        }

        // ================= Lifecycle =================

        protected override void OnClosed(EventArgs e)
        {
            StopMicMonitor();
            licenseService?.Dispose();
            licenseService = null;
            base.OnClosed(e);
        }
    }
}

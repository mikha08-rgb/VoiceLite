using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using VoiceLite.Core.Interfaces.Features;
using VoiceLite.Core.Interfaces.Services;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    /// <summary>
    /// Service wrapper for application settings
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private readonly Settings _settings;
        private readonly string _settingsPath;

        // General Settings
        public bool MinimizeToTray
        {
            get => _settings.MinimizeToTray;
            set
            {
                var oldValue = _settings.MinimizeToTray;
                _settings.MinimizeToTray = value;
                OnSettingChanged(nameof(MinimizeToTray), oldValue, value);
            }
        }

        public bool StartMinimized
        {
            get => _settings.StartMinimized;
            set
            {
                var oldValue = _settings.StartMinimized;
                _settings.StartMinimized = value;
                OnSettingChanged(nameof(StartMinimized), oldValue, value);
            }
        }

        public bool StartWithWindows
        {
            get => _settings.LaunchAtStartup;
            set
            {
                var oldValue = _settings.LaunchAtStartup;
                _settings.LaunchAtStartup = value;
                OnSettingChanged(nameof(StartWithWindows), oldValue, value);
            }
        }

        public bool CheckForUpdates
        {
            get => _settings.CheckForUpdates;
            set
            {
                var oldValue = _settings.CheckForUpdates;
                _settings.CheckForUpdates = value;
                OnSettingChanged(nameof(CheckForUpdates), oldValue, value);
            }
        }

        // AI Model Settings
        public string SelectedModel
        {
            get => _settings.SelectedModel;
            set
            {
                var oldValue = _settings.SelectedModel;
                _settings.SelectedModel = value;
                OnSettingChanged(nameof(SelectedModel), oldValue, value);
            }
        }

        public string TranscriptionLanguage
        {
            get => _settings.Language;
            set
            {
                var oldValue = _settings.Language;
                _settings.Language = value;
                OnSettingChanged(nameof(TranscriptionLanguage), oldValue, value);
            }
        }

        public bool UseGpuAcceleration
        {
            get => _settings.UseGpuAcceleration;
            set
            {
                var oldValue = _settings.UseGpuAcceleration;
                _settings.UseGpuAcceleration = value;
                OnSettingChanged(nameof(UseGpuAcceleration), oldValue, value);
            }
        }

        // Text Injection Settings
        public ITextInjector.InjectionMode InjectionMode
        {
            get => ConvertToInjectionMode(_settings.TextInjectionMode);
            set
            {
                var oldMode = _settings.TextInjectionMode;
                _settings.TextInjectionMode = ConvertFromInjectionMode(value);
                OnSettingChanged(nameof(InjectionMode), oldMode, _settings.TextInjectionMode);
            }
        }

        public int TypingDelayMs
        {
            get => _settings.TypingDelayMs;
            set
            {
                var oldValue = _settings.TypingDelayMs;
                _settings.TypingDelayMs = value;
                OnSettingChanged(nameof(TypingDelayMs), oldValue, value);
            }
        }

        public bool AddSpaceAfterInjection
        {
            get => _settings.AddSpaceAfterInjection;
            set
            {
                var oldValue = _settings.AddSpaceAfterInjection;
                _settings.AddSpaceAfterInjection = value;
                OnSettingChanged(nameof(AddSpaceAfterInjection), oldValue, value);
            }
        }

        // Hotkey Settings
        public string GlobalHotkey
        {
            get => _settings.GlobalHotkey;
            set
            {
                var oldValue = _settings.GlobalHotkey;
                _settings.GlobalHotkey = value;
                OnSettingChanged(nameof(GlobalHotkey), oldValue, value);
            }
        }

        public bool HotkeysEnabled
        {
            get => _settings.HotkeysEnabled;
            set
            {
                var oldValue = _settings.HotkeysEnabled;
                _settings.HotkeysEnabled = value;
                OnSettingChanged(nameof(HotkeysEnabled), oldValue, value);
            }
        }

        // Audio Settings
        public string AudioInputDevice
        {
            get => _settings.SelectedAudioDevice ?? "Default";
            set
            {
                var oldValue = _settings.SelectedAudioDevice;
                _settings.SelectedAudioDevice = value;
                OnSettingChanged(nameof(AudioInputDevice), oldValue, value);
            }
        }

        public int AudioSampleRate
        {
            get => 16000; // Fixed sample rate for Whisper
            set { } // Ignored - Whisper requires 16kHz
        }

        public bool PlaySoundFeedback
        {
            get => _settings.PlaySoundFeedback;
            set
            {
                var oldValue = _settings.PlaySoundFeedback;
                _settings.PlaySoundFeedback = value;
                OnSettingChanged(nameof(PlaySoundFeedback), oldValue, value);
            }
        }

        // History Settings
        public int MaxHistoryItems
        {
            get => _settings.MaxHistoryItems;
            set
            {
                var oldValue = _settings.MaxHistoryItems;
                _settings.MaxHistoryItems = value;
                OnSettingChanged(nameof(MaxHistoryItems), oldValue, value);
            }
        }

        public bool SaveHistory
        {
            get => _settings.SaveHistory;
            set
            {
                var oldValue = _settings.SaveHistory;
                _settings.SaveHistory = value;
                OnSettingChanged(nameof(SaveHistory), oldValue, value);
            }
        }

        // Events
        public event EventHandler<SettingChangedEventArgs>? SettingChanged;

        public SettingsService()
        {
            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoiceLite",
                "settings.json");

            _settings = Settings.Load();
        }

        public SettingsService(Settings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoiceLite",
                "settings.json");
        }

        public async Task LoadSettingsAsync()
        {
            await Task.Run(() =>
            {
                var loaded = Settings.Load();
                CopySettings(loaded, _settings);
            });
        }

        public async Task SaveSettingsAsync()
        {
            await Task.Run(() => _settings.Save());
        }

        public void ResetToDefaults()
        {
            var defaults = new Settings();
            CopySettings(defaults, _settings);
            _settings.Save();
        }

        public async Task ExportSettingsAsync(string filePath)
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(filePath, json);
        }

        public async Task ImportSettingsAsync(string filePath)
        {
            var json = await File.ReadAllTextAsync(filePath);
            var imported = JsonSerializer.Deserialize<Settings>(json);

            if (imported != null)
            {
                CopySettings(imported, _settings);
                _settings.Save();
            }
        }

        private void CopySettings(Settings source, Settings target)
        {
            // Copy all properties from source to target
            target.SelectedModel = source.SelectedModel;
            target.Language = source.Language;
            target.BeamSize = source.BeamSize;
            target.BestOf = source.BestOf;
            target.Threads = source.Threads;
            target.MinimizeToTray = source.MinimizeToTray;
            target.StartMinimized = source.StartMinimized;
            target.LaunchAtStartup = source.LaunchAtStartup;
            target.CheckForUpdates = source.CheckForUpdates;
            target.UseGpuAcceleration = source.UseGpuAcceleration;
            target.TypingDelayMs = source.TypingDelayMs;
            target.AddSpaceAfterInjection = source.AddSpaceAfterInjection;
            target.GlobalHotkey = source.GlobalHotkey;
            target.HotkeysEnabled = source.HotkeysEnabled;
            target.SelectedAudioDevice = source.SelectedAudioDevice;
            target.PlaySoundFeedback = source.PlaySoundFeedback;
            target.MaxHistoryItems = source.MaxHistoryItems;
            target.SaveHistory = source.SaveHistory;
            target.TextInjectionMode = source.TextInjectionMode;
            target.LicenseKey = source.LicenseKey;
            target.ActivationCount = source.ActivationCount;
        }

        private ITextInjector.InjectionMode ConvertToInjectionMode(TextInjectionMode mode)
        {
            return mode switch
            {
                TextInjectionMode.AlwaysType => ITextInjector.InjectionMode.Type,
                TextInjectionMode.AlwaysPaste => ITextInjector.InjectionMode.Paste,
                _ => ITextInjector.InjectionMode.SmartAuto
            };
        }

        private TextInjectionMode ConvertFromInjectionMode(ITextInjector.InjectionMode mode)
        {
            return mode switch
            {
                ITextInjector.InjectionMode.Type => TextInjectionMode.AlwaysType,
                ITextInjector.InjectionMode.Paste => TextInjectionMode.AlwaysPaste,
                _ => TextInjectionMode.SmartAuto
            };
        }

        private void OnSettingChanged(string settingName, object oldValue, object newValue)
        {
            if (!Equals(oldValue, newValue))
            {
                SettingChanged?.Invoke(this, new SettingChangedEventArgs
                {
                    SettingName = settingName,
                    OldValue = oldValue,
                    NewValue = newValue
                });

                // Auto-save on change
                _settings.Save();
            }
        }
    }
}
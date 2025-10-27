using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace VoiceLite.Models
{
    public enum RecordMode
    {
        PushToTalk,
        Toggle
    }

    public enum TextInjectionMode
    {
        SmartAuto,      // Automatically choose based on text length and context
        AlwaysType,     // Always use keyboard typing (most compatible)
        AlwaysPaste,    // Always use clipboard paste (fastest)
        PreferType,     // Type for short text, paste for long
        PreferPaste     // Paste when possible, type when necessary
    }

    public enum UIPreset
    {
        Default,        // Hybrid baseline - clean and balanced
        Compact         // Power user - maximum density
    }

    public enum TranscriptionPreset
    {
        Speed,          // Ultra-fast transcription (beam_size=1, all speed optimizations) - Current performance
        Balanced,       // Good balance of speed and accuracy (beam_size=3, moderate optimizations) - Recommended
        Accuracy        // Best transcription quality (beam_size=5, minimal optimizations) - Slower but more accurate
    }

    public class WhisperPresetConfig
    {
        public int BeamSize { get; set; }
        public int BestOf { get; set; }
        public double EntropyThreshold { get; set; }
        public bool UseFallback { get; set; }
        public int MaxContext { get; set; }
        public bool UseFlashAttention { get; set; }
        public string Description { get; set; } = string.Empty;

        public static WhisperPresetConfig GetPresetConfig(TranscriptionPreset preset)
        {
            return preset switch
            {
                TranscriptionPreset.Speed => new WhisperPresetConfig
                {
                    BeamSize = 1,
                    BestOf = 1,
                    EntropyThreshold = 3.0,
                    UseFallback = false,
                    MaxContext = 64,
                    UseFlashAttention = true,
                    Description = "Fastest transcription - Good for quick notes and commands"
                },
                TranscriptionPreset.Balanced => new WhisperPresetConfig
                {
                    BeamSize = 3,
                    BestOf = 2,
                    EntropyThreshold = 2.5,
                    UseFallback = true,
                    MaxContext = 224,
                    UseFlashAttention = true,
                    Description = "Balanced speed and accuracy - Recommended for most users"
                },
                TranscriptionPreset.Accuracy => new WhisperPresetConfig
                {
                    BeamSize = 5,           // Higher beam search for better quality
                    BestOf = 5,             // More candidates for best result (increased from 3)
                    EntropyThreshold = 2.4, // Default Whisper value for better stability
                    UseFallback = true,     // Enable temperature fallback for difficult audio
                    MaxContext = -1,        // Use all available context (no limit)
                    UseFlashAttention = true,
                    Description = "Highest quality - Professional transcription (recommended default)"
                },
                _ => GetPresetConfig(TranscriptionPreset.Balanced)
            };
        }
    }

    public class Settings
    {
        // THREAD SAFETY: Lock object for synchronizing access from multiple threads
        // Use this when reading/writing settings from different threads
        public readonly object SyncRoot = new object();

        private RecordMode _mode = RecordMode.PushToTalk;
        private TextInjectionMode _textInjectionMode = TextInjectionMode.SmartAuto;
        private Key _recordHotkey = Key.Z; // Default hotkey: Shift+Z
        private ModifierKeys _hotkeyModifiers = ModifierKeys.Shift;
        private string _whisperModel = "ggml-tiny.bin"; // MVP default - fastest model, bundled with installer
        private double _whisperTimeoutMultiplier = 2.0;
        private TranscriptionPreset _transcriptionPreset = TranscriptionPreset.Balanced; // Default to balanced for good speed/accuracy tradeoff
        private bool _enableVAD = true; // CRITICAL: Enable VAD by default for better accuracy and noise filtering
        private double _vadThreshold = 0.5; // Default VAD threshold (0.0-1.0) - calibrated for balanced sensitivity

        public RecordMode Mode
        {
            get => _mode;
            set => _mode = Enum.IsDefined(typeof(RecordMode), value) ? value : RecordMode.PushToTalk;
        }

        public TextInjectionMode TextInjectionMode
        {
            get => _textInjectionMode;
            set => _textInjectionMode = Enum.IsDefined(typeof(TextInjectionMode), value) ? value : TextInjectionMode.SmartAuto;
        }

        public Key RecordHotkey
        {
            get => _recordHotkey;
            set => _recordHotkey = Enum.IsDefined(typeof(Key), value) ? value : Key.Z;
        }

        public ModifierKeys HotkeyModifiers
        {
            get => _hotkeyModifiers;
            set => _hotkeyModifiers = value;
        }

        public string WhisperModel
        {
            get => _whisperModel;
            set => _whisperModel = string.IsNullOrWhiteSpace(value) ? "ggml-tiny.bin" : value;
        }

        public TranscriptionPreset TranscriptionPreset
        {
            get => _transcriptionPreset;
            set => _transcriptionPreset = Enum.IsDefined(typeof(TranscriptionPreset), value) ? value : TranscriptionPreset.Balanced;
        }

        // UPDATED v1.2.0: BeamSize and BestOf are now driven by TranscriptionPreset
        // Users can choose Speed/Balanced/Accuracy presets in Settings UI
        // This provides better UX than exposing raw Whisper parameters
        public int BeamSize => WhisperPresetConfig.GetPresetConfig(_transcriptionPreset).BeamSize;
        public int BestOf => WhisperPresetConfig.GetPresetConfig(_transcriptionPreset).BestOf;

        public double WhisperTimeoutMultiplier
        {
            get => _whisperTimeoutMultiplier;
            set => _whisperTimeoutMultiplier = Math.Clamp(value, 0.5, 10.0);
        }

        public bool EnableVAD
        {
            get => _enableVAD;
            set => _enableVAD = value;
        }

        public double VADThreshold
        {
            get => _vadThreshold;
            set => _vadThreshold = Math.Clamp(value, 0.0, 1.0);
        }

        public bool ShowTrayIcon { get; set; } = true;
        public bool MinimizeToTray { get; set; } = true;
        public bool StartMinimized { get; set; } = false;
        public bool LaunchAtStartup { get; set; } = false;
        public bool CloseToTray { get; set; } = false;
        public bool CheckForUpdates { get; set; } = true;
        public string SelectedModel { get; set; } = "ggml-tiny.bin";
        public string Language { get; set; } = "en";
        public int SelectedMicrophoneIndex { get; set; } = -1; // -1 = default device
        public string? SelectedMicrophoneName { get; set; }
        public bool AutoPaste { get; set; } = true; // Auto-paste after transcription (default enabled)

        // Additional properties for SettingsService compatibility
        public bool UseGpu { get; set; } = false;
        public bool UseGpuAcceleration { get; set; } = false;
        public int TypingDelay { get; set; } = 10;
        public int TypingDelayMs { get; set; } = 10;
        public bool AddSpaceAfter { get; set; } = false;
        public bool AddSpaceAfterInjection { get; set; } = false;
        public Key HotkeyKey { get; set; } = Key.LeftAlt;
        public string GlobalHotkey { get; set; } = "Alt";
        public bool HotkeyEnabled { get; set; } = true;
        public bool HotkeysEnabled { get; set; } = true;
        public string InputDevice { get; set; } = "Default";
        public string SelectedAudioDevice { get; set; } = "Default";
        public int SampleRate { get; set; } = 16000;
        public bool PlaySoundFeedback { get; set; } = false;
        public bool SaveHistory { get; set; } = true;
        public int ActivationCount { get; set; } = 0;

        // Transcription History
        private int _maxHistoryItems = 50;

        // BUG FIX (BUG-016 + BUG-003): Reduced cap from 1000 to 250 to prevent memory bloat
        // 250 items = ~500KB-1MB memory footprint (reasonable for 24/7 usage)
        // 1000 items = ~2-5MB memory + slow UI rendering with large lists
        public int MaxHistoryItems
        {
            get => _maxHistoryItems;
            set => _maxHistoryItems = Math.Clamp(value, 1, 250); // BUG-003 FIX: Reduced from 1000 to 250
        }
        public bool EnableHistory { get; set; } = true; // Allow users to disable history tracking
        public List<TranscriptionHistoryItem> TranscriptionHistory { get; set; } = new List<TranscriptionHistoryItem>();

        // History Display Preferences
        public bool ShowHistoryPanel { get; set; } = true; // Toggle history panel visibility
        public bool HistoryShowWordCount { get; set; } = true; // Show word count in history items
        public bool HistoryShowTimestamp { get; set; } = true; // Show timestamp in history items
        public double HistoryPanelWidth { get; set; } = 280; // Remember panel width

        // UI Preset (Appearance) - Hardcoded to Compact
        public UIPreset UIPreset => UIPreset.Compact;

        // License Management
        public string? LicenseKey { get; set; } = null; // Pro license key
        public bool IsProLicense { get; set; } = false; // True if Pro license is activated

        // Performance Settings
        // CRITICAL: Always capped at 4 threads to prevent CPU thrashing (see v1.1.2 performance fix)
        public int Threads { get; set; } = 4; // Fixed optimal thread count

        // Static methods for loading/saving settings
        public static Settings Load()
        {
            // This is a placeholder - actual implementation would load from JSON
            return new Settings();
        }

        public void Save()
        {
            // This is a placeholder - actual implementation would save to JSON
        }

        public void Reload()
        {
            // This is a placeholder - actual implementation would reload from JSON
        }
    }

    public static class SettingsValidator
    {
        public static Settings ValidateAndRepair(Settings? settings)
        {
            if (settings == null)
                return new Settings();

            // Validate language code (top 30 most popular languages supported by Whisper)
            var validLanguages = new HashSet<string> {
                "en", "zh", "de", "es", "ru", "ko", "fr", "ja", "pt", "tr",
                "pl", "ca", "nl", "ar", "sv", "it", "id", "hi", "fi", "vi",
                "he", "uk", "el", "ms", "cs", "ro", "da", "hu", "ta", "no"
            };
            if (!validLanguages.Contains(settings.Language))
            {
                settings.Language = "en";
            }

            // Validate microphone index
            if (settings.SelectedMicrophoneIndex < -1)
            {
                settings.SelectedMicrophoneIndex = -1;
            }

            // CRITICAL FIX (v1.1.5): Cap Threads at 4 to prevent CPU thrashing
            // Issue: Settings persisted Threads=8 causing severe performance degradation
            // This ensures even if settings.json has a bad value, we cap it
            if (settings.Threads > 4)
            {
                settings.Threads = 4;
            }
            else if (settings.Threads < 1)
            {
                settings.Threads = 4;
            }

            // v1.1.6: BeamSize and BestOf are now hard-coded properties (always return 1)
            // No need to validate/reset since they can't be changed via JSON deserialization
            // Old settings.json files with BeamSize/BestOf will be ignored on load

            // Force re-validation by triggering property setters
            settings.WhisperTimeoutMultiplier = settings.WhisperTimeoutMultiplier;

            return settings;
        }
    }
}

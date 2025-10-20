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

    public class Settings
    {
        // THREAD SAFETY: Lock object for synchronizing access from multiple threads
        // Use this when reading/writing settings from different threads
        public readonly object SyncRoot = new object();

        private RecordMode _mode = RecordMode.PushToTalk;
        private TextInjectionMode _textInjectionMode = TextInjectionMode.SmartAuto;
        private Key _recordHotkey = Key.LeftAlt; // Default hotkey for recording
        private ModifierKeys _hotkeyModifiers = ModifierKeys.None;
        private string _whisperModel = "ggml-tiny.bin"; // Default to Tiny model (free tier, pre-installed)
        private int _beamSize = 1; // PERFORMANCE: Changed from 5 to 1 for 5x faster transcription (greedy decoding)
        private int _bestOf = 1;   // PERFORMANCE: Changed from 5 to 1 for 5x faster transcription (single sampling)
        private double _whisperTimeoutMultiplier = 2.0;

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
            set => _recordHotkey = Enum.IsDefined(typeof(Key), value) ? value : Key.LeftAlt;
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

        public int BeamSize
        {
            get => _beamSize;
            set => _beamSize = Math.Clamp(value, 1, 10);
        }

        public int BestOf
        {
            get => _bestOf;
            set => _bestOf = Math.Clamp(value, 1, 10);
        }

        public double WhisperTimeoutMultiplier
        {
            get => _whisperTimeoutMultiplier;
            set => _whisperTimeoutMultiplier = Math.Clamp(value, 0.5, 10.0);
        }
        // Audio enhancement settings (disabled - kept for compatibility)
        public bool EnableNoiseSuppression { get; set; } = false;
        public bool EnableAutomaticGain { get; set; } = false;

        private float _targetRmsLevel = 0.2f;
        public float TargetRmsLevel
        {
            get => _targetRmsLevel;
            set => _targetRmsLevel = Math.Clamp(value, 0.05f, 0.95f);
        }

        private double _noiseGateThreshold = 0.005;
        public double NoiseGateThreshold
        {
            get => _noiseGateThreshold;
            set => _noiseGateThreshold = Math.Clamp(value, 0.001, 0.5);
        }

        public bool StartWithWindows { get; set; } = false;
        public bool ShowTrayIcon { get; set; } = true;
        public bool MinimizeToTray { get; set; } = true;
        public string Language { get; set; } = "en";
        public int SelectedMicrophoneIndex { get; set; } = -1; // -1 = default device
        public string? SelectedMicrophoneName { get; set; }
        public bool AutoPaste { get; set; } = true; // Auto-paste after transcription (default enabled)

        // Whisper settings
        public bool UseTemperatureOptimization { get; set; } = true; // Use temperature 0.2 for better accuracy
        private float _whisperTemperature = 0.2f;

        public float WhisperTemperature
        {
            get => _whisperTemperature;
            set => _whisperTemperature = Math.Clamp(value, 0.0f, 2.0f);
        }

        public bool UseVAD { get; set; } = true; // Use Voice Activity Detection to trim silence

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

        // First-Run Experience
        public bool HasSeenWelcomeDialog { get; set; } = false; // Show welcome dialog on first launch
        public bool HasSeenFirstRunDiagnostics { get; set; } = false; // Show first-run diagnostic window after installation

        // UI Preset (Appearance) - Hardcoded to Compact
        public UIPreset UIPreset => UIPreset.Compact;
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

            // Force re-validation by triggering property setters
            settings.BeamSize = settings.BeamSize;
            settings.BestOf = settings.BestOf;
            settings.WhisperTimeoutMultiplier = settings.WhisperTimeoutMultiplier;
            settings.WhisperTemperature = settings.WhisperTemperature;
            settings.TargetRmsLevel = settings.TargetRmsLevel;
            settings.NoiseGateThreshold = settings.NoiseGateThreshold;

            return settings;
        }
    }
}

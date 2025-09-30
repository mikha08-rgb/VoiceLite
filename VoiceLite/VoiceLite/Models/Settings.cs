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

    public class Settings
    {
        private RecordMode _mode = RecordMode.PushToTalk;
        private TextInjectionMode _textInjectionMode = TextInjectionMode.SmartAuto;
        private Key _recordHotkey = Key.LeftAlt;
        private ModifierKeys _hotkeyModifiers = ModifierKeys.None;
        private string _whisperModel = "tiny"; // Changed from ggml-small.bin to tiny (free tier default)
        private int _beamSize = 5;
        private int _bestOf = 5;
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
            set => _whisperModel = string.IsNullOrWhiteSpace(value) ? "tiny" : value;
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
        public bool EnableNoiseSuppression { get; set; } = false;
        public bool EnableAutomaticGain { get; set; } = false;
        private float _targetRmsLevel = 0.2f;
        private double _noiseGateThreshold = 0.02;

        public float TargetRmsLevel
        {
            get => _targetRmsLevel;
            set => _targetRmsLevel = Math.Clamp(value, 0.0f, 1.0f);
        }

        public double NoiseGateThreshold
        {
            get => _noiseGateThreshold;
            set => _noiseGateThreshold = Math.Clamp(value, 0.0, 1.0);
        }

        public bool StartWithWindows { get; set; } = false;
        public bool ShowTrayIcon { get; set; } = true;
        public bool MinimizeToTray { get; set; } = true;
        public bool PlaySoundFeedback { get; set; } = false;
        public bool ShowVisualIndicator { get; set; } = true;
        public string Language { get; set; } = "en";
        public int SelectedMicrophoneIndex { get; set; } = -1; // -1 = default device
        public string? SelectedMicrophoneName { get; set; }
        public bool AutoPaste { get; set; } = true; // Auto-paste after transcription (default enabled)

        // Accuracy Improvement Features - ALWAYS ON for best experience
        public bool UseTemperatureOptimization { get; set; } = true; // Use temperature 0.2 for better accuracy
        private float _whisperTemperature = 0.2f;

        public float WhisperTemperature
        {
            get => _whisperTemperature;
            set => _whisperTemperature = Math.Clamp(value, 0.0f, 2.0f);
        }

        // Optimal for accuracy (default is 1.0)
        public bool UseContextPrompt { get; set; } = true; // Use recent transcriptions as context
        public bool UseEnhancedDictionary { get; set; } = true; // Use enhanced technical term corrections
        public bool UseVAD { get; set; } = true; // Use Voice Activity Detection to trim silence
        public bool EnableMetrics { get; set; } = false; // Track accuracy metrics (off by default for privacy)

        // Model Benchmark Cache
        public string? LastBenchmarkDate { get; set; }
        public Dictionary<string, ModelBenchmarkCache> BenchmarkCache { get; set; } = new Dictionary<string, ModelBenchmarkCache>();
        public bool ShowModelComparison { get; set; } = true; // Show visual comparison by default
        public bool PrioritizeSpeed { get; set; } = false; // For model recommendations

        // License properties
        public string? LicenseKey { get; set; }
        public bool IsProVersion => !string.IsNullOrEmpty(LicenseKey) && LicenseKey.StartsWith("PRO-2024-");
    }

    public class ModelBenchmarkCache
    {
        public double TranscriptionTime { get; set; }
        public double ProcessingRatio { get; set; }
        public long PeakMemoryUsage { get; set; }
        public DateTime CacheDate { get; set; }
        public bool IsValid => CacheDate != default && (DateTime.Now - CacheDate).TotalDays < 30; // Cache valid for 30 days
    }

    public static class SettingsValidator
    {
        public static Settings ValidateAndRepair(Settings? settings)
        {
            if (settings == null)
                return new Settings();

            // Validate language code
            var validLanguages = new HashSet<string> { "en", "de", "es", "fr", "it", "ja", "ko", "pt", "ru", "zh" };
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

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

    public enum TranscriptionPreset
    {
        Speed,          // Ultra-fast transcription (beam_size=1, all speed optimizations)
        Balanced,       // Good balance of speed and accuracy (beam_size=3) - Recommended
        Accuracy        // Best transcription quality (beam_size=5)
    }

    public class WhisperPresetConfig
    {
        public int BeamSize { get; set; }
        public double EntropyThreshold { get; set; }
        public bool UseFallback { get; set; }
        public int MaxContext { get; set; }
        public string Description { get; set; } = string.Empty;

        public static WhisperPresetConfig GetPresetConfig(TranscriptionPreset preset)
        {
            return preset switch
            {
                TranscriptionPreset.Speed => new WhisperPresetConfig
                {
                    BeamSize = 1,
                    EntropyThreshold = 3.0,
                    UseFallback = false,
                    MaxContext = 64,
                    Description = "Fastest transcription - Good for quick notes and commands"
                },
                TranscriptionPreset.Balanced => new WhisperPresetConfig
                {
                    BeamSize = 3,
                    EntropyThreshold = 2.5,
                    UseFallback = true,
                    MaxContext = 224,
                    Description = "Balanced speed and accuracy - Recommended for most users"
                },
                TranscriptionPreset.Accuracy => new WhisperPresetConfig
                {
                    BeamSize = 5,
                    EntropyThreshold = 2.3,
                    UseFallback = true,
                    MaxContext = -1,
                    Description = "Dragon-level quality - Professional dictation (99% accuracy)"
                },
                _ => GetPresetConfig(TranscriptionPreset.Balanced)
            };
        }
    }

    public class Settings
    {
        // Lock object for synchronizing access from multiple threads.
        public readonly object SyncRoot = new object();

        private RecordMode _mode = RecordMode.Toggle;
        private Key _recordHotkey = Key.Z;
        private ModifierKeys _hotkeyModifiers = ModifierKeys.Shift;
        private string _whisperModel = "ggml-base.bin";
        private TranscriptionPreset _transcriptionPreset = TranscriptionPreset.Balanced;
        private bool _enableVAD = true;

        public RecordMode Mode
        {
            get => _mode;
            set => _mode = Enum.IsDefined(typeof(RecordMode), value) ? value : RecordMode.PushToTalk;
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
            set => _whisperModel = string.IsNullOrWhiteSpace(value) ? "ggml-base.bin" : value;
        }

        public TranscriptionPreset TranscriptionPreset
        {
            get => _transcriptionPreset;
            set => _transcriptionPreset = Enum.IsDefined(typeof(TranscriptionPreset), value) ? value : TranscriptionPreset.Balanced;
        }

        // BeamSize is driven by TranscriptionPreset (Speed/Balanced/Accuracy)
        public int BeamSize => WhisperPresetConfig.GetPresetConfig(_transcriptionPreset).BeamSize;

        public bool EnableVAD
        {
            get => _enableVAD;
            set => _enableVAD = value;
        }

        public bool MinimizeToTray { get; set; } = true;
        public string Language { get; set; } = "en";
        public int SelectedMicrophoneIndex { get; set; } = -1; // -1 = default device
        public string? SelectedMicrophoneName { get; set; }
        public bool AutoPaste { get; set; } = true;

        // Clinical pilot opt-out: when false, transcriptions are never written to TranscriptionHistory
        // (and therefore never persisted to settings.json). Defaults true to match current behavior;
        // a site that previously set this false in settings.json keeps that preference on upgrade.
        public bool EnableHistory { get; set; } = true;

        public List<TranscriptionHistoryItem> TranscriptionHistory { get; set; } = new List<TranscriptionHistoryItem>();
        public List<CustomShortcut> CustomShortcuts { get; set; } = new List<CustomShortcut>();

        public string? LicenseKey { get; set; } = null;
        public bool IsProLicense { get; set; } = false;
    }

    public static class SettingsValidator
    {
        public static Settings ValidateAndRepair(Settings? settings)
        {
            if (settings == null)
                return new Settings();

            // Validate language code (top 30 most popular languages supported by Whisper + auto-detect)
            var validLanguages = new HashSet<string>
            {
                "auto", "en", "zh", "de", "es", "ru", "ko", "fr", "ja", "pt", "tr",
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

            return settings;
        }
    }
}

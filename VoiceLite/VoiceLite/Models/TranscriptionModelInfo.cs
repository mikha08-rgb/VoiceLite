using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace VoiceLite.Models
{
    /// <summary>
    /// Single-engine model registry for the Parakeet lineup.
    /// (Named WhisperModelInfo — the 5-tier Whisper registry — until the 2026-07-17 rename.)
    /// </summary>
    public class TranscriptionModelInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string FileSizeDisplay => FormatFileSize(FileSizeBytes);
        public int SpeedRating { get; set; } // 1-5 (5 = fastest)
        public int AccuracyRating { get; set; } // 1-5 (5 = most accurate)
        public double SpeedRatingWidth => (SpeedRating / 5.0) * 150;
        public double AccuracyRatingWidth => (AccuracyRating / 5.0) * 150;
        public Visibility IsRecommended { get; set; }
        public double TypicalProcessingTime { get; set; } // seconds per audio second
        public double RequiredRAMGB { get; set; }
        public bool IsInstalled { get; set; }
        public List<string> Pros { get; set; } = new List<string>();
        public List<string> Cons { get; set; } = new List<string>();
        public string Description { get; set; } = string.Empty;
        public bool SupportsMultilingual { get; set; }

        // Canonical id for the single Parakeet entry.
        public const string ParakeetId = "parakeet-tdt-0.6b-v3-int8";

        // Legacy GGML filenames previous versions persisted in settings.json.
        // Used by SettingsMigration to detect upgrade paths.
        public static readonly IReadOnlyList<string> LegacyGgmlFileNames = new[]
        {
            "ggml-tiny.bin",
            "ggml-base.bin",
            "ggml-small.bin",
            "ggml-medium.bin",
            "ggml-large-v3-turbo-q8_0.bin",
            "ggml-large-v3.bin",
        };

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.#} {sizes[order]}";
        }

        /// <summary>
        /// Returns the display name for a model id. Legacy GGML filenames are tagged "(legacy)"
        /// — they should never appear post-migration but rendering them gracefully avoids crashes.
        /// </summary>
        public static string GetDisplayName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "Unknown";

            var lower = fileName.ToLower();
            if (lower == ParakeetId)
                return "Parakeet v3";

            if (LegacyGgmlFileNames.Any(n => n.Equals(lower, StringComparison.OrdinalIgnoreCase)))
                return "(legacy)";

            return "Unknown";
        }

        /// <summary>
        /// Returns the single-entry model registry. Install detection probes the standard
        /// Parakeet model directory under <paramref name="basePath"/>.
        /// </summary>
        public static List<TranscriptionModelInfo> GetAvailableModels(string basePath)
        {
            var parakeet = new TranscriptionModelInfo
            {
                FileName = ParakeetId,
                DisplayName = "Parakeet v3",
                FileSizeBytes = 640L * 1024 * 1024, // ~640MB int8 quantized
                SpeedRating = 4,
                AccuracyRating = 5,
                TypicalProcessingTime = 0.15,
                RequiredRAMGB = 2.0,
                Description = "NVIDIA Parakeet TDT v3 — multilingual transducer ASR (25 European languages).",
                SupportsMultilingual = true,
                IsRecommended = Visibility.Visible,
                Pros = new List<string>
                {
                    "Beats Whisper Large v3 on Open ASR Leaderboard (6.34% avg WER)",
                    "Transducer architecture — no hallucinations on silence",
                    "2–3x faster than Whisper Large on CPU",
                    "Multilingual: 25 European languages",
                },
                Cons = new List<string>
                {
                    "640MB download on first launch",
                    "2GB+ RAM recommended",
                },
            };

            // IsInstalled checks the Parakeet directory under the supplied base path.
            // Probes both ./models/parakeet-v3 and ./whisper/parakeet-v3 for installer-folder compat.
            parakeet.IsInstalled = IsParakeetInstalledAt(basePath);

            return new List<TranscriptionModelInfo> { parakeet };
        }

        private static bool IsParakeetInstalledAt(string basePath)
        {
            if (string.IsNullOrWhiteSpace(basePath))
                return false;

            string[] candidates =
            {
                Path.Combine(basePath, "parakeet-v3"),
                Path.Combine(basePath, "..", "models", "parakeet-v3"),
            };

            foreach (var dir in candidates)
            {
                if (Directory.Exists(dir)
                    && File.Exists(Path.Combine(dir, "encoder.int8.onnx"))
                    && File.Exists(Path.Combine(dir, "decoder.int8.onnx"))
                    && File.Exists(Path.Combine(dir, "joiner.int8.onnx"))
                    && File.Exists(Path.Combine(dir, "tokens.txt")))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Single-model lineup means this always returns the same entry.
        /// Kept for call-site compatibility with the previous RAM/speed advisor.
        /// </summary>
        public static TranscriptionModelInfo? GetRecommendedModel(double availableRAMGB, bool prioritizeSpeed = false)
        {
            return GetAvailableModels(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models"))
                .FirstOrDefault();
        }
    }
}

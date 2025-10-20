using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace VoiceLite.Models
{
    public class WhisperModelInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string FileSizeDisplay => FormatFileSize(FileSizeBytes);
        public int SpeedRating { get; set; } // 1-5 (5 = fastest)
        public int AccuracyRating { get; set; } // 1-5 (5 = most accurate)
        public double SpeedRatingWidth => (SpeedRating / 5.0) * 150; // Convert to width
        public double AccuracyRatingWidth => (AccuracyRating / 5.0) * 150; // Convert to width
        public Visibility IsRecommended { get; set; } // Changed to Visibility type
        public bool IsSelected { get; set; } // Indicates if this model is currently selected
        public double TypicalProcessingTime { get; set; } // seconds per audio second
        public double RequiredRAMGB { get; set; }
        public bool IsInstalled { get; set; }
        public List<string> Pros { get; set; } = new List<string>();
        public List<string> Cons { get; set; } = new List<string>();
        public string Description { get; set; } = string.Empty;
        public bool SupportsMultilingual { get; set; }

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
        /// Gets the display name for a model file name (e.g., "ggml-small.bin" → "Pro")
        /// </summary>
        public static string GetDisplayName(string fileName)
        {
            return fileName?.ToLower() switch
            {
                "ggml-tiny.bin" => "Lite",
                "ggml-base.bin" => "Swift",
                "ggml-small.bin" => "Pro",
                "ggml-medium.bin" => "Elite",
                "ggml-large-v3.bin" => "Ultra",
                _ => "Unknown"
            };
        }

        public static List<WhisperModelInfo> GetAvailableModels(string whisperPath)
        {
            var models = new List<WhisperModelInfo>
            {
                new WhisperModelInfo
                {
                    FileName = "ggml-base.bin",
                    DisplayName = "Swift",
                    FileSizeBytes = 142 * 1024 * 1024, // 142MB
                    SpeedRating = 4,
                    AccuracyRating = 2,
                    TypicalProcessingTime = 0.2,
                    RequiredRAMGB = 1.0,
                    Description = "Fast model with improved accuracy",
                    SupportsMultilingual = false,
                    IsRecommended = Visibility.Collapsed,
                    Pros = new List<string>
                    {
                        "Fast processing",
                        "Good for most casual use",
                        "Better with common terms",
                        "Low resource usage"
                    },
                    Cons = new List<string>
                    {
                        "Limited technical vocabulary",
                        "English only",
                        "May miss complex phrases"
                    }
                },
                new WhisperModelInfo
                {
                    FileName = "ggml-small.bin",
                    DisplayName = "Pro",
                    FileSizeBytes = 466 * 1024 * 1024, // 466MB
                    SpeedRating = 3,
                    AccuracyRating = 3,
                    TypicalProcessingTime = 0.4,
                    RequiredRAMGB = 2.0,
                    Description = "Balanced model - recommended for most users",
                    SupportsMultilingual = true,
                    IsRecommended = Visibility.Visible,
                    Pros = new List<string>
                    {
                        "Good balance of speed/accuracy",
                        "Handles technical terms well",
                        "Supports multiple languages",
                        "Reliable for coding dictation"
                    },
                    Cons = new List<string>
                    {
                        "Moderate processing time",
                        "2GB+ RAM recommended"
                    }
                },
                new WhisperModelInfo
                {
                    FileName = "ggml-medium.bin",
                    DisplayName = "Elite",
                    FileSizeBytes = 1500L * 1024 * 1024, // 1.5GB
                    SpeedRating = 2,
                    AccuracyRating = 4,
                    TypicalProcessingTime = 0.8,
                    RequiredRAMGB = 3.0,
                    Description = "High accuracy model for demanding tasks",
                    SupportsMultilingual = true,
                    IsRecommended = Visibility.Collapsed,
                    Pros = new List<string>
                    {
                        "High accuracy",
                        "Excellent with technical vocabulary",
                        "Great multilingual support",
                        "Handles accents well"
                    },
                    Cons = new List<string>
                    {
                        "Slower processing",
                        "3GB+ RAM required",
                        "Large download size"
                    }
                },
                new WhisperModelInfo
                {
                    FileName = "ggml-large-v3.bin",
                    DisplayName = "Ultra",
                    FileSizeBytes = 2900L * 1024 * 1024, // 2.9GB
                    SpeedRating = 1,
                    AccuracyRating = 5,
                    TypicalProcessingTime = 1.5,
                    RequiredRAMGB = 5.0,
                    Description = "State-of-the-art accuracy, latest Whisper model",
                    SupportsMultilingual = true,
                    IsRecommended = Visibility.Collapsed,
                    Pros = new List<string>
                    {
                        "Highest possible accuracy",
                        "Best-in-class performance",
                        "Excellent for all languages",
                        "Handles complex audio well"
                    },
                    Cons = new List<string>
                    {
                        "Very slow processing",
                        "5GB+ RAM required",
                        "Large storage requirement"
                    }
                }
            };

            // Check which models are actually installed
            foreach (var model in models)
            {
                var modelPath = Path.Combine(whisperPath, model.FileName);
                model.IsInstalled = File.Exists(modelPath);
            }

            return models;
        }

        public static WhisperModelInfo? GetRecommendedModel(double availableRAMGB, bool prioritizeSpeed = false)
        {
            var models = GetAvailableModels(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper"));

            if (prioritizeSpeed)
            {
                // For speed priority, pick fastest model that fits in RAM
                if (availableRAMGB < 2.0)
                    return models.Find(m => m.FileName == "ggml-base.bin") ?? models.FirstOrDefault();
                else
                    return models.Find(m => m.FileName == "ggml-small.bin") ?? models.FirstOrDefault();
            }
            else
            {
                // For accuracy priority, pick best model that fits in RAM
                if (availableRAMGB >= 5.0)
                    return models.Find(m => m.FileName == "ggml-large-v3.bin") ?? models.LastOrDefault();
                else if (availableRAMGB >= 3.0)
                    return models.Find(m => m.FileName == "ggml-medium.bin") ?? models.LastOrDefault();
                else if (availableRAMGB >= 2.0)
                    return models.Find(m => m.FileName == "ggml-small.bin") ?? models.FirstOrDefault();
                else
                    return models.Find(m => m.FileName == "ggml-base.bin") ?? models.FirstOrDefault();
            }
        }
    }
}
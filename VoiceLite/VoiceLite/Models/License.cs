using System;
using System.Collections.Generic;

namespace VoiceLite.Models
{
    /// <summary>
    /// Represents a VoiceLite license with feature flags.
    /// Returned by the license validation API.
    /// </summary>
    public class License
    {
        public bool Valid { get; set; }
        public string Status { get; set; } = "FREE"; // "ACTIVE", "EXPIRED", "CANCELED", "INVALID", "FREE"
        public string? Type { get; set; } // "SUBSCRIPTION" or "LIFETIME"
        public List<string> Features { get; set; } = new List<string>();
        public DateTime? ExpiresAt { get; set; }
        public string? Error { get; set; }

        /// <summary>
        /// Check if a specific feature is enabled.
        /// </summary>
        public bool HasFeature(string feature)
        {
            return Valid && Features.Contains(feature);
        }

        /// <summary>
        /// Check if user has Pro tier (all models unlocked).
        /// </summary>
        public bool IsPro => HasFeature("all_models");

        /// <summary>
        /// Create a free tier license (no features).
        /// </summary>
        public static License Free()
        {
            return new License
            {
                Valid = false,
                Status = "FREE",
                Features = new List<string>()
            };
        }
    }
}

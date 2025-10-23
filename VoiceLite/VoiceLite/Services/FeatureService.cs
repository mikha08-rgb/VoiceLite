using System;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    /// <summary>
    /// Centralized service for checking feature availability based on license.
    /// All Pro feature checks should go through this service.
    /// </summary>
    public class FeatureService
    {
        private readonly Settings settings;

        public FeatureService(Settings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Check if a specific feature is enabled for the current license.
        /// </summary>
        public bool IsEnabled(string featureName)
        {
            if (string.IsNullOrWhiteSpace(featureName))
                return false;

            // Free tier has no features
            if (string.IsNullOrEmpty(settings.LicenseKey))
                return false;

            // Check if feature is in the license features list
            return settings.LicenseFeatures.Contains(featureName);
        }

        /// <summary>
        /// Check if user has Pro tier (all models unlocked).
        /// </summary>
        public bool IsPro => IsEnabled("all_models");

        /// <summary>
        /// Check if user has specific Pro features.
        /// These are helper methods for common feature checks.
        /// </summary>
        public bool HasCloudSync => IsEnabled("cloud_sync");
        public bool HasVoiceShortcuts => IsEnabled("voice_shortcuts");
        public bool HasAdvancedFormatting => IsEnabled("advanced_formatting");

        /// <summary>
        /// Get user-friendly license status text.
        /// </summary>
        public string GetLicenseStatusText()
        {
            if (string.IsNullOrEmpty(settings.LicenseKey))
                return "Free - Lite model only";

            switch (settings.LicenseStatus)
            {
                case "ACTIVE":
                    return IsPro ? "Pro - All features unlocked" : "Active";
                case "EXPIRED":
                    return "Expired - Please renew your subscription";
                case "CANCELED":
                    return "Canceled";
                case "INVALID":
                    return "Invalid license";
                default:
                    return "Free - Lite model only";
            }
        }

        /// <summary>
        /// Check if license needs revalidation (older than 24 hours).
        /// </summary>
        public bool NeedsRevalidation()
        {
            if (!settings.LicenseLastValidated.HasValue)
                return true;

            return (DateTime.UtcNow - settings.LicenseLastValidated.Value).TotalHours >= 24;
        }
    }
}

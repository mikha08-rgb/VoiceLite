using System;

namespace VoiceLite.Services
{
    /// <summary>
    /// Centralized feature gating system for Free vs Pro tiers.
    /// Makes it easy to add new Pro-exclusive features in the future.
    /// </summary>
    public static class FeatureGate
    {
        /// <summary>
        /// Check if a specific Pro feature is enabled for the current user.
        /// Returns true if user has Pro license, false otherwise.
        /// </summary>
        /// <param name="featureName">Name of the feature to check (e.g., "base_model", "model_selector_ui")</param>
        /// <returns>True if feature is enabled, false if requires Pro license</returns>
        public static bool IsProFeatureEnabled(string featureName)
        {
            bool isPro = SimpleLicenseStorage.IsProVersion();

            return featureName switch
            {
                // AI Model Features
                "base_model" => isPro,      // Base model (142MB) - Pro only
                "small_model" => isPro,     // Small model (466MB) - Pro only
                "medium_model" => isPro,    // Medium model (1.5GB) - Pro only
                "large_model" => isPro,     // Large model (2.9GB) - Pro only
                "model_selector_ui" => isPro, // Model selection UI in settings - Pro only

                // Future Pro Features (add here as needed):
                // "custom_hotkeys" => isPro,        // Multiple hotkey support
                // "cloud_sync" => isPro,            // Cloud backup/sync
                // "advanced_audio" => isPro,        // Advanced audio settings
                // "batch_transcription" => isPro,   // Batch file processing
                // "custom_dictionary" => isPro,     // Custom word dictionary
                // "export_formats" => isPro,        // Export to multiple formats
                // "priority_support" => isPro,      // Priority customer support

                // Free Features (always available)
                "tiny_model" => true,        // Tiny model - always free
                "basic_recording" => true,   // Basic recording functionality
                "clipboard_copy" => true,    // Copy to clipboard
                "hotkey_support" => true,    // Single hotkey

                // Unknown features default to free (fail-open for backward compatibility)
                _ => false
            };
        }

        /// <summary>
        /// Check if user is using Pro version (has valid license).
        /// </summary>
        public static bool IsPro()
        {
            return SimpleLicenseStorage.IsProVersion();
        }

        /// <summary>
        /// Check if user is using Free version (no license).
        /// </summary>
        public static bool IsFree()
        {
            return SimpleLicenseStorage.IsFreeVersion();
        }

        /// <summary>
        /// Get human-readable feature requirement message.
        /// </summary>
        /// <param name="featureName">Feature that requires Pro</param>
        /// <returns>User-friendly message about Pro requirement</returns>
        public static string GetProRequirementMessage(string featureName)
        {
            return featureName switch
            {
                "base_model" or "small_model" or "medium_model" or "large_model" =>
                    "This AI model requires a Pro license.\n\n" +
                    "Free tier includes:\n" +
                    "• Tiny model only (80-85% accuracy)\n\n" +
                    "Pro tier unlocks:\n" +
                    "• Base model (90% accuracy)\n" +
                    "• Small model (92% accuracy)\n" +
                    "• Medium model (95% accuracy)\n" +
                    "• Large model (98% accuracy)\n\n" +
                    "Get Pro for $20 at voicelite.app",

                "model_selector_ui" =>
                    "Model selection is a Pro feature.\n\n" +
                    "Upgrade to Pro to access all 5 AI models.\n\n" +
                    "Get Pro for $20 at voicelite.app",

                _ =>
                    "This feature requires a Pro license.\n\n" +
                    "Get Pro for $20 at voicelite.app"
            };
        }
    }
}

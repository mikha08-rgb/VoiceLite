using System.Windows;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    /// <summary>
    /// Centralized service for managing Pro feature access and visibility.
    /// Adding new Pro features: Just add one property here + bind in XAML.
    /// </summary>
    public class ProFeatureService
    {
        private readonly Settings _settings;

        public ProFeatureService(Settings settings)
        {
            _settings = settings ?? throw new System.ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Core license check - true if user has activated Pro license
        /// </summary>
        public bool IsProUser => _settings.IsProLicense;

        /// <summary>
        /// Controls visibility of AI Models tab in Settings.
        /// Free users: Hidden (only Tiny model available)
        /// Pro users: Visible (can download/select all 5 models)
        /// </summary>
        public Visibility AIModelsTabVisibility => IsProUser ? Visibility.Visible : Visibility.Collapsed;

        // ============================================================
        // Future Pro Features - PLANNED BUT NOT YET IMPLEMENTED
        // When implementing: Create UI tab/button, then bind to these visibility properties
        // ============================================================

        /// <summary>
        /// Voice Shortcuts feature (Future Pro feature - NOT YET IMPLEMENTED)
        /// TODO: Implement voice command shortcuts UI for custom transcription triggers
        /// </summary>
        public Visibility VoiceShortcutsTabVisibility => IsProUser ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Export History button visibility (Future Pro feature - NOT YET IMPLEMENTED)
        /// TODO: Add export functionality for transcription history (CSV, JSON, TXT formats)
        /// </summary>
        public Visibility ExportHistoryButtonVisibility => IsProUser ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Custom Dictionary feature (Future Pro feature - NOT YET IMPLEMENTED)
        /// TODO: Allow users to add custom vocabulary/pronunciation corrections
        /// </summary>
        public Visibility CustomDictionaryTabVisibility => IsProUser ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Advanced Settings (beam size, temperature, etc.) - (Future Pro feature - NOT YET IMPLEMENTED)
        /// TODO: Expose Whisper advanced parameters for fine-tuning accuracy vs speed
        /// </summary>
        public Visibility AdvancedSettingsVisibility => IsProUser ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Checks if user can use a specific Whisper model.
        /// Free tier: Only Tiny model (ggml-tiny.bin)
        /// Pro tier: All 5 models (Tiny, Base, Small, Medium, Large)
        /// </summary>
        /// <param name="modelFileName">Model file name (e.g., "ggml-small.bin")</param>
        /// <returns>True if user can use this model</returns>
        public bool CanUseModel(string modelFileName)
        {
            if (IsProUser)
                return true; // Pro users can use any model

            // Free tier: Only Tiny model
            return modelFileName?.ToLower() == "ggml-tiny.bin";
        }

        /// <summary>
        /// Gets upgrade message for a specific feature.
        /// Used in tooltips and error messages.
        /// </summary>
        /// <param name="featureName">Name of the Pro feature</param>
        /// <returns>User-friendly upgrade message</returns>
        public string GetUpgradeMessage(string featureName)
        {
            return $"{featureName} is a Pro feature. Upgrade to VoiceLite Pro for just $20 (one-time payment)!";
        }

        /// <summary>
        /// Gets the user's current tier display name
        /// </summary>
        public string TierName => IsProUser ? "Pro ⭐" : "Free";

        /// <summary>
        /// Gets a description of the current tier
        /// </summary>
        public string TierDescription => IsProUser
            ? "Pro tier unlocked! You have access to all 5 AI models and future Pro features."
            : "Free tier includes the Tiny model (80-85% accuracy). Upgrade to Pro for all 5 models and 90-98% accuracy.";
    }
}

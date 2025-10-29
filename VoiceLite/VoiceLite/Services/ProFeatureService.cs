using System.Windows;
using VoiceLite.Models;
using VoiceLite.Core.Interfaces.Features;

namespace VoiceLite.Services
{
    /// <summary>
    /// Centralized service for managing Pro feature access and visibility.
    /// Adding new Pro features: Just add one property here + bind in XAML.
    /// </summary>
    public class ProFeatureService : IProFeatureService
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
        /// Always visible for everyone.
        /// Free users: Only see Base model (bundled with installer)
        /// Pro users: See all 5 models (Tiny, Base, Small, Medium, Large)
        /// </summary>
        public Visibility AIModelsTabVisibility => Visibility.Visible;

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

        // Additional interface properties (aliases for consistency)
        public Visibility ExportHistoryVisibility => ExportHistoryButtonVisibility;
        public Visibility CustomDictionaryVisibility => CustomDictionaryTabVisibility;

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
        /// Free tier: Base only (bundled with installer)
        /// Pro tier: All 5 models (Tiny, Base, Small, Medium, Large)
        /// </summary>
        /// <param name="modelFileName">Model file name (e.g., "ggml-small.bin")</param>
        /// <returns>True if user can use this model</returns>
        public bool CanUseModel(string modelFileName)
        {
            if (IsProUser)
                return true; // Pro users can use any model

            // Free tier: Base only
            var lowerFileName = modelFileName?.ToLower();
            return lowerFileName == "ggml-base.bin";
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
            : "Free tier includes Base model (85-90% accuracy). Upgrade to Pro for all 5 models and up to 98% accuracy.";

        #region IProFeatureService Methods

        /// <summary>
        /// Checks if a specific model is available
        /// </summary>
        public bool IsModelAvailable(string modelName)
        {
            if (IsProUser)
                return true; // Pro users can use all models

            // Free users can only use base model
            var lowerName = modelName?.ToLower();
            return lowerName?.Contains("base") == true;
        }

        /// <summary>
        /// Gets the list of available models based on license status
        /// </summary>
        public string[] GetAvailableModels()
        {
            if (IsProUser)
            {
                return new[]
                {
                    "ggml-tiny.bin",
                    "ggml-base.bin",
                    "ggml-small.bin",
                    "ggml-medium.bin",
                    "ggml-large-v3.bin"
                };
            }
            else
            {
                return new[] { "ggml-base.bin" };
            }
        }

        /// <summary>
        /// Refreshes the Pro status from the license service
        /// </summary>
        public void RefreshProStatus()
        {
            // Force settings reload to get latest license status
            _settings.Reload();
        }

        /// <summary>
        /// Shows the upgrade prompt to the user
        /// </summary>
        public void ShowUpgradePrompt()
        {
            System.Windows.MessageBox.Show(
                "Upgrade to VoiceLite Pro for just $20 (one-time payment) to unlock:\n\n" +
                "• All 5 AI models (up to 98% accuracy)\n" +
                "• Voice shortcuts\n" +
                "• Export history\n" +
                "• Custom dictionary\n" +
                "• Priority support\n\n" +
                "Visit voicelite.app to upgrade!",
                "Upgrade to Pro",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        #endregion
    }
}

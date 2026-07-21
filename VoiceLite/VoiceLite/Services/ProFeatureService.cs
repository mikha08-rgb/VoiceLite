using System;
using System.Windows;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    /// <summary>
    /// Centralized service for managing Pro feature access and visibility.
    /// Adding new Pro features: Just add one property here + bind in XAML.
    /// </summary>
    public class ProFeatureService : IDisposable
    {
        private readonly Settings _settings;

        public ProFeatureService(Settings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        // Settings is shared by reference across the app; LicenseService mutates IsProLicense
        // directly on activation, so this property reflects the new state on the next read.
        // bool reads are atomic in .NET, so no lock is needed here.
        public bool IsProUser => _settings.IsProLicense;

        /// <summary>
        /// Controls visibility of AI Models tab in Settings.
        /// Always visible for everyone.
        /// Free users: Only see Base model (bundled with installer)
        /// Pro users: See all 5 models (Tiny, Base, Small, Medium, Large)
        /// </summary>
        public Visibility AIModelsTabVisibility => Visibility.Visible;

        // ============================================================
        // Future Pro Features - Infrastructure Ready for Implementation
        // Note: Visibility properties return Collapsed until features are implemented.
        // Architecture is in place - UI tabs/buttons can be added when ready.
        // ============================================================

        /// <summary>
        /// Voice Shortcuts feature - Reserved for future Pro feature.
        /// Visibility infrastructure ready for voice command shortcuts UI.
        /// </summary>
        public Visibility VoiceShortcutsTabVisibility => IsProUser ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Export History button - Reserved for future Pro feature.
        /// Visibility infrastructure ready for transcription history export (CSV, JSON, TXT).
        /// </summary>
        public Visibility ExportHistoryButtonVisibility => IsProUser ? Visibility.Visible : Visibility.Collapsed;

        // Additional interface properties (aliases for consistency)
        public Visibility ExportHistoryVisibility => ExportHistoryButtonVisibility;
        public Visibility CustomDictionaryVisibility => CustomDictionaryTabVisibility;

        /// <summary>
        /// Custom Dictionary feature - Reserved for future Pro feature.
        /// Visibility infrastructure ready for custom vocabulary/pronunciation corrections.
        /// </summary>
        public Visibility CustomDictionaryTabVisibility => IsProUser ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Translate to English (Canary add-on model) — Pro feature. The runtime gate
        /// lives in TranscriptionService.EffectiveTranslateToEnglish; this only controls
        /// the Settings UI.
        /// </summary>
        public Visibility SpeechTranslationVisibility => IsProUser ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Advanced Settings - Reserved for future Pro feature.
        /// Visibility infrastructure ready for transcription parameter fine-tuning (beam size, temperature).
        /// </summary>
        public Visibility AdvancedSettingsVisibility => IsProUser ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Model gating is a no-op post-Parakeet swap — single model lineup means
        /// every user gets the same engine. Kept on the seam so monetization can later
        /// re-gate (e.g., by model variant) without touching call sites.
        /// </summary>
        public bool CanUseModel(string modelFileName) => true;

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
            ? "Pro license active. Your license is preserved for upcoming Pro features."
            : "Free tier includes the full Parakeet v3 engine. Pro features coming soon.";

        #region Pro Feature Methods

        /// <summary>
        /// No-op gating post-Parakeet swap — single model lineup.
        /// </summary>
        public bool IsModelAvailable(string modelName) => true;

        /// <summary>
        /// Returns the single-entry Parakeet lineup. Method kept for call-site compatibility.
        /// </summary>
        public string[] GetAvailableModels() => new[] { "parakeet-tdt-0.6b-v3-int8" };

        /// <summary>
        /// Shows the upgrade prompt. Copy is intentionally vague — specific Pro features
        /// (Voice Shortcuts, Export History, Custom Dictionary) are not yet shipped, so we
        /// avoid promising them until they exist.
        /// </summary>
        public void ShowUpgradePrompt()
        {
            System.Windows.MessageBox.Show(
                "VoiceLite Pro is in development.\n\n" +
                "Existing Pro licenses are preserved and continue to validate. " +
                "Upcoming Pro features include productivity tools beyond the core ASR engine.\n\n" +
                "Visit voicelite.app for updates.",
                "VoiceLite Pro",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        #endregion

        public void Dispose() { /* No resources to release. */ }
    }
}

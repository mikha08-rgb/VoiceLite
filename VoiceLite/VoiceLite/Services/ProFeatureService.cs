using System;
using System.Threading;
using System.Windows;
using VoiceLite.Models;
using VoiceLite.Core.Interfaces.Features;

namespace VoiceLite.Services
{
    /// <summary>
    /// Centralized service for managing Pro feature access and visibility.
    /// Adding new Pro features: Just add one property here + bind in XAML.
    ///
    /// Thread Safety: Uses ReaderWriterLockSlim to allow concurrent reads while
    /// ensuring exclusive access during RefreshProStatus(). All property reads
    /// acquire read locks to prevent stale data during settings reload.
    /// </summary>
    public class ProFeatureService : IProFeatureService, IDisposable
    {
        private readonly Settings _settings;
        private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();
        private bool _disposed;

        public ProFeatureService(Settings settings)
        {
            _settings = settings ?? throw new System.ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Core license check - true if user has activated Pro license.
        /// Thread-safe: acquires read lock to prevent reading stale data during refresh.
        /// </summary>
        public bool IsProUser
        {
            get
            {
                _rwLock.EnterReadLock();
                try
                {
                    return _settings.IsProLicense;
                }
                finally
                {
                    _rwLock.ExitReadLock();
                }
            }
        }

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
        /// Advanced Settings - Reserved for future Pro feature.
        /// Visibility infrastructure ready for Whisper parameter fine-tuning (beam size, temperature).
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
                    "ggml-large-v3-turbo-q8_0.bin",
                    "ggml-large-v3.bin"
                };
            }
            else
            {
                return new[] { "ggml-base.bin" };
            }
        }

        /// <summary>
        /// Refreshes the Pro status from the license service.
        /// Thread-safe: uses write lock to ensure exclusive access during reload,
        /// preventing any property reads from seeing stale data.
        /// </summary>
        public void RefreshProStatus()
        {
            _rwLock.EnterWriteLock();
            try
            {
                // Force settings reload to get latest license status
                _settings.Reload();
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
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

        #region IDisposable

        /// <summary>
        /// Disposes the ReaderWriterLockSlim to prevent resource leaks.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            if (disposing)
            {
                _rwLock?.Dispose();
            }
        }

        #endregion
    }
}

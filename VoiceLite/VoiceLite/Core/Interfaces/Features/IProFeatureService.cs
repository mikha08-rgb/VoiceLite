using System.Windows;

namespace VoiceLite.Core.Interfaces.Features
{
    /// <summary>
    /// Interface for Pro feature gating
    /// </summary>
    public interface IProFeatureService
    {
        /// <summary>
        /// Gets whether the user has Pro features enabled
        /// </summary>
        bool IsProUser { get; }

        /// <summary>
        /// Gets visibility for the AI Models tab
        /// </summary>
        Visibility AIModelsTabVisibility { get; }

        /// <summary>
        /// Gets visibility for Voice Shortcuts (future feature)
        /// </summary>
        Visibility VoiceShortcutsTabVisibility { get; }

        /// <summary>
        /// Gets visibility for Export History (future feature)
        /// </summary>
        Visibility ExportHistoryVisibility { get; }

        /// <summary>
        /// Gets visibility for Custom Dictionary (future feature)
        /// </summary>
        Visibility CustomDictionaryVisibility { get; }

        /// <summary>
        /// Checks if a specific model is available
        /// </summary>
        /// <param name="modelName">Name of the model (e.g., "small", "medium", "large")</param>
        bool IsModelAvailable(string modelName);

        /// <summary>
        /// Checks if user can use a specific Whisper model by filename.
        /// SECURITY FIX (MODEL-GATE-001): Added for Pro model access control
        /// </summary>
        /// <param name="modelFileName">Model file name (e.g., "ggml-small.bin")</param>
        /// <returns>True if user can use this model</returns>
        bool CanUseModel(string modelFileName);

        /// <summary>
        /// Gets user-friendly upgrade message for a specific feature.
        /// SECURITY FIX (MODEL-GATE-001): Added for consistent messaging
        /// </summary>
        /// <param name="featureName">Name of the Pro feature</param>
        /// <returns>User-friendly upgrade message</returns>
        string GetUpgradeMessage(string featureName);

        /// <summary>
        /// Gets the list of available models based on license status
        /// </summary>
        string[] GetAvailableModels();

        /// <summary>
        /// Refreshes the Pro status from the license service
        /// </summary>
        void RefreshProStatus();

        /// <summary>
        /// Shows the upgrade prompt to the user
        /// </summary>
        void ShowUpgradePrompt();
    }
}
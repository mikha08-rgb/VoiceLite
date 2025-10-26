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
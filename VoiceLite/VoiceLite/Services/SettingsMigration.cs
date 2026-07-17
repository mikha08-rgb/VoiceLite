using System;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    /// <summary>
    /// One-shot migration applied after settings are deserialized at startup.
    /// Rewrites legacy Whisper GGML model identifiers to the Parakeet canonical id so
    /// the resolver can locate the new single-model lineup.
    /// </summary>
    public static class SettingsMigration
    {
        public const string ParakeetModelId = "parakeet-tdt-0.6b-v3-int8";

        /// <summary>
        /// Applies any necessary migrations to <paramref name="settings"/>. Returns
        /// true if anything was changed (caller should persist).
        /// </summary>
        public static bool Migrate(Settings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            bool changed = false;

            // Replace any persisted GGML filename (or null/empty) with the Parakeet id.
            var current = settings.TranscriptionModel?.ToLowerInvariant() ?? string.Empty;

            bool isLegacyGgml = false;
            foreach (var legacy in TranscriptionModelInfo.LegacyGgmlFileNames)
            {
                if (string.Equals(current, legacy, StringComparison.OrdinalIgnoreCase))
                {
                    isLegacyGgml = true;
                    break;
                }
            }

            if (string.IsNullOrEmpty(current) || isLegacyGgml)
            {
                if (settings.TranscriptionModel != ParakeetModelId)
                {
                    ErrorLogger.LogWarning($"SettingsMigration: TranscriptionModel '{settings.TranscriptionModel}' → '{ParakeetModelId}'");
                    settings.TranscriptionModel = ParakeetModelId;
                    changed = true;
                }
            }

            return changed;
        }
    }
}

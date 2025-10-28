using System;
using System.Linq;
using VoiceLite.Core.Interfaces.Services;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    /// <summary>
    /// Service for processing custom text expansion shortcuts.
    /// Replaces trigger phrases with their defined replacement text (case-insensitive).
    /// </summary>
    public class CustomShortcutService : ICustomShortcutService
    {
        private readonly Settings _settings;

        public CustomShortcutService(Settings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // Ensure shortcuts list is initialized
            if (_settings.CustomShortcuts == null)
            {
                _settings.CustomShortcuts = new System.Collections.Generic.List<CustomShortcut>();
            }
        }

        /// <summary>
        /// Process the input text and replace any matching shortcuts.
        /// Uses case-insensitive matching and processes shortcuts in order.
        /// </summary>
        /// <param name="text">The text to process (after Whisper transcription and post-processing)</param>
        /// <returns>Text with shortcuts replaced</returns>
        public string ProcessShortcuts(string text)
        {
            // Early exit if no text or no shortcuts
            if (string.IsNullOrWhiteSpace(text))
                return text;

            if (_settings.CustomShortcuts == null || !_settings.CustomShortcuts.Any())
                return text;

            var result = text;

            // Process each enabled shortcut
            foreach (var shortcut in _settings.CustomShortcuts.Where(s => s.IsEnabled))
            {
                // Skip invalid shortcuts
                if (string.IsNullOrWhiteSpace(shortcut.Trigger))
                    continue;

                // Case-insensitive replacement
                // Note: Using StringComparison.OrdinalIgnoreCase for performance
                result = result.Replace(
                    shortcut.Trigger,
                    shortcut.Replacement ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase
                );
            }

            return result;
        }
    }
}

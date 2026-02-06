using System;
using System.Linq;
using System.Text.RegularExpressions;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    /// <summary>
    /// Service for processing custom text expansion shortcuts.
    /// Replaces trigger phrases with their defined replacement text (case-insensitive, whole-word matching).
    /// </summary>
    public class CustomShortcutService
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
        /// Uses case-insensitive, whole-word matching to prevent partial word replacements.
        /// Thread-safe: locks on Settings.SyncRoot to prevent concurrent modification.
        /// </summary>
        /// <param name="text">The text to process (after Whisper transcription and post-processing)</param>
        /// <returns>Text with shortcuts replaced</returns>
        public string ProcessShortcuts(string text)
        {
            // Early exit if no text
            if (string.IsNullOrEmpty(text))
                return text ?? string.Empty;

            // Thread-safe read from settings
            lock (_settings.SyncRoot)
            {
                if (_settings.CustomShortcuts == null || !_settings.CustomShortcuts.Any())
                    return text;

                var result = text;

                // Process each enabled shortcut
                foreach (var shortcut in _settings.CustomShortcuts.Where(s => s.IsEnabled))
                {
                    // Skip invalid shortcuts
                    if (string.IsNullOrWhiteSpace(shortcut.Trigger))
                        continue;

                    // Use regex for whole-word matching with word boundaries
                    // \b = word boundary (matches position between word and non-word character)
                    // Regex.Escape prevents special regex characters in trigger from being interpreted
                    var pattern = $@"\b{Regex.Escape(shortcut.Trigger)}\b";

                    try
                    {
                        // Case-insensitive whole-word replacement
                        result = Regex.Replace(
                            result,
                            pattern,
                            shortcut.Replacement ?? string.Empty,
                            RegexOptions.IgnoreCase,
                            TimeSpan.FromMilliseconds(100) // Timeout to prevent catastrophic backtracking
                        );
                    }
                    catch (RegexMatchTimeoutException ex)
                    {
                        // If regex times out, skip this shortcut (safety measure)
                        ErrorLogger.LogError($"Regex timeout for shortcut: '{shortcut.Trigger}'", ex);
                        continue;
                    }
                }

                return result;
            }
        }
    }
}

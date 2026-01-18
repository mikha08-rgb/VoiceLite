using System;
using System.Collections.Generic;
using System.Linq;

namespace VoiceLite.Models
{
    public class LanguageInfo
    {
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        // MINOR-12 FIX: Static set of valid language codes for validation
        // These are the ISO 639-1 codes supported by Whisper
        private static readonly HashSet<string> ValidLanguageCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "auto", "en", "zh", "de", "es", "ru", "ko", "fr", "ja", "pt",
            "tr", "pl", "ca", "nl", "ar", "sv", "it", "id", "hi", "fi",
            "vi", "he", "uk", "el", "ms", "cs", "ro", "da", "hu", "ta", "no"
        };

        /// <summary>
        /// MINOR-12 FIX: Validates whether a language code is supported.
        /// </summary>
        /// <param name="code">The language code to validate (e.g., "en", "auto")</param>
        /// <returns>True if the code is valid and supported by Whisper</returns>
        public static bool IsValidLanguageCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;

            return ValidLanguageCodes.Contains(code.Trim());
        }

        /// <summary>
        /// MINOR-12 FIX: Gets a validated language code, returning default if invalid.
        /// </summary>
        /// <param name="code">The language code to validate</param>
        /// <param name="defaultCode">Default code to return if invalid (default: "en")</param>
        /// <returns>The validated code, or default if invalid</returns>
        public static string GetValidatedCode(string? code, string defaultCode = "en")
        {
            return IsValidLanguageCode(code) ? code!.Trim().ToLowerInvariant() : defaultCode;
        }

        public static List<LanguageInfo> GetSupportedLanguages() => new()
        {
            new() { Code = "auto", DisplayName = "Auto-detect" },
            new() { Code = "en", DisplayName = "English" },
            new() { Code = "zh", DisplayName = "Chinese" },
            new() { Code = "de", DisplayName = "German" },
            new() { Code = "es", DisplayName = "Spanish" },
            new() { Code = "ru", DisplayName = "Russian" },
            new() { Code = "ko", DisplayName = "Korean" },
            new() { Code = "fr", DisplayName = "French" },
            new() { Code = "ja", DisplayName = "Japanese" },
            new() { Code = "pt", DisplayName = "Portuguese" },
            new() { Code = "tr", DisplayName = "Turkish" },
            new() { Code = "pl", DisplayName = "Polish" },
            new() { Code = "ca", DisplayName = "Catalan" },
            new() { Code = "nl", DisplayName = "Dutch" },
            new() { Code = "ar", DisplayName = "Arabic" },
            new() { Code = "sv", DisplayName = "Swedish" },
            new() { Code = "it", DisplayName = "Italian" },
            new() { Code = "id", DisplayName = "Indonesian" },
            new() { Code = "hi", DisplayName = "Hindi" },
            new() { Code = "fi", DisplayName = "Finnish" },
            new() { Code = "vi", DisplayName = "Vietnamese" },
            new() { Code = "he", DisplayName = "Hebrew" },
            new() { Code = "uk", DisplayName = "Ukrainian" },
            new() { Code = "el", DisplayName = "Greek" },
            new() { Code = "ms", DisplayName = "Malay" },
            new() { Code = "cs", DisplayName = "Czech" },
            new() { Code = "ro", DisplayName = "Romanian" },
            new() { Code = "da", DisplayName = "Danish" },
            new() { Code = "hu", DisplayName = "Hungarian" },
            new() { Code = "ta", DisplayName = "Tamil" },
            new() { Code = "no", DisplayName = "Norwegian" }
        };
    }
}

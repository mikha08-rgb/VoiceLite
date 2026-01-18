using System.Collections.Generic;

namespace VoiceLite.Models
{
    public class LanguageInfo
    {
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

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

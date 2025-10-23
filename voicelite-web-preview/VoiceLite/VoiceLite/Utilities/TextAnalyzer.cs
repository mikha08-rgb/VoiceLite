using System;

namespace VoiceLite.Utilities
{
    /// <summary>
    /// Utility methods for analyzing and processing text
    /// </summary>
    public static class TextAnalyzer
    {
        /// <summary>
        /// Word separators used for counting words in transcriptions
        /// </summary>
        private static readonly char[] WordSeparators = { ' ', '\t', '\n', '\r' };

        /// <summary>
        /// Counts the number of words in a text string by splitting on whitespace
        /// </summary>
        /// <param name="text">Text to analyze</param>
        /// <returns>Number of words, or 0 if text is null/empty</returns>
        public static int CountWords(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            return text.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        /// <summary>
        /// Truncates text to a maximum length with ellipsis
        /// </summary>
        /// <param name="text">Text to truncate</param>
        /// <param name="maxLength">Maximum length</param>
        /// <returns>Truncated text with "..." suffix if exceeded</returns>
        public static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength) + "...";
        }
    }
}

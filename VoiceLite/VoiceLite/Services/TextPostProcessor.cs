using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace VoiceLite.Services
{
    /// <summary>
    /// Post-processes transcription text to add punctuation and proper capitalization.
    /// Uses rule-based approach optimized for English speech patterns.
    /// </summary>
    public class TextPostProcessor
    {
        // Common sentence-ending phrases that should get periods
        private static readonly HashSet<string> SentenceEnders = new(StringComparer.OrdinalIgnoreCase)
        {
            "thanks", "thank you", "goodbye", "bye", "okay", "ok", "alright", "right",
            "yes", "no", "sure", "exactly", "correct", "understood", "got it"
        };

        // Question words that indicate interrogative sentences
        private static readonly HashSet<string> QuestionWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "what", "when", "where", "who", "whom", "whose", "why", "which", "how",
            "is", "are", "can", "could", "would", "should", "will", "do", "does", "did"
        };

        // Words that should always be capitalized (common proper nouns in tech context)
        private static readonly HashSet<string> AlwaysCapitalized = new(StringComparer.OrdinalIgnoreCase)
        {
            "i", "github", "google", "microsoft", "apple", "amazon", "facebook", "twitter",
            "python", "javascript", "java", "react", "vue", "angular", "docker", "kubernetes",
            "windows", "linux", "macos", "android", "ios"
        };

        /// <summary>
        /// Processes transcription text to add punctuation and capitalization.
        /// </summary>
        /// <param name="text">Raw transcription text</param>
        /// <param name="enablePunctuation">Whether to add punctuation</param>
        /// <param name="enableCapitalization">Whether to fix capitalization</param>
        /// <returns>Processed text with punctuation and capitalization</returns>
        public static string Process(string text, bool enablePunctuation = true, bool enableCapitalization = true)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            string result = text.Trim();

            // Step 1: Add punctuation
            if (enablePunctuation)
            {
                result = AddPunctuation(result);
            }

            // Step 2: Fix capitalization
            if (enableCapitalization)
            {
                result = FixCapitalization(result);
            }

            return result;
        }

        /// <summary>
        /// Adds punctuation to text based on linguistic patterns.
        /// </summary>
        private static string AddPunctuation(string text)
        {
            // Remove existing punctuation at the end (will be re-added correctly)
            text = text.TrimEnd('.', '!', '?', ',', ';', ':');

            // Check if text is a question
            bool isQuestion = IsQuestion(text);

            // Add ending punctuation
            if (isQuestion)
            {
                text += "?";
            }
            else
            {
                text += ".";
            }

            // Add commas for better readability (simple heuristics)
            text = AddCommas(text);

            return text;
        }

        /// <summary>
        /// Determines if a sentence is a question based on linguistic patterns.
        /// </summary>
        private static bool IsQuestion(string text)
        {
            // Split into words
            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                return false;

            // Check if starts with question word
            string firstWord = words[0].ToLowerInvariant();
            if (QuestionWords.Contains(firstWord))
            {
                // Additional check: "is this", "are you", "can you", "do you" patterns
                if (words.Length >= 2)
                {
                    string secondWord = words[1].ToLowerInvariant();
                    if (firstWord is "is" or "are" or "can" or "could" or "would" or "should" or "will" or "do" or "does" or "did")
                    {
                        // These are likely questions if followed by pronouns or "this/that"
                        if (secondWord is "you" or "i" or "we" or "they" or "he" or "she" or "it" or "this" or "that" or "there")
                            return true;
                    }
                    else
                    {
                        // "what", "when", "where", "who", "why", "which", "how" are almost always questions
                        return true;
                    }
                }
            }

            // Check for rising intonation patterns (voice-to-text sometimes captures these)
            if (text.Contains("right?") || text.Contains("yeah?") || text.Contains("okay?"))
                return true;

            return false;
        }

        /// <summary>
        /// Adds commas to improve readability (simple conjunction/pause detection).
        /// </summary>
        private static string AddCommas(string text)
        {
            // Add commas before coordinating conjunctions in longer sentences
            if (text.Length > 30) // Only for longer sentences
            {
                // Pattern: " and " preceded by at least 2 words
                text = Regex.Replace(text, @"(\w+\s+\w+)\s+(and|but|or)\s+", "$1, $2 ", RegexOptions.IgnoreCase);
            }

            return text;
        }

        /// <summary>
        /// Fixes capitalization to follow English grammar rules.
        /// </summary>
        private static string FixCapitalization(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            // Split into words while preserving punctuation
            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>();

            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                string cleanWord = Regex.Replace(word, @"[^\w]", ""); // Remove punctuation for checking

                if (i == 0)
                {
                    // Always capitalize first word
                    result.Add(CapitalizeFirst(word));
                }
                else if (i > 0 && words[i - 1].EndsWith('.') || words[i - 1].EndsWith('!') || words[i - 1].EndsWith('?'))
                {
                    // Capitalize after sentence-ending punctuation
                    result.Add(CapitalizeFirst(word));
                }
                else if (AlwaysCapitalized.Contains(cleanWord))
                {
                    // Capitalize specific words (I, proper nouns, tech terms)
                    result.Add(CapitalizeWord(word, cleanWord));
                }
                else
                {
                    // Keep lowercase
                    result.Add(word.ToLowerInvariant());
                }
            }

            return string.Join(" ", result);
        }

        /// <summary>
        /// Capitalizes the first character of a word, preserving punctuation.
        /// </summary>
        private static string CapitalizeFirst(string word)
        {
            if (string.IsNullOrEmpty(word))
                return word;

            // Find first letter and capitalize it
            for (int i = 0; i < word.Length; i++)
            {
                if (char.IsLetter(word[i]))
                {
                    return word.Substring(0, i) + char.ToUpperInvariant(word[i]) + word.Substring(i + 1).ToLowerInvariant();
                }
            }

            return word;
        }

        /// <summary>
        /// Capitalizes a specific word based on its cleaned form, preserving punctuation.
        /// </summary>
        private static string CapitalizeWord(string word, string cleanWord)
        {
            // Special case for "I"
            if (cleanWord.Equals("i", StringComparison.OrdinalIgnoreCase))
            {
                return word.Replace(cleanWord, "I", StringComparison.OrdinalIgnoreCase);
            }

            // For tech terms and proper nouns, use proper casing
            foreach (var term in AlwaysCapitalized)
            {
                if (cleanWord.Equals(term, StringComparison.OrdinalIgnoreCase))
                {
                    // Apply proper capitalization (first letter uppercase, rest as-is)
                    string properCase = char.ToUpperInvariant(term[0]) + term.Substring(1);
                    return word.Replace(cleanWord, properCase, StringComparison.OrdinalIgnoreCase);
                }
            }

            return word;
        }
    }
}

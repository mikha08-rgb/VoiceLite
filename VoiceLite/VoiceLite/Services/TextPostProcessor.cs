using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    /// <summary>
    /// Post-processes transcription text to add punctuation and proper capitalization.
    /// Uses rule-based approach optimized for English speech patterns.
    ///
    /// Thread Safety: This class is thread-safe. All methods are static and operate
    /// on immutable input data. The pre-compiled Regex instances are thread-safe
    /// by design when used with instance methods like Replace() - they do not store
    /// any mutable per-call state. See: https://docs.microsoft.com/en-us/dotnet/standard/base-types/thread-safety-in-regular-expressions
    /// </summary>
    public class TextPostProcessor
    {
        // Pre-compiled regex patterns for performance (compiled once, reused many times)
        // MINOR-11 FIX: These are thread-safe for concurrent use because:
        // 1. Regex instances are immutable after construction
        // 2. Instance methods (Replace, Match, etc.) don't modify internal state
        // 3. The Compiled flag only affects performance, not thread safety
        private static readonly Regex ConjunctionCommaRegex = new Regex(
            @"(\w+\s+\w+)\s+(and|but|or)\s+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex NonWordCharRegex = new Regex(
            @"[^\w]",
            RegexOptions.Compiled);

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

        // Canonical casing for terms users frequently dictate. Applied AFTER FixCapitalization
        // to override the default "first letter only" cap with the right brand-style casing.
        // Necessary because Parakeet's transducer architecture has no equivalent of Whisper's
        // initial-prompt vocab biasing — terms like "GitHub" land as "github" without this.
        private static readonly Dictionary<string, string> DevTermDictionary = new(StringComparer.OrdinalIgnoreCase)
        {
            { "github", "GitHub" },
            { "javascript", "JavaScript" },
            { "typescript", "TypeScript" },
            { "python", "Python" },
            { "voicelite", "VoiceLite" },
            { ".net", ".NET" },
            { "node.js", "Node.js" },
            { "api", "API" },
            { "json", "JSON" },
            { "sql", "SQL" },
            { "react", "React" },
            { "c sharp", "C#" },
        };

        // Negative lookbehind/lookahead for word chars handles dotted terms like ".net"
        // and multi-word terms like "c sharp" cleanly. Sort by length descending so that
        // longer alternatives match before shorter overlapping ones.
        private static readonly Regex DevTermRegex = new Regex(
            @"(?<![A-Za-z0-9_])(" +
            string.Join("|",
                DevTermDictionary.Keys
                    .OrderByDescending(t => t.Length)
                    .Select(Regex.Escape)) +
            @")(?![A-Za-z0-9_])",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Processes transcription text to add punctuation and capitalization.
        /// </summary>
        /// <param name="text">Raw transcription text</param>
        /// <param name="enablePunctuation">Whether to add punctuation</param>
        /// <param name="enableCapitalization">Whether to fix capitalization</param>
        /// <param name="customDictionary">Optional Pro user dictionary (applied after the built-in dev-term dictionary)</param>
        /// <returns>Processed text with punctuation and capitalization</returns>
        public static string Process(
            string text,
            bool enablePunctuation = true,
            bool enableCapitalization = true,
            IReadOnlyList<CustomDictionaryEntry>? customDictionary = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            string result = text.Trim();

            // Step 1: Add punctuation
            if (enablePunctuation)
            {
                result = AddPunctuation(result);
            }

            // Step 2: Fix capitalization + apply dev-term + Pro custom dict
            if (enableCapitalization)
            {
                result = FixCapitalization(result);
                result = ApplyDevTermDictionary(result);
                result = ApplyCustomDictionary(result, customDictionary);
            }

            return result;
        }

        private static string ApplyDevTermDictionary(string text)
        {
            return DevTermRegex.Replace(text, match =>
                DevTermDictionary.TryGetValue(match.Value, out var canonical)
                    ? canonical
                    : match.Value);
        }

        // Per-entries-instance cache: the compiled regex + lookup map are rebuilt only when
        // the user mutates their dictionary (Settings replaces the List<T> reference on edit).
        // ConditionalWeakTable lets the cache entries get GC'd if the list is replaced and
        // the old reference is no longer held anywhere.
        //
        // FOOTGUN: cache invalidation depends on the caller assigning a NEW list reference
        // (settings.CustomDictionary = newList) when entries change. If a future feature
        // mutates the list in place (settings.CustomDictionary.Add(entry)), the cached
        // regex will be stale. Today only SettingsWindowNew.CommitCustomDictionaryToSettings
        // touches this collection and it always reassigns; keep that invariant.
        private static readonly ConditionalWeakTable<IReadOnlyList<CustomDictionaryEntry>, CustomDictionaryCache> CustomDictCache = new();

        private sealed class CustomDictionaryCache
        {
            public Regex Regex { get; }
            public Dictionary<string, string> Lookup { get; }

            public CustomDictionaryCache(IReadOnlyList<CustomDictionaryEntry> entries)
            {
                // De-dupe by Spoken (case-insensitive); last write wins. Skip empty keys.
                Lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.Spoken)) continue;
                    Lookup[entry.Spoken.Trim()] = entry.Written ?? string.Empty;
                }

                // Mirror DevTermRegex: word-boundary + length-desc sort for greedy match.
                Regex = new Regex(
                    @"(?<![A-Za-z0-9_])(" +
                    string.Join("|",
                        Lookup.Keys
                            .OrderByDescending(t => t.Length)
                            .Select(Regex.Escape)) +
                    @")(?![A-Za-z0-9_])",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
        }

        private static string ApplyCustomDictionary(string text, IReadOnlyList<CustomDictionaryEntry>? entries)
        {
            // Hot path for Free users (no Pro dictionary) — zero regex construction cost.
            if (entries == null || entries.Count == 0)
                return text;

            // Build cache once per list-reference; subsequent calls hit the cached compiled regex.
            var cache = CustomDictCache.GetValue(entries, key => new CustomDictionaryCache(key));

            // Empty dictionary after de-dupe (e.g., all entries had blank Spoken keys) — skip.
            if (cache.Lookup.Count == 0)
                return text;

            return cache.Regex.Replace(text, match =>
                cache.Lookup.TryGetValue(match.Value, out var written)
                    ? written
                    : match.Value);
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
                // Pattern: " and " preceded by at least 2 words - uses pre-compiled regex
                text = ConjunctionCommaRegex.Replace(text, "$1, $2 ");
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
                string cleanWord = NonWordCharRegex.Replace(word, ""); // Remove punctuation for checking (pre-compiled)

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

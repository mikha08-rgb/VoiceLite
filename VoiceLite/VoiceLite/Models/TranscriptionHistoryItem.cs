using System;
using VoiceLite.Utilities;

namespace VoiceLite.Models
{
    /// <summary>
    /// Represents a single transcription in the history panel.
    /// Stores metadata about the transcription for display and analysis.
    /// </summary>
    public class TranscriptionHistoryItem
    {
        /// <summary>
        /// Unique identifier for this history item
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// When this transcription was created
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// The transcribed text content (AFTER shortcut processing)
        /// This is what was actually injected/displayed to the user
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// The original transcription from Whisper (BEFORE shortcut processing)
        /// Used for re-injection to avoid double-processing shortcuts
        /// </summary>
        public string? OriginalText { get; set; }

        /// <summary>
        /// Number of words in the transcription (for quick metrics)
        /// </summary>
        public int WordCount { get; set; }

        /// <summary>
        /// How long the recording took (in seconds)
        /// </summary>
        public double DurationSeconds { get; set; }

        /// <summary>
        /// Which Whisper model was used (tiny, base, small, medium, large)
        /// </summary>
        public string ModelUsed { get; set; } = "tiny";

        /// <summary>
        /// Optional confidence score from Whisper (0.0 to 1.0)
        /// Future enhancement: Whisper can provide confidence scores
        /// </summary>
        public double? ConfidenceScore { get; set; }

        /// <summary>
        /// Whether this item is pinned to the top of the history list
        /// </summary>
        public bool IsPinned { get; set; } = false;

        /// <summary>
        /// Display-friendly timestamp (for UI binding)
        /// </summary>
        public string DisplayTimestamp => Timestamp.ToString("h:mm tt");

        /// <summary>
        /// Truncated preview of text for compact display
        /// </summary>
        public string PreviewText => TextAnalyzer.Truncate(Text, 100);
    }
}

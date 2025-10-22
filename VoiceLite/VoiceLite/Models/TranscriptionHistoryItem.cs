using System;

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
        /// The transcribed text content
        /// </summary>
        public string Text { get; set; } = string.Empty;

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
        /// Display-friendly timestamp (for UI binding)
        /// </summary>
        public string DisplayTimestamp => Timestamp.ToString("h:mm tt");

        /// <summary>
        /// Truncated preview of text for compact display
        /// </summary>
        public string PreviewText
        {
            get
            {
                // BUG FIX (BUG-015): Add null check to prevent NullReferenceException
                if (string.IsNullOrEmpty(Text))
                    return string.Empty;

                const int maxLength = 100;
                if (Text.Length <= maxLength)
                    return Text;
                return Text.Substring(0, maxLength) + "...";
            }
        }
    }
}

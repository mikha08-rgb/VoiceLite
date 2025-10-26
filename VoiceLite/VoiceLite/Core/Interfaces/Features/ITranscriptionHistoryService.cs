using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VoiceLite.Core.Interfaces.Features
{
    /// <summary>
    /// Interface for managing transcription history
    /// </summary>
    public interface ITranscriptionHistoryService
    {
        /// <summary>
        /// Adds a new transcription to the history
        /// </summary>
        void AddTranscription(TranscriptionItem item);

        /// <summary>
        /// Gets all transcription history items
        /// </summary>
        IEnumerable<TranscriptionItem> GetHistory();

        /// <summary>
        /// Gets history items within a date range
        /// </summary>
        IEnumerable<TranscriptionItem> GetHistoryRange(DateTime from, DateTime to);

        /// <summary>
        /// Searches history for items containing the specified text
        /// </summary>
        IEnumerable<TranscriptionItem> SearchHistory(string searchText);

        /// <summary>
        /// Clears all history items
        /// </summary>
        void ClearHistory();

        /// <summary>
        /// Removes a specific history item
        /// </summary>
        void RemoveItem(Guid itemId);

        /// <summary>
        /// Pins or unpins a history item
        /// </summary>
        void TogglePin(Guid itemId);

        /// <summary>
        /// Gets all pinned items
        /// </summary>
        IEnumerable<TranscriptionItem> GetPinnedItems();

        /// <summary>
        /// Exports history to a file
        /// </summary>
        Task ExportHistoryAsync(string filePath, ExportFormat format);

        /// <summary>
        /// Gets the maximum number of history items to keep
        /// </summary>
        int MaxHistoryItems { get; set; }

        /// <summary>
        /// Raised when a new item is added to history
        /// </summary>
        event EventHandler<TranscriptionItem> ItemAdded;
    }

    /// <summary>
    /// Represents a transcription history item
    /// </summary>
    public class TranscriptionItem
    {
        public Guid Id { get; set; }
        public string Text { get; set; }
        public DateTime Timestamp { get; set; }
        public string ModelUsed { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public bool IsPinned { get; set; }
        public string ApplicationContext { get; set; }
    }

    /// <summary>
    /// Export format options
    /// </summary>
    public enum ExportFormat
    {
        Text,
        Json,
        Csv,
        Markdown
    }
}
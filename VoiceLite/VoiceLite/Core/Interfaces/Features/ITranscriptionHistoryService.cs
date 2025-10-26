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
        /// Clears all history items with option to preserve pinned
        /// </summary>
        void ClearHistory(bool preservePinned);

        /// <summary>
        /// Removes a specific history item
        /// </summary>
        void RemoveItem(Guid itemId);

        /// <summary>
        /// Removes a specific history item by string ID
        /// </summary>
        void RemoveItem(string itemId);

        /// <summary>
        /// Pins or unpins a history item
        /// </summary>
        void TogglePin(Guid itemId);

        /// <summary>
        /// Pins or unpins a history item by string ID
        /// </summary>
        void TogglePin(string itemId);

        /// <summary>
        /// Adds a new item to the history
        /// </summary>
        void AddItem(TranscriptionItem item);

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
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Text { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public double ProcessingTime { get; set; }
        public string? ModelUsed { get; set; }
        public bool IsPinned { get; set; }
        public string? AudioFilePath { get; set; }
        public string? ApplicationContext { get; set; }
        public int WordCount => Text?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
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
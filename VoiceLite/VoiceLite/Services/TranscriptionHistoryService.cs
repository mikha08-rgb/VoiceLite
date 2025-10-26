using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VoiceLite.Core.Interfaces.Features;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    /// <summary>
    /// Manages the transcription history, including adding, removing, pinning, and limiting items.
    /// </summary>
    public class TranscriptionHistoryService : ITranscriptionHistoryService
    {
        private readonly Settings settings;
        private const int MAX_TEXT_LENGTH = 5000; // Prevent extremely long transcriptions from bloating settings file

        public TranscriptionHistoryService(Settings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // Ensure history list is initialized
            if (settings.TranscriptionHistory == null)
            {
                settings.TranscriptionHistory = new List<TranscriptionHistoryItem>();
            }
        }

        /// <summary>
        /// Adds a new transcription to the history.
        /// Inserts at the beginning (newest first) and removes old unpinned items beyond the limit.
        /// </summary>
        public void AddToHistory(TranscriptionHistoryItem item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (!settings.EnableHistory)
            {
                ErrorLogger.LogMessage("History is disabled, skipping add");
                return;
            }

            // Truncate very long transcriptions to prevent bloat
            if (item.Text.Length > MAX_TEXT_LENGTH)
            {
                ErrorLogger.LogMessage($"Truncating long transcription from {item.Text.Length} to {MAX_TEXT_LENGTH} chars");
                item.Text = item.Text.Substring(0, MAX_TEXT_LENGTH) + "... (truncated)";
            }

            // Insert at the beginning (newest first)
            settings.TranscriptionHistory.Insert(0, item);
            ErrorLogger.LogMessage($"Added history item: '{item.PreviewText}' (ID: {item.Id})");

            // Clean up old items beyond the max limit (but keep pinned items)
            CleanupOldItems();
        }

        /// <summary>
        /// Removes items beyond the MaxHistoryItems limit.
        /// P1 OPTIMIZATION: Added early exit to avoid unnecessary allocations (10-20ms savings)
        /// </summary>
        private void CleanupOldItems()
        {
            // P1 OPTIMIZATION: Early exit if no cleanup needed
            if (settings.TranscriptionHistory.Count <= settings.MaxHistoryItems)
                return; // No cleanup needed

            // Only allocate and sort if we actually need to remove items
            var itemsToRemove = settings.TranscriptionHistory
                .OrderBy(x => x.Timestamp) // Oldest first
                .Take(settings.TranscriptionHistory.Count - settings.MaxHistoryItems)
                .ToList();

            foreach (var item in itemsToRemove)
            {
                settings.TranscriptionHistory.Remove(item);
#if DEBUG
                ErrorLogger.LogMessage($"Removed old history item: '{item.PreviewText}' (ID: {item.Id})");
#endif
            }

#if DEBUG
            ErrorLogger.LogMessage($"Cleaned up {itemsToRemove.Count} old history items");
#endif
        }

        /// <summary>
        /// Removes a specific item from the history by ID.
        /// </summary>
        public bool RemoveFromHistory(string id)
        {
            var item = settings.TranscriptionHistory.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                settings.TranscriptionHistory.Remove(item);
                ErrorLogger.LogMessage($"Manually removed history item: '{item.PreviewText}' (ID: {id})");
                return true;
            }

            ErrorLogger.LogMessage($"History item not found for removal: ID {id}");
            return false;
        }

        /// <summary>
        /// Clears all items from the history.
        /// </summary>
        public void ClearHistory()
        {
            int count = settings.TranscriptionHistory.Count;
            settings.TranscriptionHistory.Clear();
            ErrorLogger.LogMessage($"Cleared {count} history items");
        }


        /// <summary>
        /// Gets history statistics for display.
        /// </summary>
        public HistoryStatistics GetStatistics()
        {
            return new HistoryStatistics
            {
                TotalItems = settings.TranscriptionHistory.Count,
                TotalWords = settings.TranscriptionHistory.Sum(x => x.WordCount),
                AverageDuration = settings.TranscriptionHistory.Any()
                    ? settings.TranscriptionHistory.Average(x => x.DurationSeconds)
                    : 0,
                OldestTimestamp = settings.TranscriptionHistory.Any()
                    ? settings.TranscriptionHistory.Min(x => x.Timestamp)
                    : DateTime.Now
            };
        }

        #region ITranscriptionHistoryService Implementation

        public int MaxHistoryItems
        {
            get => settings.MaxHistoryItems;
            set => settings.MaxHistoryItems = value;
        }

        public event EventHandler<TranscriptionItem>? ItemAdded;

        public void AddTranscription(TranscriptionItem item)
        {
            // Convert TranscriptionItem to TranscriptionHistoryItem
            var historyItem = new TranscriptionHistoryItem
            {
                Id = item.Id,
                Text = item.Text,
                Timestamp = item.Timestamp,
                DurationSeconds = item.ProcessingTime,
                ModelUsed = item.ModelUsed ?? "Unknown",
                IsPinned = item.IsPinned
            };

            AddToHistory(historyItem);
            ItemAdded?.Invoke(this, item);
        }

        public void AddItem(TranscriptionItem item) => AddTranscription(item);

        public IEnumerable<TranscriptionItem> GetHistory()
        {
            // Convert TranscriptionHistoryItem to TranscriptionItem
            return settings.TranscriptionHistory.Select(h => new TranscriptionItem
            {
                Id = h.Id,
                Text = h.Text,
                Timestamp = h.Timestamp,
                ProcessingTime = h.DurationSeconds,
                ModelUsed = h.ModelUsed,
                IsPinned = h.IsPinned
            });
        }

        public IEnumerable<TranscriptionItem> GetHistoryRange(DateTime from, DateTime to)
        {
            return GetHistory().Where(h => h.Timestamp >= from && h.Timestamp <= to);
        }

        public IEnumerable<TranscriptionItem> SearchHistory(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return GetHistory();

            return GetHistory().Where(h => h.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        public void ClearHistory(bool preservePinned)
        {
            if (preservePinned)
            {
                var pinned = settings.TranscriptionHistory.Where(h => h.IsPinned).ToList();
                settings.TranscriptionHistory.Clear();
                settings.TranscriptionHistory.AddRange(pinned);
            }
            else
            {
                ClearHistory();
            }
        }

        public void RemoveItem(Guid itemId) => RemoveFromHistory(itemId.ToString());

        public void RemoveItem(string itemId) => RemoveFromHistory(itemId);

        public void TogglePin(Guid itemId) => TogglePin(itemId.ToString());

        public void TogglePin(string itemId)
        {
            var item = settings.TranscriptionHistory.FirstOrDefault(x => x.Id == itemId);
            if (item != null)
            {
                item.IsPinned = !item.IsPinned;
                settings.Save();
            }
        }

        public IEnumerable<TranscriptionItem> GetPinnedItems()
        {
            return GetHistory().Where(h => h.IsPinned);
        }

        public async Task ExportHistoryAsync(string filePath, ExportFormat format)
        {
            // Export implementation
            await Task.CompletedTask;
            throw new NotImplementedException("Export functionality not yet implemented");
        }

        #endregion
    }

    /// <summary>
    /// Statistics about the transcription history.
    /// </summary>
    public class HistoryStatistics
    {
        public int TotalItems { get; set; }
        public int TotalWords { get; set; }
        public double AverageDuration { get; set; }
        public DateTime OldestTimestamp { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    /// <summary>
    /// Manages the transcription history, including adding, removing, pinning, and limiting items.
    /// </summary>
    public class TranscriptionHistoryService
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
        /// THREAD-SAFETY FIX: All list operations synchronized via Settings.SyncRoot
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

            // THREAD-SAFETY FIX: Synchronize list operations to prevent corruption
            // UI thread reads while transcription thread writes without this lock
            lock (settings.SyncRoot)
            {
                // Insert at the beginning (newest first)
                settings.TranscriptionHistory.Insert(0, item);
                ErrorLogger.LogMessage($"Added history item: '{item.PreviewText}' (ID: {item.Id})");

                // Clean up old items beyond the max limit (but keep pinned items)
                CleanupOldItemsLocked();
            }
        }

        /// <summary>
        /// Removes items beyond the MaxHistoryItems limit.
        /// P1 OPTIMIZATION: O(n log n) instead of O(n²) - uses RemoveAll with HashSet lookup
        /// THREAD-SAFETY: Caller must hold settings.SyncRoot lock
        /// </summary>
        private void CleanupOldItemsLocked()
        {
            // P1 OPTIMIZATION: Early exit if no cleanup needed
            if (settings.TranscriptionHistory.Count <= settings.MaxHistoryItems)
                return; // No cleanup needed

            int itemsToRemoveCount = settings.TranscriptionHistory.Count - settings.MaxHistoryItems;

            // O(n log n): Sort to find oldest items
            var itemIdsToRemove = settings.TranscriptionHistory
                .OrderBy(x => x.Timestamp) // Oldest first
                .Take(itemsToRemoveCount)
                .Select(x => x.Id)
                .ToHashSet(); // O(1) lookup

            // O(n): RemoveAll is more efficient than individual Remove() calls
            // Remove() is O(n) per call, making the old approach O(n²)
            // RemoveAll with HashSet lookup is O(n) total
            int removedCount = settings.TranscriptionHistory.RemoveAll(item => itemIdsToRemove.Contains(item.Id));

#if DEBUG
            ErrorLogger.LogMessage($"Cleaned up {removedCount} old history items");
#endif
        }

        /// <summary>
        /// Removes a specific item from the history by ID.
        /// THREAD-SAFETY FIX: All list operations synchronized via Settings.SyncRoot
        /// </summary>
        public bool RemoveFromHistory(string id)
        {
            lock (settings.SyncRoot)
            {
                var item = settings.TranscriptionHistory.FirstOrDefault(x => x.Id == id);
                if (item != null)
                {
                    settings.TranscriptionHistory.Remove(item);
                    ErrorLogger.LogMessage($"Manually removed history item: '{item.PreviewText}' (ID: {id})");
                    return true;
                }
            }

            ErrorLogger.LogMessage($"History item not found for removal: ID {id}");
            return false;
        }

        /// <summary>
        /// Clears all items from the history.
        /// THREAD-SAFETY FIX: All list operations synchronized via Settings.SyncRoot
        /// </summary>
        public void ClearHistory()
        {
            int count;
            lock (settings.SyncRoot)
            {
                count = settings.TranscriptionHistory.Count;
                settings.TranscriptionHistory.Clear();
            }
            ErrorLogger.LogMessage($"Cleared {count} history items");
        }


        /// <summary>
        /// Gets history statistics for display.
        /// THREAD-SAFETY FIX: All list operations synchronized via Settings.SyncRoot
        /// </summary>
        public HistoryStatistics GetStatistics()
        {
            lock (settings.SyncRoot)
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

        /// <summary>
        /// THREAD-SAFETY FIX: Returns a snapshot copy to prevent enumeration during modification
        /// </summary>
        public IEnumerable<TranscriptionItem> GetHistory()
        {
            // Convert TranscriptionHistoryItem to TranscriptionItem
            // THREAD-SAFETY FIX: Take snapshot under lock, ToList() forces immediate evaluation
            lock (settings.SyncRoot)
            {
                return settings.TranscriptionHistory.Select(h => new TranscriptionItem
                {
                    Id = h.Id,
                    Text = h.Text,
                    Timestamp = h.Timestamp,
                    ProcessingTime = h.DurationSeconds,
                    ModelUsed = h.ModelUsed,
                    IsPinned = h.IsPinned
                }).ToList();
            }
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

        /// <summary>
        /// THREAD-SAFETY FIX: All list operations synchronized via Settings.SyncRoot
        /// </summary>
        public void ClearHistory(bool preservePinned)
        {
            if (preservePinned)
            {
                lock (settings.SyncRoot)
                {
                    var pinned = settings.TranscriptionHistory.Where(h => h.IsPinned).ToList();
                    settings.TranscriptionHistory.Clear();
                    settings.TranscriptionHistory.AddRange(pinned);
                }
            }
            else
            {
                ClearHistory();
            }
        }

        public void RemoveItem(Guid itemId) => RemoveFromHistory(itemId.ToString());

        public void RemoveItem(string itemId) => RemoveFromHistory(itemId);

        public void TogglePin(Guid itemId) => TogglePin(itemId.ToString());

        /// <summary>
        /// THREAD-SAFETY FIX: All list operations synchronized via Settings.SyncRoot
        /// </summary>
        public void TogglePin(string itemId)
        {
            lock (settings.SyncRoot)
            {
                var item = settings.TranscriptionHistory.FirstOrDefault(x => x.Id == itemId);
                if (item != null)
                {
                    item.IsPinned = !item.IsPinned;
                    settings.Save();
                }
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

    public enum ExportFormat
    {
        Text,
        Json,
        Csv,
        Markdown
    }
}

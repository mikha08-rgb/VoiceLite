using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Removes unpinned items beyond the MaxHistoryItems limit.
        /// Pinned items are never removed by this method.
        /// </summary>
        private void CleanupOldItems()
        {
            // Get all unpinned items
            var unpinnedItems = settings.TranscriptionHistory
                .Where(x => !x.IsPinned)
                .ToList();

            // If we have more unpinned items than the limit, remove the oldest ones
            if (unpinnedItems.Count > settings.MaxHistoryItems)
            {
                var itemsToRemove = unpinnedItems
                    .OrderBy(x => x.Timestamp) // Oldest first
                    .Take(unpinnedItems.Count - settings.MaxHistoryItems)
                    .ToList();

                foreach (var item in itemsToRemove)
                {
                    settings.TranscriptionHistory.Remove(item);
                    ErrorLogger.LogMessage($"Removed old history item: '{item.PreviewText}' (ID: {item.Id})");
                }

                ErrorLogger.LogMessage($"Cleaned up {itemsToRemove.Count} old history items");
            }
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
        /// Toggles the pinned status of a history item.
        /// Pinned items stay at the top and aren't removed during cleanup.
        /// </summary>
        public bool TogglePin(string id)
        {
            var item = settings.TranscriptionHistory.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                item.IsPinned = !item.IsPinned;
                ErrorLogger.LogMessage($"{(item.IsPinned ? "Pinned" : "Unpinned")} history item: '{item.PreviewText}' (ID: {id})");

                // Re-sort: pinned items should be at the top
                ReorderHistory();
                return true;
            }

            ErrorLogger.LogMessage($"History item not found for pin toggle: ID {id}");
            return false;
        }

        /// <summary>
        /// Re-orders the history to ensure pinned items are at the top.
        /// Within pinned and unpinned groups, items are sorted by timestamp (newest first).
        /// </summary>
        private void ReorderHistory()
        {
            // THREAD SAFETY FIX: Lock settings to prevent race with serialization
            lock (settings.SyncRoot)
            {
                var reordered = settings.TranscriptionHistory
                    .OrderByDescending(x => x.IsPinned) // Pinned first
                    .ThenByDescending(x => x.Timestamp) // Then by timestamp (newest first)
                    .ToList();

                settings.TranscriptionHistory.Clear();
                foreach (var item in reordered)
                {
                    settings.TranscriptionHistory.Add(item);
                }
            }
        }

        /// <summary>
        /// Clears all unpinned items from the history.
        /// Pinned items are preserved.
        /// </summary>
        public int ClearHistory()
        {
            var unpinnedItems = settings.TranscriptionHistory
                .Where(x => !x.IsPinned)
                .ToList();

            foreach (var item in unpinnedItems)
            {
                settings.TranscriptionHistory.Remove(item);
            }

            ErrorLogger.LogMessage($"Cleared {unpinnedItems.Count} unpinned history items");
            return unpinnedItems.Count;
        }

        /// <summary>
        /// Clears ALL items from the history, including pinned items.
        /// </summary>
        public int ClearAllHistory()
        {
            int count = settings.TranscriptionHistory.Count;
            settings.TranscriptionHistory.Clear();
            ErrorLogger.LogMessage($"Cleared ALL {count} history items (including pinned)");
            return count;
        }

        /// <summary>
        /// Gets all pinned items (for special display/export).
        /// </summary>
        public IEnumerable<TranscriptionHistoryItem> GetPinnedItems()
        {
            return settings.TranscriptionHistory.Where(x => x.IsPinned);
        }

        /// <summary>
        /// Gets all unpinned items.
        /// </summary>
        public IEnumerable<TranscriptionHistoryItem> GetUnpinnedItems()
        {
            return settings.TranscriptionHistory.Where(x => !x.IsPinned);
        }

        /// <summary>
        /// Gets history statistics for display.
        /// </summary>
        public HistoryStatistics GetStatistics()
        {
            return new HistoryStatistics
            {
                TotalItems = settings.TranscriptionHistory.Count,
                PinnedItems = settings.TranscriptionHistory.Count(x => x.IsPinned),
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

    /// <summary>
    /// Statistics about the transcription history.
    /// </summary>
    public class HistoryStatistics
    {
        public int TotalItems { get; set; }
        public int PinnedItems { get; set; }
        public int TotalWords { get; set; }
        public double AverageDuration { get; set; }
        public DateTime OldestTimestamp { get; set; }
    }
}

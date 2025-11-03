using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using VoiceLite.Core.Interfaces.Features;
using VoiceLite.Models;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// Tests for TranscriptionHistoryService - history management and cleanup
    /// High priority: Validates P1 optimization (CleanupOldItems early exit) and prevents data loss
    /// </summary>
    public class TranscriptionHistoryServiceTests
    {
        private Settings CreateTestSettings()
        {
            return new Settings
            {
                EnableHistory = true,
                MaxHistoryItems = 100,
                TranscriptionHistory = new List<TranscriptionHistoryItem>()
            };
        }

        private TranscriptionHistoryItem CreateTestItem(string text = "Test transcription", bool isPinned = false)
        {
            return new TranscriptionHistoryItem
            {
                Id = Guid.NewGuid().ToString(),
                Text = text,
                Timestamp = DateTime.Now,
                DurationSeconds = 1.5,
                ModelUsed = "ggml-base.bin",
                IsPinned = isPinned
            };
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidSettings_Succeeds()
        {
            // Arrange
            var settings = CreateTestSettings();

            // Act
            var service = new TranscriptionHistoryService(settings);

            // Assert
            service.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullSettings_ThrowsArgumentNullException()
        {
            // Act
            Action act = () => new TranscriptionHistoryService(null);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("settings");
        }

        [Fact]
        public void Constructor_WithNullHistoryList_InitializesEmptyList()
        {
            // Arrange
            var settings = new Settings
            {
                EnableHistory = true,
                TranscriptionHistory = null // Null initially
            };

            // Act
            var service = new TranscriptionHistoryService(settings);

            // Assert
            settings.TranscriptionHistory.Should().NotBeNull();
            settings.TranscriptionHistory.Should().BeEmpty();
        }

        #endregion

        #region AddToHistory Tests

        [Fact]
        public void AddToHistory_ValidItem_AddsToBeginning()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            var item = CreateTestItem("First item");

            // Act
            service.AddToHistory(item);

            // Assert
            settings.TranscriptionHistory.Should().HaveCount(1);
            settings.TranscriptionHistory[0].Text.Should().Be("First item");
        }

        [Fact]
        public void AddToHistory_MultipleItems_NewestFirst()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);

            // Act
            service.AddToHistory(CreateTestItem("First"));
            service.AddToHistory(CreateTestItem("Second"));
            service.AddToHistory(CreateTestItem("Third"));

            // Assert
            settings.TranscriptionHistory.Should().HaveCount(3);
            settings.TranscriptionHistory[0].Text.Should().Be("Third", "Newest should be first");
            settings.TranscriptionHistory[1].Text.Should().Be("Second");
            settings.TranscriptionHistory[2].Text.Should().Be("First", "Oldest should be last");
        }

        [Fact]
        public void AddToHistory_NullItem_ThrowsArgumentNullException()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);

            // Act
            Action act = () => service.AddToHistory(null);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("item");
        }

        [Fact]
        public void AddToHistory_WhenHistoryDisabled_DoesNotAdd()
        {
            // Arrange
            var settings = CreateTestSettings();
            settings.EnableHistory = false;
            var service = new TranscriptionHistoryService(settings);
            var item = CreateTestItem();

            // Act
            service.AddToHistory(item);

            // Assert
            settings.TranscriptionHistory.Should().BeEmpty("History is disabled");
        }

        [Fact]
        public void AddToHistory_VeryLongText_TruncatesTo5000Chars()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            var longText = new string('A', 10000); // 10,000 characters
            var item = CreateTestItem(longText);

            // Act
            service.AddToHistory(item);

            // Assert
            var addedItem = settings.TranscriptionHistory[0];
            addedItem.Text.Length.Should().BeLessOrEqualTo(5000 + 20); // +20 for "... (truncated)"
            addedItem.Text.Should().EndWith("... (truncated)");
        }

        #endregion

        #region CleanupOldItems Tests (P1 Optimization)

        [Fact]
        public void CleanupOldItems_BelowLimit_NoCleanup()
        {
            // Arrange
            var settings = CreateTestSettings();
            settings.MaxHistoryItems = 10;
            var service = new TranscriptionHistoryService(settings);

            // Add 5 items (below limit)
            for (int i = 0; i < 5; i++)
            {
                service.AddToHistory(CreateTestItem($"Item {i}"));
            }

            // Assert
            settings.TranscriptionHistory.Should().HaveCount(5, "All items should remain");
        }

        [Fact]
        public void CleanupOldItems_ExceedsLimit_RemovesOldest()
        {
            // Arrange
            var settings = CreateTestSettings();
            settings.MaxHistoryItems = 3;
            var service = new TranscriptionHistoryService(settings);

            // Act - Add items with delays to ensure different timestamps
            var firstItem = CreateTestItem("First (oldest)");
            firstItem.Timestamp = DateTime.Now.AddMinutes(-5);
            service.AddToHistory(firstItem);

            var secondItem = CreateTestItem("Second");
            secondItem.Timestamp = DateTime.Now.AddMinutes(-3);
            service.AddToHistory(secondItem);

            var thirdItem = CreateTestItem("Third");
            thirdItem.Timestamp = DateTime.Now.AddMinutes(-1);
            service.AddToHistory(thirdItem);

            var fourthItem = CreateTestItem("Fourth (newest)");
            fourthItem.Timestamp = DateTime.Now;
            service.AddToHistory(fourthItem);

            // Assert
            settings.TranscriptionHistory.Should().HaveCount(3, "Should enforce max limit");
            settings.TranscriptionHistory.Should().Contain(x => x.Text == "Fourth (newest)");
            settings.TranscriptionHistory.Should().Contain(x => x.Text == "Third");
            settings.TranscriptionHistory.Should().Contain(x => x.Text == "Second");
            settings.TranscriptionHistory.Should().NotContain(x => x.Text == "First (oldest)",
                "Oldest item should be removed");
        }

        [Fact]
        public void CleanupOldItems_P1Optimization_EarlyExitWorksCorrectly()
        {
            // Arrange
            var settings = CreateTestSettings();
            settings.MaxHistoryItems = 100;
            var service = new TranscriptionHistoryService(settings);

            // Act - Add 50 items (well below limit)
            for (int i = 0; i < 50; i++)
            {
                service.AddToHistory(CreateTestItem($"Item {i}"));
            }

            // Assert - P1 optimization should prevent allocations
            settings.TranscriptionHistory.Should().HaveCount(50);
            // Note: Early exit path doesn't allocate itemsToRemove list when count <= limit
        }

        #endregion

        #region RemoveFromHistory Tests

        [Fact]
        public void RemoveFromHistory_ExistingItem_ReturnsTrue()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            var item = CreateTestItem();
            service.AddToHistory(item);

            // Act
            var result = service.RemoveFromHistory(item.Id);

            // Assert
            result.Should().BeTrue();
            settings.TranscriptionHistory.Should().BeEmpty();
        }

        [Fact]
        public void RemoveFromHistory_NonExistentItem_ReturnsFalse()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);

            // Act
            var result = service.RemoveFromHistory("nonexistent-id");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void RemoveFromHistory_SpecificItem_LeavesOthersIntact()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            var item1 = CreateTestItem("Item 1");
            var item2 = CreateTestItem("Item 2");
            var item3 = CreateTestItem("Item 3");

            service.AddToHistory(item1);
            service.AddToHistory(item2);
            service.AddToHistory(item3);

            // Act
            service.RemoveFromHistory(item2.Id);

            // Assert
            settings.TranscriptionHistory.Should().HaveCount(2);
            settings.TranscriptionHistory.Should().Contain(x => x.Id == item1.Id);
            settings.TranscriptionHistory.Should().Contain(x => x.Id == item3.Id);
            settings.TranscriptionHistory.Should().NotContain(x => x.Id == item2.Id);
        }

        #endregion

        #region ClearHistory Tests

        [Fact]
        public void ClearHistory_RemovesAllItems()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);

            for (int i = 0; i < 5; i++)
            {
                service.AddToHistory(CreateTestItem($"Item {i}"));
            }

            // Act
            service.ClearHistory();

            // Assert
            settings.TranscriptionHistory.Should().BeEmpty();
        }

        [Fact]
        public void ClearHistory_WithPreservePinned_KeepsPinnedItems()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);

            var pinnedItem = CreateTestItem("Pinned", isPinned: true);
            var normalItem = CreateTestItem("Normal", isPinned: false);

            service.AddToHistory(pinnedItem);
            service.AddToHistory(normalItem);

            // Act
            service.ClearHistory(preservePinned: true);

            // Assert
            settings.TranscriptionHistory.Should().HaveCount(1);
            settings.TranscriptionHistory[0].Text.Should().Be("Pinned");
            settings.TranscriptionHistory[0].IsPinned.Should().BeTrue();
        }

        [Fact]
        public void ClearHistory_WithoutPreservePinned_RemovesEverything()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);

            service.AddToHistory(CreateTestItem("Pinned", isPinned: true));
            service.AddToHistory(CreateTestItem("Normal", isPinned: false));

            // Act
            service.ClearHistory(preservePinned: false);

            // Assert
            settings.TranscriptionHistory.Should().BeEmpty();
        }

        #endregion

        #region GetStatistics Tests

        [Fact]
        public void GetStatistics_EmptyHistory_ReturnsZeros()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);

            // Act
            var stats = service.GetStatistics();

            // Assert
            stats.TotalItems.Should().Be(0);
            stats.TotalWords.Should().Be(0);
            stats.AverageDuration.Should().Be(0);
        }

        [Fact]
        public void GetStatistics_WithItems_CalculatesCorrectly()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);

            var item1 = CreateTestItem("Hello world");
            item1.DurationSeconds = 2.0;
            item1.WordCount = 2;

            var item2 = CreateTestItem("This is a test");
            item2.DurationSeconds = 4.0;
            item2.WordCount = 4;

            service.AddToHistory(item1);
            service.AddToHistory(item2);

            // Act
            var stats = service.GetStatistics();

            // Assert
            stats.TotalItems.Should().Be(2);
            stats.TotalWords.Should().Be(6);
            stats.AverageDuration.Should().Be(3.0); // (2.0 + 4.0) / 2
        }

        #endregion

        #region SearchHistory Tests

        [Fact]
        public void SearchHistory_EmptySearchText_ReturnsAll()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            service.AddToHistory(CreateTestItem("First"));
            service.AddToHistory(CreateTestItem("Second"));

            // Act
            var results = service.SearchHistory("");

            // Assert
            results.Should().HaveCount(2);
        }

        [Fact]
        public void SearchHistory_MatchingText_ReturnsFiltered()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            service.AddToHistory(CreateTestItem("Hello world"));
            service.AddToHistory(CreateTestItem("Goodbye world"));
            service.AddToHistory(CreateTestItem("Something else"));

            // Act
            var results = service.SearchHistory("world");

            // Assert
            results.Should().HaveCount(2);
            results.Should().AllSatisfy(r => r.Text.Should().Contain("world"));
        }

        [Fact]
        public void SearchHistory_CaseInsensitive_Works()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            service.AddToHistory(CreateTestItem("HELLO WORLD"));

            // Act
            var results = service.SearchHistory("hello");

            // Assert
            results.Should().HaveCount(1);
        }

        #endregion

        #region TogglePin Tests

        [Fact]
        public void TogglePin_ExistingItem_TogglesState()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            var item = CreateTestItem(isPinned: false);
            service.AddToHistory(item);

            // Act
            service.TogglePin(item.Id);

            // Assert
            settings.TranscriptionHistory[0].IsPinned.Should().BeTrue();

            // Act again
            service.TogglePin(item.Id);

            // Assert
            settings.TranscriptionHistory[0].IsPinned.Should().BeFalse();
        }

        [Fact]
        public void GetPinnedItems_ReturnsOnlyPinned()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            service.AddToHistory(CreateTestItem("Pinned 1", isPinned: true));
            service.AddToHistory(CreateTestItem("Normal", isPinned: false));
            service.AddToHistory(CreateTestItem("Pinned 2", isPinned: true));

            // Act
            var pinned = service.GetPinnedItems();

            // Assert
            pinned.Should().HaveCount(2);
            pinned.Should().AllSatisfy(p => p.IsPinned.Should().BeTrue());
        }

        #endregion

        #region GetHistoryRange Tests

        [Fact]
        public void GetHistoryRange_FiltersCorrectly()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);

            var oldItem = CreateTestItem("Old");
            oldItem.Timestamp = DateTime.Now.AddDays(-5);

            var recentItem = CreateTestItem("Recent");
            recentItem.Timestamp = DateTime.Now.AddDays(-1);

            var newItem = CreateTestItem("New");
            newItem.Timestamp = DateTime.Now;

            service.AddToHistory(oldItem);
            service.AddToHistory(recentItem);
            service.AddToHistory(newItem);

            // Act
            var range = service.GetHistoryRange(DateTime.Now.AddDays(-2), DateTime.Now);

            // Assert
            range.Should().HaveCount(2);
            range.Should().Contain(x => x.Text == "Recent");
            range.Should().Contain(x => x.Text == "New");
            range.Should().NotContain(x => x.Text == "Old");
        }

        #endregion

        #region AddTranscription (Interface) Tests

        [Fact]
        public void AddTranscription_ConvertsAndAdds()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            var transcriptionItem = new TranscriptionItem
            {
                Id = Guid.NewGuid().ToString(),
                Text = "Test",
                Timestamp = DateTime.Now,
                ProcessingTime = 2.5,
                ModelUsed = "ggml-small.bin",
                IsPinned = false
            };

            // Act
            service.AddTranscription(transcriptionItem);

            // Assert
            settings.TranscriptionHistory.Should().HaveCount(1);
            var added = settings.TranscriptionHistory[0];
            added.Text.Should().Be("Test");
            added.DurationSeconds.Should().Be(2.5);
            added.ModelUsed.Should().Be("ggml-small.bin");
        }

        [Fact]
        public void AddTranscription_FiresItemAddedEvent()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            TranscriptionItem? eventItem = null;
            service.ItemAdded += (sender, item) => eventItem = item;

            var transcriptionItem = new TranscriptionItem
            {
                Id = Guid.NewGuid().ToString(),
                Text = "Event test",
                Timestamp = DateTime.Now
            };

            // Act
            service.AddTranscription(transcriptionItem);

            // Assert
            eventItem.Should().NotBeNull();
            eventItem.Text.Should().Be("Event test");
        }

        #endregion

        #region MaxHistoryItems Property Tests

        [Fact]
        public void MaxHistoryItems_GetSet_WorksCorrectly()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);

            // Act
            service.MaxHistoryItems = 50;

            // Assert
            service.MaxHistoryItems.Should().Be(50);
            settings.MaxHistoryItems.Should().Be(50);
        }

        #endregion
    }
}

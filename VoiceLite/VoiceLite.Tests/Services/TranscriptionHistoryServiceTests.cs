using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using VoiceLite.Models;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    [Trait("Category", "Unit")]
    public class TranscriptionHistoryServiceTests
    {
        private Settings CreateTestSettings()
        {
            return new Settings
            {
                EnableHistory = true,
                MaxHistoryItems = 5,
                TranscriptionHistory = new List<TranscriptionHistoryItem>()
            };
        }

        [Fact]
        public void Constructor_WithValidSettings_Succeeds()
        {
            // Arrange
            var settings = CreateTestSettings();

            // Act
            var service = new TranscriptionHistoryService(settings);

            // Assert
            service.Should().NotBeNull();
            settings.TranscriptionHistory.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullSettings_ThrowsArgumentNullException()
        {
            // Act & Assert
            Action act = () => new TranscriptionHistoryService(null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("settings");
        }

        [Fact]
        public void Constructor_WithNullHistoryList_InitializesEmptyList()
        {
            // Arrange
            var settings = new Settings { EnableHistory = true, MaxHistoryItems = 5 };
            settings.TranscriptionHistory = null!;

            // Act
            var service = new TranscriptionHistoryService(settings);

            // Assert
            settings.TranscriptionHistory.Should().NotBeNull();
            settings.TranscriptionHistory.Should().BeEmpty();
        }

        [Fact]
        public void AddToHistory_WithValidItem_InsertsAtBeginning()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            var item = new TranscriptionHistoryItem
            {
                Text = "Test transcription",
                Timestamp = DateTime.Now,
                WordCount = 2
            };

            // Act
            service.AddToHistory(item);

            // Assert
            settings.TranscriptionHistory.Should().HaveCount(1);
            settings.TranscriptionHistory[0].Should().Be(item);
            settings.TranscriptionHistory[0].Text.Should().Be("Test transcription");
        }

        [Fact]
        public void AddToHistory_WithMultipleItems_MaintainsNewestFirst()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            var item1 = new TranscriptionHistoryItem { Text = "First", Timestamp = DateTime.Now };
            var item2 = new TranscriptionHistoryItem { Text = "Second", Timestamp = DateTime.Now.AddSeconds(1) };
            var item3 = new TranscriptionHistoryItem { Text = "Third", Timestamp = DateTime.Now.AddSeconds(2) };

            // Act
            service.AddToHistory(item1);
            service.AddToHistory(item2);
            service.AddToHistory(item3);

            // Assert
            settings.TranscriptionHistory.Should().HaveCount(3);
            settings.TranscriptionHistory[0].Text.Should().Be("Third");
            settings.TranscriptionHistory[1].Text.Should().Be("Second");
            settings.TranscriptionHistory[2].Text.Should().Be("First");
        }

        [Fact]
        public void AddToHistory_WithNullItem_ThrowsArgumentNullException()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);

            // Act & Assert
            Action act = () => service.AddToHistory(null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("item");
        }

        [Fact]
        public void AddToHistory_WhenHistoryDisabled_DoesNotAdd()
        {
            // Arrange
            var settings = CreateTestSettings();
            settings.EnableHistory = false;
            var service = new TranscriptionHistoryService(settings);
            var item = new TranscriptionHistoryItem { Text = "Test", Timestamp = DateTime.Now };

            // Act
            service.AddToHistory(item);

            // Assert
            settings.TranscriptionHistory.Should().BeEmpty();
        }

        [Fact]
        public void AddToHistory_WithLongText_TruncatesToMaxLength()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            var longText = new string('a', 6000); // Exceeds MAX_TEXT_LENGTH (5000)
            var item = new TranscriptionHistoryItem { Text = longText, Timestamp = DateTime.Now };

            // Act
            service.AddToHistory(item);

            // Assert
            settings.TranscriptionHistory.Should().HaveCount(1);
            settings.TranscriptionHistory[0].Text.Length.Should().BeLessOrEqualTo(5020); // 5000 + "... (truncated)"
            settings.TranscriptionHistory[0].Text.Should().EndWith("... (truncated)");
        }

        [Fact]
        public void AddToHistory_ExceedingMaxItems_RemovesOldestUnpinned()
        {
            // Arrange
            var settings = CreateTestSettings();
            settings.MaxHistoryItems = 3;
            var service = new TranscriptionHistoryService(settings);

            // Add 4 items (exceeding limit)
            for (int i = 1; i <= 4; i++)
            {
                service.AddToHistory(new TranscriptionHistoryItem
                {
                    Text = $"Item {i}",
                    Timestamp = DateTime.Now.AddSeconds(i)
                });
            }

            // Assert
            settings.TranscriptionHistory.Should().HaveCount(3);
            settings.TranscriptionHistory.Any(x => x.Text == "Item 1").Should().BeFalse(); // Oldest removed
            settings.TranscriptionHistory.Any(x => x.Text == "Item 4").Should().BeTrue(); // Newest kept
        }

        [Fact]
        public void AddToHistory_ExceedingMaxItems_PreservesPinnedItems()
        {
            // Arrange
            var settings = CreateTestSettings();
            settings.MaxHistoryItems = 2; // Limit to 2 unpinned items
            var service = new TranscriptionHistoryService(settings);

            // Add items and pin item1 immediately
            var item1 = new TranscriptionHistoryItem { Text = "Item 1", Timestamp = DateTime.Now };
            service.AddToHistory(item1);
            service.TogglePin(item1.Id); // Pin item1

            var item2 = new TranscriptionHistoryItem { Text = "Item 2", Timestamp = DateTime.Now.AddSeconds(1) };
            service.AddToHistory(item2);

            var item3 = new TranscriptionHistoryItem { Text = "Item 3", Timestamp = DateTime.Now.AddSeconds(2) };
            service.AddToHistory(item3);

            var item4 = new TranscriptionHistoryItem { Text = "Item 4", Timestamp = DateTime.Now.AddSeconds(3) };
            service.AddToHistory(item4);

            // Assert
            settings.TranscriptionHistory.Should().HaveCount(3); // 1 pinned + 2 unpinned
            settings.TranscriptionHistory.Any(x => x.Text == "Item 1").Should().BeTrue(); // Pinned item kept
            settings.TranscriptionHistory.Any(x => x.Text == "Item 2").Should().BeFalse(); // Oldest unpinned removed
            settings.TranscriptionHistory.Any(x => x.Text == "Item 3").Should().BeTrue(); // Kept (newer)
            settings.TranscriptionHistory.Any(x => x.Text == "Item 4").Should().BeTrue(); // Kept (newest)
        }

        [Fact]
        public void RemoveFromHistory_WithValidId_RemovesItem()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            var item = new TranscriptionHistoryItem { Text = "Test", Timestamp = DateTime.Now };
            service.AddToHistory(item);

            // Act
            var result = service.RemoveFromHistory(item.Id);

            // Assert
            result.Should().BeTrue();
            settings.TranscriptionHistory.Should().BeEmpty();
        }

        [Fact]
        public void RemoveFromHistory_WithInvalidId_ReturnsFalse()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            var item = new TranscriptionHistoryItem { Text = "Test", Timestamp = DateTime.Now };
            service.AddToHistory(item);

            // Act
            var result = service.RemoveFromHistory("non-existent-id");

            // Assert
            result.Should().BeFalse();
            settings.TranscriptionHistory.Should().HaveCount(1);
        }

        [Fact]
        public void TogglePin_WithValidId_TogglesPinStatus()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            var item = new TranscriptionHistoryItem { Text = "Test", Timestamp = DateTime.Now };
            service.AddToHistory(item);

            // Act
            var result1 = service.TogglePin(item.Id);
            var isPinnedAfterFirst = item.IsPinned;
            var result2 = service.TogglePin(item.Id);
            var isPinnedAfterSecond = item.IsPinned;

            // Assert
            result1.Should().BeTrue();
            isPinnedAfterFirst.Should().BeTrue();
            result2.Should().BeTrue();
            isPinnedAfterSecond.Should().BeFalse();
        }

        [Fact]
        public void TogglePin_WithInvalidId_ReturnsFalse()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);

            // Act
            var result = service.TogglePin("non-existent-id");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void TogglePin_ReordersHistory_PinnedItemsFirst()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            var item1 = new TranscriptionHistoryItem { Text = "First", Timestamp = DateTime.Now };
            var item2 = new TranscriptionHistoryItem { Text = "Second", Timestamp = DateTime.Now.AddSeconds(1) };
            var item3 = new TranscriptionHistoryItem { Text = "Third", Timestamp = DateTime.Now.AddSeconds(2) };

            service.AddToHistory(item1);
            service.AddToHistory(item2);
            service.AddToHistory(item3);

            // Act - Pin the oldest item
            service.TogglePin(item1.Id);

            // Assert
            settings.TranscriptionHistory[0].Should().Be(item1); // Pinned item moved to top
            settings.TranscriptionHistory[1].Text.Should().Be("Third"); // Unpinned items by timestamp
            settings.TranscriptionHistory[2].Text.Should().Be("Second");
        }

        [Fact]
        public void ClearHistory_RemovesUnpinnedItems_PreservesPinned()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            var pinnedItem = new TranscriptionHistoryItem { Text = "Pinned", Timestamp = DateTime.Now, IsPinned = true };
            var unpinnedItem1 = new TranscriptionHistoryItem { Text = "Unpinned1", Timestamp = DateTime.Now };
            var unpinnedItem2 = new TranscriptionHistoryItem { Text = "Unpinned2", Timestamp = DateTime.Now };

            service.AddToHistory(pinnedItem);
            service.AddToHistory(unpinnedItem1);
            service.AddToHistory(unpinnedItem2);

            // Act
            var removedCount = service.ClearHistory();

            // Assert
            removedCount.Should().Be(2);
            settings.TranscriptionHistory.Should().HaveCount(1);
            settings.TranscriptionHistory[0].Should().Be(pinnedItem);
        }

        [Fact]
        public void ClearAllHistory_RemovesAllItems_IncludingPinned()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            var pinnedItem = new TranscriptionHistoryItem { Text = "Pinned", Timestamp = DateTime.Now, IsPinned = true };
            var unpinnedItem = new TranscriptionHistoryItem { Text = "Unpinned", Timestamp = DateTime.Now };

            service.AddToHistory(pinnedItem);
            service.AddToHistory(unpinnedItem);

            // Act
            var removedCount = service.ClearAllHistory();

            // Assert
            removedCount.Should().Be(2);
            settings.TranscriptionHistory.Should().BeEmpty();
        }

        [Fact]
        public void GetPinnedItems_ReturnsOnlyPinnedItems()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            var pinnedItem1 = new TranscriptionHistoryItem { Text = "Pinned1", Timestamp = DateTime.Now, IsPinned = true };
            var pinnedItem2 = new TranscriptionHistoryItem { Text = "Pinned2", Timestamp = DateTime.Now, IsPinned = true };
            var unpinnedItem = new TranscriptionHistoryItem { Text = "Unpinned", Timestamp = DateTime.Now };

            service.AddToHistory(pinnedItem1);
            service.AddToHistory(unpinnedItem);
            service.AddToHistory(pinnedItem2);

            // Act
            var pinnedItems = service.GetPinnedItems().ToList();

            // Assert
            pinnedItems.Should().HaveCount(2);
            pinnedItems.Should().Contain(pinnedItem1);
            pinnedItems.Should().Contain(pinnedItem2);
            pinnedItems.Should().NotContain(unpinnedItem);
        }

        [Fact]
        public void GetUnpinnedItems_ReturnsOnlyUnpinnedItems()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            var pinnedItem = new TranscriptionHistoryItem { Text = "Pinned", Timestamp = DateTime.Now, IsPinned = true };
            var unpinnedItem1 = new TranscriptionHistoryItem { Text = "Unpinned1", Timestamp = DateTime.Now };
            var unpinnedItem2 = new TranscriptionHistoryItem { Text = "Unpinned2", Timestamp = DateTime.Now };

            service.AddToHistory(pinnedItem);
            service.AddToHistory(unpinnedItem1);
            service.AddToHistory(unpinnedItem2);

            // Act
            var unpinnedItems = service.GetUnpinnedItems().ToList();

            // Assert
            unpinnedItems.Should().HaveCount(2);
            unpinnedItems.Should().Contain(unpinnedItem1);
            unpinnedItems.Should().Contain(unpinnedItem2);
            unpinnedItems.Should().NotContain(pinnedItem);
        }

        [Fact]
        public void GetStatistics_ReturnsCorrectAggregations()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);
            var baseTime = DateTime.Now;

            var item1 = new TranscriptionHistoryItem
            {
                Text = "Test one",
                Timestamp = baseTime,
                WordCount = 2,
                DurationSeconds = 1.5,
                IsPinned = true
            };

            var item2 = new TranscriptionHistoryItem
            {
                Text = "Test two three",
                Timestamp = baseTime.AddMinutes(1),
                WordCount = 3,
                DurationSeconds = 2.5
            };

            service.AddToHistory(item1);
            service.AddToHistory(item2);

            // Act
            var stats = service.GetStatistics();

            // Assert
            stats.TotalItems.Should().Be(2);
            stats.PinnedItems.Should().Be(1);
            stats.TotalWords.Should().Be(5); // 2 + 3
            stats.AverageDuration.Should().Be(2.0); // (1.5 + 2.5) / 2
            stats.OldestTimestamp.Should().Be(baseTime);
        }

        [Fact]
        public void GetStatistics_WithEmptyHistory_ReturnsZeroValues()
        {
            // Arrange
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);

            // Act
            var stats = service.GetStatistics();

            // Assert
            stats.TotalItems.Should().Be(0);
            stats.PinnedItems.Should().Be(0);
            stats.TotalWords.Should().Be(0);
            stats.AverageDuration.Should().Be(0);
        }
    }
}

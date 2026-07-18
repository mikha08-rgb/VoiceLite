using AwesomeAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
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
                TranscriptionHistory = null! // Null initially
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
        public void AddToHistory_WhenEnableHistoryIsFalse_DoesNotPersist()
        {
            // Clinical-pilot opt-out: settings.EnableHistory = false must prevent
            // any transcribed text from being written to TranscriptionHistory.
            var settings = CreateTestSettings();
            settings.EnableHistory = false;
            var service = new TranscriptionHistoryService(settings);

            service.AddToHistory(CreateTestItem("sensitive patient transcription"));

            settings.TranscriptionHistory.Should().BeEmpty(
                "EnableHistory=false must block all writes to history (PILOT.md privacy guarantee)");
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
            addedItem.Text.Length.Should().BeLessThanOrEqualTo(5000 + 20); // +20 for "... (truncated)"
            addedItem.Text.Should().EndWith("... (truncated)");
        }

        #endregion

        #region CleanupOldItems Tests (P1 Optimization)

        [Fact]
        public void CleanupOldItems_BelowLimit_NoCleanup()
        {
            // Arrange — MAX_HISTORY_ITEMS is hardcoded to 250.
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);

            // Add 50 items (well below the 250 cap)
            for (int i = 0; i < 50; i++)
            {
                service.AddToHistory(CreateTestItem($"Item {i}"));
            }

            // Assert
            settings.TranscriptionHistory.Should().HaveCount(50, "All items should remain when below cap");
        }

        [Fact]
        public void CleanupOldItems_AboveLimit_RemovesOldestUnpinned_ButKeepsPinned()
        {
            // Arrange — MAX_HISTORY_ITEMS is hardcoded to 250.
            var settings = CreateTestSettings();
            var service = new TranscriptionHistoryService(settings);

            // Oldest item of all is PINNED — it must survive cap enforcement.
            var pinnedItem = CreateTestItem("Pinned oldest", isPinned: true);
            pinnedItem.Timestamp = DateTime.Now.AddDays(-30);
            service.AddToHistory(pinnedItem);

            // Second-oldest is unpinned — it is the one that must age out.
            var oldestUnpinned = CreateTestItem("Unpinned oldest");
            oldestUnpinned.Timestamp = DateTime.Now.AddDays(-20);
            service.AddToHistory(oldestUnpinned);

            // Fill to 251 total (1 over the cap).
            for (int i = 0; i < 249; i++)
            {
                service.AddToHistory(CreateTestItem($"Item {i}"));
            }

            // Assert — cap enforced, pinned item exempt, oldest UNPINNED removed instead.
            settings.TranscriptionHistory.Should().HaveCount(250);
            settings.TranscriptionHistory.Should().Contain(x => x.Id == pinnedItem.Id,
                "pinned items must be exempt from the 250-item cap");
            settings.TranscriptionHistory.Should().NotContain(x => x.Id == oldestUnpinned.Id,
                "the oldest unpinned item should age out in its place");
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

        #endregion

    }
}

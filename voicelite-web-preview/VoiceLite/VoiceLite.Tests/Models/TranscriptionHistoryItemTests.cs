using System;
using FluentAssertions;
using VoiceLite.Models;
using Xunit;

namespace VoiceLite.Tests.Models
{
    [Trait("Category", "Unit")]
    public class TranscriptionHistoryItemTests
    {
        #region Constructor and Default Values

        [Fact]
        public void Constructor_SetsDefaultValues()
        {
            // Arrange & Act
            var item = new TranscriptionHistoryItem();

            // Assert
            item.Id.Should().NotBeNullOrEmpty();
            item.Timestamp.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
            item.Text.Should().Be(string.Empty);
            item.WordCount.Should().Be(0);
            item.DurationSeconds.Should().Be(0);
            item.ModelUsed.Should().Be("tiny");
            item.ConfidenceScore.Should().BeNull();
            item.IsPinned.Should().BeFalse();
        }

        [Fact]
        public void Id_DefaultValue_IsGuid()
        {
            // Arrange & Act
            var item = new TranscriptionHistoryItem();

            // Assert
            item.Id.Should().NotBeNullOrEmpty();
            Guid.TryParse(item.Id, out _).Should().BeTrue();
        }

        [Fact]
        public void Timestamp_DefaultValue_IsNow()
        {
            // Arrange
            var before = DateTime.Now;

            // Act
            var item = new TranscriptionHistoryItem();
            var after = DateTime.Now;

            // Assert
            item.Timestamp.Should().BeOnOrAfter(before);
            item.Timestamp.Should().BeOnOrBefore(after);
        }

        [Fact]
        public void Text_DefaultValue_IsEmpty()
        {
            // Arrange & Act
            var item = new TranscriptionHistoryItem();

            // Assert
            item.Text.Should().Be(string.Empty);
        }

        [Fact]
        public void WordCount_DefaultValue_Is0()
        {
            // Arrange & Act
            var item = new TranscriptionHistoryItem();

            // Assert
            item.WordCount.Should().Be(0);
        }

        [Fact]
        public void DurationSeconds_DefaultValue_Is0()
        {
            // Arrange & Act
            var item = new TranscriptionHistoryItem();

            // Assert
            item.DurationSeconds.Should().Be(0);
        }

        [Fact]
        public void ModelUsed_DefaultValue_IsTiny()
        {
            // Arrange & Act
            var item = new TranscriptionHistoryItem();

            // Assert
            item.ModelUsed.Should().Be("tiny");
        }

        [Fact]
        public void ConfidenceScore_DefaultValue_IsNull()
        {
            // Arrange & Act
            var item = new TranscriptionHistoryItem();

            // Assert
            item.ConfidenceScore.Should().BeNull();
        }

        [Fact]
        public void IsPinned_DefaultValue_IsFalse()
        {
            // Arrange & Act
            var item = new TranscriptionHistoryItem();

            // Assert
            item.IsPinned.Should().BeFalse();
        }

        #endregion

        #region Property Setters

        [Fact]
        public void Id_SetToValue_Updates()
        {
            // Arrange
            var item = new TranscriptionHistoryItem();
            var newId = Guid.NewGuid().ToString();

            // Act
            item.Id = newId;

            // Assert
            item.Id.Should().Be(newId);
        }

        [Fact]
        public void Timestamp_SetToValue_Updates()
        {
            // Arrange
            var item = new TranscriptionHistoryItem();
            var newTimestamp = new DateTime(2025, 1, 10, 14, 30, 0);

            // Act
            item.Timestamp = newTimestamp;

            // Assert
            item.Timestamp.Should().Be(newTimestamp);
        }

        [Fact]
        public void Text_SetToValue_Updates()
        {
            // Arrange
            var item = new TranscriptionHistoryItem();

            // Act
            item.Text = "Hello world";

            // Assert
            item.Text.Should().Be("Hello world");
        }

        [Fact]
        public void WordCount_SetToValue_Updates()
        {
            // Arrange
            var item = new TranscriptionHistoryItem();

            // Act
            item.WordCount = 5;

            // Assert
            item.WordCount.Should().Be(5);
        }

        [Fact]
        public void DurationSeconds_SetToValue_Updates()
        {
            // Arrange
            var item = new TranscriptionHistoryItem();

            // Act
            item.DurationSeconds = 3.5;

            // Assert
            item.DurationSeconds.Should().Be(3.5);
        }

        [Fact]
        public void ModelUsed_SetToValue_Updates()
        {
            // Arrange
            var item = new TranscriptionHistoryItem();

            // Act
            item.ModelUsed = "large";

            // Assert
            item.ModelUsed.Should().Be("large");
        }

        [Fact]
        public void ConfidenceScore_SetToValue_Updates()
        {
            // Arrange
            var item = new TranscriptionHistoryItem();

            // Act
            item.ConfidenceScore = 0.95;

            // Assert
            item.ConfidenceScore.Should().Be(0.95);
        }

        [Fact]
        public void IsPinned_SetToTrue_Updates()
        {
            // Arrange
            var item = new TranscriptionHistoryItem();

            // Act
            item.IsPinned = true;

            // Assert
            item.IsPinned.Should().BeTrue();
        }

        #endregion

        #region DisplayTimestamp Tests

        [Fact]
        public void DisplayTimestamp_FormatsMorningTime()
        {
            // Arrange
            var item = new TranscriptionHistoryItem
            {
                Timestamp = new DateTime(2025, 1, 10, 9, 30, 0)
            };

            // Act & Assert
            item.DisplayTimestamp.Should().Be("9:30 AM");
        }

        [Fact]
        public void DisplayTimestamp_FormatsAfternoonTime()
        {
            // Arrange
            var item = new TranscriptionHistoryItem
            {
                Timestamp = new DateTime(2025, 1, 10, 14, 45, 0)
            };

            // Act & Assert
            item.DisplayTimestamp.Should().Be("2:45 PM");
        }

        [Fact]
        public void DisplayTimestamp_FormatsMidnight()
        {
            // Arrange
            var item = new TranscriptionHistoryItem
            {
                Timestamp = new DateTime(2025, 1, 10, 0, 0, 0)
            };

            // Act & Assert
            item.DisplayTimestamp.Should().Be("12:00 AM");
        }

        [Fact]
        public void DisplayTimestamp_FormatsNoon()
        {
            // Arrange
            var item = new TranscriptionHistoryItem
            {
                Timestamp = new DateTime(2025, 1, 10, 12, 0, 0)
            };

            // Act & Assert
            item.DisplayTimestamp.Should().Be("12:00 PM");
        }

        [Fact]
        public void DisplayTimestamp_FormatsEveningTime()
        {
            // Arrange
            var item = new TranscriptionHistoryItem
            {
                Timestamp = new DateTime(2025, 1, 10, 20, 15, 0)
            };

            // Act & Assert
            item.DisplayTimestamp.Should().Be("8:15 PM");
        }

        #endregion

        #region PreviewText Tests

        [Fact]
        public void PreviewText_ShortText_ReturnsFullText()
        {
            // Arrange
            var item = new TranscriptionHistoryItem
            {
                Text = "Hello world"
            };

            // Act & Assert
            item.PreviewText.Should().Be("Hello world");
        }

        [Fact]
        public void PreviewText_LongText_TruncatesTo100CharsWithEllipsis()
        {
            // Arrange
            var longText = new string('A', 150); // 150 chars
            var item = new TranscriptionHistoryItem
            {
                Text = longText
            };

            // Act
            var preview = item.PreviewText;

            // Assert
            preview.Length.Should().Be(103); // 100 chars + "..."
            preview.Should().EndWith("...");
            preview.Should().StartWith(new string('A', 100));
        }

        [Fact]
        public void PreviewText_ExactlyMaxLength_ReturnsFullTextNoEllipsis()
        {
            // Arrange
            var exactText = new string('B', 100); // Exactly 100 chars
            var item = new TranscriptionHistoryItem
            {
                Text = exactText
            };

            // Act & Assert
            item.PreviewText.Should().Be(exactText);
            item.PreviewText.Should().NotContain("...");
        }

        [Fact]
        public void PreviewText_NullText_ReturnsEmpty()
        {
            // Arrange
            var item = new TranscriptionHistoryItem
            {
                Text = null! // BUG-015 fix test
            };

            // Act & Assert
            item.PreviewText.Should().Be(string.Empty);
        }

        [Fact]
        public void PreviewText_EmptyText_ReturnsEmpty()
        {
            // Arrange
            var item = new TranscriptionHistoryItem
            {
                Text = string.Empty
            };

            // Act & Assert
            item.PreviewText.Should().Be(string.Empty);
        }

        [Fact]
        public void PreviewText_WhitespaceText_ReturnsWhitespace()
        {
            // Arrange
            var item = new TranscriptionHistoryItem
            {
                Text = "   " // 3 spaces
            };

            // Act & Assert
            item.PreviewText.Should().Be("   ");
        }

        [Fact]
        public void PreviewText_101Chars_TruncatesWithEllipsis()
        {
            // Arrange
            var text = new string('C', 101); // 101 chars (just over limit)
            var item = new TranscriptionHistoryItem
            {
                Text = text
            };

            // Act
            var preview = item.PreviewText;

            // Assert
            preview.Length.Should().Be(103); // 100 chars + "..."
            preview.Should().EndWith("...");
        }

        [Fact]
        public void PreviewText_99Chars_ReturnsFullTextNoEllipsis()
        {
            // Arrange
            var text = new string('D', 99); // 99 chars (under limit)
            var item = new TranscriptionHistoryItem
            {
                Text = text
            };

            // Act & Assert
            item.PreviewText.Should().Be(text);
            item.PreviewText.Should().NotContain("...");
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void MultipleInstances_HaveUniqueIds()
        {
            // Arrange & Act
            var item1 = new TranscriptionHistoryItem();
            var item2 = new TranscriptionHistoryItem();

            // Assert
            item1.Id.Should().NotBe(item2.Id);
        }

        [Fact]
        public void ConfidenceScore_CanBeSetToZero()
        {
            // Arrange
            var item = new TranscriptionHistoryItem();

            // Act
            item.ConfidenceScore = 0.0;

            // Assert
            item.ConfidenceScore.Should().Be(0.0);
        }

        [Fact]
        public void ConfidenceScore_CanBeSetToOne()
        {
            // Arrange
            var item = new TranscriptionHistoryItem();

            // Act
            item.ConfidenceScore = 1.0;

            // Assert
            item.ConfidenceScore.Should().Be(1.0);
        }

        [Fact]
        public void IsPinned_CanToggle()
        {
            // Arrange
            var item = new TranscriptionHistoryItem();

            // Act & Assert
            item.IsPinned.Should().BeFalse();
            item.IsPinned = true;
            item.IsPinned.Should().BeTrue();
            item.IsPinned = false;
            item.IsPinned.Should().BeFalse();
        }

        #endregion
    }
}

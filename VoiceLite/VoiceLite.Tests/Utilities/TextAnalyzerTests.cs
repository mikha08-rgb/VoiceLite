using FluentAssertions;
using VoiceLite.Utilities;
using Xunit;

namespace VoiceLite.Tests.Utilities
{
    [Trait("Category", "Unit")]
    public class TextAnalyzerTests
    {
        #region CountWords Tests

        [Fact]
        public void CountWords_NullInput_ReturnsZero()
        {
            TextAnalyzer.CountWords(null).Should().Be(0);
        }

        [Fact]
        public void CountWords_EmptyString_ReturnsZero()
        {
            TextAnalyzer.CountWords("").Should().Be(0);
        }

        [Fact]
        public void CountWords_WhitespaceOnly_ReturnsZero()
        {
            TextAnalyzer.CountWords("   \t\n\r   ").Should().Be(0);
        }

        [Fact]
        public void CountWords_SingleWord_ReturnsOne()
        {
            TextAnalyzer.CountWords("hello").Should().Be(1);
        }

        [Fact]
        public void CountWords_MultipleWords_ReturnsCorrectCount()
        {
            TextAnalyzer.CountWords("hello world test").Should().Be(3);
        }

        [Fact]
        public void CountWords_MultipleSpaces_ReturnsCorrectCount()
        {
            TextAnalyzer.CountWords("hello    world").Should().Be(2);
        }

        [Fact]
        public void CountWords_LeadingTrailingSpaces_ReturnsCorrectCount()
        {
            TextAnalyzer.CountWords("  hello world  ").Should().Be(2);
        }

        [Fact]
        public void CountWords_TabSeparated_ReturnsCorrectCount()
        {
            TextAnalyzer.CountWords("hello\tworld\ttest").Should().Be(3);
        }

        [Fact]
        public void CountWords_NewlineSeparated_ReturnsCorrectCount()
        {
            TextAnalyzer.CountWords("hello\nworld\ntest").Should().Be(3);
        }

        [Fact]
        public void CountWords_MixedWhitespace_ReturnsCorrectCount()
        {
            TextAnalyzer.CountWords("hello \t world \n test \r\n end").Should().Be(4);
        }

        [Fact]
        public void CountWords_LongTranscription_ReturnsCorrectCount()
        {
            var text = "This is a longer transcription with multiple words and punctuation marks, to test the word counting functionality.";
            // Count: This(1) is(2) a(3) longer(4) transcription(5) with(6) multiple(7) words(8) and(9) punctuation(10) marks(11) to(12) test(13) the(14) word(15) counting(16) functionality(17)
            TextAnalyzer.CountWords(text).Should().Be(17);
        }

        #endregion

        #region Truncate Tests

        [Fact]
        public void Truncate_NullInput_ReturnsNull()
        {
            TextAnalyzer.Truncate(null!, 10).Should().BeNull();
        }

        [Fact]
        public void Truncate_EmptyString_ReturnsEmpty()
        {
            TextAnalyzer.Truncate("", 10).Should().Be("");
        }

        [Fact]
        public void Truncate_ShorterThanMax_ReturnsOriginal()
        {
            TextAnalyzer.Truncate("hello", 10).Should().Be("hello");
        }

        [Fact]
        public void Truncate_ExactlyMaxLength_ReturnsOriginal()
        {
            TextAnalyzer.Truncate("hello", 5).Should().Be("hello");
        }

        [Fact]
        public void Truncate_LongerThanMax_TruncatesWithEllipsis()
        {
            TextAnalyzer.Truncate("hello world", 5).Should().Be("hello...");
        }

        [Fact]
        public void Truncate_LongText_TruncatesCorrectly()
        {
            var text = "This is a very long transcription that needs to be truncated";
            var result = TextAnalyzer.Truncate(text, 20);
            result.Should().Be("This is a very long ...");
            result.Length.Should().Be(23); // 20 + "..." = 23
        }

        [Fact]
        public void Truncate_ZeroMaxLength_ReturnsEllipsis()
        {
            TextAnalyzer.Truncate("hello", 0).Should().Be("...");
        }

        [Fact]
        public void Truncate_OneCharMax_TruncatesCorrectly()
        {
            TextAnalyzer.Truncate("hello", 1).Should().Be("h...");
        }

        [Fact]
        public void Truncate_WithSpecialCharacters_TruncatesCorrectly()
        {
            TextAnalyzer.Truncate("Hello! How are you?", 10).Should().Be("Hello! How...");
        }

        [Fact]
        public void Truncate_WithUnicode_TruncatesCorrectly()
        {
            TextAnalyzer.Truncate("Hello 世界 World", 10).Should().Be("Hello 世界 W...");
        }

        #endregion
    }
}

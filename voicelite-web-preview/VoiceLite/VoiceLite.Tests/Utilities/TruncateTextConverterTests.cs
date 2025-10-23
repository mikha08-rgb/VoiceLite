using FluentAssertions;
using System;
using System.Globalization;
using VoiceLite.Utilities;
using Xunit;

namespace VoiceLite.Tests.Utilities
{
    [Trait("Category", "Unit")]
    public class TruncateTextConverterTests
    {
        private readonly TruncateTextConverter _converter = new();

        [Fact]
        public void Convert_NullValue_ReturnsNull()
        {
            var result = _converter.Convert(null!, typeof(string), null, CultureInfo.CurrentCulture);

            // Null value is not a string, so returns original value (null)
            result.Should().BeNull();
        }

        [Fact]
        public void Convert_EmptyString_ReturnsEmpty()
        {
            var result = _converter.Convert("", typeof(string), null, CultureInfo.CurrentCulture);

            result.Should().Be(string.Empty);
        }

        [Fact]
        public void Convert_ShortText_ReturnsOriginal()
        {
            var text = "Hello World";
            var result = _converter.Convert(text, typeof(string), null, CultureInfo.CurrentCulture);

            result.Should().Be("Hello World");
        }

        [Fact]
        public void Convert_TextAtMaxLength_ReturnsOriginal()
        {
            var text = new string('a', 100); // Exactly 100 characters (default max)
            var result = _converter.Convert(text, typeof(string), null, CultureInfo.CurrentCulture);

            result.Should().Be(text);
        }

        [Fact]
        public void Convert_TextOverMaxLength_TruncatesWithEllipsis()
        {
            var text = new string('a', 150); // 150 characters (over default 100)
            var result = _converter.Convert(text, typeof(string), null, CultureInfo.CurrentCulture);

            result.Should().Be(new string('a', 100) + "...");
        }

        [Fact]
        public void Convert_WithIntParameter_UsesCustomMaxLength()
        {
            var text = "This is a test string that should be truncated";
            var result = _converter.Convert(text, typeof(string), 10, CultureInfo.CurrentCulture);

            result.Should().Be("This is a ...");
        }

        [Fact]
        public void Convert_WithStringParameter_UsesCustomMaxLength()
        {
            var text = "This is a test string that should be truncated";
            var result = _converter.Convert(text, typeof(string), "15", CultureInfo.CurrentCulture);

            result.Should().Be("This is a test ...");
        }

        [Fact]
        public void Convert_WithInvalidStringParameter_UsesDefaultMaxLength()
        {
            var text = new string('a', 150);
            var result = _converter.Convert(text, typeof(string), "invalid", CultureInfo.CurrentCulture);

            // Should use default 100 when parameter is invalid
            result.Should().Be(new string('a', 100) + "...");
        }

        [Fact]
        public void Convert_WithNullParameter_UsesDefaultMaxLength()
        {
            var text = new string('a', 150);
            var result = _converter.Convert(text, typeof(string), null, CultureInfo.CurrentCulture);

            // Should use default 100 when parameter is null
            result.Should().Be(new string('a', 100) + "...");
        }

        [Fact]
        public void Convert_ZeroMaxLength_ReturnsEllipsis()
        {
            var text = "Hello World";
            var result = _converter.Convert(text, typeof(string), 0, CultureInfo.CurrentCulture);

            result.Should().Be("...");
        }

        [Fact]
        public void Convert_OneCharMaxLength_TruncatesCorrectly()
        {
            var text = "Hello World";
            var result = _converter.Convert(text, typeof(string), 1, CultureInfo.CurrentCulture);

            result.Should().Be("H...");
        }

        [Fact]
        public void Convert_LongTranscription_TruncatesCorrectly()
        {
            var text = "This is a very long transcription that contains a lot of information and should definitely be truncated to fit in the UI";
            var result = _converter.Convert(text, typeof(string), 50, CultureInfo.CurrentCulture);

            // First 50 chars: "This is a very long transcription that contains a " (note trailing space)
            result.Should().Be("This is a very long transcription that contains a ...");
            result.ToString()!.Length.Should().Be(53); // 50 + "..." = 53
        }

        [Fact]
        public void Convert_WithUnicode_TruncatesCorrectly()
        {
            var text = "Hello 世界 World 你好";
            var result = _converter.Convert(text, typeof(string), 10, CultureInfo.CurrentCulture);

            result.Should().Be("Hello 世界 W...");
        }

        [Fact]
        public void Convert_NonStringValue_ReturnsOriginalValue()
        {
            var number = 12345;
            var result = _converter.Convert(number, typeof(string), null, CultureInfo.CurrentCulture);

            result.Should().Be(12345);
        }

        [Fact]
        public void ConvertBack_ThrowsNotImplementedException()
        {
            Action act = () => _converter.ConvertBack("truncated...", typeof(string), null, CultureInfo.CurrentCulture);

            act.Should().Throw<NotImplementedException>()
                .WithMessage("TruncateTextConverter does not support two-way binding");
        }
    }
}

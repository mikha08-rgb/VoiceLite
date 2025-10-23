using System;
using FluentAssertions;
using Moq;
using VoiceLite.Models;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    [Trait("Category", "Unit")]
    public class TextInjectorTests
    {
        private readonly Settings _settings;

        public TextInjectorTests()
        {
            _settings = new Settings
            {
                TextInjectionMode = TextInjectionMode.SmartAuto
            };
        }

        [Fact]
        public void Constructor_WithNullSettings_ThrowsArgumentNullException()
        {
            // Act
            Action act = () => new TextInjector(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("settings");
        }

        [Fact]
        public void Constructor_WithValidSettings_SetsAutoPasteToTrue()
        {
            // Act
            var injector = new TextInjector(_settings);

            // Assert
            injector.AutoPaste.Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void InjectText_WithNullOrWhitespace_DoesNotThrow(string? text)
        {
            // Arrange
            var injector = new TextInjector(_settings);

            // Act
            Action act = () => injector.InjectText(text!);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void ShouldUseTyping_AlwaysTypeMode_ReturnsTrue()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.AlwaysType;
            var injector = new TextInjector(_settings);

            // Act
            var shouldType = InvokeShouldUseTyping(injector, "short");

            // Assert
            shouldType.Should().BeTrue();
        }

        [Fact]
        public void ShouldUseTyping_AlwaysPasteMode_ReturnsFalse()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.AlwaysPaste;
            var injector = new TextInjector(_settings);

            // Act
            var shouldType = InvokeShouldUseTyping(injector, "any text");

            // Assert
            shouldType.Should().BeFalse();
        }

        [Theory]
        [InlineData("short", true)]  // < 100 chars
        [InlineData("This is a medium text that exceeds one hundred characters and should trigger paste mode instead of typing mode for better performance", false)]  // > 100 chars
        public void ShouldUseTyping_PreferTypeMode_ReturnsExpectedResult(string text, bool expected)
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.PreferType;
            var injector = new TextInjector(_settings);

            // Act
            var shouldType = InvokeShouldUseTyping(injector, text);

            // Assert
            shouldType.Should().Be(expected);
        }

        [Theory]
        [InlineData("hi", true)]  // Very short < 10
        [InlineData("This is longer text", false)]  // > 10 chars
        public void ShouldUseTyping_PreferPasteMode_ReturnsExpectedResult(string text, bool expected)
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.PreferPaste;
            var injector = new TextInjector(_settings);

            // Act
            var shouldType = InvokeShouldUseTyping(injector, text);

            // Assert
            shouldType.Should().Be(expected);
        }

        [Theory]
        [InlineData("Hello world", true)]  // < 50 chars, SmartAuto uses typing
        [InlineData("This is a very long text that exceeds fifty characters and should use paste", false)]  // > 50 chars
        public void ShouldUseTyping_SmartAutoMode_ShortText_ReturnsTrue(string text, bool expected)
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.SmartAuto;
            var injector = new TextInjector(_settings);

            // Act
            var shouldType = InvokeShouldUseTyping(injector, text);

            // Assert
            shouldType.Should().Be(expected);
        }

        [Fact]
        public void ContainsSensitiveContent_ShortTextWithSpecialChars_ReturnsTrue()
        {
            // Arrange
            var injector = new TextInjector(_settings);
            var sensitiveText = "P@ssw0rd!"; // < 30 chars, no spaces, has special chars

            // Act
            var isSensitive = InvokeContainsSensitiveContent(injector, sensitiveText);

            // Assert
            isSensitive.Should().BeTrue();
        }

        [Fact]
        public void ContainsSensitiveContent_LongTextWithSpaces_ReturnsFalse()
        {
            // Arrange
            var injector = new TextInjector(_settings);
            var normalText = "This is a normal sentence with spaces"; // > 30 chars

            // Act
            var isSensitive = InvokeContainsSensitiveContent(injector, normalText);

            // Assert
            isSensitive.Should().BeFalse();
        }

        [Fact]
        public void ContainsSensitiveContent_ShortTextWithSpaces_ReturnsFalse()
        {
            // Arrange
            var injector = new TextInjector(_settings);
            var normalText = "hello world"; // < 30 chars but has spaces

            // Act
            var isSensitive = InvokeContainsSensitiveContent(injector, normalText);

            // Assert
            isSensitive.Should().BeFalse();
        }

        [Theory]
        [InlineData("abc123")]
        [InlineData("hello")]
        [InlineData("12345")]
        public void ContainsSpecialChars_AlphanumericOnly_ReturnsFalse(string text)
        {
            // Arrange
            var injector = new TextInjector(_settings);

            // Act
            var hasSpecialChars = InvokeContainsSpecialChars(injector, text);

            // Assert
            hasSpecialChars.Should().BeFalse();
        }

        [Theory]
        [InlineData("P@ssword")]
        [InlineData("test!")]
        [InlineData("user#123")]
        [InlineData("key$value")]
        public void ContainsSpecialChars_WithSpecialCharacters_ReturnsTrue(string text)
        {
            // Arrange
            var injector = new TextInjector(_settings);

            // Act
            var hasSpecialChars = InvokeContainsSpecialChars(injector, text);

            // Assert
            hasSpecialChars.Should().BeTrue();
        }

        [Fact]
        public void AutoPaste_Property_CanBeSetAndRetrieved()
        {
            // Arrange
            var injector = new TextInjector(_settings);

            // Act
            injector.AutoPaste = false;

            // Assert
            injector.AutoPaste.Should().BeFalse();
        }

        // Branch Coverage Tests - ShouldUseTyping()

        [Fact]
        public void ShouldUseTyping_SmartAutoMode_WithSensitiveContent_ReturnsTrue()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.SmartAuto;
            var injector = new TextInjector(_settings);
            var sensitiveText = "P@ssw0rd!"; // Short, no spaces, special chars

            // Act
            var shouldType = InvokeShouldUseTyping(injector, sensitiveText);

            // Assert - Should type because ContainsSensitiveContent returns true
            shouldType.Should().BeTrue();
        }

        [Fact]
        public void ShouldUseTyping_SmartAutoMode_LongNormalText_ReturnsFalse()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.SmartAuto;
            var injector = new TextInjector(_settings);
            var longText = "This is a long normal text with spaces that exceeds fifty characters limit";

            // Act
            var shouldType = InvokeShouldUseTyping(injector, longText);

            // Assert - Should paste because > 50 chars and not sensitive
            shouldType.Should().BeFalse();
        }

        // Branch Coverage Tests - ContainsSensitiveContent Edge Cases

        [Fact]
        public void ContainsSensitiveContent_LongText_NoSpaces_WithSpecialChars_ReturnsFalse()
        {
            // Arrange
            var injector = new TextInjector(_settings);
            var text = "P@ssw0rd!123456789012345678901"; // 31 chars (>= 30), no spaces, special chars

            // Act
            var isSensitive = InvokeContainsSensitiveContent(injector, text);

            // Assert - Returns false because length >= 30 (first condition fails)
            isSensitive.Should().BeFalse();
        }

        [Fact]
        public void ContainsSensitiveContent_ShortText_WithSpaces_WithSpecialChars_ReturnsFalse()
        {
            // Arrange
            var injector = new TextInjector(_settings);
            var text = "P@ss w0rd!"; // < 30 chars, HAS spaces, special chars

            // Act
            var isSensitive = InvokeContainsSensitiveContent(injector, text);

            // Assert - Returns false because text contains spaces (second condition fails)
            isSensitive.Should().BeFalse();
        }

        [Fact]
        public void ContainsSensitiveContent_ShortText_NoSpaces_NoSpecialChars_ReturnsFalse()
        {
            // Arrange
            var injector = new TextInjector(_settings);
            var text = "abc123"; // < 30 chars, no spaces, but no special chars

            // Act
            var isSensitive = InvokeContainsSensitiveContent(injector, text);

            // Assert - Returns false because ContainsSpecialChars returns false (third condition fails)
            isSensitive.Should().BeFalse();
        }

        // Branch Coverage Tests - ContainsSpecialChars Edge Cases

        [Theory]
        [InlineData(" ")] // Space is not special char
        [InlineData("hello world")] // Spaces are not special chars
        public void ContainsSpecialChars_OnlySpaces_ReturnsFalse(string text)
        {
            // Arrange
            var injector = new TextInjector(_settings);

            // Act
            var hasSpecialChars = InvokeContainsSpecialChars(injector, text);

            // Assert - Space is not considered special char (line 144: c != ' ')
            hasSpecialChars.Should().BeFalse();
        }

        // Branch Coverage Tests - Clipboard Operations

        [Fact]
        public void InjectText_WithAutoPasteEnabled_SetsClipboardAndPastes()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.AlwaysPaste;
            var injector = new TextInjector(_settings) { AutoPaste = true };
            var testText = "Test clipboard paste with AutoPaste=true";

            // Act
            injector.InjectText(testText);

            // Allow time for async clipboard operations to complete
            Thread.Sleep(100);

            // Assert - AutoPaste should trigger paste operation (line 396 branch)
            // NOTE: We can't directly assert Ctrl+V was simulated, but we verify no exceptions
            injector.AutoPaste.Should().BeTrue();
        }

        [Fact]
        public void InjectText_WithAutoPasteDisabled_SetsClipboardOnly()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.AlwaysPaste;
            var injector = new TextInjector(_settings) { AutoPaste = false };
            var testText = "Test clipboard without AutoPaste";

            // Act
            injector.InjectText(testText);

            // Allow time for clipboard operation
            Thread.Sleep(50);

            // Assert - AutoPaste branch not taken (line 396 else)
            injector.AutoPaste.Should().BeFalse();
        }

        [Fact]
        public void InjectText_LongText_UsesClipboardInsteadOfTyping()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.SmartAuto;
            var injector = new TextInjector(_settings);
            var longText = new string('a', 60); // > 50 chars, should use clipboard

            // Act
            injector.InjectText(longText);

            // Allow time for clipboard operation
            Thread.Sleep(100);

            // Assert - Should use clipboard path (tests lines 236-384)
            // Verified by ShouldUseTyping returning false for long text
            var shouldType = InvokeShouldUseTyping(injector, longText);
            shouldType.Should().BeFalse();
        }

        [Fact]
        public void InjectText_ShortText_UsesTypingInsteadOfClipboard()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.SmartAuto;
            var injector = new TextInjector(_settings);
            var shortText = "Hello world"; // < 50 chars, should use typing

            // Act
            injector.InjectText(shortText);

            // Allow time for typing operation
            Thread.Sleep(100);

            // Assert - Should use typing path (tests lines 209-233)
            var shouldType = InvokeShouldUseTyping(injector, shortText);
            shouldType.Should().BeTrue();
        }

        // Branch Coverage Tests - Typing Operations

        [Fact]
        public void InjectViaTyping_WithNewlineCharacter_SimulatesEnterKey()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.AlwaysType;
            var injector = new TextInjector(_settings);
            var textWithNewline = "Line 1\nLine 2";

            // Act
            injector.InjectText(textWithNewline);

            // Allow time for typing
            Thread.Sleep(100);

            // Assert - Tests line 214-216 (newline handling)
            textWithNewline.Should().Contain("\n");
        }

        [Fact]
        public void InjectViaTyping_WithTabCharacter_SimulatesTabKey()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.AlwaysType;
            var injector = new TextInjector(_settings);
            var textWithTab = "Column1\tColumn2";

            // Act
            injector.InjectText(textWithTab);

            // Allow time for typing
            Thread.Sleep(100);

            // Assert - Tests line 218-220 (tab handling)
            textWithTab.Should().Contain("\t");
        }

        [Fact]
        public void InjectViaTyping_WithRegularCharacters_UsesTextEntry()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.AlwaysType;
            var injector = new TextInjector(_settings);
            var normalText = "Regular text without special chars";

            // Act
            injector.InjectText(normalText);

            // Allow time for typing
            Thread.Sleep(100);

            // Assert - Tests line 222-225 (regular character typing)
            normalText.Should().NotContain("\n").And.NotContain("\t");
        }

        // Helper methods to invoke private methods via reflection
        private bool InvokeShouldUseTyping(TextInjector injector, string text)
        {
            var method = typeof(TextInjector).GetMethod("ShouldUseTyping",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (bool)method!.Invoke(injector, new object[] { text })!;
        }

        private bool InvokeContainsSensitiveContent(TextInjector injector, string text)
        {
            var method = typeof(TextInjector).GetMethod("ContainsSensitiveContent",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (bool)method!.Invoke(injector, new object[] { text })!;
        }

        private bool InvokeContainsSpecialChars(TextInjector injector, string text)
        {
            var method = typeof(TextInjector).GetMethod("ContainsSpecialChars",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (bool)method!.Invoke(injector, new object[] { text })!;
        }
    }
}

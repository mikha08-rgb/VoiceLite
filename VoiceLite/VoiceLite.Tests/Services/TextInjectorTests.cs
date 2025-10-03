using System;
using FluentAssertions;
using Moq;
using VoiceLite.Models;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
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
        public void InjectText_WithNullOrWhitespace_DoesNotThrow(string text)
        {
            // Arrange
            var injector = new TextInjector(_settings);

            // Act
            Action act = () => injector.InjectText(text);

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

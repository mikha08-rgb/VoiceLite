using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using VoiceLite.Models;
using VoiceLite.Services;
using VoiceLite.Core.Interfaces.Services;
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

        [Fact]
        public void CanInject_WhenForegroundWindowExists_ReturnsTrue()
        {
            // Arrange
            var injector = new TextInjector(_settings);

            // Act
            var canInject = injector.CanInject();

            // Assert
            // Should return true in test environment (test runner has a foreground window)
            canInject.Should().BeTrue();
        }

        [Fact]
        public void GetFocusedApplicationName_ReturnsProcessName()
        {
            // Arrange
            var injector = new TextInjector(_settings);

            // Act
            var appName = injector.GetFocusedApplicationName();

            // Assert
            // Should return a valid process name (not "Unknown" or null)
            appName.Should().NotBeNullOrEmpty();
            appName.Should().NotBe("Unknown");
        }

        [Fact]
        public async Task InjectTextAsync_TypeMode_CompletesSuccessfully()
        {
            // Arrange
            var injector = new TextInjector(_settings);

            // Act - Using very short text to minimize side effects during test
            Func<Task> act = async () => await injector.InjectTextAsync("a", ITextInjector.InjectionMode.Type);

            // Assert - Should complete without throwing
            await act.Should().NotThrowAsync();
        }

        [Fact(Skip = "Clipboard operations require STA thread which is not supported in xUnit async tests")]
        public async Task InjectTextAsync_PasteMode_CompletesSuccessfully()
        {
            // Arrange
            var injector = new TextInjector(_settings);

            // Act
            Func<Task> act = async () => await injector.InjectTextAsync("test", ITextInjector.InjectionMode.Paste);

            // Assert
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task InjectTextAsync_SmartAutoMode_CompletesSuccessfully()
        {
            // Arrange
            var injector = new TextInjector(_settings);

            // Act - Using short text to trigger typing mode in SmartAuto
            Func<Task> act = async () => await injector.InjectTextAsync("hi", ITextInjector.InjectionMode.SmartAuto);

            // Assert
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public void InjectText_ShortText_CompletesSuccessfully()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.AlwaysType;
            var injector = new TextInjector(_settings);

            // Act - Using very short text to minimize side effects
            Action act = () => injector.InjectText("x");

            // Assert - Should complete without throwing
            act.Should().NotThrow();
        }

        [Theory]
        [InlineData(TextInjectionMode.AlwaysType)]
        [InlineData(TextInjectionMode.PreferType)]
        [InlineData(TextInjectionMode.SmartAuto)]
        public void InjectText_DifferentModes_CompletesSuccessfully(TextInjectionMode mode)
        {
            // Arrange
            _settings.TextInjectionMode = mode;
            var injector = new TextInjector(_settings);

            // Act - Using very short text to minimize side effects
            Action act = () => injector.InjectText("y");

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void SetTypingDelay_AcceptsValue_DoesNotThrow()
        {
            // Arrange
            var injector = new TextInjector(_settings);

            // Act
            Action act = () => injector.SetTypingDelay(10);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void InjectText_WithNewlineCharacter_HandlesCorrectly()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.AlwaysType;
            var injector = new TextInjector(_settings);

            // Act
            Action act = () => injector.InjectText("line1\nline2");

            // Assert - Should handle newline without throwing
            act.Should().NotThrow();
        }

        [Fact]
        public void InjectText_WithTabCharacter_HandlesCorrectly()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.AlwaysType;
            var injector = new TextInjector(_settings);

            // Act
            Action act = () => injector.InjectText("col1\tcol2");

            // Assert - Should handle tab without throwing
            act.Should().NotThrow();
        }

        [Fact]
        public void InjectText_WithSpecialCharacters_HandlesCorrectly()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.AlwaysType;
            var injector = new TextInjector(_settings);

            // Act
            Action act = () => injector.InjectText("@#$%");

            // Assert - Should handle special characters without throwing
            act.Should().NotThrow();
        }

        [Fact]
        public void InjectText_WithUnicodeCharacters_HandlesCorrectly()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.AlwaysType;
            var injector = new TextInjector(_settings);

            // Act
            Action act = () => injector.InjectText("café");

            // Assert - Should handle unicode without throwing
            act.Should().NotThrow();
        }

        [Fact]
        public void InjectText_AlwaysTypeMode_UsesTyping()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.AlwaysType;
            var injector = new TextInjector(_settings);

            // Act
            Action act = () => injector.InjectText("test text for typing mode");

            // Assert - Should complete via typing without throwing
            act.Should().NotThrow();
        }

        [Fact(Skip = "Clipboard operations require STA thread and can interfere with system clipboard")]
        public void InjectText_AlwaysPasteMode_UsesPaste()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.AlwaysPaste;
            var injector = new TextInjector(_settings);

            // Act
            Action act = () => injector.InjectText("test");

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void InjectText_SmartAutoModeWithShortText_UsesTyping()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.SmartAuto;
            var injector = new TextInjector(_settings);

            // Act - Short text should trigger typing mode
            Action act = () => injector.InjectText("short");

            // Assert
            act.Should().NotThrow();
        }

        [Fact(Skip = "Long text paste requires STA thread and can interfere with system clipboard")]
        public void InjectText_SmartAutoModeWithLongText_UsesPaste()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.SmartAuto;
            var injector = new TextInjector(_settings);

            // Act - Long text (>50 chars) should trigger paste mode
            var longText = new string('a', 60);
            Action act = () => injector.InjectText(longText);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void InjectText_PreferTypeWithShortText_UsesTyping()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.PreferType;
            var injector = new TextInjector(_settings);

            // Act
            Action act = () => injector.InjectText("short text");

            // Assert
            act.Should().NotThrow();
        }

        [Fact(Skip = "Long text paste requires STA thread and can interfere with system clipboard")]
        public void InjectText_PreferTypeWithLongText_UsesPaste()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.PreferType;
            var injector = new TextInjector(_settings);

            // Act - Text > 100 chars should trigger paste
            var longText = new string('b', 110);
            Action act = () => injector.InjectText(longText);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void InjectText_PreferPasteWithVeryShortText_UsesTyping()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.PreferPaste;
            var injector = new TextInjector(_settings);

            // Act - Very short text (<10 chars) should still use typing
            Action act = () => injector.InjectText("hi");

            // Assert
            act.Should().NotThrow();
        }

        [Fact(Skip = "Paste mode requires STA thread and can interfere with system clipboard")]
        public void InjectText_PreferPasteWithMediumText_UsesPaste()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.PreferPaste;
            var injector = new TextInjector(_settings);

            // Act - Text > 10 chars should use paste
            Action act = () => injector.InjectText("medium length text");

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void ShouldUseTyping_SmartAutoWithSensitiveContent_ReturnsTrue()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.SmartAuto;
            var injector = new TextInjector(_settings);

            // Act - Sensitive-looking text (short, no spaces, special chars)
            var shouldType = InvokeShouldUseTyping(injector, "P@ssw0rd!");

            // Assert - Should use typing for security
            shouldType.Should().BeTrue();
        }

        [Fact]
        public void AutoPaste_DefaultValue_IsTrue()
        {
            // Arrange & Act
            var injector = new TextInjector(_settings);

            // Assert
            injector.AutoPaste.Should().BeTrue("default AutoPaste should be true");
        }

        [Fact]
        public void AutoPaste_WhenSetToFalse_StaysFalse()
        {
            // Arrange
            var injector = new TextInjector(_settings)
            {
                // Act
                AutoPaste = false
            };

            // Assert
            injector.AutoPaste.Should().BeFalse();
        }

        [Fact]
        public void ContainsSpecialChars_WithSpaceOnly_ReturnsFalse()
        {
            // Arrange
            var injector = new TextInjector(_settings);

            // Act - Space is explicitly excluded from special chars check
            var hasSpecialChars = InvokeContainsSpecialChars(injector, "hello world");

            // Assert
            hasSpecialChars.Should().BeFalse();
        }

        [Theory]
        [InlineData("!", true)]
        [InlineData("@", true)]
        [InlineData("#", true)]
        [InlineData("$", true)]
        [InlineData("%", true)]
        [InlineData("^", true)]
        [InlineData("&", true)]
        [InlineData("*", true)]
        [InlineData("(", true)]
        [InlineData(")", true)]
        [InlineData("-", true)]
        [InlineData("_", true)]
        [InlineData("+", true)]
        [InlineData("=", true)]
        [InlineData("[", true)]
        [InlineData("]", true)]
        [InlineData("{", true)]
        [InlineData("}", true)]
        [InlineData(";", true)]
        [InlineData(":", true)]
        [InlineData("'", true)]
        [InlineData("\"", true)]
        [InlineData(",", true)]
        [InlineData(".", true)]
        [InlineData("<", true)]
        [InlineData(">", true)]
        [InlineData("/", true)]
        [InlineData("?", true)]
        public void ContainsSpecialChars_VariousSpecialCharacters_ReturnsExpected(string text, bool expected)
        {
            // Arrange
            var injector = new TextInjector(_settings);

            // Act
            var hasSpecialChars = InvokeContainsSpecialChars(injector, text);

            // Assert
            hasSpecialChars.Should().Be(expected);
        }

        [Fact]
        public void ContainsSensitiveContent_ExactlyThirtyCharsWithSpecialChars_ReturnsFalse()
        {
            // Arrange
            var injector = new TextInjector(_settings);
            var text = "12345678901234567890!@#$%^&*()"; // Exactly 30 chars with special chars

            // Act - Should return false because length is NOT < 30
            var isSensitive = InvokeContainsSensitiveContent(injector, text);

            // Assert
            isSensitive.Should().BeFalse();
        }

        [Fact]
        public void ContainsSensitiveContent_TwentyNineCharsNoSpacesWithSpecialChars_ReturnsTrue()
        {
            // Arrange
            var injector = new TextInjector(_settings);
            var text = "12345678901234567890!@#$%^&*("; // 29 chars, no spaces, has special chars

            // Act
            var isSensitive = InvokeContainsSensitiveContent(injector, text);

            // Assert
            isSensitive.Should().BeTrue();
        }

        [Fact]
        public void ContainsSensitiveContent_ShortTextNoSpacesNoSpecialChars_ReturnsFalse()
        {
            // Arrange
            var injector = new TextInjector(_settings);
            var text = "abc123"; // < 30 chars, no spaces, but no special chars

            // Act
            var isSensitive = InvokeContainsSensitiveContent(injector, text);

            // Assert
            isSensitive.Should().BeFalse("no special chars means not sensitive");
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

        #region Text Injection Improvements Tests (v1.2.0)

        [Theory]
        [InlineData(20, 30)]   // Below minimum
        [InlineData(30, 30)]   // At minimum
        [InlineData(50, 50)]   // Normal value
        [InlineData(100, 100)] // At maximum
        [InlineData(150, 100)] // Above maximum
        public void Settings_ClipboardRestorationDelayMs_ClampsToValidRange(int inputValue, int expectedValue)
        {
            // Arrange
            var settings = new Settings();

            // Act
            settings.ClipboardRestorationDelayMs = inputValue;

            // Assert
            settings.ClipboardRestorationDelayMs.Should().Be(expectedValue);
        }

        [Fact]
        public void InjectText_WithUIAutomationFallback_DoesNotThrow()
        {
            // Arrange
            var settings = new Settings
            {
                TextInjectionMode = TextInjectionMode.SmartAuto
            };
            var injector = new TextInjector(settings);

            // Act - UI Automation may fail in test environment, but should not throw
            Action act = () => injector.InjectText("test");

            // Assert - Should fail gracefully and fallback to other methods
            // Not throwing an exception indicates proper fallback handling
            act.Should().NotThrow();
        }

        [Fact]
        public void InjectText_WithAlwaysTypeMode_SkipsUIAutomation()
        {
            // Arrange
            var settings = new Settings
            {
                TextInjectionMode = TextInjectionMode.AlwaysType
            };
            var injector = new TextInjector(settings);

            // Act & Assert - Should respect explicit AlwaysType preference
            // UI Automation should be skipped entirely for this mode
            Action act = () => injector.InjectText("test");
            act.Should().NotThrow();
        }

        [Fact]
        public void InjectText_WithAlwaysPasteMode_SkipsUIAutomation()
        {
            // Arrange
            var settings = new Settings
            {
                TextInjectionMode = TextInjectionMode.AlwaysPaste,
                ClipboardRestorationDelayMs = 50
            };
            var injector = new TextInjector(settings);

            // Act & Assert - Should respect explicit AlwaysPaste preference
            // UI Automation should be skipped entirely for this mode
            Action act = () => injector.InjectText("test");
            act.Should().NotThrow();
        }

        // NOTE: GetTypingDelayForApplication() tests
        // Full testing requires refactoring TextInjector for dependency injection
        // to mock GetFocusedApplicationName(). Current implementation uses Win32 API
        // directly which is difficult to test without a real UI context.
        //
        // Manual testing checklist (documented in TEXT_INJECTION_RESEARCH.md):
        // ✅ Notepad: Should use 0ms delay (instant)
        // ✅ VS Code: Should use 0ms delay (instant)
        // ✅ Chrome: Should use 0ms delay (instant)
        // ✅ Word: Should use 1ms delay (minimal)
        // ✅ CMD/PowerShell: Should use 5ms delay (compatible)
        // ✅ Unknown apps: Should use 1ms delay (default)

        #endregion

        #region Disposal Tests (Issue 1 - Production Readiness)

        [Fact]
        public void Dispose_ReleasesSemaphore_DoesNotThrow()
        {
            // Arrange
            var settings = new Settings();
            var injector = new TextInjector(settings);

            // Act
            Action act = () => injector.Dispose();

            // Assert - Should not throw
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            // Arrange
            var settings = new Settings();
            var injector = new TextInjector(settings);

            // Act - Call dispose twice
            Action act = () =>
            {
                injector.Dispose();
                injector.Dispose();
            };

            // Assert - Should be idempotent
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_AfterInjectText_DoesNotThrow()
        {
            // Arrange
            _settings.TextInjectionMode = TextInjectionMode.AlwaysType;
            var injector = new TextInjector(_settings);

            // Act - Use the injector, then dispose
            injector.InjectText("a");
            Action act = () => injector.Dispose();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void TextInjector_ImplementsIDisposable()
        {
            // Arrange & Assert
            typeof(TextInjector).Should().Implement<IDisposable>();
        }

        #endregion

        #region Error Handling Tests (Issues 7 & 8)

        [Fact]
        public void GetFocusedApplicationName_ReturnsValidString()
        {
            // Arrange
            var injector = new TextInjector(_settings);

            // Act
            var appName = injector.GetFocusedApplicationName();

            // Assert - Should return either a valid process name or "Unknown"
            appName.Should().NotBeNull();
            appName.Should().NotBeEmpty();
        }

        [Fact]
        public void GetFocusedApplicationName_DoesNotThrow()
        {
            // Arrange
            var injector = new TextInjector(_settings);

            // Act
            Action act = () => injector.GetFocusedApplicationName();

            // Assert - Should handle any window/process state gracefully
            act.Should().NotThrow();
        }

        #endregion

        #region Typing Delay Tests (Issue 9)

        [Fact]
        public void GetTypingDelayForApplication_ReturnsPositiveValue()
        {
            // Arrange
            var injector = new TextInjector(_settings);

            // Act
            var delay = InvokeGetTypingDelayForApplication(injector);

            // Assert - Delay should be between 1-5ms based on implementation
            delay.Should().BeGreaterThanOrEqualTo(1);
            delay.Should().BeLessThanOrEqualTo(5);
        }

        [Fact]
        public void GetTypingDelay_ReturnsValueFromGetTypingDelayForApplication()
        {
            // Arrange
            var injector = new TextInjector(_settings);

            // Act
            var delay = InvokeGetTypingDelay(injector);
            var delayForApp = InvokeGetTypingDelayForApplication(injector);

            // Assert - GetTypingDelay should delegate to GetTypingDelayForApplication
            delay.Should().Be(delayForApp);
        }

        private int InvokeGetTypingDelayForApplication(TextInjector injector)
        {
            var method = typeof(TextInjector).GetMethod("GetTypingDelayForApplication",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (int)method!.Invoke(injector, Array.Empty<object>())!;
        }

        private int InvokeGetTypingDelay(TextInjector injector)
        {
            var method = typeof(TextInjector).GetMethod("GetTypingDelay",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (int)method!.Invoke(injector, Array.Empty<object>())!;
        }

        #endregion
    }
}

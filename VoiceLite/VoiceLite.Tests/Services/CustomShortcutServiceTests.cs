using System;
using System.Collections.Generic;
using FluentAssertions;
using VoiceLite.Models;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    public class CustomShortcutServiceTests : IDisposable
    {
        private Settings _settings;
        private CustomShortcutService _service;

        public CustomShortcutServiceTests()
        {
            _settings = new Settings
            {
                CustomShortcuts = new List<CustomShortcut>()
            };
            _service = new CustomShortcutService(_settings);
        }

        public void Dispose()
        {
            // Cleanup if needed
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullSettings_ThrowsArgumentNullException()
        {
            // Act & Assert
            Action act = () => new CustomShortcutService(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Constructor_WithSettingsHavingNullShortcuts_InitializesEmptyList()
        {
            // Arrange
            var settings = new Settings { CustomShortcuts = null! };

            // Act
            var service = new CustomShortcutService(settings);

            // Assert
            settings.CustomShortcuts.Should().NotBeNull();
            settings.CustomShortcuts.Should().BeEmpty();
        }

        #endregion

        #region ProcessShortcuts Tests

        [Fact]
        public void ProcessShortcuts_WithNullText_ReturnsNull()
        {
            // Act
            var result = _service.ProcessShortcuts(null!);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void ProcessShortcuts_WithEmptyText_ReturnsEmpty()
        {
            // Act
            var result = _service.ProcessShortcuts("");

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void ProcessShortcuts_WithWhitespaceText_ReturnsWhitespace()
        {
            // Act
            var result = _service.ProcessShortcuts("   ");

            // Assert
            result.Should().Be("   ");
        }

        [Fact]
        public void ProcessShortcuts_WithNoShortcuts_ReturnsOriginalText()
        {
            // Arrange
            var text = "Hello world";

            // Act
            var result = _service.ProcessShortcuts(text);

            // Assert
            result.Should().Be(text);
        }

        [Fact]
        public void ProcessShortcuts_WithSingleEnabledShortcut_ReplacesText()
        {
            // Arrange
            _settings.CustomShortcuts.Add(new CustomShortcut
            {
                Trigger = "my email",
                Replacement = "john@example.com",
                IsEnabled = true
            });

            // Act
            var result = _service.ProcessShortcuts("Send it to my email please");

            // Assert
            result.Should().Be("Send it to john@example.com please");
        }

        [Fact]
        public void ProcessShortcuts_CaseInsensitive_ReplacesRegardlessOfCase()
        {
            // Arrange
            _settings.CustomShortcuts.Add(new CustomShortcut
            {
                Trigger = "my email",
                Replacement = "john@example.com",
                IsEnabled = true
            });

            // Act
            var result1 = _service.ProcessShortcuts("My Email is here");
            var result2 = _service.ProcessShortcuts("MY EMAIL is here");
            var result3 = _service.ProcessShortcuts("my email is here");

            // Assert
            result1.Should().Be("john@example.com is here");
            result2.Should().Be("john@example.com is here");
            result3.Should().Be("john@example.com is here");
        }

        [Fact]
        public void ProcessShortcuts_WithDisabledShortcut_DoesNotReplace()
        {
            // Arrange
            _settings.CustomShortcuts.Add(new CustomShortcut
            {
                Trigger = "my email",
                Replacement = "john@example.com",
                IsEnabled = false
            });

            // Act
            var result = _service.ProcessShortcuts("Send it to my email please");

            // Assert
            result.Should().Be("Send it to my email please");
        }

        [Fact]
        public void ProcessShortcuts_WithMultipleShortcuts_ReplacesAll()
        {
            // Arrange
            _settings.CustomShortcuts.Add(new CustomShortcut
            {
                Trigger = "my email",
                Replacement = "john@example.com",
                IsEnabled = true
            });
            _settings.CustomShortcuts.Add(new CustomShortcut
            {
                Trigger = "my phone",
                Replacement = "(555) 123-4567",
                IsEnabled = true
            });

            // Act
            var result = _service.ProcessShortcuts("Contact me at my email or my phone");

            // Assert
            result.Should().Be("Contact me at john@example.com or (555) 123-4567");
        }

        [Fact]
        public void ProcessShortcuts_WithMultipleOccurrences_ReplacesAll()
        {
            // Arrange
            _settings.CustomShortcuts.Add(new CustomShortcut
            {
                Trigger = "btw",
                Replacement = "by the way",
                IsEnabled = true
            });

            // Act
            var result = _service.ProcessShortcuts("btw I forgot to mention, btw this is important");

            // Assert
            result.Should().Be("by the way I forgot to mention, by the way this is important");
        }

        [Fact]
        public void ProcessShortcuts_WithEmptyTrigger_SkipsShortcut()
        {
            // Arrange
            _settings.CustomShortcuts.Add(new CustomShortcut
            {
                Trigger = "",
                Replacement = "should not appear",
                IsEnabled = true
            });

            // Act
            var result = _service.ProcessShortcuts("Hello world");

            // Assert
            result.Should().Be("Hello world");
        }

        [Fact]
        public void ProcessShortcuts_WithNullReplacement_ReplacesWithEmpty()
        {
            // Arrange
            _settings.CustomShortcuts.Add(new CustomShortcut
            {
                Trigger = "remove this",
                Replacement = null!,
                IsEnabled = true
            });

            // Act
            var result = _service.ProcessShortcuts("Please remove this from text");

            // Assert
            result.Should().Be("Please  from text");
        }

        [Fact]
        public void ProcessShortcuts_WithSpecialCharacters_ReplacesCorrectly()
        {
            // Arrange
            _settings.CustomShortcuts.Add(new CustomShortcut
            {
                Trigger = "lambda",
                Replacement = "λ",
                IsEnabled = true
            });
            _settings.CustomShortcuts.Add(new CustomShortcut
            {
                Trigger = "arrow",
                Replacement = "→",
                IsEnabled = true
            });

            // Act
            var result = _service.ProcessShortcuts("The lambda function arrow points here");

            // Assert
            result.Should().Be("The λ function → points here");
        }

        [Fact]
        public void ProcessShortcuts_WithLongReplacement_ReplacesCorrectly()
        {
            // Arrange
            var longReplacement = @"Dear Sir/Madam,

I am writing to inform you that...

Best regards,
John Doe";
            _settings.CustomShortcuts.Add(new CustomShortcut
            {
                Trigger = "email template",
                Replacement = longReplacement,
                IsEnabled = true
            });

            // Act
            var result = _service.ProcessShortcuts("Insert email template here");

            // Assert
            result.Should().Contain("Dear Sir/Madam");
            result.Should().Contain("Best regards");
        }

        [Fact]
        public void ProcessShortcuts_MixedEnabledAndDisabled_OnlyReplacesEnabled()
        {
            // Arrange
            _settings.CustomShortcuts.Add(new CustomShortcut
            {
                Trigger = "enabled",
                Replacement = "REPLACED",
                IsEnabled = true
            });
            _settings.CustomShortcuts.Add(new CustomShortcut
            {
                Trigger = "disabled",
                Replacement = "SHOULD_NOT_APPEAR",
                IsEnabled = false
            });

            // Act
            var result = _service.ProcessShortcuts("This is enabled and disabled");

            // Assert
            result.Should().Be("This is REPLACED and disabled");
        }

        #endregion
    }
}

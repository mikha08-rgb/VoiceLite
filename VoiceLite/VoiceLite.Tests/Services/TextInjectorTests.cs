using System;
using AwesomeAssertions;
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
            _settings = new Settings();
        }

        [Fact]
        public void Constructor_WithNullSettings_ThrowsArgumentNullException()
        {
            Action act = () => new TextInjector(null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("settings");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t\n")]
        public void InjectText_WithNullOrWhitespace_DoesNotThrow(string? text)
        {
            using var injector = new TextInjector(_settings);
            Action act = () => injector.InjectText(text!);
            act.Should().NotThrow();
        }

        [Fact]
        public void CanInject_InTestEnvironment_ReturnsTrue()
        {
            // The test runner has a foreground window — GetForegroundWindow() should return non-zero.
            using var injector = new TextInjector(_settings);
            injector.CanInject().Should().BeTrue();
        }

        [Fact]
        public void GetFocusedApplicationName_ReturnsNonEmptyString()
        {
            using var injector = new TextInjector(_settings);
            var appName = injector.GetFocusedApplicationName();
            appName.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void Dispose_DoesNotThrow()
        {
            var injector = new TextInjector(_settings);
            Action act = () => injector.Dispose();
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_CalledTwice_IsIdempotent()
        {
            var injector = new TextInjector(_settings);
            Action act = () =>
            {
                injector.Dispose();
                injector.Dispose();
            };
            act.Should().NotThrow();
        }

        [Fact]
        public void TextInjector_ImplementsIDisposable()
        {
            typeof(TextInjector).Should().Implement<IDisposable>();
        }
    }
}

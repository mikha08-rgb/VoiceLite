using FluentAssertions;
using VoiceLite.Utilities;
using Xunit;

namespace VoiceLite.Tests.Utilities
{
    [Trait("Category", "Unit")]
    public class TimingConstantsTests
    {
        [Fact]
        public void ClickDebounceMs_HasCorrectValue()
        {
            TimingConstants.ClickDebounceMs.Should().Be(300);
        }

        [Fact]
        public void HotkeyDebounceMs_HasCorrectValue()
        {
            TimingConstants.HotkeyDebounceMs.Should().Be(250);
        }

        [Fact]
        public void SettingsSaveDebounceMs_HasCorrectValue()
        {
            TimingConstants.SettingsSaveDebounceMs.Should().Be(500);
        }

        [Fact]
        public void StatusRevertDelayMs_HasCorrectValue()
        {
            TimingConstants.StatusRevertDelayMs.Should().Be(1500);
        }

        [Fact]
        public void FileCleanupRetryDelayMs_HasCorrectValue()
        {
            TimingConstants.FileCleanupRetryDelayMs.Should().Be(100);
        }

        [Fact]
        public void FileCleanupMaxRetries_HasCorrectValue()
        {
            TimingConstants.FileCleanupMaxRetries.Should().Be(3);
        }

        [Fact]
        public void TranscriptionTextResetDelayMs_HasCorrectValue()
        {
            TimingConstants.TranscriptionTextResetDelayMs.Should().Be(3000);
        }

        [Fact]
        public void AllDebounceValues_ArePositive()
        {
            TimingConstants.ClickDebounceMs.Should().BeGreaterThan(0);
            TimingConstants.HotkeyDebounceMs.Should().BeGreaterThan(0);
            TimingConstants.SettingsSaveDebounceMs.Should().BeGreaterThan(0);
        }

        [Fact]
        public void AllDelayValues_ArePositive()
        {
            TimingConstants.StatusRevertDelayMs.Should().BeGreaterThan(0);
            TimingConstants.FileCleanupRetryDelayMs.Should().BeGreaterThan(0);
            TimingConstants.TranscriptionTextResetDelayMs.Should().BeGreaterThan(0);
        }

        [Fact]
        public void FileCleanupMaxRetries_IsReasonable()
        {
            // Should have at least 1 retry, but not excessive
            TimingConstants.FileCleanupMaxRetries.Should().BeInRange(1, 10);
        }

        [Fact]
        public void HotkeyDebounce_IsShorterThanClickDebounce()
        {
            // Hotkey debounce should be responsive, slightly shorter than click debounce
            TimingConstants.HotkeyDebounceMs.Should().BeLessThanOrEqualTo(TimingConstants.ClickDebounceMs);
        }

        [Fact]
        public void SettingsSaveDebounce_IsLongestDebounce()
        {
            // Settings save should batch writes, so longest debounce
            TimingConstants.SettingsSaveDebounceMs.Should().BeGreaterThanOrEqualTo(TimingConstants.ClickDebounceMs);
            TimingConstants.SettingsSaveDebounceMs.Should().BeGreaterThanOrEqualTo(TimingConstants.HotkeyDebounceMs);
        }
    }
}

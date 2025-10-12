using FluentAssertions;
using System;
using System.Globalization;
using VoiceLite.Utilities;
using Xunit;

namespace VoiceLite.Tests.Utilities
{
    [Trait("Category", "Unit")]
    public class RelativeTimeConverterTests
    {
        private readonly RelativeTimeConverter _converter = new();

        [Fact]
        public void Convert_LessThan60Seconds_ReturnsJustNow()
        {
            var timestamp = DateTime.Now.AddSeconds(-30);
            var result = _converter.Convert(timestamp, typeof(string), null, CultureInfo.CurrentCulture);

            result.Should().Be("Just now");
        }

        [Fact]
        public void Convert_ExactlyZeroSeconds_ReturnsJustNow()
        {
            var timestamp = DateTime.Now;
            var result = _converter.Convert(timestamp, typeof(string), null, CultureInfo.CurrentCulture);

            result.Should().Be("Just now");
        }

        [Fact]
        public void Convert_59Seconds_ReturnsJustNow()
        {
            var timestamp = DateTime.Now.AddSeconds(-59);
            var result = _converter.Convert(timestamp, typeof(string), null, CultureInfo.CurrentCulture);

            result.Should().Be("Just now");
        }

        [Fact]
        public void Convert_1Minute_Returns1mAgo()
        {
            var timestamp = DateTime.Now.AddMinutes(-1);
            var result = _converter.Convert(timestamp, typeof(string), null, CultureInfo.CurrentCulture);

            result.Should().Be("1m ago");
        }

        [Fact]
        public void Convert_5Minutes_Returns5mAgo()
        {
            var timestamp = DateTime.Now.AddMinutes(-5);
            var result = _converter.Convert(timestamp, typeof(string), null, CultureInfo.CurrentCulture);

            result.Should().Be("5m ago");
        }

        [Fact]
        public void Convert_59Minutes_Returns59mAgo()
        {
            var timestamp = DateTime.Now.AddMinutes(-59);
            var result = _converter.Convert(timestamp, typeof(string), null, CultureInfo.CurrentCulture);

            result.Should().Be("59m ago");
        }

        [Fact]
        public void Convert_1Hour_Returns1hAgo()
        {
            var timestamp = DateTime.Now.AddHours(-1);
            var result = _converter.Convert(timestamp, typeof(string), null, CultureInfo.CurrentCulture);

            result.Should().Be("1h ago");
        }

        [Fact]
        public void Convert_5Hours_Returns5hAgo()
        {
            var timestamp = DateTime.Now.AddHours(-5);
            var result = _converter.Convert(timestamp, typeof(string), null, CultureInfo.CurrentCulture);

            result.Should().Be("5h ago");
        }

        [Fact]
        public void Convert_23Hours_Returns23hAgo()
        {
            var timestamp = DateTime.Now.AddHours(-23);
            var result = _converter.Convert(timestamp, typeof(string), null, CultureInfo.CurrentCulture);

            result.Should().Be("23h ago");
        }

        [Fact]
        public void Convert_1Day_Returns1dAgo()
        {
            var timestamp = DateTime.Now.AddDays(-1);
            var result = _converter.Convert(timestamp, typeof(string), null, CultureInfo.CurrentCulture);

            result.Should().Be("1d ago");
        }

        [Fact]
        public void Convert_3Days_Returns3dAgo()
        {
            var timestamp = DateTime.Now.AddDays(-3);
            var result = _converter.Convert(timestamp, typeof(string), null, CultureInfo.CurrentCulture);

            result.Should().Be("3d ago");
        }

        [Fact]
        public void Convert_6Days_Returns6dAgo()
        {
            var timestamp = DateTime.Now.AddDays(-6);
            var result = _converter.Convert(timestamp, typeof(string), null, CultureInfo.CurrentCulture);

            result.Should().Be("6d ago");
        }

        [Fact]
        public void Convert_7Days_ReturnsFormattedDate()
        {
            var timestamp = DateTime.Now.AddDays(-7);
            var result = _converter.Convert(timestamp, typeof(string), null, CultureInfo.CurrentCulture);

            // Should return formatted date like "Jan 3, 2:30 PM"
            result.Should().NotBe("7d ago");
            result.Should().NotBeNull();
            result.ToString()!.Should().Contain(timestamp.ToString("MMM"));
        }

        [Fact]
        public void Convert_30Days_ReturnsFormattedDate()
        {
            var timestamp = DateTime.Now.AddDays(-30);
            var result = _converter.Convert(timestamp, typeof(string), null, CultureInfo.CurrentCulture);

            // Should return formatted date like "Dec 11, 2:30 PM"
            result.Should().NotBeNull();
            result.ToString()!.Should().Contain(timestamp.ToString("MMM"));
        }

        [Fact]
        public void Convert_NonDateTimeValue_ReturnsUnknown()
        {
            var result = _converter.Convert("not a datetime", typeof(string), null, CultureInfo.CurrentCulture);

            result.Should().Be("Unknown");
        }

        [Fact]
        public void Convert_NullValue_ReturnsUnknown()
        {
            var result = _converter.Convert(null!, typeof(string), null, CultureInfo.CurrentCulture);

            result.Should().Be("Unknown");
        }

        [Fact]
        public void ConvertBack_ThrowsNotImplementedException()
        {
            Action act = () => _converter.ConvertBack("Just now", typeof(DateTime), null, CultureInfo.CurrentCulture);

            act.Should().Throw<NotImplementedException>()
                .WithMessage("RelativeTimeConverter does not support two-way binding");
        }
    }
}

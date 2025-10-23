using FluentAssertions;
using System.Windows.Media;
using VoiceLite.Utilities;
using Xunit;

namespace VoiceLite.Tests.Utilities
{
    [Trait("Category", "Unit")]
    public class StatusColorsTests
    {
        [Fact]
        public void Recording_HasCorrectColor()
        {
            StatusColors.Recording.Should().Be(Color.FromRgb(231, 76, 60));
        }

        [Fact]
        public void Processing_HasCorrectColor()
        {
            StatusColors.Processing.Should().Be(Color.FromRgb(243, 156, 18));
        }

        [Fact]
        public void Ready_HasCorrectColor()
        {
            StatusColors.Ready.Should().Be(Color.FromRgb(39, 174, 96));
        }

        [Fact]
        public void Inactive_HasCorrectColor()
        {
            StatusColors.Inactive.Should().Be(Colors.Gray);
        }

        [Fact]
        public void Error_HasCorrectColor()
        {
            StatusColors.Error.Should().Be(Colors.Red);
        }

        [Fact]
        public void Info_HasCorrectColor()
        {
            StatusColors.Info.Should().Be(Colors.Blue);
        }

        [Fact]
        public void AllColors_AreNotNull()
        {
            StatusColors.Recording.Should().NotBe(default(Color));
            StatusColors.Processing.Should().NotBe(default(Color));
            StatusColors.Ready.Should().NotBe(default(Color));
            StatusColors.Inactive.Should().NotBe(default(Color));
            StatusColors.Error.Should().NotBe(default(Color));
            StatusColors.Info.Should().NotBe(default(Color));
        }

        [Fact]
        public void RecordingColor_IsRed()
        {
            // Recording should be a red tone
            StatusColors.Recording.R.Should().BeGreaterThan(200);
            StatusColors.Recording.G.Should().BeLessThan(100);
            StatusColors.Recording.B.Should().BeLessThan(100);
        }

        [Fact]
        public void ProcessingColor_IsOrange()
        {
            // Processing should be an orange tone
            StatusColors.Processing.R.Should().BeGreaterThan(200);
            StatusColors.Processing.G.Should().BeGreaterThan(100);
            StatusColors.Processing.B.Should().BeLessThan(50);
        }

        [Fact]
        public void ReadyColor_IsGreen()
        {
            // Ready should be a green tone
            StatusColors.Ready.R.Should().BeLessThan(100);
            StatusColors.Ready.G.Should().BeGreaterThan(150);
            StatusColors.Ready.B.Should().BeLessThan(150);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Windows;
using AwesomeAssertions;
using VoiceLite.Models;
using Xunit;

namespace VoiceLite.Tests.Models
{
    public class TranscriptionModelInfoTests
    {
        [Fact]
        public void FileSizeDisplay_FormatsBytes_Correctly()
        {
            var model = new TranscriptionModelInfo { FileSizeBytes = 1024 };
            model.FileSizeDisplay.Should().Be("1 KB");
        }

        [Fact]
        public void FileSizeDisplay_FormatsMegabytes_Correctly()
        {
            var model = new TranscriptionModelInfo { FileSizeBytes = 5 * 1024 * 1024 };
            model.FileSizeDisplay.Should().Contain("MB");
        }

        [Fact]
        public void FileSizeDisplay_FormatsGigabytes_Correctly()
        {
            var model = new TranscriptionModelInfo { FileSizeBytes = 2L * 1024 * 1024 * 1024 };
            model.FileSizeDisplay.Should().Contain("GB");
        }

        [Fact]
        public void SpeedRatingWidth_CalculatesCorrectly()
        {
            var model = new TranscriptionModelInfo { SpeedRating = 5 };
            model.SpeedRatingWidth.Should().Be(150);

            model = new TranscriptionModelInfo { SpeedRating = 3 };
            model.SpeedRatingWidth.Should().Be(90);
        }

        [Fact]
        public void AccuracyRatingWidth_CalculatesCorrectly()
        {
            var model = new TranscriptionModelInfo { AccuracyRating = 5 };
            model.AccuracyRatingWidth.Should().Be(150);

            model = new TranscriptionModelInfo { AccuracyRating = 2 };
            model.AccuracyRatingWidth.Should().Be(60);
        }

        // GGML-era tests (5-model lineup, IsInstalled scan, RAM-based recommendation) were
        // deleted 2026-07-17 — the lineup collapsed to a single Parakeet entry in v2.0 and the
        // old assertions were permanently skipped false-green. See docs/audit/COMPLEXITY.md.
    }
}

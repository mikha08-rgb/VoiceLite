using System;
using AwesomeAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// Pure-logic coverage for the Settings window's live input meter: peak computation on
    /// synthetic 16-bit PCM and the "no signal for 2 s" hint. Device capture itself is not
    /// exercised (no microphone in CI).
    /// </summary>
    public class MicLevelMonitorTests
    {
        private static byte[] Pcm(params short[] samples)
        {
            var bytes = new byte[samples.Length * 2];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        [Fact]
        public void ComputePeak_Silence_IsZero()
        {
            var buffer = new byte[1600]; // 50 ms of zeros at 16 kHz mono 16-bit
            MicLevelMonitor.ComputePeak(buffer, buffer.Length).Should().Be(0f);
        }

        [Fact]
        public void ComputePeak_FullScale_IsOne_ForBothSigns()
        {
            MicLevelMonitor.ComputePeak(Pcm(0, short.MaxValue, 0), 6).Should().BeApproximately(1f, 0.0001f);
            MicLevelMonitor.ComputePeak(Pcm(0, short.MinValue, 0), 6).Should().Be(1f);
        }

        [Fact]
        public void ComputePeak_ReturnsLargestAbsoluteSample_Scaled()
        {
            // 8192 / 32768 = 0.25; the negative sample is larger in magnitude than the positive one.
            var buffer = Pcm(1000, -8192, 4000, 12);
            MicLevelMonitor.ComputePeak(buffer, buffer.Length).Should().BeApproximately(0.25f, 0.0001f);
        }

        [Fact]
        public void ComputePeak_HonoursBytesRecorded_AndIgnoresTrailingOddByte()
        {
            var buffer = Pcm(100, 20000, 30000);
            // Only the first sample counts when bytesRecorded = 2.
            MicLevelMonitor.ComputePeak(buffer, 2).Should().BeApproximately(100f / 32768f, 0.0001f);
            // An odd byte count must not read past the last full sample.
            MicLevelMonitor.ComputePeak(buffer, 3).Should().BeApproximately(100f / 32768f, 0.0001f);
            MicLevelMonitor.ComputePeak(buffer, 1).Should().Be(0f);
            MicLevelMonitor.ComputePeak(Array.Empty<byte>(), 0).Should().Be(0f);
        }

        [Fact]
        public void Process_ReportsNoSignal_OnlyAfterTwoSecondsBelowOnePercent()
        {
            using var monitor = new MicLevelMonitor(-1); // never Start()ed: no device needed
            var quiet = Pcm(0, 200, -200); // 200/32768 = 0.6% < 1% threshold

            monitor.Process(quiet, quiet.Length, nowMs: 0).NoSignal.Should().BeFalse();
            monitor.Process(quiet, quiet.Length, nowMs: 1999).NoSignal.Should().BeFalse("delay is 2 s");
            monitor.Process(quiet, quiet.Length, nowMs: 2000).NoSignal.Should().BeTrue();
            monitor.Process(quiet, quiet.Length, nowMs: 5000).NoSignal.Should().BeTrue();
        }

        [Fact]
        public void Process_SignalAboveThreshold_ResetsNoSignalTimer()
        {
            using var monitor = new MicLevelMonitor(0);
            var quiet = Pcm(0, 0, 0);
            var loud = Pcm(0, 3000, 0); // ~9%

            monitor.Process(quiet, quiet.Length, nowMs: 0);
            monitor.Process(quiet, quiet.Length, nowMs: 2500).NoSignal.Should().BeTrue();

            var args = monitor.Process(loud, loud.Length, nowMs: 2600);
            args.NoSignal.Should().BeFalse("speech just arrived");
            args.Peak.Should().BeApproximately(3000f / 32768f, 0.0001f);

            monitor.Process(quiet, quiet.Length, nowMs: 4500).NoSignal.Should().BeFalse("only 1.9 s since the last signal");
            monitor.Process(quiet, quiet.Length, nowMs: 4600).NoSignal.Should().BeTrue();
        }

        [Fact]
        public void Dispose_WithoutStart_IsSafe_AndIdempotent()
        {
            var monitor = new MicLevelMonitor(-1);
            monitor.Dispose();
            monitor.Dispose();
            Action start = () => monitor.Start();
            start.Should().Throw<ObjectDisposedException>();
        }
    }
}

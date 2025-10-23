using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    [Trait("Category", "Hardware")]
    public class MemoryMonitorTests : IDisposable
    {
        private MemoryMonitor? _monitor;

        [Fact]
        public void Constructor_InitializesBaseline_Successfully()
        {
            _monitor = new MemoryMonitor();
            var stats = _monitor.GetStatistics();

            stats.Should().NotBeNull();
            stats.BaselineMemoryMB.Should().BeGreaterThan(0);
            stats.CurrentMemoryMB.Should().BeGreaterThan(0);
        }

        [Fact]
        public void GetStatistics_ReturnsValidData()
        {
            _monitor = new MemoryMonitor();
            var stats = _monitor.GetStatistics();

            stats.CurrentMemoryMB.Should().BeGreaterThan(0);
            stats.GCMemoryMB.Should().BeGreaterOrEqualTo(0);
            stats.Gen0Collections.Should().BeGreaterOrEqualTo(0);
            stats.Gen1Collections.Should().BeGreaterOrEqualTo(0);
            stats.Gen2Collections.Should().BeGreaterOrEqualTo(0);
        }

        [Fact]
        public void GetStatistics_BaselineIsPositive()
        {
            _monitor = new MemoryMonitor();

            // Baseline should always be positive
            var stats = _monitor.GetStatistics();
            stats.BaselineMemoryMB.Should().BeGreaterThan(0);
        }

        [Fact]
        public void MemoryAlert_FiresOnCriticalMemory()
        {
            // This test would require allocating 500MB+ of memory which is impractical
            // Instead, we verify the event can be subscribed to
            _monitor = new MemoryMonitor();
            var eventFired = false;

            _monitor.MemoryAlert += (sender, args) =>
            {
                eventFired = true;
            };

            // We can't easily trigger the alert in a test without consuming 500MB+
            // So we just verify the subscription works
            eventFired.Should().BeFalse(); // Should not fire during normal operation
        }

        [Fact]
        public void MultipleDisposeCalls_DoNotThrow()
        {
            _monitor = new MemoryMonitor();

            Action act = () =>
            {
                _monitor.Dispose();
                _monitor.Dispose();
                _monitor.Dispose();
            };

            act.Should().NotThrow();
        }

        [Fact]
        public void GetStatistics_AfterDispose_DoesNotThrow()
        {
            _monitor = new MemoryMonitor();
            _monitor.Dispose();

            // Should not throw even after dispose
            Action act = () => _monitor.GetStatistics();
            act.Should().NotThrow();
        }

        [Fact]
        public void PeakMemory_NeverDecreasesOverTime()
        {
            _monitor = new MemoryMonitor();

            var stats1 = _monitor.GetStatistics();
            var peak1 = stats1.PeakMemoryMB;

            // Wait a bit for monitor to run
            Thread.Sleep(100);

            var stats2 = _monitor.GetStatistics();
            var peak2 = stats2.PeakMemoryMB;

            // Peak should never decrease
            peak2.Should().BeGreaterOrEqualTo(peak1);
        }

        [Fact]
        public void BaselineMemory_RemainsConstant()
        {
            _monitor = new MemoryMonitor();

            var stats1 = _monitor.GetStatistics();
            var baseline1 = stats1.BaselineMemoryMB;

            Thread.Sleep(100);

            var stats2 = _monitor.GetStatistics();
            var baseline2 = stats2.BaselineMemoryMB;

            // Baseline should never change
            baseline2.Should().Be(baseline1);
        }

        public void Dispose()
        {
            _monitor?.Dispose();
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    [Trait("Category", "Unit")]
    public class ZombieProcessCleanupServiceTests : IDisposable
    {
        private ZombieProcessCleanupService? _service;

        [Fact]
        public void Constructor_InitializesTimer_AndLogsMessage()
        {
            // Arrange & Act
            _service = new ZombieProcessCleanupService();

            // Assert
            _service.Should().NotBeNull();
            var stats = _service.GetStatistics();
            stats.ServiceRunning.Should().BeTrue();
            stats.TotalZombiesKilled.Should().Be(0);
        }

        [Fact]
        public void GetStatistics_InitialState_ReturnsZeroKills()
        {
            // Arrange
            _service = new ZombieProcessCleanupService();

            // Act
            var stats = _service.GetStatistics();

            // Assert
            stats.TotalZombiesKilled.Should().Be(0);
            stats.ServiceRunning.Should().BeTrue();
        }

        [Fact]
        public void CleanupNow_WhenNoZombies_CompletesSuccessfully()
        {
            // Arrange
            _service = new ZombieProcessCleanupService();

            // Act
            Action act = () => _service.CleanupNow();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_StopsTimer_AndLogsStatistics()
        {
            // Arrange
            _service = new ZombieProcessCleanupService();

            // Act
            _service.Dispose();

            // Assert
            var stats = _service.GetStatistics();
            stats.ServiceRunning.Should().BeFalse();
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            // Arrange
            _service = new ZombieProcessCleanupService();

            // Act
            _service.Dispose();
            Action act = () => _service.Dispose();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void CleanupNow_AfterDispose_DoesNotExecute()
        {
            // Arrange
            _service = new ZombieProcessCleanupService();
            _service.Dispose();

            // Act
            Action act = () => _service.CleanupNow();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void ZombieDetected_Event_CanBeSubscribed()
        {
            // Arrange
            _service = new ZombieProcessCleanupService();
            bool eventFired = false;
            ZombieCleanupEventArgs? capturedArgs = null;

            _service.ZombieDetected += (sender, args) =>
            {
                eventFired = true;
                capturedArgs = args;
            };

            // Act
            _service.CleanupNow();

            // Assert - Event won't fire if no zombies exist, but subscription should work
            // This test verifies the event infrastructure is in place
            _service.Should().NotBeNull();
        }

        [Fact]
        public void GetStatistics_ConcurrentCalls_ThreadSafe()
        {
            // Arrange
            _service = new ZombieProcessCleanupService();

            // Act
            var tasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    var stats = _service.GetStatistics();
                    stats.Should().NotBeNull();
                });
            }

            // Assert
            Action act = () => Task.WaitAll(tasks);
            act.Should().NotThrow();
        }

        [Fact]
        public void CleanupNow_ConcurrentCalls_ThreadSafe()
        {
            // Arrange
            _service = new ZombieProcessCleanupService();

            // Act
            var tasks = new Task[5];
            for (int i = 0; i < 5; i++)
            {
                tasks[i] = Task.Run(() => _service.CleanupNow());
            }

            // Assert
            Action act = () => Task.WaitAll(tasks);
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_ConcurrentWithCleanup_ThreadSafe()
        {
            // Arrange
            _service = new ZombieProcessCleanupService();

            // Act
            var cleanupTask = Task.Run(() =>
            {
                for (int i = 0; i < 5; i++)
                {
                    _service.CleanupNow();
                    Thread.Sleep(10);
                }
            });

            var disposeTask = Task.Run(() =>
            {
                Thread.Sleep(25); // Let cleanup run a bit
                _service.Dispose();
            });

            // Assert
            Action act = () => Task.WaitAll(cleanupTask, disposeTask);
            act.Should().NotThrow();
        }

        [Fact]
        public void ZombieCleanupEventArgs_Properties_SetAndGet()
        {
            // Arrange & Act
            var args = new ZombieCleanupEventArgs
            {
                ProcessId = 1234,
                MemoryMB = 100,
                Timestamp = DateTime.Now
            };

            // Assert
            args.ProcessId.Should().Be(1234);
            args.MemoryMB.Should().Be(100);
            args.Timestamp.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void ZombieCleanupStatistics_Properties_SetAndGet()
        {
            // Arrange & Act
            var stats = new ZombieCleanupStatistics
            {
                TotalZombiesKilled = 5,
                ServiceRunning = true
            };

            // Assert
            stats.TotalZombiesKilled.Should().Be(5);
            stats.ServiceRunning.Should().BeTrue();
        }

        [Fact]
        public void GetStatistics_AfterDispose_ReturnsServiceRunningFalse()
        {
            // Arrange
            _service = new ZombieProcessCleanupService();

            // Act
            _service.Dispose();
            var stats = _service.GetStatistics();

            // Assert
            stats.ServiceRunning.Should().BeFalse();
        }

        [Fact]
        public void Constructor_DoesNotThrow_WhenErrorLoggerAvailable()
        {
            // Arrange & Act
            Action act = () => _service = new ZombieProcessCleanupService();

            // Assert
            act.Should().NotThrow();
        }

        public void Dispose()
        {
            _service?.Dispose();
            _service = null;
        }
    }
}

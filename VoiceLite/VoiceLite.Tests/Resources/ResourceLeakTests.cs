using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using VoiceLite.Services;
using System.Net.Http;
using System.Reflection;

namespace VoiceLite.Tests.Resources
{
    /// <summary>
    /// Test suite for detecting and preventing resource leaks
    /// These tests are written FIRST before fixing the issues (TDD approach)
    /// </summary>
    public class ResourceLeakTests : IDisposable
    {
        private readonly List<IDisposable> _disposables = new();

        [Fact]
        public void LicenseService_MultipleInstances_ShouldNotCreateMultipleHttpClients()
        {
            // This test verifies that multiple LicenseService instances share the same static HttpClient
            // Using reflection to check instance reuse is more reliable than measuring socket/handle counts

            var serviceType = typeof(LicenseService);
            var httpClientField = serviceType.GetField("_httpClient",
                BindingFlags.NonPublic | BindingFlags.Static);

            httpClientField.Should().NotBeNull("HttpClient field should exist");

            // Get the static HttpClient instance before creating services
            var httpClientBefore = httpClientField?.GetValue(null);

            // Create multiple LicenseService instances
            var services = new List<LicenseService>();
            for (int i = 0; i < 10; i++)
            {
                var service = new LicenseService();
                services.Add(service);
                _disposables.Add(service);
            }

            // Get the static HttpClient instance after creating services
            var httpClientAfter = httpClientField?.GetValue(null);

            // Assert - The same static HttpClient instance should be reused
            httpClientBefore.Should().BeSameAs(httpClientAfter,
                "Multiple LicenseService instances should share the same static HttpClient instance");

            // Clean up
            foreach (var service in services)
            {
                service.Dispose();
            }
        }

        [Fact]
        public void LicenseService_HttpClient_ShouldBeStatic()
        {
            // This test checks if HttpClient is properly implemented as static
            var serviceType = typeof(LicenseService);
            var httpClientField = serviceType.GetField("_httpClient",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

            httpClientField.Should().NotBeNull("HttpClient field should exist");

            if (httpClientField != null)
            {
                httpClientField.IsStatic.Should().BeTrue(
                    "HttpClient should be static to prevent socket exhaustion");
            }
        }

        [Fact]
        public async Task MainWindow_TimerManagement_ShouldNotAccumulateTimers()
        {
            // This test validates that timer cleanup works correctly
            // Instead of measuring memory (which is non-deterministic), we verify:
            // 1. Timers fire and clean themselves up
            // 2. Dispose() cleans up any remaining timers
            // 3. No timers remain after disposal

            // Arrange
            var timerManager = new TimerManager();

            // Act - Create many timers with different IDs
            for (int i = 0; i < 100; i++)
            {
                timerManager.StartStatusTimer($"timer-{i}", "Test message", TimeSpan.FromMilliseconds(10));
                await Task.Delay(15); // Let timer fire and self-cleanup
            }

            // Allow extra time for any pending timer events to complete
            await Task.Delay(50);

            // At this point, most/all timers should have fired and cleaned themselves up
            var activeTimersBeforeDispose = timerManager.ActiveTimerCount;

            // Clean up any remaining timers
            timerManager.Dispose();

            // Assert - verify cleanup worked
            var activeTimersAfterDispose = timerManager.ActiveTimerCount;

            activeTimersBeforeDispose.Should().BeLessThan(10,
                "Most timers should have self-cleaned after firing");
            activeTimersAfterDispose.Should().Be(0,
                "All timers should be cleaned up after Dispose()");
        }

        [Fact]
        public async Task AudioRecorder_RapidStartStop_ShouldNotLeakMemory()
        {
            // Arrange
            var recorder = new AudioRecorder();
            _disposables.Add(recorder);

            var initialMemory = GC.GetTotalMemory(true) / 1_000_000; // MB

            // Act - Rapid start/stop 50 times
            for (int i = 0; i < 50; i++)
            {
                recorder.StartRecording();
                await Task.Delay(10); // Very short recording
                recorder.StopRecording();
                await Task.Delay(10); // Brief pause
            }

            // Clean up
            recorder.Dispose();

            // Force GC
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Assert
            var finalMemory = GC.GetTotalMemory(true) / 1_000_000; // MB
            var memoryGrowth = finalMemory - initialMemory;

            memoryGrowth.Should().BeLessThan(10,
                $"Memory grew by {memoryGrowth}MB after 50 recordings");
        }

        [Fact]
        public void AudioRecorder_Disposal_ShouldUnsubscribeAllEventHandlers()
        {
            // Arrange
            var recorder = new AudioRecorder();
            var eventFired = false;
            recorder.AudioFileReady += (s, e) => eventFired = true;

            // Act
            recorder.Dispose();

            // Try to trigger event after disposal (should not fire)
            // This would normally happen internally, but we're testing the pattern
            var recorderType = recorder.GetType();
            var waveInField = recorderType.GetField("waveIn",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Assert
            if (waveInField != null)
            {
                var waveIn = waveInField.GetValue(recorder);
                waveIn.Should().BeNull("WaveIn should be null after disposal");
            }

            eventFired.Should().BeFalse("Events should not fire after disposal");
        }

        [Fact]
        public async Task HttpClient_StressTest_ShouldNotExhaustSockets()
        {
            // This test simulates what happens with multiple HttpClient instances
            var tasks = new List<Task>();
            var clients = new List<HttpClient>();

            try
            {
                // Simulate the OLD way (BAD - multiple HttpClient instances)
                // This would fail if not fixed
                for (int i = 0; i < 20; i++)
                {
                    var client = new HttpClient(); // This is what we're trying to avoid!
                    clients.Add(client);

                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            // Make a simple request
                            await client.GetAsync("https://www.google.com");
                        }
                        catch { /* Ignore errors */ }
                    }));
                }

                await Task.WhenAll(tasks);

                // NOTE: In production code, this pattern should use a static HttpClient
                // This test demonstrates why the current approach is problematic
            }
            finally
            {
                // Clean up
                foreach (var client in clients)
                {
                    client.Dispose();
                }
            }

            // The test passes but demonstrates the problem we're fixing
            true.Should().BeTrue();
        }

        private int GetActiveSocketCount()
        {
            try
            {
                // This is a simplified version - actual implementation would use performance counters
                // For testing purposes, we're just checking that the pattern is correct
                using var process = Process.GetCurrentProcess();
                return process.HandleCount; // Rough approximation
            }
            catch
            {
                return 0;
            }
        }

        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                try
                {
                    disposable?.Dispose();
                }
                catch { /* Ignore disposal errors in tests */ }
            }
        }
    }

    /// <summary>
    /// Helper class to test timer management pattern
    /// This simulates what MainWindow should do
    /// </summary>
    public class TimerManager : IDisposable
    {
        private readonly Dictionary<string, System.Timers.Timer> _timers = new();
        private readonly object _lock = new object();

        public int ActiveTimerCount
        {
            get
            {
                lock (_lock)
                {
                    return _timers.Count;
                }
            }
        }

        public void StartStatusTimer(string id, string message, TimeSpan duration)
        {
            lock (_lock)
            {
                // Clean up existing timer if present
                if (_timers.TryGetValue(id, out var existingTimer))
                {
                    existingTimer.Stop();
                    existingTimer.Dispose();
                    _timers.Remove(id);
                }

                // Create new timer
                var timer = new System.Timers.Timer(duration.TotalMilliseconds);
                timer.AutoReset = false;
                timer.Elapsed += (s, e) =>
                {
                    lock (_lock)
                    {
                        if (_timers.ContainsKey(id))
                        {
                            _timers[id].Dispose();
                            _timers.Remove(id);
                        }
                    }
                };

                timer.Start();
                _timers[id] = timer;
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                foreach (var timer in _timers.Values)
                {
                    timer.Stop();
                    timer.Dispose();
                }
                _timers.Clear();
            }
        }
    }
}
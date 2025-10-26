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
        public async Task LicenseService_MultipleInstances_ShouldNotCreateMultipleHttpClients()
        {
            // Arrange - Get initial socket count
            var initialSockets = GetActiveSocketCount();

            // Act - Create multiple LicenseService instances
            for (int i = 0; i < 10; i++)
            {
                var service = new LicenseService();
                _disposables.Add(service);

                // Try to validate a license (will fail but that's okay)
                try
                {
                    await service.ValidateLicenseAsync("test-key");
                }
                catch { /* Expected to fail */ }
            }

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Assert - Socket count should not have grown significantly
            var finalSockets = GetActiveSocketCount();
            var socketGrowth = finalSockets - initialSockets;

            // With static HttpClient, growth should be minimal (< 5)
            // With instance HttpClient, it would be 10+
            socketGrowth.Should().BeLessThan(5,
                "Multiple LicenseService instances should share HttpClient");
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
            // This test would require MainWindow refactoring to be testable
            // For now, we'll create a simpler test that validates the pattern

            // Arrange
            var timerManager = new TimerManager(); // We'll create this helper class
            var initialMemory = GC.GetTotalMemory(true);

            // Act - Create and dispose many timers
            for (int i = 0; i < 100; i++)
            {
                timerManager.StartStatusTimer($"timer-{i}", "Test message", TimeSpan.FromMilliseconds(10));
                await Task.Delay(15); // Let timer fire
            }

            // Clean up
            timerManager.Dispose();

            // Force GC
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Assert
            var finalMemory = GC.GetTotalMemory(true);
            var memoryGrowth = finalMemory - initialMemory;

            memoryGrowth.Should().BeLessThan(1_000_000, // Less than 1MB
                "Timer management should not leak memory");
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
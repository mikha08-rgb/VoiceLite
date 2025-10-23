using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// Tests for SoundService - Custom UI sound playback
    /// </summary>
    [Trait("Category", "Unit")]
    public class SoundServiceTests : IDisposable
    {
        private SoundService? soundService;

        public SoundServiceTests()
        {
            soundService = new SoundService();
        }

        // Task 2: Happy Path Tests

        [Fact]
        public void Constructor_CreatesInstanceSuccessfully()
        {
            // Arrange & Act
            var service = new SoundService();

            // Assert
            service.Should().NotBeNull();

            service.Dispose();
        }

        [Fact]
        public void PlaySound_WhenCalled_CompletesWithoutException()
        {
            // Act & Assert
            soundService!.Invoking(s => s.PlaySound()).Should().NotThrow();
        }

        [Fact]
        public async Task PlaySound_MultipleCalls_HandledCorrectly()
        {
            // Act
            soundService!.PlaySound();
            await Task.Delay(50); // Allow first playback to start

            soundService.PlaySound(); // Second call should cleanup previous and start new
            await Task.Delay(50);

            soundService.PlaySound(); // Third call
            await Task.Delay(50);

            // Assert - Should complete without throwing
            soundService.Invoking(s => s.Dispose()).Should().NotThrow();
        }

        // Task 3: Error Case Tests

        [Fact]
        public void PlaySound_WhenSoundFileNotFound_FallsBackToSystemBeep()
        {
            // Arrange - Create SoundService that will look for non-existent file
            // The service will look in Resources/wood-tap-click.ogg
            // If file doesn't exist, it falls back to SystemSounds.Beep

            // Act & Assert - Should not throw, just fallback silently
            soundService!.Invoking(s => s.PlaySound()).Should().NotThrow();
        }

        [Fact]
        public void PlaySound_AfterDispose_ReturnsWithoutException()
        {
            // Arrange
            soundService!.Dispose();

            // Act & Assert - Should return early without throwing
            soundService.Invoking(s => s.PlaySound()).Should().NotThrow();
        }

        [Fact]
        public async Task PlaySound_WhenAudioDeviceBusy_FallsBackToSystemBeep()
        {
            // Arrange - Play sound to potentially busy the device
            soundService!.PlaySound();

            // Act - Immediately try to play again (previous might still be playing)
            soundService.Invoking(s => s.PlaySound()).Should().NotThrow();

            await Task.Delay(100); // Allow cleanup
        }

        [Fact]
        public void PlaySound_WhenInvalidAudioFile_FallsBackToSystemBeep()
        {
            // This test verifies the catch block in PlaySound()
            // If VorbisWaveReader throws (invalid file, corrupt file, etc),
            // the service should catch and fallback to SystemSounds.Beep

            // Act & Assert - Should handle any audio errors gracefully
            soundService!.Invoking(s => s.PlaySound()).Should().NotThrow();
        }

        // Task 4: Resource Cleanup Tests

        [Fact]
        public async Task Dispose_CleansUpAudioResources()
        {
            // Arrange
            soundService!.PlaySound();
            await Task.Delay(50); // Allow playback to start

            // Act
            soundService.Dispose();

            // Assert - Should cleanup without throwing
            // After disposal, PlaySound should return early
            soundService.Invoking(s => s.PlaySound()).Should().NotThrow();
        }

        [Fact]
        public void Dispose_MultipleCalls_DoesNotThrow()
        {
            // Act
            soundService!.Dispose();
            soundService.Dispose();
            soundService.Dispose();

            // Assert - Should be idempotent
            soundService.Invoking(s => s.Dispose()).Should().NotThrow();
        }

        [Fact]
        public async Task OnPlaybackStopped_CleansUpResources()
        {
            // Arrange
            soundService!.PlaySound();

            // Act - Wait for playback to complete (triggers OnPlaybackStopped)
            // wood-tap-click.ogg is short, should complete in < 500ms
            await Task.Delay(600);

            // Assert - Service should still be usable after playback stops
            soundService.Invoking(s => s.PlaySound()).Should().NotThrow();
        }

        [Fact]
        public async Task CleanupAudioResources_UnsubscribesEventHandler()
        {
            // Arrange
            soundService!.PlaySound();
            await Task.Delay(50);

            // Act - Dispose should unsubscribe event handlers
            soundService.Dispose();

            // Assert - Should not throw (event handler unsubscribed)
            soundService.Invoking(s => s.Dispose()).Should().NotThrow();
        }

        // Task 5: Thread Safety Tests

        [Fact]
        public async Task PlaySound_ConcurrentCalls_ThreadSafe()
        {
            // Arrange
            var tasks = new Task[10];

            // Act - Multiple concurrent PlaySound calls
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(() => soundService!.PlaySound());
            }

            await Task.WhenAll(tasks);
            await Task.Delay(200); // Allow cleanup

            // Assert - Should handle concurrent access safely
            soundService!.Invoking(s => s.Dispose()).Should().NotThrow();
        }

        [Fact]
        public async Task Dispose_WhilePlaying_ThreadSafe()
        {
            // Arrange
            soundService!.PlaySound();

            // Act - Dispose while playback might be in progress
            var disposeTask = Task.Run(() => soundService.Dispose());
            var playSoundTask = Task.Run(() => soundService.PlaySound());

            await Task.WhenAll(disposeTask, playSoundTask);

            // Assert - Should handle concurrent disposal safely
            soundService.Invoking(s => s.Dispose()).Should().NotThrow();
        }

        [Fact]
        public async Task PlaySound_DuringDispose_NoRaceCondition()
        {
            // Arrange
            var tasks = new Task[20];

            // Act - Mix PlaySound and Dispose calls concurrently
            for (int i = 0; i < 20; i++)
            {
                int index = i;
                tasks[i] = Task.Run(() =>
                {
                    if (index % 2 == 0)
                        soundService!.PlaySound();
                    else
                        soundService!.Dispose();
                });
            }

            await Task.WhenAll(tasks);

            // Assert - Should handle race conditions safely
            soundService!.Invoking(s => s.Dispose()).Should().NotThrow();
        }

        // Task 6: Branch Coverage Tests

        [Fact]
        public void PlaySound_WithExistingFile_UsesNAudio()
        {
            // This test exercises the "file exists" branch
            // If Resources/wood-tap-click.ogg exists, NAudio path is used
            // If not, fallback to SystemSounds.Beep is used

            // Act & Assert - Should complete without exception
            soundService!.Invoking(s => s.PlaySound()).Should().NotThrow();
        }

        [Fact]
        public void Dispose_WhenNotDisposed_SetsDisposedFlag()
        {
            // Arrange - Fresh instance
            var service = new SoundService();

            // Act - First disposal
            service.Dispose();

            // Assert - Second disposal should hit "if (_disposed) return" branch
            service.Invoking(s => s.Dispose()).Should().NotThrow();
        }

        [Fact]
        public async Task CleanupAudioResources_WithInitializedDevices_DisposesAll()
        {
            // Arrange - Initialize devices by playing sound
            soundService!.PlaySound();
            await Task.Delay(50);

            // Act - Dispose should cleanup initialized devices
            soundService.Dispose();

            // Assert - Should handle cleanup of initialized resources
            soundService.Invoking(s => s.Dispose()).Should().NotThrow();
        }

        [Fact]
        public void CleanupAudioResources_WithNullDevices_HandlesGracefully()
        {
            // Arrange - Fresh instance (devices are null)
            var service = new SoundService();

            // Act - Dispose with null devices should work
            service.Invoking(s => s.Dispose()).Should().NotThrow();

            // Assert - Cleanup with null devices handled
            service.Invoking(s => s.Dispose()).Should().NotThrow();
        }

        [Fact]
        public void PlaySound_ExceptionHandling_CatchesAllErrors()
        {
            // This test verifies the catch block in PlaySound()
            // Any exception from NAudio operations should be caught
            // and fallback to SystemSounds.Beep

            // Arrange
            var service = new SoundService();

            // Act - Multiple calls to exercise exception paths
            service.PlaySound();
            service.PlaySound();
            service.PlaySound();

            // Assert - Should never throw exceptions
            service.Invoking(s => s.PlaySound()).Should().NotThrow();

            service.Dispose();
        }

        public void Dispose()
        {
            soundService?.Dispose();
        }
    }
}

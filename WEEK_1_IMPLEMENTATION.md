# Week 1: Stabilize - Detailed Implementation Guide

## Overview
Since you have a working version in production, we can fix the critical issues without rushing. This week focuses on stability without any major architectural changes.

## Day 1-2: Fix Resource Leaks (Monday-Tuesday)

### Task 1: Fix HttpClient in LicenseService (2 hours)

**File**: `VoiceLite/VoiceLite/Services/LicenseService.cs`

**Current Problem**:
```csharp
public class LicenseService : IDisposable
{
    private readonly HttpClient _httpClient;

    public LicenseService()
    {
        _httpClient = new HttpClient();  // NEW INSTANCE = SOCKET LEAK
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }
}
```

**Fix**:
```csharp
public class LicenseService : IDisposable
{
    // SHARED STATIC INSTANCE - No more socket exhaustion
    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(10),
        BaseAddress = new Uri("https://voicelite.app/api/")
    };

    static LicenseService()
    {
        // Configure once at startup
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "VoiceLite-Desktop/1.0");
    }

    public void Dispose()
    {
        // Remove HttpClient disposal - static instance lives forever
        // Only dispose other resources if any
    }

    public async Task<bool> ValidateLicenseAsync(string licenseKey)
    {
        try
        {
            var content = new StringContent(
                JsonSerializer.Serialize(new { licenseKey }),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync("licenses/validate", content);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<LicenseValidationResponse>(json);
                return result?.Valid ?? false;
            }

            return false;
        }
        catch (TaskCanceledException)
        {
            ErrorLogger.LogWarning("License validation timed out");
            return false;
        }
        catch (Exception ex)
        {
            ErrorLogger.LogError("License validation failed", ex);
            return false;
        }
    }
}
```

### Task 2: Fix Timer Accumulation in MainWindow (3 hours)

**File**: `VoiceLite/VoiceLite/MainWindow.xaml.cs`

**Current Problem**:
```csharp
private readonly List<DispatcherTimer> activeStatusTimers = new();
// Timers added but never removed = MEMORY LEAK
```

**Fix**:
```csharp
public partial class MainWindow : Window
{
    // Replace list with proper timer management
    private readonly Dictionary<string, DispatcherTimer> _activeTimers = new();
    private readonly object _timerLock = new object();

    private void StartStatusTimer(string timerId, string message, TimeSpan duration)
    {
        lock (_timerLock)
        {
            // Clean up existing timer if present
            if (_activeTimers.TryGetValue(timerId, out var existingTimer))
            {
                existingTimer.Stop();
                existingTimer.Tick -= OnStatusTimerTick;
                _activeTimers.Remove(timerId);
            }

            // Create new timer
            var timer = new DispatcherTimer
            {
                Interval = duration,
                Tag = new { Id = timerId, Message = message }
            };

            timer.Tick += OnStatusTimerTick;
            timer.Start();

            _activeTimers[timerId] = timer;
        }
    }

    private void OnStatusTimerTick(object sender, EventArgs e)
    {
        var timer = (DispatcherTimer)sender;
        timer.Stop();
        timer.Tick -= OnStatusTimerTick;

        var tag = (dynamic)timer.Tag;
        lock (_timerLock)
        {
            _activeTimers.Remove(tag.Id);
        }

        // Update UI
        Dispatcher.Invoke(() => StatusText.Text = "Ready");
    }

    // CRITICAL: Clean up all timers on window close
    protected override void OnClosed(EventArgs e)
    {
        lock (_timerLock)
        {
            foreach (var timer in _activeTimers.Values)
            {
                timer.Stop();
                timer.Tick -= OnStatusTimerTick;
            }
            _activeTimers.Clear();
        }

        base.OnClosed(e);
    }
}
```

### Task 3: Fix Event Handler Leaks in AudioRecorder (1 hour)

**File**: `VoiceLite/VoiceLite/Services/AudioRecorder.cs`

**Add proper cleanup**:
```csharp
public void Dispose()
{
    if (isDisposed) return;

    lock (recordingLock)
    {
        // Stop recording if active
        if (isRecording)
        {
            try
            {
                StopRecording();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Error stopping recording during disposal", ex);
            }
        }

        // CRITICAL: Unsubscribe event handlers BEFORE disposing
        if (waveIn != null)
        {
            waveIn.DataAvailable -= OnDataAvailable;
            waveIn.RecordingStopped -= OnRecordingStopped;

            try
            {
                waveIn.Dispose();
            }
            catch { }

            waveIn = null;
        }

        // Dispose other resources
        waveFile?.Dispose();
        audioMemoryStream?.Dispose();

        isDisposed = true;
    }

    // Stop cleanup timer
    audioCleanupTimer?.Dispose();
}
```

---

## Day 3-4: Fix All Async Void Handlers (Wednesday-Thursday)

### Task 4: Create Safe Async Handler Wrapper (1 hour)

**Create new file**: `VoiceLite/VoiceLite/Helpers/AsyncHelper.cs`

```csharp
using System;
using System.Threading.Tasks;
using System.Windows;

namespace VoiceLite.Helpers
{
    public static class AsyncHelper
    {
        /// <summary>
        /// Safely executes async code in event handlers with proper error handling
        /// </summary>
        public static async void SafeFireAndForget(
            Task task,
            string operationName = "Operation",
            bool showUserMessage = true)
        {
            try
            {
                await task;
            }
            catch (TaskCanceledException)
            {
                // Normal during shutdown - don't log
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError($"{operationName} failed", ex);

                if (showUserMessage)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            $"{operationName} failed: {ex.Message}",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    });
                }
            }
        }

        /// <summary>
        /// Safely executes async code with a return value
        /// </summary>
        public static async Task<T?> SafeExecuteAsync<T>(
            Func<Task<T>> taskFactory,
            string operationName = "Operation",
            T? defaultValue = default)
        {
            try
            {
                return await taskFactory();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError($"{operationName} failed", ex);
                return defaultValue;
            }
        }
    }
}
```

### Task 5: Fix MainWindow Event Handlers (4 hours)

**File**: `VoiceLite/VoiceLite/MainWindow.xaml.cs`

**Before**:
```csharp
private async void StartStopButton_Click(object sender, RoutedEventArgs e)
{
    if (audioRecorder.IsRecording)
    {
        await StopRecordingAsync();
    }
    else
    {
        await StartRecordingAsync();
    }
}
```

**After**:
```csharp
private void StartStopButton_Click(object sender, RoutedEventArgs e)
{
    AsyncHelper.SafeFireAndForget(
        audioRecorder.IsRecording ? StopRecordingAsync() : StartRecordingAsync(),
        audioRecorder.IsRecording ? "Stop recording" : "Start recording");
}

// Alternative approach with inline try-catch for more control:
private async void StartStopButton_Click(object sender, RoutedEventArgs e)
{
    try
    {
        StartStopButton.IsEnabled = false; // Prevent double-clicks

        if (audioRecorder.IsRecording)
        {
            await StopRecordingAsync();
        }
        else
        {
            await StartRecordingAsync();
        }
    }
    catch (InvalidOperationException ex)
    {
        // Specific handling for expected errors
        ErrorLogger.LogWarning($"Recording state issue: {ex.Message}");
        UpdateStatus("Recording busy - please wait", StatusType.Warning);
    }
    catch (Exception ex)
    {
        // Generic handling for unexpected errors
        ErrorLogger.LogError("Start/Stop recording failed", ex);
        MessageBox.Show(
            $"Recording operation failed: {ex.Message}",
            "Recording Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
    finally
    {
        StartStopButton.IsEnabled = true;
    }
}
```

### Task 6: Fix Model Download Handlers (2 hours)

**File**: `VoiceLite/VoiceLite/Controls/ModelDownloadControl.xaml.cs`

**Line 182 - Currently missing outer try-catch**:
```csharp
private async void ActionButton_Click(object sender, RoutedEventArgs e)
{
    // CRITICAL FIX #5: Add complete exception handling
    try
    {
        if (sender is not Button button || button.Tag is not WhisperModelInfo model)
            return;

        // Disable all buttons during operation
        SetAllButtonsEnabled(false);

        try
        {
            // Permission check
            if (!await CheckDownloadPermissionAsync(model))
            {
                UpdateStatus($"Download cancelled", StatusType.Info);
                return;
            }

            // Start download
            await DownloadModelAsync(model);
        }
        catch (HttpRequestException ex)
        {
            ErrorLogger.LogError($"Network error downloading {model.Name}", ex);
            UpdateStatus($"Network error: {ex.Message}", StatusType.Error);
        }
        catch (IOException ex)
        {
            ErrorLogger.LogError($"File error downloading {model.Name}", ex);
            UpdateStatus($"File error: {ex.Message}", StatusType.Error);
        }
        finally
        {
            SetAllButtonsEnabled(true);
        }
    }
    catch (Exception ex)
    {
        // Outer catch for ANY unhandled exception
        ErrorLogger.LogError("Model download button click failed completely", ex);
        MessageBox.Show(
            $"An unexpected error occurred: {ex.Message}",
            "Download Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
```

**File**: `VoiceLite/VoiceLite/Controls/ModelComparisonControl.xaml.cs`

**Line 248 - Similar fix needed**

---

## Day 5: Add Integration Tests (Friday)

### Task 7: Set Up Integration Test Project (1 hour)

**Create new test file**: `VoiceLite/VoiceLite.Tests/Integration/EndToEndTests.cs`

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;

namespace VoiceLite.Tests.Integration
{
    public class EndToEndTests : IDisposable
    {
        private readonly string _testDataPath;
        private readonly AudioRecorder _recorder;
        private readonly PersistentWhisperService _whisper;
        private readonly TextInjector _injector;

        public EndToEndTests()
        {
            _testDataPath = Path.Combine(Path.GetTempPath(), $"VoiceLiteTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDataPath);

            _recorder = new AudioRecorder();
            _whisper = new PersistentWhisperService();
            _injector = new TextInjector();
        }

        [Fact]
        public async Task FullTranscriptionFlow_ShouldCompleteSuccessfully()
        {
            // Arrange
            string transcribedText = null;
            _whisper.TranscriptionComplete += (s, e) => transcribedText = e.Text;

            // Act - Record 2 seconds of silence
            _recorder.StartRecording();
            await Task.Delay(2000);
            var audioPath = _recorder.StopRecording();

            // Transcribe
            await _whisper.TranscribeAsync(audioPath);

            // Assert
            audioPath.Should().NotBeNullOrEmpty();
            File.Exists(audioPath).Should().BeTrue();
            transcribedText.Should().NotBeNull();
        }

        [Fact]
        public async Task RapidStartStop_ShouldNotCrash()
        {
            // Act - Rapidly start/stop 20 times
            for (int i = 0; i < 20; i++)
            {
                _recorder.StartRecording();
                await Task.Delay(50);
                _recorder.StopRecording();
                await Task.Delay(50);
            }

            // Assert - We got here without crashing
            _recorder.IsRecording.Should().BeFalse();
        }

        [Fact]
        public async Task SimultaneousClose_DuringTranscription_ShouldNotDeadlock()
        {
            // Arrange
            var tcs = new TaskCompletionSource<bool>();

            // Act - Start transcription then immediately dispose
            var audioPath = CreateTestAudioFile();
            var transcriptionTask = _whisper.TranscribeAsync(audioPath);

            // Dispose while transcription is running
            _whisper.Dispose();

            // Wait for completion or timeout
            var completedTask = await Task.WhenAny(
                transcriptionTask,
                Task.Delay(5000));

            // Assert - Should complete without deadlock
            completedTask.Should().Be(transcriptionTask);
        }

        [Fact]
        public async Task MemoryStressTest_100Recordings_ShouldNotLeak()
        {
            // Arrange
            var initialMemory = GC.GetTotalMemory(true) / 1_000_000; // MB

            // Act - 100 quick recordings
            for (int i = 0; i < 100; i++)
            {
                _recorder.StartRecording();
                await Task.Delay(10);
                _recorder.StopRecording();
            }

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var finalMemory = GC.GetTotalMemory(true) / 1_000_000; // MB

            // Assert - Memory shouldn't grow more than 50MB
            var memoryGrowth = finalMemory - initialMemory;
            memoryGrowth.Should().BeLessThan(50, $"Memory grew by {memoryGrowth}MB");
        }

        [Fact]
        public async Task ClipboardRestore_AfterPasteInjection_ShouldPreserveOriginal()
        {
            // Arrange
            var originalClipboard = "Original clipboard content";
            Clipboard.SetText(originalClipboard);

            // Act
            var result = await _injector.InjectTextAsync(
                "Transcribed text",
                InjectionMode.Paste);

            await Task.Delay(100); // Wait for clipboard restore

            // Assert
            result.Success.Should().BeTrue();
            Clipboard.GetText().Should().Be(originalClipboard);
        }

        public void Dispose()
        {
            _recorder?.Dispose();
            _whisper?.Dispose();
            _injector?.Dispose();

            if (Directory.Exists(_testDataPath))
            {
                Directory.Delete(_testDataPath, true);
            }
        }

        private string CreateTestAudioFile()
        {
            // Create a minimal valid WAV file for testing
            var path = Path.Combine(_testDataPath, "test.wav");
            // ... WAV file creation logic
            return path;
        }
    }
}
```

### Task 8: Run and Fix Tests (3 hours)

```bash
# Run all tests including new integration tests
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj

# Run only integration tests
dotnet test --filter "FullyQualifiedName~Integration"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

---

## End of Week 1 Checklist

### Must Complete
- [ ] HttpClient converted to static singleton
- [ ] Timer management fixed (no more accumulation)
- [ ] All async void handlers have try-catch
- [ ] 5+ integration tests passing
- [ ] Stress test: 100 recordings without memory leak
- [ ] Stress test: Close during transcription (no deadlock)

### Should Complete
- [ ] AsyncHelper utility class created
- [ ] ModelDownload handlers fully wrapped
- [ ] Settings save is atomic (temp file → move)
- [ ] Disposal patterns consistent across all services

### Nice to Have
- [ ] Basic retry logic on LicenseService
- [ ] Logging improved in critical paths
- [ ] Dead code removed

---

## How to Test Your Fixes

### Memory Leak Test
```csharp
[Fact]
public async Task NoMemoryLeaks_After100Operations()
{
    var before = GC.GetTotalMemory(true);

    for (int i = 0; i < 100; i++)
    {
        using var recorder = new AudioRecorder();
        recorder.StartRecording();
        await Task.Delay(10);
        recorder.StopRecording();
    }

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    var after = GC.GetTotalMemory(true);
    Assert.True(after - before < 10_000_000); // Less than 10MB growth
}
```

### Deadlock Test
```csharp
[Fact]
public async Task NoDeadlock_WhenClosingDuringOperation()
{
    var window = new MainWindow();
    window.Show();

    // Start operation
    window.StartRecording();

    // Close immediately
    var closeTask = Task.Run(() => window.Close());

    // Should complete within 5 seconds
    var completed = await Task.WhenAny(
        closeTask,
        Task.Delay(5000));

    Assert.Equal(closeTask, completed);
}
```

---

## Common Pitfalls to Avoid

### 1. Don't Forget ConfigureAwait(false)
```csharp
// In library code (not UI code)
await SomeAsync().ConfigureAwait(false);
```

### 2. Don't Create HttpClient in Loops
```csharp
// BAD
for (int i = 0; i < 100; i++)
{
    using var client = new HttpClient(); // SOCKET EXHAUSTION
}

// GOOD
// Use static or injected HttpClient
```

### 3. Always Dispose WaveIn Events
```csharp
// CRITICAL: Unsubscribe before dispose
waveIn.DataAvailable -= OnDataAvailable;
waveIn.Dispose();
```

### 4. Test Disposal Scenarios
```csharp
// Always test dispose during operations
StartOperation();
Dispose(); // Should not throw or deadlock
```

---

## Questions You Might Have

**Q: Should I branch or work on main?**
A: Create a branch called `refactor/week-1-stability` and merge daily after tests pass.

**Q: What if a fix breaks something?**
A: That's why we add integration tests first. They'll catch breaks immediately.

**Q: Can I skip some of these?**
A: The HttpClient and async void fixes are critical. Others can wait if needed.

**Q: How do I know it's working?**
A: Run the stress tests. If they pass, you're good.

---

## Next Week Preview (Week 2-3)

Once Week 1 is complete, we'll:
1. Create interfaces for all services
2. Implement MVVM with ViewModels
3. Add dependency injection
4. Refactor MainWindow from 2,591 → ~200 lines

But focus on Week 1 first. Make it stable, then make it pretty.

**Ready to start Day 1?**
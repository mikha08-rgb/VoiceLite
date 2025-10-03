# Performance Targets for VoiceLite

## Application Performance Metrics

### Startup Performance
| Metric | Target | Current | Status |
|--------|--------|---------|--------|
| Cold start | < 2s | 1.6s | ✅ |
| Warmup time | < 3s | 2.8s | ✅ |
| First transcription latency | < 5s | 4.2s | ✅ |

**Measurement Method**:
```csharp
var stopwatch = Stopwatch.StartNew();
// Application startup code
stopwatch.Stop();
Console.WriteLine($"Startup: {stopwatch.ElapsedMilliseconds}ms");
```

### Transcription Performance
| Metric | Target | Model-Dependent |
|--------|--------|-----------------|
| Post-speech latency | < 200ms | Excluding model processing |
| Small model latency | < 3s | For 5s audio clip |
| Medium model latency | < 6s | For 5s audio clip |
| Large model latency | < 12s | For 5s audio clip |

**Formula**: `Total Latency = Audio Processing + Model Processing + Text Injection`

### Memory Usage
| Metric | Target | Current | Status |
|--------|--------|---------|--------|
| Idle RAM | < 100MB | 88MB | ✅ |
| Active RAM (recording) | < 300MB | 285MB | ✅ |
| Peak RAM (transcription) | < 500MB | 420MB | ✅ |
| Memory leak rate | 0 MB/transcription | 0 MB | ✅ |

**Measurement Method**:
```csharp
using System.Diagnostics;

var process = Process.GetCurrentProcess();
var memoryMB = process.WorkingSet64 / 1024 / 1024;
Console.WriteLine($"Memory: {memoryMB} MB");
```

### CPU Usage
| Metric | Target | Current | Status |
|--------|--------|---------|--------|
| Idle CPU | < 5% | 2% | ✅ |
| Recording CPU | < 15% | 12% | ✅ |
| Transcription CPU | < 80% | 65% | ✅ |

### Disk I/O
| Metric | Target | Notes |
|--------|--------|-------|
| Temp file cleanup | Immediate | Delete after transcription |
| Settings save time | < 50ms | JSON serialization |
| Log file rotation | At 10MB | Prevent disk bloat |

---

## Performance Optimization Techniques

### 1. Memory Optimization

#### Object Pooling
```csharp
// Use ArrayPool for temporary buffers
using System.Buffers;

public byte[] ProcessAudio(byte[] input)
{
    // Rent from pool instead of allocating
    byte[] buffer = ArrayPool<byte>.Shared.Rent(input.Length);
    try
    {
        // Process using buffer
        return result;
    }
    finally
    {
        // Return to pool
        ArrayPool<byte>.Shared.Return(buffer);
    }
}
```

#### Dispose Pattern
```csharp
// Always dispose IDisposable resources
using (var waveIn = new WaveInEvent())
{
    // Use waveIn
} // Automatically disposed
```

#### Weak Event Pattern
```csharp
// Prevent memory leaks from event subscriptions
public class MyService : IDisposable
{
    public MyService()
    {
        GlobalEvents.SomeEvent += OnEvent;
    }

    public void Dispose()
    {
        GlobalEvents.SomeEvent -= OnEvent; // Unsubscribe!
    }

    private void OnEvent(object sender, EventArgs e) { }
}
```

### 2. CPU Optimization

#### Async I/O
```csharp
// ❌ SLOW - Blocks thread
var text = File.ReadAllText(path);

// ✅ FAST - Non-blocking
var text = await File.ReadAllTextAsync(path);
```

#### Parallel Processing (When Safe)
```csharp
// Process multiple files in parallel
var files = Directory.GetFiles(directory);
await Parallel.ForEachAsync(files, async (file, ct) =>
{
    await ProcessFileAsync(file, ct);
});
```

#### Avoid Unnecessary LINQ
```csharp
// ❌ SLOW - Multiple enumerations
var items = GetItems().ToList();
var count = items.Count();
var first = items.FirstOrDefault();

// ✅ FAST - Single enumeration
var items = GetItems().ToList(); // Enumerate once
var count = items.Count;          // Property access
var first = items.FirstOrDefault();
```

### 3. I/O Optimization

#### Buffered Streams
```csharp
// Use BufferedStream for better I/O performance
using (var fileStream = File.OpenRead(path))
using (var bufferedStream = new BufferedStream(fileStream, 8192))
{
    // Read from bufferedStream
}
```

#### Async File Operations
```csharp
// Write files asynchronously
await using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
{
    await stream.WriteAsync(data, 0, data.Length);
}
```

### 4. Caching

#### Path Caching
```csharp
// Cache file paths to avoid repeated lookups
private static string? _cachedWhisperPath;

public string GetWhisperPath()
{
    if (_cachedWhisperPath == null)
    {
        _cachedWhisperPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "whisper", "whisper.exe"
        );
    }
    return _cachedWhisperPath;
}
```

#### Settings Caching
```csharp
// Cache settings in memory with file watcher for updates
private static Settings? _cachedSettings;
private static FileSystemWatcher? _watcher;

public static Settings Load()
{
    if (_cachedSettings == null)
    {
        _cachedSettings = LoadFromDisk();
        SetupFileWatcher();
    }
    return _cachedSettings;
}

private static void SetupFileWatcher()
{
    _watcher = new FileSystemWatcher(settingsDir)
    {
        Filter = "settings.json",
        NotifyFilter = NotifyFilters.LastWrite
    };
    _watcher.Changed += (s, e) =>
    {
        _cachedSettings = null; // Invalidate cache
    };
    _watcher.EnableRaisingEvents = true;
}
```

---

## Performance Profiling

### Measuring Latency
```csharp
public async Task<string> TranscribeWithMetrics(string audioPath)
{
    var stopwatch = Stopwatch.StartNew();

    // Audio preprocessing
    var preprocessStart = stopwatch.ElapsedMilliseconds;
    var processedAudio = await _preprocessor.ProcessAsync(audioPath);
    var preprocessTime = stopwatch.ElapsedMilliseconds - preprocessStart;

    // Whisper transcription
    var whisperStart = stopwatch.ElapsedMilliseconds;
    var text = await _whisper.TranscribeAsync(processedAudio);
    var whisperTime = stopwatch.ElapsedMilliseconds - whisperStart;

    // Post-processing
    var postprocessStart = stopwatch.ElapsedMilliseconds;
    var finalText = _postprocessor.Process(text);
    var postprocessTime = stopwatch.ElapsedMilliseconds - postprocessStart;

    stopwatch.Stop();

    // Log metrics
    MetricsTracker.RecordTranscription(new TranscriptionMetrics
    {
        TotalTime = stopwatch.ElapsedMilliseconds,
        PreprocessTime = preprocessTime,
        WhisperTime = whisperTime,
        PostprocessTime = postprocessTime,
        AudioLengthSeconds = GetAudioLength(audioPath),
    });

    return finalText;
}
```

### Memory Profiling
```csharp
public class MemoryMonitor
{
    private long _previousMemory;

    public void StartMonitoring()
    {
        _previousMemory = GC.GetTotalMemory(forceFullCollection: false);
    }

    public long GetMemoryDelta()
    {
        var currentMemory = GC.GetTotalMemory(forceFullCollection: false);
        var delta = currentMemory - _previousMemory;
        _previousMemory = currentMemory;
        return delta;
    }

    public void LogMemoryUsage(string operation)
    {
        var process = Process.GetCurrentProcess();
        var workingSet = process.WorkingSet64 / 1024 / 1024; // MB
        var gcMemory = GC.GetTotalMemory(false) / 1024 / 1024; // MB

        Console.WriteLine($"{operation}: WorkingSet={workingSet}MB, GC={gcMemory}MB");
    }
}
```

### CPU Profiling
```csharp
// Use diagnostic tools
// - dotnet-trace (built-in)
// - JetBrains dotTrace
// - Visual Studio Profiler

// Measure method CPU time
public async Task<T> MeasureCpuTime<T>(Func<Task<T>> operation)
{
    var threadTimeBefore = Process.GetCurrentProcess().TotalProcessorTime;

    var result = await operation();

    var threadTimeAfter = Process.GetCurrentProcess().TotalProcessorTime;
    var cpuTime = (threadTimeAfter - threadTimeBefore).TotalMilliseconds;

    Console.WriteLine($"CPU time: {cpuTime}ms");
    return result;
}
```

---

## Common Performance Issues

### Issue: Memory Leak After Transcriptions
**Symptom**: RAM increases by 5-10MB per transcription
**Diagnosis**:
```csharp
// Run 100 transcriptions and monitor memory
for (int i = 0; i < 100; i++)
{
    await TranscribeAsync(testAudio);
    GC.Collect();
    GC.WaitForPendingFinalizers();
    var memoryMB = Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024;
    Console.WriteLine($"After {i+1}: {memoryMB}MB");
}
```

**Common Causes**:
1. WaveFileWriter not disposed
2. Event handlers not unsubscribed
3. Process objects not disposed
4. Large buffers not cleared

**Fix**: Ensure all IDisposable resources are disposed, unsubscribe events

### Issue: Slow First Transcription
**Symptom**: First transcription takes 10s, subsequent ones 2s
**Cause**: Model not loaded into memory
**Fix**: Implement warmup process
```csharp
public async Task WarmupAsync()
{
    // Generate 1-second silent audio
    var warmupPath = Path.Combine(Path.GetTempPath(), "warmup.wav");
    GenerateSilentWav(warmupPath, duration: 1);

    // Transcribe to load model
    await TranscribeAsync(warmupPath);

    // Clean up
    File.Delete(warmupPath);
}
```

### Issue: High CPU During Idle
**Symptom**: CPU usage 15-20% when app is idle
**Diagnosis**: Check for polling loops, timers, or background threads
```csharp
// ❌ BAD - Polling loop
while (true)
{
    if (CheckCondition())
        DoSomething();
    Thread.Sleep(10); // Still consumes CPU!
}

// ✅ GOOD - Event-driven
someObject.PropertyChanged += (s, e) => DoSomething();
```

### Issue: UI Freezing During Transcription
**Symptom**: UI unresponsive for 3-5 seconds
**Cause**: Blocking UI thread
**Fix**: Use async/await and Dispatcher
```csharp
// ❌ BLOCKS UI
private void OnHotkeyPressed()
{
    var text = TranscribeAsync().Result; // Blocks!
    lblStatus.Content = text;
}

// ✅ NON-BLOCKING
private async void OnHotkeyPressed()
{
    var text = await TranscribeAsync();
    await Dispatcher.InvokeAsync(() => {
        lblStatus.Content = text;
    });
}
```

---

## Performance Testing Scenarios

### Scenario 1: Sustained Load
```csharp
// Simulate 100 transcriptions over 10 minutes
for (int i = 0; i < 100; i++)
{
    await TranscribeAsync(testAudio);
    await Task.Delay(6000); // 6 seconds between transcriptions

    // Check memory every 10 iterations
    if (i % 10 == 0)
    {
        var memoryMB = Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024;
        Console.WriteLine($"After {i} transcriptions: {memoryMB}MB");
    }
}
```

### Scenario 2: Burst Load
```csharp
// Simulate rapid transcription requests
var tasks = new List<Task>();
for (int i = 0; i < 10; i++)
{
    tasks.Add(TranscribeAsync(testAudio));
}
await Task.WhenAll(tasks);
// Expected: Semaphore ensures only 1 concurrent transcription
```

### Scenario 3: Long-Running Session
```csharp
// Simulate 8-hour work session
var startTime = DateTime.Now;
int transcriptionCount = 0;

while (DateTime.Now - startTime < TimeSpan.FromHours(8))
{
    await TranscribeAsync(testAudio);
    transcriptionCount++;
    await Task.Delay(Random.Shared.Next(30000, 300000)); // 30s-5min delay

    // Log metrics every hour
    if (transcriptionCount % 20 == 0)
    {
        LogPerformanceMetrics(transcriptionCount, DateTime.Now - startTime);
    }
}
```

---

## Performance Regression Testing

### Automated Performance Tests
```csharp
[Fact]
public async Task TranscriptionLatency_ShouldBeLessThan3Seconds()
{
    // Arrange
    var testAudio = TestData.GetShortAudio(); // 5-second clip
    var service = new PersistentWhisperService(settings);
    await service.WarmupAsync(); // Exclude warmup from measurement

    // Act
    var stopwatch = Stopwatch.StartNew();
    var text = await service.TranscribeAsync(testAudio);
    stopwatch.Stop();

    // Assert
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000);
}

[Fact]
public void MemoryUsage_ShouldNotLeakAfter100Transcriptions()
{
    // Arrange
    var initialMemory = Process.GetCurrentProcess().WorkingSet64;

    // Act
    for (int i = 0; i < 100; i++)
    {
        TranscribeAsync(testAudio).Wait();
        if (i % 10 == 0)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    // Assert
    var finalMemory = Process.GetCurrentProcess().WorkingSet64;
    var leakMB = (finalMemory - initialMemory) / 1024 / 1024;
    leakMB.Should().BeLessThan(50); // Allow 50MB tolerance
}
```

### CI/CD Performance Gates
```yaml
# .github/workflows/performance-tests.yml
- name: Run Performance Tests
  run: dotnet test --filter "Category=Performance"

- name: Check Performance Metrics
  run: |
    if [ $TRANSCRIPTION_LATENCY -gt 3000 ]; then
      echo "Performance regression detected!"
      exit 1
    fi
```

---

## Performance Optimization Checklist

Before each release:
- [ ] Startup time < 2s (measure with Stopwatch)
- [ ] Idle RAM < 100MB (check Task Manager)
- [ ] No memory leaks (run 100 transcriptions, check RAM growth)
- [ ] Transcription latency within targets (Small < 3s)
- [ ] UI responsive during transcription (no freezes)
- [ ] CPU usage < 5% idle, < 80% active
- [ ] Temp files cleaned up immediately
- [ ] All IDisposable resources disposed
- [ ] Event handlers unsubscribed in Dispose()
- [ ] Async/await used for I/O operations
- [ ] No blocking calls on UI thread

---

## Performance Monitoring in Production

### Metrics Collection
```csharp
public class MetricsTracker
{
    public static void RecordTranscription(TranscriptionMetrics metrics)
    {
        // Send to analytics (if user opted in)
        if (Settings.Load().EnableAnalytics)
        {
            AnalyticsService.Track("transcription_completed", new
            {
                duration_ms = metrics.TotalTime,
                audio_length_s = metrics.AudioLengthSeconds,
                model = metrics.ModelUsed,
            });
        }
    }
}
```

### User-Facing Performance Dashboard
```csharp
// Show performance stats in settings
public class PerformanceStats
{
    public double AverageLatencyMs { get; set; }
    public int TotalTranscriptions { get; set; }
    public long TotalMemoryMB { get; set; }
    public DateTime LastReset { get; set; }
}
```

---

## References
- .NET Performance Tips: https://docs.microsoft.com/en-us/dotnet/core/performance/
- Memory Management: https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/
- Async Best Practices: https://docs.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming

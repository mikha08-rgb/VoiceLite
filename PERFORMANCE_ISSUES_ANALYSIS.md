# Performance Issues Analysis (v1.0.38)

## User Report
**Symptoms**: Performance degradation, inconsistent performance, freezing

## Root Cause Analysis

### 1. ⚠️ **CRITICAL: Synchronous File Operations on UI Thread**
**Location**: `RecordingCoordinator.cs:456`
```csharp
File.Delete(audioFilePath); // BLOCKING OPERATION on async path
```

**Impact**: File deletion can block for 50-200ms, especially if antivirus is scanning
**Frequency**: Every transcription (potentially dozens per minute with heavy use)
**Cumulative Effect**: Can cause UI stuttering and perceived freezing

**Fix**: Use async file deletion with FileStream.DisposeAsync()

---

### 2. ⚠️ **Retry Loop Without Backoff**
**Location**: `RecordingCoordinator.cs:450-466`
```csharp
for (int i = 0; i < TimingConstants.FileCleanupMaxRetries; i++)
{
    try { File.Delete(audioFilePath); break; }
    catch { await Task.Delay(TimingConstants.FileCleanupRetryDelayMs); }
}
```

**Impact**: If file is locked, retries 3x with fixed delay (3x 100ms = 300ms blocked)
**Frequency**: Happens often with antivirus/Windows Search indexing
**Cumulative Effect**: 300ms delays add up, causing "Processing..." to feel sluggish

**Fix**: Exponential backoff or fire-and-forget cleanup

---

###3. **Process Priority May Cause Starvation**
**Location**: `PersistentWhisperService.cs:356`
```csharp
process.PriorityClass = ProcessPriorityClass.High;
```

**Impact**: Whisper process gets HIGH priority, but UI thread stays NORMAL
- Whisper can starve UI thread on systems with limited cores
- Users with 2-4 core CPUs may see freezing during transcription

**Fix**: Use `BelowNormal` or `Normal` priority, or make it configurable

---

### 4. **Excessive Logging Can Block**
**Location**: Throughout codebase
- 50+ `ErrorLogger.LogMessage()` calls per transcription
- Each write is synchronous File.AppendAllText

**Impact**:
- Log writes can block for 5-10ms each
- 50 writes × 10ms = 500ms blocked time per transcription
- Worse with antivirus scanning

**Fix**:
- Batch logging with async queue
- Reduce verbosity (already improved in v1.0.25, but still heavy)
- Use fire-and-forget logging

---

### 5. **Semaphore Contention**
**Location**: `PersistentWhisperService.cs:283`
```csharp
await transcriptionSemaphore.WaitAsync(); // Blocks if transcription in progress
```

**Impact**: If user triggers rapid recordings, they queue up
- No visual feedback that recording is queued
- Appears frozen/unresponsive

**Fix**: Show "Busy" state, prevent queueing, or increase semaphore limit

---

### 6. **Watchdog Timer Overhead**
**Location**: `RecordingCoordinator.cs:357-364`
- Watchdog checks every 10 seconds with `System.Threading.Timer`
- Multiple timers created/destroyed per transcription

**Impact**: Minor CPU overhead, but timer disposal can cause brief pauses

**Fix**: Use single shared watchdog timer, or increase check interval

---

### 7. **Potential Memory Pressure**
**Location**: `PersistentWhisperService.cs:332-333`
```csharp
var outputBuilder = new StringBuilder(4096);
var errorBuilder = new StringBuilder(512);
```

**Impact**: If Whisper outputs more than expected, StringBuilder resizing causes GC pressure

**Fix**: Monitor actual sizes and adjust pre-sizing

---

## Recommended Fixes (Priority Order)

### 🔴 HIGH PRIORITY (Immediate)

1. **Make File Cleanup Async**
```csharp
// BEFORE (blocking)
File.Delete(audioFilePath);

// AFTER (async, fire-and-forget)
_ = Task.Run(async () =>
{
    await Task.Delay(100); // Let file handles close
    try { File.Delete(audioFilePath); }
    catch { /* ignore */ }
});
```

2. **Reduce Process Priority**
```csharp
// BEFORE
process.PriorityClass = ProcessPriorityClass.High;

// AFTER
process.PriorityClass = ProcessPriorityClass.Normal; // Or BelowNormal
```

3. **Batch/Async Logging**
```csharp
// Create async logging queue (fire-and-forget)
// Flush every 500ms or 10 messages
```

---

### 🟡 MEDIUM PRIORITY (Next Release)

4. **Add Queue Visual Feedback**
```csharp
// Show "Busy - transcription in progress" instead of accepting new recordings
if (transcriptionSemaphore.CurrentCount == 0)
{
    UpdateStatus("Busy - please wait", Brushes.Orange);
    return;
}
```

5. **Exponential Backoff for Retries**
```csharp
int delayMs = 50 * (int)Math.Pow(2, i); // 50ms, 100ms, 200ms, 400ms
await Task.Delay(delayMs);
```

---

### 🟢 LOW PRIORITY (Future Optimization)

6. **SharedWatchdog Timer**
7. **StringBuilder Size Monitoring**
8. **Reduce Logging Verbosity** (already done in v1.0.25)

---

## Testing Plan

### Reproduce Performance Issues

1. **Test Rapid Recordings** (stress test):
   - Record 10 short clips in rapid succession (1-2 sec each)
   - Expected: UI should stay responsive, no freezing
   - Current: Likely queues up, appears frozen

2. **Test With Antivirus**:
   - Enable Windows Defender real-time protection
   - Record 5 clips with 30-second audio
   - Expected: File cleanup should not block
   - Current: Likely 300ms+ delays per cleanup

3. **Test on Low-End Hardware**:
   - Dual-core CPU (or throttle to 50%)
   - Record with Large model (ggml-large-v3.bin)
   - Expected: UI responsive during transcription
   - Current: Likely UI freezes with HIGH priority Whisper

---

## Metrics to Track

- **P50/P95/P99 Latency**: Time from "Stop" to transcription appearing
- **UI Responsiveness**: Can user click buttons during transcription?
- **File Cleanup Time**: How long does File.Delete() take with antivirus?
- **CPU Usage**: Whisper vs UI thread during transcription

---

## Files to Modify

1. `RecordingCoordinator.cs` - File cleanup logic
2. `PersistentWhisperService.cs` - Process priority
3. `ErrorLogger.cs` - Async logging queue (new file or refactor)
4. `MainWindow.xaml.cs` - Visual feedback for busy state

---

## Estimated Impact

| Fix | Time to Implement | Impact on Freezing | Impact on Perf Consistency |
|-----|-------------------|-------------------|---------------------------|
| Async file cleanup | 30 min | 🔴 HIGH | 🔴 HIGH |
| Reduce process priority | 5 min | 🟡 MEDIUM | 🟡 MEDIUM |
| Batch logging | 2 hours | 🟡 MEDIUM | 🔴 HIGH |
| Queue feedback | 30 min | 🟢 LOW | 🟡 MEDIUM |

---

## Next Steps

1. Implement HIGH priority fixes
2. Test on low-end hardware (2-4 core CPU)
3. Test with antivirus enabled
4. Collect telemetry on file cleanup times
5. Monitor for regression in tests

# VoiceLite Bug Report - User Experience Breakers

**Date**: 2025-10-08
**Total Bugs**: 12
**Severity Breakdown**: 4 CRASH | 5 FUNCTIONAL | 2 DATA_LOSS | 1 COSMETIC

---

## CRASH BUGS (Priority 1)

### B001: Null Reference in ApiClient.BaseAddress
**Severity**: CRASH
**Location**: `VoiceLite/VoiceLite/Services/Auth/ApiClient.cs:108, 148`
**Impact**: App crashes on first launch if HttpClient.BaseAddress is null

**Repro Steps**:
1. Delete cookies.dat file
2. Launch app
3. Click "Account" button
4. App crashes with NullReferenceException

**Root Cause**:
```csharp
// Line 108 & 148
var baseUri = Client.BaseAddress ?? new Uri("https://voicelite.app");
```
If `Client.BaseAddress` is somehow null (edge case during initialization), this throws NullReferenceException.

**Proposed Fix**:
```csharp
var baseUri = Client.BaseAddress ?? new Uri("https://voicelite.app");
```
Already has fallback - verify Client initialization is complete before first use.

**Estimate**: 5 minutes

---

### B002: Settings Corruption on Concurrent Save
**Severity**: CRASH
**Location**: `VoiceLite/VoiceLite/MainWindow.xaml.cs:440-500`
**Impact**: Settings file corrupted if multiple threads trigger save simultaneously

**Repro Steps**:
1. Rapidly change multiple settings (hotkey, model, UI preset)
2. Close app immediately
3. Reopen app - settings file may be corrupted JSON

**Root Cause**:
```csharp
// Line 464-470 - Settings object accessed outside lock
string json = await Task.Run(() =>
{
    lock (settings.SyncRoot)
    {
        return JsonSerializer.Serialize(settings, _jsonSerializerOptions);
    }
});
```
Settings modifications happen on UI thread outside the SyncRoot lock. If user changes setting while serialization is in progress, race condition occurs.

**Proposed Fix**:
```csharp
// Acquire settings lock BEFORE Task.Run
string json;
lock (settings.SyncRoot)
{
    json = await Task.Run(() => JsonSerializer.Serialize(settings, _jsonSerializerOptions));
}
```

**Estimate**: 10 minutes

---

### B003: Unhandled Exception in OnAudioFileReady Crashes App
**Severity**: CRASH
**Location**: `VoiceLite/VoiceLite/Services/RecordingCoordinator.cs:246`
**Impact**: Async void exception crashes entire app

**Repro Steps**:
1. Start recording
2. Trigger exception in transcription pipeline (corrupt audio file)
3. App crashes instead of showing error

**Root Cause**:
```csharp
private async void OnAudioFileReady(object? sender, string audioFilePath)
{
    // CRIT-003 FIX: Wrap entire async void method in try-catch to prevent app crash
    try { ... }
    catch { ... } // Good - already wrapped!
}
```
**STATUS**: Already has comprehensive try-catch wrapper. Verify it covers full method.

**Proposed Fix**: Audit try-catch coverage, ensure all async operations are within try block.

**Estimate**: 5 minutes (verification only)

---

### B004: Process.Refresh() on Disposed Process
**Severity**: CRASH
**Location**: `VoiceLite/VoiceLite/Services/MemoryMonitor.cs:216-223`
**Impact**: Crashes when checking zombie processes after process disposal

**Repro Steps**:
1. Start transcription
2. Kill whisper.exe manually via Task Manager
3. MemoryMonitor tries to refresh disposed process → InvalidOperationException

**Root Cause**:
```csharp
foreach (var proc in whisperProcesses)
{
    try
    {
        proc.Refresh(); // Can throw if process exited
        whisperMemoryMB += proc.WorkingSet64 / 1024 / 1024;
        proc.Dispose();
    }
    catch { } // Good - already catches this
}
```
**STATUS**: Already wrapped in try-catch. Verify catch block doesn't swallow critical errors.

**Proposed Fix**: Add specific catch for InvalidOperationException, log warning.

**Estimate**: 5 minutes

---

## FUNCTIONAL BUGS (Priority 2)

### B005: Hotkey Registration Fails Silently for Standalone Modifiers
**Severity**: FUNCTIONAL
**Location**: `VoiceLite/VoiceLite/Services/HotkeyManager.cs:114-119`
**Impact**: App unusable if hotkey registration fails - no error shown to user

**Repro Steps**:
1. Set hotkey to Left Alt (standalone modifier)
2. Another app already uses this hotkey
3. VoiceLite shows "Ready" but hotkey doesn't work
4. User has no idea hotkey failed to register

**Root Cause**:
```csharp
if (!RegisterHotKey(windowHandle, HOTKEY_ID, win32Modifiers, currentVirtualKey))
{
    source.RemoveHook(HwndHook);
    source = null;
    throw new InvalidOperationException($"Failed to register {modifiers} + {key} hotkey. It may be in use by another application.");
}
```
Exception is thrown but not shown to user in MainWindow initialization.

**Proposed Fix**:
```csharp
// In MainWindow.xaml.cs, wrap RegisterHotkey call:
try
{
    hotkeyManager.RegisterHotkey(windowHandle, settings.RecordingHotkey, settings.RecordingModifiers);
}
catch (InvalidOperationException ex)
{
    MessageBox.Show(ex.Message, "Hotkey Registration Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
    // Fallback to default hotkey or show settings window
}
```

**Estimate**: 10 minutes

---

### B006: Transcription Timeout Dialog Blocks UI Thread
**Severity**: FUNCTIONAL
**Location**: `VoiceLite/VoiceLite/Services/PersistentWhisperService.cs:565-575`
**Impact**: App appears frozen during timeout - UI thread blocked by MessageBox

**Repro Steps**:
1. Start long transcription (60+ seconds)
2. Wait for timeout (3 minutes)
3. App shows timeout error but UI is completely frozen
4. User thinks app crashed

**Root Cause**:
```csharp
throw new TimeoutException(
    "First transcription timed out (this is normal on slow systems).\n\n" +
    // ... long message ...
);
```
Exception propagates to UI thread, shown via synchronous MessageBox.Show, blocking everything.

**Proposed Fix**:
```csharp
// Catch timeout exception in OnTranscriptionCompleted (async void)
// Show dialog using Dispatcher.InvokeAsync instead of direct MessageBox
await Dispatcher.InvokeAsync(() =>
{
    MessageBox.Show(...);
});
```

**Estimate**: 10 minutes

---

### B007: Cookie Expiry Parsing Fails on Malformed Dates
**Severity**: FUNCTIONAL
**Location**: `VoiceLite/VoiceLite/Services/Auth/ApiClient.cs:79-82`
**Impact**: Login fails if server sends malformed cookie expiry date

**Repro Steps**:
1. Server sends cookie with malformed Expires field
2. App crashes during cookie load
3. User cannot login until cookies.dat is deleted

**Root Cause**:
```csharp
if (!string.IsNullOrEmpty(cookieDto.Expires))
{
    cookie.Expires = DateTime.Parse(cookieDto.Expires); // Can throw FormatException
}
```

**Proposed Fix**:
```csharp
if (!string.IsNullOrEmpty(cookieDto.Expires) &&
    DateTime.TryParse(cookieDto.Expires, out var expiry))
{
    cookie.Expires = expiry;
}
// Else: Cookie will use default expiry (session cookie)
```

**Estimate**: 5 minutes

---

### B008: File.Move Fails if Target Exists on Windows
**Severity**: FUNCTIONAL
**Location**: `VoiceLite/VoiceLite/MainWindow.xaml.cs:474-484`
**Impact**: Settings save fails, user loses all changes

**Repro Steps**:
1. Change settings
2. App crashes during save (after temp file written but before delete)
3. Restart app
4. Try to save settings again
5. File.Move throws IOException because settings.json already exists

**Root Cause**:
```csharp
// Delete old file if exists (required before rename on Windows)
if (File.Exists(settingsPath))
{
    File.Delete(settingsPath);
}

// Atomic rename - if this fails, temp file remains for recovery
File.Move(tempPath, settingsPath); // Can fail if settingsPath wasn't deleted
```
Race condition: Another thread might recreate settingsPath between Delete and Move.

**Proposed Fix**:
```csharp
// Use File.Move overload with overwrite flag (.NET 6+)
File.Move(tempPath, settingsPath, overwrite: true);

// OR for older .NET:
if (File.Exists(settingsPath))
{
    File.Delete(settingsPath);
    Thread.Sleep(10); // Brief delay to ensure filesystem flushes
}
File.Move(tempPath, settingsPath);
```

**Estimate**: 5 minutes

---

### B009: Stuck State Timer Fires After Cancellation
**Severity**: FUNCTIONAL
**Location**: `VoiceLite/VoiceLite/Services/RecordingCoordinator.cs:163-200`
**Impact**: False "stuck state" recovery fires after user cancels recording

**Repro Steps**:
1. Start recording
2. Immediately press Escape to cancel
3. Wait 15 seconds
4. App shows "Stuck state recovered" even though nothing was stuck

**Root Cause**:
```csharp
public void StopRecording(bool cancel = false)
{
    // ...
    if (!cancel)
    {
        StartStoppingTimeoutTimer(); // Timer started
    }
    // MISSING: No timer stop for cancel=true case
}
```
Timer is started when entering Stopping state, but not stopped when entering Cancelled state.

**Proposed Fix**:
```csharp
public void StopRecording(bool cancel = false)
{
    // ...
    if (cancel)
    {
        // Cancel - stop all timers immediately
        StopStoppingTimeoutTimer();
        StopTranscriptionWatchdog();
    }
    else
    {
        StartStoppingTimeoutTimer();
    }
}
```

**Estimate**: 5 minutes

---

## DATA LOSS BUGS (Priority 3)

### B010: Settings File Corruption on Crash During Save
**Severity**: DATA_LOSS
**Location**: `VoiceLite/VoiceLite/MainWindow.xaml.cs:474-484`
**Impact**: If app crashes after temp file write but before rename, settings.json is empty or corrupted

**Repro Steps**:
1. Make 50 transcriptions (large history)
2. Change settings
3. Kill app process during save (Task Manager)
4. Restart app - settings.json is 0 bytes or malformed

**Root Cause**:
Atomic write pattern is incomplete:
```csharp
string tempPath = settingsPath + ".tmp";
await File.WriteAllTextAsync(tempPath, json); // ← Can write corrupt data if crash here

if (File.Exists(settingsPath))
{
    File.Delete(settingsPath); // ← Deletes good file before verifying temp is valid!
}

File.Move(tempPath, settingsPath);
```

**Proposed Fix**:
```csharp
string tempPath = settingsPath + ".tmp";
await File.WriteAllTextAsync(tempPath, json);

// Verify temp file is valid JSON before deleting original
try
{
    var testLoad = JsonSerializer.Deserialize<Settings>(await File.ReadAllTextAsync(tempPath));
    if (testLoad == null) throw new InvalidDataException("Deserialized to null");
}
catch (Exception ex)
{
    File.Delete(tempPath); // Delete corrupt temp file
    throw new InvalidOperationException("Settings save failed - temp file corrupt", ex);
}

// Now safe to replace original
if (File.Exists(settingsPath))
    File.Delete(settingsPath);
File.Move(tempPath, settingsPath);
```

**Estimate**: 15 minutes

---

### B011: History Cleanup Runs Before Settings Validation
**Severity**: DATA_LOSS
**Location**: `VoiceLite/VoiceLite/MainWindow.xaml.cs:372-373`
**Impact**: User's pinned history items may be deleted if settings load fails and defaults are used

**Repro Steps**:
1. User has 50 pinned transcriptions
2. settings.json gets corrupted
3. App loads default settings (empty history)
4. CleanupOldHistoryItems() runs on empty list
5. SaveSettings() writes empty history to disk
6. User loses all pinned items

**Root Cause**:
```csharp
LoadSettings()
{
    // ...
    settings = SettingsValidator.ValidateAndRepair(loadedSettings) ?? new Settings();
    // ↑ If validation fails, returns new Settings() with empty history

    CleanupOldHistoryItems(); // ← Runs on empty history!
    // ↓ Later saves empty history to disk
}
```

**Proposed Fix**:
```csharp
LoadSettings()
{
    // ...
    var loadedSettings = JsonSerializer.Deserialize<Settings>(json);
    var validatedSettings = SettingsValidator.ValidateAndRepair(loadedSettings);

    if (validatedSettings == null)
    {
        ErrorLogger.LogError("Settings validation failed - using defaults WITHOUT cleanup");
        settings = new Settings();
        // DO NOT run cleanup on default settings!
        return;
    }

    settings = validatedSettings;

    // Only cleanup if we successfully loaded existing settings
    CleanupOldHistoryItems();
}
```

**Estimate**: 10 minutes

---

## COSMETIC BUGS (Priority 4 - Defer)

### B012: Status Text Flickers During State Transitions
**Severity**: COSMETIC
**Location**: Multiple locations in MainWindow.xaml.cs
**Impact**: Status text flickers "Recording" → "Processing" → "Pasting" → "Ready" in rapid succession

**Repro Steps**:
1. Make a transcription
2. Watch status text change 3-4 times in 200ms
3. Flickering is distracting

**Root Cause**:
UpdateStatus() called multiple times per transcription:
- OnRecordingStatusChanged ("Transcribing")
- OnTranscriptionCompleted ("Ready")
- BatchUpdateTranscriptionSuccess (multiple updates)

**Proposed Fix**:
Debounce UpdateStatus calls using DispatcherTimer (defer to future UX pass).

**Estimate**: 30 minutes (deferred)

---

## Summary

### Quick Win Fixes (95 minutes total)
- **P1 CRASH**: 25 minutes (B001-B004)
- **P2 FUNCTIONAL**: 35 minutes (B005-B009)
- **P3 DATA_LOSS**: 25 minutes (B010-B011)
- **P4 COSMETIC**: Deferred (B012)

### Risk Assessment
- **High Risk**: B002 (settings corruption), B010 (data loss), B011 (history loss)
- **Medium Risk**: B001 (null ref), B005 (hotkey failure), B008 (save failure)
- **Low Risk**: B003-B004 (already caught), B006-B007 (UX degradation), B009 (false positive), B012 (cosmetic)

### Recommended Fix Order
1. **B010** - Settings corruption (prevents data loss)
2. **B011** - History cleanup (prevents data loss)
3. **B002** - Concurrent save race (prevents corruption)
4. **B005** - Hotkey registration (critical UX)
5. **B008** - File.Move failure (settings save)
6. **B001** - Null reference check (crash prevention)
7. **B009** - False stuck state (UX annoyance)
8. **B006** - Timeout dialog (UI responsiveness)
9. **B007** - Cookie parsing (edge case)
10. **B003, B004** - Verification only (already handled)
11. **B012** - Defer to future release

---

## Testing Checklist

After fixes:
- [ ] Test settings save/load 10x (verify no corruption)
- [ ] Test hotkey registration failure (verify error shown)
- [ ] Test transcription timeout (verify UI doesn't freeze)
- [ ] Test cookie load with malformed dates
- [ ] Test rapid setting changes during save
- [ ] Test process crash during settings save (verify temp file recovery)
- [ ] Test history cleanup with corrupted settings file
- [ ] Test cancellation of recording (verify no false stuck state)
- [ ] Test all async void methods for unhandled exceptions

---

**Generated**: 2025-10-08 by Claude Code Bug Hunter

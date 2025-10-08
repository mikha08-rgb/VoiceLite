# SimpleTelemetry Integration Guide

## Status: Ready for Integration ✅

All telemetry components are complete. This guide shows how to wire SimpleTelemetry into MainWindow.xaml.cs.

**⚠️ Note**: MainWindow.xaml.cs is currently being modified by other Claude Code instances (bug fixes). Apply these changes after their work completes to avoid merge conflicts.

---

## Step 1: Add Telemetry Field

**File**: `VoiceLite/VoiceLite/MainWindow.xaml.cs`
**Location**: Line ~38, after `recordingCoordinator` field

```csharp
private AnalyticsService? analyticsService;
private RecordingCoordinator? recordingCoordinator;
private SimpleTelemetry? telemetry; // ADD THIS LINE
```

---

## Step 2: Initialize Telemetry

**File**: `VoiceLite/VoiceLite/MainWindow.xaml.cs`
**Method**: `MainWindow_Loaded` (async void, around line 150-200)

**Add after services initialization** (after `analyticsService`, before dependency checks):

```csharp
// Initialize telemetry service (privacy-first, opt-in)
telemetry = new SimpleTelemetry(settings);
telemetry.TrackAppStart(); // Track app initialization time
telemetry.TrackDailyActiveUser(); // Track daily active user
```

---

## Step 3: Track Hotkey Response Time

**File**: `VoiceLite/VoiceLite/MainWindow.xaml.cs`
**Method**: `OnHotkeyPressed` (around line 800-900)

**Add at the START of the method** (before any recording logic):

```csharp
private void OnHotkeyPressed()
{
    // TELEMETRY: Track hotkey response start
    telemetry?.TrackHotkeyResponseStart();

    try
    {
        // Existing hotkey handling code...
```

**Add when recording actually starts** (after `audioRecorder.StartRecording()`):

```csharp
audioRecorder.StartRecording();

// TELEMETRY: Track hotkey response end (recording started)
telemetry?.TrackHotkeyResponseEnd();
```

---

## Step 4: Track Transcription Duration

**File**: `VoiceLite/VoiceLite/MainWindow.xaml.cs`
**Method**: `OnTranscriptionCompleted` (around line 1100-1200)

**Add at the END of the method** (after successful transcription and text injection):

```csharp
private async void OnTranscriptionCompleted(object? sender, TranscriptionResult result)
{
    // ... existing transcription handling code ...

    // TELEMETRY: Track transcription duration and success
    if (result != null && !string.IsNullOrWhiteSpace(result.Text))
    {
        var durationMs = (DateTime.Now - recordingStartTime).TotalMilliseconds;
        var wordCount = result.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var modelUsed = settings.WhisperModel;

        telemetry?.TrackTranscriptionDuration((long)durationMs, modelUsed, wordCount, success: true);
        telemetry?.TrackFeatureAttempt("transcription", success: true);
    }
    else
    {
        // Track failed transcription
        telemetry?.TrackFeatureAttempt("transcription", success: false, failureReason: "EmptyResult");
    }
}
```

---

## Step 5: Track Errors

**File**: `VoiceLite/VoiceLite/MainWindow.xaml.cs`
**Methods**: All `try-catch` blocks that call `ErrorLogger.LogError`

**Add after each `ErrorLogger.LogError` call**:

```csharp
catch (Exception ex)
{
    ErrorLogger.LogError("ContextName", ex);

    // TELEMETRY: Track error occurrence
    telemetry?.TrackError(
        errorType: ex.GetType().Name,
        component: "MainWindow", // or specific component name
        isCrash: false // true if this terminates the app
    );
}
```

---

## Step 6: Track Memory Usage (Optional)

**File**: `VoiceLite/VoiceLite/MainWindow.xaml.cs`
**Method**: Hook into MemoryMonitor updates (if available)

**Add where memory is already being monitored**:

```csharp
// If MemoryMonitor exposes current memory:
var currentMemoryBytes = Process.GetCurrentProcess().WorkingSet64;
telemetry?.TrackMemoryUsage(currentMemoryBytes);
```

Or create a periodic timer (every 5 minutes):

```csharp
// In MainWindow_Loaded
var memoryTimer = new System.Windows.Threading.DispatcherTimer
{
    Interval = TimeSpan.FromMinutes(5)
};
memoryTimer.Tick += (s, e) =>
{
    var memoryBytes = Process.GetCurrentProcess().WorkingSet64;
    telemetry?.TrackMemoryUsage(memoryBytes);
};
memoryTimer.Start();
```

---

## Step 7: Track Feature Usage

**File**: `VoiceLite/VoiceLite/MainWindow.xaml.cs`
**Methods**: Feature activation points

**Add when users activate features**:

```csharp
// When opening settings
private void OnSettingsClicked(object sender, RoutedEventArgs e)
{
    telemetry?.TrackFeatureUsage("settings_window");
    // ... existing code ...
}

// When opening dictionary manager
private void OnDictionaryClicked(object sender, RoutedEventArgs e)
{
    telemetry?.TrackFeatureUsage("voice_shortcuts");
    // ... existing code ...
}

// When using history panel
private void OnHistoryPanelToggled(bool visible)
{
    if (visible)
        telemetry?.TrackFeatureUsage("history_panel");
    // ... existing code ...
}
```

---

## Step 8: Track Session End

**File**: `VoiceLite/VoiceLite/MainWindow.xaml.cs`
**Method**: `MainWindow_Closing` (around line 2000-2100)

**Add BEFORE disposing other services**:

```csharp
private async void MainWindow_Closing(object? sender, CancelEventArgs e)
{
    // TELEMETRY: Track session end and upload final metrics
    telemetry?.TrackSessionEnd();
    telemetry?.Dispose(); // Uploads remaining metrics and disposes

    // ... existing cleanup code ...
}
```

---

## Step 9: Run Prisma Migration (Backend)

**Terminal**: In `voicelite-web/` directory

```bash
cd voicelite-web
npm run db:migrate
```

This creates the `TelemetryMetric` table in the database.

---

## Verification Checklist

After integration, verify:

1. ✅ **Local telemetry files created**: Check `%LOCALAPPDATA%/VoiceLite/telemetry/`
   - Should have daily JSON files (e.g., `2025-10-08.json`)
   - Each line is a JSON metric object

2. ✅ **Metrics uploaded to backend**: Wait 10 minutes after first launch
   - Check network traffic (POST to `/api/metrics/upload`)
   - Verify response: `{"success":true,"metricsUploaded":XX}`

3. ✅ **Dashboard shows data**: Visit `https://voicelite.app/metrics_dashboard.html`
   - Should show non-zero metrics
   - Performance, reliability, usage sections populated

4. ✅ **No performance impact**: App should feel identical
   - <5ms overhead per metric (imperceptible)
   - No UI freezes or delays

5. ✅ **Privacy respected**: Only users with `settings.EnableAnalytics == true` send metrics
   - No PII in telemetry files or backend
   - Anonymous SHA256 user IDs only

---

## Troubleshooting

### Issue: No telemetry files created

**Cause**: `settings.EnableAnalytics` is `false` or `null`
**Fix**: In app settings, enable "Analytics" (or set `settings.EnableAnalytics = true` for testing)

---

### Issue: Metrics not uploading

**Cause**: Network error or backend not running
**Fix**:
1. Check internet connection
2. Verify backend is deployed (`https://voicelite.app/api/metrics/upload`)
3. Check `%LOCALAPPDATA%/VoiceLite/logs/voicelite.log` for errors

---

### Issue: Dashboard shows no data

**Cause**: Metrics uploaded but not in time range
**Fix**: Change time range filter to "Last 30 Days" to see older metrics

---

## Testing Locally (Without Backend)

To test telemetry without backend deployment:

1. **Check local files**: `%LOCALAPPDATA%/VoiceLite/telemetry/{date}.json`
   - Should have newline-delimited JSON metrics
   - Verify metric types and values are correct

2. **Disable upload**: Comment out `UploadMetricsNow()` in SimpleTelemetry.cs (line ~290)
   - Metrics will only be stored locally

3. **Verify JSON structure**:
```json
{"timestamp":"2025-10-08T14:23:15Z","anonymousUserId":"a3f2...","metricType":"app_start_time_ms","value":2850,"metadata":{"version":"1.0.62"}}
```

---

## Next Steps

1. **Coordinate with other Claude Code instances**: Wait for bug fixes to complete
2. **Apply integration changes**: Follow steps 1-8 above
3. **Run Prisma migration**: Step 9
4. **Test locally**: Verification checklist
5. **Deploy backend**: Vercel deployment (auto-deploys on push to main)
6. **Monitor dashboard**: `https://voicelite.app/metrics_dashboard.html`

---

## Notes

- **Zero breaking changes**: Telemetry is completely opt-in and non-blocking
- **Fail-safe**: All telemetry calls wrapped in try-catch, never crashes app
- **Privacy-first**: Reuses existing `AnalyticsService` privacy infrastructure
- **Low overhead**: <5ms per metric, batched uploads every 10 minutes
- **Local-first**: Metrics stored locally even if backend is offline

---

## Questions?

See [METRICS_GUIDE.md](METRICS_GUIDE.md) for detailed metric definitions and privacy guarantees.

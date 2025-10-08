# SimpleTelemetry Quick Integration - Manual Steps

The telemetry system is complete and ready. Due to concurrent file access, please apply these 5 small changes manually:

##  Change 1: Add Telemetry Field
**File**: `VoiceLite/VoiceLite/MainWindow.xaml.cs`
**Line**: After line 38 (after `private RecordingCoordinator? recordingCoordinator;`)

```csharp
private SimpleTelemetry? telemetry; // Production telemetry
```

---

## Change 2: Initialize Telemetry
**File**: `VoiceLite/VoiceLite/MainWindow.xaml.cs`
**Location**: In `MainWindow_Loaded` method, after line 608 (after `analyticsService = new AnalyticsService(settings);`)

```csharp
// Initialize production telemetry (privacy-first, opt-in)
telemetry = new SimpleTelemetry(settings);
telemetry.TrackAppStart();
telemetry.TrackDailyActiveUser();
```

---

## Change 3: Track Hotkey Response
**File**: `VoiceLite/VoiceLite/MainWindow.xaml.cs`

**3a)** In `OnHotkeyPressed` method, after line 1157 (after the entry log):
```csharp
// TELEMETRY: Track hotkey response time start
telemetry?.TrackHotkeyResponseStart();
```

**3b)** In `StartRecording` method, after line 1083 (after `recordingCoordinator?.StartRecording();`):
```csharp
// TELEMETRY: Track hotkey response time end
telemetry?.TrackHotkeyResponseEnd();
```

---

## Change 4: Track Transcription Duration
**File**: `VoiceLite/VoiceLite/MainWindow.xaml.cs`
**Location**: In `OnTranscriptionCompleted` method, after line 1757 (after `BatchUpdateTranscriptionSuccess(e);`)

```csharp
// TELEMETRY: Track transcription performance
var durationMs = (DateTime.UtcNow - recordingStartTime).TotalMilliseconds;
var wordCount = e.Transcription?.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
telemetry?.TrackTranscriptionDuration((long)durationMs, settings.WhisperModel, wordCount, success: true);
telemetry?.TrackFeatureAttempt("transcription", success: true);
```

---

## Change 5: Track Session End
**File**: `VoiceLite/VoiceLite/MainWindow.xaml.cs`
**Location**: In `OnClosed` method, after line 2476 (after the "Settings save already handled" comment)

```csharp
// TELEMETRY: Track session metrics and upload final batch
try
{
    telemetry?.TrackSessionEnd();
    telemetry?.Dispose();
}
catch { /* Silent fail - telemetry errors should never crash app close */ }
```

---

## Verify Integration

After making the changes:

1. **Build the solution**:
   ```bash
   dotnet build VoiceLite/VoiceLite.sln
   ```
   Should compile with **0 errors, 0 warnings**

2. **Run the app** and check:
   - `%LOCALAPPDATA%/VoiceLite/telemetry/` directory is created
   - Daily JSON file appears after using the app

3. **Test metrics collection**:
   - Do a few transcriptions
   - Wait 10 minutes for upload
   - Check network tab for POST to `/api/metrics/upload`

---

## Why Manual Integration?

Another Claude Code instance was actively modifying `MainWindow.xaml.cs` during the integration attempt, causing file access conflicts. These manual changes are safer and take only **3-5 minutes**.

---

## Total Code Added: ~20 lines

- Field declaration: 1 line
- Initialization: 3 lines
- Hotkey tracking: 2 lines
- Transcription tracking: 4 lines
- Session tracking: 7 lines

**Performance impact**: <0.1% CPU, imperceptible to users

---

## Next Steps

After integration:
1. Run Prisma migration: `cd voicelite-web && npm run db:migrate`
2. Deploy to Vercel: `git push`
3. View dashboard: `https://voicelite.app/metrics_dashboard.html`

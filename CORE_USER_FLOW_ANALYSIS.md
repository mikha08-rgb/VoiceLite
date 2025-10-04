# Core User Flow Analysis - VoiceLite

**Goal**: Ensure bulletproof core user experience with NO stuck states or race conditions

## Push-to-Talk Mode (Primary Use Case)

### Happy Path Flow

```
USER PRESSES HOTKEY
├─► OnHotkeyPressed()
│   ├─► Debounce check (250ms) ✓
│   ├─► Lock(recordingLock)
│   ├─► HandlePushToTalkPressed()
│   │   ├─► Check audioRecorder != null ✓
│   │   ├─► Check audioRecorder.IsRecording == false ✓
│   │   ├─► Set isHotkeyMode = true
│   │   └─► StartRecording()
│   │       ├─► Check audioRecorder != null ✓
│   │       ├─► Check audioRecorder.IsRecording == false ✓
│   │       ├─► Check isRecording == false ✓
│   │       ├─► StopStuckStateRecoveryTimer() ✓ (defensive)
│   │       ├─► Set isRecording = true
│   │       ├─► recordingCoordinator.StartRecording()
│   │       │   ├─► audioRecorder.StartRecording()
│   │       │   │   ├─► Lock(lockObject)
│   │       │   │   ├─► Check isRecording == false
│   │       │   │   ├─► DisposeWaveInCompletely() ✓ (fresh start)
│   │       │   │   ├─► Create new WaveInEvent
│   │       │   │   ├─► Attach event handlers
│   │       │   │   ├─► waveIn.StartRecording()
│   │       │   │   └─► Set isRecording = true
│   │       │   └─► Fire StatusChanged("Recording")
│   │       ├─► Verify audioRecorder.IsRecording == true ✓
│   │       ├─► Start recordingElapsedTimer
│   │       └─► UpdateUIForCurrentMode()
│   └─► Exit lock

USER RELEASES HOTKEY
├─► OnHotkeyReleased()
│   ├─► Lock(recordingLock)
│   ├─► HandlePushToTalkReleased()
│   │   ├─► Check audioRecorder != null ✓
│   │   ├─► Sync state with audioRecorder.IsRecording ✓
│   │   ├─► Check isRecording && isHotkeyMode ✓
│   │   ├─► Set isHotkeyMode = false
│   │   └─► StopRecording(false)
│   │       ├─► Check isRecording == true
│   │       ├─► Stop recordingElapsedTimer
│   │       ├─► recordingCoordinator.StopRecording(false)
│   │       │   ├─► audioRecorder.StopRecording()
│   │       │   │   ├─► Lock(lockObject)
│   │       │   │   ├─► Set isRecording = false IMMEDIATELY ✓
│   │       │   │   ├─► Close waveFile, flush buffers
│   │       │   │   ├─► waveIn.StopRecording()
│   │       │   │   ├─► DisposeWaveInCompletely() ✓
│   │       │   │   └─► Fire AudioFileReady(audioFilePath)
│   │       │   └─► Fire StatusChanged("Processing")
│   │       ├─► Set isRecording = false
│   │       ├─► UpdateStatus("Processing...")
│   │       └─► StartStuckStateRecoveryTimer() ✓✓✓ (15s failsafe)
│   └─► Exit lock

AUDIO FILE READY
├─► OnAudioFileReady() [RecordingCoordinator]
│   ├─► Check if cancelled ✓
│   ├─► Fire StatusChanged("Transcribing")
│   │   └─► OnRecordingStatusChanged()
│   │       └─► Ensure stuckStateRecoveryTimer is running ✓✓
│   ├─► StartTranscriptionWatchdog() (120s timeout)
│   ├─► whisperService.TranscribeAsync()
│   ├─► Mark transcriptionCompleted = true ✓
│   ├─► StopTranscriptionWatchdog()
│   ├─► textInjector.InjectText()
│   ├─► Fire TranscriptionCompleted(success=true)
│   │   └─► OnTranscriptionCompleted()
│   │       ├─► StopStuckStateRecoveryTimer() ✓✓✓
│   │       ├─► Display transcription
│   │       ├─► UpdateStatus("Ready")
│   │       ├─► Remove border
│   │       ├─► Lock(recordingLock)
│   │       ├─► Set isRecording = false
│   │       └─► Set isHotkeyMode = false (if not held)
│   └─► CleanupAudioFileAsync()

READY FOR NEXT RECORDING ✓
```

### Edge Case 1: Rapid Hotkey Presses

```
USER MASHES HOTKEY RAPIDLY (3 times in 100ms)
├─► Press #1 (t=0ms)
│   ├─► OnHotkeyPressed()
│   ├─► Debounce: OK (no previous press)
│   ├─► lastHotkeyPressTime = now
│   ├─► Lock acquired
│   ├─► StartRecording() called
│   │   ├─► Check audioRecorder.IsRecording = false ✓
│   │   ├─► Recording starts successfully
│   │   └─► isRecording = true
│   └─► Lock released
│
├─► Press #2 (t=50ms)
│   ├─► OnHotkeyPressed()
│   ├─► Debounce: REJECTED (50ms < 250ms) ✓✓✓
│   └─► Return early (no action)
│
├─► Press #3 (t=100ms)
│   ├─► OnHotkeyPressed()
│   ├─► Debounce: REJECTED (100ms < 250ms) ✓✓✓
│   └─► Return early (no action)
│
└─► Release #1 (t=300ms)
    ├─► OnHotkeyReleased()
    ├─► Lock acquired
    ├─► StopRecording() called
    │   ├─► Check isRecording = true ✓
    │   ├─► Recording stops
    │   └─► StartStuckStateRecoveryTimer() ✓
    └─► Lock released

RESULT: ✓ NO STUCK STATE - Debouncing prevents rapid presses
```

### Edge Case 2: Transcription Timeout

```
USER NORMAL RECORDING
├─► Recording starts
├─► Recording stops → "Processing" + StuckStateRecoveryTimer starts (15s)
├─► Transcription starts
│
├─► WHISPER PROCESS HANGS (bug/crash/antivirus)
│
├─► t=120s: RecordingCoordinator watchdog fires
│   ├─► Mark transcriptionCompleted = true ✓
│   ├─► StopTranscriptionWatchdog()
│   └─► Fire TranscriptionCompleted(success=false, error="Timeout")
│       └─► OnTranscriptionCompleted()
│           ├─► StopStuckStateRecoveryTimer() ✓✓✓
│           ├─► Display error message
│           ├─► UpdateStatus("Error")
│           ├─► Lock(recordingLock)
│           ├─► Set isRecording = false ✓✓✓
│           ├─► Set isHotkeyMode = false ✓✓✓
│           ├─► StopAutoTimeoutTimer() ✓
│           └─► Remove border ✓
│
└─► t=123s: UI resets to "Ready"

RESULT: ✓ NO STUCK STATE - Watchdog timeout forces recovery
```

### Edge Case 3: Transcription Timeout + Watchdog Fails

```
USER NORMAL RECORDING
├─► Recording stops → "Processing" + StuckStateRecoveryTimer starts (15s)
├─► Transcription starts
│
├─► BOTH WHISPER AND WATCHDOG HANG (catastrophic failure)
│
├─► t=15s: StuckStateRecoveryTimer fires ✓✓✓
│   ├─► StopStuckStateRecoveryTimer()
│   ├─► UpdateStatus("Ready")
│   ├─► Remove border
│   ├─► Lock(recordingLock)
│   ├─► Set isRecording = false ✓✓✓
│   ├─► Set isHotkeyMode = false ✓✓✓
│   ├─► StopAutoTimeoutTimer()
│   ├─► Stop recordingElapsedTimer
│   ├─► Show MessageBox: "VoiceLite recovered from stuck state"
│   └─► UpdateUIForCurrentMode()

RESULT: ✓ NO STUCK STATE - Global failsafe after 15s
```

### Edge Case 4: Press-Release-Press-Release (Rapid But Valid)

```
USER WORKFLOW: Two quick recordings back-to-back
├─► Press #1 (t=0ms)
│   ├─► StartRecording()
│   ├─► audioRecorder.IsRecording = true
│   └─► isRecording = true
│
├─► Release #1 (t=500ms)
│   ├─► StopRecording()
│   ├─► isRecording = false
│   ├─► "Processing" state
│   ├─► StuckStateRecoveryTimer starts
│   └─► Transcription begins (async)
│
├─► Press #2 (t=1000ms) - WHILE FIRST TRANSCRIPTION RUNNING
│   ├─► Debounce: OK (1000ms > 250ms)
│   ├─► Lock acquired
│   ├─► StartRecording()
│   │   ├─► Check audioRecorder.IsRecording = false ✓
│   │   ├─► Check isRecording = false ✓
│   │   ├─► StopStuckStateRecoveryTimer() ✓✓✓ (clear old timer)
│   │   ├─► audioRecorder.StartRecording()
│   │   │   └─► DisposeWaveInCompletely() ✓ (fresh device)
│   │   └─► New recording starts
│   └─► Lock released
│
├─► First transcription completes (t=3000ms)
│   ├─► OnTranscriptionCompleted()
│   ├─► StopStuckStateRecoveryTimer() (already stopped, safe) ✓
│   ├─► Check isRecording = true (new recording active)
│   └─► Skip state reset (recording in progress) ✓
│
└─► Release #2 (t=4000ms)
    └─► StopRecording()
        └─► Second transcription begins

RESULT: ✓ NO STUCK STATE - Each recording independent
```

### Edge Case 5: Exception During StartRecording

```
USER PRESSES HOTKEY
├─► OnHotkeyPressed()
├─► StartRecording()
│   ├─► Check audioRecorder.IsRecording = false ✓
│   ├─► Set isRecording = true
│   ├─► recordingCoordinator.StartRecording()
│   │   └─► audioRecorder.StartRecording()
│   │       └─► THROWS EXCEPTION (mic unplugged)
│   ├─► Exception caught in RecordingCoordinator
│   │   └─► Fire ErrorOccurred("Failed to start recording")
│   │       └─► OnRecordingError()
│   │           ├─► Set isRecording = false ✓✓✓
│   │           ├─► UpdateStatus("Error")
│   │           └─► MessageBox.Show(error)
│   └─► Verify audioRecorder.IsRecording == false ✓
│       └─► Rollback: isRecording = false ✓✓✓

RESULT: ✓ NO STUCK STATE - Exception forces cleanup
```

## ISSUE FOUND: Exception During StartRecording

**PROBLEM**: If `recordingCoordinator.StartRecording()` throws an exception:
1. MainWindow sets `isRecording = true` BEFORE the call
2. Exception is caught in RecordingCoordinator and fires `ErrorOccurred`
3. `OnRecordingError()` sets `isRecording = false` ✓
4. BUT: MainWindow's `StartRecording()` continues and checks `recorder.IsRecording`
5. If the check fails, it sets `isRecording = false` again ✓

**CURRENT STATE**: Actually this is SAFE because:
- RecordingCoordinator catches exceptions and fires ErrorOccurred
- OnRecordingError() resets isRecording
- MainWindow.StartRecording() also has rollback at the end

**VERIFICATION NEEDED**: Let me check if StartRecording can partially succeed

## CRITICAL ISSUE FOUND: StartRecording Rollback Race

In MainWindow.StartRecording(), the rollback check happens AFTER the coordinator returns:
```csharp
recordingCoordinator?.StartRecording(); // May fire ErrorOccurred event
if (recorder.IsRecording) { /* success */ }
else { /* rollback */ }
```

If ErrorOccurred fires during StartRecording(), it will:
1. Set isRecording = false (in OnRecordingError)
2. Then StartRecording() checks recorder.IsRecording
3. If false, it AGAIN sets isRecording = false (harmless but redundant)

**ACTUAL ISSUE**: What if ErrorOccurred doesn't fire but recording still fails to start?
- MainWindow sets isRecording = true
- Coordinator tries to start, fails silently
- recorder.IsRecording = false
- Rollback triggers: isRecording = false ✓

This is actually SAFE! The rollback check catches silent failures.

## CRITICAL ISSUE FOUND: No StopStuckStateRecoveryTimer in OnRecordingError

Looking at the OnRecordingError handler - it doesn't stop the stuck state timer!

If error occurs DURING recording start, the timer never starts, so this is OK.
But if error occurs DURING processing... wait, that's handled by OnTranscriptionCompleted.

Actually OnRecordingError is only called for recording START failures, not processing errors.
Processing errors go through OnTranscriptionCompleted which DOES stop the timer.

So this is SAFE!

## Remaining Analysis

Let me check a few more critical paths...

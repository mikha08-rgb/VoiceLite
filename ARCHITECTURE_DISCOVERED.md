# VoiceLite Architecture - Discovered Structure

**Generated**: 2025-10-08
**Purpose**: Map the actual implementation (not the intended design) of VoiceLite

---

## Executive Summary

VoiceLite is a **dual-stack application**:
- **Desktop**: .NET 8.0 WPF app (Windows-only) - handles audio recording and transcription locally
- **Web Backend**: Next.js 15 + PostgreSQL (Vercel-hosted) - manages licensing and analytics

**Pattern**: Not MVVM (despite being WPF). More of a **Service-Coordinator pattern** with event-driven orchestration.

---

## 1. Desktop Application (.NET 8.0 WPF)

### Entry Points

```
App.xaml.cs (line 14)
  ↓
MainWindow.xaml.cs (line 82: constructor)
  ↓
MainWindow_Loaded (async initialization)
```

### Core Components

#### MainWindow (2,183 lines - the God Object)
**Path**: `VoiceLite/VoiceLite/MainWindow.xaml.cs`

**Responsibilities** (too many):
- Service initialization (~500 lines)
- Hotkey event handling
- UI state management
- Settings persistence
- Child window lifecycle
- Recording coordination (delegates to RecordingCoordinator)

**Key Fields** (line 25-77):
```csharp
// Services (nullable - initialized async)
private AudioRecorder? audioRecorder;
private ITranscriber? whisperService;           // Actually PersistentWhisperService
private HotkeyManager? hotkeyManager;
private TextInjector? textInjector;
private SystemTrayManager? systemTrayManager;
private MemoryMonitor? memoryMonitor;
private TranscriptionHistoryService? historyService;
private SoundService? soundService;
private AnalyticsService? analyticsService;
private RecordingCoordinator? recordingCoordinator;  // CRITICAL: Orchestrates flow

// State
private readonly object recordingLock = new object();
private readonly SemaphoreSlim saveSettingsSemaphore = new SemaphoreSlim(1, 1);

// Timers (DANGER: 5 timer types)
private System.Timers.Timer? autoTimeoutTimer;
private System.Windows.Threading.DispatcherTimer? recordingElapsedTimer;
private System.Windows.Threading.DispatcherTimer? settingsSaveTimer;
private System.Windows.Threading.DispatcherTimer? stuckStateRecoveryTimer;

// Child windows (DANGER: Not always disposed)
private SettingsWindowNew? currentSettingsWindow;
private DictionaryManagerWindow? currentDictionaryWindow;
private LoginWindow? currentLoginWindow;
private FeedbackWindow? currentFeedbackWindow;
private AnalyticsConsentWindow? currentAnalyticsConsentWindow;
```

---

## 2. Critical Flow: Hotkey → Transcribed Text

### Step-by-Step Execution Path

#### **Step 1: User Presses Hotkey (Left Alt by default)**

**File**: `VoiceLite/VoiceLite/Services/HotkeyManager.cs`

```
Line 180: StartPollingForModifierKey(Key key)
  → Spawns background Task that polls GetAsyncKeyState() every 15ms
  → Line 198: Detects key press via Win32 API
  → Line 218: RunOnDispatcher(() => HotkeyPressed?.Invoke(...))
```

**Why Polling?**: `RegisterHotKey` Win32 API doesn't work for standalone modifier keys (Left Alt, Left Ctrl). Fallback to polling (line 84).

---

#### **Step 2: MainWindow Receives HotkeyPressed Event**

**File**: `VoiceLite/VoiceLite/MainWindow.xaml.cs`

```
Line 602: hotkeyManager.HotkeyPressed += OnHotkeyPressed;

Line 1093: OnHotkeyPressed(object? sender, EventArgs e)
  → Line 1102: Debounce check (50ms)
  → Line 1113: Check settings.Mode (PushToTalk or Toggle)
  → Line 1259: StartRecording()
```

---

#### **Step 3: RecordingCoordinator Starts Audio Capture**

**File**: `VoiceLite/VoiceLite/MainWindow.xaml.cs:976`

```csharp
private void StartRecording()
{
    lock (recordingLock)
    {
        // Line 1024: Delegate to coordinator
        recordingCoordinator?.StartRecording();

        // Line 1027: Verify recording actually started
        if (recorder.IsRecording) {
            // Start UI timer for elapsed time
            recordingElapsedTimer?.Start();
        }
    }
}
```

**File**: `VoiceLite/VoiceLite/Services/RecordingCoordinator.cs:85`

```csharp
public void StartRecording()
{
    lock (recordingLock)
    {
        // Line 93: State machine transition Idle → Recording
        if (!stateMachine.TryTransition(RecordingState.Recording))
            return; // Reject if already recording

        // Line 123: Actual audio capture starts
        audioRecorder.StartRecording();

        // Line 126: Notify UI via event
        StatusChanged?.Invoke(this, new RecordingStatusEventArgs {
            Status = "Recording",
            IsRecording = true
        });
    }
}
```

---

#### **Step 4: AudioRecorder Captures Audio via NAudio**

**File**: `VoiceLite/VoiceLite/Services/AudioRecorder.cs:234`

```csharp
public void StartRecording()
{
    lock (lockObject)
    {
        // Line 258: CRITICAL - Always dispose old waveIn first
        DisposeWaveInCompletely();

        // Line 272: Create fresh NAudio device
        waveIn = new WaveInEvent {
            WaveFormat = new WaveFormat(16000, 16, 1), // Whisper format
            BufferMilliseconds = 30,
            NumberOfBuffers = 3
        };

        // Line 281: Attach event handlers
        waveIn.DataAvailable += OnDataAvailable;

        // Line 288: Memory buffer mode (enforced)
        audioMemoryStream = new MemoryStream();
        waveFile = new WaveFileWriter(audioMemoryStream, waveIn.WaveFormat);

        // Line 292: Start capture
        waveIn.StartRecording();
    }
}

// Line 311: OnDataAvailable fires every 30ms with audio chunks
private void OnDataAvailable(object? sender, WaveInEventArgs e)
{
    lock (lockObject)
    {
        // Line 347: Instance ID check (prevents late callbacks from old sessions)
        if (callbackInstanceId != currentRecordingInstanceId)
            return; // CRITICAL: Discard stale audio data

        // Line 377: Rent buffer from pool
        buffer = ArrayPool<byte>.Shared.Rent(e.BytesRecorded);

        // Line 379: Apply volume scaling (0.8x)
        // Line 392: Write to memory stream
        localWaveFile.Write(buffer, 0, pairCount * 2);

        // Line 406: Return buffer to pool (clearArray: true for security)
        ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
    }
}
```

**DANGER**: If instance ID check fails, late callbacks can corrupt next recording session.

---

#### **Step 5: User Releases Hotkey (Stop Recording)**

**File**: `VoiceLite/VoiceLite/Services/HotkeyManager.cs:410`

```
Line 410: StartReleaseMonitoring()
  → Polls every 10ms to detect key release
  → Line 443: RunOnDispatcher(() => HotkeyReleased?.Invoke(...))
```

**File**: `VoiceLite/VoiceLite/MainWindow.xaml.cs:1146`

```csharp
Line 1146: OnHotkeyReleased(object? sender, EventArgs e)
  → Line 1292: StopRecording(false) // false = don't cancel
```

**File**: `VoiceLite/VoiceLite/Services/RecordingCoordinator.cs:163`

```csharp
public void StopRecording(bool cancel = false)
{
    lock (recordingLock)
    {
        // Line 172: State transition Recording → Stopping (or Cancelled)
        RecordingState targetState = cancel ? RecordingState.Cancelled : RecordingState.Stopping;
        stateMachine.TryTransition(targetState);

        // Line 185: Stop audio capture
        audioRecorder.StopRecording();

        // Line 198: Start safety timer (recovers if AudioFileReady never fires)
        if (!cancel) {
            StartStoppingTimeoutTimer(); // 10-second timeout
        }
    }
}
```

**File**: `VoiceLite/VoiceLite/Services/AudioRecorder.cs:452`

```csharp
public void StopRecording()
{
    lock (lockObject)
    {
        // Line 461: Set isRecording=false IMMEDIATELY
        isRecording = false;

        // Line 470: Flush and close wave file
        waveFile.Flush();
        waveFile.Dispose();

        // Line 481: Get audio data from memory
        var audioData = audioMemoryStream.ToArray();

        // Line 499: Fire event with audio bytes
        AudioDataReady?.Invoke(this, audioData);

        // Line 532: Dispose waveIn device IMMEDIATELY
        waveIn.Dispose();
        waveIn = null; // No more audio capture
    }
}
```

**CRITICAL**: Event `AudioDataReady` fires, which triggers `RecordingCoordinator.OnAudioFileReady()`.

---

#### **Step 6: Transcription via Whisper.cpp Subprocess**

**File**: `VoiceLite/VoiceLite/Services/RecordingCoordinator.cs:246`

```csharp
private async void OnAudioFileReady(object? sender, string audioFilePath)
{
    // Line 252: Stop safety timer (AudioFileReady fired successfully)
    StopStoppingTimeoutTimer();

    // Line 279: Process audio file
    await ProcessAudioFileAsync(audioFilePath);
}

private async Task ProcessAudioFileAsync(string audioFilePath)
{
    // Line 359: State transition Stopping → Transcribing
    stateMachine.TryTransition(RecordingState.Transcribing);

    // Line 374: Start watchdog timer (120-second timeout)
    StartTranscriptionWatchdog();

    // Line 395: Run transcription with retry logic (max 3 attempts)
    for (int attempt = 1; attempt <= 3; attempt++)
    {
        transcription = await Task.Run(async () =>
            await whisperService.TranscribeAsync(audioFilePath));
        break; // Success
    }

    // Line 489: State transition Transcribing → Injecting
    stateMachine.TryTransition(RecordingState.Injecting);

    // Line 508: Inject text into active window
    await Task.Run(() => textInjector.InjectText(transcription));

    // Line 513: State transition Injecting → Complete
    stateMachine.TryTransition(RecordingState.Complete);

    // Line 555: Fire TranscriptionCompleted event
    TranscriptionCompleted?.Invoke(this, eventArgs);

    // Line 571: State transition Complete → Idle
    stateMachine.TryTransition(RecordingState.Idle);
}
```

**File**: `VoiceLite/VoiceLite/Services/PersistentWhisperService.cs:321`

```csharp
public async Task<string> TranscribeAsync(string audioFilePath)
{
    // Line 345: Acquire semaphore (only 1 transcription at a time)
    await transcriptionSemaphore.WaitAsync();

    // Line 367: Resolve whisper.exe path
    var whisperExePath = cachedWhisperExePath ?? ResolveWhisperExePath();

    // Line 372: Build command arguments
    var arguments = $"-m \"{modelPath}\" -f \"{audioFilePath}\" " +
                    $"--no-timestamps --language {settings.Language} " +
                    $"--beam-size {settings.BeamSize} --best-of {settings.BestOf}";

    // Line 393: Spawn whisper.exe subprocess
    process = new Process { StartInfo = processStartInfo };
    process.Start();

    // Line 417: Track process ID (for zombie detection)
    lock (processLock) {
        activeProcessIds.Add(process.Id);
    }

    // Line 477: Wait for process with smart timeout
    bool exited = await Task.Run(() => process.WaitForExit(timeoutSeconds * 1000));

    // Line 607: Clean up output (remove [whisper] system messages)
    result = cleanedResult.ToString().Trim();

    // Line 611: Post-process transcription (capitalization, filler words, etc.)
    result = TranscriptionPostProcessor.ProcessTranscription(result, settings.UseEnhancedDictionary, customDict, settings.PostProcessing);

    // Line 620: Return transcribed text
    return result;
}
```

**DANGER**: If timeout occurs (line 479), process is killed but may become zombie. Tracked via `activeProcessIds`.

---

#### **Step 7: Text Injection via Clipboard or Keyboard Simulation**

**File**: `VoiceLite/VoiceLite/Services/TextInjector.cs:51`

```csharp
public void InjectText(string text)
{
    // Line 68: Decide method based on text length and context
    if (ShouldUseTyping(text)) {
        InjectViaTyping(text);
    } else {
        InjectViaClipboard(text);
    }
}

private bool ShouldUseTyping(string text)
{
    switch (settings.TextInjectionMode)
    {
        case TextInjectionMode.SmartAuto:
            // Line 123: Use typing for short text (<50 chars)
            if (text.Length < SHORT_TEXT_THRESHOLD)
                return true;
            // Line 129: Use typing for sensitive content (passwords)
            return false;
    }
}

// Line 150: Check if focused field is password field (Win32 API)
private bool IsInSecureField()
{
    IntPtr focusedHandle = GetFocus();
    // Line 162: Check window class name for "password" or "secure"
    // Line 179: Check ES_PASSWORD style flag
    return (style & ES_PASSWORD) == ES_PASSWORD;
}
```

**CRITICAL**: Uses `H.InputSimulator` library for keyboard simulation, clipboard for paste.

---

### State Machine Flow

**File**: `VoiceLite/VoiceLite/Services/RecordingStateMachine.cs`

```
Idle
 ↓ (StartRecording)
Recording
 ↓ (StopRecording)
Stopping
 ↓ (AudioFileReady)
Transcribing
 ↓ (Whisper completes)
Injecting
 ↓ (Text pasted/typed)
Complete → Idle
```

**Failure paths**:
- Any state → Error → Idle
- Recording → Cancelled → Idle

---

## 3. Web Backend (Next.js 15 + PostgreSQL)

### Architecture

```
voicelite-web/
├── app/
│   ├── api/                    # API routes (22 total)
│   │   ├── auth/
│   │   │   ├── request/        # Magic link request
│   │   │   ├── otp/            # OTP verification
│   │   │   └── logout/         # Session logout
│   │   ├── licenses/
│   │   │   ├── activate/       # Device activation
│   │   │   ├── issue/          # Ed25519 signed license
│   │   │   ├── crl/            # Certificate Revocation List
│   │   │   └── validate/       # License validation
│   │   ├── analytics/
│   │   │   └── event/          # Privacy-first analytics
│   │   ├── checkout/           # Stripe checkout
│   │   └── webhook/            # Stripe webhooks
│   └── page.tsx                # Landing page
└── prisma/
    └── schema.prisma           # PostgreSQL schema
```

### Desktop → Web Communication

**Desktop File**: `VoiceLite/VoiceLite/Services/Licensing/LicenseService.cs`

```csharp
// Line 29: Ed25519 public key (embedded in desktop app)
private static string ResolvedLicensePublicKey => GetKeyOrFallback(...);

// Line 160: Issue license from backend
var response = await ApiClient.PostAsync<LicensePayload>(
    "/api/licenses/issue",
    new { DeviceFingerprint = fingerprint }
);

// Line 198: Verify Ed25519 signature locally (offline)
public bool VerifySignature(LicensePayload license)
{
    // Line 203: Load public key from environment or fallback
    var publicKeyBytes = Convert.FromBase64String(ResolvedLicensePublicKey);

    // Line 217: Verify signature using BouncyCastle.Cryptography
    var result = Ed25519.Verify(signatureBytes, messageBytes, publicKeyBytes);

    // Signature valid → License is authentic
    return result;
}
```

**Backend File**: `voicelite-web/app/api/licenses/issue/route.ts`

```typescript
// Sign license payload with Ed25519 private key
import { sign } from '@noble/ed25519';

export async function POST(request: Request) {
  // Fetch user's license from database
  const license = await prisma.license.findFirst({
    where: { userId: session.userId }
  });

  // Create license payload
  const payload = {
    email: user.email,
    tier: license.tier,
    expiresAt: license.expiresAt,
    deviceFingerprint: deviceFingerprint
  };

  // Sign with Ed25519 private key (environment variable)
  const signature = await sign(
    Buffer.from(JSON.stringify(payload)),
    Buffer.from(process.env.LICENSE_PRIVATE_KEY!, 'hex')
  );

  return Response.json({
    ...payload,
    signature: Buffer.from(signature).toString('base64')
  });
}
```

**Security**: Desktop app cannot forge licenses without private key (server-side only).

---

## 4. Service Layer Architecture

### Service Categorization

#### **Recording & Audio**
- `AudioRecorder.cs` - NAudio wrapper, memory buffer mode
- `PersistentWhisperService.cs` - Whisper.cpp subprocess manager
- `WhisperServerService.cs` - Experimental HTTP server mode (5x faster)
- `RecordingCoordinator.cs` - Orchestrates recording workflow

#### **User Interface**
- `HotkeyManager.cs` - Win32 hotkey registration + polling
- `SystemTrayManager.cs` - System tray icon and context menu
- `SoundService.cs` - Wood-tap-click.ogg sound feedback

#### **Text Processing**
- `TextInjector.cs` - Clipboard paste or keyboard simulation
- `TranscriptionPostProcessor.cs` - Capitalization, filler word removal (70+ compiled regex patterns)
- `TranscriptionHistoryService.cs` - History panel with pinning

#### **Infrastructure**
- `ErrorLogger.cs` - Static logger (writes to %LocalAppData%\VoiceLite\logs\voicelite.log)
- `MemoryMonitor.cs` - Memory usage tracking
- `StartupDiagnostics.cs` - First-run health checks
- `DependencyChecker.cs` - VC++ Runtime validation

#### **Backend Integration**
- `AnalyticsService.cs` - Privacy-first opt-in analytics (SHA256 anonymous IDs)
- `AuthenticationService.cs` - Magic link + OTP authentication
- `LicenseService.cs` - Ed25519 license validation

---

## 5. Data Flow & Storage

### Desktop App Data Locations

**AppData Path**: `%LocalAppData%\VoiceLite\` (moved from Roaming for privacy)

```
C:\Users\{User}\AppData\Local\VoiceLite\
├── settings.json                # User settings (NOT synced across PCs)
├── license.dat                  # Ed25519 signed license
├── crl.dat                      # Certificate Revocation List cache
├── cookies.dat                  # Auth session cookies
├── logs\
│   └── voicelite.log            # Error logs (10MB max, rotates)
└── temp\
    ├── dummy_warmup.wav         # Whisper warmup file
    └── recording_*.wav          # Temp audio files (auto-cleanup)
```

**Windows Temp**: `C:\Users\{User}\AppData\Local\Temp\VoiceLite\audio\`
- Auto-cleanup every 30 minutes (files older than 30 mins)

### Settings Persistence

**File**: `VoiceLite/VoiceLite/MainWindow.xaml.cs:2091`

```csharp
private async Task SaveSettingsAsync()
{
    // Line 2097: Acquire semaphore (prevents concurrent saves)
    await saveSettingsSemaphore.WaitAsync();

    try {
        // Line 2106: Serialize to JSON with pre-configured options
        string json = JsonSerializer.Serialize(settings, _jsonSerializerOptions);

        // Line 2109: Write to AppData
        await File.WriteAllTextAsync(GetSettingsPath(), json);
    }
    finally {
        // Line 2113: Release semaphore
        saveSettingsSemaphore.Release();
    }
}
```

**DANGER**: If semaphore is disposed before Release(), throws `SemaphoreFullException`.

---

## 6. Patterns & Anti-Patterns

### Patterns (Good)

1. **Service Coordinator Pattern**: `RecordingCoordinator` orchestrates complex workflow
2. **State Machine**: `RecordingStateMachine` prevents invalid state transitions
3. **Event-Driven**: Services communicate via events (loose coupling)
4. **Watchdog Timers**: Detect stuck states and auto-recover

### Anti-Patterns (Bad)

1. **God Object**: `MainWindow` has 2,183 lines, 50+ responsibilities
2. **Static Singletons**: `ErrorLogger`, `ApiClient.Client` (static HttpClient)
3. **No Dependency Injection**: Services manually instantiated in MainWindow
4. **Mixed Concerns**: UI logic + business logic in same class
5. **Event Leaks**: Event handlers not always unsubscribed (memory leaks)

---

## 7. Technology Stack Summary

### Desktop (.NET 8.0)
- **Framework**: WPF (Windows Presentation Foundation)
- **Language**: C# 10+
- **Audio**: NAudio 2.2.1 (WaveInEvent, WaveFileWriter)
- **AI**: Whisper.cpp (external subprocess, GGML models)
- **Keyboard**: H.InputSimulator 1.2.1 (Win32 SendInput wrapper)
- **Crypto**: BouncyCastle.Cryptography 2.4.0 (Ed25519 signatures)
- **Serialization**: System.Text.Json 9.0.9

### Web Backend (Next.js 15)
- **Framework**: Next.js 15.5.4 (App Router)
- **Language**: TypeScript 5
- **Database**: PostgreSQL (Supabase) + Prisma ORM 6.1.0
- **Payments**: Stripe 18.5.0
- **Email**: Resend 6.1.0
- **Rate Limiting**: Upstash Redis + @upstash/ratelimit
- **Crypto**: @noble/ed25519 3.0.0 (Ed25519 signing)

### Deployment
- **Desktop**: Inno Setup installer (543MB with Pro model)
- **Web**: Vercel (serverless API routes)
- **Distribution**: GitHub Releases + Google Drive

---

## 8. Known Architectural Debt

### Critical Issues

1. **No Clear Separation of Concerns**
   - MainWindow does everything (UI + orchestration + lifecycle)
   - Should be split into Presenter, ViewModel, Services

2. **Static State Everywhere**
   - `ApiClient.Client` - static HttpClient
   - `ErrorLogger` - static logger
   - `PersistentWhisperService.activeProcessIds` - static process tracker

3. **Manual Memory Management**
   - 200+ event subscriptions to track
   - 6 child window types to dispose
   - 5 timer types across services

4. **Fragile State Machine**
   - 7 states, 12 transitions
   - Force-reset mechanism suggests frequent stuck states

5. **Process Lifecycle Hell**
   - Whisper.exe zombies tracked in static HashSet
   - Cleanup logic scattered across 5 files

---

## Conclusion

VoiceLite is a **working prototype** that grew into production without refactoring. The code shows signs of:
- **Vibe coding**: Features added incrementally without architectural planning
- **Defensive coding**: Extensive error handling, retry logic, watchdogs
- **Performance hacks**: Memory buffer mode, greedy decoding, process pooling

The app **works reliably** but is **difficult to maintain** due to:
- High coupling between components
- God object anti-pattern
- Manual resource management
- State machine complexity

**Next**: See `CRITICAL_PATHS.md` for exact code paths and `DANGER_ZONES.md` for memory leak suspects.

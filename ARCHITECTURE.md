# VoiceLite Architecture

**Version**: v1.4.0.0
**Framework**: .NET 8.0, WPF
**Pattern**: Direct instantiation (no DI container, no MVVM controllers)

---

## Overview

VoiceLite is a Windows desktop speech-to-text application. The user presses a hotkey, speaks, and the transcribed text is injected into the active window.

**Core pipeline**: Audio capture (NAudio) -> Preprocessing (HPF/NoiseGate/AGC) -> Silence trimming (Silero VAD) -> Transcription (Whisper.net in-process) -> Text injection (InputSimulator)

**Backend**: Next.js 15 web API at voicelite.app handles license validation, Stripe checkout, and webhook processing.

---

## Project Structure

```
VoiceLite/
├── Core/Interfaces/Features/
│   └── IProFeatureService.cs           # Only surviving interface (DI seam for testing)
│
├── Services/
│   ├── AudioRecorder.cs                # NAudio 16kHz mono capture
│   ├── PersistentWhisperService.cs     # Whisper.net in-process transcription
│   ├── TextInjector.cs                 # H.InputSimulator keyboard/clipboard injection
│   ├── HotkeyManager.cs               # Win32 RegisterHotKey global hotkeys
│   ├── LicenseService.cs              # HTTP license validation + DPAPI storage
│   ├── ProFeatureService.cs           # Pro tier feature gating (implements IProFeatureService)
│   ├── ModelResolverService.cs        # Model path resolution + license check
│   ├── TranscriptionHistoryService.cs # History persistence + export
│   ├── CustomShortcutService.cs       # Regex text shortcuts (100ms timeout)
│   ├── SystemTrayManager.cs           # Tray icon and context menu
│   ├── TextPostProcessor.cs           # Punctuation/capitalization cleanup
│   ├── HardwareIdService.cs           # WMI machine ID for device activation
│   ├── ErrorLogger.cs                 # Static logging (Debug + Release levels)
│   ├── DisposableExtensions.cs        # Safe disposal helpers
│   └── Audio/
│       ├── AudioPreprocessor.cs       # Pipeline orchestrator (ISampleProvider chain)
│       ├── SileroVadService.cs        # Silero VAD v5 ONNX speech detection
│       ├── HighPassFilter.cs          # 80Hz rumble removal
│       ├── SimpleNoiseGate.cs         # Background noise reduction
│       └── AutomaticGainControl.cs    # Volume normalization
│
├── Presentation/
│   ├── ViewModels/                    # Lightweight data-binding only (no business logic)
│   │   ├── ViewModelBase.cs           # INotifyPropertyChanged base
│   │   ├── RecordingViewModel.cs      # Recording state + elapsed timer
│   │   ├── HistoryViewModel.cs        # History list display
│   │   └── StatusViewModel.cs         # Status bar display
│   └── Commands/
│       ├── RelayCommand.cs            # ICommand implementation
│       └── AsyncRelayCommand.cs       # Async ICommand implementation
│
├── Models/
│   ├── Settings.cs                    # App config + WhisperPresetConfig
│   ├── WhisperModelInfo.cs            # Model metadata (name, size, tier)
│   ├── TranscriptionHistoryItem.cs    # Single history entry
│   ├── CustomShortcut.cs              # Shortcut trigger/replacement pair
│   └── LanguageInfo.cs                # Language display name + code
│
├── Infrastructure/Resilience/
│   └── RetryPolicies.cs              # Polly retry/circuit breaker for HTTP
│
├── Helpers/
│   └── AsyncHelper.cs                # Safe async void wrappers
│
├── Utilities/
│   ├── HotkeyDisplayHelper.cs        # Key combo display formatting
│   ├── StatusColors.cs               # UI color constants
│   ├── TimingConstants.cs            # Shared timing values
│   ├── TextAnalyzer.cs               # Text statistics
│   ├── RelativeTimeConverter.cs      # "2 minutes ago" formatting
│   └── TruncateTextConverter.cs      # XAML text truncation converter
│
├── App.xaml.cs                       # Exception handlers + new MainWindow()
├── MainWindow.xaml.cs                # Entry point: instantiates all services, orchestrates everything
├── MainWindow.xaml                   # Main UI
├── SettingsWindowNew.xaml            # Settings UI
│
└── whisper/                          # Model files (not code)
    ├── ggml-base.bin                 # Swift model (142MB, bundled)
    └── silero_vad_v5.onnx            # VAD model (~2.3MB, bundled)
```

---

## Startup Flow

```
App.xaml.cs
  └─ OnStartup()
       ├─ Register global exception handlers
       └─ new MainWindow() → .Show()

MainWindow constructor
  ├─ LoadSettings()
  ├─ Create ViewModels (RecordingViewModel, HistoryViewModel, StatusViewModel)
  ├─ Set DataContext = this
  └─ Background init (Task.Run):
       ├─ new AudioRecorder()
       ├─ new PersistentWhisperService(settings, modelResolver, proFeatureService)
       │     └─ Background model warm-up: WhisperFactory.FromPath(modelPath)
       ├─ new HotkeyManager()
       ├─ new TextInjector()
       ├─ new ProFeatureService(settings)
       ├─ new SileroVadService(vadModelPath)
       ├─ new TranscriptionHistoryService(settings)
       ├─ new CustomShortcutService(settings)
       └─ new SystemTrayManager()
```

There is no DI container. MainWindow directly instantiates every service. `App.xaml.cs` just does `new MainWindow()` plus exception handling.

The ViewModels (RecordingViewModel, HistoryViewModel, StatusViewModel) are lightweight data-binding wrappers. They hold UI state (recording elapsed time, status text) and fire events back to MainWindow. MainWindow is the actual controller -- it wires events, orchestrates the recording/transcription flow, and calls services directly.

---

## Data Flow: Recording to Text

```
User presses Shift+Z (or configured hotkey)
  │
  ▼
HotkeyManager (Win32 RegisterHotKey)
  │ fires HotkeyPressed event
  ▼
MainWindow.OnHotkeyPressed()
  │
  ├─ START RECORDING:
  │   AudioRecorder.StartRecording()
  │   └─ NAudio WaveInEvent → 16kHz mono PCM → MemoryStream
  │      └─ Audio pipeline (real-time via AudioPreprocessor ISampleProvider chain):
  │           HighPassFilter (80Hz) → SimpleNoiseGate → AutomaticGainControl
  │
  ├─ ... user speaks ...
  │
  └─ STOP RECORDING (hotkey pressed again):
      AudioRecorder.StopRecording()
        │ returns WAV byte[]
        ▼
      SileroVadService.TrimSilence(wavBytes, threshold: 0.35)
        │ ONNX inference on 512-sample windows
        │ detects speech segments, strips leading/trailing silence
        ▼
      PersistentWhisperService.TranscribeFromStreamAsync(stream)
        │ EnsureFactoryLoaded(modelPath)  ← model stays in memory
        │ factory.CreateBuilder() → configure → .Build() → WhisperProcessor
        │ processor.ProcessAsync(wavStream) → segments
        ▼
      TextPostProcessor.Process(rawText)
        │ punctuation cleanup, capitalization
        ▼
      CustomShortcutService.ProcessShortcuts(text)
        │ regex-based trigger → expansion
        ▼
      TextInjector.InjectText(text, mode)
        │ SmartAuto: clipboard paste for >100 chars, typing for short text
        ▼
      Text appears in the previously-active window
```

---

## Core Components

### PersistentWhisperService

Whisper.net C# bindings via P/Invoke. Loads the GGML model once via `WhisperFactory.FromPath()` and keeps it resident in memory. Each transcription creates a `WhisperProcessor` from the factory, runs `ProcessAsync()`, then disposes the processor.

- Model switching: disposes old factory, creates new one (under `factoryLock`)
- Cancellation: `transcriptionCts` protected by `ctsLock`, `semaphoreAcquired` bool prevents `SemaphoreFullException`
- Initial prompt guides vocabulary (technical terms, proper nouns)
- Preset system: `WhisperPresetConfig` controls beam size, entropy threshold, temperature fallback

### SileroVadService

Silero VAD v5 ONNX model (~2.3MB). Runs as a preprocessing step BEFORE Whisper to trim silence and reduce hallucinations. Input: 512-sample windows + 64-sample context = 576 floats per inference. Output: speech probability 0-1 per window. Segments above threshold are kept; the rest is stripped.

- Lazy ONNX session loading (double-checked locking)
- Settings: `EnableVAD` (default true), `VADThreshold` (default 0.35)

### AudioPreprocessor

NAudio `ISampleProvider` chain. Wraps the raw microphone input in three processing stages applied during recording (real-time, not post-hoc):

1. **HighPassFilter**: Butterworth 80Hz cutoff, removes rumble/wind
2. **SimpleNoiseGate**: Threshold-based, reduces background hum during silence
3. **AutomaticGainControl**: Normalizes volume to target RMS

### LicenseService

Validates license keys via `POST https://voicelite.app/api/licenses/validate`. Uses static `HttpClient` (prevents socket exhaustion). Retry with Polly (3 attempts, exponential backoff). Caches result locally via DPAPI encryption at `%LOCALAPPDATA%\VoiceLite\license.dat` in `key|email` format for tamper detection.

### ModelResolverService

Resolves model file paths. Searches: `baseDir/whisper/`, `baseDir/`, `%LocalAppData%/VoiceLite/whisper/`. Validates Pro license via `IProFeatureService.CanUseModel()` before returning Pro-tier model paths. Throws `UnauthorizedAccessException` for free users requesting Pro models.

### TextInjector

Uses H.InputSimulator for keyboard simulation. Three modes:
- **SmartAuto**: Clipboard paste for text >100 chars, character typing for short text
- **Type**: Always types character-by-character
- **Paste**: Always uses clipboard (Ctrl+V)

Captures the foreground window handle at START of transcription (not at injection time).

---

## Thread Safety

| Component | Mechanism | Protects |
|-----------|-----------|----------|
| PersistentWhisperService | `factoryLock` (object lock) | Model loading/switching |
| PersistentWhisperService | `ctsLock` (object lock) | CancellationTokenSource access |
| PersistentWhisperService | `transcriptionSemaphore` (SemaphoreSlim) | Serializes transcriptions |
| SileroVadService | `sessionLock` (object lock) | ONNX session initialization |
| LicenseService | `_cacheLock` (object lock) | Thread-safe license cache |
| ProFeatureService | `ReaderWriterLockSlim` | Concurrent reads, exclusive refresh |
| TextInjector | `volatile bool _disposed` | Cross-thread disposal visibility |
| MainWindow | `recordingLock` (object lock) | Recording state transitions |
| MainWindow | `saveSettingsSemaphore` (SemaphoreSlim) | Settings file writes |

---

## Testing

**412 passing tests, 35 skipped** (hardware/UI dependent). Framework: xUnit + Moq.

Key test areas:
- **Disposal tests**: Every service with resources has disposal tests (memory leak prevention)
- **ModelResolverServiceTests**: Mocks `IProFeatureService` to test license gating
- **Audio pipeline tests**: Each filter tested independently with known signals
- **SileroVadService tests**: Speech detection on real audio patterns (note: pure sine waves are not detected as speech)
- **Stress tests**: Concurrent transcription, rapid start/stop, resource exhaustion

Run tests:
```bash
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj
```

---

## Performance

### Resource Usage

| State | RAM | CPU |
|-------|-----|-----|
| Idle (tray) | 60-90MB | 0-2% |
| Recording | 100-150MB | 2-5% |
| Transcribing (base) | 150-300MB | 15-40% |
| Transcribing (large-v3) | 3-4GB | 60-90% |

### Why Whisper.net In-Process

The previous architecture spawned a `whisper.exe` subprocess for each transcription. This required: writing temp WAV files, parsing stdout, managing zombie processes, and reloading the model every time.

Whisper.net loads the model once via `WhisperFactory.FromPath()`. Subsequent transcriptions reuse the loaded model. Result: 2-10x faster (no model reload), no temp files, no subprocess management, no stdout parsing.

### Model Sizes

| Display Name | File | Size | Speed | Tier |
|-------------|------|------|-------|------|
| Swift | ggml-base.bin | 142MB | Fast | Free |
| Pro | ggml-small.bin | 466MB | Medium | Pro |
| Elite | ggml-medium.bin | 1.5GB | Slow | Pro |
| Turbo | ggml-large-v3-turbo-q8_0.bin | 874MB | Medium | Pro |
| Ultra | ggml-large-v3.bin | 2.9GB | Slowest | Pro |

Q8_0 quantization: 45% smaller, 30-40% faster, 99.98% identical accuracy vs F16.

---

## Debugging

### Log Locations

- **Application log**: `%LOCALAPPDATA%\VoiceLite\logs\voicelite.log`
- **Error log**: `%LOCALAPPDATA%\VoiceLite\voicelite_error.log`
- **Settings**: `%LOCALAPPDATA%\VoiceLite\settings.json`
- **License cache**: `%LOCALAPPDATA%\VoiceLite\license.dat` (DPAPI encrypted)

### Logging Levels

```csharp
ErrorLogger.LogMessage("info");      // Debug builds only
ErrorLogger.LogWarning("visible");   // Shows in Release builds
ErrorLogger.LogError("context", ex); // Always shows, writes to error log
```

### Common Issues

**Model not loading**: Check `%LOCALAPPDATA%\VoiceLite\whisper\` and `baseDir\whisper\` for model files. `ModelResolverService` searches both locations.

**Text injected into wrong window**: `TextInjector` captures the foreground window at START of transcription. Long transcriptions may finish after the user switches windows.

**License validation fails**: Check network connectivity to `voicelite.app`. Polly retries 3 times with exponential backoff. Cached license in `license.dat` allows offline use after first successful validation.

**VAD trimming too aggressively**: Lower `VADThreshold` in settings (default 0.35). Lower values = more permissive speech detection.

**Whisper hallucinations on silence**: This is why Silero VAD was added. If VAD is disabled (`EnableVAD = false`), Whisper may hallucinate text from background noise or silence.

---

## Security

- **HTTPS enforced** for all API calls to voicelite.app
- **DPAPI encryption** for license storage (tied to Windows user account, not portable)
- **License tamper detection**: `key|email` format in storage, verified on load via `VerifyLicenseKeyMatchesStorage()`
- **Model access control**: `ModelResolverService` checks `IProFeatureService.CanUseModel()` before resolving Pro model paths
- **3-device activation limit**: Enforced server-side via `LicenseActivation` table
- **Password field detection**: `TextInjector` detects password inputs and avoids logging
- **Regex timeout**: `CustomShortcutService` uses 100ms regex timeout to prevent catastrophic backtracking
- **No secrets in code**: License key stored via DPAPI, API calls use HTTPS
- **Hardware ID fallback**: `HardwareIdService` gracefully handles WMI failures (VMs, headless systems) with persistent GUID fallback

---

## Web Backend

**Stack**: Next.js 15.5, React 19, Prisma 6, Supabase PostgreSQL, Stripe

**Key endpoints**:
- `POST /api/licenses/validate` -- License validation (rate limited: 5/hour/IP)
- `POST /api/checkout` -- Stripe checkout session creation
- `POST /api/webhook` -- Stripe webhook handler (idempotent via WebhookEvent table)

**Database**: License, LicenseActivation (3-device limit), LicenseEvent (audit trail), WebhookEvent (idempotency)

**Location**: `voicelite-web/` directory, separate from desktop app.

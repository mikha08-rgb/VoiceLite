# VoiceLite - Technical Architecture

**Version**: 1.0
**Date**: 2025-01-10
**Status**: Living Document

---

## System Overview

VoiceLite is a Windows-native desktop application that provides real-time speech-to-text transcription using OpenAI's Whisper AI model. The architecture follows a service-oriented design with clear separation of concerns.

**Core Flow**: `Recording → Whisper Transcription → Text Injection`

---

## Architecture Principles

### Design Philosophy (v1.0.65+)
- **Simplicity First**: Core-only features, no unnecessary complexity
- **Reliability Over Features**: Removed 15,000+ lines of experimental code
- **Privacy by Design**: Zero analytics, zero tracking, 100% local processing
- **Fail-Fast**: Clear errors over silent degradation

### Key Constraints
- Windows 10/11 only (no cross-platform)
- .NET 8.0 Windows (WPF framework)
- Local-only processing (no required cloud dependencies)
- Single-user desktop application

---

## System Architecture

### High-Level Component Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                        MainWindow (WPF)                      │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │ Recording UI │  │ History Panel│  │ Settings UI  │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                              │
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                      Service Layer                           │
│  ┌─────────────────┐  ┌──────────────────┐  ┌────────────┐ │
│  │ AudioRecorder   │→ │ PersistentWhisper│→ │TextInjector│ │
│  │   (NAudio)      │  │    Service       │  │(InputSim)  │ │
│  └─────────────────┘  └──────────────────┘  └────────────┘ │
│                                                              │
│  ┌─────────────────┐  ┌──────────────────┐  ┌────────────┐ │
│  │ HotkeyManager   │  │HistoryService    │  │ErrorLogger │ │
│  └─────────────────┘  └──────────────────┘  └────────────┘ │
└─────────────────────────────────────────────────────────────┘
                              │
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                    External Dependencies                     │
│  ┌─────────────────┐  ┌──────────────────┐                 │
│  │  Whisper.exe    │  │ Windows Clipboard│                 │
│  │  (subprocess)   │  │   + Win32 API    │                 │
│  └─────────────────┘  └──────────────────┘                 │
└─────────────────────────────────────────────────────────────┘
```

---

## Core Components

### 1. MainWindow (Entry Point)
**File**: `VoiceLite/MainWindow.xaml.cs` (2183 lines)

**Responsibilities**:
- Application lifecycle management
- Recording state coordination
- UI event handling
- Service initialization and disposal
- Settings persistence
- Transcription history management

**Key Patterns**:
- WPF MVVM-lite (no formal MVVM framework)
- Dispatcher for thread-safe UI updates
- Lock-based state management for recording
- Debounced settings save (500ms timer)

**Critical Methods**:
- `InitializeAsync()` - Service setup + diagnostics
- `OnHotkeyPressed()` - Recording state machine
- `OnTranscriptionCompleted()` - Result handling
- `OnClosed()` - Resource cleanup

---

### 2. Service Layer (13 Active Services)

#### Core Services

**AudioRecorder** (`Services/AudioRecorder.cs`)
- **Tech**: NAudio 2.2.1 (WasapiCapture)
- **Format**: 16kHz, 16-bit mono WAV (Whisper requirement)
- **Features**: Device switching, noise suppression (disabled by default)
- **Thread Safety**: Lock-based (recording state)
- **Disposal**: Stops capture, disposes WaveFileWriter

**PersistentWhisperService** (`Services/PersistentWhisperService.cs`)
- **Tech**: whisper.exe subprocess (greedy decoding)
- **Concurrency**: SemaphoreSlim (1 transcription at a time)
- **Performance**: beam_size=1, best_of=1 (5x faster than beam search)
- **Timeout**: Configurable multiplier (default 2.0x)
- **Disposal**: Process cleanup (5s observed delay - known issue)

**TextInjector** (`Services/TextInjector.cs`)
- **Tech**: H.InputSimulator 1.2.1
- **Modes**: SmartAuto, AlwaysType, AlwaysPaste, PreferType, PreferPaste
- **Smart Logic**: Clipboard for >50 chars, typing for short text
- **Clipboard Safety**: CRC32 verification, restore original
- **Thread Safety**: Dispatcher marshaling for UI thread

#### Supporting Services

**HotkeyManager** (`Services/HotkeyManager.cs`)
- Win32 API (RegisterHotKey, UnregisterHotKey)
- Fallback polling mode if registration fails
- Event-based notification to MainWindow

**TranscriptionHistoryService** (`Services/TranscriptionHistoryService.cs`)
- In-memory history with pinning support
- Auto-cleanup (>7 days, not pinned)
- Max items: 250 (down from 1000 in v1.0.62)

**SystemTrayManager** (`Services/SystemTrayManager.cs`)
- Hardcodet.NotifyIcon.Wpf 2.0.1
- Balloon tips, context menu
- Minimize to tray

**SoundService** (`Services/SoundService.cs`)
- NAudio.Vorbis 1.5.0
- Custom sound effects (wood-tap-click.ogg)
- Disabled by default

**ErrorLogger** (`Services/ErrorLogger.cs`)
- File-based logging: `%LOCALAPPDATA%\VoiceLite\logs\voicelite.log`
- Log rotation at 10MB
- Levels: Error, Warning, Info, Debug
- Thread-safe (lock-based)

**MemoryMonitor** (`Services/MemoryMonitor.cs`)
- Periodic memory usage tracking
- Leak detection support

**ZombieProcessCleanupService** (`Services/ZombieProcessCleanupService.cs`)
- Kills orphaned whisper.exe processes
- Runs every 60 seconds
- Safety: Only kills processes without parent

**StartupDiagnostics** (`Services/StartupDiagnostics.cs`)
- Pre-flight checks (whisper.exe, models, permissions)
- Auto-repair common issues
- User-friendly error reporting

**DependencyChecker** (`Services/DependencyChecker.cs`)
- VC++ Runtime verification
- Model file validation
- Executable integrity checks

**AudioPreprocessor** (`Services/AudioPreprocessor.cs`)
- Audio enhancement (disabled by default for reliability)
- Noise gate, normalization
- VAD (Voice Activity Detection) silence trimming

---

### 3. Models & Data Structures

**Settings** (`Models/Settings.cs`)
- JSON serialization (System.Text.Json)
- Location: `%LOCALAPPDATA%\VoiceLite\settings.json`
- Validation + auto-repair
- Debounced save (500ms)

**WhisperModelInfo** (`Models/WhisperModelInfo.cs`)
- Model metadata (size, accuracy, speed)
- 4 tiers: Lite (75MB), Pro (466MB), Elite (1.5GB), Ultra (2.9GB)

**TranscriptionResult** (`Models/TranscriptionResult.cs`)
- Whisper output parsing
- Text + metadata (duration, confidence)

**TranscriptionHistoryItem** (`Models/TranscriptionHistoryItem.cs`)
- UI data model for history panel
- Pinning, timestamps, relative time

---

## Technology Stack

### Core Framework
- **.NET 8.0 Windows** (net8.0-windows)
- **WPF** (Windows Presentation Foundation)
- **C# 12**

### Dependencies (NuGet)
```xml
<PackageReference Include="NAudio" Version="2.2.1" />
<PackageReference Include="NAudio.Vorbis" Version="1.5.0" />
<PackageReference Include="H.InputSimulator" Version="1.2.1" />
<PackageReference Include="Hardcodet.NotifyIcon.Wpf" Version="2.0.1" />
<PackageReference Include="System.Text.Json" Version="9.0.9" />
<PackageReference Include="System.Management" Version="8.0.0" />
```

### External Dependencies
- **Whisper.cpp** (whisper.exe) - Subprocess for transcription
- **VC++ Runtime 2015-2022 x64** - Required for whisper.exe
- **Windows APIs**: Hotkey registration, clipboard, Win32

---

## Data Flow

### Recording → Transcription → Injection

```
1. USER PRESSES HOTKEY
   ↓
2. HotkeyManager → Raises HotkeyPressed event
   ↓
3. MainWindow.OnHotkeyPressed()
   ├─ Check recording state (lock)
   ├─ Toggle: Start or Stop recording
   └─ Update UI (status text, colors)
   ↓
4a. START RECORDING
    ├─ AudioRecorder.StartRecording()
    ├─ Create temp WAV file
    └─ Capture audio from microphone
    ↓
4b. STOP RECORDING
    ├─ AudioRecorder.StopRecording()
    ├─ Save WAV file
    └─ Return audio path
    ↓
5. TRANSCRIPTION (async)
   ├─ PersistentWhisperService.TranscribeAsync(audioPath)
   ├─ Spawn whisper.exe subprocess
   ├─ Parse stdout for transcription text
   └─ Return TranscriptionResult
   ↓
6. TEXT INJECTION
   ├─ TextInjector.InjectText(text)
   ├─ Determine mode (paste vs type)
   ├─ Save/restore clipboard if pasting
   └─ Simulate keyboard input
   ↓
7. HISTORY UPDATE
   ├─ TranscriptionHistoryService.Add(item)
   ├─ MainWindow updates UI (Dispatcher)
   └─ Settings.Save() (debounced)
```

---

## Thread Safety

### Critical Sections
- **Recording State**: Protected by `lock (_recordingLock)` in MainWindow
- **Settings Save**: Debounced timer + lock in `SaveSettingsAsync()`
- **Transcription Queue**: SemaphoreSlim in PersistentWhisperService (max 1 concurrent)
- **Error Logging**: Lock-based in ErrorLogger

### UI Thread Marshaling
- All UI updates via `Dispatcher.Invoke()` or `Dispatcher.BeginInvoke()`
- Async operations use `ConfigureAwait(false)` for non-UI work

---

## Performance Characteristics

### Targets (from CLAUDE.md)
- Transcription latency: **<200ms** after speech stops
- Idle RAM: **<100MB**
- Active RAM: **<300MB**
- Idle CPU: **<5%**
- Startup time: **<3 seconds**

### Actual Performance (v1.0.66)
- Greedy decoding (beam_size=1): **5x faster** than v1.0.24 beam search
- Raw audio (no preprocessing): **Simpler, more reliable**
- Smart text injection: **Clipboard for long text, typing for short**

### Known Performance Issues
- PersistentWhisperService disposal: **5 seconds** (slow process termination)
- First transcription: **2-3s warmup** (model loading)
- Large audio files: **Timeout multiplier** prevents hangs

---

## Error Handling Strategy

### Philosophy
- **Fail-Fast**: Clear errors over silent degradation
- **User-Friendly**: No stack traces to users (log only)
- **Recovery**: Automatic repair where possible
- **Fallback**: Degrade gracefully (e.g., polling mode if hotkey fails)

### Error Categories
1. **Startup Errors**: Missing files, permissions → Diagnostics auto-fix
2. **Recording Errors**: Mic unavailable → Device selection UI
3. **Transcription Errors**: Process crash → Timeout handling + cleanup
4. **Injection Errors**: Clipboard failures → Retry with typing mode
5. **Persistence Errors**: Settings corruption → Auto-repair or reset

---

## Security Considerations

### Privacy
- **Zero Analytics**: No telemetry, no tracking (removed in v1.0.65)
- **Local Processing**: All transcription happens on-device
- **No PII**: Settings and history never leave the machine
- **Temp File Cleanup**: Audio files deleted immediately after transcription

### Process Isolation
- Whisper runs as separate process (sandboxed)
- ZombieProcessCleanupService prevents orphaned processes

### No Secrets in Code
- No API keys, no hardcoded credentials
- Web backend optional (feedback only, not required)

---

## Testing Strategy

### Current State (v1.0.66)
- **217 tests** exist (xUnit framework)
- **Coverage**: Unknown % (need to measure)
- **Test Libraries**: xUnit, Moq, FluentAssertions

### Test Categories
1. **Unit Tests**: Service logic, models, utilities
2. **Integration Tests**: Service interactions (AudioRecorder → Whisper → TextInjector)
3. **UI Tests**: Limited (WPF testing challenges)
4. **Performance Tests**: Memory leak detection, disposal timing

### Known Test Issues
- PersistentWhisperService disposal timeout (5s vs 500ms expected)
- Some tests skipped (WPF UI testing limitations)
- Flaky tests (async timing issues)

---

## Build & Deployment

### Build Process
```bash
dotnet build VoiceLite/VoiceLite.sln -c Release
dotnet publish VoiceLite/VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained
```

### Installer (Inno Setup)
- Bundles: App + Whisper models (Lite 75MB + Pro 466MB)
- VC++ Runtime bundled (14MB)
- Total size: ~557MB
- Auto-creates AppData directories
- Desktop shortcut + Start Menu entry

### CI/CD (GitHub Actions)
- Automated testing on PRs
- Automated release builds on git tags
- Installer compilation via Inno Setup

---

## File System Layout

```
%LOCALAPPDATA%\VoiceLite\
├── settings.json              # User settings (Local only, no sync)
├── logs\
│   └── voicelite.log         # Error logs (10MB rotation)
└── temp\
    └── *.wav                  # Temp audio files (auto-cleanup)

%PROGRAMFILES%\VoiceLite\
├── VoiceLite.exe
├── whisper\
│   ├── whisper.exe
│   ├── ggml-tiny.bin         # Lite model (75MB)
│   ├── ggml-small.bin        # Pro model (466MB)
│   ├── ggml-base.bin         # Swift model (optional)
│   ├── ggml-medium.bin       # Elite model (optional)
│   └── ggml-large-v3.bin     # Ultra model (manual download)
└── [DLLs and dependencies]
```

---

## Known Technical Debt

### From "Vibe Coding"
- **BUG-XXX FIX comments**: Throughout codebase (should be proper docs)
- **Inconsistent patterns**: Error handling varies across services
- **Large methods**: MainWindow has some >100 line methods
- **Magic numbers**: Timeouts, thresholds hardcoded
- **Test coverage gaps**: Unknown % coverage

### Deferred to Post-v1.0
- Refactor MainWindow (2183 lines → split into ViewModels)
- Extract constants to configuration
- Consistent async/await patterns
- Formal MVVM architecture

---

## Future Architecture Considerations (Out of Scope for v1.0)

### Potential Improvements
- Multi-language UI support
- Plugin architecture for text formatters
- Cloud backup for settings/history
- Auto-update mechanism
- Telemetry (opt-in, privacy-respecting)

### Not Planned
- Cross-platform (macOS, Linux)
- Mobile apps
- Web version
- Real-time streaming transcription

---

## References

- [CLAUDE.md](../CLAUDE.md) - Development guide
- [PRD](prd.md) - Product requirements
- [NAudio Documentation](https://github.com/naudio/NAudio)
- [Whisper.cpp](https://github.com/ggerganov/whisper.cpp)

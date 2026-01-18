# VoiceLite

Windows speech-to-text app. Desktop (.NET 8 WPF) + Web backend (Next.js 15). Recording → Whisper.cpp → text injection.

## Quick Start

```bash
# Build & run desktop
dotnet build VoiceLite/VoiceLite.sln
dotnet run --project VoiceLite/VoiceLite/VoiceLite.csproj

# Release build + installer
dotnet publish VoiceLite/VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" VoiceLite/Installer/VoiceLiteSetup.iss

# Tests (~412 tests, must all pass before commit)
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj

# Web backend
cd voicelite-web && npm run dev
```

## Architecture Decisions

**Why subprocess for Whisper**: whisper.cpp as Process, not library binding. Easier to kill zombie processes, debug crashes, update whisper version independently.

**Why no DI container**: Manual service wiring in MainWindow. Small app, MVVM extraction in progress. ViewModels own events, MainWindow wires handlers.

**Why static HttpClient in LicenseService**: Single API endpoint (voicelite.app). Prevents socket exhaustion. Intentionally NOT disposed.

**Why DPAPI for license storage**: Windows-native encryption, tied to user account. `%LOCALAPPDATA%\VoiceLite\license.dat`. Auto-migrates from plaintext settings.json.

**Why Q8_0 quantization**: 45% smaller models, 30-40% faster inference, 99.98% identical accuracy. large-v3 still F16 (no upstream Q8).

## Critical Paths

**Recording flow**: `MainWindow` → `AudioRecorder.StartRecording()` → 16kHz mono WAV → `PersistentWhisperService.TranscribeAsync()` → `TextInjector.InjectText()`

**License validation**: Desktop calls `/api/licenses/validate` → Prisma lookup → device activation (3-device limit) → DPAPI-cached locally

## Gotchas & Past Failures

- **Model files in .gitignore**: v1.0.96 broke because `ggml-tiny.bin` was gitignored. Use `git add -f` for model files.
- **Release logging disabled**: v1.0.94 had silent failures because `#if DEBUG` wrapped logging. Use `ErrorLogger.LogWarning()` for release-visible logs.
- **Process disposal timeout**: Whisper processes can zombie. `PersistentWhisperService` has 2-second disposal timeout + taskkill fallback.
- **Semaphore tracking**: `TranscribeAsync()` tracks `semaphoreAcquired` bool to prevent `SemaphoreFullException` on cancellation.
- **TextInjector window capture**: Captures foreground window at start of transcription, not at injection time. Long transcriptions may redirect to wrong window.

## Patterns to Follow

**Error logging**:
```csharp
ErrorLogger.LogMessage("info");      // Debug only
ErrorLogger.LogWarning("visible");   // Shows in Release builds
ErrorLogger.LogError("context", ex); // Always shows
```

**Process cleanup**: Always `process.Kill(entireProcessTree: true)` for whisper.exe. Child processes can orphan.

**Disposal**: Every service implements `IDisposable`. Tests validate disposal (`*DisposalTests.cs`).

**Settings path**: `%LOCALAPPDATA%\VoiceLite\` - NOT Roaming (privacy fix, no cloud sync).

**ViewModel events**: ViewModels expose `*Requested` events. MainWindow subscribes, calls services:
```csharp
recordingViewModel.RecordingStartRequested += OnRecordingStartRequested;
```

## File Locations

| Path | Purpose |
|------|---------|
| `VoiceLite/VoiceLite/Services/` | Core services (AudioRecorder, PersistentWhisperService, TextInjector, etc.) |
| `VoiceLite/VoiceLite/Core/Interfaces/` | Service interfaces (IWhisperService, IAudioRecorder, etc.) |
| `VoiceLite/VoiceLite/Presentation/ViewModels/` | MVVM ViewModels (in-progress extraction) |
| `VoiceLite/VoiceLite/Models/` | Settings, WhisperModelInfo, TranscriptionPreset |
| `VoiceLite/whisper/` | whisper.exe + models (ggml-base.bin bundled) |
| `voicelite-web/app/api/` | Next.js API routes (licenses, checkout, feedback) |
| `voicelite-web/prisma/schema.prisma` | Database schema |

## Web Backend

**Stack**: Next.js 15.5, React 19, Prisma 6, Supabase PostgreSQL, Stripe

**Key endpoints**:
- `POST /api/licenses/validate` - License validation (rate limited: 5/hour/IP)
- `POST /api/checkout` - Stripe checkout session
- `POST /api/webhook` - Stripe webhook handler

**Database models**: License, LicenseActivation (3-device limit), LicenseEvent (audit), WebhookEvent (idempotency)

## Release Process

```bash
# Auto-releases via GitHub Actions on tag push
git tag v1.2.0.X
git push --tags
# Workflow: validate versions → build → create installer → GitHub release
```

**Version sync required**: `VoiceLite.csproj`, `VoiceLiteSetup.iss` must match tag. Workflow validates.

## Pro Feature Gating

```csharp
// ProFeatureService.cs controls visibility
public Visibility AiModelsTabVisibility => IsProUser ? Visibility.Visible : Visibility.Collapsed;

// In XAML: bind tab visibility to service property
```

Free tier: Base model only. Pro ($20): All 5 models + AI Models settings tab.

## Testing

- Coverage target: ≥75% overall, ≥80% Services/
- Disposal tests are critical (memory leak prevention)
- Run `dotnet test` before every commit

## Version

Check `git tag` for current version. Desktop version in `VoiceLite/VoiceLite/VoiceLite.csproj`.

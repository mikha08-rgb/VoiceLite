# VoiceLite

Windows speech-to-text app. Desktop (.NET 8 WPF) + Web backend (Next.js 15). Recording → Whisper.net (in-process) → text injection.

## Quick Start

```bash
# Build & run desktop
dotnet build VoiceLite/VoiceLite.sln
dotnet run --project VoiceLite/VoiceLite/VoiceLite.csproj

# Release build + installer
dotnet publish VoiceLite/VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" VoiceLite/Installer/VoiceLiteSetup.iss

# Tests (must all pass before commit)
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj

# Web backend
cd voicelite-web && npm run dev
```

## Architecture Decisions

**Why Whisper.net in-process**: Whisper.net (C# bindings via P/Invoke) loads the model once into memory via `WhisperFactory.FromPath()`. Subsequent transcriptions reuse the loaded model (2-10x faster). No subprocess management, no zombie processes, no temp WAV files, no stdout parsing. CPU-only runtime (`Whisper.net.Runtime` NuGet). CUDA can be added later as optional download.

**Why no DI container**: DI infrastructure was removed — MainWindow directly instantiates all services. App.xaml.cs just does `new MainWindow()`. This matches what actually ran at runtime (MainWindow always bypassed the DI layer).

**Why static HttpClient in LicenseService**: Single API endpoint (voicelite.app). Prevents socket exhaustion. Intentionally NOT disposed.

**Why DPAPI for license storage**: Windows-native encryption, tied to user account. `%LOCALAPPDATA%\VoiceLite\license.dat`. Auto-migrates from plaintext settings.json. Stores `key|email` format for tamper detection via `VerifyLicenseKeyMatchesStorage()`.

**Why Q8_0 quantization**: 45% smaller models, 30-40% faster inference, 99.98% identical accuracy. large-v3 still F16 (no upstream Q8). large-v3-turbo uses Q8_0 (874MB vs 2.9GB for full large-v3).

## Critical Paths

**Recording flow**: `MainWindow` → `AudioRecorder.StartRecording()` → 16kHz mono WAV → `PersistentWhisperService.TranscribeFromStreamAsync()` → Whisper.net `ProcessAsync()` → `TextPostProcessor` → `TextInjector.InjectText()`

**License validation**: Desktop calls `/api/licenses/validate` → Prisma lookup → device activation (3-device limit) → DPAPI-cached locally as `key|email` format

**Model resolution**: `PersistentWhisperService` → `ModelResolverService.ResolveModelPath()` → Pro license check → path returned

## Gotchas & Past Failures

- **Model files in .gitignore**: v1.0.96 broke because `ggml-tiny.bin` was gitignored. Use `git add -f` for model files.
- **Release logging disabled**: v1.0.94 had silent failures because `#if DEBUG` wrapped logging. Use `ErrorLogger.LogWarning()` for release-visible logs.
- **Semaphore tracking**: `TranscribeAsync()` tracks `semaphoreAcquired` bool to prevent `SemaphoreFullException` on cancellation.
- **TextInjector window capture**: Captures foreground window at start of transcription, not at injection time. Long transcriptions may redirect to wrong window.
- **Regex timeout in shortcuts**: `CustomShortcutService` uses 100ms timeout to prevent catastrophic backtracking on malicious patterns.
- **Hardware ID fallback**: `HardwareIdService` gracefully falls back if WMI fails (VM/headless systems) to persistent GUID.
- **License tamper detection**: `VerifyLicenseKeyMatchesStorage()` validates both key AND email from `key|email` storage format.
- **Interface removal gotcha**: Deleted interfaces (`IAudioRecorder`, `IWhisperService`, etc.) extended `IDisposable`. Removing them silently drops `IDisposable` from implementing classes — must add `: IDisposable` back explicitly.
- **Helper types in interface files**: `InjectionMode` lives in `TextInjector.cs`, `HotkeyEventArgs` in `HotkeyManager.cs`, `TranscriptionItem`/`ExportFormat` in `TranscriptionHistoryService.cs`, `LicenseValidationResult` in `LicenseService.cs`.
- **VAD temporary regression**: Silero VAD parameters aren't exposed in Whisper.net builder. Users may notice more silence-related hallucinations. Follow-up: add VAD as separate audio preprocessing step using Silero ONNX model.
- **WhisperFactory thread safety**: `EnsureFactoryLoaded()` is protected by `factoryLock`. The factory is disposed and recreated when switching models. Don't call `WhisperFactory.Dispose()` while a `WhisperProcessor` is active.

## Patterns to Follow

**Error logging**:
```csharp
ErrorLogger.LogMessage("info");      // Debug only
ErrorLogger.LogWarning("visible");   // Shows in Release builds
ErrorLogger.LogError("context", ex); // Always shows
```

**Disposal**: Services with resources implement `IDisposable` directly (no interfaces). `PersistentWhisperService.Dispose()` disposes the `WhisperFactory` (releases model memory), cancellation tokens, and semaphore. Tests validate disposal (`*DisposalTests.cs`).

**Settings path**: `%LOCALAPPDATA%\VoiceLite\` - NOT Roaming (privacy fix, no cloud sync).

**Only surviving interface**: `IProFeatureService` in `Core/Interfaces/Features/` — used as a DI seam in `ModelResolverService` and `PersistentWhisperService` constructors, mocked in `ModelResolverServiceTests`.

## File Locations

| Path | Purpose |
|------|---------|
| `VoiceLite/VoiceLite/Services/` | Core services (AudioRecorder, PersistentWhisperService, TextInjector, etc.) |
| `VoiceLite/VoiceLite/Services/Audio/` | Audio preprocessing pipeline (HighPassFilter, NoiseGate, AGC) |
| `VoiceLite/VoiceLite/Core/Interfaces/Features/` | IProFeatureService (only surviving interface) |
| `VoiceLite/VoiceLite/Infrastructure/Resilience/` | RetryPolicies (Polly) |
| `VoiceLite/VoiceLite/Presentation/Commands/` | RelayCommand, AsyncRelayCommand implementations |
| `VoiceLite/VoiceLite/Models/` | Settings, WhisperModelInfo, TranscriptionPreset |
| `VoiceLite/whisper/` | Model files only (ggml-base.bin bundled). Whisper.net DLLs from NuGet. |
| `voicelite-web/app/api/` | Next.js API routes (licenses, checkout, feedback) |
| `voicelite-web/prisma/schema.prisma` | Database schema |

## Available Models

| Model | File | Size | Speed | Accuracy | Tier |
|-------|------|------|-------|----------|------|
| Swift | ggml-base.bin | 142MB | 4/5 | 2/5 | Free |
| Pro | ggml-small.bin | 466MB | 3/5 | 3/5 | Pro |
| Elite | ggml-medium.bin | 1.5GB | 2/5 | 4/5 | Pro |
| Turbo | ggml-large-v3-turbo-q8_0.bin | 874MB | 3/5 | 5/5 | Pro |
| Ultra | ggml-large-v3.bin | 2.9GB | 1/5 | 5/5 | Pro |

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

Free tier: Base model only. Pro ($20): All 6 models + AI Models settings tab.

## Testing

- Disposal tests are critical (memory leak prevention)
- Run `dotnet test` before every commit

## Version

Check `git tag` for current version. Desktop version in `VoiceLite/VoiceLite/VoiceLite.csproj`.

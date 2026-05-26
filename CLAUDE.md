# VoiceLite

Windows speech-to-text app. Desktop (.NET 8 WPF) + Web backend (Next.js 15). Recording → Sherpa-ONNX + Parakeet v3 (in-process) → text injection.

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

**Why Sherpa-ONNX + Parakeet v3 in-process**: Sherpa-ONNX (`org.k2fsa.sherpa.onnx` NuGet) ships C# bindings to a k2-fsa native runtime that loads ONNX speech models in-process via P/Invoke. `OfflineRecognizer` holds the loaded model; subsequent transcriptions reuse it. The model is **Parakeet TDT 0.6B v3** (NVIDIA, CC-BY-4.0) — a transducer architecture that beats Whisper Large v3 on the HF Open ASR Leaderboard at ~2-3× the CPU speed and physically cannot hallucinate "Thanks for watching!" on silence (transducer outputs are aligned to audio frames). No subprocess, no temp WAVs, no stdout parsing. ONNX Runtime is also already a dep (Silero VAD), so no net-new native runtime.

**Why no DI container**: DI infrastructure was removed — MainWindow directly instantiates all services. App.xaml.cs just does `new MainWindow()`. This matches what actually ran at runtime (MainWindow always bypassed the DI layer).

**Why static HttpClient in LicenseService**: Single API endpoint (voicelite.app). Prevents socket exhaustion. Intentionally NOT disposed.

**Why DPAPI for license storage**: Windows-native encryption, tied to user account. `%LOCALAPPDATA%\VoiceLite\license.dat`. Auto-migrates from plaintext `settings.json` (cleared on first launch after upgrade). Stores `key|email` format — DPAPI presence is the authoritative source of truth for Pro tier; `settings.IsProLicense` is just a cache flag and is reset to Free at startup if `license.dat` is missing.

**Why post-install model download (not bundled)**: Parakeet v3 int8 is ~640MB. Bundling it would push the installer past 800MB. Instead the installer stays at ~150MB and the first-launch UI (`Controls/ModelDownloadControl`) streams `sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8.tar.bz2` from the k2-fsa GitHub Releases mirror, extracts via SharpCompress, and places the four required files (`encoder.int8.onnx`, `decoder.int8.onnx`, `joiner.int8.onnx`, `tokens.txt`) in `%LocalAppData%\VoiceLite\models\parakeet-v3\`. `App.xaml.cs` blocks `MainWindow` creation until the model is present or the user exits.

## Critical Paths

**Recording flow**: `MainWindow` → `AudioRecorder.StartRecording()` → 16kHz mono WAV → `PersistentWhisperService.TranscribeFromStreamAsync()` → WAV decoded to `float[]` via `WaveFileReader.ToSampleProvider()` → `OfflineRecognizer.Decode()` (blocking, wrapped in `Task.Run`) → `TextPostProcessor` → `TextInjector.InjectText()`

**License validation**: Desktop calls `/api/licenses/validate` → Prisma lookup → device activation (3-device limit) → DPAPI-cached locally as `key|email` format

**Model resolution**: `PersistentWhisperService` → `ModelResolverService.ResolveModelPath()` → probes `models/parakeet-v3`, `whisper/parakeet-v3`, `%LocalAppData%/VoiceLite/models/parakeet-v3` → returns directory containing the four ONNX files. Pro gating is a no-op post-v2.0.

**Startup gate**: `App.xaml.cs` runs three pre-MainWindow checks: (1) `NativeLibrary.Load("sherpa-onnx-c-api")` — fails fast with a "Missing VC++ Runtime" dialog if Sherpa native deps can't load; (2) model-installed probe — opens `ModelDownloadControl` if the four Parakeet files aren't present; (3) only then `new MainWindow()`.

## Gotchas & Past Failures

- **Release logging disabled**: v1.0.94 had silent failures because `#if DEBUG` wrapped logging. Use `ErrorLogger.LogWarning()` for release-visible logs.
- **Semaphore tracking**: `TranscribeAsync()` tracks `semaphoreAcquired` bool to prevent `SemaphoreFullException` on cancellation.
- **TextInjector window capture**: Captures foreground window at start of transcription, not at injection time. Long transcriptions may redirect to wrong window.
- **Regex timeout in shortcuts**: `CustomShortcutService` uses 100ms timeout to prevent catastrophic backtracking on malicious patterns.
- **Hardware ID fallback**: `HardwareIdService` gracefully falls back if WMI fails (VM/headless systems) to persistent GUID.
- **License tamper detection**: At startup, if `settings.IsProLicense=true` but `license.dat` is missing, the user is reset to Free (covers manual `settings.json` edits). Stale plaintext `LicenseKey` from older builds is cleared on next launch.
- **Interface removal gotcha**: Deleted interfaces (`IAudioRecorder`, `IWhisperService`, etc.) extended `IDisposable`. Removing them silently drops `IDisposable` from implementing classes — must add `: IDisposable` back explicitly.
- **Helper types in interface files**: `InjectionMode` lives in `TextInjector.cs`, `HotkeyEventArgs` in `HotkeyManager.cs`, `TranscriptionItem`/`ExportFormat` in `TranscriptionHistoryService.cs`, `LicenseValidationResult` in `LicenseService.cs`.
- **Parakeet has no prompt-bias**: Transducer models can't accept initial prompts. The dev-term substitutions (`github`→`GitHub`, `typescript`→`TypeScript`, etc.) are applied post-hoc in `TextPostProcessor._devTermDictionary`. Add new terms there, not via prompt.
- **OfflineRecognizer thread safety**: `EnsureRecognizerLoaded()` is protected by `factoryLock` (legacy name kept). The recognizer is disposed and recreated when settings change. Don't dispose while a `Decode()` call is in flight — the wrapping `Task.Run` doesn't make the native call cancellable.
- **`OfflineRecognizer.Decode()` is blocking**: It's native C++ with no async API. Always wrap in `await Task.Run(...)` from any UI thread call site, or the WPF dispatcher freezes for the duration of the transcription.
- **VC++ runtime probe**: `App.xaml.cs` calls `NativeLibrary.Load("sherpa-onnx-c-api")` at startup. The installer bundles + runs `vc_redist.x64.exe` silently, but AV can block it. Without the probe, a missing VC++ runtime surfaces as a cryptic `DllNotFoundException` on first transcription.
- **First-launch model gate**: `App.xaml.cs.IsParakeetModelInstalled()` runs before `MainWindow` is created. If it returns false, `ShowFirstLaunchModelDownload()` opens a modal hosting `ModelDownloadControl`. Cancel exits the app — `MainWindow` is never instantiated without a model.
- **SharpCompress API gotcha**: `tar.bz2` extraction uses `ArchiveFactory.OpenArchive(string, ReaderOptions)`. Both `ArchiveFactory.Open` and `ReaderFactory.Open` were renamed in newer SharpCompress versions; using the wrong name fails at runtime, not compile.
- **Always `dotnet clean` before release publish**: Incremental `dotnet publish` can leave stale NuGet package DLLs in `bin/Release/.../publish/` even when csproj declares a newer version. Hit in v2.1.1: publish output had `System.Text.Json.dll` v9.0.0.0 while csproj referenced v10.0.0. The installer copies `*.dll` from publish, ships the wrong DLL, and on first launch the CLR can't bind `System.Text.Json, Version=10.0.0.0` → `MainWindow` static init (`_jsonSerializerOptions`) throws `TypeInitializationException`. Symptom is the misleading "type initializer for 'VoiceLite.MainWindow' threw" dialog with no obvious cause. Fix: `rm -rf VoiceLite/VoiceLite/bin VoiceLite/VoiceLite/obj && dotnet publish ... --self-contained` before every installer build. GH Actions does this implicitly (fresh runner). Local builds for tagged releases must clean first.
- **Uninstall preserves user data by default; pass `/PURGEDATA` to wipe**: `VoiceLiteSetup.iss` `CurUninstallStepChanged` no longer auto-deletes `%LocalAppData%\VoiceLite\` on silent uninstall. Default behavior: keep settings + license + Parakeet model + history. This matters because Inno's in-place upgrade flow silently invokes the old version's uninstaller before installing the new one — the previous "wipe on silent" code path was destroying every user's data on every upgrade. New rules: silent uninstall without `/PURGEDATA` preserves data (Inno upgrades, Intune redeploys); interactive uninstall asks via clearly-worded dialog with NO as default; `unins000.exe /VERYSILENT /PURGEDATA` is the explicit decommission path for IT admins.

## Patterns to Follow

**Error logging**:
```csharp
ErrorLogger.LogMessage("info");      // Debug only
ErrorLogger.LogWarning("visible");   // Shows in Release builds
ErrorLogger.LogError("context", ex); // Always shows
```

**Disposal**: Services with resources implement `IDisposable` directly (no interfaces). `PersistentWhisperService.Dispose()` disposes the `OfflineRecognizer` (releases native model memory), cancellation tokens, and semaphore. Tests validate disposal (`*DisposalTests.cs`).

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
| `VoiceLite/VoiceLite/Services/SettingsMigration.cs` | Rewrites legacy GGML model names → Parakeet on first run after upgrade |
| `VoiceLite/whisper/` | Silero VAD ONNX only (`silero_vad_v5.onnx`). Sherpa-ONNX native DLLs come from NuGet runtimes. Parakeet model files live in `%LocalAppData%\VoiceLite\models\parakeet-v3\` post-download. |
| `VoiceLite/LICENSES/` | Third-party license texts. CC-BY-4.0 + NVIDIA Parakeet notice — ships with installer (mandatory legal). |
| `voicelite-web/app/api/` | Next.js API routes (licenses, checkout, feedback) |
| `voicelite-web/prisma/schema.prisma` | Database schema |

## Speech Engine

| Engine | Model | Size | Tier |
|--------|-------|------|------|
| Sherpa-ONNX (k2-fsa) | NVIDIA Parakeet TDT 0.6B v3 (int8) | ~640MB | All users |

Single model post-v2.0. Downloaded on first launch from k2-fsa GitHub Releases, extracted to `%LocalAppData%\VoiceLite\models\parakeet-v3\`. License: CC-BY-4.0 (attribution shipped in `LICENSES\`).

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
git tag v2.0.0-beta
git push --tags
# Workflow: validate versions → build → create installer → GitHub release
```

**Version sync required**: `VoiceLite.csproj`, `VoiceLiteSetup.iss` must match tag. Workflow validates.

## Pro Feature Gating

Post-v2.0, **model gating is removed** — the single Parakeet model is available to all users (Decision 4 in `docs/parakeet-migration-plan.md`). `ProFeatureService.CanUseModel` / `IsModelAvailable` now always return `true`. The `IsProUser` flag and DB schema are kept — the seam survives for future Pro features (Voice Shortcuts, Export History, Custom Dictionary, Advanced Settings — all scaffolded as `*Visibility` properties but not yet shipped).

```csharp
// ProFeatureService.cs still controls visibility for non-model features
public Visibility VoiceShortcutsTabVisibility => IsProUser ? Visibility.Visible : Visibility.Collapsed;
```

Pro pricing copy is intentionally vague ("Pro features coming — your license is preserved") until the four scaffolded features ship.

## Testing

- Disposal tests are critical (memory leak prevention)
- Run `dotnet test` before every commit
- **Phase E debt (v2.0)**: ~61 tests in `WhisperModelInfoTests`, `ProFeatureServiceTests`, `ModelResolverServiceTests`, `WhisperServiceTests`, `WhisperErrorRecoveryTests` still reference the deleted 5-model GGML lineup and old Pro gating. These are rewritten in a separate Phase E session per `docs/parakeet-migration-plan.md`. Don't touch them in Phase D work.

## Version

Check `git tag` for current version. Desktop version in `VoiceLite/VoiceLite/VoiceLite.csproj`.

# CLAUDE.md

VoiceLite: Radically simplified Windows speech-to-text app using OpenAI Whisper AI. **Philosophy**: Core-only, zero complexity. Just recording → Whisper → text injection.

## Quick Commands

### Build & Run
```bash
# Build and run
dotnet build VoiceLite/VoiceLite.sln
dotnet run --project VoiceLite/VoiceLite/VoiceLite.csproj

# Release build
dotnet build VoiceLite/VoiceLite.sln -c Release
dotnet publish VoiceLite/VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained
```

### Testing
```bash
# Run all tests (~200 tests)
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj

# With coverage (target: ≥75% overall, ≥80% Services/)
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj --collect:"XPlat Code Coverage"
```

### Installer (Inno Setup)
```bash
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" VoiceLiteSetup_Simple.iss
```

### Web App (voicelite-web/)
```bash
cd voicelite-web
npm install
npm run dev                # Development server
npm run build              # Production build
npm run db:migrate         # Database migrations
npm run db:studio          # Prisma Studio GUI
vercel deploy --prod       # Deploy to production
```

### Release (Automated via GitHub Actions)
```bash
# Tag and push - workflow auto-builds installer
git tag v1.0.XX
git push --tags
# Workflow updates versions, builds, creates GitHub release (~5-7 min)
```

## Project Architecture

**Target**: .NET 8.0 Windows | **Distribution**: 100% free, no licensing

### Core Components (8 Services - v1.0.74 simplification)

**Active Services**:
- `AudioRecorder`: NAudio recording (16kHz mono WAV, no preprocessing)
- `PersistentWhisperService`: Whisper.cpp subprocess (greedy decoding: beam_size=1)
- `TextInjector`: Text injection via InputSimulator (SmartAuto/Type/Paste modes)
- `HotkeyManager`: Global hotkeys via Win32 API
- `SystemTrayManager`: Tray icon + context menu
- `TranscriptionHistoryService`: History with pinning
- `ErrorLogger`: Centralized error logging
- `LicenseService`: Legacy/unused (TODO: remove)

**Removed in v1.0.65** (~15,000 lines deleted):
- ❌ VoiceShortcuts, TranscriptionPostProcessor, Analytics, Licensing
- ❌ WhisperServerService, RecordingCoordinator, ModelBenchmarkService

### Whisper Models (in `whisper/` directory)

- `ggml-tiny.bin` (75MB): **Lite** - Legacy fallback
- `ggml-small.bin` (466MB): **Pro** ⭐ - Current default (ships with installer)
- `ggml-base.bin` (142MB): **Swift** - Fast
- `ggml-medium.bin` (1.5GB): **Elite** - Higher accuracy
- `ggml-large-v3.bin` (2.9GB): **Ultra** - Highest accuracy

**Whisper Command** (v1.0.65+):
```bash
whisper.exe -m [model] -f [audio.wav] --no-timestamps --language en \
  --temperature 0.2 --beam-size 1 --best-of 1  # Greedy decoding (5x faster)
```

### File Locations

- **Settings**: `%LOCALAPPDATA%\VoiceLite\settings.json` (Local, NOT synced)
- **Logs**: `%LOCALAPPDATA%\VoiceLite\logs\voicelite.log` (10MB rotation)
- **Dependencies**: Visual C++ Runtime 2015-2022 x64 (bundled in installer)

### Key Dependencies

```xml
<PackageReference Include="NAudio" Version="2.2.1" />
<PackageReference Include="H.InputSimulator" Version="1.2.1" />
<PackageReference Include="Hardcodet.NotifyIcon.Wpf" Version="2.0.1" />
<PackageReference Include="System.Text.Json" Version="9.0.9" />
<PackageReference Include="System.Management" Version="8.0.0" />
```

## Code Guidelines

### Critical Implementation Rules

1. **Audio Format**: 16kHz, 16-bit mono WAV (no preprocessing for reliability)
2. **Thread Safety**: Use `lock` for recording state, `Dispatcher.Invoke()` for UI updates
3. **Process Management**: One whisper.exe per transcription, auto-cleanup via ZombieProcessCleanupService
4. **Memory**: Always dispose IDisposable, check disposal tests
5. **Error Handling**: Centralized via ErrorLogger, graceful fallbacks
6. **Text Injection**: SmartAuto mode (clipboard for >100 chars, typing for short)

### Testing Standards

- **Coverage**: ≥75% overall, ≥80% Services/
- **Frameworks**: xUnit, Moq, FluentAssertions
- **Run before commit**: `dotnet test` (all tests must pass)
- **Disposal tests**: Critical for memory leak prevention

### WPF Patterns

- **XAML**: Use `ModernStyles.xaml` for consistency
- **Converters**: `RelativeTimeConverter` ("5 mins ago"), `TruncateTextConverter`
- **UI Updates**: Always use `Dispatcher.Invoke()` from non-UI threads
- **Resources**: Icons in root

## Known Issues

1. **VCRUNTIME140_1.dll**: Installer bundles VC++ Runtime (auto-installs)
2. **Antivirus**: Text injection may trigger false positives (global hotkeys)
3. **First Run**: StartupDiagnostics auto-fixes common issues

## Version Context

**Current Desktop**: v1.0.74 (100% free, radical simplification)
**Major Change (v1.0.65)**: Removed ~15,000 lines (VoiceShortcuts, Analytics, Licensing, Server mode)
**Philosophy**: Reliability over features - core-only workflow

## Web Backend (voicelite-web)

**Tech Stack**: Next.js 15, React 19, Prisma, PostgreSQL (Supabase)
**Purpose**: Landing page, download links, feedback collection
**No Licensing**: Backend no longer validates licenses (100% free app), no Stripe/payments

**API Endpoints** (simplified v1.0.65+):
- `POST /api/feedback` - User feedback (rate limited via Upstash Redis)
- `POST /api/metrics/upload` - Server telemetry (desktop app doesn't use)
- Landing page at `/`

**Removed**: Authentication, licensing, desktop analytics, Ed25519 signing

## Performance Targets

- Transcription latency: <200ms after speech stops
- Idle RAM: <100MB | Active RAM: <300MB
- Idle CPU: <5%
- Accuracy: 95%+ on technical terms (git, npm, useState)

## Distribution

**Installer**: `VoiceLite-Setup-{VERSION}.exe` (~540MB, includes Pro + Lite models)
**Release Process**: GitHub Actions auto-builds on git tag push
**Channels**: GitHub Releases (primary), Google Drive (mirror)

---

**For detailed changelogs, architecture history, and migration notes**: See git history and inline code comments. This file focuses on **actionable development commands and critical context**.

# CLAUDE.md

VoiceLite: Windows speech-to-text desktop app using OpenAI Whisper AI.
**Architecture**: .NET 8.0 WPF + Next.js web backend
**Business Model**: Free tier (Tiny model) + Pro upgrade ($20 one-time, unlocks 4 advanced models)

## Tech Stack

**Desktop (.NET 8.0)**
- WPF UI + System tray
- NAudio (audio recording)
- Whisper.cpp (speech-to-text subprocess)
- H.InputSimulator (text injection)
- xUnit + Moq + FluentAssertions (testing)

**Web Backend (voicelite-web/)**
- Next.js 15.5 + React 19 + TypeScript
- Prisma 6.1 + PostgreSQL (Supabase)
- Stripe 18.5 (payments)
- Upstash Redis (rate limiting)

## Project Structure

```
VoiceLite/
├── VoiceLite/               # Desktop app (.NET)
│   ├── Services/            # Core business logic (9 services)
│   ├── Views/               # WPF windows/controls
│   ├── Styles/              # ModernStyles.xaml + converters
│   └── whisper/             # Whisper models (ggml-*.bin)
├── VoiceLite.Tests/         # xUnit tests (~200 tests)
├── VoiceLiteSetup_Simple.iss # Inno Setup installer script
└── voicelite-web/           # Web backend (Next.js)
    ├── app/api/             # API routes (licenses, checkout, feedback)
    ├── prisma/              # Database schema + migrations
    └── lib/                 # Utilities (Stripe, rate limiting)
```

**IMPORTANT**: User data is in `%LOCALAPPDATA%\VoiceLite\` (settings.json, logs/)

## Essential Commands

### Desktop: Build & Test
```bash
# Development
dotnet build VoiceLite/VoiceLite.sln
dotnet run --project VoiceLite/VoiceLite/VoiceLite.csproj

# Testing (MUST pass before commit)
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj

# Coverage (target: ≥75% overall, ≥80% Services/)
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj --collect:"XPlat Code Coverage"

# Release build
dotnet publish VoiceLite/VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained
```

### Desktop: Installer
```bash
# Requires Inno Setup 6
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" VoiceLiteSetup_Simple.iss
```

### Web: Development
```bash
cd voicelite-web
npm install
npm run dev                # Local dev server
npm run build              # Production build
npm run db:migrate         # Apply Prisma migrations
npm run db:studio          # Prisma GUI
vercel deploy --prod       # Deploy to production
```

### Release Process (GitHub Actions)
```bash
# Tag triggers automated workflow: version bump → build → installer → GitHub release
git tag v1.0.XX
git push --tags
```

## Code Guidelines

### CRITICAL: Thread Safety & Resource Management
1. **ALWAYS dispose IDisposable resources** - Check disposal tests before commit
2. **ALWAYS use `lock` for shared recording state** - Prevents race conditions
3. **ALWAYS use `Dispatcher.Invoke()` for UI updates from background threads** - WPF requirement
4. **NEVER skip disposal tests** - They prevent memory leaks

### CRITICAL: Audio & Whisper Processing
1. **Audio format MUST be 16kHz, 16-bit mono WAV** - No preprocessing (reliability)
2. **One whisper.exe subprocess per transcription** - Manual cleanup via `Process.Kill()`
3. **Whisper command**:
   ```bash
   whisper.exe -m [model] -f [audio.wav] --no-timestamps --language en \
     --temperature 0.2 --beam-size 1 --best-of 1
   ```
4. **Model locations**: `whisper/` directory (ggml-*.bin files)

### CRITICAL: Text Injection
- **SmartAuto mode (default)**: Clipboard for >100 chars, typing for shorter
- **NEVER block UI thread during injection** - Use background tasks
- **Known issue**: Antivirus may flag InputSimulator as suspicious

### WPF Patterns
- **XAML styling**: Use `ModernStyles.xaml` for consistency
- **Converters**: `RelativeTimeConverter`, `TruncateTextConverter`
- **Icons**: Stored in project root

### Testing Requirements
- **Coverage targets**: ≥75% overall, ≥80% Services/
- **Frameworks**: xUnit, Moq, FluentAssertions
- **MUST run before every commit**: `dotnet test` (all tests pass)
- **MUST check disposal tests** - Critical for memory leak prevention

## Core Services (VoiceLite/Services/)

1. **AudioRecorder** - NAudio recording (16kHz mono WAV)
2. **PersistentWhisperService** - Whisper.cpp subprocess management
3. **TextInjector** - InputSimulator text injection (SmartAuto/Type/Paste)
4. **HotkeyManager** - Global hotkeys via Win32 API
5. **SystemTrayManager** - Tray icon + context menu
6. **TranscriptionHistoryService** - History with pinning
7. **ErrorLogger** - Centralized error logging
8. **LicenseService** - Pro license validation (HTTP to voicelite.app/api/licenses/validate)
9. **ProFeatureService** - Feature gating (UI visibility: `IsProUser` property)

## Pro Features System

**License Flow**:
1. User pays $20 via Stripe → Backend creates License record
2. User enters key in Settings → Desktop validates via API
3. LicenseService caches validation → ProFeatureService gates UI

**Free Tier**: Tiny model only (ggml-tiny.bin, 75MB, bundled in installer)

**Pro Tier** ($20 one-time):
- Small model (ggml-small.bin, 466MB) - Included in source
- Base/Medium/Large models - Downloaded via AI Models tab
- 3 device activations per license

**Adding New Pro Features** (3-step pattern):
```csharp
// 1. ProFeatureService.cs - Add visibility property
public Visibility NewFeatureVisibility => IsProUser ? Visibility.Visible : Visibility.Collapsed;

// 2. SettingsWindowNew.xaml - Add gated UI
<TabItem Header="New Feature" Name="NewFeatureTab">...</TabItem>

// 3. SettingsWindowNew.xaml.cs - Bind visibility
NewFeatureTab.Visibility = proFeatureService.NewFeatureVisibility;
```

## Web API (voicelite-web/app/api/)

**Key Endpoints**:
- `POST /api/licenses/validate` - License validation (rate limited: 5/hour/IP)
- `POST /api/checkout` - Stripe checkout session
- `POST /api/webhook` - Stripe webhook handler
- `POST /api/feedback/submit` - User feedback
- `POST /api/download` - Download tracking

**Database Models** (Prisma):
- `License` - Email-based licensing (no user accounts)
- `LicenseActivation` - Device tracking (3-device limit)
- `LicenseEvent` - Audit trail
- `Feedback` - User feedback with priority/status

## Common Pitfalls (DO NOT)

1. **DO NOT preprocess audio** - Use raw 16kHz mono WAV (reliability over quality)
2. **DO NOT skip test runs** - All 200 tests must pass before commit
3. **DO NOT forget disposal** - IDisposable resources must be cleaned up
4. **DO NOT update UI from background threads** - Always use `Dispatcher.Invoke()`
5. **DO NOT commit without checking coverage** - Maintain ≥75% overall, ≥80% Services/
6. **DO NOT bundle large models in installer** - Only Tiny (75MB) is bundled
7. **DO NOT skip license validation** - Always gate Pro features via ProFeatureService
8. **DO NOT leave zombie whisper.exe processes** - Manual cleanup via Process.Kill()

## Performance Targets

- Transcription latency: <200ms after speech stops
- Idle RAM: <100MB | Active RAM: <300MB
- Idle CPU: <5%
- Accuracy: 95%+ on technical terms (git, npm, useState)

## Distribution

**Installer**: `VoiceLite-Setup-{VERSION}.exe` (~100-150MB, Tiny model only)
**Process**: GitHub Actions auto-builds on tag push → Creates GitHub release (~5-7 min)
**Channels**: GitHub Releases (primary), Google Drive (mirror)

## Dependencies

**Desktop** (NuGet):
- NAudio 2.2.1
- H.InputSimulator 1.2.1
- Hardcodet.NotifyIcon.Wpf 2.0.1
- System.Text.Json 9.0.9
- System.Management 8.0.0

**Web** (npm):
- Next.js 15.5.4 + React 19.2.0
- Prisma 6.1.0 + @prisma/client
- Stripe 18.5.0
- @upstash/redis (rate limiting)

**System**: Visual C++ Runtime 2015-2022 x64 (bundled in installer)

---

**For historical context**: See git log and inline code comments. This file contains essential development info only.

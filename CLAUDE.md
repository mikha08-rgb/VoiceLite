# CLAUDE.md

VoiceLite: Windows speech-to-text desktop app using OpenAI Whisper AI.
**Architecture**: .NET 8.0 WPF + Next.js web backend | **Model**: Free tier (Tiny) + Pro upgrade ($20, unlocks 4 models)

## Project Structure

```
VoiceLite/
├── VoiceLite/VoiceLite/      # Desktop app (.NET)
│   ├── Services/             # 9 core services (business logic)
│   ├── Views/                # WPF windows/controls
│   └── whisper/              # Whisper models (ggml-*.bin)
├── VoiceLite/VoiceLite.Tests/  # xUnit tests (~200 tests)
└── voicelite-web/            # Web backend (Next.js + Prisma + Stripe)
```

**User data**: `%LOCALAPPDATA%\VoiceLite\` (settings.json, logs/)

## Tech Stack

**Desktop**: .NET 8.0, WPF, NAudio 2.2.1, H.InputSimulator 1.2.1, Whisper.cpp subprocess
**Web**: Next.js 15.5, React 19, Prisma 6.1, PostgreSQL (Supabase), Stripe 18.5, Upstash Redis
**Testing**: xUnit, Moq, FluentAssertions

## Essential Commands

```bash
# Desktop: Build & Test
dotnet build VoiceLite/VoiceLite.sln
dotnet run --project VoiceLite/VoiceLite/VoiceLite.csproj
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj  # MUST pass before commit
dotnet test --collect:"XPlat Code Coverage"  # Coverage: ≥75% overall, ≥80% Services/

# Desktop: Release
dotnet publish VoiceLite/VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" VoiceLiteSetup_Simple.iss

# Web: Development
cd voicelite-web && npm install && npm run dev
npm run db:migrate  # Prisma migrations
vercel deploy --prod

# Release (GitHub Actions)
git tag v1.0.XX && git push --tags  # Auto-builds installer in ~5-7 min
```

## Critical Development Rules

### Thread Safety & Resources (MOST IMPORTANT)
1. **ALWAYS dispose IDisposable** - Use `using` statements (see AudioRecorder.cs pattern). Undisposed resources leak 5MB/recording → 500MB after 100 recordings
2. **ALWAYS lock shared state** - Use `lock (_recordingLock)` before accessing `_isRecording` etc. Race conditions cause duplicate recordings
3. **ALWAYS use Dispatcher.Invoke()** for UI updates from background threads - WPF throws InvalidOperationException otherwise
4. **NEVER skip disposal tests** - Run before every commit touching Services/

### Audio & Whisper (NEVER MODIFY)
5. **Audio MUST be 16kHz, 16-bit mono WAV** - No preprocessing (see AudioRecorder.cs). Whisper trained on this format; noise reduction reduces accuracy
6. **One whisper.exe subprocess per transcription** - ALWAYS Process.Kill() in finally block (see PersistentWhisperService.cs). Zombies consume 200MB each
7. **Whisper command** (DO NOT CHANGE):
   ```bash
   whisper.exe -m [model] -f [audio.wav] --no-timestamps --language en --temperature 0.2 --beam-size 1 --best-of 1
   ```
   Greedy decoding (beam-size=1) is 5x faster with minimal accuracy loss

### Text Injection & WPF
8. **SmartAuto mode**: Clipboard for >100 chars (fast), typing for short text (preserves formatting). See TextInjector.cs
9. **XAML**: Use ModernStyles.xaml for consistency. UI updates always via Dispatcher.Invoke()

### Testing (BEFORE EVERY COMMIT)
- Run: `dotnet test` - ALL ~200 tests MUST pass (no skips)
- Check: Coverage ≥75% overall, ≥80% Services/
- Verify: ALL disposal tests pass

## Core Services (VoiceLite/Services/)

When modifying functionality, identify which service owns the logic:

1. **AudioRecorder** - NAudio recording (16kHz mono WAV, no preprocessing)
2. **PersistentWhisperService** - whisper.exe subprocess lifecycle
3. **TextInjector** - Text injection via InputSimulator (SmartAuto/Type/Paste)
4. **HotkeyManager** - Global hotkeys (Win32 API)
5. **SystemTrayManager** - Tray icon + context menu
6. **TranscriptionHistoryService** - History with pin/delete/copy
7. **ErrorLogger** - Centralized logging to `%LOCALAPPDATA%\VoiceLite\logs\`
8. **LicenseService** - Pro validation via voicelite.app/api/licenses/validate
9. **ProFeatureService** - Feature gating (`IsProUser` property for UI visibility)

## Pro Features System

**License Flow**: Stripe payment → Backend creates License → User enters key → Desktop validates → ProFeatureService gates UI

**Free**: Tiny model (75MB, bundled)
**Pro ($20)**: Small (466MB, in source) + Base/Medium/Large (downloadable via AI Models tab) + 3 device activations

**Adding Pro Features** (3-step pattern - see ProFeatureService.cs + SettingsWindowNew.xaml):
1. Add visibility property to ProFeatureService: `public Visibility FeatureVisibility => IsProUser ? Visible : Collapsed;`
2. Add gated UI to XAML: `<TabItem Header="Feature" Name="FeatureTab">...</TabItem>`
3. Bind visibility in code-behind: `FeatureTab.Visibility = proFeatureService.FeatureVisibility;`

**WHY**: Centralized in ProFeatureService = single source of truth, prevents bypass

## Web API (voicelite-web/app/api/)

- `POST /api/licenses/validate` - Validates license (rate limited: 5/hour/IP)
- `POST /api/checkout` - Stripe checkout ($20 payment)
- `POST /api/webhook` - Stripe webhook (creates License on payment)
- `POST /api/feedback/submit` - User feedback (rate limited)
- `POST /api/download` - Download tracking

**DB Models**: License (email-based, Stripe-linked), LicenseActivation (3-device limit), LicenseEvent (audit), Feedback

## Common Pitfalls - DO NOT

1. **Preprocess audio** - Whisper trained on raw audio, preprocessing reduces accuracy
2. **Skip tests** - 200 tests exist to catch regressions
3. **Forget disposal** - Memory leaks crash long-running sessions
4. **Update UI from background threads** - WPF throws InvalidOperationException
5. **Bundle large models** - Only Tiny (75MB) in installer; Base/Medium/Large download via AI Models tab
6. **Skip license validation** - Always gate Pro features via ProFeatureService.IsProUser
7. **Leave zombie whisper.exe** - Each consumes ~200MB RAM

## Performance Targets

Measure via Task Manager + test recordings:
- Transcription latency: <200ms from speech stop to text injection
- Idle RAM: <100MB | Active RAM: <300MB | Idle CPU: <5%
- Accuracy: 95%+ on technical terms (Small+ models)

## Distribution

**Installer**: VoiceLite-Setup-{VERSION}.exe (~100-150MB, Tiny only)
**Process**: Tag push → GitHub Actions → Version bump → Build → Installer → Release (~5-7 min)
**Channels**: GitHub Releases (primary), Google Drive (mirror)

---

**For detailed context**: See git log. This file focuses on VoiceLite-specific patterns, not general .NET/WPF knowledge.

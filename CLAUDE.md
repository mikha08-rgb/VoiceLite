# CLAUDE.md

VoiceLite: Windows speech-to-text app using OpenAI Whisper AI. **Philosophy**: Core-only workflow with Pro licensing for advanced models. Recording → Whisper → text injection.

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

**Target**: .NET 8.0 Windows | **Distribution**: Free tier + Pro upgrade ($20 one-time)

### Core Components (9 Services - v1.0.79)

**Active Services**:
- `AudioRecorder`: NAudio recording (16kHz mono WAV, no preprocessing)
- `PersistentWhisperService`: Whisper.cpp subprocess (greedy decoding: beam_size=1)
- `TextInjector`: Text injection via InputSimulator (SmartAuto/Type/Paste modes)
- `HotkeyManager`: Global hotkeys via Win32 API
- `SystemTrayManager`: Tray icon + context menu
- `TranscriptionHistoryService`: History with pinning
- `ErrorLogger`: Centralized error logging
- `LicenseService`: Pro license validation via web API
- `ProFeatureService`: Centralized Pro feature gating (AI Models tab, future features)

**Note**: Services like `MemoryMonitor`, `StartupDiagnostics`, `DependencyChecker`, `ZombieProcessCleanupService` exist only in `voicelite-web-preview/` branch (v1.0.70), not in main production app.

### Whisper Models (in `whisper/` directory)

**Most models Q8_0 quantized (v1.0.88+)** for 45% size reduction & 30-40% speed boost with identical accuracy. Large-v3 uses F16 (Q8_0 unavailable upstream).

**Free Tier (bundled with installer):**
- `ggml-tiny.bin` (42MB Q8_0): **Lite** - Default, 80-85% accuracy, <0.8s processing

**Pro Tier ($20 one-time - downloadable in-app):**
- `ggml-base.bin` (78MB Q8_0): **Swift** - 85-90% accuracy, ~1.5s processing
- `ggml-small.bin` (253MB Q8_0): **Pro** ⭐ - 90-93% accuracy, ~3s processing (recommended)
- `ggml-medium.bin` (823MB Q8_0): **Elite** - 95-97% accuracy, ~12s processing
- `ggml-large-v3.bin` (3.1GB F16): **Ultra** - 97-98% accuracy, ~15s processing (not quantized - Q8_0 unavailable)

**Quantization Details (v1.0.88)**:
- Method: Q8_0 (8-bit integer quantization)
- Accuracy: 99.98% identical to F16 (research-proven, arXiv 2503.09905)
- Benefits: Smaller downloads, faster inference, lower memory usage
- F16 backups: Available as `*-f16.backup` for rollback if needed

**Whisper Command** (v1.0.87+):
```bash
whisper.exe -m [model] -f [audio.wav] --no-timestamps --language en \
  --beam-size 1 --best-of 1 --entropy-thold 3.0 --no-fallback \
  --max-context 64 --flash-attn  # Optimized for speed (67-73% faster than v1.0.84)
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
3. **Process Management**: One whisper.exe per transcription, manual cleanup via Process.Kill()
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
3. **License Validation**: Requires internet connection for initial Pro activation (cached after first validation)

## Version Context

**Current Desktop**: v1.1.5 (Fix Large model download 404 error)
**Current Web**: v0.1.0 (Next.js 15 + React 19 + Prisma)
**Philosophy**: Core-only workflow with Pro feature gating for advanced models

**Performance Journey** (v1.0.85-88):
- v1.0.85: Command-line optimizations (entropy-thold, no-fallback, optimal threads) - 40% faster
- v1.0.86: Upgraded whisper.cpp v1.6.0 → v1.7.6 - Additional 20-40% faster
- v1.0.87: Added flash attention + Q8_0 tiny model - Additional 7-12% faster
- v1.0.88: Q8_0 quantization for all Pro models - 67-73% faster overall, 45% smaller

**Recent Critical Fixes**:
- **v1.0.96**: CRITICAL model file not in git - `ggml-tiny.bin` (42MB) was ignored by `.gitignore`, causing GitHub Actions builds to fail (100% failure rate on fresh installs). Fixed by force-adding model to git with `git add -f`. v1.0.95 was broken for all fresh installations.
- **v1.0.95**: PARTIAL FIX - Fixed installer path but model still missing (only fixed local builds, not GitHub Actions)
- **v1.0.94**: Critical logging bug - Release builds had logging suppressed, preventing diagnostics
- **v1.0.77-79**: Security fixes - Closed freemium bypass vulnerabilities, Pro feature gating

## Web Backend (voicelite-web)

**Tech Stack**: Next.js 15.5.4, React 19.2.0, Prisma 6.1.0, PostgreSQL (Supabase), Stripe 18.5.0
**Purpose**: License validation, Stripe payments, model downloads, feedback collection

**API Endpoints**:
- `POST /api/licenses/validate` - License key validation (rate limited: 5/hour/IP)
- `POST /api/checkout` - Stripe checkout session creation
- `POST /api/feedback/submit` - User feedback (rate limited via Upstash Redis)
- `POST /api/download` - Model/app download tracking
- `POST /api/webhook` - Stripe webhook handler for payment events
- `GET /api/docs` - API documentation

**Database Models** (Prisma):
- `License` - Core licensing with Stripe integration (email-based, no user accounts)
- `LicenseActivation` - Device activation tracking (3-device limit per license)
- `LicenseEvent` - Audit trail for license operations
- `WebhookEvent` - Stripe webhook event deduplication
- `Feedback` - User feedback with priority/status tracking

## Performance Targets

- Transcription latency: <200ms after speech stops
- Idle RAM: <100MB | Active RAM: <300MB
- Idle CPU: <5%
- Accuracy: 95%+ on technical terms (git, npm, useState)

## Distribution

**Installer**: `VoiceLite-Setup-{VERSION}.exe` (~100-150MB, includes Tiny model only)
**Release Process**: GitHub Actions auto-builds on git tag push
**Channels**: GitHub Releases (primary), Google Drive (mirror)

**Pro License**: $20 one-time payment via Stripe → Email with UUID license key → Activate in Settings → Unlock AI Models tab for in-app model downloads

## Pro Features System

**Architecture**: Centralized feature gating via `ProFeatureService.cs` + `LicenseService.cs`

**License Validation Flow**:
1. User purchases via Stripe → Backend creates License record
2. User enters license key in Settings → Desktop calls `/api/licenses/validate`
3. LicenseService caches validation result (HTTPClient to voicelite.app)
4. ProFeatureService exposes `IsProUser` property based on cached status

**Free Tier**:
- Tiny model (ggml-tiny.bin) only
- Basic transcription
- Settings: General + License tabs only

**Pro Tier** ($20 one-time):
- All 5 AI models (Small included in source, Base/Medium/Large downloadable via AI Models tab)
- Settings: General + **AI Models** + License tabs
- 3 device activations per license
- Future Pro features: Voice Shortcuts, Export History, Custom Dictionary, etc.

**Adding New Pro Features** (3-step pattern):
```csharp
// 1. In ProFeatureService.cs: Add visibility property
public Visibility VoiceShortcutsTabVisibility => IsProUser ? Visibility.Visible : Visibility.Collapsed;

// 2. In SettingsWindowNew.xaml: Add gated tab
<TabItem Header="Voice Shortcuts" Name="VoiceShortcutsTab">...</TabItem>

// 3. In SettingsWindowNew.xaml.cs: Bind visibility
VoiceShortcutsTab.Visibility = proFeatureService.VoiceShortcutsTabVisibility;
```

---

**For detailed changelogs, architecture history, and migration notes**: See git history and inline code comments. This file focuses on **actionable development commands and critical context**.


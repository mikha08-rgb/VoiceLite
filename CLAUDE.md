# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview
VoiceLite is a production-ready Windows native speech-to-text application using OpenAI Whisper AI. It provides instant voice typing anywhere in Windows via global hotkey. The app is fully functional with comprehensive error handling, multiple Whisper models, and performance optimizations.

## Common Development Commands

### Build & Run
```bash
# Build the solution
dotnet build VoiceLite/VoiceLite.sln

# Run in Debug mode
dotnet run --project VoiceLite/VoiceLite/VoiceLite.csproj

# Build Release version
dotnet build VoiceLite/VoiceLite.sln -c Release

# Publish self-contained executable
dotnet publish VoiceLite/VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained
```

### Testing
```bash
# Run all tests
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj

# Run specific test
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj --filter "FullyQualifiedName~TestName"

# Run tests with coverage
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj --collect:"XPlat Code Coverage" --settings VoiceLite/VoiceLite.Tests/coverlet.runsettings
```

### Code Quality
```bash
# Format code
dotnet format VoiceLite/VoiceLite.sln

# Analyze code (if analyzers are installed)
dotnet build VoiceLite/VoiceLite.sln /p:RunAnalyzers=true
```

### Installer (Inno Setup)
```bash
# Build installer (requires Inno Setup installed)
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" VoiceLiteSetup_Simple.iss

# Build from project root (if script is in Installer/ directory)
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" VoiceLite/Installer/VoiceLiteSetup_Simple.iss
```

### CI/CD (GitHub Actions)
```bash
# Automated PR testing (runs on every PR to master)
# - Tests desktop app (106 tests)
# - Validates web app build and types
# Workflow: .github/workflows/pr-tests.yml

# Automated release build (run via git tag)
git tag v1.0.22
git push --tags
# This triggers .github/workflows/release.yml which:
# - Auto-updates version in .csproj and .iss files
# - Builds Release with dotnet publish
# - Compiles installer with Inno Setup
# - Creates GitHub release with installer upload

# Manual release trigger (via GitHub UI)
# Go to Actions → Build and Release → Run workflow
# Enter version number (e.g., 1.0.22)
```

### Web Application (Next.js Backend)
```bash
# Navigate to web directory
cd voicelite-web

# Install dependencies
npm install

# Run development server
npm run dev

# Build for production
npm run build

# Start production server
npm start

# Deploy to Vercel (requires Vercel CLI)
vercel deploy --prod
```

### Database (Prisma)
```bash
# Navigate to web directory
cd voicelite-web

# Create and apply migration
npm run db:migrate

# Push schema changes (without migration)
npm run db:push

# Seed database with initial data
npm run db:seed

# Open Prisma Studio (database GUI)
npm run db:studio

# Generate license keys
npm run keygen
```

### API Documentation
```bash
# View interactive API documentation
# Start dev server: npm run dev (in voicelite-web/)
# Then visit: http://localhost:3000/docs

# API specification is also available at:
# http://localhost:3000/api/docs (OpenAPI 3.0 JSON)
```

**Available API Routes** (22 total):
- **Authentication**: Magic link login, OTP verification, logout
- **Payments**: Stripe checkout, customer portal
- **Licenses**: Activation, deactivation, validation, CRL
- **Analytics**: Privacy-first event tracking (opt-in)
- **Admin**: Dashboard stats, feedback management
- **User**: Profile and license retrieval

**Key Features**:
- OpenAPI 3.0 specification auto-generated from Zod schemas
- Interactive Swagger UI at `/docs`
- Type-safe request/response validation
- Comprehensive error handling
- Rate limiting (via Upstash Redis)

## Architecture Overview

### Core Components

1. **MainWindow** (`MainWindow.xaml.cs`): Entry point, coordinates all services
   - Manages recording state and hotkey handling
   - Implements push-to-talk and toggle modes
   - Handles system tray integration
   - Visual state management for recording indicators

2. **Service Layer** (`Services/`): Modular, single-responsibility services
   - `AudioRecorder`: NAudio-based recording with noise suppression
   - `PersistentWhisperService`: Main Whisper.cpp subprocess manager with warmup and process pooling
   - `TextInjector`: Text injection using InputSimulator (supports multiple modes)
   - `HotkeyManager`: Global hotkey registration via Win32 API
   - `SystemTrayManager`: System tray icon and context menu
   - `AudioPreprocessor`: Audio enhancement (noise gate, gain control)
   - `TranscriptionPostProcessor`: Text corrections and formatting (v1.0.24: Added customizable post-processing - capitalization, punctuation, filler word removal, contractions, grammar fixes)
   - `TranscriptionHistoryService`: Manages transcription history with pinning and auto-cleanup
   - `SoundService`: Custom UI sound effects (wood-tap-click.ogg)
   - `ModelBenchmarkService`: Model performance testing
   - `MemoryMonitor`: Memory usage tracking
   - `ErrorLogger`: Centralized error logging
   - `StartupDiagnostics`: Comprehensive startup checks and auto-fixes
   - _Legacy licensing components removed_: VoiceLite now ships as a fully free build
   - `MetricsTracker`: Performance metrics collection
   - `SecurityService`: Security and obfuscation helpers
   - `DependencyChecker`: Verify runtime dependencies
   - `AnalyticsService`: Privacy-first opt-in analytics with SHA256 anonymous user IDs (v1.0.17+)
   - `RecordingCoordinator`: Orchestrates recording workflow and state management (v1.0.18+)

3. **Models** (`Models/`): Data structures and configuration
   - `Settings`: User preferences with validation
   - `TranscriptionResult`: Whisper output parsing
   - `WhisperModelInfo`: Model metadata and benchmarking (Lite/Swift/Pro/Elite/Ultra)
   - `TranscriptionHistoryItem`: History panel items with metadata
   - `CustomDictionary`: VoiceShortcuts - custom word/phrase replacements with templates (Medical, Legal, Tech)
   - `UserSession`: Session tracking and metrics
   - `LicensePayload`: License validation structures

4. **Interfaces** (`Interfaces/`): Contract definitions for dependency injection
   - `IRecorder`, `ITranscriber`, `ITextInjector`

5. **UI Components**
   - `MainWindow.xaml`: Main application window with recording status and history panel
   - `SettingsWindow.xaml` / `SettingsWindowNew.xaml`: Settings UI with model selection
   - `DictionaryManagerWindow.xaml`: VoiceShortcuts manager with template presets
   - `LoginWindow.xaml`: Pro license activation window
   - `AnalyticsConsentWindow.xaml`: First-run analytics opt-in dialog (v1.0.17+)
   - `Controls/SimpleModelSelector`: Model selection control
   - `Controls/ModelComparisonControl`: Model comparison and selection UI
   - `Resources/ModernStyles.xaml`: WPF styling resources
   - `Utilities/RelativeTimeConverter`: Converter for "5 mins ago" timestamps
   - `Utilities/TruncateTextConverter`: Converter for text preview truncation

## Whisper Integration

### Available Models (in `whisper/` directory)
- `ggml-tiny.bin` (75MB): **Lite** - Fastest, lowest accuracy - legacy free tier, kept as fallback
- `ggml-small.bin` (466MB): **Pro** ⭐ - **Current free tier default** - balanced accuracy and speed, ships with installer (temporary promotion)
- `ggml-base.bin` (142MB): **Swift** - Fast, good for basic use - Pro tier
- `ggml-medium.bin` (1.5GB): **Elite** - Higher accuracy - Pro tier, optional download from settings
- `ggml-large-v3.bin` (2.9GB): **Ultra** - Highest accuracy - Pro tier, manual download required and resource heavy

### Whisper Process Management
- **PersistentWhisperService is the primary implementation** (WhisperService/WhisperProcessPool are deprecated)
- Warmup process on startup using dummy audio file for reduced first-transcription latency
- Process spawned per transcription with automatic cleanup
- Semaphore-based concurrency control (1 transcription at a time)
- Automatic timeout handling with configurable multiplier
- Temperature optimization (0.2) for better accuracy
- Beam search parameters (beam_size=5, best_of=5)
- Path caching for whisper.exe and model files

### Whisper Command Format
```bash
whisper.exe -m [model] -f [audio.wav] --no-timestamps --language [lang] --temperature 0.2 --beam-size 5 --best-of 5
```

**Language Support**: VoiceLite supports 99 languages via Whisper's multilingual capabilities. Default is English (`en`), but can be configured in settings.

## Licensing & Freemium Model

### Desktop App (Free Tier)
- Desktop client is **100% free** with no usage caps
- **Pro model (466MB)** ships with installer as free tier default (temporary growth promotion)
- Lite model (75MB) kept as legacy fallback for compatibility
- Works completely offline - no license validation required
- No online authentication or tracking
- All core features available (hotkeys, text injection, settings)
- **Accuracy**: ~90-93% with Pro model (vs 80-85% with Lite)

### Web-Based Pro Tier (Optional)
- Stripe subscription managed via voicelite.app ($20/3mo or $99 lifetime)
- Modern Next.js backend validates Pro subscriptions for premium models (Swift, Elite, Ultra)
- Unlocks: Even better accuracy (93-97%), advanced models, priority support, early access
- Backend platform: Next.js 15 + PostgreSQL + Prisma at `voicelite-web/`
- Desktop app validates Pro licenses via Ed25519 cryptographic signatures

### Implementation Details
- **Single Backend Architecture**: Modern Next.js platform at https://voicelite.app
- Free tier: Pro model runs standalone without any server connection (temporary promotion)
- Pro tier: Premium models require license validation via Ed25519 signed licenses
- Recordings processed locally and discarded after transcription (both tiers)
- License keys use Ed25519 cryptographic signing for tamper-proof security
- Desktop app can function fully offline after premium models are downloaded
- License validation includes Certificate Revocation List (CRL) checks

## Key Technical Details

### Dependencies (NuGet Packages)
- `NAudio` (2.2.1): Audio recording and processing
- `NAudio.Vorbis` (1.5.0): OGG audio file support for sound effects
- `H.InputSimulator` (1.2.1): Keyboard/mouse simulation for text injection
- `Hardcodet.NotifyIcon.Wpf` (2.0.1): System tray integration
- `System.Text.Json` (9.0.9): Settings persistence
- `System.Management` (8.0.0): System information
- `BouncyCastle.Cryptography` (2.4.0): License encryption and cryptographic operations

### Test Dependencies (xUnit)
- `xunit` (2.9.2): Test framework
- `Moq` (4.20.70): Mocking framework
- `FluentAssertions` (6.12.0): Assertion library

### Performance Optimizations
- Audio preprocessing with noise gate and AGC
- Whisper process pooling for reduced latency
- Smart text injection (clipboard for long text, typing for short)
- Memory monitoring and cleanup
- Cached model benchmarking
- VAD (Voice Activity Detection) for silence trimming

### Settings & Configuration
- **Settings stored in `%LOCALAPPDATA%\VoiceLite\settings.json`** (Local machine only, does NOT sync)
- **Privacy Note**: Changed from Roaming to Local AppData to prevent transcription history from syncing across PCs
- AppData directory created automatically on first run
- Automatic migration from old Roaming AppData location (one-time, transparent)
- Settings migration from old Program Files location (if exists)
- Default hotkey: Left Alt (customizable)
- Default mode: Push-to-talk (not toggle)
- Auto-paste enabled by default
- Multiple text injection modes (SmartAuto, AlwaysType, AlwaysPaste, PreferType, PreferPaste)
- Sound feedback disabled by default (wood-tap-click.ogg via NAudio.Vorbis)
- **Transcription history**: Configurable max items (default 50), supports pinning, auto-cleanup
- **VoiceShortcuts**: Pattern-based replacements with templates (Medical, Legal, Tech)
- **Analytics**: Opt-in privacy-first analytics with SHA256 anonymous IDs (v1.0.17+), no PII collected

### Error Handling & Logging
- Comprehensive error logging via `ErrorLogger` service
- **Logs stored in `%LOCALAPPDATA%\VoiceLite\logs\voicelite.log`** (Local machine only, does NOT sync)
- Log directory created automatically on first write
- Log rotation at 10MB max size
- Graceful fallbacks for missing models or whisper.exe
- Microphone device detection and switching
- Process crash recovery
- Timeout handling for hung processes
- Special handling for UnauthorizedAccessException with user-friendly messages

## Important Development Notes

### Platform Requirements
- Target Framework: .NET 8.0 Windows
- Requires Visual C++ Runtime 2015-2022 x64
- Windows 10/11 (uses Windows Forms for some features)

### Build Configuration
- Application manifest for DPI awareness (`app.manifest`)
- Icon resources embedded in project (`VoiceLite.ico`)
- Whisper binaries copied to output on build
- Self-contained deployment supported
- Post-build obfuscation in Release mode (ObfuscateRelease.bat)

### Critical Implementation Details
1. **Audio Format**: Must be 16kHz, 16-bit mono WAV for Whisper
2. **Hotkey Registration**: Uses Win32 API, may require admin for some keys
3. **Text Injection**: May trigger antivirus warnings (false positives)
4. **Process Management**: Whisper.exe path relative to executable location
5. **Memory Management**: Automatic cleanup after transcription
6. **Thread Safety**: Recording state protected by lock
7. **Distribution Model**: Licensing removed – desktop client runs fully unlocked
8. **Usage Tracking**: No freemium caps; recordings are processed locally and discarded

### Testing Compatibility
Works across all Windows applications:
- Text editors (Notepad, VS Code, Visual Studio)
- Terminals (CMD, PowerShell, Windows Terminal)
- Browsers (Chrome, Firefox, Edge)
- Communication apps (Discord, Teams, Slack)
- Games (windowed mode)
- Admin-elevated applications

### Performance Targets
- Transcription latency: <200ms after speech stops
- Accuracy on technical terms: 95%+ (git, npm, useState, forEach)
- Idle RAM usage: <100MB
- Active RAM usage: <300MB
- Idle CPU usage: <5%

## Web Application Architecture

The `voicelite-web` directory contains a Next.js 15 application for:
- Marketing landing page (`app/page.tsx`)
- Stripe checkout integration (`app/api/checkout/route.ts`)
- Webhook handling for subscriptions (`app/api/webhook/route.ts`)
- Pro subscription management

### Tech Stack
- Next.js 15.5.4 (React 19)
- TypeScript
- Tailwind CSS v4 (PostCSS-based, no config file)
- Prisma ORM with SQLite (dev) / PostgreSQL (production)
- Stripe for Pro subscription payments
- Resend for transactional emails
- Upstash Redis for rate limiting
- Ed25519 cryptography (@noble/ed25519) for license signing
- Zod for schema validation

## Backend API Architecture

The desktop application communicates with the modern Next.js backend at `voicelite.app` for Pro tier features.

### Desktop App API Integration

**Base URL**: `https://voicelite.app` (hardcoded in Release builds)

**Authentication Endpoints** (Magic Link + OTP):
- `POST /api/auth/request` - Send magic link email to user
- `POST /api/auth/otp` - Verify OTP code and create JWT session
- `POST /api/auth/logout` - Revoke current session

**License Endpoints** (Ed25519 Cryptographic Signatures):
- `GET /api/me` - Get user profile + active licenses
- `POST /api/licenses/activate` - Activate license on device (machine fingerprinting)
- `POST /api/licenses/issue` - Issue cryptographically signed license file
- `GET /api/licenses/crl` - Fetch Certificate Revocation List (CRL)

**Analytics Endpoints** (Privacy-first opt-in):
- `POST /api/analytics/event` - Submit anonymous analytics events (v1.0.17+)

### Backend Technology Stack
- **Framework**: Next.js 15 (App Router) with React 19
- **Database**: PostgreSQL (Supabase) with Prisma ORM
- **Authentication**: Passwordless magic link + JWT sessions
- **Security**: Ed25519 signatures, rate limiting (Upstash Redis), CSRF protection
- **Payments**: Stripe (subscriptions + one-time payments)
- **Email**: Resend for transactional emails
- **Deployment**: Vercel (serverless API routes)

### License Validation Flow

1. **Desktop App** → Fetches signed license via `POST /api/licenses/issue`
2. **Server** → Signs license payload with Ed25519 private key
3. **Desktop App** → Verifies signature using embedded public key (BouncyCastle.Cryptography)
4. **Desktop App** → Checks CRL for revoked licenses via `GET /api/licenses/crl`
5. **Desktop App** → Validates device fingerprint, expiry, grace period locally
6. **Result** → License valid/invalid/expired/revoked (fully offline validation after initial fetch)

### Analytics System (v1.0.17+)

VoiceLite includes a **privacy-first, opt-in analytics system** that respects user privacy while providing insights into app usage.

**Privacy Features**:
- **Opt-in only**: Users see consent dialog on first launch, must explicitly agree
- **Anonymous IDs**: SHA256-hashed machine ID + timestamp (irreversible, no PII)
- **No IP logging**: Backend does not store IP addresses
- **No sensitive data**: No recording content, no file paths, no user names
- **Local control**: Settings stored in `settings.json`, can be disabled anytime
- **Fail-safe**: Analytics failures never break app functionality (silent failures)

**Events Tracked**:
- `APP_LAUNCHED`: App start events (tier, version, OS)
- `TRANSCRIPTION_COMPLETED`: Aggregated daily (count, total words, model used)
- `MODEL_CHANGED`: Model switches (old model → new model)
- `SETTINGS_CHANGED`: Settings modifications (setting name only)
- `ERROR_OCCURRED`: Error types for debugging (no stack traces or paths)
- `PRO_UPGRADE`: Pro tier activations

**Implementation**:
- **Desktop**: `AnalyticsService.cs` sends events to backend API
- **Backend**: `POST /api/analytics/event` stores in PostgreSQL via Prisma
- **UI**: `AnalyticsConsentWindow.xaml` shows transparent consent dialog
- **Settings**: `EnableAnalytics` property (null = not asked, false = opted out, true = opted in)
- **Aggregation**: Transcriptions logged once per day, then every 10 transcriptions to reduce noise

## Deployment & Distribution

### Automated Release Process (Recommended)
**Using GitHub Actions** (preferred method as of v1.0.22):

```bash
# 1. Tag the release
git tag v1.0.22
git push --tags

# 2. GitHub Actions automatically:
#    - Updates version in .csproj and .iss files
#    - Builds Release with dotnet publish
#    - Compiles installer with Inno Setup
#    - Creates GitHub release with installer upload
#    - Takes ~5-7 minutes total

# 3. Release is published at:
#    https://github.com/mikha08-rgb/VoiceLite/releases/tag/v1.0.22

# 4. Update website download link (manual step):
#    Edit voicelite-web/app/page.tsx to point to new version
#    git commit && git push (Vercel auto-deploys)
```

**Workflow file**: `.github/workflows/release.yml`

### Manual Release Process (Legacy)
If GitHub Actions is unavailable:

1. Build Release version: `dotnet publish VoiceLite/VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained`
2. Published files appear in `VoiceLite/VoiceLite/bin/Release/net8.0-windows/win-x64/publish/`
3. Update version in `VoiceLite/VoiceLite/VoiceLite.csproj` (Version, AssemblyVersion, FileVersion)
4. Update version in `VoiceLite/Installer/VoiceLiteSetup_Simple.iss` (AppVersion, OutputBaseFilename)
5. Run Inno Setup compiler: `"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" VoiceLite\Installer\VoiceLiteSetup_Simple.iss`
6. Output: `VoiceLite-Setup-{VERSION}.exe` in root directory
7. Create GitHub release manually with `gh release create`
8. Upload installer to release

### Installer Features
- **Includes Pro model (466MB) + Lite model (75MB)** - temporary growth promotion (v1.0.14+)
- Installer size: ~540MB (up from ~150MB with Lite-only)
- Auto-installs to Program Files
- Creates desktop shortcut
- Uninstaller removes AppData settings
- Version tracking via AppId GUID
- Current installer: `VoiceLite-Setup-1.0.19.exe`

### Distribution Channels
- GitHub Releases (primary)
- Google Drive for large downloads (~540MB with Small model)
- Direct download from voicelite.app

## Known Issues & Limitations

1. **VCRUNTIME140_1.dll Error**: Requires Visual C++ Redistributable 2015-2022 x64 ([Download](https://aka.ms/vs/17/release/vc_redist.x64.exe))
2. **Antivirus False Positives**: Global hotkeys and text injection may trigger warnings
3. **Windows Defender**: Files may need to be unblocked (right-click → Properties → Unblock)
4. **First Run Diagnostics**: StartupDiagnostics checks and auto-fixes common issues

## Recent Cleanup Activities

The project underwent significant cleanup in October 2025:
- **Legacy code removal**: 40GB+ freed (build outputs, old releases, dead code)
- **Architecture simplification**: Removed legacy license server, unified backend at voicelite.app
- **Documentation consolidation**: Historical docs moved to archives
- See [CLEANUP_COMPLETE.md](CLEANUP_COMPLETE.md) and [DEAD_CODE_ANALYSIS.md](DEAD_CODE_ANALYSIS.md) for details

## Development Troubleshooting

### Common Build Issues
- **Missing whisper.exe**: Ensure `whisper/whisper.exe` exists in project directory
- **Build fails on Services/**: Clean solution (`dotnet clean`) then rebuild
- **Test project won't load**: Verify xUnit, Moq, FluentAssertions packages are restored

### Common Runtime Issues
- **Settings not persisting**: Check AppData write permissions (`%APPDATA%\VoiceLite\`)
- **UnauthorizedAccessException on logs**: ErrorLogger auto-creates `%APPDATA%\VoiceLite\logs\`
- **Whisper process hangs**: Check timeout multiplier in settings (default 2.0x)
- **No text injection**: Verify InputSimulator has proper permissions

### Web/License Server Issues
- **Prisma migrations fail**: Check `voicelite-web/prisma/schema.prisma` is valid
- **Stripe webhook errors**: Verify webhook secret in environment variables
- **License validation fails**: Check API_KEY matches between desktop app and server
- **CORS errors**: Verify ALLOWED_ORIGINS environment variable

## Version Information

- **Desktop App**: v1.0.24 (current release)
- **Web App**: v0.1.0 (see voicelite-web/package.json)

## Changelog Highlights

### v1.0.24 (Current Desktop Release)
- **Text Formatting Feature**: Added comprehensive post-processing customization in Settings → "Text Formatting" tab
  - **Capitalization controls**: Toggle first letter, after periods, after ?/! independently
  - **Ending punctuation**: Choose default (period/question/exclamation) + smart question detection
  - **Filler word removal**: 5 intensity levels (None/Light/Moderate/Aggressive/Custom) with 5 built-in categories
    - Hesitations: "um", "uh", "er", "ah", "hmm"
    - Verbal tics: "like", "you know", "I mean", "I guess", "I think"
    - Qualifiers: "sort of", "kind of", "pretty much"
    - Intensifiers: "literally", "actually", "honestly", "seriously"
    - Transitions: "so yeah", "anyway", "well"
  - **Contractions**: Expand ("don't" → "do not"), Contract ("do not" → "don't"), or Leave as-is
  - **Grammar fixes**: Homophones (their/there/they're), double negatives, subject-verb agreement
  - **Quick presets**: Professional (all corrections), Code (preserve casing), Casual (light cleanup)
  - **Live preview**: Real-time before/after comparison with editable sample text
- **Code quality**: Fixed circular reference in settings serialization, improved null safety
- **Performance**: Post-processing adds <50ms latency (negligible impact)

### v1.0.19
- **CRITICAL FIX**: History tracking now works correctly - new transcriptions appear in history panel
- **Privacy Enhancement**: Old Roaming AppData folder deleted after migration (prevents history syncing across PCs via Microsoft account)
- **Privacy First**: Transcription history cleared during migration from old versions (sensitive data not migrated)
- **Removed Legacy Fallback**: No more silent degradation to lower-quality models - fail-fast with clear error messages
- **Improved Error Messages**: Clear "Please reinstall VoiceLite" errors instead of silent fallbacks to Tiny model
- **Default Model Updated**: Changed default from Tiny (75MB) → Small (466MB) in all locations
- **Code Quality**: Removed ~30 lines of complex fallback logic, fixed misleading comments

### v1.0.18 (Previous Desktop Release)
- **CRITICAL FIX**: History tracking now works correctly - new transcriptions appear in history panel
- **Privacy Enhancement**: Old Roaming AppData folder deleted after migration (prevents history syncing across PCs via Microsoft account)
- **Privacy First**: Transcription history cleared during migration from old versions (sensitive data not migrated)
- **Removed Legacy Fallback**: No more silent degradation to lower-quality models - fail-fast with clear error messages
- **Improved Error Messages**: Clear "Please reinstall VoiceLite" errors instead of silent fallbacks to Tiny model
- **Default Model Updated**: Changed default from Tiny (75MB) → Small (466MB) in all locations
- **Code Quality**: Removed ~30 lines of complex fallback logic, fixed misleading comments

### v1.0.18
- **Privacy Fixes**: Improved analytics consent flow and data handling
- **RecordingCoordinator Refactor**: New service to orchestrate recording workflow and state management
- **Comprehensive Unit Tests**: Added extensive test coverage for RecordingCoordinator
- **Code Quality**: Improved separation of concerns in recording logic

### v1.0.17
- **Analytics System**: Add privacy-first opt-in analytics with SHA256 anonymous user IDs
- **Analytics Tracking**: Track app launches, transcriptions (aggregated daily), model changes, settings changes
- **Privacy Enhancements**: No PII collection, no IP logging, full transparency in consent dialog
- **Bug Fixes**: Fix UUID generation for analytics events, improve daily transcription aggregation
- **Code Quality**: Wire up analytics tracking throughout desktop app

### v1.0.16
- **VoiceShortcuts Rebrand**: Renamed custom dictionary feature to "VoiceShortcuts" for clarity
- **Model Naming Updates**: Improved model naming consistency across UI
- **Minor fixes**: Documentation and UI polish

### v1.0.15
- **Multi-Language Support**: Added support for 99 languages via Whisper AI
- **Major UX improvements**: Improved user experience across the application
- **Performance optimizations**: Better resource management and speed
- **Stability enhancements**: Bug fixes and reliability improvements

### v1.0.14 (Growth Promotion)
- **Free Tier Upgrade**: Pro model (466MB) now ships as free tier default instead of Lite (75MB)
- **Accuracy Boost**: Free users now get ~90-93% accuracy (up from 80-85%)
- **Strategic**: Temporary promotion to improve first impressions during growth phase
- **Installer Size**: ~540MB (up from ~150MB) - includes Pro + Lite models
- **Fallback Safety**: Lite model kept as legacy fallback for compatibility
- **Code Changes**: Updated Settings.cs, SettingsWindow.xaml.cs, PersistentWhisperService.cs, installer script

### v1.0.13
- **Critical Fix**: Fixed permission errors on launch - temp files now use AppData instead of Program Files
- **Warmup Fix**: Whisper warmup process now works without admin permissions
- **Diagnostics Fix**: Removed Program Files write test from permission checks (expected to be read-only)
- **Stability**: Zero permission errors, all file operations now use appropriate directories

### v1.0.12
- **UX Improvement**: Added keyboard shortcuts to VoiceShortcuts Manager (Ctrl+S to save, Escape to close)
- **Transparency**: AI model name now displayed on main window (Lite/Swift/Pro/Elite/Ultra)
- **History Control**: Added "Clear All" button to delete all history items including pinned ones
- **Quality**: All three quick wins implemented with zero breaking changes

### v1.0.5
- **Settings Location**: Fixed critical bug where settings were saved to Program Files (protected directory) instead of AppData
- **Error Logs Location**: Fixed error logs being written to Program Files. Now correctly uses `%APPDATA%\VoiceLite\logs\`
- **First-Run Experience**: Added automatic AppData directory creation and settings migration
- **Installer**: Pre-creates AppData directories during installation to prevent first-run issues
- **Service Refactor**: Migrated to PersistentWhisperService as primary transcription service

### Architecture Changes
- **Deprecated Services**: WhisperService, WhisperProcessPool, ModelEncryptionService (deleted from codebase)
- **Active Services**: PersistentWhisperService is now the sole Whisper integration point

---

## Custom Agents for VoiceLite

VoiceLite uses a comprehensive system of 20+ custom agents to automate quality gates, code reviews, security audits, and documentation maintenance. These agents are defined in [AGENTS.md](AGENTS.md) and provide intelligent assistance throughout the development lifecycle.

### Quick Reference: When to Use Agents

**Daily Development**:
- **Before commits**: `"Run pre-commit-workflow"` - Fast quality gates (<10s)
- **After modifying Services**: File-specific validators auto-trigger (<30s)
- **Code review**: `"Use code-reviewer to review all modified files"` (~45s)

**Weekly Maintenance**:
- **Security audit**: `"Use security-audit-workflow"` (~5min)
- **Dependency updates**: `"Use dependency-upgrade-advisor to check for updates"` (~3min)

**Release Workflow**:
- **Production release**: `"Use ship-to-production-workflow to prepare v{version}"` (~15min)
  - Runs 6 phases: Code quality → Security → Tests → Legal → Build → Deployment
  - Blocks on critical issues, warns on medium/low issues
  - Generates installer and changelog

**Domain Expertise**:
- **Whisper accuracy issues**: `"Use whisper-model-expert to debug {issue}"`
- **WPF/XAML questions**: `"Use wpf-ui-expert to review {component}"`
- **Stripe integration**: `"Use stripe-integration-expert to explain {topic}"`

**Documentation**:
- **Sync CLAUDE.md**: `"Use claude-md-sync-agent to update CLAUDE.md"` (<1min)
- **Generate API docs**: `"Use api-docs-generator"` (~2min)

### Agent Categories

**1. Workflow Orchestrators** (4 agents)
- `ship-to-production-workflow`: Complete CI/CD pipeline from code review to release
- `pre-commit-workflow`: Fast quality gates before git commits (<10s)
- `security-audit-workflow`: Comprehensive security review (~5min)
- `performance-optimization-workflow`: Systematic bottleneck identification

**2. File-Specific Validators** (8 agents)
Auto-trigger when specific files are modified:
- `whisper-service-guardian`: Validates PersistentWhisperService.cs changes
- `mainwindow-coordinator-guard`: Validates MainWindow.xaml.cs (thread safety, null checks)
- `audio-recorder-validator`: Validates AudioRecorder.cs (audio format, disposal)
- `stripe-checkout-guardian`: Validates checkout/route.ts (security, pricing)
- `webhook-security-enforcer`: Validates webhook/route.ts (signature verification)
- `settings-persistence-guard`: Validates Settings.cs (AppData paths)
- `legal-docs-sync-validator`: Validates privacy/terms consistency
- `api-route-security-scanner`: Validates all API routes (auth, injection, errors)

**3. Domain Experts** (5 agents)
- `whisper-model-expert`: Whisper AI troubleshooting (accuracy, models, parameters)
- `wpf-ui-expert`: WPF/XAML patterns (MVVM, thread safety, disposal)
- `stripe-integration-expert`: Stripe payments (webhooks, testing, errors)
- `test-coverage-enforcer`: Test coverage analysis and test generation
- `dependency-upgrade-advisor`: NuGet/npm dependency management and CVE scanning

**4. Documentation Agents** (3 agents)
- `claude-md-sync-agent`: Keeps CLAUDE.md synchronized with codebase
- `readme-generator`: Auto-generates README.md from codebase state
- `api-docs-generator`: Creates OpenAPI specs and API documentation

### Example Workflow: Feature Development

```bash
# 1. Develop feature (modify Services/AudioRecorder.cs)
# → audio-recorder-validator auto-triggers

# 2. Before committing
"Run pre-commit-workflow"
# → Checks secrets, localhost URLs, debug code

# 3. Review code quality
"Use code-reviewer to review all files I changed today"
# → Scores code quality, suggests improvements

# 4. Add tests
"Use test-coverage-enforcer to suggest tests for AudioRecorder.cs"
# → Generates test stubs with expected cases

# 5. Prepare release
"Use ship-to-production-workflow to prepare v1.0.12"
# → 6-phase pipeline, generates installer + changelog

# 6. Update docs
"Use claude-md-sync-agent to update CLAUDE.md"
# → Syncs service list, dependencies, version numbers
```

### Additional Resources

- **[AGENTS.md](AGENTS.md)** - Full agent definitions with detailed instructions (1385 lines)
- **[WORKFLOWS.md](WORKFLOWS.md)** - Comprehensive workflow guide with real-world examples
- **[AGENT-EXAMPLES.md](AGENT-EXAMPLES.md)** - Copy-paste examples and expected outputs

### Agent Quality Standards

All agents follow strict quality standards:
- **Single responsibility** - Each agent has one clear purpose
- **Explicit triggers** - Clear conditions for when to use each agent
- **Measurable success** - Pass/fail criteria with severity levels (CRITICAL/HIGH/MEDIUM/LOW)
- **Actionable output** - Specific file:line references and fixes
- **Safety checks** - Validates before making changes
- **Performance budgets** - File validators <30s, workflows <5min

### Integration with Development Workflow

Agents integrate seamlessly with daily development:
1. **Auto-validation**: File-specific agents auto-trigger when you modify critical files
2. **Manual invocation**: Run agents via Claude Code chat (e.g., "Run pre-commit-workflow")
3. **Chaining**: Combine agents for complex workflows (e.g., ship-to-production-workflow spawns 10+ agents)
4. **Error handling**: Agents use fail-fast for critical issues, collect warnings for minor issues

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

<critical_rules>
When working with VoiceLite code, you MUST follow these patterns to ensure stability and prevent memory leaks, race conditions, and UI freezes. These rules exist because violations cause production bugs that are difficult to debug.

### Thread Safety & Resource Management

**RULE 1: ALWAYS dispose IDisposable resources**
- WHY: Undisposed resources cause memory leaks that accumulate during long-running sessions
- WHEN: Every time you create objects implementing IDisposable (streams, processes, audio devices)
- VERIFICATION: Check disposal tests pass before committing any code

<example_correct>
using (var recorder = new WaveInEvent())
{
    // Use recorder
} // Automatically disposed
</example_correct>

<example_incorrect>
var recorder = new WaveInEvent();
// Use recorder - NEVER DISPOSED, causes memory leak
</example_incorrect>

**RULE 2: ALWAYS use `lock` for shared recording state**
- WHY: Multiple threads (UI, hotkey handler, recording timer) access state simultaneously
- WHEN: Any access to recording state variables (isRecording, currentRecording, etc.)
- CONSEQUENCE: Race conditions cause duplicate recordings or state corruption

<example_correct>
lock (_recordingLock)
{
    if (!_isRecording) return;
    _isRecording = false;
}
</example_correct>

<example_incorrect>
if (!_isRecording) return;  // Race condition - another thread could modify _isRecording between check and update
_isRecording = false;
</example_incorrect>

**RULE 3: ALWAYS use `Dispatcher.Invoke()` for UI updates from background threads**
- WHY: WPF enforces UI thread affinity - cross-thread access throws InvalidOperationException
- WHEN: Updating any UI element from Whisper callback, audio event handler, or async task
- PATTERN: Wrap ALL UI updates in Dispatcher.Invoke()

<example_correct>
// In background thread
Dispatcher.Invoke(() =>
{
    StatusLabel.Content = "Processing...";
    ProgressBar.Value = 50;
});
</example_correct>

<example_incorrect>
// In background thread - CRASHES with InvalidOperationException
StatusLabel.Content = "Processing...";
</example_incorrect>

**RULE 4: NEVER skip disposal tests**
- WHY: They prevent memory leaks that aren't obvious during development
- WHEN: Before every commit that touches Services/ code
- VERIFICATION: Run full test suite, verify all disposal tests pass

### Audio & Whisper Processing

**RULE 5: Audio format MUST be 16kHz, 16-bit mono WAV**
- WHY: Whisper.cpp is trained on this format - other formats reduce accuracy
- WHY NOT preprocess: Noise reduction/normalization introduces artifacts worse than background noise
- ENFORCEMENT: NAudio configuration in AudioRecorder service hardcodes this format

<example_correct>
var waveFormat = new WaveFormat(16000, 16, 1); // 16kHz, 16-bit, mono
</example_correct>

<example_incorrect>
var waveFormat = new WaveFormat(44100, 16, 2); // 44.1kHz stereo - reduces accuracy
</example_incorrect>

**RULE 6: One whisper.exe subprocess per transcription**
- WHY: Whisper.cpp doesn't support concurrent processing - causes crashes
- CLEANUP: ALWAYS call Process.Kill() on completion/error/timeout
- CONSEQUENCE: Zombie processes consume memory and interfere with future transcriptions

<example_correct>
var process = Process.Start(whisperPath, args);
try
{
    process.WaitForExit(timeout);
    return ParseOutput(process.StandardOutput);
}
finally
{
    if (!process.HasExited) process.Kill();
}
</example_correct>

**RULE 7: Whisper command format (DO NOT MODIFY)**
```bash
whisper.exe -m [model] -f [audio.wav] --no-timestamps --language en \
  --temperature 0.2 --beam-size 1 --best-of 1
```
- WHY `--no-timestamps`: Text injection doesn't need timing, removes noise
- WHY `--temperature 0.2`: Low temperature = more deterministic/accurate output
- WHY `--beam-size 1`: Greedy decoding is 5x faster with minimal accuracy loss for short clips
- Model locations: `whisper/` directory (ggml-*.bin files)

### Text Injection

**RULE 8: NEVER block UI thread during text injection**
- WHY: Long text (>100 chars) can take 1-3 seconds to type, freezing the app
- PATTERN: Use SmartAuto mode - clipboard for >100 chars, typing for shorter text
- IMPLEMENTATION: TextInjector service handles this automatically

**RULE 9: SmartAuto mode decision logic**
```csharp
if (text.Length > 100)
{
    UseClipboard(text);  // Fast, no UI freeze
}
else
{
    UseKeyboardSimulation(text);  // More reliable for short text, preserves formatting
}
```

**Known Issue**: Antivirus software may flag H.InputSimulator (global keyboard hooks) as suspicious behavior

### WPF Patterns

**XAML styling**: ALWAYS use `ModernStyles.xaml` for visual consistency
**Converters**: Use existing converters - `RelativeTimeConverter` ("5 mins ago"), `TruncateTextConverter` (long text)
**Icons**: Stored in project root - reference via relative path

### Testing Requirements

**BEFORE EVERY COMMIT:**
1. Run: `dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj`
2. Verify: ALL ~200 tests pass (no skips allowed)
3. Check: Coverage ≥75% overall, ≥80% Services/ (run with `--collect:"XPlat Code Coverage"`)
4. Verify: ALL disposal tests pass (critical for memory safety)

**Frameworks**: xUnit (test runner), Moq (mocking), FluentAssertions (readable assertions)

**Why disposal tests matter**: Memory leaks accumulate during long recording sessions. A leak of 5MB per recording becomes 500MB after 100 recordings.
</critical_rules>

## Core Services (VoiceLite/Services/)

These 9 services form the application backbone. When modifying functionality, identify which service owns the logic:

1. **AudioRecorder** - NAudio-based recording (16kHz mono WAV, no preprocessing)
2. **PersistentWhisperService** - Manages whisper.exe subprocess lifecycle
3. **TextInjector** - Injects transcribed text via H.InputSimulator (SmartAuto/Type/Paste modes)
4. **HotkeyManager** - Registers global hotkeys via Win32 API
5. **SystemTrayManager** - System tray icon and context menu
6. **TranscriptionHistoryService** - Manages history with pin/delete/copy functionality
7. **ErrorLogger** - Centralized error logging to `%LOCALAPPDATA%\VoiceLite\logs\`
8. **LicenseService** - Pro license validation via HTTP to voicelite.app/api/licenses/validate
9. **ProFeatureService** - Feature gating based on license status (exposes `IsProUser` property for UI visibility)

## Pro Features System

**License Flow**:
1. User pays $20 via Stripe → Backend creates License record
2. User enters license key in Settings → Desktop calls `/api/licenses/validate`
3. LicenseService caches validation result locally
4. ProFeatureService reads cached status → Gates UI via Visibility properties

**Free Tier**: Tiny model only (ggml-tiny.bin, 75MB, bundled in installer)

**Pro Tier** ($20 one-time):
- Small model (ggml-small.bin, 466MB) - Included in source repository
- Base/Medium/Large models - Downloaded via AI Models tab (requires Pro)
- 3 device activations per license
- Future: Voice Shortcuts, Export History, Custom Dictionary

**Adding New Pro Features** (3-step pattern):

<example_implementation>
// Step 1: ProFeatureService.cs - Add visibility property
public Visibility VoiceShortcutsTabVisibility => IsProUser
    ? Visibility.Visible
    : Visibility.Collapsed;

// Step 2: SettingsWindowNew.xaml - Add gated tab
<TabItem Header="Voice Shortcuts" Name="VoiceShortcutsTab">
    <!-- Feature UI here -->
</TabItem>

// Step 3: SettingsWindowNew.xaml.cs - Bind visibility in constructor
VoiceShortcutsTab.Visibility = proFeatureService.VoiceShortcutsTabVisibility;
</example_implementation>

**WHY this pattern**: Centralized in ProFeatureService = single source of truth, prevents bypass attempts

## Web API (voicelite-web/app/api/)

**Key Endpoints**:
- `POST /api/licenses/validate` - Validates license key, returns status (rate limited: 5/hour/IP to prevent brute force)
- `POST /api/checkout` - Creates Stripe checkout session for $20 payment
- `POST /api/webhook` - Stripe webhook handler (creates License record on successful payment)
- `POST /api/feedback/submit` - User feedback collection (rate limited via Upstash Redis)
- `POST /api/download` - Tracks model/installer downloads

**Database Models** (Prisma schema):
- `License` - Email-based licensing (no user accounts), linked to Stripe payment
- `LicenseActivation` - Tracks device activations (3-device limit per license)
- `LicenseEvent` - Audit trail for all license operations
- `Feedback` - User feedback with priority/status fields

## Common Pitfalls - Anti-Patterns to Avoid

<anti_patterns>
When you encounter these patterns, flag them as bugs and fix immediately:

1. **DO NOT preprocess audio** (noise reduction, normalization, resampling)
   - WHY: Whisper is trained on raw audio - preprocessing reduces accuracy more than it helps
   - CORRECT: Use raw 16kHz mono WAV directly from NAudio

2. **DO NOT skip test runs before commits**
   - WHY: 200 tests exist to catch regressions - skipping them allows bugs into production
   - CORRECT: Run `dotnet test` and verify all pass before every commit

3. **DO NOT forget disposal** - IDisposable resources MUST be cleaned up
   - WHY: Memory leaks accumulate and cause crashes in long-running sessions
   - CORRECT: Use `using` statements or explicit try/finally with Dispose()

4. **DO NOT update UI from background threads without Dispatcher.Invoke()**
   - WHY: WPF throws InvalidOperationException on cross-thread UI access
   - CORRECT: Wrap ALL UI updates in `Dispatcher.Invoke(() => { ... })`

5. **DO NOT commit without checking coverage**
   - WHY: Coverage targets (≥75% overall, ≥80% Services/) prevent untested code paths
   - CORRECT: Run `dotnet test --collect:"XPlat Code Coverage"` and verify thresholds met

6. **DO NOT bundle large models in installer**
   - WHY: Base (142MB), Medium (1.5GB), Large (2.9GB) would make installer too large
   - CORRECT: Only Tiny (75MB) is bundled; Pro models download via AI Models tab

7. **DO NOT skip license validation for Pro features**
   - WHY: Revenue protection - prevents freemium bypass
   - CORRECT: ALWAYS gate Pro features via `ProFeatureService.IsProUser` property

8. **DO NOT leave zombie whisper.exe processes**
   - WHY: Each process consumes ~200MB RAM - zombies cause memory exhaustion
   - CORRECT: ALWAYS call `Process.Kill()` in finally block or using statement
</anti_patterns>

## Performance Targets

When implementing features, maintain these metrics (measure via Task Manager + test recordings):
- **Transcription latency**: <200ms from speech stop to text injection
- **Idle RAM**: <100MB (app sitting in tray)
- **Active RAM**: <300MB (during recording + transcription)
- **Idle CPU**: <5% (polling only)
- **Accuracy**: 95%+ on technical terms (git, npm, useState, etc.) with Small+ models

## Distribution

**Installer**: `VoiceLite-Setup-{VERSION}.exe` (~100-150MB, includes only Tiny model)
**Process**: GitHub Actions workflow (triggered by git tag push) → Version bump → Build → Create installer → GitHub release (~5-7 min)
**Channels**: GitHub Releases (primary), Google Drive (mirror for reliability)

## Dependencies

**Desktop** (NuGet packages):
- NAudio 2.2.1 - Audio recording
- H.InputSimulator 1.2.1 - Keyboard/mouse simulation
- Hardcodet.NotifyIcon.Wpf 2.0.1 - System tray icon
- System.Text.Json 9.0.9 - JSON serialization (settings)
- System.Management 8.0.0 - System info queries

**Web** (npm packages):
- Next.js 15.5.4 + React 19.2.0 - Web framework
- Prisma 6.1.0 + @prisma/client - Database ORM
- Stripe 18.5.0 - Payment processing
- @upstash/redis - Rate limiting (serverless Redis)

**System Requirements**:
- Windows 10/11 x64
- Visual C++ Runtime 2015-2022 x64 (bundled in installer, auto-installs if missing)

---

**For historical context and detailed changelogs**: See git log. This file contains only current, actionable development information optimized for Claude Code workflow.

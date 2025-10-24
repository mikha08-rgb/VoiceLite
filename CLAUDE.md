# CLAUDE.md

VoiceLite: Windows speech-to-text app (MVP phase - ship fast, iterate quickly)
**Stack**: .NET 8.0 WPF + Next.js backend | **Model**: Free (Tiny) + Pro ($20, unlocks 4 models)

## Project Structure

```
VoiceLite/VoiceLite/          # Desktop (.NET)
  ├── Services/               # 9 services (but not strict - refactor freely)
  ├── Views/                  # WPF UI
  └── whisper/                # Models (ggml-*.bin)
VoiceLite/VoiceLite.Tests/    # Tests (~200, aim for >60% coverage)
voicelite-web/                # Next.js + Prisma + Stripe
```

**User data**: `%LOCALAPPDATA%\VoiceLite\`

## Quick Commands

```bash
# Build & Run
dotnet build VoiceLite/VoiceLite.sln
dotnet run --project VoiceLite/VoiceLite/VoiceLite.csproj

# Test (run before major commits - doesn't have to be every tiny change)
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj

# Release
dotnet publish VoiceLite/VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained
git tag v1.0.XX && git push --tags  # Triggers GitHub Actions build
```

## Non-Negotiable Rules (Prevent Crashes Only)

### Memory & Threading
1. **Dispose IDisposable resources** - WaveInEvent, FileStream, Process all leak memory. Use `using` statements
2. **Lock recording state** - `lock (_recordingLock)` before touching `_isRecording`. Race conditions cause duplicate recordings
3. **Dispatcher.Invoke() for UI updates** - WPF crashes on cross-thread access

### Pro License (Revenue Protection)
4. **Gate Pro features via ProFeatureService.IsProUser** - Free users get Tiny model only. See ProFeatureService.cs pattern

### That's It
Everything else is negotiable. Experiment freely.

## Current Defaults (Feel Free to Change)

**Audio**: 16kHz mono WAV works well with Whisper (see AudioRecorder.cs). If you want to try preprocessing/different formats, go ahead - just test accuracy

**Whisper command**:
```bash
whisper.exe -m [model] -f [audio.wav] --no-timestamps --language en --temperature 0.2 --beam-size 1
```
beam-size=1 is 5x faster than beam-size=5. Tune if needed for your use case

**Text injection**: SmartAuto = clipboard for >100 chars, typing for short (TextInjector.cs). Works well but not locked

**Tests**: Aim for >60% coverage on Services/. Disposal tests catch memory leaks - run those before releases

## 9 Core Services

Loose ownership - refactor if architecture needs to change:

1. **AudioRecorder** - NAudio recording
2. **PersistentWhisperService** - whisper.exe subprocess
3. **TextInjector** - InputSimulator text injection
4. **HotkeyManager** - Global hotkeys
5. **SystemTrayManager** - Tray icon
6. **TranscriptionHistoryService** - History UI
7. **ErrorLogger** - Logging
8. **LicenseService** - License validation
9. **ProFeatureService** - Feature gating

## Common Gotchas

- **Undisposed resources** → Memory leaks (5MB/recording adds up)
- **No Dispatcher.Invoke()** → WPF crashes
- **Unlocked state access** → Race conditions
- **Bypassing ProFeatureService** → Revenue loss
- **Zombie whisper.exe** → RAM bloat (200MB each)

## MVP Philosophy

**Shipping > Perfection**
- It's okay to skip tests for quick experiments
- Technical debt is fine if it validates assumptions faster
- Refactor when you have users, not before

**When to Be Careful**
- Memory leaks (hard to debug in production)
- Thread safety (crashes lose user trust)
- License bypass (loses revenue)

**When to Move Fast**
- Audio format experiments
- UI/UX changes
- New features
- Architecture pivots

---

**In doubt?** Ask "Does this risk crashes or revenue loss?"
- Yes → Follow the rule
- No → Ship it and iterate

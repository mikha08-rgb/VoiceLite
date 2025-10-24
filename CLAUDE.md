# CLAUDE.md

VoiceLite: Windows speech-to-text app (MVP phase - ship fast, iterate quickly)
**Stack**: .NET 8.0 WPF + Next.js backend | **Model**: Free (Tiny) + Pro ($20, unlocks 4 models)

## Project Structure

```
VoiceLite/VoiceLite/          # Desktop (.NET)
  ├── Services/               # 9 services (refactor if needed)
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

# Test (run before releases or risky changes)
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj

# Release
dotnet publish VoiceLite/VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained
git tag v1.0.XX && git push --tags  # Triggers GitHub Actions build
```

## Non-Negotiable Rules (Prevent Crashes & Revenue Loss)

### Memory & Threading (Crash Prevention)
1. **Dispose IDisposable resources** - WaveInEvent, FileStream, Process all leak memory. Use `using` statements
2. **Lock recording state** - `lock (_recordingLock)` before touching `_isRecording`. Race conditions cause duplicate recordings
3. **Dispatcher.Invoke() for UI updates** - WPF crashes on cross-thread access

### Pro License (Revenue Protection)
4. **Gate Pro features via ProFeatureService.IsProUser** - Free users get Tiny model only. See ProFeatureService.cs pattern

## Important Context (Read Before Changing These)

**Audio format**: Currently 16kHz mono WAV (see AudioRecorder.cs)
- **Why**: Whisper is trained on this format
- **If you want to change**: Test accuracy with 10+ real samples first. Preprocessing often reduces accuracy despite sounding better
- **Red flags**: If I suggest "noise reduction" or "audio enhancement" without testing data, push back on me

**Whisper command**: Currently using beam-size=1 (see PersistentWhisperService.cs)
- **Why**: 5x faster than beam-size=5 with minimal accuracy loss
- **Trade-off**: beam-size=5 = better accuracy but 5x slower
- **If you want to change**: Consider the user experience - will they wait 10 seconds instead of 2 seconds?

**Memory limits**: Desktop app targets <300MB RAM during active use
- **Why**: Users keep this running 24/7 in system tray
- **Red flags**: If I suggest caching "all history in memory" or "preload all models", ask about memory impact
- **Good question to ask me**: "How much RAM will this use after 100 recordings?"

**Testing philosophy**: Run tests before releases and major changes, skip for tiny UI tweaks
- **When to test**: New features, refactors, anything touching Services/
- **When to skip**: Button color changes, text updates, non-critical UI polish

## How to Work With Claude

### When Claude Should Push Back on You

**You're non-technical, so I should warn you if your request will:**
- ❌ Cause memory leaks (caching too much data)
- ❌ Create performance issues (nested loops, heavy processing)
- ❌ Break existing features (changing core audio/Whisper logic without testing)
- ❌ Add complexity without clear user value

**Example - You ask:** "Let's preload all 5 Whisper models at startup for instant switching"
**I should say:** "⚠️ That's 5GB of RAM. Most users have 8GB total - their computer would freeze. Instead: load models on-demand (2 second delay when switching). Want me to implement that?"

### When Claude Should Just Build It

**Low-risk changes I should implement without fuss:**
- ✅ UI text/color/layout changes
- ✅ Adding settings options
- ✅ New UI features (buttons, tabs, dialogs)
- ✅ Export/import functionality
- ✅ Improving error messages

**Example - You ask:** "Add a dark mode toggle"
**I should say:** "Quick implementation: [... code ...]. Low risk, ships it."

### Decision Framework

**Before implementing your request, I should ask myself:**

1. **Memory impact?** Will this leak or bloat RAM?
   - Yes → Explain the problem + suggest alternative
   - No → Build it

2. **Performance impact?** Will this slow down the app noticeably?
   - Yes → Explain + ask if trade-off is worth it
   - No → Build it

3. **Breaks core functionality?** Audio, Whisper, licensing?
   - Yes → Explain risk + suggest testing approach
   - No → Build it

4. **Revenue risk?** Could free users get Pro features?
   - Yes → Block + explain
   - No → Build it

If all answers are "No" → Build it fast, iterate later.

## 9 Core Services

Loose ownership - refactor if needed, but here's current responsibility:

1. **AudioRecorder** - NAudio recording
2. **PersistentWhisperService** - whisper.exe subprocess
3. **TextInjector** - InputSimulator text injection
4. **HotkeyManager** - Global hotkeys
5. **SystemTrayManager** - Tray icon
6. **TranscriptionHistoryService** - History UI
7. **ErrorLogger** - Logging
8. **LicenseService** - License validation
9. **ProFeatureService** - Feature gating

## Common Gotchas (Warn User About These)

- **Undisposed resources** → Memory leaks (5MB/recording adds up fast)
- **No Dispatcher.Invoke()** → App crashes
- **Unlocked state access** → Duplicate recordings, weird bugs
- **Bypassing ProFeatureService** → Free users get Pro features = revenue loss
- **Zombie whisper.exe** → RAM bloat (200MB each process)
- **Caching too much data** → Memory bloat (ask "how much RAM after 1000 uses?")
- **Synchronous I/O on UI thread** → App freezes (file operations, network calls)

## MVP Philosophy

**Ship fast BUT warn about technical debt that causes:**
- Production crashes
- Memory leaks
- Performance degradation
- Revenue loss

**Example balance:**
- ✅ User: "Add export to CSV" → Build it fast
- ⚠️ User: "Cache all transcriptions in RAM" → "That'll use 50MB+ after 10k recordings. Use SQLite instead?"
- ✅ User: "Skip tests for this button" → Build it
- ⚠️ User: "Skip tests for new audio recording logic" → "Audio bugs are hard to debug. Run disposal tests at least?"

---

**Key instruction for Claude:** User is non-technical. Explain trade-offs before implementing. Push back constructively on ideas that risk crashes, memory leaks, or bad UX. Suggest better alternatives. Don't just say "yes" to everything.

# Quick Start Guide for New Chat

## Context Transfer Complete ✓

All architecture review findings are saved in:
**`ARCHITECTURE_REVIEW_2025.md`**

---

## To Resume in New Chat

Copy-paste this prompt:

```
I'm working on fixing performance and reliability issues in VoiceLite (C# WPF app).

I have a comprehensive architecture review at:
ARCHITECTURE_REVIEW_2025.md

Please read this file and confirm you understand:
1. The 6 critical problems (P1-P6)
2. The 4-week fix plan
3. Week 1 Day 1-2 tasks (Dispatcher.Invoke optimization)

Then give me a detailed execution plan for Day 1-2 with:
- Specific files and line numbers to change
- Code before/after examples
- Testing strategy
- Success criteria

Ready to start fixing!
```

---

## What's in the Architecture Review

### Problems Identified:
1. **P1: Async/Await Misuse** (45 async void, 86 Dispatcher calls) → 70% of sluggishness
2. **P2: State Desync** (3 isRecording flags) → 50% of reliability bugs
3. **P3: Audio Late Callbacks** → "ghost text" corruption
4. **P4: 3,140-line God Object** (MainWindow) → unmaintainable
5. **P5: Clipboard Race Conditions** → data loss
6. **P6: Process Zombies** → memory leaks (300-1000MB)

### 4-Week Fix Plan:
- **Week 1**: Critical fixes (async void, state machine, app hang)
- **Week 2-3**: MVVM refactor, pipeline pattern, split Settings
- **Week 4**: Performance (hotkey hook, SQLite history)

### Expected Results:
- Startup: 5-10s → <3s
- Transcription lag: 500-1000ms → <200ms
- App close: 5-30s hang → <1s
- Bug reduction: 60-70%

---

## Files Already Created

1. ✅ `ARCHITECTURE_REVIEW_2025.md` - Full review (600+ lines)
2. ✅ `START_HERE_NEW_CHAT.md` - This file (quick reference)

---

## Priority Files to Fix (Week 1)

From highest to lowest impact:

1. **MainWindow.xaml.cs** (3,140 lines)
   - Lines 1567-1800: OnTranscriptionCompleted handler
   - Lines 1180-1310: Recording state handlers

2. **RecordingCoordinator.cs** (617 lines)
   - Lines 544-592: Dispose() with spin-wait

3. **AudioRecorder.cs** (682 lines)
   - Lines 234-450: OnDataAvailable late callback bug

4. **TextInjector.cs** (445 lines)
   - Lines 256-329: Clipboard restore race condition

---

## Success Metrics (Validate After Each Fix)

Test these after each day's work:

✅ **Day 1-2 Success**:
- [ ] Transcription completes in <200ms (was 500-1000ms)
- [ ] No UI freezes during transcription
- [ ] Settings save doesn't block UI

✅ **Day 2-3 Success**:
- [ ] App closes in <1 second (was 5-30s)
- [ ] No "state mismatch" log messages
- [ ] Recording state never gets stuck

✅ **Day 3-5 Success**:
- [ ] 100 rapid start/stop cycles without corruption
- [ ] State machine prevents invalid transitions
- [ ] No async void exceptions logged

---

## Quick Reference: Problem → File → Fix

| Problem | File | Line | Fix |
|---------|------|------|-----|
| UI lag 500ms | MainWindow.xaml.cs | 1632 | Remove Dispatcher.InvokeAsync |
| App hang 30s | RecordingCoordinator.cs | 562 | Replace spin-wait with event |
| State desync | MainWindow/Coordinator/Recorder | N/A | Add state machine |
| Ghost text | AudioRecorder.cs | 340 | Better instance tracking |
| Clipboard loss | TextInjector.cs | 273 | Queue-based restore |

---

## Important Notes

⚠️ **Don't start with refactoring** - Fix critical bugs first (Week 1)

⚠️ **Test after each change** - Don't batch multiple fixes

⚠️ **Keep old code** - Comment out, don't delete (rollback safety)

⚠️ **Document WHY** - Add comments explaining the fix rationale

---

## Questions for New Chat

If uncertain, ask these:

1. "Should I start with Day 1-2 or a different priority?"
2. "Do you want me to create the state machine first, or fix async void handlers?"
3. "Should I make a backup branch before starting?"

Default: **Start with Day 1-2 (Dispatcher.Invoke optimization) - highest impact, lowest risk**

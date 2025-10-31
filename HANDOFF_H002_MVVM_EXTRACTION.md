# Handoff: H-002 MVVM Extraction Options

**Date**: 2025-10-30
**Branch**: test-reliability-improvements
**Context**: 59% (118k/200k tokens used)
**Status**: M-007 complete (error handling standardized), ready for H-002

---

## Quick Start for Next Session

```bash
git status
# Branch: test-reliability-improvements
# 7 commits ahead of origin
# Last commit: 68e0581 (M-007: error handling standardization)

# Verify tests still passing
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj
# Expected: 311/311 passing
```

---

## Current Architectural State

### Key Discovery: MainViewModel Already Exists!

**MainWindow.xaml.cs** (2657 lines):
- directly creates/manages 6 services (AudioRecorder, PersistentWhisperService, etc.)
- handles all UI logic, event handlers, state management
- working, battle-tested code with many subtle fixes
- complex: hotkey modes, timers, disposal, threading edge cases

**MainViewModel.cs** (810 lines):
- COMPLETE MVVM implementation already written
- designed for dependency injection
- **CRITICAL GAP:** expects controller layer that doesn't exist:
  - `IRecordingController` ❌ (not implemented)
  - `ITranscriptionController` ❌ (not implemented)
  - `ISettingsService` ❌ (not implemented)
  - `IProFeatureService` ✅ (exists)
  - `ITranscriptionHistoryService` ❌ (not implemented)
  - `IHotkeyManager` ❌ (interface doesn't exist, only concrete class)
  - `ISystemTrayManager` ❌ (interface doesn't exist)
  - `IErrorLogger` ❌ (not implemented)

**The Mismatch:**
MainWindow bypasses the controller layer and uses services directly. MainViewModel expects controllers that don't exist yet.

---

## Three Options for H-002

### Option A: Full MVVM with Controller Layer (20h)

**Create missing abstraction layer:**

```
Current:  MainWindow → Services (AudioRecorder, WhisperService, etc.)
Target:   MainWindow → MainViewModel → Controllers → Services
```

**What to build:**
1. **IRecordingController + RecordingController** (~4h)
   - StartRecordingAsync()
   - StopRecordingAsync(transcribe: bool)
   - RecordAndTranscribeAsync()
   - Events: RecordingStarted, RecordingStopped, ProgressChanged
   - Manages AudioRecorder lifecycle

2. **ITranscriptionController + TranscriptionController** (~4h)
   - TranscribeAsync(audioPath, modelPath)
   - ValidateTranscriptionSetupAsync()
   - CancelCurrentTranscriptionAsync()
   - Events: TranscriptionStarted, TranscriptionCompleted
   - Manages PersistentWhisperService + TextInjector

3. **ISettingsService + SettingsService** (~3h)
   - LoadSettingsAsync(), SaveSettingsAsync()
   - GetModelPath(), GetSelectedModel()
   - Properties for all settings
   - Event: SettingChanged

4. **Missing Interfaces** (~2h)
   - ITranscriptionHistoryService (wrapper for existing service)
   - IHotkeyManager (extract interface from HotkeyManager)
   - ISystemTrayManager (extract interface from SystemTrayManager)
   - IErrorLogger (wrapper for static ErrorLogger)

5. **Wire MainWindow to MainViewModel** (~5h)
   - Remove direct service instantiation
   - Create controllers in MainWindow constructor
   - Set DataContext = new MainViewModel(controllers...)
   - Update XAML bindings
   - Test all scenarios (recording, hotkeys, settings, history)

6. **Testing & Bug Fixes** (~2h)
   - Edge cases: disposal, threading, timer cleanup
   - Hotkey modes (toggle vs push-to-talk)
   - Settings persistence
   - History filtering

**Pros:**
- ✅ Cleanest architecture (proper MVVM, separation of concerns)
- ✅ MainViewModel already designed for this
- ✅ Testability improves dramatically (can mock controllers)
- ✅ Sets up for future features cleanly
- ✅ Matches industry best practices

**Cons:**
- ❌ HIGH RISK: refactoring 2657 lines of working code
- ❌ 20 hours = 2-3 full sessions
- ❌ Controller layer is new abstraction (more complexity)
- ❌ Potential for bugs in edge cases (MainWindow has subtle fixes)
- ❌ MainWindow has been battle-tested in production

**Verdict:** Architecturally excellent, but risky for production app.

---

### Option B: Simplify MainViewModel (8h)

**Adapt MainViewModel to use services directly (remove controller layer):**

```
Current:  MainWindow → Services
Target:   MainWindow → Simplified MainViewModel → Services
```

**Changes:**
1. **Simplify MainViewModel** (~3h)
   - Replace IRecordingController with direct AudioRecorder
   - Replace ITranscriptionController with PersistentWhisperService + TextInjector
   - Replace ISettingsService with direct Settings object
   - Keep existing service interfaces where possible

2. **Update MainWindow** (~3h)
   - Instantiate services
   - Pass services to MainViewModel constructor
   - Set DataContext = new MainViewModel(services...)
   - Remove duplicate logic from MainWindow

3. **Update XAML bindings** (~1h)
   - Bind UI to MainViewModel properties
   - Test all scenarios

4. **Testing** (~1h)
   - Verify functionality preserved
   - Check edge cases

**Pros:**
- ✅ Moderate effort (8h vs 20h)
- ✅ Still gets MVVM benefits (separation, testability)
- ✅ No new abstractions (simpler)
- ✅ Lower risk than Option A

**Cons:**
- ❌ MainViewModel becomes less "pure" MVVM
- ❌ Services still directly in ViewModel (not ideal)
- ❌ Less testable than controller approach
- ❌ Still refactoring 2657 lines

**Verdict:** Pragmatic compromise, but sacrifices some architecture quality.

---

### Option C: Incremental Extraction (12h) ⭐ RECOMMENDED

**Extract smaller ViewModels without full rewrite:**

```
Current:  MainWindow (2657 lines, everything mixed)
Target:   MainWindow (orchestrator) + Small ViewModels (focused responsibilities)
```

**Approach:**
1. **Create RecordingViewModel** (~3h)
   - Extract recording state (isRecording, recordingStartTime)
   - Extract recording commands (StartRecording, StopRecording)
   - Handle AudioRecorder events
   - Properties: IsRecording, RecordingElapsed, CanRecord
   - MainWindow passes AudioRecorder to ViewModel

2. **Create HistoryViewModel** (~2h)
   - Extract history display logic
   - Commands: ClearHistory, CopyToClipboard, DeleteItem, TogglePin
   - Filtering and search
   - MainWindow passes TranscriptionHistoryService

3. **Create StatusViewModel** (~1h)
   - Extract status text/color management
   - Properties: StatusText, StatusTextColor, StatusIndicator
   - Methods: UpdateStatus(text, color)

4. **Slim MainWindow** (~4h)
   - Keep service instantiation (works well)
   - Delegate to ViewModels for UI logic
   - Remain as orchestrator (wires services to ViewModels)
   - Preserve working hotkey/timer logic

5. **Update XAML** (~1h)
   - Bind sections to respective ViewModels
   - Test all functionality

6. **Testing** (~1h)
   - Verify no regressions
   - Test all scenarios

**Pros:**
- ✅ Lower risk (incremental changes, preserve working code)
- ✅ Reasonable effort (12h, manageable in 1-2 sessions)
- ✅ Each ViewModel is small, focused, testable
- ✅ MainWindow remains thin orchestrator
- ✅ Can evolve to full MVVM later (Option A) if desired
- ✅ Preserves battle-tested logic (hotkeys, timers, disposal)

**Cons:**
- ❌ Not "pure" MVVM (MainWindow still instantiates services)
- ❌ Less clean than Option A architecturally
- ❌ Still some coupling between MainWindow and services

**Verdict:** Best balance of improvement vs risk. Proven incremental approach.

---

## Detailed Breakdown: Option C (Recommended)

### Phase 1: Create RecordingViewModel (~3h)

**File:** `VoiceLite/VoiceLite/Presentation/ViewModels/RecordingViewModel.cs`

**Responsibilities:**
- Recording state management
- AudioRecorder event handling
- Recording commands
- Elapsed time display

**Properties:**
```csharp
public bool IsRecording { get; set; }
public TimeSpan RecordingElapsed { get; set; }
public bool CanRecord => !IsRecording && !IsTranscribing;
public string RecordingButtonText => IsRecording ? "Stop Recording" : "Start Recording";
```

**Commands:**
```csharp
public ICommand StartRecordingCommand { get; }
public ICommand StopRecordingCommand { get; }
```

**Constructor:**
```csharp
public RecordingViewModel(AudioRecorder audioRecorder)
{
    _audioRecorder = audioRecorder;
    // Subscribe to events
    _audioRecorder.AudioFileReady += OnAudioFileReady;
}
```

**Extract from MainWindow:**
- Lines 38-42 (recording state fields)
- Lines 730-770 (StartRecording method)
- Lines 1342-1380 (HandleAudioFileReady)
- Recording elapsed timer logic

### Phase 2: Create HistoryViewModel (~2h)

**File:** `VoiceLite/VoiceLite/Presentation/ViewModels/HistoryViewModel.cs`

**Responsibilities:**
- Display transcription history
- Search/filter
- History item commands (copy, delete, pin)

**Properties:**
```csharp
public ObservableCollection<TranscriptionItem> History { get; set; }
public string SearchText { get; set; }
public TranscriptionItem? SelectedItem { get; set; }
```

**Commands:**
```csharp
public ICommand ClearHistoryCommand { get; }
public ICommand CopyToClipboardCommand { get; }
public ICommand DeleteItemCommand { get; }
public ICommand TogglePinCommand { get; }
public ICommand SearchCommand { get; }
```

**Extract from MainWindow:**
- Lines 2200-2400 (history display logic)
- Search/filter methods
- Copy/delete/pin handlers

### Phase 3: Create StatusViewModel (~1h)

**File:** `VoiceLite/VoiceLite/Presentation/ViewModels/StatusViewModel.cs`

**Responsibilities:**
- Status text and color
- Status indicator

**Properties:**
```csharp
public string StatusText { get; set; }
public Brush StatusTextColor { get; set; }
public Brush StatusIndicatorColor { get; set; }
```

**Methods:**
```csharp
public void UpdateStatus(string text, Brush color)
{
    StatusText = text;
    StatusTextColor = color;
    StatusIndicatorColor = GetIndicatorColor(color);
}
```

**Extract from MainWindow:**
- Status update logic throughout file
- Temporary status message timer logic

### Phase 4: Slim MainWindow (~4h)

**Keep in MainWindow:**
- Service instantiation (InitializeServicesAsync)
- Hotkey registration (works well, complex)
- Window lifecycle (Loaded, Closing)
- Settings persistence
- Timer cleanup

**Delegate to ViewModels:**
- Recording logic → RecordingViewModel
- History display → HistoryViewModel
- Status updates → StatusViewModel

**MainWindow becomes:**
```csharp
public partial class MainWindow : Window
{
    // Services (keep as-is)
    private AudioRecorder audioRecorder;
    private PersistentWhisperService whisperService;
    // ... etc

    // ViewModels (new)
    private RecordingViewModel recordingViewModel;
    private HistoryViewModel historyViewModel;
    private StatusViewModel statusViewModel;

    private void InitializeViewModels()
    {
        recordingViewModel = new RecordingViewModel(audioRecorder);
        historyViewModel = new HistoryViewModel(historyService);
        statusViewModel = new StatusViewModel();

        // Wire ViewModel events
        recordingViewModel.RecordingCompleted += OnRecordingCompleted;
        recordingViewModel.StatusChanged += (s, msg) => statusViewModel.UpdateStatus(msg.Text, msg.Color);
    }
}
```

### Phase 5: Update XAML Bindings (~1h)

**Recording Section:**
```xml
<StackPanel DataContext="{Binding RecordingViewModel}">
    <Button Content="{Binding RecordingButtonText}"
            Command="{Binding StartRecordingCommand}"
            IsEnabled="{Binding CanRecord}"/>
    <TextBlock Text="{Binding RecordingElapsed}"/>
</StackPanel>
```

**History Section:**
```xml
<ListBox DataContext="{Binding HistoryViewModel}"
         ItemsSource="{Binding History}"
         SelectedItem="{Binding SelectedItem}">
    <!-- Context menu commands bound to HistoryViewModel -->
</ListBox>
```

**Status Section:**
```xml
<StackPanel DataContext="{Binding StatusViewModel}">
    <TextBlock Text="{Binding StatusText}"
               Foreground="{Binding StatusTextColor}"/>
    <Ellipse Fill="{Binding StatusIndicatorColor}"/>
</StackPanel>
```

---

## Implementation Steps (Option C)

### Session 1 (~6h)

**Checkpoint 1: RecordingViewModel** (~3h)
1. Create `RecordingViewModel.cs` with properties/commands
2. Extract recording logic from MainWindow
3. Update MainWindow to use RecordingViewModel
4. Test recording flow (start/stop)
5. Commit: "refactor: extract RecordingViewModel from MainWindow"

**Checkpoint 2: HistoryViewModel** (~2h)
1. Create `HistoryViewModel.cs`
2. Extract history logic from MainWindow
3. Update MainWindow to use HistoryViewModel
4. Test history operations (copy, delete, pin, search)
5. Commit: "refactor: extract HistoryViewModel from MainWindow"

**Checkpoint 3: StatusViewModel** (~1h)
1. Create `StatusViewModel.cs`
2. Extract status logic from MainWindow
3. Update all status calls to use StatusViewModel
4. Test status updates
5. Commit: "refactor: extract StatusViewModel from MainWindow"

### Session 2 (~6h)

**Checkpoint 4: Slim MainWindow** (~4h)
1. Remove extracted logic from MainWindow
2. Wire ViewModels together
3. Update XAML bindings
4. Verify all functionality works
5. Commit: "refactor: slim MainWindow using ViewModels"

**Checkpoint 5: Testing & Polish** (~2h)
1. Run full test suite (311 tests)
2. Manual smoke testing:
   - Recording flow
   - History operations
   - Settings changes
   - Hotkey modes
   - Window lifecycle (minimize, close)
3. Fix any issues
4. Commit: "test: verify H-002 MVVM extraction complete"

---

## Testing Strategy

### Automated Tests
```bash
# Run all tests
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj

# Expected: 311/311 passing (maintain current rate)
```

### Manual Smoke Test Checklist

**Recording:**
- [ ] Start recording via button
- [ ] Start recording via hotkey
- [ ] Stop recording (button + hotkey)
- [ ] Recording elapsed timer updates
- [ ] Status updates during recording

**Transcription:**
- [ ] Transcribe completes successfully
- [ ] Text injected into target app
- [ ] Status shows processing time
- [ ] History item added

**History:**
- [ ] History displays items
- [ ] Copy to clipboard works
- [ ] Delete item works
- [ ] Pin/unpin works
- [ ] Search filters correctly
- [ ] Clear history preserves pinned

**Settings:**
- [ ] Open settings window
- [ ] Change model (updates UI)
- [ ] Change hotkey (registers correctly)
- [ ] Settings persist on restart

**Window Lifecycle:**
- [ ] Minimize to tray works
- [ ] Restore from tray works
- [ ] Close to tray works
- [ ] Exit properly disposes services

---

## Risk Assessment

### Option A Risks (HIGH)
- ❌ 20 hours = multiple sessions (high context overhead)
- ❌ Refactoring 2657 lines (many subtle edge cases)
- ❌ New abstractions (controller layer) = potential bugs
- ❌ MainWindow has complex logic (hotkeys, timers, threading)
- ❌ Could introduce regressions in production

### Option B Risks (MEDIUM)
- ⚠️ Still refactoring 2657 lines
- ⚠️ MainViewModel becomes less pure MVVM
- ⚠️ 8 hours = needs careful planning

### Option C Risks (LOW) ⭐
- ✅ Incremental (preserve working code)
- ✅ Each ViewModel is small, easy to test
- ✅ MainWindow remains as orchestrator (tested logic preserved)
- ✅ Can rollback individual changes if issues arise
- ✅ 12 hours = manageable in 1-2 sessions

---

## Success Criteria

**Code Quality:**
- [ ] MainWindow.xaml.cs reduced from 2657 → ~1800 lines
- [ ] 3 new focused ViewModels (Recording, History, Status)
- [ ] All existing functionality preserved
- [ ] No new warnings or errors

**Testing:**
- [ ] All 311 tests pass
- [ ] Manual smoke test passes
- [ ] No performance regressions

**Architecture:**
- [ ] Clear separation of concerns
- [ ] ViewModels are testable
- [ ] MainWindow is thin orchestrator
- [ ] Can evolve to full MVVM later (Option A) if desired

---

## Decision Matrix

| Criterion | Option A | Option B | Option C |
|-----------|----------|----------|----------|
| Effort | 20h | 8h | 12h ⭐ |
| Risk | HIGH ❌ | MEDIUM | LOW ✅ |
| Architecture Quality | Excellent ✅ | Good | Good ⭐ |
| Testability | Excellent ✅ | Good | Good ⭐ |
| Preserves Working Code | No ❌ | No ❌ | Yes ✅ |
| Can Evolve Later | N/A | No | Yes ✅ |
| Production Safe | Risky ❌ | Moderate | Safe ✅ |

**Recommendation: Option C** - Best balance of improvement, risk, and effort.

---

## Context for Next Session

**Branch Status:**
```bash
# Current branch
test-reliability-improvements (7 commits ahead)

# Recent commits
68e0581 refactor: standardize error handling (M-007)
b7df284 refactor: extract methods from TranscribeAsync (M-003)
e09a578 security: fix IP spoofing in rate limiter (S-001)
```

**Completed Tasks:**
- ✅ M-007: Error handling standardization (16 fixes across 4 services)
- ✅ M-003: TranscribeAsync method extraction
- ✅ S-001: IP spoofing security fix

**Current State:**
- Build: 0 warnings, 0 errors
- Tests: 311/311 passing (0 failures, 42 skipped)
- Clean working directory
- Ready for H-002

**Files to Review:**
- `MainWindow.xaml.cs` (2657 lines) - target for extraction
- `MainViewModel.cs` (810 lines) - reference for MVVM patterns
- `ViewModelBase.cs` - base class for new ViewModels

---

## Quick Decision Prompt for Next Session

```
Continue H-002 MVVM extraction.

Current state:
- Branch: test-reliability-improvements (clean, 7 commits)
- Tests: 311/311 passing
- M-007 complete (error handling standardized)

Read HANDOFF_H002_MVVM_EXTRACTION.md for analysis.

Three options:
1. Option A: Full MVVM + controllers (20h, high risk, cleanest)
2. Option B: Simplify MainViewModel (8h, medium risk, pragmatic)
3. Option C: Incremental extraction (12h, low risk, balanced) ⭐ RECOMMENDED

Which option? Or need more analysis first?
```

---

**End of Handoff Document**

Ready for H-002 decision in next session! 🚀

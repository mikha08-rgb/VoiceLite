# Handoff: H-002 MVVM Extraction Progress (Option C)

**Date**: 2025-10-31
**Branch**: test-reliability-improvements
**Context**: 62% (123k/200k tokens used)
**Status**: Phases 1-2 complete, ready for phases 3-5

---

## Quick Start for Next Session

```bash
git status
# Branch: test-reliability-improvements
# 10 commits ahead of origin
# Last commits:
#   049200e - H-002 Phase 2: HistoryViewModel
#   9073c28 - H-002 Phase 1: RecordingViewModel
#   4352933 - H-002 analysis document
#   68e0581 - M-007: error handling standardization

# Verify tests
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj
# Expected: 311/311 passing

# View newly extracted ViewModels
ls VoiceLite/VoiceLite/Presentation/ViewModels/
# RecordingViewModel.cs (200 lines)
# HistoryViewModel.cs (250 lines)
```

---

## Progress Summary

### ✅ Completed Phases

**Phase 1: RecordingViewModel (commit 9073c28)**
- Extracted recording state and elapsed timer
- Properties: IsRecording, CanRecord, RecordingElapsed, RecordingButtonText
- Commands: ToggleRecordingCommand
- Events: RecordingStartRequested, RecordingStopRequested
- Wired into MainWindow with event handlers
- All tests passing

**Phase 2: HistoryViewModel (commit 049200e)**
- Extracted history management logic
- Properties: SearchText, IsSearchVisible, DisplayedItems, HasHistory
- Commands: ClearHistory, ClearAllHistory, CopyToClipboard, DeleteItem, ReInject, ToggleSearch, ClearSearch
- Events: 6 event handlers for MainWindow orchestration
- Wired into MainWindow
- All tests passing

### ⏸️ Remaining Phases

**Phase 3: StatusViewModel (~1h, ~15k tokens)**
- Extract status text/color management
- Properties: StatusText, StatusTextColor, StatusIndicatorColor
- Methods: UpdateStatus(text, color)
- Simple extraction, minimal risk

**Phase 4: Slim MainWindow + XAML Bindings (~4h, complex)**
- Update MainWindow.xaml to bind to ViewModels
- Remove duplicate event handlers (ClearHistory_Click, etc.)
- Set DataContext bindings
- Verify all UI functionality works
- **HIGH RISK:** XAML changes can break UI

**Phase 5: Testing & Polish (~2h)**
- Full regression testing
- Manual smoke tests (recording, history, settings)
- Update documentation
- Final commit

---

## Current State

### Files Modified

**VoiceLite/VoiceLite/MainWindow.xaml.cs:**
- Added recordingViewModel and historyViewModel fields
- Wired 8 event handlers total:
  - OnRecordingStartRequested, OnRecordingStopRequested
  - OnClearHistoryRequested, OnClearAllHistoryRequested
  - OnCopyToClipboardRequested, OnDeleteItemRequested
  - OnReInjectRequested, OnSearchTextChanged
- Synced ViewModel state in StartRecording/StopRecording
- **Still 2657 lines** (reduction happens in phase 4)

**New Files Created:**
- `VoiceLite/VoiceLite/Presentation/ViewModels/RecordingViewModel.cs` (200 lines)
- `VoiceLite/VoiceLite/Presentation/ViewModels/HistoryViewModel.cs` (250 lines)

### Tests Status
- ✅ 311/311 passing (0 failures, 42 skipped)
- ✅ Build: 0 warnings, 0 errors
- ✅ No regressions

---

## Phase 3 Implementation Guide (Next Session)

### Create StatusViewModel (~1h)

**File:** `VoiceLite/VoiceLite/Presentation/ViewModels/StatusViewModel.cs`

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

private Brush GetIndicatorColor(Brush statusColor)
{
    if (statusColor == Brushes.Green) return Brushes.LightGreen;
    if (statusColor == Brushes.Red) return Brushes.LightCoral;
    if (statusColor == Brushes.Orange) return Brushes.LightGoldenrodYellow;
    return Brushes.LightGray;
}
```

**Integration Steps:**
1. Create StatusViewModel.cs
2. Add `private StatusViewModel? statusViewModel;` to MainWindow
3. Initialize in MainWindow constructor:
   ```csharp
   statusViewModel = new StatusViewModel();
   ```
4. Replace all `UpdateStatus(text, color)` calls with:
   ```csharp
   statusViewModel?.UpdateStatus(text, color);
   ```
5. Build and test (expect 311 tests passing)
6. Commit: "refactor: extract StatusViewModel from MainWindow (H-002 Phase 3)"

---

## Phase 4 Implementation Guide (Complex, ~4h)

### Step 1: Update MainWindow.xaml (~2h)

**Recording Section:**
Find existing recording UI elements and update bindings:

```xml
<!-- OLD: Direct event handlers -->
<Button Content="Start Recording" Click="StartRecording_Click" />

<!-- NEW: ViewModel binding -->
<StackPanel DataContext="{Binding RecordingViewModel}">
    <Button Content="{Binding RecordingButtonText}"
            Command="{Binding ToggleRecordingCommand}"
            IsEnabled="{Binding CanRecord}"
            ToolTip="{Binding RecordingButtonToolTip}"/>
    <TextBlock Text="{Binding RecordingElapsed}"/>
</StackPanel>
```

**Status Section:**
```xml
<StackPanel DataContext="{Binding StatusViewModel}">
    <TextBlock Text="{Binding StatusText}"
               Foreground="{Binding StatusTextColor}"/>
    <Ellipse Fill="{Binding StatusIndicatorColor}"
             Width="12" Height="12"/>
</StackPanel>
```

**History Section:**
```xml
<StackPanel DataContext="{Binding HistoryViewModel}">
    <!-- Search box -->
    <TextBox Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}"
             Visibility="{Binding IsSearchVisible, Converter={StaticResource BoolToVisibilityConverter}}"/>

    <!-- History list (MainWindow still renders cards) -->
    <ItemsControl ItemsSource="{Binding DisplayedItems}">
        <!-- Existing card rendering logic -->
    </ItemsControl>

    <!-- Commands -->
    <Button Content="Clear History"
            Command="{Binding ClearHistoryCommand}"/>
    <Button Content="Toggle Search"
            Command="{Binding ToggleSearchCommand}"/>
</StackPanel>
```

### Step 2: Set DataContext in MainWindow.xaml.cs (~1h)

In MainWindow constructor, after initializing ViewModels:

```csharp
// Set DataContext for View models (XAML will bind to these)
this.DataContext = this; // MainWindow is root context

// Or create a container ViewModel that holds all sub-ViewModels:
// this.DataContext = new { Recording = recordingViewModel, History = historyViewModel, Status = statusViewModel };
```

Better approach - expose ViewModels as properties:

```csharp
public RecordingViewModel RecordingViewModel => recordingViewModel!;
public HistoryViewModel HistoryViewModel => historyViewModel!;
public StatusViewModel StatusViewModel => statusViewModel!;
```

Then in XAML:
```xml
<Window x:Class="VoiceLite.MainWindow"
        DataContext="{Binding RelativeSource={RelativeSource Self}}">
    <StackPanel DataContext="{Binding RecordingViewModel}">
        <!-- Recording UI -->
    </StackPanel>
</Window>
```

### Step 3: Remove Duplicate Event Handlers (~30min)

**Can remove (replaced by ViewModel commands):**
- `ClearHistory_Click` → replaced by ClearHistoryCommand
- `ToggleSearchButton_Click` → replaced by ToggleSearchCommand
- `HistorySearchBox_TextChanged` → replaced by SearchText property binding

**Keep (still needed for complex logic):**
- `StartRecording()` - orchestrates AudioRecorder
- `StopRecording()` - orchestrates AudioRecorder
- `OnAudioFileReady()` - async transcription pipeline
- `CreateHistoryCard()` - UI rendering logic

### Step 4: Test Thoroughly (~30min)

**Manual Testing:**
- [ ] Click "Start Recording" button → recording starts
- [ ] Recording elapsed timer updates
- [ ] Click "Stop Recording" → transcription happens
- [ ] Status text updates correctly
- [ ] History displays transcriptions
- [ ] Search filters history
- [ ] Copy/delete/re-inject work from context menu
- [ ] Clear history works
- [ ] Toggle search works

**Automated Testing:**
```bash
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj
# Expected: 311/311 passing
```

---

## Phase 5 Implementation Guide (~2h)

### Full Regression Testing (~1h)

**Automated:**
```bash
# Run all tests
dotnet test

# Run specific categories if available
dotnet test --filter "Category=Integration"
```

**Manual Smoke Test Checklist:**

Recording Flow:
- [ ] Start recording via button
- [ ] Start recording via hotkey
- [ ] Recording elapsed timer displays
- [ ] Stop recording via button
- [ ] Stop recording via hotkey
- [ ] Transcription completes
- [ ] Text injected into target app

History:
- [ ] History displays items
- [ ] Newest items at top
- [ ] Copy to clipboard works
- [ ] Delete item works
- [ ] Re-inject works
- [ ] Search filters correctly
- [ ] Clear history preserves pinned (if implemented)
- [ ] History persists after restart

Settings:
- [ ] Open settings window
- [ ] Change recording hotkey
- [ ] Change AI model
- [ ] Change text injection mode
- [ ] Settings persist

Window Lifecycle:
- [ ] Minimize to tray
- [ ] Restore from tray
- [ ] Close to tray
- [ ] Exit properly disposes services

### Documentation Updates (~30min)

Update these files:
1. **CLAUDE.md** - Note MVVM extraction in recent changes
2. **README.md** (if exists) - Update architecture section
3. **M007_ERROR_HANDLING_ANALYSIS.md** - Add note about H-002 progress

### Final Commit (~30min)

```bash
git add -A
git commit -m "refactor: complete H-002 MVVM extraction (phases 3-5)

completed incremental MVVM extraction from MainWindow:
- phase 3: StatusViewModel extracted
- phase 4: XAML bindings updated, duplicate handlers removed
- phase 5: comprehensive testing, all 311 tests passing

**architecture:**
- RecordingViewModel: recording state + commands
- HistoryViewModel: history management + search
- StatusViewModel: status display
- MainWindow: slim orchestrator (services + ViewModels)

**testing:**
- all 311 tests passing
- manual smoke tests complete
- no regressions

**impact:**
- MainWindow reduced from 2657 → ~1800 lines
- cleaner separation of concerns
- ViewModels are testable
- can evolve to full MVVM (option A) later if desired

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Risk Assessment

### Phase 3 Risks (LOW)
- ✅ Simple extraction (status text/color)
- ✅ No XAML changes required
- ✅ Minimal logic change
- ✅ Easy to rollback if issues

### Phase 4 Risks (MEDIUM-HIGH)
- ⚠️ XAML binding changes can break UI
- ⚠️ DataContext setup can be tricky
- ⚠️ Easy to miss event handler removal
- ⚠️ UI might not update if bindings wrong
- **Mitigation:** Test each section incrementally, keep old event handlers temporarily

### Phase 5 Risks (LOW)
- ✅ Just testing and documentation
- ✅ No code changes
- ✅ Can identify issues before final commit

---

## Architectural Decisions Made

### Why Event-Based Communication?

```
MainWindow ← event → ViewModel
```

**Rationale:**
- MainWindow keeps orchestration logic (low risk)
- ViewModels remain simple (state + commands)
- Easy to understand and debug
- Backward compatible (existing logic preserved)

**Alternative (not chosen):**
- ViewModels directly call services → higher complexity
- Would require dependency injection refactor
- Higher risk for production app

### Why Keep UI Rendering in MainWindow?

**Methods kept in MainWindow:**
- `CreateHistoryCard()` - complex WPF UI creation
- `CreateHistoryContextMenu()` - menu building
- `UpdateHistoryUI()` - layout updates

**Rationale:**
- WPF UI code is verbose (100+ lines per method)
- Moving to ViewModel = no real benefit (still coupled to WPF)
- Keeping it reduces phase 4 complexity
- Can extract later if needed

---

## Success Criteria

### Phases 1-2 (✅ COMPLETE)
- [x] RecordingViewModel created and wired
- [x] HistoryViewModel created and wired
- [x] All 311 tests passing
- [x] 0 build warnings/errors
- [x] Commits created with clear messages

### Phase 3 (⏸️ NEXT)
- [ ] StatusViewModel created
- [ ] Integrated into MainWindow
- [ ] All UpdateStatus calls use ViewModel
- [ ] Tests passing
- [ ] Commit created

### Phase 4 (⏸️ PENDING)
- [ ] XAML bindings updated
- [ ] DataContext set correctly
- [ ] Duplicate event handlers removed
- [ ] UI functionality verified
- [ ] Tests passing

### Phase 5 (⏸️ PENDING)
- [ ] Full regression testing complete
- [ ] Manual smoke tests passed
- [ ] Documentation updated
- [ ] Final commit created
- [ ] Ready to merge or continue with more features

---

## Context for Next Session

**Branch Status:**
```bash
# Current branch
test-reliability-improvements (10 commits ahead)

# Recent commits
049200e refactor: extract HistoryViewModel (H-002 Phase 2)
9073c28 refactor: extract RecordingViewModel (H-002 Phase 1)
4352933 docs: H-002 MVVM extraction analysis
68e0581 refactor: standardize error handling (M-007)
```

**Completed:**
- ✅ M-007: Error handling standardization
- ✅ H-002 Phase 1: RecordingViewModel
- ✅ H-002 Phase 2: HistoryViewModel

**Ready for:**
- Phase 3: StatusViewModel (~1h)
- Phase 4: XAML bindings (~4h, complex)
- Phase 5: Testing (~2h)

**Files to Review:**
- `RecordingViewModel.cs` - reference for pattern
- `HistoryViewModel.cs` - reference for pattern
- `MainWindow.xaml.cs` - see event handler wiring
- `MainWindow.xaml` - will need updates in phase 4

**Total Effort Remaining:** ~7 hours (phases 3-5)

---

## Quick Decision Prompt

```
Continue H-002 MVVM extraction.

Progress: 2/5 phases complete
- Phase 1 ✅ RecordingViewModel
- Phase 2 ✅ HistoryViewModel
- Phase 3 ⏸️ StatusViewModel (~1h, low risk)
- Phase 4 ⏸️ XAML bindings (~4h, medium-high risk)
- Phase 5 ⏸️ Testing (~2h)

Context: 62% used (76k remaining - enough for phase 3, marginal for phase 4-5)

Recommendation:
1. Complete phase 3 now (StatusViewModel) - quick win, low risk
2. Create final handoff before phase 4 (complex XAML changes need fresh context)

Options:
A) Continue with phase 3 (StatusViewModel)
B) Stop here, comprehensive handoff created
C) Go all the way (phases 3-5) - risky with limited context

Your choice?
```

---

**End of Handoff Document**

Phases 1-2 complete, tested, committed! Ready for phases 3-5! 🚀

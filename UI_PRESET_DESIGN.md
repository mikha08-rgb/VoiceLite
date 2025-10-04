# VoiceLite UI Preset System - Design Document

## Overview
This document outlines the 3 UI presets designed for VoiceLite, along with implementation notes.

---

## Preset 1: Default (Hybrid Baseline) ✅ IMPLEMENT FIRST

**Description**: Clean and simple - perfect for most users

**Visual Design**:
```
┌─────────────────────────────────────────────────────┐
│ VoiceLite                              ● Ready      │
├─────────────────────────────────────────────────────┤
│ Hotkey: LeftShift  •  Model: Small                  │
│                                                      │
│ ┌─────────────────────────────────────────────────┐ │
│ │                                                  │ │
│ │  Just now                                        │ │
│ │  How can we improve our core main user...       │ │
│ │                                                  │ │
│ │  ─────────────────────────────────────────────  │ │
│ │                                                  │ │
│ │  Just now                                        │ │
│ │  How can we improve our core?                   │ │
│ │                                                  │ │
│ │  ─────────────────────────────────────────────  │ │
│ │                                                  │ │
│ │  5 mins ago                                      │ │
│ │  Add progress indicator to processing status    │ │
│ │                                                  │ │
│ └─────────────────────────────────────────────────┘ │
│                                                      │
│ ┌──────────────────┐  ┌────────────────────────┐   │
│ │  VoiceShortcuts  │  │      Settings          │   │
│ └──────────────────┘  └────────────────────────┘   │
│                                                      │
│ ☑ Minimize to system tray on close                 │
└─────────────────────────────────────────────────────┘
```

**Changes from Current**:
- ❌ Remove "Recent Transcriptions • 10 items" header
- ❌ Remove 🔍 search icon (keep Ctrl+F keyboard shortcut)
- ❌ Remove ••• menu icon (keep right-click context menu)
- ❌ Remove metadata lines (13 words • 5.6s • ggml-small.bin)
- ✅ Keep VoiceLite title + Ready status
- ✅ Keep Hotkey/Model config line
- ✅ Keep both buttons at bottom
- ✅ Keep checkbox
- ✅ Cleaner history cards (timestamp + text only, no metadata)

**Implementation**:
1. Edit `MainWindow.xaml` lines 79-217 (remove header clutter)
2. Edit `MainWindow.xaml.cs` CreateHistoryCard() method (remove metadata TextBlock)
3. Keep search/menu functionality but hide UI buttons

---

## Preset 2: Compact (Power User)

**Description**: Maximum density - see more at a glance

**Visual Design**:
```
┌─────────────────────────────────────────────────────┐
│ VoiceLite                                   ● Ready │
│ LeftShift • Small • 8 items                         │
├─────────────────────────────────────────────────────┤
│                                                      │
│ Just now   How can we improve our core main user... │
│ Just now   How can we improve our core?             │
│ 5 mins     Add progress indicator to processing...  │
│ 12 mins    Fix stuck state bugs with comprehensive  │
│ 1 hour     Eliminate all stuck-state bugs with...   │
│ 2 hours    Update download link to v1.0.29          │
│ 3 hours    Implement baseline hybrid UI cleanup     │
│                                                      │
├─────────────────────────────────────────────────────┤
│ VoiceShortcuts            Settings    ☑ Minimize    │
└─────────────────────────────────────────────────────┘
```

**Features**:
- **7-8 items visible** (vs 4 in Default)
- **Natural language time** (not 00:01 = easier to read)
- **Two-line header** (less cramped)
- **No separators** between items
- **Inline footer** (saves vertical space)
- **Item count** shown in header

**Implementation**:
- Create `CompactHistoryView.xaml` UserControl
- Single-line items with left-aligned timestamp
- Inline config bar and footer
- No borders/boxes between items

---

## Preset 3: Status Hero

**Description**: Large status indicator - never miss recording state

**Visual Design**:
```
┌─────────────────────────────────────────────────────┐
│                                                      │
│   ████████████  Ready to Transcribe  ████████████   │
│                 LeftShift  •  Small                 │
│                                                      │
├─────────────────────────────────────────────────────┤
│                                                      │
│  Just now                                            │
│  How can we improve our core main user...           │
│                                                      │
│  ────────────────────────────────────────────────   │
│                                                      │
│  Just now                                            │
│  How can we improve our core?                       │
│                                                      │
│  ────────────────────────────────────────────────   │
│                                                      │
│  5 mins ago                                          │
│  Add progress indicator to processing status        │
│                                                      │
│  ────────────────────────────────────────────────   │
│                                                      │
│ ┌──────────────────┐  ┌────────────────────────┐   │
│ │  VoiceShortcuts  │  │      Settings          │   │
│ └──────────────────┘  └────────────────────────┘   │
└─────────────────────────────────────────────────────┘
```

**Features**:
- **Huge status banner** (38px height, impossible to miss)
- **Color-coded states**:
  - Ready: Blue gradient (#667eea)
  - Recording: Red (#ef4444) with pulse
  - Processing: Orange (#f59e0b) with spinner
  - Success: Green (#10b981)
- **No history panel border** (cleaner look)
- **Config integrated** into banner subtitle

**Implementation**:
- Create `StatusHeroHistoryView.xaml` UserControl
- Large Border at top with gradient background
- TextBlock status text (24pt, centered)
- Remove border from history ScrollViewer

---

## Settings Integration

### SettingsWindowNew.xaml - Add Appearance Tab

```xaml
<TabItem Header="Appearance">
    <StackPanel Margin="20">
        <TextBlock Text="UI Preset" FontSize="14" FontWeight="SemiBold" Margin="0,0,0,12"/>

        <RadioButton x:Name="PresetDefaultRadio"
                     Content="Default (Balanced)"
                     GroupName="UIPreset"
                     IsChecked="True"
                     Margin="0,0,0,8"/>
        <TextBlock Text="Clean and simple - perfect for most users"
                   FontSize="12"
                   Foreground="#7A7A7A"
                   Margin="24,0,0,16"/>

        <RadioButton x:Name="PresetCompactRadio"
                     Content="Compact (Power User)"
                     GroupName="UIPreset"
                     Margin="0,0,0,8"/>
        <TextBlock Text="Maximum density - see more at a glance"
                   FontSize="12"
                   Foreground="#7A7A7A"
                   Margin="24,0,0,16"/>

        <RadioButton x:Name="PresetStatusHeroRadio"
                     Content="Status Hero (Clear Feedback)"
                     GroupName="UIPreset"
                     Margin="0,0,0,8"/>
        <TextBlock Text="Large status indicator - never miss recording state"
                   FontSize="12"
                   Foreground="#7A7A7A"
                   Margin="24,0,0,16"/>

        <TextBlock Text="⚠ Changing UI preset requires app restart"
                   FontSize="11"
                   Foreground="#F39C12"
                   Margin="0,16,0,0"/>
    </StackPanel>
</TabItem>
```

### MainWindow.xaml.cs - Preset Switching Logic

```csharp
// In InitializeServicesAsync():
private async Task InitializeServicesAsync()
{
    // ... existing code ...

    // Load appropriate history view based on preset
    LoadHistoryViewForPreset(settings.UIPreset);
}

private void LoadHistoryViewForPreset(UIPreset preset)
{
    // Clear existing history panel
    HistoryPanelContainer.Child = null;

    switch (preset)
    {
        case UIPreset.Default:
            var defaultView = new Controls.DefaultHistoryView();
            defaultView.Initialize(settings, historyService, textInjector,
                () => SaveSettings(),
                (status, color) => UpdateStatus(status, color));
            HistoryPanelContainer.Child = defaultView;
            break;

        case UIPreset.Compact:
            var compactView = new Controls.CompactHistoryView();
            compactView.Initialize(settings, historyService, textInjector,
                () => SaveSettings(),
                (status, color) => UpdateStatus(status, color));
            HistoryPanelContainer.Child = compactView;
            break;

        case UIPreset.StatusHero:
            var heroView = new Controls.StatusHeroHistoryView();
            heroView.Initialize(settings, historyService, textInjector,
                () => SaveSettings(),
                (status, color) => UpdateStatus(status, color));
            HistoryPanelContainer.Child = heroView;
            break;
    }
}
```

---

## Implementation Phases

### Phase 1: Baseline Cleanup (DO THIS NOW) ✅
- [x] Add UIPreset enum to Settings.cs
- [ ] Clean up MainWindow.xaml (remove header clutter)
- [ ] Simplify history cards (remove metadata)
- [ ] Test Default preset

### Phase 2: Compact Preset (OPTIONAL)
- [ ] Create CompactHistoryView.xaml UserControl
- [ ] Implement single-line compact layout
- [ ] Add inline config/footer
- [ ] Test switching between Default and Compact

### Phase 3: Status Hero Preset (OPTIONAL)
- [ ] Create StatusHeroHistoryView.xaml UserControl
- [ ] Implement large status banner with gradients
- [ ] Add color-coded state transitions
- [ ] Add pulsing animations for Recording state

### Phase 4: Settings Integration (OPTIONAL)
- [ ] Add Appearance tab to SettingsWindowNew.xaml
- [ ] Wire up preset radio buttons
- [ ] Add preset switching logic to MainWindow.xaml.cs
- [ ] Add "requires restart" warning

---

## Quick Win: Just Do Phase 1

For immediate impact, **only implement Phase 1** (baseline cleanup). This gives you:
- ✅ Cleaner UI (remove visual noise)
- ✅ No code complexity (just edit existing XAML)
- ✅ 15 minutes of work
- ✅ Immediate user feedback

Phases 2-4 can be added later based on user demand.

---

## Files to Edit for Phase 1

1. **Settings.cs** ✅ DONE
   - Added UIPreset enum
   - Added UIPreset property

2. **MainWindow.xaml** (lines 79-217)
   - Remove header bar with "Recent Transcriptions", search icon, menu icon
   - Keep search functionality (Ctrl+F still works)

3. **MainWindow.xaml.cs** (CreateHistoryCard method, ~line 1960)
   - Remove metadata TextBlock (13 words • 5.6s • ggml-small.bin)
   - Keep timestamp + text only

---

## Color Codes for Status Hero

```csharp
// Status colors for Status Hero preset
Ready: #667eea (cool blue)
Recording: #ef4444 (hot red) + pulse animation
Processing: #f59e0b (amber) + spinner
Success: #10b981 (green) + checkmark
Error: #dc2626 (red)
```

---

**End of Design Document**

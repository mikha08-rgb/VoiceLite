# VoiceLite - Minimal, Approachable Design

## Problem
Tools that look complex are considered hard to approach. Quick Settings panel added clutter.

## Solution: Radical Simplification

### Principle
**"Show only what the user needs right now. Everything else: one click away."**

---

## Main Window - Minimal Design

```
┌────────────────────────────────────────┐
│ VoiceLite                   Ready ●    │
├────────────────────────────────────────┤
│                                        │
│        Press Left Alt to record        │
│                                        │
│  ┌────────────────────────────────┐   │
│  │ Recent Transcriptions          │   │
│  ├────────────────────────────────┤   │
│  │ 2 mins ago                     │   │
│  │ This is a test recording       │   │
│  │                                │   │
│  │ 5 mins ago                     │   │
│  │ Hello world example            │   │
│  │                                │   │
│  │ (48 more)                      │   │
│  └────────────────────────────────┘   │
│                                        │
│  [Settings]                            │
└────────────────────────────────────────┘
```

**Key features:**
- ✅ Single clear instruction: "Press Left Alt to record"
- ✅ History panel (useful context)
- ✅ One button: "Settings"
- ✅ No clutter, no confusion
- ✅ Width: 550px (smaller, less intimidating)

---

## Settings - Minimal Design

**Instead of 6 tabs, use a SINGLE SCROLLABLE PAGE with sections:**

```
┌──────────────────────────────────────────────┐
│ Settings                              [X]    │
├──────────────────────────────────────────────┤
│                                              │
│  Quick Setup                                 │
│  ──────────────────────────────────────      │
│  Hotkey: [Left Alt        ] [Change]         │
│  Mode:   (•) Hold to talk  ( ) Toggle        │
│  Model:  [Pro (Free)    ▼] [Download]        │
│                                              │
│  ─────────────────────────────────────       │
│                                              │
│  VoiceShortcuts                              │
│  ──────────────────────────────────────      │
│  Transform words as you speak                │
│  [Manage Shortcuts (3 active)]               │
│                                              │
│  ─────────────────────────────────────       │
│                                              │
│  Text Cleanup                                │
│  ──────────────────────────────────────      │
│  Choose a preset:                            │
│  ( ) Off  (•) Light  ( ) Full                │
│                                              │
│  ─────────────────────────────────────       │
│                                              │
│  Advanced (click to expand) ▼                │
│                                              │
│  [Save]  [Cancel]                            │
└──────────────────────────────────────────────┘
```

**Sections (top to bottom):**
1. **Quick Setup** - Hotkey, Mode, Model (3 lines)
2. **VoiceShortcuts** - One-liner description + Manage button
3. **Text Cleanup** - 3 radio buttons (Off/Light/Full)
4. **Advanced** - Collapsed, click to expand

**Total visible options: 8** (down from 50+)

---

## Implementation

### Step 1: Simplify Main Window

Keep it minimal - just remove the Quick Settings panel we just added (already reverted).

Ensure:
- Width: 550px (not 900px)
- Single instruction line: "Press {hotkey} to record"
- History panel
- One Settings button

### Step 2: Simplify Settings to Single Page

**File**: `SettingsWindowNew.xaml`

Replace TabControl with ScrollViewer:

```xaml
<Window x:Class="VoiceLite.SettingsWindowNew"
        Title="Settings"
        Height="600"
        Width="500"
        WindowStartupLocation="CenterOwner">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Header -->
        <Border Grid.Row="0" Background="#FAFAFA" Padding="20,15">
            <TextBlock Text="Settings" FontSize="18" FontWeight="SemiBold"/>
        </Border>

        <!-- Content (single scrollable page) -->
        <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto">
            <StackPanel Margin="20">

                <!-- SECTION 1: QUICK SETUP -->
                <TextBlock Text="Quick Setup"
                          FontSize="16"
                          FontWeight="SemiBold"
                          Margin="0,0,0,12"/>
                <Border BorderBrush="#E0E0E0"
                       BorderThickness="0,0,0,1"
                       Padding="0,0,0,16"
                       Margin="0,0,0,20">
                    <StackPanel>
                        <!-- Hotkey -->
                        <Grid Margin="0,0,0,8">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="100"/>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0"
                                      Text="Hotkey:"
                                      VerticalAlignment="Center"/>
                            <TextBox Grid.Column="1"
                                    Name="HotkeyTextBox"
                                    IsReadOnly="True"
                                    Padding="8,4"
                                    Margin="0,0,8,0"/>
                            <Button Grid.Column="2"
                                   Content="Change"
                                   Width="80"
                                   Click="ChangeHotkey_Click"/>
                        </Grid>

                        <!-- Mode -->
                        <Grid Margin="0,0,0,8">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="100"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0"
                                      Text="Mode:"
                                      VerticalAlignment="Center"/>
                            <StackPanel Grid.Column="1" Orientation="Horizontal">
                                <RadioButton Name="HoldToTalkRadio"
                                           Content="Hold to talk"
                                           IsChecked="True"
                                           Margin="0,0,16,0"/>
                                <RadioButton Name="ToggleRadio"
                                           Content="Toggle"/>
                            </StackPanel>
                        </Grid>

                        <!-- Model -->
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="100"/>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0"
                                      Text="Model:"
                                      VerticalAlignment="Center"/>
                            <ComboBox Grid.Column="1"
                                     Name="ModelComboBox"
                                     Margin="0,0,8,0"
                                     Padding="8,4"/>
                            <Button Grid.Column="2"
                                   Content="Download"
                                   Width="80"/>
                        </Grid>
                    </StackPanel>
                </Border>

                <!-- SECTION 2: VOICESHORTCUTS -->
                <TextBlock Text="VoiceShortcuts"
                          FontSize="16"
                          FontWeight="SemiBold"
                          Margin="0,0,0,8"/>
                <TextBlock Text="Transform words as you speak (brb → be right back)"
                          FontSize="12"
                          Foreground="#666"
                          Margin="0,0,0,8"
                          TextWrapping="Wrap"/>
                <Border BorderBrush="#E0E0E0"
                       BorderThickness="0,0,0,1"
                       Padding="0,0,0,16"
                       Margin="0,0,0,20">
                    <Button Name="ManageShortcutsButton"
                           Content="Manage Shortcuts (3 active)"
                           HorizontalAlignment="Left"
                           Padding="12,6"
                           Click="ManageShortcuts_Click"/>
                </Border>

                <!-- SECTION 3: TEXT CLEANUP -->
                <TextBlock Text="Text Cleanup"
                          FontSize="16"
                          FontWeight="SemiBold"
                          Margin="0,0,0,8"/>
                <TextBlock Text="Remove filler words and fix grammar"
                          FontSize="12"
                          Foreground="#666"
                          Margin="0,0,0,8"
                          TextWrapping="Wrap"/>
                <Border BorderBrush="#E0E0E0"
                       BorderThickness="0,0,0,1"
                       Padding="0,0,0,16"
                       Margin="0,0,0,20">
                    <StackPanel Orientation="Horizontal">
                        <RadioButton Name="CleanupOffRadio"
                                   Content="Off"
                                   GroupName="Cleanup"
                                   Margin="0,0,16,0"/>
                        <RadioButton Name="CleanupLightRadio"
                                   Content="Light"
                                   GroupName="Cleanup"
                                   IsChecked="True"
                                   Margin="0,0,16,0"/>
                        <RadioButton Name="CleanupFullRadio"
                                   Content="Full"
                                   GroupName="Cleanup"/>
                    </StackPanel>
                </Border>

                <!-- SECTION 4: ADVANCED (collapsed) -->
                <Expander Header="Advanced"
                         FontSize="16"
                         FontWeight="SemiBold"
                         IsExpanded="False"
                         Margin="0,0,0,20">
                    <StackPanel Margin="0,12,0,0">
                        <!-- All advanced options here -->
                        <CheckBox Content="⚡ Fast Mode (5x faster)"
                                 Name="FastModeCheckBox"
                                 Margin="0,0,0,8"/>
                        <TextBlock Text="Microphone, audio enhancement, etc."
                                  FontSize="12"
                                  Foreground="#666"/>
                    </StackPanel>
                </Expander>

            </StackPanel>
        </ScrollViewer>

        <!-- Footer Buttons -->
        <Border Grid.Row="2"
               BorderBrush="#E0E0E0"
               BorderThickness="0,1,0,0"
               Background="#FAFAFA"
               Padding="20,12">
            <StackPanel Orientation="Horizontal"
                       HorizontalAlignment="Right">
                <Button Content="Cancel"
                       Width="80"
                       Margin="0,0,8,0"
                       Click="Cancel_Click"/>
                <Button Content="Save"
                       Width="80"
                       FontWeight="SemiBold"
                       Click="Save_Click"/>
            </StackPanel>
        </Border>

    </Grid>
</Window>
```

---

## Comparison

### Before (v1.0.38 with Quick Settings)
- Main window: 900px wide, Quick Settings panel
- Settings: 6 tabs, 50+ options
- **Feeling**: Complex, professional tool

### After (Minimal)
- Main window: 550px wide, single instruction
- Settings: 1 scrollable page, 8 visible options
- **Feeling**: Simple, approachable, "I can use this"

---

## User Mental Model

**Before**:
"This has a lot of features I don't understand. I'm overwhelmed."

**After**:
"Oh, I just press Left Alt. That's it. If I want to customize, there's Settings."

---

## Progressive Disclosure Done Right

1. **Main window**: One action (press hotkey)
2. **Settings → Quick Setup**: 3 options (hotkey, mode, model)
3. **Settings → VoiceShortcuts**: 1 button (Manage)
4. **Settings → Text Cleanup**: 3 radios (Off/Light/Full)
5. **Settings → Advanced**: Click to expand (for power users)

**Total cognitive load**: Minimal
**Total clicks to power**: 1-2 clicks
**Total confusion**: Zero

---

## Next Steps

1. Revert Quick Settings panel (DONE)
2. Simplify Settings to single scrollable page
3. Update version to v1.0.39
4. Test with fresh eyes: "Can a beginner use this?"

---

## Success Criteria

✅ Main window is calming, not intimidating
✅ Settings fit on one screen (no tabs)
✅ User can start using in 30 seconds
✅ Power features available but hidden
✅ Zero learning curve

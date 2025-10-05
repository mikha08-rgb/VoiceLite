# VoiceLite Feature Testing Checklist (v1.0.40)

## User Reports: "Features not working as expected"

This checklist will help identify which specific features are broken.

---

## ✅ Core Features (MUST WORK)

### 1. Recording & Transcription
- [ ] **Push-to-Talk Mode**: Press hotkey → record → release → transcribe → paste
- [ ] **Toggle Mode**: Press hotkey → record → press again → transcribe → paste
- [ ] **Auto-Paste**: Transcription automatically pastes into active window
- [ ] **Manual Paste**: Transcription copies to clipboard only
- [ ] **Cancel Recording**: Press Esc during recording → cancels without transcription

**Test Steps**:
1. Open Notepad
2. Press Left Alt (default hotkey)
3. Speak: "This is a test"
4. Release Left Alt
5. **Expected**: Text appears in Notepad within 2-5 seconds
6. **Actual**: _____________________

**Common Issues**:
- ❌ No text appears (transcription failed?)
- ❌ Text appears but not pasted (auto-paste disabled?)
- ❌ App shows "Processing..." forever (stuck state?)

---

### 2. Whisper Models
- [ ] **Lite Model (ggml-tiny.bin)**: Works, lower accuracy
- [ ] **Pro Model (ggml-small.bin)**: Works, DEFAULT, balanced accuracy
- [ ] **Swift Model (ggml-base.bin)**: Works if downloaded
- [ ] **Elite Model (ggml-medium.bin)**: Works if downloaded
- [ ] **Ultra Model (ggml-large-v3.bin)**: Works if downloaded

**Test Steps**:
1. Settings → Model Selection
2. Switch to different model
3. Record short clip
4. **Expected**: Transcription works with selected model
5. **Actual**: _____________________

**Common Issues**:
- ❌ Model missing error (file not found)
- ❌ Transcription takes forever with Large model
- ❌ Model switch doesn't persist after restart

---

### 3. VoiceShortcuts (Custom Dictionary)
- [ ] **Add Entry**: "llm" → "large language model" works
- [ ] **Persistence**: Entries saved after restart
- [ ] **Templates**: Medical/Legal/Tech templates load correctly
- [ ] **Enable/Disable**: Toggle switch works

**Test Steps**:
1. Open VoiceShortcuts button → Add entry: "ai" → "artificial intelligence"
2. Save and close
3. Record: "I work with ai"
4. **Expected**: "I work with artificial intelligence"
5. **Actual**: _____________________

**Common Issues**:
- ❌ Replacements don't apply (feature disabled?)
- ❌ Entries disappear after restart (not saving?)
- ❌ Templates don't load (button broken?)

---

### 4. Text Formatting (Post-Processing)
- [ ] **Capitalization**: First letter, after periods, after ?!
- [ ] **Ending Punctuation**: Period, question, exclamation, none
- [ ] **Filler Word Removal**: um, uh, like, you know, etc.
- [ ] **Contractions**: Expand, contract, leave as-is
- [ ] **Grammar Fixes**: their/there/they're, double negatives

**Test Steps**:
1. Settings → Text Formatting tab
2. Enable all capitalization options
3. Record: "hello world this is a test"
4. **Expected**: "Hello world. This is a test."
5. **Actual**: _____________________

**Common Issues**:
- ❌ No capitalization applied
- ❌ Filler words not removed
- ❌ Settings don't save

---

### 5. Fast Mode (Whisper Server)
- [ ] **Enable Server Mode**: Toggle in Settings → Advanced
- [ ] **Faster Transcription**: ~0.5s vs ~2.5s
- [ ] **Requires Restart**: App must restart to enable
- [ ] **Fallback**: If server fails, uses standard mode

**Test Steps**:
1. Settings → Advanced → Enable "Fast Mode"
2. Restart app
3. Record short clip
4. **Expected**: Transcription happens in <1 second
5. **Actual**: _____________________

**Common Issues**:
- ❌ Server never starts (server.exe missing?)
- ❌ No performance improvement
- ❌ Post-processing doesn't work in server mode (BUG-??? from v1.0.36)

---

### 6. History Panel
- [ ] **Transcriptions Appear**: New recordings show in history
- [ ] **Click to Copy**: Click history item → copies to clipboard
- [ ] **Pin/Unpin**: Pin important items
- [ ] **Delete**: Delete individual items
- [ ] **Clear All**: Clear all history including pinned
- [ ] **Search (Ctrl+F)**: Search through history
- [ ] **Compact Mode**: Single-line layout (default since v1.0.38)

**Test Steps**:
1. Record 3 short clips
2. **Expected**: All 3 appear in history panel
3. Click on first item
4. **Expected**: Text copied to clipboard
5. **Actual**: _____________________

**Common Issues**:
- ❌ History doesn't show new transcriptions (BUG from v1.0.18-v1.0.19?)
- ❌ Click doesn't copy
- ❌ Search doesn't work

---

### 7. Hotkey Configuration
- [ ] **Change Hotkey**: Can change from Left Alt to other keys
- [ ] **Modifier Keys**: Ctrl, Shift, Alt combinations work
- [ ] **Hotkey Persists**: Saved after restart
- [ ] **Conflicts**: Warning if hotkey already used by Windows

**Test Steps**:
1. Settings → Hotkey → Change to F2
2. Save and restart
3. **Expected**: F2 now triggers recording
4. **Actual**: _____________________

**Common Issues**:
- ❌ New hotkey doesn't work
- ❌ Reverts to default after restart
- ❌ No warning about conflicts

---

### 8. System Tray
- [ ] **Minimize to Tray**: Checkbox works
- [ ] **Tray Icon Shows**: Icon appears in system tray
- [ ] **Double-Click Restore**: Restores window from tray
- [ ] **Right-Click Menu**: Settings, Exit options work

**Test Steps**:
1. Enable "Minimize to system tray on close"
2. Close window
3. **Expected**: App minimizes to tray (doesn't quit)
4. **Actual**: _____________________

---

## 🔍 Known Issues from Recent Fixes

### Already Fixed (v1.0.36-v1.0.40)
- ✅ WhisperServerService post-processing bypass (v1.0.36)
- ✅ Dictionary Manager not persisting (v1.0.36)
- ✅ Template loading not persisting (v1.0.36)
- ✅ Timer disposal race conditions (v1.0.40)
- ✅ Settings corruption on shutdown (v1.0.40)

### Still Pending (from BUG_AUDIT_REPORT.md)
- ⚠️ BUG-002: Analytics aggregation logic (HIGH)
- ⚠️ BUG-003: Null reference in mic name (HIGH)
- ⚠️ BUG-004: Task.Wait deadlock in HotkeyManager (HIGH)
- ⚠️ BUG-006: WhisperServerService null check (HIGH)
- ⚠️ BUG-015: PreviewText null check (HIGH)

---

## 🎯 Most Likely User Complaints

Based on recent changes, these are most likely to be broken:

### 1. **History Panel Not Showing New Transcriptions**
**Status**: Should be FIXED in v1.0.19+
**Test**: Record 3 clips → all should appear in history
**If broken**: Check TranscriptionHistoryService integration

### 2. **VoiceShortcuts Not Working**
**Status**: Should be FIXED in v1.0.36+
**Test**: Add "ai" → "artificial intelligence", record "ai"
**If broken**: Check if EnableCustomDictionary is true, check post-processing

### 3. **Text Formatting Not Applied**
**Status**: Should work (added v1.0.24)
**Test**: Record with capitalization enabled
**If broken**: Check PostProcessing settings, check if preset is Custom

### 4. **Fast Mode Slower Than Expected**
**Status**: Experimental feature, may have issues
**Test**: Compare with/without Fast Mode
**If broken**: Check if server.exe is running, check logs

### 5. **Auto-Paste Not Working**
**Status**: Should work
**Test**: Record in Notepad with AutoPaste enabled
**If broken**: Check Settings.AutoPaste value, check TextInjector mode

---

## 📋 Quick Test Procedure (5 minutes)

1. **Basic Recording** (2 min)
   - Open Notepad
   - Press Left Alt, say "Hello world", release
   - **Pass**: Text appears in Notepad
   - **Fail**: ___________________

2. **VoiceShortcuts** (1 min)
   - VoiceShortcuts → Add "test" → "testing one two three"
   - Record: "this is a test"
   - **Pass**: "this is a testing one two three"
   - **Fail**: ___________________

3. **History Panel** (1 min)
   - Record 2 clips
   - **Pass**: Both appear in history, click to copy works
   - **Fail**: ___________________

4. **Settings Persistence** (1 min)
   - Change model to Lite (if you have it)
   - Restart app
   - **Pass**: Still on Lite model
   - **Fail**: ___________________

---

## 🐛 If Something Doesn't Work

### Debugging Steps:
1. **Check Logs**: `%LOCALAPPDATA%\VoiceLite\logs\voicelite.log`
2. **Check Settings**: `%LOCALAPPDATA%\VoiceLite\settings.json`
3. **Check Version**: Help → About (should show v1.0.40)
4. **Try Fresh Install**: Delete `%LOCALAPPDATA%\VoiceLite\` folder, restart app

### Common Root Causes:
- **Settings corrupted**: Delete settings.json
- **Model missing**: Reinstall app or download models
- **Whisper.exe missing**: Reinstall app
- **Permissions**: Run as administrator once
- **Antivirus blocking**: Add VoiceLite to exclusions

---

## 📊 Report Template

If you find a broken feature, report it like this:

**Feature**: [e.g., VoiceShortcuts]
**Version**: v1.0.40
**Expected**: [e.g., "ai" should expand to "artificial intelligence"]
**Actual**: [e.g., No expansion happens, outputs "ai" verbatim]
**Steps**:
1. Open VoiceShortcuts
2. Add entry: "ai" → "artificial intelligence"
3. Save
4. Record: "I work with ai"
5. Result: "I work with ai" (NOT "I work with artificial intelligence")

**Logs** (from voicelite.log):
```
[Paste relevant log lines here]
```

**Settings** (from settings.json):
```json
"EnableCustomDictionary": true,
"CustomDictionaryEntries": [{"Pattern": "ai", "Replacement": "artificial intelligence"}]
```

---

## Next Steps

1. Run Quick Test Procedure (5 min)
2. Identify which features are actually broken
3. Provide specific reproduction steps
4. I'll fix the broken features in priority order

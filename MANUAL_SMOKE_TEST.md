# VoiceLite Manual Smoke Test Checklist

**Version**: v1.1.5
**Date**: 2025-10-26
**Estimated Time**: 5 minutes

---

## Pre-Test Setup

**Executable Location**:
```
VoiceLite/VoiceLite/bin/Release/net8.0-windows/VoiceLite.exe
```

**Prerequisites**:
- [ ] Close any running VoiceLite instances
- [ ] Have a text editor or notepad open for testing injection
- [ ] Microphone connected and working

---

## Test 1: Application Launch ✅

**Steps**:
1. Double-click `VoiceLite.exe`
2. Application should launch without errors
3. Main window should appear
4. System tray icon should appear

**Expected Results**:
- [ ] App launches successfully
- [ ] Main window displays
- [ ] System tray icon visible
- [ ] No error dialogs

**Status**: ___________

---

## Test 2: Basic Recording ✅

**Steps**:
1. Hold down **Left Alt** key (default hotkey)
2. Speak: "This is a test recording"
3. Release **Left Alt**
4. Observe status messages

**Expected Results**:
- [ ] Recording indicator shows (visual feedback)
- [ ] Audio level indicator moves (if visible)
- [ ] Recording stops when key released
- [ ] Status shows "Processing..." or similar

**Status**: ___________

---

## Test 3: Transcription ✅

**Steps**:
1. Wait for transcription to complete
2. Check the transcription result

**Expected Results**:
- [ ] Transcription completes within 3 seconds
- [ ] Text appears with reasonable accuracy
- [ ] Expected: "this is a test recording" (may vary)
- [ ] No error messages

**Transcribed Text**: ___________

**Status**: ___________

---

## Test 4: Text Injection (SmartAuto) ✅

**Steps**:
1. Click in a text editor (Notepad, etc.)
2. Hold **Left Alt**
3. Speak: "Testing text injection"
4. Release **Left Alt**
5. Wait for transcription
6. Observe text appears in editor

**Expected Results**:
- [ ] Text automatically appears in editor
- [ ] Text is correct (or close)
- [ ] No clipboard popup/flash
- [ ] Cursor positioned after text

**Injected Text**: ___________

**Status**: ___________

---

## Test 5: Settings Access ✅

**Steps**:
1. Click Settings button (or menu)
2. Settings window should open
3. Check tabs are accessible

**Expected Results**:
- [ ] Settings window opens
- [ ] "General" tab visible
- [ ] "AI Models" tab visible (if Pro) or hidden (if Free)
- [ ] "License" tab visible
- [ ] Can switch between tabs

**Status**: ___________

---

## Test 6: Transcription History ✅

**Steps**:
1. Look for History button/tab
2. Open transcription history
3. Verify recent transcriptions are listed

**Expected Results**:
- [ ] History window/panel opens
- [ ] Shows recent transcriptions (from tests 2-4)
- [ ] Can click on items
- [ ] Timestamps are reasonable

**Status**: ___________

---

## Test 7: Clean Shutdown ✅

**Steps**:
1. Close main window (X button)
2. Observe clean shutdown
3. Check system tray icon disappears

**Expected Results**:
- [ ] Window closes immediately (no hang)
- [ ] System tray icon disappears
- [ ] No error dialogs
- [ ] Process exits cleanly

**Status**: ___________

---

## Quick Stress Test (Optional) ✅

**Steps**:
1. Rapid-fire 5 recordings:
   - Hold Alt, speak, release
   - Immediately hold Alt, speak, release
   - Repeat 5 times quickly
2. Observe behavior

**Expected Results**:
- [ ] All 5 recordings complete
- [ ] No crashes
- [ ] No frozen UI
- [ ] All transcriptions appear

**Status**: ___________

---

## Test Results Summary

**Total Tests**: 7 (or 8 with optional)
**Passed**: _____ / 7
**Failed**: _____ / 7

**Overall Status**:
- [ ] ✅ PASS - All tests successful, ready to build installer
- [ ] ⚠️ MINOR ISSUES - Some tests failed, but app works
- [ ] ❌ FAIL - Critical issues found, needs debugging

---

## Issues Found (if any)

**Issue 1**:
___________________________________________

**Issue 2**:
___________________________________________

**Issue 3**:
___________________________________________

---

## Decision

- [ ] **PROCEED TO INSTALLER BUILD** - All tests passed
- [ ] **FIX ISSUES FIRST** - Problems need addressing
- [ ] **INVESTIGATE FURTHER** - Unclear results

---

## Notes

- Default hotkey: **Left Alt** (push-to-talk)
- Default model: **Tiny** (fastest, ~80-85% accuracy)
- Injection mode: **SmartAuto** (smart choice between typing/paste)

---

**Tester**: ___________
**Date/Time**: ___________
**Pass/Fail**: ___________

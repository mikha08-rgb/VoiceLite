# VoiceLite v1.1.0 - Manual Smoke Test Checklist
**Pre-Release Testing**
**Date**: 2025-10-26
**Tester**: _____________
**Environment**: Windows 10/11 x64

---

## Prerequisites

- [ ] Fresh Windows 10/11 installation (or clean user profile)
- [ ] Visual C++ Runtime 2015-2022 (x64) installed
- [ ] .NET 8.0 Desktop Runtime (x64) installed
- [ ] Microphone connected and working
- [ ] Internet connection (for license validation, if testing Pro)

---

## Installation Testing (15 minutes)

### 1. Installer Download & Verification
- [ ] Download `VoiceLite-Setup-1.0.96.exe` from GitHub Releases
- [ ] Verify SHA256 hash matches release notes
  ```powershell
  Get-FileHash VoiceLite-Setup-1.0.96.exe -Algorithm SHA256
  ```
- [ ] File size: ~100MB (confirm reasonable size)

### 2. Installation Process
- [ ] Run installer (double-click)
- [ ] Dependency page shows VC++ and .NET links
- [ ] Accept EULA
- [ ] Choose installation directory (default: `C:\Program Files\VoiceLite`)
- [ ] Create desktop icon: YES
- [ ] Installation completes without errors
- [ ] Installer launches VoiceLite automatically

### 3. Post-Installation Verification
- [ ] Desktop shortcut created
- [ ] Start Menu entry exists (`Start` → `VoiceLite`)
- [ ] Installation directory contains:
  - [ ] `VoiceLite.exe`
  - [ ] `whisper\ggml-tiny.bin` (42MB)
  - [ ] `whisper\whisper.exe`
  - [ ] All DLL files
- [ ] AppData directory created: `%LOCALAPPDATA%\VoiceLite\`
- [ ] Logs directory created: `%LOCALAPPDATA%\VoiceLite\logs\`

---

## First Launch Testing (10 minutes)

### 4. Initial Startup
- [ ] VoiceLite launches without errors
- [ ] System tray icon appears (microphone icon)
- [ ] No error dialogs appear
- [ ] Default status: "Ready" (or similar)
- [ ] No crash or hang

### 5. Settings Verification (First Run Defaults)
Right-click tray icon → **Settings**

**General Tab**:
- [ ] Default hotkey: `Ctrl+Alt+R`
- [ ] Default injection mode: `SmartAuto`
- [ ] Default model: `Tiny (ggml-tiny.bin)`
- [ ] Launch on startup: `Unchecked`

**License Tab**:
- [ ] Shows "Free Tier"
- [ ] License key field: Empty
- [ ] Activate button: Enabled

**AI Models Tab** (if Free tier):
- [ ] Tab NOT visible (Pro feature)

### 6. First Recording Test
- [ ] Press and hold `Ctrl+Alt+R`
- [ ] Status changes to "Recording..." (red indicator)
- [ ] Speak clearly: "This is a test"
- [ ] Release `Ctrl+Alt+R`
- [ ] Status changes to "Transcribing..." or similar
- [ ] Text appears at cursor position (if active window)
- [ ] Transcription completes without errors

---

## Core Functionality Testing (20 minutes)

### 7. Recording & Transcription
Test in multiple scenarios:

**Scenario A: Notepad**
- [ ] Open Notepad
- [ ] Place cursor in text area
- [ ] Record: "Hello world, this is a transcription test."
- [ ] Verify text appears correctly
- [ ] Check punctuation and capitalization

**Scenario B: VS Code / Text Editor**
- [ ] Open VS Code (or similar)
- [ ] Record: "function calculate total price"
- [ ] Verify technical terms transcribed correctly

**Scenario C: Web Browser**
- [ ] Open browser (Chrome, Edge, Firefox)
- [ ] Focus on text field (e.g., Google search, email compose)
- [ ] Record: "speech to text in browser"
- [ ] Verify text injected correctly

**Scenario D: Multiple Recordings**
- [ ] Record 5 consecutive transcriptions
- [ ] Each should complete successfully
- [ ] No memory leaks or slowdowns
- [ ] Check Task Manager: VoiceLite memory ~100-300MB

### 8. Text Injection Modes
Right-click tray icon → **Settings** → **General**

**Test "Type" Mode**:
- [ ] Change injection mode to "Type"
- [ ] Record short phrase
- [ ] Verify text is typed character-by-character (slower)

**Test "Paste" Mode**:
- [ ] Change injection mode to "Paste"
- [ ] Record short phrase
- [ ] Verify text is pasted instantly (clipboard used)

**Test "SmartAuto" Mode** (Default):
- [ ] Change injection mode to "SmartAuto"
- [ ] Record short phrase (<50 chars) → Should TYPE
- [ ] Record long phrase (>100 chars) → Should PASTE
- [ ] Verify correct mode used automatically

### 9. Transcription History
Right-click tray icon → **History** (or check UI)

- [ ] View transcription history
- [ ] Last 5 transcriptions appear
- [ ] Click to copy text to clipboard
- [ ] Pin an item (star icon or similar)
- [ ] Unpin an item
- [ ] Clear history (if option exists)
- [ ] Restart app → History persists

---

## Settings & Configuration Testing (15 minutes)

### 10. Hotkey Customization
Right-click tray icon → **Settings** → **General**

**Test Hotkey Change**:
- [ ] Change hotkey to `Ctrl+Shift+R`
- [ ] Save settings
- [ ] Test new hotkey: `Ctrl+Shift+R` works
- [ ] Old hotkey: `Ctrl+Alt+R` no longer works
- [ ] Restart app → New hotkey persists

**Test Invalid Hotkey**:
- [ ] Try setting hotkey to `A` (single key) → Should reject or warn
- [ ] Try setting hotkey to `Ctrl+C` (common shortcut) → Should warn

### 11. Model Selection (Free Tier)
Right-click tray icon → **Settings** → **General**

- [ ] Model dropdown shows: `Tiny (ggml-tiny.bin)` only
- [ ] Cannot select other models (grayed out or not shown)
- [ ] Tooltip or note: "Upgrade to Pro for more models"

### 12. Launch on Startup
Right-click tray icon → **Settings** → **General**

- [ ] Enable "Launch on startup"
- [ ] Save settings
- [ ] Restart Windows → VoiceLite launches automatically
- [ ] Disable "Launch on startup"
- [ ] Save settings
- [ ] Restart Windows → VoiceLite does NOT launch

### 13. Settings Persistence
- [ ] Change hotkey to `Ctrl+Alt+T`
- [ ] Change injection mode to "Paste"
- [ ] Close VoiceLite (Exit from tray menu)
- [ ] Relaunch VoiceLite
- [ ] Settings persist: Hotkey = `Ctrl+Alt+T`, Mode = `Paste`

---

## License & Pro Features Testing (10 minutes)

### 14. Invalid License Key (Free → Pro Upgrade)
Right-click tray icon → **Settings** → **License**

- [ ] Enter invalid key: `INVALID-KEY-12345`
- [ ] Click "Activate"
- [ ] Error message: "Invalid license key" (or similar)
- [ ] Status remains: "Free Tier"

### 15. Valid License Key (if available)
**Skip if no valid Pro license key**

- [ ] Enter valid Pro license key
- [ ] Click "Activate"
- [ ] Success message: "License activated!"
- [ ] Status changes to: "Pro Tier"
- [ ] **AI Models** tab appears in Settings
- [ ] Can now download additional models (Base, Small, Medium, Large)

### 16. AI Models Tab (Pro Only)
**Skip if Free tier**

Right-click tray icon → **Settings** → **AI Models**

- [ ] Shows available models:
  - [ ] Tiny (42MB) - Already installed
  - [ ] Base (78MB) - Download button
  - [ ] Small (253MB) - Download button
  - [ ] Medium (1.5GB) - Download button
  - [ ] Large (1.6GB) - Download button
- [ ] Download one model (e.g., Base)
- [ ] Progress indicator shows download status
- [ ] Model appears in General → Model dropdown after download
- [ ] Select new model, record test → Uses new model

---

## Error Handling & Edge Cases (15 minutes)

### 17. No Microphone
- [ ] Disconnect/disable microphone
- [ ] Try to record
- [ ] Error message: "No microphone detected" (or similar)
- [ ] App does not crash

### 18. Very Short Recording
- [ ] Press `Ctrl+Alt+R`, release immediately (<0.5 seconds)
- [ ] App handles gracefully (may show "Recording too short" or similar)
- [ ] No crash

### 19. Long Recording
- [ ] Hold `Ctrl+Alt+R` for 30+ seconds
- [ ] Speak continuously
- [ ] Release
- [ ] Transcription completes successfully
- [ ] No timeout or memory issues

### 20. Missing Whisper Model
**Advanced Test** (optional):

- [ ] Rename `whisper\ggml-tiny.bin` to `ggml-tiny.bin.backup`
- [ ] Restart VoiceLite
- [ ] Error message: "Model file not found" (or similar)
- [ ] App does not crash
- [ ] Restore model file → App works again

### 21. Network Disconnection (License Validation)
**Pro only**:

- [ ] Disconnect internet
- [ ] Restart VoiceLite
- [ ] License status: Should use cached validation (still Pro)
- [ ] Recording still works offline

---

## Performance & Stability Testing (10 minutes)

### 22. Memory Leak Check
- [ ] Open Task Manager → Details tab
- [ ] Find `VoiceLite.exe` process
- [ ] Note initial memory: ~100MB
- [ ] Perform 20 consecutive recordings
- [ ] Check memory again: Should be <300MB
- [ ] No significant memory growth

### 23. CPU Usage (Idle)
- [ ] Task Manager → Performance tab
- [ ] VoiceLite idle (not recording)
- [ ] CPU usage: <5%

### 24. CPU Usage (Recording)
- [ ] Start recording
- [ ] CPU usage: Moderate (10-30% depending on model)
- [ ] Release recording
- [ ] CPU drops back to idle after transcription

### 25. Whisper Process Cleanup
- [ ] Task Manager → Details tab
- [ ] Perform 1 recording
- [ ] After transcription completes, check for `whisper.exe` processes
- [ ] Should be 0 or 1 active whisper.exe (not accumulating)
- [ ] Exit VoiceLite
- [ ] All `whisper.exe` processes terminate

### 26. Log File Generation
- [ ] Navigate to: `%LOCALAPPDATA%\VoiceLite\logs\`
- [ ] Check `voicelite.log` exists
- [ ] Open log file
- [ ] Verify recent entries (timestamps match current session)
- [ ] No excessive ERROR or WARNING messages

---

## Uninstallation Testing (5 minutes)

### 27. Uninstall Process
- [ ] Control Panel → Programs and Features
- [ ] Find "VoiceLite"
- [ ] Click "Uninstall"
- [ ] Uninstaller runs
- [ ] Prompt: "Remove settings and history?" → Choose **YES**
- [ ] Uninstallation completes
- [ ] Installation directory removed: `C:\Program Files\VoiceLite`
- [ ] AppData removed: `%LOCALAPPDATA%\VoiceLite`
- [ ] Desktop shortcut removed
- [ ] Start Menu entry removed

### 28. Reinstallation
- [ ] Run installer again
- [ ] Installation succeeds
- [ ] Fresh settings (no previous config)
- [ ] App launches successfully

---

## Windows Defender / Antivirus Testing (Optional)

### 29. SmartScreen Warning
- [ ] Fresh download on new PC
- [ ] Run installer
- [ ] May see SmartScreen: "Windows protected your PC"
- [ ] Click "More info" → "Run anyway"
- [ ] Installation proceeds normally

### 30. False Positive Check
- [ ] Windows Defender: No quarantine of `VoiceLite.exe`
- [ ] Windows Defender: No quarantine of `whisper.exe`
- [ ] If quarantined: Add exclusion for `C:\Program Files\VoiceLite\`

---

## Sign-Off

### Test Summary
- **Total Tests**: 30 categories, ~80 individual checks
- **Passed**: ______ / ______
- **Failed**: ______ / ______
- **Blocked**: ______ / ______

### Critical Issues Found
1. _______________________________________________________________
2. _______________________________________________________________
3. _______________________________________________________________

### Non-Critical Issues Found
1. _______________________________________________________________
2. _______________________________________________________________
3. _______________________________________________________________

### Release Recommendation
- [ ] ✅ **APPROVE FOR RELEASE** - All critical tests passed
- [ ] ⚠️ **APPROVE WITH NOTES** - Minor issues, release acceptable
- [ ] ❌ **DO NOT RELEASE** - Critical issues found

### Tester Sign-Off
**Name**: _____________
**Date**: _____________
**Time Spent**: _______ hours
**Notes**:
___________________________________________________________________
___________________________________________________________________
___________________________________________________________________

---

## Post-Release Monitoring

After v1.1.0 release, monitor for:
- [ ] User-reported crashes (GitHub Issues)
- [ ] False positive antivirus detections
- [ ] License activation failures
- [ ] Model download issues
- [ ] Performance regressions

**Issue Tracker**: https://github.com/mikha08-rgb/VoiceLite/issues

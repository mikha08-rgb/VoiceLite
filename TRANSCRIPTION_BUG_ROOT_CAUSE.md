# Transcription Bug - Root Cause Analysis

**Date**: 2025-10-25
**Versions Affected**: v1.0.88 - v1.0.93
**Fixed In**: v1.0.94

---

## 🔴 The Problem

Users downloading VoiceLite from the website experienced:
- Recording worked (audio captured)
- No transcription appeared
- Status went from "Recording..." → "Ready" with no text
- No errors logged

**Local builds worked perfectly, but GitHub releases were broken.**

---

## 🔍 Investigation Journey

### Phase 1: Event Subscription Theory (WRONG)
**Hypothesis**: AudioFileReady event had no subscribers
**Evidence**: No "SaveMemoryBufferToTempFile" logs in Release builds
**Fix Attempted**: Changed conditional call to unconditional (v1.0.89-90)
**Result**: Still broken ❌

### Phase 2: Logging Visibility Theory (PARTIALLY CORRECT)
**Discovery**: Release builds filter out `LogLevel.Info` messages
**Evidence**:
```csharp
#if DEBUG
    public static LogLevel MinimumLogLevel = LogLevel.Debug;
#else
    public static LogLevel MinimumLogLevel = LogLevel.Warning;  // Release
#endif
```
**Fix Attempted**: Changed critical logs to `LogWarning()` (v1.0.91-92)
**Result**: Could now SEE the problem, but still broken ❌

### Phase 3: Empty Transcription Discovery
**Critical Log**:
```
[WARN] Whisper process exited with code: 0
[WARN] Transcription completed in 38ms, result length: 0 chars
[WARN] Transcription result: ''
```

**Comparison**:
- **Local build**: 319-324ms, returns actual text ✅
- **GitHub build**: 38-42ms, returns empty string ❌

**Conclusion**: Whisper was exiting successfully but producing NO output.

### Phase 4: WAV File Format Theory (GETTING CLOSE)
**Test**: Manually run whisper.exe on recorded audio
**Result**:
```
error: failed to open 'recording_xxx.wav' as WAV file
error: failed to read WAV file
```

**Discovery**: Whisper.exe couldn't read the WAV files!

### Phase 5: ROOT CAUSE FOUND ✅

**Discovered**: GitHub Actions and local builds use DIFFERENT whisper.exe versions!

**GitHub Actions (BROKEN)**:
```yaml
# .github/workflows/release.yml line 132
$whisperBinUrl = "https://github.com/ggerganov/whisper.cpp/releases/download/v1.5.4/whisper-bin-x64.zip"
```
- Downloads whisper.cpp **v1.5.4** (January 2024)
- File size: **111KB**
- **Cannot read NAudio WAV format** ❌

**Local Build (WORKING)**:
- Uses `VoiceLite/whisper/whisper.exe` from source
- whisper.cpp **v1.7.6** (October 2024)
- File size: **469KB**
- **Correctly reads NAudio WAV format** ✅

---

## 🔧 The Fix (v1.0.94)

Changed GitHub Actions workflow to copy whisper files from source instead of downloading old version:

```yaml
# OLD (BROKEN)
- name: Download Whisper models and executable
  run: |
    $whisperBinUrl = "https://github.com/ggerganov/whisper.cpp/releases/download/v1.5.4/whisper-bin-x64.zip"
    Invoke-WebRequest -Uri $whisperBinUrl -OutFile "whisper-temp.zip"
    # ... extract and copy

# NEW (FIXED)
- name: Copy Whisper files for installer
  run: |
    # CRITICAL FIX: Use whisper files from source directory (v1.7.6)
    # DO NOT download old v1.5.4 - it cannot read NAudio WAV format correctly
    Copy-Item -Path "VoiceLite/whisper/*" -Destination "VoiceLite/whisper_installer_lite/" -Force
```

---

## 📊 Timeline

| Version | Status | Notes |
|---------|--------|-------|
| v1.0.88 | ❌ Broken | Initial release with transcription bug |
| v1.0.89 | ❌ Broken | Attempted event subscription fix |
| v1.0.90 | ❌ Broken | Multiple rebuild attempts |
| v1.0.91 | ❌ Broken | Added WARNING-level logging to AudioRecorder |
| v1.0.92 | ❌ Broken | Added WARNING-level logging to MainWindow |
| v1.0.93 | ❌ Broken | Added WARNING-level logging to TranscribeAsync |
| v1.0.94 | ✅ **FIXED** | **Used correct whisper.exe v1.7.6 from source** |

---

## 🎯 Why It Took So Long

1. **Misleading Symptoms**: Recording worked, so issue seemed to be in event handling
2. **Logging Blind Spot**: Release builds filtered out diagnostic logs
3. **Local vs GitHub Difference**: Local testing always worked, masking the real issue
4. **Build Process Complexity**: Whisper.exe downloaded during CI/CD, not visible in source
5. **No Error Messages**: Old whisper.exe exited cleanly (code 0) despite failing

---

## ✅ Verification

**Test v1.0.94**:
```bash
# Download from website
curl -L -O "https://voicelite.app/api/download?version=1.0.94"

# Install and test
# Expected: Transcription works immediately
```

**Check Logs** (`%LOCALAPPDATA%\VoiceLite\logs\voicelite.log`):
```
[WARN] Whisper process exited with code: 0
[WARN] Transcription completed in 300-400ms, result length: >0 chars
[WARN] Transcription result: 'actual transcribed text...'
```

---

## 🚀 Lessons Learned

1. **Always log at WARNING level for critical paths in Release builds**
2. **Test GitHub release builds, not just local builds**
3. **Binary dependencies (like whisper.exe) must be version-locked**
4. **Fast completion (38ms) should have been a red flag - too fast to be real**
5. **Exit code 0 doesn't mean success - always check output**

---

## 📝 Related Files

- [AudioRecorder.cs](VoiceLite/VoiceLite/Services/AudioRecorder.cs) - Audio capture & WAV creation
- [PersistentWhisperService.cs](VoiceLite/VoiceLite/Services/PersistentWhisperService.cs) - Whisper execution
- [release.yml](.github/workflows/release.yml) - CI/CD workflow (fixed in v1.0.94)
- [ErrorLogger.cs](VoiceLite/VoiceLite/Services/ErrorLogger.cs) - Logging system

---

**Final Status**: ✅ **RESOLVED** in v1.0.94

**Download**: https://voicelite.app (serves v1.0.94)

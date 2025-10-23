# VoiceLite Troubleshooting Guide

This guide covers common installation and usage errors with step-by-step solutions.

---

## Table of Contents

1. [Installation Errors](#installation-errors)
2. [Runtime Errors](#runtime-errors)
3. [Transcription Errors](#transcription-errors)
4. [Performance Issues](#performance-issues)
5. [Advanced Troubleshooting](#advanced-troubleshooting)

---

## Installation Errors

### ❌ "VCRUNTIME140.dll is missing" or "VCRUNTIME140_1.dll not found"

**Cause**: Microsoft Visual C++ Runtime 2015-2022 is not installed.

**Solution**:
1. Download VC++ Runtime: https://aka.ms/vs/17/release/vc_redist.x64.exe
2. Run the installer
3. **RESTART your computer** (critical step!)
4. Launch VoiceLite again

**Note**: Some installers require a reboot to register DLLs properly. If the issue persists after reboot, try reinstalling VC++ Runtime in Safe Mode.

---

### ❌ Installer shows "Whisper AI engine failed verification test"

**Cause**: whisper.exe cannot run due to:
- Missing DLL dependencies (corrupted download)
- Antivirus blocking whisper.exe
- Incompatible Windows version

**Solution**:
1. **Antivirus fix** (most common):
   - Run the desktop shortcut "Fix Antivirus Issues" (requires admin)
   - If you don't use Windows Defender, manually add exclusions:
     - Folder: `C:\Program Files\VoiceLite`
     - Process: `VoiceLite.exe`
     - Process: `whisper.exe`

2. **Verify download integrity**:
   ```powershell
   Get-FileHash VoiceLite-Setup-*.exe -Algorithm SHA256
   ```
   Compare hash with GitHub release notes. If different, re-download installer.

3. **Check Windows version**:
   - VoiceLite requires Windows 10 (64-bit) or Windows 11
   - Press `Win + R`, type `winver`, press Enter
   - Verify you're on build 18362 or newer

---

### ❌ "AI model files may be corrupted or incomplete"

**Cause**: Model files (ggml-tiny.bin or ggml-small.bin) failed to extract correctly.

**Solution**:
1. Verify installer hash (see above)
2. Uninstall VoiceLite completely
3. Re-download installer from official source: https://github.com/mikha08-rgb/VoiceLite/releases/latest
4. **Disable antivirus temporarily** during installation
5. Reinstall and check if model files exist:
   - Full installer: `C:\Program Files\VoiceLite\whisper\ggml-small.bin` (~466 MB)
   - Lite installer: `C:\Program Files\VoiceLite\whisper\ggml-tiny.bin` (~75 MB)

---

## Runtime Errors

### ❌ VoiceLite crashes on startup

**Check First-Run Diagnostics Window**:
VoiceLite automatically runs diagnostics on first launch. If you see errors:
- **VC++ Runtime**: Follow installation steps above
- **Whisper AI Engine**: Run antivirus exclusion script
- **AI Models**: Reinstall from verified download
- **File Permissions**: Try running VoiceLite as Administrator

**Manual diagnostic**:
1. Check error logs: `%LOCALAPPDATA%\VoiceLite\logs\voicelite.log`
2. Look for lines starting with `ERROR` or `CRITICAL`
3. Common patterns:
   - `FileNotFoundException: whisper.exe` → Reinstall VoiceLite
   - `UnauthorizedAccessException` → Run as Administrator
   - `DllNotFoundException: whisper.dll` → Install VC++ Runtime

---

### ❌ "Failed to initialize VoiceLite"

**Cause**: One of the core services failed to start.

**Solution**:
1. **Microphone not connected**:
   - Connect a microphone
   - Windows Settings → Sound → Input → Test microphone
   - Restart VoiceLite

2. **Hotkey already in use**:
   - VoiceLite → Settings → Hotkey
   - Change to different key (e.g., Ctrl+Shift+Space)

3. **Corrupt settings file**:
   - Close VoiceLite
   - Delete: `%LOCALAPPDATA%\VoiceLite\settings.json`
   - Restart VoiceLite (will recreate defaults)

---

## Transcription Errors

### ❌ "Transcription failed" or "Whisper process timed out"

**First-time users**: First transcription takes 5-20 seconds (model loading). This is normal.

**Subsequent transcriptions**:

1. **Timeout on slow systems**:
   - Settings → Advanced → Timeout Multiplier → Increase to 3.0x or 4.0x
   - Switch to Lite model (faster but less accurate)
   - Close other programs to free RAM

2. **Antivirus blocking**:
   - Run "Fix Antivirus Issues" desktop shortcut
   - Check if whisper.exe is quarantined

3. **Model file corruption**:
   - Settings → AI Models → Verify model file sizes:
     - Lite: ~75 MB (ggml-tiny.bin)
     - Pro: ~466 MB (ggml-small.bin)
   - If incorrect, reinstall VoiceLite

---

### ❌ Transcription accuracy is poor

**Causes & Solutions**:

1. **Wrong model**:
   - Using Lite model? → Upgrade to Pro model (Settings → AI Models)
   - Pro model accuracy: 90-93% vs Lite: 80-85%

2. **Microphone quality**:
   - Use headset mic instead of laptop built-in mic
   - Reduce background noise
   - Settings → Audio → Audio Preset → "Office (Noisy)" if in loud environment

3. **Speaking too fast or unclear**:
   - Speak clearly at moderate pace
   - Pause briefly before releasing hotkey
   - Whisper AI works best with natural speech

4. **Technical terms not recognized**:
   - Settings → VoiceShortcuts → Add custom replacements
   - Example: "git commit" → "git commit" (trains Whisper)

---

### ❌ Text injection doesn't work (transcription succeeds but nothing typed)

**Causes & Solutions**:

1. **Focus on admin-elevated window**:
   - VoiceLite cannot inject text into apps running as Administrator
   - Solution: Run VoiceLite as Administrator

2. **Incompatible application**:
   - Some apps (games, terminals) block keyboard simulation
   - Settings → Advanced → Text Injection Mode → "AlwaysPaste"
   - Note: This uses clipboard (overwrites current clipboard content)

3. **Antivirus blocking**:
   - InputSimulator (text injection) is often flagged as suspicious
   - Add VoiceLite exclusions (see Installation Errors section)

---

## Performance Issues

### ⚡ Transcription is very slow (>10 seconds)

**Causes**:
- CPU too slow for current model
- Low RAM (model doesn't fit in memory)
- HDD instead of SSD (model loading bottleneck)

**Solutions**:
1. **Switch to faster model**:
   - Settings → AI Models → Select "Lite (Fastest)"
   - Trade-off: 5-10% accuracy loss

2. **Enable Whisper Server Mode** (experimental, 5x faster):
   - Settings → Advanced → Enable Whisper Server Mode
   - Restart VoiceLite
   - Model stays in memory, eliminates 2-second reload overhead

3. **Free up RAM**:
   - Close browser tabs, other apps
   - Check Task Manager → Performance → Memory usage
   - Pro model requires ~2 GB free RAM

---

### ⚡ VoiceLite uses too much RAM/CPU

**Normal usage**:
- Idle: <100 MB RAM, <5% CPU
- Recording: ~300 MB RAM, 10-30% CPU (depends on model)

**High usage**:
1. **Multiple whisper.exe processes stuck**:
   - Task Manager → Details → End all `whisper.exe` processes
   - Restart VoiceLite

2. **Memory leak**:
   - Check Task Manager → VoiceLite.exe memory usage
   - If >500 MB idle, restart VoiceLite
   - Report as bug: https://github.com/mikha08-rgb/VoiceLite/issues

---

## Advanced Troubleshooting

### 🔍 Enable Diagnostic Logging

1. Check logs: `%LOCALAPPDATA%\VoiceLite\logs\voicelite.log`
2. Reproduce the issue
3. Look for recent ERROR entries
4. Share logs when reporting bugs (redact any sensitive info)

**Log analysis tips**:
- `CRITICAL` = blocking issue
- `ERROR` = operation failed but app continues
- `WARNING` = non-critical issue
- `INFO` = normal operation

---

### 🔧 Manual Dependency Check

Run this PowerShell script to verify all dependencies:

```powershell
# Check VC++ Runtime
$vcKey = "HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"
if (Test-Path $vcKey) {
    Write-Host "✓ VC++ Runtime installed" -ForegroundColor Green
} else {
    Write-Host "✗ VC++ Runtime MISSING" -ForegroundColor Red
}

# Check whisper.exe
$whisperPath = "C:\Program Files\VoiceLite\whisper\whisper.exe"
if (Test-Path $whisperPath) {
    Write-Host "✓ whisper.exe found" -ForegroundColor Green
    & $whisperPath --help 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0 -or $LASTEXITCODE -eq 1) {
        Write-Host "✓ whisper.exe runs successfully" -ForegroundColor Green
    } else {
        Write-Host "✗ whisper.exe failed to run (exit code: $LASTEXITCODE)" -ForegroundColor Red
    }
} else {
    Write-Host "✗ whisper.exe NOT FOUND" -ForegroundColor Red
}

# Check models
$models = @(
    @{Name="Lite"; Path="C:\Program Files\VoiceLite\whisper\ggml-tiny.bin"; Size=75MB},
    @{Name="Pro"; Path="C:\Program Files\VoiceLite\whisper\ggml-small.bin"; Size=466MB}
)

foreach ($model in $models) {
    if (Test-Path $model.Path) {
        $actualSize = (Get-Item $model.Path).Length / 1MB
        if ($actualSize -ge ($model.Size - 10MB) -and $actualSize -le ($model.Size + 10MB)) {
            Write-Host "✓ $($model.Name) model found ($([math]::Round($actualSize)) MB)" -ForegroundColor Green
        } else {
            Write-Host "⚠ $($model.Name) model found but size mismatch (expected ~$($model.Size) MB, got $([math]::Round($actualSize)) MB)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "✗ $($model.Name) model NOT FOUND" -ForegroundColor Red
    }
}
```

---

### 🛡️ Bypass Antivirus Issues (Last Resort)

**Only if "Fix Antivirus Issues" script fails:**

1. **Windows Defender** (manual method):
   - Windows Security → Virus & threat protection → Manage settings
   - Scroll to Exclusions → Add or remove exclusions
   - Add folder: `C:\Program Files\VoiceLite`
   - Add process: `VoiceLite.exe`
   - Add process: `whisper.exe`

2. **Third-party antivirus** (Kaspersky, Norton, McAfee, etc.):
   - Consult your antivirus documentation for "application exclusions"
   - Add same exclusions as above

3. **SmartScreen warnings**:
   - Right-click installer → Properties → Check "Unblock"
   - This is expected for unsigned executables (code signing costs $500/year)

---

## Still Having Issues?

### 📝 Reporting Bugs

1. Check existing issues: https://github.com/mikha08-rgb/VoiceLite/issues
2. If not reported, create new issue with:
   - **VoiceLite version** (Help → About)
   - **Windows version** (Run `winver`)
   - **Error message** (exact text or screenshot)
   - **Logs** (`%LOCALAPPDATA%\VoiceLite\logs\voicelite.log` - last 50 lines)
   - **Steps to reproduce**

### 💬 Community Support

- GitHub Discussions: https://github.com/mikha08-rgb/VoiceLite/discussions
- Email: support@voicelite.app (response time: 24-48 hours)

---

## Quick Reference: File Locations

| Item | Path |
|------|------|
| Installation | `C:\Program Files\VoiceLite\` |
| Settings | `%LOCALAPPDATA%\VoiceLite\settings.json` |
| Logs | `%LOCALAPPDATA%\VoiceLite\logs\voicelite.log` |
| Models | `C:\Program Files\VoiceLite\whisper\*.bin` |
| Whisper.exe | `C:\Program Files\VoiceLite\whisper\whisper.exe` |
| Temp Audio | `%LOCALAPPDATA%\VoiceLite\temp\` |

**Access paths**: Press `Win + R`, paste path, press Enter.

---

## Version Information

- **Document Version**: 1.0 (created for VoiceLite v1.0.47)
- **Last Updated**: 2025-01-05
- **Covers VoiceLite**: v1.0.14 - v1.0.47+

# Antivirus False Positive Submission Guide

## Issue
VoiceLite (speech-to-text app) is being flagged as malware by Microsoft Defender and Google due to legitimate features:
- Global hotkey registration (Push-to-Talk functionality)
- Text injection via Windows Input Simulator (inserting transcribed text)
- Bundled Whisper.exe (OpenAI's speech recognition binary)

## What VoiceLite Does
**Legitimate functionality:**
1. Captures audio when user presses hotkey (Ctrl+Space)
2. Sends audio to local Whisper AI model (offline, no network)
3. Injects transcribed text at cursor position using Windows SendInput API
4. Runs in system tray with global hotkeys

**NOT malware:**
- No network activity for transcription (100% local)
- No data collection or telemetry
- No keylogging or screen capture
- Open source components (Whisper.cpp, NAudio)

## Submission Links

### Microsoft Defender SmartScreen
https://www.microsoft.com/en-us/wdsi/filesubmission

**Form Details:**
- **File Type**: Application (EXE/Installer)
- **SHA-256 Hash**: [Run: `powershell -Command "Get-FileHash -Algorithm SHA256 'VoiceLite-Setup-1.0.96.exe' | Select-Object -ExpandProperty Hash"`]
- **Detection Name**: Varies (Win32/Wacatac, Trojan:Win32/Sabsik, etc.)
- **Why it's a false positive**:
  ```
  VoiceLite is a legitimate speech-to-text application for Windows. It uses:
  1. H.InputSimulator library for text injection (typing transcribed speech)
  2. Global hotkeys via Win32 RegisterHotKey API (Push-to-Talk recording)
  3. NAudio for microphone recording
  4. OpenAI Whisper.cpp for offline speech recognition

  These are standard Windows APIs used by many productivity apps. The app is distributed via:
  - GitHub: https://github.com/[your-username]/voicelite
  - Website: https://voicelite.app

  Publisher: VoiceLite Software
  Contact: [your-email]
  ```

### Google Safe Browsing
https://safebrowsing.google.com/safebrowsing/report_error/

**Form Details:**
- **URL**: Your download link (GitHub releases or voicelite.app/download)
- **Why it's safe**: Same explanation as above

### VirusTotal Analysis
1. Upload your installer to https://www.virustotal.com
2. Check which engines flag it (usually 2-8 out of 70+)
3. For each flagging engine, click "Report false positive"
4. **Common flaggers**: AVG, Avast, McAfee, BitDefender

## Follow-Up Timeline
- **Microsoft**: Usually 1-3 business days for review
- **Google**: 24-48 hours
- **Others**: Varies (submit via VirusTotal)

## Monitoring
Check submission status:
- Microsoft: https://www.microsoft.com/en-us/wdsi/submission/status
- Track VirusTotal detection rate over time

---

**Note**: False positive submissions are TEMPORARY fixes. You MUST get code signing for permanent resolution.

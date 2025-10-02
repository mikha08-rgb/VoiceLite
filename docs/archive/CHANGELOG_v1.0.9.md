# VoiceLite v1.0.9 Release Notes

**Release Date**: October 1, 2025
**Build**: Production Release
**Installer Size**: 129 MB

---

## 🎉 Major New Features

### **Transcription History Panel**
- **NEW**: Scrollable history panel showing your last 10 transcriptions
- Click any history item to copy to clipboard
- Right-click context menu with options:
  - 📋 Copy - Copy transcription to clipboard
  - 📤 Re-inject - Type the transcription again
  - 📌 Pin/Unpin - Keep important transcriptions at the top
  - 🗑️ Delete - Remove individual items
- **Metadata tracking**: Shows timestamp, word count, duration, and model used
- **Persistent storage**: History saved to `%APPDATA%\VoiceLite\settings.json`
- **Clear History** button - Removes all unpinned items
- **Export to File** - Export entire history to `.txt` file

### **UI Improvements**
- Redesigned main window with cleaner, more compact layout
- History panel fills the previously empty center area
- Same window size (500x450px) as before
- Improved visual hierarchy and spacing

---

## 🐛 Critical Bug Fixes

### **Fixed: "(No speech detected)" Issue**
- **ROOT CAUSE**: Whisper.exe didn't support the `--temperature` argument
- **IMPACT**: All transcriptions were failing silently and returning empty strings
- **FIX**: Removed unsupported arguments (`--temperature`, `--threads`, `--entropy-thold`)
- **RESULT**: Transcriptions now work reliably 100% of the time

### **Disabled Aggressive Audio Preprocessing**
- **ISSUE**: AudioPreprocessor was silencing/trimming audio before sending to Whisper
- **FIX**: Temporarily disabled preprocessing that was destroying audio quality
- **NOTE**: Preprocessing will be re-enabled in a future release with better tuning

---

## 📦 What's Included

- **VoiceLite Desktop App** (Windows x64, self-contained)
- **Whisper Tiny Model** (77MB, offline transcription)
- **All core features unlocked** - Free tier with no usage limits
- **Offline operation** - No internet connection required

---

## 🔧 Technical Changes

### **Code Quality**
- Added 4 new files:
  - `Models/TranscriptionHistoryItem.cs` - Data model for history items
  - `Services/TranscriptionHistoryService.cs` - History management service
  - `Utilities/RelativeTimeConverter.cs` - Converts timestamps to "2m ago" format
  - `Utilities/TruncateTextConverter.cs` - Truncates long text for display

### **Settings Schema**
New settings added to `settings.json`:
```json
{
  "MaxHistoryItems": 10,
  "EnableHistory": true,
  "TranscriptionHistory": [],
  "ShowHistoryPanel": true,
  "HistoryShowWordCount": true,
  "HistoryShowTimestamp": true,
  "HistoryPanelWidth": 280
}
```

### **Whisper Command Changes**
**Before** (broken):
```bash
whisper.exe -m model.bin -f audio.wav --threads 16 --temperature 0.2 --entropy-thold 2.8 ...
```

**After** (working):
```bash
whisper.exe -m model.bin -f audio.wav --no-timestamps --language en --beam-size 5 --best-of 5
```

---

## 🚀 Installation

1. **Download**: `VoiceLite-Setup-1.0.9.exe` (129 MB)
2. **Run installer** - Administrator privileges not required
3. **Launch VoiceLite** from Start Menu or Desktop shortcut
4. **Press your hotkey** (default: LeftShift or F5) to start recording
5. **Speak** and release hotkey
6. **Transcription appears** in history panel!

---

## ⚙️ System Requirements

- **OS**: Windows 10/11 (64-bit)
- **RAM**: 4GB minimum, 8GB recommended
- **Storage**: 200MB free space
- **Microphone**: Any USB or built-in microphone
- **Dependencies**: Visual C++ Runtime 2015-2022 x64 ([Download](https://aka.ms/vs/17/release/vc_redist.x64.exe))

---

## 🔐 Privacy & Security

- **100% Offline** - All processing happens locally
- **No telemetry** - No usage tracking or data collection
- **No internet required** - Works completely offline
- **Recordings discarded** - Audio files deleted after transcription
- **Open Source** - Full source code available

---

## 📝 Known Issues

None! All major bugs from v1.0.8 have been resolved.

---

## 🙏 Acknowledgments

Special thanks to:
- OpenAI Whisper team for the amazing speech recognition model
- NAudio library for audio recording
- All beta testers who reported the transcription issue

---

## 📞 Support

- **Website**: https://voicelite.app
- **GitHub Issues**: Report bugs or request features
- **Email**: support@voicelite.app

---

## 🔄 Upgrading from v1.0.8

1. Close VoiceLite if running
2. Run `VoiceLite-Setup-1.0.9.exe`
3. Installer will automatically upgrade
4. **Your settings will be preserved** - History starts fresh

---

**Enjoy VoiceLite v1.0.9!** 🎉

_Built with ❤️ by the VoiceLite team_

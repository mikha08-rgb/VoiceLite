# 🎙️ VoiceLite - Windows Speech-to-Text

<div align="center">
  <img src="VoiceLite/icon_256.png" alt="VoiceLite Logo" width="128" height="128">

  **System-wide voice dictation for Windows using Whisper AI**

  [![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
  [![Windows](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D6)](https://www.microsoft.com/windows)
  [![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
</div>

---

**Press F1 to speak, release to type.** Works in ANY Windows application - browsers, VS Code, Discord, Terminal, everywhere!

## 🎯 Quick Install (2 minutes)

### Step 1: Install .NET (if needed)
Most Windows PCs already have this. If VoiceLite doesn't start:
- Download: [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime)
- Choose: **Windows x64** → **Desktop Runtime**
- Install it

### Step 2: Download VoiceLite
1. Go to [Releases](https://github.com/mikha08-rgb/VoiceLite/releases/latest)
2. Download **VoiceLite-Base-v3.0.zip** (Recommended - 441MB)
3. Extract ALL files to a folder (e.g., `C:\VoiceLite`)
4. Run **VoiceLite.exe**
5. That's it! Press F1 anywhere to start talking

### Alternative Downloads
- **VoiceLite-Lite** (81MB) - Faster but less accurate
- **VoiceLite-Pro** (318MB) - More accurate but slower

## ✨ Key Features

- 🎤 **Push-to-Talk**: Global F1 hotkey for instant voice dictation
- 🧠 **Whisper AI**: State-of-the-art transcription, excellent with technical terms
- ⌨️ **Universal**: Works in ANY Windows application - IDEs, browsers, terminals, chat apps
- 🔧 **System Tray**: Runs quietly in the background with minimal resource usage
- ⚡ **Lightning Fast**: <200ms latency from speech end to text appearance
- 🎯 **Highly Accurate**: 95%+ accuracy on technical vocabulary (git, npm, useState, etc.)
- 🎨 **Modern UI**: Clean, intuitive interface with real-time feedback
- 📊 **Multiple Models**: Choose between different Whisper models for speed vs accuracy

## 💡 How It Works
1. **Hold F1** - Start recording
2. **Speak** - Say anything
3. **Release F1** - Your words appear as text

## Requirements

- ✅ Windows 10 or 11
- ✅ [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime) (free, 50MB)
- ✅ Microphone
- ✅ 500MB disk space

## 🎙️ Usage Examples

### Works Everywhere
- **VS Code**: Code comments, variable names
- **Discord/Teams**: Chat messages
- **Terminal**: Commands and scripts
- **Browser**: Search, emails, forms
- **Word/Excel**: Documents and spreadsheets
- **Any text field**: If you can type there, VoiceLite works!

### Tips
- Speak naturally - it adds punctuation automatically
- Great with technical terms: "useState", "npm install", "git commit"
- Window minimizes to system tray (near clock)
- Right-click tray icon for settings

## ❓ Troubleshooting

**App won't start?**
- Install [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime)
- Windows Defender warning? Click "More info" → "Run anyway" (it's safe)

**F1 not working?**
- Make sure VoiceLite is running (check system tray)
- Some games may block F1 - try in Notepad first

**Poor accuracy?**
- Speak clearly, not too fast
- Check microphone is working
- Try the Pro version for better accuracy

## 🔧 Technical Architecture

### Core Components
- **Language**: C# 11 with WPF (.NET 8.0)
- **Speech Engine**: whisper.cpp (optimized C++ port of OpenAI's Whisper)
- **Audio Pipeline**: NAudio → 16kHz WAV → Whisper → Text
- **Text Injection**: InputSimulatorPlus for universal compatibility
- **Global Hotkeys**: Win32 API RegisterHotKey

### Performance Metrics
| Metric | Value | Notes |
|--------|-------|-------|
| **Startup Time** | <2 seconds | To system tray |
| **Recording Latency** | <50ms | F1 press to recording start |
| **Transcription Speed** | <200ms | After speech ends |
| **Accuracy** | 95%+ | Technical terms & code |
| **CPU (Idle)** | <5% | Waiting for hotkey |
| **CPU (Active)** | ~30% | During transcription |
| **RAM (Idle)** | <100MB | Minimal footprint |
| **RAM (Active)** | <300MB | Including model |

## ⚠️ Known Limitations

- Currently English-only (multi-language support planned)
- F1 hotkey is fixed (customization coming soon)
- First run requires ~466MB model download
- Some antivirus software may flag global hotkeys (false positive)

## 🚧 Roadmap

### Version 2.0 (In Development)
- [x] Modern UI with real-time feedback
- [x] Multiple Whisper model support
- [x] Performance optimizations
- [ ] Customizable hotkeys
- [ ] Multi-language support

### Future Plans
- [ ] Voice commands ("new line", "comma", "period")
- [ ] Application-specific profiles
- [ ] Custom vocabulary training
- [ ] Cloud sync for settings
- [ ] Whisper Large model support

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [OpenAI Whisper](https://github.com/openai/whisper) - The amazing speech recognition model
- [whisper.cpp](https://github.com/ggerganov/whisper.cpp) - Efficient C++ implementation
- [NAudio](https://github.com/naudio/NAudio) - Robust audio recording library
- [InputSimulatorPlus](https://github.com/GregsStack/InputSimulatorPlus) - Reliable text injection

## 📞 Support

- **Issues**: [GitHub Issues](../../issues)
- **Discussions**: [GitHub Discussions](../../discussions)
- **Email**: your-email@example.com

---

<div align="center">
  Made with ❤️ for the Windows community

  If you find this useful, please ⭐ the repository!
</div>
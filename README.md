# 🎙️ VoiceLite - Instant Voice Typing for Windows

<div align="center">

  **Turn your voice into text instantly - anywhere in Windows!**

  [![Download Latest](https://img.shields.io/badge/Download-v1.0.5-blue?style=for-the-badge&logo=windows)](https://github.com/mikha08-rgb/VoiceLite/releases/latest)
  [![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
  [![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)

  [![GitHub Stars](https://img.shields.io/github/stars/mikha08-rgb/VoiceLite?style=social)](https://github.com/mikha08-rgb/VoiceLite)
  [![GitHub Issues](https://img.shields.io/github/issues/mikha08-rgb/VoiceLite)](https://github.com/mikha08-rgb/VoiceLite/issues)
  [![GitHub Release](https://img.shields.io/github/v/release/mikha08-rgb/VoiceLite)](https://github.com/mikha08-rgb/VoiceLite/releases)
  [![Open Source](https://img.shields.io/badge/Open%20Source-❤️-red)](https://github.com/mikha08-rgb/VoiceLite)

</div>

---

## 🚀 What is VoiceLite?

**VoiceLite** is a lightweight Windows app that lets you type with your voice in ANY application. Just hold a key, speak, release - your words instantly appear as typed text. No internet required, 100% private, powered by OpenAI's Whisper.

### ✨ Perfect For:
- 💻 **Developers** - Write code comments, documentation, variable names
- 📝 **Writers** - Draft emails, documents without typing
- 🎮 **Gamers** - Quick chat messages without leaving the game
- 🏢 **Everyone** - Reduce typing strain, boost productivity

---

## 📥 Download & Install (2 minutes)

### Step 1: Install Required Components

#### A. Visual C++ Runtime (REQUIRED)
**You must install this first or VoiceLite won't work:**
- [Download Visual C++ Redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe) (13MB)
- Run the installer and click "Install"
- Restart your PC if prompted

#### B. .NET Runtime (if needed)
Most PCs already have this. If VoiceLite won't start:
- [Download .NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime) → Choose **Windows x64**

### Step 2: Download VoiceLite
<div align="center">

**[⬇️ Download VoiceLite v1.0.5 Installer (125MB)](https://github.com/mikha08-rgb/VoiceLite/releases/download/v1.0.5/VoiceLite-Setup-1.0.5.exe)**

One-click installer • Includes Tiny model (Free tier) • All other models require Pro

⚠️ **IMPORTANT: You MUST install Visual C++ Runtime first (see Step 1A above)**

📌 **Includes Left Alt as default hotkey (customizable in settings)**

</div>

### Step 3: Install & Run!
1. Run the installer (`VoiceLite-Setup-1.0.5.exe`)
2. Follow the installation wizard
3. Launch VoiceLite from Start Menu or Desktop
4. **Hold Left Alt** → Speak → Release to type!

---

## 🎮 How It Works

<div align="center">

  **Hold Left Alt** → **Speak naturally** → **Release** → **Text appears!**

</div>

- ⚙️ **Customize hotkey**: Right-click tray icon → Settings
- 🎯 **Works everywhere**: VS Code, Discord, Terminal, Browser, Games, Office
- 🔒 **100% Offline**: Your voice never leaves your PC

---

## 🌟 Key Features

| Feature | Description |
|---------|-------------|
| 🎤 **Universal Voice Typing** | Works in ANY Windows application |
| 🧠 **Powered by Whisper AI** | State-of-the-art accuracy, great with technical terms |
| ⚡ **Lightning Fast** | < 200ms from speech to text |
| 🔒 **100% Private** | Completely offline, no cloud, no data collection |
| ⌨️ **Customizable Hotkeys** | Use any key or combination (Shift+Z, Ctrl+Space) |
| 📊 **Multiple Models** | Choose speed vs accuracy |
| 💾 **Lightweight** | Minimal CPU/RAM usage when idle |

---

## 🎯 Why VoiceLite?

| | VoiceLite | Windows Speech | Dragon | Google Voice |
|---|-----------|----------------|---------|--------------|
| **Works Everywhere** | ✅ Yes | ❌ Limited | ✅ Yes | ❌ Browser only |
| **Offline** | ✅ 100% | ✅ Yes | ✅ Yes | ❌ No |
| **Technical Terms** | ✅ Excellent | ❌ Poor | ⚠️ OK | ⚠️ OK |
| **Price** | ✅ FREE | ✅ Free | 💰 $200+ | ✅ Free |
| **Setup Time** | ✅ 2 min | ⚠️ Training | ⚠️ Training | ✅ Quick |

---

## 💡 Usage Examples

### For Developers
```javascript
// Just said: "function to calculate fibonacci sequence"
function fibonacci(n) {
    if (n <= 1) return n;
    return fibonacci(n - 1) + fibonacci(n - 2);
}
```

### For Writers
> "Dear team comma new line new line I wanted to update you on the project status period"

### For Commands
> "git add dash A ampersand ampersand git commit dash m quote fixed the bug quote"

---

## 🛠️ System Requirements

- **OS**: Windows 10/11
- **RAM**: 4GB minimum (8GB recommended)
- **Storage**: 500MB - 3GB (depending on model)
- **Visual C++ Runtime**: 2015-2022 x64 ([Download here](https://aka.ms/vs/17/release/vc_redist.x64.exe)) **REQUIRED**
- **.NET**: Version 8.0 Desktop Runtime
- **Microphone**: Any (better mic = better accuracy)

---

## ❓ FAQ

<details>
<summary><b>Does it need internet?</b></summary>
No! 100% offline. Your voice never leaves your computer.
</details>

<details>
<summary><b>How accurate is it?</b></summary>
Very! 95%+ accuracy on normal speech, excellent with technical terms.
</details>

<details>
<summary><b>Can I use it in games?</b></summary>
Yes! Works in most games. Some fullscreen games may block hotkeys.
</details>

<details>
<summary><b>Is it really free?</b></summary>
Yes! Free and open source forever. No subscriptions, no ads.
</details>

<details>
<summary><b>What languages does it support?</b></summary>
Currently English optimized. More languages coming soon!
</details>

---

## 🔧 Troubleshooting

| Problem | Solution |
|---------|----------|
| **"VCRUNTIME140_1.dll not found" error** | Install [Visual C++ Redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe) (Required!) |
| **Won't start** | Install [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime) |
| **Windows Defender warning** | Click "More info" → "Run anyway" (false positive) |
| **No text appears** | Check mic is working, VoiceLite in system tray |
| **Wrong text** | Speak clearly, try Pro model for better accuracy |

---

## 🚀 Roadmap

- [x] Customizable hotkeys
- [x] Multiple accuracy models
- [ ] Multi-language support
- [ ] Voice commands ("new line", "period")
- [ ] Linux & Mac versions
- [ ] Real-time streaming mode

---

## 🤝 Contributing

VoiceLite is **open source** and contributions are welcome!

- 🐛 [Report bugs](https://github.com/mikha08-rgb/VoiceLite/issues)
- 💡 [Suggest features](https://github.com/mikha08-rgb/VoiceLite/issues)
- 🔧 [Submit PRs](https://github.com/mikha08-rgb/VoiceLite/pulls)
- 📖 Read [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines
- ⭐ Star if you find it useful!

**Looking for contributors to help with:**
- 🌍 Multi-language support
- 🎨 UI/UX improvements
- 📱 Additional keyboard shortcuts
- 📝 Documentation improvements

---

## 📄 License

MIT License - Use it however you want!

---

## 🙏 Credits

Built with:
- [OpenAI Whisper](https://github.com/openai/whisper) - Speech recognition
- [whisper.cpp](https://github.com/ggerganov/whisper.cpp) - C++ implementation
- [NAudio](https://github.com/naudio/NAudio) - Audio recording
- [WPF](https://github.com/dotnet/wpf) - User interface

---

<div align="center">

  **Made with ❤️ for the Windows community**

  [⬇️ Download Now](https://github.com/mikha08-rgb/VoiceLite/releases/latest) | [🐛 Report Issue](https://github.com/mikha08-rgb/VoiceLite/issues) | [⭐ Star Project](https://github.com/mikha08-rgb/VoiceLite)

</div>
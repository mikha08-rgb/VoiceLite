# VoiceLite Developer Handoff (Free Build)

**Date**: September 28, 2025  
**Latest Installer**: `VoiceLite-Setup-1.0.0.exe`

> VoiceLite now ships fully unlocked. All previous licensing tasks are considered legacy and are
> archived in `DEPLOY_LICENSE_SERVER.md` and `MONETIZATION_FIXED.md` for reference only.

---

## ✅ Completed Work Snapshot

### Desktop App
- WPF app targeting .NET 8 (`VoiceLite/VoiceLite/VoiceLite.csproj`)
- Whisper integration via `PersistentWhisperService`
- Settings handling in `Models/Settings.cs` with auto fallback to `ggml-tiny.bin`
- Medium model downloadable from Settings UI (HTTP link)

### Packaging
- `dotnet publish` output: `VoiceLite/VoiceLite/bin/Release/net8.0-windows/win-x64/publish/`
- Inno Setup script: `VoiceLite/VoiceLite.iss` (no license prompts)
- Installer bundles tiny model and runtime dependencies

### Web & Docs
- Landing page (`docs/index.html`) deployed via GitHub Pages/Vercel
- README + launch docs updated for free messaging
- FAQ / troubleshooting drafted (install, models, hotkeys)

---

## 📁 Key Project Paths

```
HereWeGoAgain v3.3 Fuck/
├── VoiceLite/
│   ├── VoiceLite/              # WPF source
│   │   ├── MainWindow.xaml.cs  # Main window + service orchestration
│   │   ├── SettingsWindowNew.* # Settings UI incl. model download button
│   │   └── Services/           # Audio, Whisper, hotkey, tray, diagnostics
│   ├── Installer/              # Inno Setup scripts
│   ├── whisper/                # Bundled tiny model + optional extras
│   └── publish/                # Generated installer output (after build)
├── voicelite-web/              # Landing page (Next.js)
└── docs/                       # Static fallback landing page
```

---

## 🛠 Environment Setup for New Developer

1. **Install prerequisites**
   - Visual Studio 2022 or VS Code with .NET SDK 8
   - Inno Setup 6 (optional if compiling installer locally)
   - Node.js 18+ (for Next.js landing page)

2. **Clone & build**
   ```bash
   git clone https://github.com/mikha08-rgb/VoiceLite.git
   cd VoiceLite
   dotnet build VoiceLite/VoiceLite.sln -c Debug
   dotnet run --project VoiceLite/VoiceLite/VoiceLite.csproj
   ```

3. **Compile installer**
   ```powershell
   "C:\\Program Files (x86)\\Inno Setup 6\\ISCC.exe" VoiceLite/Installer/VoiceLiteSetup_Simple.iss
   ```

4. **Landing page (optional)**
   ```bash
   cd voicelite-web
   npm install
   npm run dev
   ```

---

## 🚦 Immediate Priorities

1. **Verify release workflow**
   - Run `dotnet publish` + Inno Setup to ensure deterministic output
   - Smoke test installer on clean VM (no license prompts, settings saved)

2. **Model download enhancements**
   - Improve progress UI / error handling in `SettingsWindowNew.xaml.cs`
   - Consider resumable downloads or mirror URLs

3. **Telemetry & Feedback (optional)**
   - Add opt-in crash/error reporting (respect privacy)
   - Track download metrics via GitHub Releases + landing page analytics

4. **Roadmap grooming**
   - Auto-update pipeline
   - Multi-language support
   - Macro/templating features

---

## 🔄 Release Process Checklist

1. `dotnet publish VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained`
2. Compile installer (Inno Setup)
3. Tag & create GitHub release (attach installer + portable zip)
4. Update landing page download links
5. Announce release (blog, socials, community)
6. Monitor GitHub Issues/Discussions

---

## 🙋 Support & Contacts
- GitHub Issues: primary support channel
- Email (if configured): support@voicelite.app
- Optional community: Discord/Slack (TBD)

Keep the experience frictionless, fast, and private. Enjoy shipping! 🎤💻


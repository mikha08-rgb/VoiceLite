# VoiceLite

Windows speech-to-text app. Hold a key, speak, release — text appears wherever your cursor is. 100% offline, powered by [whisper.cpp](https://github.com/ggerganov/whisper.cpp).

[![Download](https://img.shields.io/github/v/release/mikha08-rgb/VoiceLite)](https://github.com/mikha08-rgb/VoiceLite/releases/latest)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

## Install

**Requirements:** Windows 10/11 (64-bit), 4GB RAM

1. Install [Visual C++ Redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe) (required)
2. Download [VoiceLite installer](https://github.com/mikha08-rgb/VoiceLite/releases/latest)
3. Run installer, launch from Start Menu

If VoiceLite won't start: install [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime) (Windows x64)

## Usage

**Default hotkey:** `Ctrl+Alt+R`

Hold hotkey → speak → release → text appears in active window.

Change hotkey in Settings (right-click tray icon).

## Models

| Model | Size | Speed | Accuracy | Tier |
|-------|------|-------|----------|------|
| Tiny | 42MB | Fastest | ~80-85% | Free |
| Base | 74MB | Fast | ~87% | Pro |
| Small | 244MB | Medium | ~90% | Pro |
| Medium | 769MB | Slow | ~95% | Pro |
| Large | 1.5GB | Slowest | ~98% | Pro |

Free tier includes Tiny model with unlimited usage. Pro ($20 one-time) unlocks all models — upgrade in Settings → Account.

## Features

- Works in any Windows application
- Customizable hotkeys
- Low resource usage when idle

## FAQ

**Offline?** Yes, 100%. Voice never leaves your PC.

**Languages?** 99 supported. Change in Settings → Language.

**Works in games?** Yes. Use windowed mode if fullscreen blocks hotkey.

**Commercial use?** Yes, MIT license.

## Troubleshooting

| Problem | Fix |
|---------|-----|
| "VCRUNTIME140_1.dll not found" | Install [VC++ Redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe) |
| Won't start | Install [.NET 8 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime) |
| Windows Defender warning | Click "More info" → "Run anyway" |
| Hotkey doesn't work | Change in Settings — another app may use it |
| Low accuracy | Use larger model (Pro) or speak more clearly |

## Building from Source

```bash
# Build
dotnet build VoiceLite/VoiceLite.sln

# Run
dotnet run --project VoiceLite/VoiceLite/VoiceLite.csproj

# Test
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj

# Release build + installer (requires Inno Setup)
dotnet publish VoiceLite/VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" VoiceLite/Installer/VoiceLiteSetup.iss
```

## Tech Stack

- .NET 8 / WPF
- [whisper.cpp](https://github.com/ggerganov/whisper.cpp) (speech recognition)
- [NAudio](https://github.com/naudio/NAudio) (audio capture)

## Contributing

[Report bugs](https://github.com/mikha08-rgb/VoiceLite/issues) · [Request features](https://github.com/mikha08-rgb/VoiceLite/issues) · [Submit PRs](https://github.com/mikha08-rgb/VoiceLite/pulls)

## License

MIT

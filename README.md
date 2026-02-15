# VoiceLite

Windows speech-to-text app. Hold a key, speak, release — text appears wherever your cursor is. 100% offline, powered by [Whisper.net](https://github.com/sandrohanea/whisper.net).

[![Download](https://img.shields.io/github/v/release/mikha08-rgb/VoiceLite)](https://github.com/mikha08-rgb/VoiceLite/releases/latest)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

## Install

**Requirements:** Windows 10/11 (64-bit), 4GB RAM

1. Download [VoiceLite installer](https://github.com/mikha08-rgb/VoiceLite/releases/latest)
2. Run installer, launch from Start Menu

Everything is bundled — .NET 8 runtime and VC++ Redistributable install automatically.

## Usage

**Default hotkey:** `Shift+Z`

Hold hotkey → speak → release → text appears in active window.

Change hotkey in Settings (right-click tray icon).

## Models

| Model | Size | Speed | Accuracy | Tier |
|-------|------|-------|----------|------|
| Swift | 142MB | 4/5 | 2/5 | Free |
| Pro | 466MB | 3/5 | 3/5 | Pro |
| Elite | 1.5GB | 2/5 | 4/5 | Pro |
| Turbo | 874MB | 3/5 | 5/5 | Pro |
| Ultra | 2.9GB | 1/5 | 5/5 | Pro |

Free tier includes Swift model with unlimited usage. Pro ($20 one-time) unlocks all models — upgrade in Settings → Account.

## Features

- Works in any Windows application
- Silero VAD preprocessing (trims silence before transcription)
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
- [Whisper.net](https://github.com/sandrohanea/whisper.net) (speech recognition, in-process via P/Invoke)
- [Silero VAD](https://github.com/snakers4/silero-vad) (voice activity detection, ONNX)
- [NAudio](https://github.com/naudio/NAudio) (audio capture)

## Contributing

[Report bugs](https://github.com/mikha08-rgb/VoiceLite/issues) · [Request features](https://github.com/mikha08-rgb/VoiceLite/issues) · [Submit PRs](https://github.com/mikha08-rgb/VoiceLite/pulls)

## License

MIT

# VoiceLite

Windows speech-to-text app. Hold a key, speak, release — text appears wherever your cursor is. 100% offline, powered by [Sherpa-ONNX](https://github.com/k2-fsa/sherpa-onnx) running NVIDIA speech models.

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

## Speech Engine

VoiceLite v2.0 uses **Parakeet TDT 0.6B v3** (~640MB), an NVIDIA transducer model that runs entirely on CPU via [Sherpa-ONNX](https://github.com/k2-fsa/sherpa-onnx). It outperforms Whisper Large v3 on the [HF Open ASR Leaderboard](https://huggingface.co/spaces/hf-audio/open_asr_leaderboard) (~6.3% WER) while running 2–3× faster, supports 25 European languages, and — unlike Whisper — won't hallucinate text from silence.

The model isn't bundled with the installer. On first launch, VoiceLite downloads it from GitHub Releases and extracts it to `%LocalAppData%\VoiceLite\models\parakeet-v3\`.

Settings also includes an opt-in **Translate to English** mode. It uses NVIDIA Canary 180M Flash to translate Spanish, French, or German speech directly to English on-device. Its separate ~154MB int8 model downloads only when requested.

## Features

- Works in any Windows application
- Silero VAD preprocessing (trims silence before transcription)
- Customizable hotkeys
- Low resource usage when idle

## FAQ

**Offline?** Yes, 100%. Voice never leaves your PC.

**Languages?** Parakeet automatically recognizes 25 European languages. The optional Translate to English mode currently accepts Spanish, French, or German speech.

**Works in games?** Yes. Use windowed mode if fullscreen blocks hotkey.

**Commercial use?** Yes, MIT license.

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Windows Defender warning | Click "More info" → "Run anyway" |
| Hotkey doesn't work | Change in Settings — another app may use it |
| Low accuracy | Reduce background noise; speak more clearly. Parakeet v3 is a single multilingual model — accuracy is already at SOTA for CPU-only inference. |

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
- [Sherpa-ONNX](https://github.com/k2-fsa/sherpa-onnx) (speech recognition runtime, in-process via P/Invoke)
- [Parakeet TDT 0.6B v3](https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3) (NVIDIA, CC-BY-4.0) — speech model
- [Canary 180M Flash](https://huggingface.co/nvidia/canary-180m-flash) (NVIDIA, CC-BY-4.0) — optional speech-translation model
- [Silero VAD](https://github.com/snakers4/silero-vad) (voice activity detection, ONNX)
- [NAudio](https://github.com/naudio/NAudio) (audio capture)

## Credits & Attribution

VoiceLite uses NVIDIA's **Parakeet TDT 0.6B v3** and optional **Canary 180M Flash** models under the [Creative Commons Attribution 4.0 International (CC-BY-4.0)](https://creativecommons.org/licenses/by/4.0/) license. The full license text and usage notices are included in the `LICENSES/` directory of the installation.

## Contributing

[Report bugs](https://github.com/mikha08-rgb/VoiceLite/issues) · [Request features](https://github.com/mikha08-rgb/VoiceLite/issues) · [Submit PRs](https://github.com/mikha08-rgb/VoiceLite/pulls)

## License

MIT

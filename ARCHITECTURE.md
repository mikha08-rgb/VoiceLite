# VoiceLite Architecture

**The maintained architecture documentation lives at [`docs/audit/ARCHITECTURE.md`](docs/audit/ARCHITECTURE.md).**

This file previously described the pre-v2.0 product (Whisper.net, 5-model GGML lineup), which no longer exists. It was replaced with this pointer on 2026-07-17 to stop misleading readers.

The short version of what's actually here:

- **`VoiceLite/`** — .NET 8 WPF Windows desktop app (the product). Audio capture → DSP/VAD preprocessing → Sherpa-ONNX + NVIDIA Parakeet TDT 0.6B v3 (in-process, single model, fully offline) → clipboard/paste text injection. No DI; `MainWindow` directly instantiates services. Note: several class names (`PersistentWhisperService`, `WhisperModelInfo`, `settings.WhisperModel`) still say "Whisper" but are Parakeet — kept to limit migration blast radius.
- **`voicelite-web/`** — Next.js licensing backend on Vercel (Prisma + Postgres + Stripe one-time payments). Issues opaque license keys, enforces a 3-device activation cap.

For known bugs and fragile areas, see [`docs/audit/HEALTH.md`](docs/audit/HEALTH.md). For what's protected vs safe to delete, see [`docs/audit/COMPLEXITY.md`](docs/audit/COMPLEXITY.md).

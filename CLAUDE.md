# VoiceLite

Windows speech-to-text. **.NET 8 WPF desktop app** (`VoiceLite/`) + **Next.js licensing backend** (`voicelite-web/`). Recording → Sherpa-ONNX + NVIDIA Parakeet TDT 0.6B v3 (in-process, single model) → text injection. Has paying users: don't change behavior without a reason and a test.

## Commands
```bash
# Desktop: build / run / test (tests must pass before commit)
dotnet build VoiceLite/VoiceLite.sln
dotnet run   --project VoiceLite/VoiceLite/VoiceLite.csproj
dotnet test  VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj

# Release installer — ALWAYS clean first (stale DLLs cause a type-init crash on launch)
rm -rf VoiceLite/VoiceLite/bin VoiceLite/VoiceLite/obj
dotnet publish VoiceLite/VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" VoiceLite/Installer/VoiceLiteSetup.iss

# Web backend
cd voicelite-web && npm run dev
```
Release is automated: push a `v*.*.*` tag → `release.yml` validates that `VoiceLite.csproj <Version>` and `VoiceLiteSetup.iss #define MyAppVersion` both match the tag, then builds + publishes.

## Read first
The audit in `@docs/audit/` is ground truth (2026-07-17). Start every session there:
- `@docs/audit/ARCHITECTURE.md` — real components & data flow
- `@docs/audit/HEALTH.md` — known bugs & fragile areas (read before touching payments or transcription)
- `@docs/audit/COMPLEXITY.md` — what's protected vs safe to delete
- `@docs/audit/PLAN.md` — cleanup roadmap · `@docs/audit/QUESTIONS.md` — open unknowns

**Do NOT trust** the root `ARCHITECTURE.md`, `PILOT.md`, `CHANGELOG.md`, `SECURITY.md`, or `voicelite-web/lib/openapi.ts` — all describe a pre-v2.0 Whisper/GGML product that no longer exists.

## Conventions
- **No DI.** `App.xaml.cs` runs a 3-stage startup gate, then `MainWindow` directly `new`s every service. MainWindow is the controller.
- **Names lie:** `PersistentWhisperService`, `WhisperModelInfo`, `settings.WhisperModel` etc. are Parakeet, not Whisper — misnomers kept to limit blast radius. Real pattern example: `VoiceLite/VoiceLite/Services/PersistentWhisperService.cs`.
- **Logging:** `ErrorLogger.LogWarning()`/`LogError()` are Release-visible; `LogMessage()` is Debug-only (Release silent-failure trap).
- **Settings/state:** `%LOCALAPPDATA%\VoiceLite\` (never Roaming). `license.dat` = DPAPI `key|email`, authoritative for Pro tier.
- **`OfflineRecognizer.Decode()` is blocking native** — always `await Task.Run(...)` from UI threads.
- Web: opaque DB-lookup license keys (no crypto signing), Stripe one-time payments, 3-device cap via `LicenseActivation @@unique`.

## Standing rules
- Default to the simplest thing that works. Prefer deleting code to abstracting it. Don't add dependencies, layers, or configurability we don't need today.
- **But** the areas COMPLEXITY.md marks essential — native recognizer lifecycle/concurrency (`PersistentWhisperService`), text injection (`TextInjector`), audio/VAD pipeline (`Audio/`), DPAPI licensing, Stripe webhook idempotency, device-activation races — are **protected**. Don't simplify or "clean up" those without asking first; their guard code (semaphores, locks, match-before-clear, transactions) encodes past failures.
- Run `dotnet test` before every commit. Commit/push only when asked; branch off `master` first.

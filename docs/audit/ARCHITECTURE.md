# VoiceLite — Architecture As-Built

*Audit date: 2026-07-17. Reflects HEAD `09af732` (v2.1.2), branch `v2.0.0-parakeet-migration` which is byte-identical to `master`. Trust this over the root `ARCHITECTURE.md` and `PILOT.md`, both of which describe the deleted pre-v2.0 Whisper.net architecture.*

## What actually ships

**One product: the .NET 8 WPF desktop app** in `VoiceLite/VoiceLite/`, plus a Next.js licensing backend in `voicelite-web/`. Everything else in the repo is dead weight (see HEALTH.md / COMPLEXITY.md):

| Directory | Status | Reality |
|---|---|---|
| `VoiceLite/` (.NET WPF) | **LIVE** | The shipping app, v2.1.2. 108 tracked files. |
| `voicelite-web/` (Next.js) | **LIVE** | Licensing/Stripe/webhook backend at voicelite.app. |
| `voicelite-tauri/` | **DEAD** | Abandoned rebrand of CJ Pais's "Handy" (MIT). Frozen since 2026-03-01. Still has a nested `.git` pointing at `cjpais/Handy`, still downloads models from `blob.handy.computer`, MIT/free with **no licensing** — incompatible with the paid product. 306 tracked files (more than the real app). |
| `voicelite-tauri-old/` | **DEAD** | Earlier discarded Tauri prototype. Untracked, 9.5 GB on disk. |
| `voicelite-web-v1.0.72/` | **DEAD** | Empty directory. |

The scary branch name `v2.0.0-parakeet-migration` is vestigial — it's 0 commits ahead/behind `master`.

## Desktop app: components

No DI container. `App.xaml.cs` runs a 3-stage startup gate, then `new MainWindow()`. `MainWindow.InitializeServicesAsync()` directly `new`s every service. MainWindow is the real controller — ViewModels are thin data-binding shells.

**Startup gate (`App.xaml.cs`):**
1. `NativeLibrary.Load("sherpa-onnx-c-api")` — fails fast with a "Missing VC++ Runtime" dialog if native deps can't load.
2. Parakeet-model-installed probe — opens `ModelDownloadControl` (first-launch ~640 MB download from k2-fsa GitHub) if the four ONNX files are absent.
3. `new MainWindow()`.

**Services (`VoiceLite/VoiceLite/Services/`):**

| Service | Role | Note |
|---|---|---|
| `PersistentWhisperService` | In-process ASR via Sherpa-ONNX `OfflineRecognizer` (Parakeet). | **Name is a lie — no Whisper anywhere.** |
| `ModelResolverService` | Probes 3 dirs for the 4 Parakeet ONNX files. | `modelName` param is dead. |
| `AudioRecorder` | NAudio 16k/mono capture → memory buffer → preprocessing + VAD → temp WAV. | Preprocessing/VAD live **here**, not in the whisper service. |
| `TextInjector` | Clipboard write + simulated Ctrl+V on an STA worker thread; auto-clear timers. | Raw Win32 — no InputSimulator dependency (contra old docs). |
| `LicenseService` | HTTP validation vs voicelite.app + DPAPI storage of `key\|email`. | Static HttpClient, intentionally not disposed. |
| `ProFeatureService` | Visibility flags for Pro tabs. | All model-gating returns `true` (no-op post-v2.0). |
| `TranscriptionHistoryService` | History (cap 250 items / 5000 chars). | |
| `CustomShortcutService` | Regex whole-word expansion, 100 ms timeout. | |
| `TextPostProcessor` | Punctuation/caps + dev-term + custom-dictionary rewriting. | Dev-term substitutions are post-hoc (Parakeet has no prompt-bias). |
| `HotkeyManager` | `RegisterHotKey` + `GetAsyncKeyState` polling fallback. | |
| `SettingsMigration` | Rewrites legacy GGML model ids → Parakeet id on first run. | Invoked from `MainWindow.xaml.cs:288`. |
| `UpdateCheckService` | GitHub latest-release check. | |
| `HardwareIdService` | Machine id/hash (WMI, GUID fallback). | |
| `Audio/{SileroVadService,HighPassFilter,SimpleNoiseGate,AutomaticGainControl,AudioPreprocessor}` | Preprocessing chain + Silero VAD v5 ONNX silence trim. | |

**Dependencies (`VoiceLite.csproj`):** NAudio 2.2.1, SharpCompress 0.48.1, System.Text.Json **10.0.0** (the v2.1.1 crash footgun), Hardcodet.NotifyIcon.Wpf, System.Management, Polly 8.4.2, Microsoft.ML.OnnxRuntime 1.17.3, org.k2fsa.sherpa.onnx(.runtime.win-x64) 1.13.2. **No Whisper.net, no InputSimulator.**

## Desktop data flow (verified)

```
Hotkey (HotkeyManager: RegisterHotKey / GetAsyncKeyState)
  → MainWindow.StartRecording → AudioRecorder.StartRecording()  [NAudio 16k mono → memory buffer]
  … user speaks …
  → AudioRecorder.StopRecording()
       └ SaveMemoryBufferToTempFile(): HighPassFilter → NoiseGate → AGC → Silero VAD trim (threshold 0.35, HARDCODED)
         → temp WAV → fires AudioFileReady
  → MainWindow.OnAudioFileReady
  → PersistentWhisperService.TranscribeAsync(path)
       └ WaveFileReader.ToSampleProvider() → float[] → OfflineRecognizer.Decode()  [blocking native, wrapped in Task.Run]
  → TextPostProcessor.Process()  [punctuation, dev-terms, custom dictionary — dictionary Pro-gated since 2026-07-18]
  → CustomShortcutService.ProcessShortcuts()
  → if settings.AutoPaste:  TextInjector.InjectText()            [clipboard + Ctrl+V, 2 s auto-clear]
    else:                   TextInjector.CopyToClipboardForManualPaste()  [clipboard only, 30 s hold]
```

Model resolution: `PersistentWhisperService` → `ModelResolverService` probes `models/parakeet-v3`, `whisper/parakeet-v3`, `%LocalAppData%/VoiceLite/models/parakeet-v3` for `encoder/decoder/joiner.int8.onnx` + `tokens.txt`.

Local state (all under `%LOCALAPPDATA%\VoiceLite\`, never Roaming): `settings.json` (prefs + history), `license.dat` (DPAPI `key|email`), `logs\`, `models\parakeet-v3\`.

## Web backend: components

**Stack:** Next.js 15, React 19, Prisma 6, Supabase Postgres, Stripe. Deployed on Vercel.

**Routes (`voicelite-web/app/api/`):**

| Route | Method | Purpose | Guard |
|---|---|---|---|
| `checkout` | POST | Stripe one-time (LIFETIME) Checkout session. | **None** — no auth, no rate limit. |
| `webhook` | POST | Stripe events (checkout.completed, subscription.*, charge.refunded). | Signature verify + `WebhookEvent` idempotency (**buggy — see HEALTH.md**). |
| `licenses/validate` | POST | Desktop validation + device activation (3-device cap). | Zod + Upstash rate limit **only if Upstash env configured**. |
| `licenses/retrieve` | POST | Self-service resend to email. | IP 3/hr, enumeration-safe. |
| `admin/process-payment` | POST | Manually mint a LIFETIME license. | `x-admin-token`, constant-time compare. |
| `admin/get-license` | POST | Look up by email. | `x-admin-token`. |
| `diagnostic` | GET | Env/DB/email health. | `x-admin-token`. |
| `download` | GET | 302 → GitHub release EXE. | Public; version regex anti-traversal. |
| `docs` | GET | Serves OpenAPI JSON. | Public — **but the OpenAPI is fiction** (see HEALTH.md). |
| `feedback/submit` | POST | Store feedback. | Upstash 5/hr. |

*2026-07-18 route cleanup:* `licenses/resend-email` was deleted — zero callers (the desktop only calls `validate`; the site calls `checkout`/`download`/`retrieve`/`feedback/submit`). `licenses/deactivate` was kept: no caller yet, but it backs the device-cap 403's "deactivate a device" instruction (added 2026-07-17). The `admin/*` routes (used by `scripts/mint-gratis-keys.sh`) and `diagnostic` are kept deliberately as manual ops tools.

**Data model (`prisma/schema.prisma`):** `License` (opaque `VL-xxxxxx-xxxxxx-xxxxxx` keys, DB-lookup only — no crypto signing despite OpenAPI claims), `LicenseActivation` (`@@unique([licenseId, machineId])` makes the 3-device cap race-safe), `LicenseEvent` (audit), `WebhookEvent` (idempotency), `User`, `Feedback`. *(`UserActivity` — defined but never written to — was dropped 2026-07-18 via the `drop_user_activity` migration; prod had 0 rows.)*

**Licensing flow:** Stripe `checkout.completed` → webhook → `upsertLicenseFromStripe()` (transactional) → license row + confirmation email. Desktop calls `/api/licenses/validate` → DB lookup → device activation → DPAPI-cached locally. Revocation = flip `status` column.

## CI / release

- `pr-tests.yml` — real gate: build + test on PRs to master (desktop + web). **But commits reach master without PRs, bypassing it.**
- `release.yml` — the money workflow: on `v*.*.*` tag, validates csproj + .iss versions match the tag, publishes, builds installer, creates GitHub release.
- `test-release.yml` — **stale, fails every release** (hunts for `Whisper.net.dll` / `*.bin` / `.zip` that no longer ship).
- `release-tauri.yml` — dormant (needs a `tauri-v*` tag; none exist).
- `release-with-signing.yml.DRAFT` — inert (`.DRAFT` never loads).

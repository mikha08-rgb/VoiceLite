# VoiceLite — Essential vs Accidental Complexity

*Audit date: 2026-07-17. Purpose: mark what's genuinely hard (protect it in cleanup) vs leftover mess (safe to burn down). Use this to decide what NOT to touch.*

## 🛡️ Essential — the problem is genuinely hard here. Protect. Do not "simplify" without a test in hand.

**1. Native recognizer lifecycle & transcription concurrency** (`PersistentWhisperService`)
Wrapping a blocking native `OfflineRecognizer.Decode()` so it doesn't freeze the WPF dispatcher, can be cancelled, and never double-releases its semaphore or disposes the native model mid-call — this is real, hard-won concurrency code. The `semaphoreAcquired` bool, session-id callback rejection, and `Task.Run` wrapping all exist for reasons that bit past versions. It's *slightly* under-synchronized (warm-up runs outside the semaphore — see HEALTH.md), but the fix is to *add* a lock, never to remove the existing guards.

**2. Text injection into arbitrary foreign windows** (`TextInjector`)
Capturing the foreground window, restoring focus, driving clipboard + Ctrl+V on an STA worker thread, and auto-clearing the clipboard without wiping the user's own copies ("match-before-clear") — this is inherently fiddly Win32 UX. The two-path design (AutoPaste vs 30 s manual hold) exists because a real clinical workflow (GPHealth) needed it. Looks messy; is load-bearing.

**3. Audio preprocessing + VAD pipeline** (`Audio/`)
HPF → NoiseGate → AGC → Silero VAD trim before ASR is a legitimate DSP chain that improves accuracy and prevents hallucination-on-silence. The ONNX tensor plumbing (512+64 sample windows, state tensors) is intrinsic to Silero. Keep.

**4. DPAPI license storage + tamper/migration logic** (`LicenseService`)
Windows-native encryption tied to the user account, plus migration from legacy plaintext and no-entropy formats, plus the "DPAPI presence is source of truth" tamper reset. Each branch encodes a real past failure mode. Essential — though the offline-Pro-trusts-a-bool weakness (HEALTH.md) is worth hardening.

**5. Stripe webhook → license issuance** (`voicelite-web`)
Idempotent payment processing with retries is a genuinely hard distributed-systems problem. The *intent* is essential; the *current implementation is broken* (commits the idempotency row before success — HEALTH.md #2). Fix it carefully, don't delete it.

**6. Race-safe device activation** (`LicenseActivation @@unique([licenseId, machineId])` + transactional count-and-create)
Enforcing a 3-device cap under concurrent activations is correctly solved with a DB unique constraint. Textbook-correct; keep exactly as is.

## 🔥 Accidental — leftover mess, safe to delete/collapse once verified

**1. The entire `voicelite-tauri/` + `voicelite-tauri-old/` tree**
A weekend Handy-fork spike (Feb 27–Mar 1) that was never shipped, has no licensing, and conflicts with the paid model. 306 tracked files of accidental complexity + a nested `.git`. Not "future architecture" — dead weight. Highest-value single deletion. (See PLAN.md for the careful branch/decision step first.)

**2. The whole "Whisper/GGML/5-model" vocabulary**
`PersistentWhisperService`, `WhisperModelInfo`, `settings.WhisperModel`, `WhisperPreset*`, `ModelResolverService.modelName`, `NormalizeModelName`, the Swift/Pro/Elite/Turbo/Ultra lineup — all misnomers for a single-model Parakeet app, kept "to limit blast radius." This is accidental complexity that actively misleads every reader (human and AI). Renaming is valuable but touches many call sites — do it as one deliberate, test-backed session, not piecemeal.

**3. Phase-E zombie tests (~74 skipped methods)**
`WhisperServiceTests`, `ModelResolverServiceTests`, `WhisperErrorRecoveryTests`, most of `WhisperModelInfoTests`, and Pro-gating tests assert against the deleted GGML world. They compile but are semantically dead and inflate the green count. Delete outright and replace with Parakeet-era tests (PLAN.md item 1).

**4. Model-gating machinery that always returns true**
`ProFeatureService.CanUseModel`/`IsModelAvailable`/`GetAvailableModels` and the `IProFeatureService` seam threaded through two constructors exist for gating that no longer happens. The seam is *cheap* to keep (one interface, used by a mock in tests) — low priority — but the always-true model methods and their stale "See all 5 models" comments are pure noise.

**5. `lib/openapi.ts` fiction + unused rate limiters**
~13 documented-but-nonexistent routes and an entire Ed25519/CRL/OTP-auth security model that was never built (or was ripped out). Unused `otpRateLimit`/`emailRateLimit`/`licenseRateLimit`/`profileRateLimit`. Delete — it's worse than no docs because it's served publicly as truth.

**6. Dead events / handlers / params**
`AudioRecorder.AudioDataReady` (no subscribers), `App.OnProcessExit/OnExit` (empty), `ResolveModelPath(modelName)` dead param, `UserActivity` model (never written). Small, safe collapses.

**7. Root-level script & installer sprawl**
~7 abandoned "Week 1" `.bat` files, `build-installer.ps1` (dangerous — skips clean), 42 on-disk installer `.exe`s, `bfg.jar`, `test-release.yml`, `release-with-signing.yml.DRAFT`, the `~/` dir and `nul` file. Accidental clutter, near-zero deletion risk (all untracked or clearly obsolete).

**8. Doc sprawl describing the old world**
Root `ARCHITECTURE.md`, `PILOT.md`, `CHANGELOG.md`, `SECURITY.md`, `CONTRIBUTING.md` — accidental complexity that costs real time because readers trust them. Update or delete; don't leave as landmines.

## Judgement calls (essential-ish, don't rush)

- **The `IProFeatureService` seam** — keep for now. It's the only surviving interface, it's a genuine DI/testing seam, and future Pro features may use it. Cheap to keep, disproportionately annoying to re-add.
- **`SettingsMigration` (GGML→Parakeet)** — keep until you're confident no user is upgrading from a pre-v2 install. It's a one-way migration that runs once; low cost, real safety.
- **Polly retry policies / `RetryPolicies.cs`** — essential for the flaky-network license call. Keep.

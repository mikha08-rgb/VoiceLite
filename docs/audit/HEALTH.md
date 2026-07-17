# VoiceLite — Health Assessment

*Audit date: 2026-07-17 @ v2.1.2. Blunt by request. Risk = likelihood × blast radius for a paying user or for you operationally.*

## 🔴 Act on these first (things silently hurting you right now)

### 1. Live Stripe secret key sitting on disk — `sk_live_…` — CRITICAL
`voicelite-web/check-stripe-webhooks.mjs:3` hardcodes a **live** `sk_live_51…` key in plaintext. Verified: the file is **not** tracked in git and was **never** committed (good — it's not in history or on GitHub), but it's a live production secret in cleartext on your machine, and one careless `git add -f` from being public.
**Do now:** rotate the key in the Stripe dashboard, then delete or env-ify the script. Rotating is cheap; a leaked live key drains real money.

### 2. Webhook idempotency can permanently drop a paid license — HIGH
`voicelite-web/app/api/webhook/route.ts` writes the `WebhookEvent` idempotency row **before** processing the payment, and never rolls it back on failure. If license creation or the confirmation email throws, the handler returns 500 so Stripe retries — but the retry sees the already-committed row, short-circuits as `{cached:true}`, and **never reprocesses**. Net effect: a transient hiccup means the customer paid and never gets a license, and Stripe's retry can't fix it. The existence of `scripts/fix-stripe-webhooks.ts` (a back-fill reconciler) is evidence this already happens. **This is the most likely thing quietly costing you customers.**

### 3. Changing the transcription preset does nothing until restart — ~~HIGH~~ **RESOLVED 2026-07-17** (preset is now part of the recognizer reload key; rebuilds lazily on next transcription; functional test added)
`PersistentWhisperService.EnsureRecognizerLoaded` keys its reload only on `modelDir`, which never changes (single model). The preset (Speed/Balanced/Accuracy → `DecodingMethod`/`MaxActivePaths`) is baked into `OfflineRecognizer` at load time, and `MainWindow.SettingsButton_Click:1656` explicitly skips recognizer recreation "for performance." So a user who switches accuracy mode in Settings gets **zero runtime effect** and no indication. Either wire a rebuild on preset change, or remove the setting — shipping a knob that does nothing is worse than not having it.

### 4. Core feature (transcription) has ZERO active tests — ~~HIGH~~ **RESOLVED 2026-07-17**
`TranscriptionServiceTests` now covers the Parakeet path functionally: known TTS WAV → asserts the actual spoken words come back; silence → asserts no hallucinated text; in-memory path parity; concurrent-call serialization; missing/tiny-file guards; dispose behavior. `AudioRecorderTests` gained the `StopRecording → VAD → temp WAV → AudioFileReady` handoff test (asserts a valid 16kHz/16-bit/mono WAV on disk). Model-dependent tests gate on the installed Parakeet model but assert real output when present — no early-return-to-green. Still uncovered: the AutoPaste=true injection branch (untestable safely — it fires real Ctrl+V into the focused window).

## Desktop app (`VoiceLite/`) — overall risk: MEDIUM

**Solid:**
- Engine swap to Sherpa-ONNX/Parakeet is real and clean — no Whisper.net/subprocess leftovers in source.
- Crash-safety plumbing is genuinely careful: `transcriptionSemaphore` with `semaphoreAcquired` double-release guard, session-id callback rejection, thread-owned clipboard release. This area is well-built — don't "simplify" it.
- DPAPI license storage, legacy-plaintext migration, and the startup native/model gates all match their docs and work.
- Only 4 empty catch blocks, all defensible (can't-log-the-logger; swallow `ObjectDisposedException` on semaphore release).

**Fragile / buggy:**
- **Preset no-op bug** (see #3 above).
- **Dispose-during-Decode is not actually synchronized.** `Decode()` runs in `Task.Run` on a local ref; the constructor warm-up calls `EnsureRecognizerLoaded` *outside* `transcriptionSemaphore`. Safe today only because the single-model invariant makes reload a no-op early-return — the safety is incidental, not enforced. CLAUDE.md claims a lock protects this; it doesn't.
- **Silent audio loss:** `SaveMemoryBufferToTempFile` on disk-write failure loses the recording and logs it as "expected behavior" — no user signal.
- **Silent injection failure:** if `TextInjector` fails, text lands in history but never reaches the target app, logged only. A clinician mid-dictation sees nothing happen.
- **Offline Pro state trusts a stale bool:** `IsProUser` trusts `settings.IsProLicense` + presence of *any* stored key; it never re-checks tier/revocation offline (only the 14-day online re-validation does). A once-Pro user stays Pro offline indefinitely.
- **Custom Dictionary "Pro feature" isn't gated:** the tab is UI-hidden for free users, but `TextPostProcessor` applies `settings.CustomDictionary` for everyone. Gating is cosmetic.

**Dead code:** `WhisperModelInfo.cs` 5-model GGML constants (only `LegacyGgmlFileNames` is live), `ModelResolverService.ResolveModelPath(modelName)` dead param, `App.OnProcessExit/OnExit` empty bodies, `AudioRecorder.AudioDataReady` event fired with no subscribers (still copies the byte[]).

## Web backend (`voicelite-web/`) — overall risk: MEDIUM

**Solid:** admin auth (constant-time, fails closed), 3-device cap is race-safe via DB unique constraint, Stripe status mapping fails closed (unknown → EXPIRED), enumeration-safe license retrieval, secrets otherwise env-sourced and placeholder-rejecting.

**Fragile / buggy:**
- Webhook idempotency (see #2).
- **Device-limit bypass:** `validate` only records activation when `machineId` is supplied, and `machineId` is optional. A client omitting it gets `valid:true, tier:pro` with no device accounting — the 3-device cap is client-cooperative, not enforced.
- **Rate-limiting is all-or-nothing on Upstash:** no Upstash env → `validate`/`retrieve`/`resend-email` fall to useless per-instance in-memory limiters (worthless on serverless); `feedback` has none. Verify Upstash is actually configured in prod.
- **`checkout` is completely unthrottled** — spammable Stripe session creation.
- **Migration drift (MEDIUM):** the `20251031000000_add_processed_at_to_webhook_events` migration is **untracked in git** but its column is already in `schema.prisma` and required by code — a fresh checkout's `prisma migrate` would be inconsistent with prod. Also a **duplicate migration** pair (`20251025003812_make_userid_optional` empty vs `20251025_make_userid_optional` with SQL) can confuse Prisma bookkeeping. And `schema.prisma` declares `userId String` (non-null) while the migration made it nullable.

**Dead code:** `lib/openapi.ts` documents ~13 routes that don't exist (`/auth/otp`, `/licenses/activate`, `/licenses/crl`, `/me`, Ed25519/CRL security model) — served publicly at `/api/docs`, actively misleading. Unused rate limiters (`otpRateLimit`, `emailRateLimit`, …) from a never-built auth system. `components/ui/` (glow-card, gradient-text) imported nowhere. Dead subscription/"quarterly" branches in webhook while `checkout` only does one-time payment.

## Tests & CI — overall risk: MEDIUM (with the HIGH false-confidence flagged above)

> **Update 2026-07-17 (post-audit cleanup):** the Phase-E zombies below were deleted the same day (suite is now 283 passed / 22 legitimately skipped), and a new HIGH footgun was found and fixed: **`LicenseServiceTests` had no file isolation — running `dotnet test` on a machine with an activated license OVERWROTE the real `%LOCALAPPDATA%\VoiceLite\license.dat` with test keys** (and one test failed against the real file). A `LicenseFileGuard` fixture now backs up/restores the real file around the class. The underlying cause — `LicenseService` hardcodes the real path with no test seam — still exists; any future test that news up `LicenseService` and saves must use the same guard.

- **~101 methods skipped, not the "35" the docs claim.** ~74 are Phase-E debt (`WhisperServiceTests`, `ModelResolverServiceTests`, `ProFeatureServiceTests`, `WhisperErrorRecoveryTests`, most of `WhisperModelInfoTests`), the rest stress/WPF/real-audio tests. They compile fine — they're semantically obsolete zombies that pass by early-returning when the (now-absent) GGML file is missing → **false green.**
- **Well-covered:** LicenseService (39), HotkeyManager (45), TranscriptionHistory (25), CustomShortcut (22), SileroVad (14), AudioRecorder (14), TextPostProcessor (28), disposal/leak suites. These are real.
- `test-release.yml` fails on every release (planting a red X) and verifies nothing real.
- `pr-tests.yml` works but is bypassed by direct-to-master pushes.
- `build-installer.ps1` skips the mandatory `dotnet clean` — will reproduce the v2.1.1 stale-DLL type-init crash if used for a tagged release. The local `.git/hooks/post-commit.ps1` version-sync hook is broken against the current `.iss` (regex no longer matches). Versions currently agree only because someone edits the `#define` by hand.
- **`v2.1.1` was committed but never tagged** — a hole in the release history.

## Repo hygiene — overall risk: LOW (embarrassing, not dangerous)

Tracked repo is tiny and clean (9.5 MB / 540 files, no secrets tracked, strong `.gitignore`). The problem is on-disk sprawl: ~30 GB total, of which ~22 GB is **untracked, free-to-delete** cruft (`voicelite-tauri-old/` 9.5 GB, 42 installer `.exe`s 12.4 GB, `sandbox-test/` 237 MB). Plus a **2.1 GB `.git`** bloated with dangling old-installer blobs (packed set is only 20 MB) — recoverable with `git reflog expire --expire=now --all && git gc --prune=now`. 13 local / 24 remote branches, mostly abandoned backups.

## Documentation — overall risk: MEDIUM (actively misleading)

The single biggest hazard for future sessions (and for you): **the prominent docs describe a product that no longer exists.**
- `ARCHITECTURE.md` (root, labelled v1.4.0) — full Whisper.net / 5-GGML-model fiction.
- `PILOT.md` — describes `WhisperFactory.FromPath()` and downloading Whisper models; **handed to a healthcare pilot** as a privacy statement. Its clipboard "2 s auto-clear" claim is also wrong for the AutoPaste-off (30 s) path.
- `CHANGELOG.md` — stops at 1.4.0, ~6 releases stale.
- `SECURITY.md`, `CONTRIBUTING.md` — pre-v2.
- `README.md` FAQ says "99 languages"; its own engine section says "25 European" (Parakeet reality). Self-contradictory.
- `CLAUDE.md` — **the most accurate doc**, matches HEAD, but bloated (~230 lines) and carries a few stale claims (VADThreshold setting, the Decode lock, the LicenseService-mutates-IsProLicense comment).

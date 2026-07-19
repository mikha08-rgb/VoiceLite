# VoiceLite — Cleanup Roadmap

*Audit date: 2026-07-17. Ranked by risk × value. Each chunk is scoped to one focused session. Do them roughly in order — early items de-risk later ones. Nothing here is started; this is the menu.*

*Status updates through 2026-07-18. The 2026-07-18 session also landed off-menu work found by review, not listed as a chunk: atomic model extract+swap with zero-byte-install detection, cancellable/stall-aware model download with disk preflight, O(1) DSP window sums (equivalence-tested), the Silero 46-byte-WAV-header fix, and an uninstall running-app check — see HEALTH.md's 2026-07-18 block.*

**Golden rule for every chunk below:** the app has paying users. Behavior must not change unless a chunk explicitly says so. Build a way to *observe* the behavior before you change it.

---

## Chunk 0 — Immediate ops (not really cleanup, do today, ~30 min)
Not a coding session; just stop active bleeding.
1. **Rotate the live Stripe key** (HEALTH.md #1), then delete/env-ify `voicelite-web/check-stripe-webhooks.mjs`. **DONE 2026-07-18** — key rotated by Misha, script deleted (plus the other one-off license-check scripts; Stripe MCP + ad-hoc read-only Prisma scripts replace them).
2. **Confirm Upstash env vars are set in the Vercel prod project** (HEALTH.md — rate limiting silently disappears without them).
3. **Confirm prod DB migration state** (`prisma migrate status`) matches `schema.prisma`. **DONE 2026-07-18** — it didn't, badly: prod had no `_prisma_migrations` table at all. Baselined with `0_init` + `drop_user_activity` (see Chunk 4 item 4 / HEALTH.md migration item).
*Value: very high. Risk: low. These are checks + a key rotation, not refactors.*

## Chunk 1 — Test safety net around what users depend on most ⭐ ~~START HERE~~ **DONE 2026-07-17** (except AutoPaste characterization — deliberately skipped, it would fire real keystrokes; zombie deletion done same day. Suite: 291 passed / 22 skipped.) **Completed 2026-07-18:** the functional-coverage bullet fully landed — `TranscriptionServiceTests` +17 active tests with real Parakeet decodes, new `ModelResolverServiceTests` (9), `AudioPipelineTests`' record→transcribe half revived off its dead GGML gate onto the shared model fixture, mic/model environment guards for bare CI runners, and the first push-triggered CI run is green. Suite: 316 passed / 0 failed / 22 skipped.
The core transcription path ~~has~~ had **zero active tests** (HEALTH.md #4 — **now false**, resolved 2026-07-17/18). Before touching any desktop code, build coverage for the flow users actually pay for.
- Add a functional test for `PersistentWhisperService`: given a known WAV + the Parakeet model present, assert non-empty transcription (gate on model presence like the old tests, but assert *real output*, not early-return-to-green).
- Cover the `AudioRecorder → VAD → temp WAV → AudioFileReady` handoff.
- Characterize the `AutoPaste` true/false branch in `TextInjector` (the GPHealth-critical path) with a fake injection target.
- **Delete the ~74 Phase-E zombie tests** (`WhisperServiceTests`, `ModelResolverServiceTests`, `WhisperErrorRecoveryTests`, the GGML parts of `WhisperModelInfoTests`, obsolete `ProFeatureServiceTests`) — they give false green and confuse the count. Web side: keep the strong LicenseService/webhook coverage; add a test that reproduces the webhook-idempotency drop (Chunk 3 depends on it).
*Value: very high (unlocks safe changes everywhere). Risk: low (adding/removing tests can't break prod).*

## Chunk 2 — Fix the two silent user-facing bugs — **DONE 2026-07-17** (preset now rebuilds the recognizer lazily + test; lost-audio and failed-paste both surface red status via existing MainWindow mechanism)
With Chunk 1's net in place:
1. **Transcription preset no-op** (HEALTH.md #3): either rebuild the `OfflineRecognizer` on preset change, or remove the Speed/Balanced/Accuracy setting entirely. Decide via QUESTIONS.md #8. Add a test proving the chosen behavior.
2. **Silent failures**: surface a user-visible signal when `SaveMemoryBufferToTempFile` loses audio or `TextInjector` fails to inject (today both are log-only). Small, high-empathy fixes.
*Value: high (real user pain). Risk: medium (touches live desktop paths — hence Chunk 1 first).*

## Chunk 3 — Fix the webhook license-drop — **DONE 2026-07-17** (retriable-failure claim-release deployed earlier; evening pass added the hard-crash case: epoch-sentinel claims, processedAt stamped only on success, atomic stale-claim takeover after 5 min, maxDuration=60)
Rework `webhook/route.ts` so the `WebhookEvent` idempotency row is committed **only after** successful processing (or is deleted on failure), so Stripe's retries actually reprocess. Reproduce with the Chunk 1 test first, then fix, then confirm the test goes green. Consider folding `fix-stripe-webhooks.ts`'s reconciliation into a scheduled job.
*Value: high (stops losing paid customers). Risk: medium (payment path — test-first, deploy to preview, watch a real event).*

## Chunk 4 — Harden the licensing edges — **DONE 2026-07-17, closed out 2026-07-18** (machineId: legacy clients ≤v1.2.0.1 never sent it, so it stays optional but omission now consumes a reserved "legacy-no-machine-id" activation slot — bypass closed without locking out old builds; checkout rate-limited 5/h/IP; ~~migrations reconciled + schema userId made nullable to match prod~~ that reconciliation was backwards — redone 2026-07-18, see item 4. ~~Item 3, offline-Pro revalidation, still open — QUESTIONS #9.~~ Item 3 closed 2026-07-18: deliberate policy, no code change.)
1. Make `machineId` required on `validate` (or document why optional) — close the device-limit bypass (QUESTIONS.md #6).
2. Add rate limiting to `checkout`.
3. Decide the offline-Pro-revalidation question (QUESTIONS.md #9) and implement. **RESOLVED 2026-07-18 — no implementation:** Misha decided status quo is deliberate; once-Pro-always-Pro offline is intended goodwill (lifetime licenses, healthcare pilot users offline for long stretches; revocation only matters for refunds/chargebacks). Documented in HEALTH.md + QUESTIONS.md #9.
4. Resolve the migration drift: commit the `20251031` migration, delete the duplicate empty `20251025` dir, reconcile `schema.prisma` `userId` nullability. **Re-done properly 2026-07-18:** prod had **no `_prisma_migrations` table at all** (evolved via `db push`/manual SQL — none of the 5 repo migrations ever applied) and prod `userId` is NOT NULL (`schema.prisma` corrected back to required). Repo migrations replaced by a `0_init` baseline (marked applied on prod via `prisma migrate resolve`) + `drop_user_activity` applied via `migrate deploy`; phantom `TelemetryMetric` migration deleted.
*Value: medium-high. Risk: medium.*

## Chunk 5 — Decide the Tauri question, then delete
Answer QUESTIONS.md #1 first (kill or keep `voicelite-tauri/`). Assuming kill:
- Tag a `graveyard/tauri-spike` branch (or just trust git) for recoverability, then `git rm -r voicelite-tauri/` and delete on-disk `voicelite-tauri-old/`.
- Delete `release-tauri.yml`.
*Value: high (removes 306 tracked files of misleading parallel code + ~19 GB disk). Risk: low once the decision is made — it's isolated, nothing imports it.*

## Chunk 6 — Kill the misleading docs
The prominent docs describe a product that doesn't exist and are actively dangerous (HEALTH.md — Documentation).
- Rewrite or delete root `ARCHITECTURE.md` (point at `docs/audit/ARCHITECTURE.md`).
- **Update `PILOT.md`** to the real Sherpa/Parakeet engine + correct clipboard behavior — it's a privacy statement given to a healthcare pilot (QUESTIONS.md #16).
- Fix `README.md` "99 languages" contradiction.
- Bring `CHANGELOG.md` up to v2.1.2 or delete it.
- Delete `lib/openapi.ts` fiction (or gut it to the ~11 real routes) — it's served publicly.
*Value: high (every future reader, human and AI, is currently misled). Risk: none (docs only).*

## Chunk 7 — Rename the Whisper→Parakeet vocabulary
One deliberate, test-backed pass (needs Chunk 1's net). Rename `PersistentWhisperService`→`TranscriptionService`, `WhisperModelInfo`, `settings.WhisperModel`, `WhisperPreset*`, drop dead `modelName`/`NormalizeModelName`. Big diff, mechanical, but touches many call sites and the settings-serialization key (`WhisperModel` in `settings.json` — needs a migration shim so existing users' settings still load).
*Value: medium (clarity). Risk: medium (settings compat — do carefully, or defer). Protected areas from COMPLEXITY.md must keep their guard code even when renamed.*

## Chunk 8 — Repo hygiene sweep (mostly free) — **mostly DONE 2026-07-17** (disk junk, git gc, 10 dead local branches, .bat pile, dangerous build-installer.ps1, broken hook all gone; build-release.ps1 now cleans bin/obj. Still open: `test-reliability-improvements` branch — 15 unpushed MVVM commits awaiting Misha's keep/kill call — and pruning stale REMOTE branches.)
- Delete untracked cruft: 42 root installer `.exe`s (12.4 GB), `voicelite-tauri-old/` if not done in Chunk 5, `sandbox-test/`, `coverage/`, `TestResults/`, `bfg.jar`, `voicelite-web-v1.0.72/`, the `~/` dir, the `nul` file. All untracked → deletion is free and zero-risk.
- Reclaim ~2 GB: back up `.git`, then `git reflog expire --expire=now --all && git gc --prune=now`.
- Prune the 9 dead local backup/archive branches and stale remotes (keep `master`; verify `test-reliability-improvements`'s 15 commits first — QUESTIONS.md #13).
- Delete dead CI: `test-release.yml`, `release-with-signing.yml.DRAFT`. Fix or delete the broken `.git/hooks/post-commit.ps1` and dangerous `build-installer.ps1`.
- Delete the abandoned root `.bat` pile (`DIAGNOSE_BUILD`, `QUICK_TEST`, `RUN_TEST`, `TEST_BUILD`, `TEST_WEEK1`).
*Value: medium (huge disk win, less confusion). Risk: low. Can be split into "disk" and "branches/CI" sub-sessions.*

## Chunk 9 — Small dead-code collapses (opportunistic) — **mostly DONE 2026-07-17, remainder decided 2026-07-18** (evening sweep: App stubs, unused limiters, components/ui + 11 dead components, lib/crypto, model registry collapsed, Language no-op UI removed, AsyncHelper, dead history/tray members — ~1,700 LOC. Closed 2026-07-18: `UserActivity` model dropped via `drop_user_activity` migration (prod had 0 rows); `IProFeatureService` collapsed to concrete `ProFeatureService` (its mock-seam justification died with the deleted zombie tests); orphaned routes decided — `licenses/deactivate` + `licenses/resend-email` DELETED (zero callers: desktop only calls `validate`; site calls checkout/download/retrieve/feedback-submit), `admin/process-payment` + `admin/get-license` (used by `scripts/mint-gratis-keys.sh`) + `diagnostic` KEPT deliberately as manual ops tools. Still open: `AudioDataReady` (kept — tests use it), webhook dead subscription branches (protected).)

---

### Suggested ordering rationale
0 → 1 first (safety + stop bleeding), then 2/3/4 (the bugs that cost users), then 5/6 (delete the misleading mass), then 7 (rename once safe), then 8/9 (hygiene). Chunks 5, 6, 8 are independent and can slot in anytime you want a low-risk win.

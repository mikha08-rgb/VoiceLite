# VoiceLite — Open Questions

*Audit date: 2026-07-17. Things I could not determine from code alone — intent, deliberate-but-strange decisions, and features I can't tell are live or abandoned. Per the workflow: answer these yourself; that's where you re-learn your own codebase. Add answers inline as `**A:**`.*

## Product direction

1. **Is `voicelite-tauri/` truly abandoned, or a paused plan B?** Code says dead (frozen Mar 1, no licensing, still on Handy's CDN, MIT/free — incompatible with the paid product). But someone invested a weekend and wrote a `release-tauri.yml`. Is there any intent to revive it, or can the whole tree + `voicelite-tauri-old/` be deleted? *(This gates ~19 GB of cleanup and a big COMPLEXITY decision.)*
   **A (2026-07-17):** Dead for good — Misha confirmed. Tree deleted from repo (recoverable via tag `graveyard/tauri-spike`), `voicelite-tauri-old/` wiped from disk, `release-tauri.yml` deleted.

2. **What is the Pro tier actually selling today?** Model-gating is a no-op (all users get Parakeet). The only "Pro" feature is Custom Dictionary — *(UPDATE 2026-07-18: now genuinely gated at processing time, no longer applied to free users)* — so the functional answer today is: Custom Dictionary, and that alone. Is Pro currently selling a promise ("features coming") more than a capability?

3. **Are there active paying subscribers, or only LIFETIME buyers?** `checkout` only creates one-time (`mode:'payment'`) sessions, but the webhook and schema carry full SUBSCRIPTION/quarterly handling. Is the subscription path (a) legacy to support existing subs, (b) planned, or (c) dead code that can go?

## Licensing & payments

4. **Has the webhook-drop bug (HEALTH.md #2) actually lost customers?** `fix-stripe-webhooks.ts` exists to back-fill missing licenses — how often have you had to run it? That tells us the real severity.
   **A (2026-07-18, partial):** the DB shows no damage in the recent window — a Stripe MCP + read-only prod DB cross-check found every recent charge has a matching license, webhook events processed within seconds, no stranded events. Sole anomaly: one customer double-purchased on 2026-05-19 (softtaildh@aol.com, two $20 charges 19 min apart, both licensed — Misha chose not to refund). Historical run-frequency of the reconciler remains unknown but is now moot.

5. **Is Upstash rate-limiting actually configured in production?** If not, `validate`/`retrieve`/`resend-email` have effectively no rate limit on Vercel. Need to confirm the env vars are set in the Vercel project.

6. **Is the `machineId`-optional device-limit bypass intentional?** Was `machineId` made optional to support some client (older desktop build? VM/headless?) that couldn't produce one, or is it just an oversight? Determines whether we can make it required.

7. ~~**Migration drift: what's the real state of prod's `_prisma_migrations`?**~~ **ANSWERED 2026-07-18 — worse than feared, now fixed.** Prod had **no `_prisma_migrations` table at all** — the DB evolved via `db push`/manual SQL; none of the 5 repo migrations were ever applied. Also: prod `License.userId` is **NOT NULL** (the 2026-07-17 "made nullable to match prod" fix was backwards — `schema.prisma` corrected back to required), and `UserActivity` existed in prod with 0 rows. Fix: repo migrations replaced by a `0_init` baseline (marked applied on prod via `prisma migrate resolve`) plus a `drop_user_activity` migration applied via `migrate deploy`; the phantom `TelemetryMetric` migration was deleted with the rest.

## Desktop behavior & intent

8. ~~**The transcription-preset setting (Speed/Balanced/Accuracy) does nothing at runtime (HEALTH.md #3). Was it ever wired?**~~ **RESOLVED 2026-07-17: fixed, not removed** — the preset is now part of the recognizer reload key and rebuilds the `OfflineRecognizer` lazily on the next transcription; functional test covers it. (Historical half of the question — whether it ever worked in the Whisper era — remains unanswered but is now moot.)

9. ~~**Should the offline Pro check re-validate tier/revocation, or is "once Pro, always Pro offline" the intended generosity?**~~ **ANSWERED 2026-07-18: deliberate — status quo stands.** Misha decided once-Pro-always-Pro offline is intended goodwill: licenses are lifetime, and healthcare pilot users may be offline for long stretches; revocation only matters for refunds/chargebacks. No code change.

10. **VAD threshold is hardcoded at `0.35` with no setting.** MEMORY.md claims a `VADThreshold` setting exists — it doesn't. Was the setting removed on purpose (too footgunny for users) or lost in migration? Should it be exposed again?

11. **Model download hits k2-fsa's GitHub release directly, not a VoiceLite mirror.** The code comment says "mirror before public launch." Is launch-day traffic to a third party an accepted risk, or a must-fix? (If k2-fsa moves/deletes the release, first-launch downloads break for all new users.)

12. **Is `UpdateCheckService` fully wired?** It checks GitHub for the latest release — does it actually prompt/download/install, or just notify? And does it point at the right repo? (Worth confirming given the updater story.)

## Repo & process

13. **`test-reliability-improvements` branch is 15 commits ahead of its remote — is that unmerged work worth keeping, or abandoned?** Only branch with unique local work; everything else is a backup/experiment safe to delete.

14. **Why do commits land on `master` without PRs?** `pr-tests.yml` only gates PRs. Is direct-push a deliberate solo-dev choice (fine, but then the gate is theater) or should master be protected?

15. **`v2.1.1` was committed but never tagged/released.** Intentional (superseded by v2.1.2 same day) or a missed release? Affects whether the "v2.1.1 shipped" claims in memory/CLAUDE.md are even true.

16. **Is the GPHealth pilot live on a current build?** `PILOT.md` describes the *old* Whisper engine and privacy posture. If the pilot is running, they may be relying on a privacy statement that no longer matches the software. Worth confirming what build they actually have and updating PILOT.md before it's an audit liability.

## Small mysteries

17. **`components/ui/` (glow-card, gradient-text)** — abandoned redesign experiment, or staged for an upcoming landing-page refresh?
18. **`sandbox-test/` (237 MB, untracked)** — what was being sandboxed? Safe to delete, but curious if it holds anything you meant to keep.
19. **Three parked git stashes** — anything valuable, or clear them?

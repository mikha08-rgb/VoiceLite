# Changelog

All notable changes to VoiceLite are documented here.

Format based on [Keep a Changelog](https://keepachangelog.com/).

## [2.3.0] - 2026-07-18

### Fixed
- Cancelled recordings no longer paste text — cancelling now discards the audio instead of transcribing and injecting it.
- "Minimize to system tray" works again.
- Second app launch no longer starts a duplicate instance (single-instance guard; the existing window is brought forward).
- Post-processing no longer lowercases proper names.
- Pinned history items are now actually preserved — exempt from both the 250-item cap and the 7-day startup purge.
- Recordings too short to transcribe now show feedback instead of failing silently.
- First-launch model install hardened: download is cancellable, checks free disk space up front, survives slow connections (stall detection instead of a flat timeout), and installs atomically — an interrupted or truncated download can no longer leave the app unable to start.
- No more seconds-long freeze after changing settings — the speech engine reloads in the background.
- More accurate speech detection: fixed a WAV-header parsing bug that fed the voice-activity detector misaligned audio.
- Uninstalling while VoiceLite is running now aborts cleanly instead of half-removing files.

### Changed
- Custom Dictionary is now correctly Pro-only (it was previously applied for all users despite being a Pro feature).
- Faster audio capture processing (noise gate and gain control are now constant-time per sample).

### Web (deployed separately)
- Device deactivation endpoint — a license seat can now be freed without contacting support.
- License email delivery hardened (fewer silently lost license emails after purchase).
- Site download version and copy corrected to match the actual release.

## [2.2.0] - 2026-07-17

### Fixed
- **Transcription preset (Speed/Balanced/Accuracy) now takes effect immediately** — previously changing it did nothing until app restart (the recognizer is now rebuilt on the next transcription after a change).
- **Failures are no longer silent**: if a recording can't be saved to disk, or transcribed text can't be pasted into the target app, the status bar now shows a red error (text remains available in History).

### Changed
- Internal Whisper-era class/property names renamed to match the real Parakeet engine (`TranscriptionService` et al.). No user-facing impact; `settings.json` stays fully compatible.
- Codebase cleanup: removed the abandoned Tauri experiment, obsolete tests, and stale pre-v2.0 documentation. Core transcription now has functional test coverage.

### Web (deployed separately)
- Stripe webhook retries now correctly re-process after transient failures (previously a payment could complete without a license being issued until manual reconciliation).
- Device-activation accounting now also applies to legacy clients that don't send a machine ID; checkout endpoint is rate-limited.

## [2.1.2] - 2026-05-26

### Fixed
- AutoPaste-off flow now holds transcription on the clipboard for 30s (match-before-clear) so it can actually be pasted manually.

### Added
- Installer `[UninstallDelete]` support: uninstall preserves user data by default; pass `/PURGEDATA` to wipe `%LOCALAPPDATA%\VoiceLite`.

## [2.1.1] - 2026-05-25 *(committed, never tagged/released — superseded by 2.1.2 the next day)*

### Added
- Startup update check (GitHub releases) with tray notification.
- Uninstall preserves user data by default.

## [2.1.0] - 2026-05-25

### Added
- **Custom Dictionary** — user-defined text replacements applied post-transcription; first concrete Pro feature (UI tab is Pro-gated).

## [2.0.2] - 2026-05-25

### Changed
- Settings UI honesty pass — removed/relabeled controls that no longer applied post-migration.

## [2.0.1] - 2026-05-25

### Fixed
- Code-review fixes from the v2.0.0 migration; removed tracked GGML leftovers.

## [2.0.0] - 2026-05-24 — **Parakeet migration**

### Changed
- **Speech engine replaced entirely**: Whisper.net + 5 GGML models → Sherpa-ONNX running NVIDIA Parakeet TDT 0.6B v3 (single model, in-process, CPU-only, 25 European languages). Faster than Whisper Large v3 with better WER and no hallucination-on-silence.
- Model is downloaded on first launch (~640MB) instead of bundled.

### Removed
- Whisper.net, GGML model lineup (tiny/base/small/medium/large), model-tier Pro gating.

## [1.4.0.0] - 2026-02-15

### Added
- **Whisper.net in-process**: Migrated from whisper.cpp subprocess to Whisper.net 1.9.0 (C# P/Invoke). Model loads once into memory; subsequent transcriptions reuse it (2-10x faster). No more subprocess management, zombie processes, or temp WAV files.
- **Silero VAD preprocessing**: Runs Silero VAD v5 (ONNX) as a preprocessing step before Whisper. Detects speech segments and trims silence to reduce hallucinations from long pauses. Pipeline: HPF → NoiseGate → AGC → VAD → Whisper. Controlled by existing EnableVAD setting (default on, threshold 0.35).
- **large-v3-turbo-q8_0 model**: New "Turbo" tier (874MB, Q8_0 quantized) — near Ultra accuracy at 3x the speed.

### Removed
- Old whisper.cpp subprocess binaries (~60MB): whisper.exe, whisper.dll, ggml.dll, libopenblas.dll, clblast.dll, SDL2.dll
- Dead download scripts: create-release.ps1, download-whisper.ps1/.py/.bat
- Dead installer files: Product.wxs, Add-VoiceLite-Exclusion.ps1
- Dead code: AsyncHelper.cs, unused Settings properties, ResolveVADModelPath()

### Changed
- FluentAssertions → AwesomeAssertions 9.3.0
- Test count: 375 → 412 passing, 35 skipped (15 new VAD tests)

### Dependencies
- Added: Microsoft.ML.OnnxRuntime 1.17.3, Whisper.net 1.9.0, Whisper.net.Runtime 1.9.0
- Added: System.Text.Json 10.0.0 (required by Whisper.net)
- Removed: Microsoft.Extensions.DependencyInjection

## [1.3.0.0] - 2026-02-14

### Changed
- **Architecture**: Removed dead DI infrastructure — interfaces, controllers, ViewModels, SettingsService, ServiceConfiguration (~4800 lines deleted)
- **Settings**: Removed 15 unused properties (SelectedModel, UseGpu, HotkeyKey, GlobalHotkey, etc.)
- **AsyncHelper**: Removed unused WrapEventHandler, RunOnUIThread, RunOnUIThreadAsync methods
- **TextInjector**: Removed no-op SetTypingDelay method (adaptive delays used instead)
- **TranscriptionHistoryItem**: Consolidated duplicate truncation logic into TextAnalyzer.Truncate()
- **LicenseService**: Fixed misleading "lifetime license" log message → "14-day cached result"

### Fixed
- **Web: Rate limiter**: Skip rate limiting when Upstash unavailable to prevent license activation failures
- **Web: Rate limiter**: Thread-safe in-memory fallback with AsyncLock
- **Web: Email**: Added retry logic with exponential backoff (3 attempts)
- **Web: Licensing**: Subscription status defaults to fail-closed for unknown Stripe statuses
- **Web: Rate limits**: Increased license validation rate limits to reduce TooManyRequests errors
- **Web: Resend-email**: Fixed Zod validation using .issues instead of .errors
- **Web: Prisma**: Removed duplicate index on LicenseActivation.licenseId
- **Web: Email retry**: Fixed off-by-one in exponential backoff delay calculation

### Updated
- FluentAssertions → AwesomeAssertions 9.3.0 (test assertion library)
- coverlet.collector → 6.0.4
- H.InputSimulator → 1.5.0
- GitHub Actions v3 → v4

## [1.2.0.14] - 2026-01-18

### Fixed
- **LicenseService**: All shared field reads now synchronized with `_cacheLock`
- **HardwareIdService**: Fixed double-release bug in AbandonedMutexException handler
- **TextInjector**: Fixed race condition where concurrent InjectText() calls could target wrong window

## [1.2.0.13] - 2026-01-18

### Fixed
- **LicenseService**: SaveLicenseKey() and RemoveLicenseKey() field updates moved inside lock
- **HardwareIdService**: Added named mutex for cross-process file synchronization
- **HardwareIdService**: Cached timeout fallback ID to prevent device limit exhaustion
- **TextInjector**: Removed shared `_targetWindowHandle` field — now captured locally per call
- **TextInjector**: Made `_disposed` volatile for cross-thread visibility

## [1.2.0.12] - 2026-01-18

### Security
- Thread safety audit of LicenseService, HardwareIdService, and TextInjector
- File I/O operations moved outside locks to prevent blocking

### Fixed
- Race conditions in license validation caching
- Potential torn reads on shared state fields

## [1.2.0.11] - 2026-01-18

### Fixed
- Installer filename now correctly matches version (was hardcoded to 1.2.0.8)

### Changed
- Inno Setup uses preprocessor variable for single version source

## [1.2.0.10] - 2026-01-18

### Security
- DPAPI encryption for machine_id.dat — prevents device limit bypass via file copy
- Settings tampering protection — detects manual edits to settings.json
- Reduced license cache from 30 days to 14 days — faster revocation detection
- Email format validation in webhook — prevents malformed data

### Fixed
- TextInjector reliability improvements and thread safety
- ProFeatureService thread-safe RefreshProStatus
- TranscriptionHistoryService O(n log n) cleanup instead of O(n²)
- Timer race condition fixes in MainWindow

## [1.2.0.9] - 2026-01-18

### Security
- License activation security hardening
- Rate limiting changed from fail-open to fail-closed
- Device activation race condition fix (transaction-based)
- Webhook idempotency using atomic INSERT

## [1.2.0.8] - 2025-01-17

### Added
- Startup toggle — launch VoiceLite with Windows
- Structured logging — better debug output format
- Test audio — verify microphone works from settings

## [1.2.0.7] - 2025-01-16

### Fixed
- Thread safety and resource management issues
- Critical race conditions in audio processing

## [1.2.0.6] - 2025-01-15

### Fixed
- Fallback to base model when selected model missing
- Production security and logging improvements
- Webhook idempotency bug
- Self-service license retrieval
- Fail-open rate limiting when Upstash disabled

### Added
- SEO improvements (robots.txt, sitemap, JSON-LD schemas)

## [1.2.0.5] - 2025-01-14

### Fixed
- Critical email and webhook issues
- LicenseActivation field names
- Missing Prisma models and relations

## [1.2.0.4] - 2025-01-12

### Fixed
- HotkeyManager.UnregisterCurrentHotkey build error

### Changed
- Refactored HotkeyDisplayHelper for consistent modifier formatting

### Removed
- Legacy publish artifact directory

## [1.0.96] - 2024-12-xx

### Added
- Q8_0 quantization for 67-73% faster inference
- Multiple AI models (Base, Small, Medium, Large)
- Pro tier with model downloads

### Fixed
- Model files gitignore issue

## [1.0.94] - 2024-12-xx

### Fixed
- Release logging disabled issue (silent failures)
- Process disposal timeout for zombie whisper processes

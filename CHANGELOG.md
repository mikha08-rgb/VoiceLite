# Changelog

All notable changes to VoiceLite are documented here.

Format based on [Keep a Changelog](https://keepachangelog.com/).

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

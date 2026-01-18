# Changelog

All notable changes to VoiceLite are documented here.

Format based on [Keep a Changelog](https://keepachangelog.com/).

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

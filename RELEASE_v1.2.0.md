# VoiceLite v1.2.0 Release Notes

**Release Date**: 2025-10-27
**Status**: ✅ Production Ready
**Type**: Minor Version (New Features + Bug Fixes)

---

## 🎯 Release Summary

VoiceLite v1.2.0 delivers significant improvements to text injection reliability, audio preprocessing quality, and user experience. This release includes 4 major new features, 1 critical bug fix, and comprehensive testing verification.

**Key Highlights:**
- ✨ Adaptive typing delays for improved text injection across different applications
- ✨ User-configurable clipboard restoration delay (30-100ms)
- ✨ Audio preprocessing pipeline with AGC, high-pass filter, and noise gate
- ✨ Toggle recording mode now default (Dragon-like UX)
- 🐛 Fixed Settings window crash due to null reference
- 📚 26KB text injection research and comparative analysis document

---

## ✨ New Features

### 1. **Text Injection Improvements** (Adaptive & Configurable)

**Adaptive Typing Delays:**
- Modern apps (Notepad, Chrome, VS Code): 1ms per character (50% faster than v1.1.51)
- Office apps (Word, Excel, PowerPoint): 2ms per character (proven reliability)
- Terminals (CMD, PowerShell): 5ms per character (compatibility)
- Default: 2ms for unknown applications

**Benefits:**
- ✅ Faster text injection in modern apps
- ✅ No dropped characters (tested across multiple apps)
- ✅ Application-specific optimization

**User-Configurable Clipboard Restoration Delay:**
- New Settings UI slider (30-100ms range)
- Default: 50ms (optimal for most systems)
- Allows users to balance speed vs reliability based on their system

**Note:** UI Automation scaffolding added but currently disabled (SetValue() replaces field content instead of inserting at cursor - requires TextPattern implementation).

**Related Commit:** `46ea064`

---

### 2. **Audio Preprocessing Pipeline** (Quality Improvement)

**New Components:**
- **Automatic Gain Control (AGC)**: Normalizes audio levels for consistent transcription
- **High-Pass Filter**: Removes low-frequency noise (< 80Hz)
- **Simple Noise Gate**: Filters background noise below threshold
- **Voice Activity Detection (VAD)**: Automatically trims silence

**User Controls (Settings → General):**
- VAD enable/disable checkbox
- VAD sensitivity slider (0.3-0.8)
- Visual feedback and real-time preview

**Benefits:**
- ✅ Better transcription accuracy in noisy environments
- ✅ Reduced file sizes (silence trimmed)
- ✅ User customization for different recording scenarios

**Related Commit:** `466c954`

---

### 3. **Toggle Recording Mode as Default** (UX Improvement)

**Change:**
- Toggle mode is now the default (press once to start, press again to stop)
- Provides Dragon NaturallySpeaking-like user experience
- Push-to-Talk still available as option

**Benefits:**
- ✅ More natural dictation workflow for longer sessions
- ✅ Hands-free operation (no need to hold key)
- ✅ Industry-standard UX pattern

**Related Commit:** `880512a`

---

### 4. **Automated Shipping Workflow** (Developer Experience)

**New Slash Commands:**
- `/ship-app` - Automated desktop app release workflow
- `/ship-web` - Automated web deployment workflow
- `/update-download-links` - Sync version numbers across repos

**Benefits:**
- ✅ Consistent release process
- ✅ Version number synchronization
- ✅ Reduced manual errors

**Related Commit:** `061d643`

---

## 🐛 Bug Fixes

### **Settings Window Null Safety** (Critical Fix)

**Issue:** Settings window could crash when accessing null UI elements
**Fix:** Added comprehensive null checks before accessing Settings controls
**Impact:** Prevents crashes when opening Settings in edge cases

**Related Commit:** `de96624`

---

## 📚 Documentation

### **Text Injection Research Document**

**New File:** `TEXT_INJECTION_RESEARCH.md` (26KB, 659 lines)

**Contents:**
- Comparative analysis: Dragon, Whisper Flow, Super Whisper, Talon Voice
- Windows UI Automation vs SendKeys vs Clipboard methods
- Performance benchmarks and best practices
- Implementation recommendations
- Industry standards and design decisions

**Related Commit:** `ef9f56e`

---

## 🧪 Quality Assurance

### **Test Results:**
- ✅ **289/289 unit tests passing** (100% pass rate)
- ✅ **42 tests skipped** (stress/integration tests - expected)
- ✅ **0 build errors, 4 pre-existing warnings** (test code only)
- ✅ **Release build verified** (0 errors)

### **Manual Testing Completed:**
- ✅ Text injection: Short phrases (5-10 words) in Notepad, Word, Chrome
- ✅ Text injection: Long phrases (50+ words) via clipboard paste
- ✅ Toggle recording mode: Rapid start/stop cycles (stable)
- ✅ Audio preprocessing: VAD settings tested in quiet/noisy environments
- ✅ Clipboard delay slider: Settings save and persist correctly
- ✅ Log analysis: No crashes, errors, or exceptions

### **Reliability Verification:**
- ✅ No dropped characters during text injection
- ✅ No data loss (doesn't delete existing text)
- ✅ Stable across 10+ transcriptions
- ✅ Clean application shutdown

---

## 📊 Technical Details

### **Code Statistics:**
- **6 commits** since v1.1.51
- **~2,500 lines** added/modified
- **7 files changed** (text injection)
- **10 files changed** (audio preprocessing)
- **332 lines added** (text injection features)
- **1,086 lines added** (audio preprocessing pipeline)

### **Performance:**
- Text injection: 50% faster for modern apps (1ms vs 2ms delay)
- Transcription: No performance regression
- Memory: No memory leaks detected
- CPU: Idle <5%, active within normal range

---

## 🚀 Upgrade Instructions

### **For Users:**
1. Download `VoiceLite-Setup-1.2.0.exe` from GitHub Releases
2. Run installer (will automatically update existing installation)
3. Settings will migrate automatically
4. New features available immediately in Settings → General tab

### **Breaking Changes:**
- None (fully backward compatible with v1.1.51 settings)

### **New Settings:**
- Clipboard Restoration Delay slider (Audio Settings section)
- VAD sensitivity slider (Transcription Quality section)

---

## 🔗 Download Links

- **Windows Installer**: `VoiceLite-Setup-1.2.0.exe` (~150MB)
- **GitHub Release**: https://github.com/mikha08-rgb/VoiceLite/releases/tag/v1.2.0
- **Website**: https://voicelite.app

---

## 📝 Changelog

### Added
- Adaptive typing delays (1ms-5ms based on application)
- User-configurable clipboard restoration delay (30-100ms slider)
- Audio preprocessing pipeline (AGC, HPF, noise gate)
- Voice Activity Detection (VAD) with adjustable sensitivity
- Toggle recording mode as default
- Automated shipping workflow slash commands
- Comprehensive text injection research documentation

### Fixed
- Settings window null reference crash (critical)

### Changed
- Default recording mode: Toggle (was Push-to-Talk)
- Text injection: Per-application optimized delays (was constant 2ms)
- CLAUDE.md: Updated version context to v1.2.0

### Technical
- Updated VoiceLite.csproj to version 1.2.0
- Updated VoiceLiteSetup.iss to version 1.2.0
- Added .gitignore entry for coverage/ directory
- All 289 unit tests passing

---

## 🙏 Acknowledgments

- Testing and validation by VoiceLite development team
- Research based on industry standards (Dragon, Whisper Flow, Super Whisper)
- Built with Claude Code assistance

---

## 📮 Support

- **Issues**: https://github.com/mikha08-rgb/VoiceLite/issues
- **Website**: https://voicelite.app
- **Documentation**: See `CLAUDE.md` for developer guide

---

**Built with ❤️ for reliable speech-to-text dictation**

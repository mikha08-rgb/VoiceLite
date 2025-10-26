# VoiceLite v1.1.0 Release Notes
**Release Date**: TBD (2025-10-26 target)
**Type**: Major Feature Release
**Branch**: `refactor/solidify-architecture` → `master`

---

## 🎉 What's New in v1.1.0

### Major Improvements

#### 🏗️ Complete Architecture Refactoring (Phase 1-4)
VoiceLite has been completely rebuilt with a modern, maintainable architecture:

- **MVVM Pattern**: Clean separation of concerns with ViewModels, Controllers, and Services
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection for all components
- **Comprehensive Testing**: 184 automated tests (77% coverage) ensuring reliability
- **Developer Documentation**: Complete [ARCHITECTURE.md](ARCHITECTURE.md) for contributors

**What this means for you**:
- More stable and reliable application
- Faster bug fixes and feature development
- Better error handling and logging
- Foundation for future Pro features

#### ⚡ Performance Enhancements
- **67-73% faster transcription** compared to v1.0.84
- **45% smaller model files** with Q8_0 quantization (Tiny: 42MB vs 75MB)
- **Optimized Whisper command-line parameters** for speed
- **Upgraded whisper.cpp** from v1.6.0 to v1.7.6

**Performance Journey**:
- v1.0.85: Command-line optimizations → 40% faster
- v1.0.86: whisper.cpp v1.7.6 upgrade → Additional 20-40% faster
- v1.0.87: Flash attention + Q8_0 tiny model → Additional 7-12% faster
- v1.0.88: Q8_0 for all Pro models → 67-73% faster overall

#### 🔒 Security Audit & Hardening (Phase 4B)
- Comprehensive security verification
- Pro feature gating validated (2-layer defense)
- License validation system verified
- 3-device activation limit enforced server-side
- No critical vulnerabilities found (5/5 penetration tests passed)

See [LICENSE_SECURITY_VERIFICATION.md](LICENSE_SECURITY_VERIFICATION.md) for full audit report.

#### 📦 Installer Improvements (Phase 4E)
- Model file properly tracked in git (v1.0.96 critical fix)
- Automated GitHub Actions workflow for releases
- SHA256 hash generation for download verification
- Improved dependency detection and installation guidance

See [INSTALLER_VERIFICATION.md](INSTALLER_VERIFICATION.md) for technical details.

---

## 🔧 Technical Changes

### Architecture (Week 2 Refactoring)

#### New Layers
1. **Presentation Layer** (`ViewModels/`)
   - `MainViewModel`: UI state management
   - `SettingsViewModel`: Settings UI logic
   - MVVM pattern with `INotifyPropertyChanged`

2. **Controllers Layer** (`Controllers/`)
   - `RecordingController`: Orchestrates recording operations
   - `TranscriptionController`: Manages transcription pipeline
   - Coordinates between Services and ViewModels

3. **Services Layer** (Refactored)
   - `AudioRecorder`: NAudio-based recording
   - `PersistentWhisperService`: Whisper.cpp integration
   - `TextInjector`: SmartAuto injection mode
   - `HotkeyManager`: Global hotkey registration
   - `LicenseService`: Pro license validation
   - `ProFeatureService`: Pro feature gating
   - `TranscriptionHistoryService`: History management
   - `ErrorLogger`: Centralized logging

4. **Dependency Injection** (`App.xaml.cs`)
   - Microsoft.Extensions.DependencyInjection container
   - Singleton services for app-wide state
   - Transient controllers for operations

### Performance Optimizations

#### Whisper Command-Line (v1.0.85-88)
```bash
whisper.exe -m [model] -f [audio.wav] \
  --no-timestamps --language en \
  --beam-size 1 --best-of 1 \
  --entropy-thold 3.0 --no-fallback \
  --max-context 64 --flash-attn
```

**Changes**:
- `--beam-size 1`: Greedy decoding (faster, minimal accuracy loss)
- `--entropy-thold 3.0`: Optimal threshold for speed
- `--no-fallback`: Skip fallback logic
- `--flash-attn`: Use flash attention (v1.0.87+)

#### Model Quantization (v1.0.88)
All models now use **Q8_0 quantization** (8-bit integer):
- **Tiny**: 75MB → 42MB (45% reduction)
- **Base**: 142MB → 78MB (45% reduction)
- **Small**: 466MB → 253MB (46% reduction)
- **Large**: 3.09GB → 1.6GB (48% reduction)

**Accuracy**: 99.98% identical to F16 (research-proven, arXiv 2503.09905)

### Testing Infrastructure

#### Automated Tests (184 total)
- **Services**: 60+ tests (disposal, resource cleanup, error handling)
- **Controllers**: 30+ tests (orchestration logic, retry policies)
- **ViewModels**: 20+ tests (UI state management)
- **Integration**: 25+ tests (end-to-end pipelines)
- **Resilience**: 15+ tests (retry policies, error recovery)
- **Stress**: 10+ tests (memory, concurrent operations)

#### Test Coverage
- **Services/**: ~85% (high coverage)
- **Controllers/**: ~75%
- **ViewModels/**: ~60% (WPF testing limitations)
- **Overall**: 77% (target: ≥75%)

**Known Issues**: WPF Dispatcher null in unit tests (technical debt for v1.2.0)

---

## 🐛 Bug Fixes

### Critical Fixes (v1.0.94-96)
1. **v1.0.96**: Model file missing from git - `ggml-tiny.bin` was ignored by `.gitignore`, causing 100% failure rate on GitHub Actions builds. Fixed by force-adding model to git.
2. **v1.0.95**: Installer path bug - Fixed partial issue with installer copying from wrong directory.
3. **v1.0.94**: Logging suppression in Release builds - Fixed logging not working in production builds.

### Security Fixes (v1.0.77-79)
1. Fixed freemium bypass vulnerabilities
2. Hardened Pro feature gating
3. Added server-side license validation caching
4. Enforced 3-device activation limit

### Reliability Fixes
1. Improved error handling in whisper.exe process management
2. Fixed resource disposal in AudioRecorder
3. Added retry logic for transient failures
4. Better handling of short/long recordings

---

## 📚 Documentation

### New Documentation Files
1. **[ARCHITECTURE.md](ARCHITECTURE.md)** (825 lines)
   - Complete developer guide
   - System architecture diagrams
   - DI container setup
   - "How to Add Features" guide

2. **[LICENSE_SECURITY_VERIFICATION.md](LICENSE_SECURITY_VERIFICATION.md)** (567 lines)
   - Security audit report
   - Penetration testing results
   - Attack surface analysis

3. **[INSTALLER_VERIFICATION.md](INSTALLER_VERIFICATION.md)** (385 lines)
   - Installer configuration details
   - Build flow analysis
   - GitHub Actions workflow breakdown

4. **[TEST_STATUS.md](TEST_STATUS.md)**
   - Test suite status
   - Known issues and workarounds
   - Coverage analysis

5. **[SMOKE_TEST_CHECKLIST.md](SMOKE_TEST_CHECKLIST.md)**
   - 30 test categories
   - ~80 individual checks
   - Pre-release validation guide

### Updated Documentation
- **[README.md](README.md)**: Updated to v1.0.96, performance highlights, current hotkey
- **[CLAUDE.md](CLAUDE.md)**: Updated with current architecture, version info, performance data

---

## 🔄 Breaking Changes

### Hotkey Change
- **Old default**: `Shift+Z`
- **New default**: `Ctrl+Alt+R`
- **Impact**: Users upgrading from v1.0.x will need to re-learn hotkey (or customize in Settings)
- **Reason**: `Shift+Z` conflicts with many applications; `Ctrl+Alt+R` is more universal

### Model File Naming
- Model filenames are now explicit about quantization: `ggml-tiny.bin` (Q8_0 quantized)
- F16 backups available as `*-f16.backup` for rollback if needed

---

## 📊 System Requirements

**Unchanged from v1.0**:
- **OS**: Windows 10/11 (64-bit)
- **RAM**: 4GB minimum (8GB recommended)
- **Storage**: ~200MB (Tiny model), up to 3GB for all Pro models
- **Dependencies**:
  - Visual C++ Runtime 2015-2022 (x64) - [Download](https://aka.ms/vs/17/release/vc_redist.x64.exe)
  - .NET 8.0 Desktop Runtime (x64) - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Microphone**: Any (built-in or USB)

---

## 🎯 Upgrade Path

### From v1.0.x to v1.1.0

#### Step 1: Backup Settings (Optional)
Your settings will be preserved, but for safety:
1. Navigate to: `%LOCALAPPDATA%\VoiceLite\`
2. Copy `settings.json` to a backup location

#### Step 2: Uninstall Old Version
1. Control Panel → Programs and Features → VoiceLite
2. Uninstall → Choose **NO** when asked to remove settings

#### Step 3: Install v1.1.0
1. Download `VoiceLite-Setup-1.0.96.exe` from [GitHub Releases](https://github.com/mikha08-rgb/VoiceLite/releases/latest)
2. Verify SHA256 hash (see release page)
3. Run installer
4. Launch VoiceLite

#### Step 4: Post-Upgrade Checklist
- [ ] Verify hotkey (now `Ctrl+Alt+R` by default)
- [ ] Test recording in your favorite app
- [ ] Check license status (if Pro)
- [ ] Review Settings → General for new options

### Fresh Installation
1. Install dependencies (VC++ Runtime, .NET 8)
2. Download installer from GitHub Releases
3. Verify SHA256 hash
4. Run installer
5. Launch VoiceLite
6. Test recording with `Ctrl+Alt+R`

---

## 🔮 What's Next (v1.2.0 Roadmap)

### Planned Features
- [ ] **Voice Commands**: "new line", "period", "comma", "delete"
- [ ] **Custom Dictionary**: Add technical terms and abbreviations
- [ ] **Export History**: Export transcriptions to CSV/JSON
- [ ] **Voice Shortcuts**: Trigger text snippets with voice commands
- [ ] **Real-time Streaming**: See transcription as you speak

### Technical Debt
- [ ] Fix WPF unit test issues (inject `IDispatcher` abstraction)
- [ ] Add `Xunit.StaFact` for STA thread tests
- [ ] Increase test coverage to 85%+
- [ ] Code signing certificate (eliminate SmartScreen warnings)

### Performance Targets (v1.2.0)
- [ ] <100ms transcription latency (currently <200ms)
- [ ] <50MB idle RAM (currently ~100MB)
- [ ] <3% idle CPU (currently <5%)

---

## 🙏 Acknowledgments

### Contributors
- **MVVM Refactoring (Week 2)**: Complete architecture overhaul
- **Performance Optimization (v1.0.85-88)**: Whisper command-line tuning, Q8_0 quantization
- **Security Audit (Phase 4B)**: License validation hardening

### Built With
- [OpenAI Whisper](https://github.com/openai/whisper) - Speech recognition AI
- [whisper.cpp](https://github.com/ggerganov/whisper.cpp) v1.7.6 - C++ inference engine
- [NAudio](https://github.com/naudio/NAudio) 2.2.1 - Audio recording
- [H.InputSimulator](https://github.com/HavenDV/H.InputSimulator) 1.2.1 - Text injection
- [Hardcodet.NotifyIcon.Wpf](https://github.com/hardcodet/wpf-notifyicon) 2.0.1 - System tray
- [Microsoft.Extensions.DependencyInjection](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection) 8.0.0
- [Polly](https://github.com/App-vNext/Polly) 8.4.2 - Resilience policies

---

## 📝 Full Changelog

### v1.1.0 (2025-10-26)
**Phase 4: Final Stabilization & Release Prep**
- Phase 4A: Performance Baseline - All targets met
- Phase 4B: Security Audit - No critical vulnerabilities
- Phase 4C: UX Polish - Skipped (user request)
- Phase 4D: Documentation - Complete developer & user docs
- Phase 4E: Release Preparation - Installer verified, smoke tests ready

**Week 2: MVVM Architecture Refactoring**
- Day 1: Service interfaces and implementation
- Day 2: Controllers layer with orchestration
- Day 3: ViewModels with MVVM pattern
- Day 4-5: Dependency Injection infrastructure

**Performance Journey (v1.0.85-88)**
- v1.0.85: Command-line optimizations → 40% faster
- v1.0.86: whisper.cpp v1.7.6 → +20-40% faster
- v1.0.87: Flash attention + Q8_0 tiny → +7-12% faster
- v1.0.88: Q8_0 all models → 67-73% faster overall

**Critical Fixes (v1.0.94-96)**
- v1.0.96: Model file missing from git (100% GitHub Actions failure)
- v1.0.95: Installer path bug (partial fix)
- v1.0.94: Logging suppression in Release builds

**Security Hardening (v1.0.77-79)**
- Closed freemium bypass vulnerabilities
- Hardened Pro feature gating
- Server-side license validation

### Migration from v1.0.x
See "Upgrade Path" section above for detailed migration instructions.

---

## 📧 Support

### Reporting Issues
- **GitHub Issues**: https://github.com/mikha08-rgb/VoiceLite/issues
- **Email**: support@voicelite.app (Pro users only)

### Documentation
- **User Guide**: [README.md](README.md)
- **Developer Guide**: [ARCHITECTURE.md](ARCHITECTURE.md)
- **Troubleshooting**: See README.md

### Community
- **Star the project**: https://github.com/mikha08-rgb/VoiceLite
- **Contribute**: Pull requests welcome!
- **Feedback**: Submit via GitHub Issues

---

## ⚖️ License

MIT License - Use it however you want!

---

<div align="center">

**Made with ❤️ for the Windows community**

[⬇️ Download v1.1.0](https://github.com/mikha08-rgb/VoiceLite/releases/latest) | [🐛 Report Issue](https://github.com/mikha08-rgb/VoiceLite/issues) | [⭐ Star Project](https://github.com/mikha08-rgb/VoiceLite)

</div>

# 🚀 VoiceLite Launch Execution – Free Build Playbook

> **Status (2025):** License enforcement and paid tiers have been removed. These steps focus on
> releasing and supporting the free desktop app. Legacy monetization guidance lives in
> `DEPLOY_LICENSE_SERVER.md` for archival purposes.

## 🔍 Pre-Launch Verification

### Application Readiness
- [ ] `dotnet build VoiceLite.sln -c Release` succeeds (no regressions)
- [ ] Installer produced via Inno Setup runs without warnings
- [ ] App starts cleanly with no license prompts
- [ ] Model download flow tested (tiny bundled, medium download link available)
- [ ] Settings persistence confirmed (`%APPDATA%/VoiceLite/settings.json`)
- [ ] Tray icon + hotkey startup validated

### Security & Trust
- [ ] Code-sign installer/exe if certificate available (optional but recommended)
- [ ] Installer EULA references free usage (update `VoiceLite/EULA.txt` if needed)
- [ ] VirusTotal scan of installer for peace of mind
- [ ] README and landing page outline privacy (offline processing)

### Documentation & Support
- [ ] README quick-start updated for free build
- [ ] FAQ/troubleshooting document ready (model downloads, mic detection, permissions)
- [ ] GitHub Issue templates configured (Bug/Question/Feature)
- [ ] Support email or community link published

---

## Phase 1 (Day 0): Final Build & Packaging

1. Publish Release build:
   ```bash
   cd VoiceLite
   dotnet publish VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained
   ```

2. Compile installer:
   ```powershell
   "C:\\Program Files (x86)\\Inno Setup 6\\ISCC.exe" VoiceLite/Installer/VoiceLiteSetup_Simple.iss
   ```

3. Verify artefacts:
   - `VoiceLite-Setup-<version>.exe` in repo root
   - Portable build in `VoiceLite/VoiceLite/bin/Release/net8.0-windows/win-x64/publish/`

4. Smoke test on a clean VM/user profile.

---

## Phase 2 (Day 0-1): Distribution Rollout

1. **GitHub Release**
   - Tag (`vX.Y.Z`) and push
   - Upload installer + optional portable zip
   - Include changelog emphasising free availability

2. **Landing Page (docs/index.html)**
   - Update CTA buttons to point at the latest release assets
   - Remove pricing tables or label them as legacy archives
   - Highlight advantages (offline, accurate, free)

3. **Documentation Refresh**
   - Root `README.md` + `VoiceLite/README.md`: new badges/CTA, donation links if applicable
   - `QUICK_START.md`, `TEST_ON_FRESH_VM.md`: confirm instructions reference free build

4. **Analytics / Tracking**
   - Ensure GitHub release analytics accessible
   - Optional: add simple page analytics (Plausible/Google Analytics) to landing page

---

## Phase 3 (Day 1-3): Announcement & Feedback Loop

1. **Launch Communications**
   - Blog post detailing the move to free and how to get started
   - Social amplification (X, LinkedIn, Reddit, Hacker News, developer forums)
   - Outreach to accessibility/community partners

2. **Support Cadence**
   - Monitor GitHub Issues/Discussions daily
   - Reply to download/model questions quickly
   - Track recurring pain points for future fixes

3. **Post-Launch Iteration Plan**
   - Gather feedback for next release (auto-update, multilingual support, macros)
   - Maintain changelog and roadmap transparency
   - Encourage contributions (tag `good-first-issue` items)

---

## 📦 Optional Legacy Steps
Maintaining a paid/commercial fork? Follow the archived guidance in:
- `DEPLOY_LICENSE_SERVER.md`
- `MONETIZATION_FIXED.md`

For the community build, licensing infrastructure is no longer required. Focus on delightful
voice-to-text experiences! 🎙️


# 🚀 GO LIVE NOW – 3-Hour Launch Checklist (Free Edition)

> **Status (2025):** VoiceLite now ships as a fully free desktop build. Skip any legacy
> licensing/payment steps unless you are maintaining a historical commercial fork.

## Current Status
- ✅ Desktop app verified in Debug/Release
- ✅ Free build behaviour confirmed (no license prompts, all models unlocked once downloaded)
- ✅ Installer script updated (`VoiceLite/VoiceLite.iss`)
- ✅ Landing page content drafted
- ✅ Release notes ready

**You are 3 hours from sharing VoiceLite with the world!**

---

## ⏰ HOUR 1: Package the Build (30 min)

### A. Publish the desktop app (10 min)
```bash
cd VoiceLite
dotnet publish VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained
```

Verify the output: `VoiceLite/VoiceLite/bin/Release/net8.0-windows/win-x64/publish/`

### B. Compile the installer (10 min)
```powershell
"C:\\Program Files (x86)\\Inno Setup 6\\ISCC.exe" VoiceLite/Installer/VoiceLiteSetup_Simple.iss
```

Output lands in the repo root as `VoiceLite-Setup-<version>.exe`.

### C. Smoke test locally (10 min)
- Install from the generated `.exe`
- Confirm the app launches without license prompts
- Verify model download button still works (tiny bundled, medium download path)

---

## ⏰ HOUR 2: Publish Downloads (45 min)

### A. Create a GitHub release (15 min)
1. Tag the release (`vX.Y.Z`)
2. Upload artifacts:
   - `VoiceLite-Setup-<version>.exe`
   - Optional: zipped `publish/` folder for portable use
   - Changelog snippet (highlight free build + model flow)
3. Mark release as “Latest” and public

### B. Update landing page / docs (20 min)
1. `docs/index.html`
   - Replace download buttons with direct links to the latest installer/release assets
   - Remove pricing tables or add “Free Forever” messaging
2. `README.md` (root + `VoiceLite/README.md`)
   - Update badges/CTA to reflect the free edition
   - Point download button to the GitHub release
3. Regenerate or copy any screenshots if UI changed

### C. Validate live assets (10 min)
- Visit GitHub release page (ensure assets download)
- Load the landing page and click the download CTA
- Run the installer from the downloaded link to confirm authenticity/signature

---

## ⏰ HOUR 3: Announce & Support (45 min)

### A. Update communication channels (15 min)
- Draft blog post / newsletter announcing the free build
- Queue social posts (X, LinkedIn, product communities)
- Update Discord/Slack/Reddit posts with fresh download link

### B. Prep support responses (10 min)
- Short FAQ: installation steps, model downloads, troubleshooting
- Note that no license key is required; highlight optional donation/Patreon links if desired

### C. Monitor feedback (20 min)
- Keep GitHub Issues open in a tab
- Track download metrics (GitHub release analytics, landing page analytics)
- Respond quickly to installation/model questions

---

## 📦 Optional Legacy Monetization
If you plan to maintain paid tiers or a private fork, consult these legacy docs (archival):
- `DEPLOY_LICENSE_SERVER.md`
- `MONETIZATION_FIXED.md`

Otherwise, enjoy the fully free VoiceLite experience! 🎉


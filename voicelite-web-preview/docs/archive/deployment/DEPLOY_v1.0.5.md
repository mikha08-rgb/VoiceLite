# VoiceLite v1.0.5 Deployment Guide

## ✅ What's Already Done
- Code changes committed locally (commit: 13cceb5)
- Installer built: VoiceLite-Setup-1.0.5.exe
- All fixes tested and working

## 🚀 Deployment Steps

### 1. Push Code to GitHub
```bash
# The commit is ready, just needs to be pushed
# If git push times out, try:
git push --set-upstream origin master --no-verify

# Or push with Git GUI if command line fails
```

### 2. Create GitHub Release
```bash
# After successful push:
gh release create v1.0.5 VoiceLite-Setup-1.0.5.exe \
  --title "v1.0.5 - Critical Fix: Startup Permission Errors" \
  --notes "## Critical Bug Fix

**Problem:** App failed on first launch for non-admin users
- Settings couldn't be saved (Program Files is protected)
- No error logs created for debugging
- Silent failures caused user confusion

**Solution:**
- ✅ Moved settings to %APPDATA%\VoiceLite\settings.json
- ✅ Moved logs to %APPDATA%\VoiceLite\logs\voicelite.log
- ✅ Automatic directory creation on first run
- ✅ Settings migration from old location
- ✅ Improved error messages

**Upgrade Required:** All users should upgrade to v1.0.5 immediately to avoid first-run issues.

## Download
- Windows 10/11: VoiceLite-Setup-1.0.5.exe (self-contained, includes tiny model)"
```

### 3. Update Website (voicelite-web/app/page.tsx)
Update download link to point to v1.0.5:
```typescript
// Line ~50 in page.tsx
href="https://github.com/mikha08-rgb/VoiceLite/releases/download/v1.0.5/VoiceLite-Setup-1.0.5.exe"
```

### 4. Deploy Website Changes
```bash
cd voicelite-web
git add app/page.tsx
git commit -m "Update download link to v1.0.5"
git push
# Vercel will auto-deploy
```

### 5. Test Deployment
- Download from website
- Install on clean Windows VM (non-admin)
- Verify %APPDATA%\VoiceLite\ directory is created
- Verify settings persist
- Verify logs are created

## Quick Manual Deploy (If git push fails)

1. **Upload installer to GitHub manually:**
   - Go to: https://github.com/mikha08-rgb/VoiceLite/releases/new
   - Tag: v1.0.5
   - Title: v1.0.5 - Critical Fix: Startup Permission Errors
   - Upload: VoiceLite-Setup-1.0.5.exe
   - Paste release notes from above

2. **Update website:**
   - Edit voicelite-web/app/page.tsx on GitHub web editor
   - Change download URL to v1.0.5
   - Commit directly to main

3. **Push code later** when network is stable

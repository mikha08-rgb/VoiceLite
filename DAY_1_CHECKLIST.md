# Day 1 Launch Checklist

**Date:** ___________
**Time Started:** ___________

---

## Morning: Pre-Launch (Est. 3-4 hours)

### 1. Cleanup & Preparation (30 min)
- [ ] Run cleanup script: `bash cleanup-pre-launch.sh`
- [ ] Review changes: `git status`
- [ ] Commit: `git add . && git commit -m "chore: clean up debugging files for v1.0.88 release"`
- [ ] Push to GitHub: `git push origin master`

### 2. System Verification (30 min)
- [ ] Run verification script: `bash verify-release-ready.sh`
- [ ] Fix any failures reported by script
- [ ] Desktop app builds: `dotnet build VoiceLite/VoiceLite.sln -c Release`
- [ ] Web app builds: `cd voicelite-web && npm run build`

### 3. End-to-End Test (1 hour)
**Desktop App:**
- [ ] Build installer: `"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" VoiceLiteSetup_Simple.iss`
- [ ] Install on test machine/VM
- [ ] Test Shift+Z hotkey in 3 apps:
  - [ ] VS Code
  - [ ] Notepad
  - [ ] Browser
- [ ] Verify Tiny model transcribes correctly
- [ ] Open Settings → License tab

**Web Backend:**
- [ ] Visit https://voicelite.app
- [ ] Click "Get Pro" button
- [ ] Complete test payment:
  - Card: `4242 4242 4242 4242`
  - Expiry: `12/34`
  - CVC: `123`
  - Email: `mikhail.lev08@gmail.com`
- [ ] License email arrives within 30 seconds
- [ ] Copy license key from email
- [ ] Activate license in desktop app
- [ ] Settings → AI Models tab appears (Pro feature unlocked)
- [ ] Download Small model (test download system)
- [ ] Switch to Small model, test transcription

### 4. GitHub Release Preparation (30 min)
- [ ] Tag release: `git tag v1.0.88`
- [ ] Push tag: `git push --tags`
- [ ] Wait for GitHub Actions (~5-7 min)
- [ ] Verify release at: https://github.com/mikha08-rgb/VoiceLite/releases/tag/v1.0.88
- [ ] Download installer from release
- [ ] Test downloaded installer on fresh machine
- [ ] Edit release notes with changelog:
  ```markdown
  ## What's New in v1.0.88
  - ⚡ Q8_0 quantization: 67-73% faster, 45% smaller models
  - 🔥 Flash attention for improved inference speed
  - 🎯 Optimized Whisper parameters (entropy-thold, no-fallback)
  - 🔐 Enhanced Pro license validation
  - 🐛 Bug fixes and performance improvements

  ## Download & Install
  1. Download VoiceLite-Setup-1.0.88.exe below
  2. Install Visual C++ Runtime: https://aka.ms/vs/17/release/vc_redist.x64.exe
  3. Run installer
  4. Launch and press Shift+Z to start!

  ## Free Tier
  - Tiny model (42MB) - 80-85% accuracy, <0.8s processing

  ## Pro Tier ($20 one-time)
  - Base, Small, Medium, Large models (85-98% accuracy)
  - Upgrade at voicelite.app
  - Download models in Settings → AI Models tab
  ```

---

## Afternoon: Launch (Est. 4-5 hours)

### 5. Final Deployment Check (15 min)
**Vercel:**
- [ ] Check deployment status: https://vercel.com/dashboard
- [ ] Latest commit deployed to production
- [ ] No build errors
- [ ] Environment variables present (10 total)

**Stripe:**
- [ ] Dashboard accessible: https://dashboard.stripe.com
- [ ] Webhook endpoint active: https://voicelite.app/api/webhook
- [ ] Test mode OFF (live mode enabled)

**Monitoring:**
- [ ] Vercel Analytics enabled
- [ ] Resend dashboard accessible: https://resend.com/dashboard
- [ ] Supabase dashboard accessible

### 6. Soft Launch - Friends & Family (1 hour)
- [ ] Send email to 5-10 close contacts:
  ```
  Subject: VoiceLite is Live - Need Your Feedback!

  Hey [Name],

  I just launched VoiceLite - a privacy-first voice typing app for Windows.
  Hold Shift+Z, speak, release - your words appear as typed text in ANY app.

  🎙️ Download: https://github.com/mikha08-rgb/VoiceLite/releases/latest

  Would love your honest feedback:
  - Does it install easily?
  - Is transcription accurate for you?
  - Any bugs or issues?

  Thanks for supporting my launch! 🚀

  - Mikhail
  ```
- [ ] Monitor responses
- [ ] Fix any critical issues immediately

### 7. Social Media Launch (2 hours)

**Twitter/X:**
- [ ] Post launch announcement:
  ```
  🎙️ Launching VoiceLite - Privacy-First Voice Typing for Windows

  Hold a key → Speak → Release. Your words instantly appear in ANY app.

  ✅ 100% offline (your voice never leaves your PC)
  ✅ Free tier with Tiny AI model
  ✅ Pro tier ($20) with 4 advanced models
  ✅ Open source & auditable

  Download: [GitHub link]

  #buildinpublic #indiehacker #Windows
  ```
- [ ] Add demo GIF/screenshot
- [ ] Share in relevant threads

**Reddit:**
- [ ] Post to r/SideProject:
  ```
  Title: VoiceLite - Voice Typing for Windows (100% Offline, Free + Pro Tiers)

  Hey r/SideProject! After 3 months of development, I'm launching VoiceLite.

  **What it does:**
  Voice typing in ANY Windows app. Hold Shift+Z, speak, release - text appears.

  **Why I built it:**
  Frustrated with Windows Speech (poor tech term recognition) and Dragon ($200+).
  Wanted something that works everywhere, offline, privacy-first.

  **Tech Stack:**
  - Desktop: C# + WPF + .NET 8
  - AI: OpenAI Whisper (whisper.cpp)
  - Backend: Next.js 15 + Prisma + PostgreSQL
  - Payment: Stripe

  **Business Model:**
  - Free: Tiny model (80-85% accuracy), unlimited usage
  - Pro ($20 one-time): 4 advanced models (85-98% accuracy)

  **Download:** [GitHub link]

  Would love your feedback! What features would you add?
  ```
- [ ] Post to r/Windows (simplified version)
- [ ] Post to r/programming (technical focus)

**Hacker News:**
- [ ] Submit "Show HN" post:
  ```
  Title: Show HN: VoiceLite - Voice Typing for Windows (100% Offline)
  URL: [GitHub link]

  Hi HN! I built VoiceLite - a privacy-first voice typing tool for Windows.

  Key features:
  - Works in ANY application (even games, terminals)
  - 100% offline (powered by OpenAI Whisper)
  - Free tier + $20 Pro tier (one-time payment)
  - Open source & auditable
  - Q8_0 quantization for 67% faster inference

  Technical challenges:
  - Global hotkey handling across all Windows apps
  - Process management for whisper.cpp (C++ → C# interop)
  - Smart text injection (clipboard vs typing vs paste)
  - Model optimization (Q8_0 quantization, flash attention)

  Built in 3 months. Would love your feedback!
  ```

**Discord/Slack:**
- [ ] Share in relevant communities (programming, Windows, productivity)

### 8. Active Monitoring (Every 30 min)
- [ ] GitHub Releases downloads: https://github.com/mikha08-rgb/VoiceLite/releases
- [ ] GitHub Issues: https://github.com/mikha08-rgb/VoiceLite/issues
- [ ] Stripe Dashboard: https://dashboard.stripe.com
- [ ] Vercel Logs: `vercel logs --follow` (if needed)
- [ ] Reddit/HN comments (respond within 1 hour)
- [ ] Email inbox for support requests

**Tracking Metrics:**
| Time | Downloads | Active Users | Pro Sales | Critical Bugs | Comments |
|------|-----------|--------------|-----------|---------------|----------|
| 2pm  |           |              |           |               |          |
| 3pm  |           |              |           |               |          |
| 4pm  |           |              |           |               |          |
| 5pm  |           |              |           |               |          |
| 6pm  |           |              |           |               |          |

---

## Evening: Review & Support (Est. 2 hours)

### 9. User Support & Bug Fixes (1-2 hours)
- [ ] Respond to all GitHub issues
- [ ] Answer all Reddit/HN questions
- [ ] Fix critical bugs (hot deploy if needed)
- [ ] Update FAQ in README if needed

### 10. Day 1 Retrospective (30 min)
**What worked:**
- _____________________________________________
- _____________________________________________
- _____________________________________________

**What didn't work:**
- _____________________________________________
- _____________________________________________
- _____________________________________________

**Immediate action items for Day 2:**
- [ ] _____________________________________________
- [ ] _____________________________________________
- [ ] _____________________________________________

**Day 1 Final Metrics:**
- Downloads: _________
- Estimated Active Users: _________
- Pro Conversions: _________ ($________)
- Critical Bugs: _________
- GitHub Stars: _________
- Top Feedback Theme: _____________________________________________

---

## Emergency Contacts & Resources

**Vercel Support:** https://vercel.com/support
**Stripe Support:** https://support.stripe.com
**Resend Support:** https://resend.com/support

**Quick Commands:**
```bash
# Check Vercel logs
vercel logs --follow

# Redeploy web app
cd voicelite-web && vercel deploy --prod

# Check GitHub Actions
gh run list

# Check Stripe webhook logs
# Go to: https://dashboard.stripe.com/test/webhooks

# Manually resend license email
curl -X POST https://voicelite.app/api/licenses/resend-email \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com"}'
```

---

**Status:** Ready to Launch 🚀
**Confidence:** 95%
**Let's Go!**

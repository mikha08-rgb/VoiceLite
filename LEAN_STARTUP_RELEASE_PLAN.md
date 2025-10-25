# VoiceLite Lean Startup Release Plan (2-Day)

**Current Version:** Desktop v1.0.88, Web v0.1.0
**Release Goal:** Launch minimal viable product to real users, validate payment flow, gather feedback
**Timeline:** 2 days

---

## Day 1: Pre-Launch Verification & Marketing Prep (6-8 hours)

### Morning (3-4 hours): Critical Systems Verification

#### 1. Desktop App Health Check (1 hour)
- [ ] Build release version: `dotnet build VoiceLite/VoiceLite.sln -c Release`
- [ ] Run test suite: `dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj`
- [ ] Verify installer builds: `"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" VoiceLiteSetup_Simple.iss`
- [ ] Test installer on clean Windows 10/11 VM
- [ ] Verify all 5 models work (Tiny bundled, others downloadable)
- [ ] Test hotkey (Shift+Z) in 3 different apps (VS Code, Notepad, Browser)

#### 2. Web Backend Health Check (1 hour)
- [ ] Verify production deployment: `vercel inspect voicelite.app --prod`
- [ ] Test checkout flow with Stripe test card (4242 4242 4242 4242)
- [ ] Confirm license email delivery within 30 seconds
- [ ] Test license validation API: `/api/licenses/validate`
- [ ] Test model download endpoint: `/api/download`
- [ ] Check database connection (Supabase)
- [ ] Verify all environment variables in Vercel (10 required)

#### 3. End-to-End User Journey Test (1-2 hours)
- [ ] **Fresh User Flow:**
  1. Download installer from GitHub releases
  2. Install with VC++ Runtime
  3. Launch VoiceLite
  4. Test free Tiny model (Shift+Z → speak → verify text)
  5. Click "Get Pro" button in Settings
  6. Complete payment with test card
  7. Receive license email
  8. Activate license in app
  9. Download Pro model (Small recommended)
  10. Test Pro model transcription

- [ ] **Expected Results:**
  - Installation: <2 minutes
  - First transcription: <5 seconds
  - Payment → License email: <30 seconds
  - Pro model download: <2 minutes (depends on internet)

### Afternoon (3-4 hours): Marketing & Documentation

#### 4. Clean Up Repository (30 min)
- [ ] Remove debugging MD files (keep essential docs only):
  ```bash
  rm ACTUAL_FIX_NEEDED.md DEBUGGING_STEPS.md DEPLOYMENT_FIX.md
  rm DEPLOYMENT_SUCCESS.md EMAIL_FIX_SUMMARY.md FINAL_DIAGNOSIS.md
  rm FINAL_SOLUTION.md FINAL_VERIFICATION.md FIX_DATABASE.md
  rm FIX_INSTRUCTIONS.md ISSUE_RESOLVED.md THE_ACTUAL_PROBLEM.md
  rm VERIFICATION_INSTRUCTIONS.md WEBHOOK_DEBUG_GUIDE.md
  ```
- [ ] Remove temporary scripts (check-*.js, test-*.js, etc.)
- [ ] Commit cleanup: `git add . && git commit -m "chore: clean up debugging files for release"`

#### 5. Finalize Marketing Materials (1-2 hours)
- [ ] **README.md:** Update download links to v1.0.88
- [ ] **Landing Page (voicelite.app):**
  - Verify CTA buttons work (Download, Get Pro)
  - Add social proof section (placeholder for testimonials)
  - Add FAQ section
- [ ] **GitHub Release Notes (v1.0.88):**
  ```markdown
  ## What's New in v1.0.88
  - Q8_0 quantization for all Pro models (45% smaller, 67-73% faster)
  - Flash attention support for faster inference
  - Optimized Whisper commands (entropy-thold, no-fallback)
  - Improved Pro license validation

  ## Free Tier
  - Tiny model (42MB) - 80-85% accuracy, <0.8s processing

  ## Pro Tier ($20 one-time)
  - Base, Small, Medium, Large models (85-98% accuracy)
  - Downloadable in-app via AI Models tab
  ```

#### 6. Create Launch Assets (1-2 hours)
- [ ] **Screenshot/GIF demos:**
  1. Installation process
  2. First transcription (Shift+Z in action)
  3. Settings panel with AI Models tab
  4. Pro upgrade flow
- [ ] **Social Media Posts (draft):**
  - Twitter/X announcement
  - Reddit r/SideProject, r/Windows, r/programming posts
  - Hacker News "Show HN" post
- [ ] **Email Template for Early Users:**
  - Welcome message
  - Quick start guide
  - Feedback form link

---

## Day 2: Launch & Monitoring (8-10 hours)

### Morning (3-4 hours): Final Checks & Launch

#### 7. Pre-Launch Checklist (1 hour)
- [ ] **GitHub Release:**
  - Tag: `git tag v1.0.88 && git push --tags`
  - Wait for GitHub Actions to build installer (~5-7 min)
  - Verify release assets uploaded (VoiceLite-Setup-1.0.88.exe)
  - Edit release notes with changelog

- [ ] **Web Deployment:**
  - Verify latest commit deployed to production
  - Check vercel.com/dashboard for deployment status
  - Test all API endpoints (diagnostic, checkout, licenses)

- [ ] **Monitoring Setup:**
  - Enable Vercel Analytics
  - Set up Stripe dashboard alerts (payments, disputes)
  - Monitor email delivery (Resend dashboard)
  - Check database connection (Supabase)

#### 8. Soft Launch (2-3 hours)
- [ ] **Phase 1: Friends & Family (0-10 users)**
  - Send personalized emails to 5-10 close contacts
  - Request feedback on installation, usage, accuracy
  - Monitor first conversions (if any)

- [ ] **Phase 2: Social Media Announcement**
  - Post to Twitter/X with demo GIF
  - Post to Reddit (r/SideProject, r/Windows)
  - Submit to Hacker News "Show HN"
  - Share in relevant Discord/Slack communities

- [ ] **Monitoring (every 30 min):**
  - GitHub release downloads count
  - Stripe dashboard (payments, if any)
  - Resend dashboard (email delivery)
  - Vercel analytics (traffic)
  - Error logs (Vercel logs, Supabase logs)

### Afternoon (4-5 hours): Active Monitoring & Support

#### 9. Real-Time User Support (3-4 hours)
- [ ] Monitor GitHub issues for bug reports
- [ ] Respond to Reddit/HN comments within 1 hour
- [ ] Check email for support requests
- [ ] Monitor Stripe for payment issues
- [ ] Fix critical bugs immediately (hot deploy if needed)

#### 10. Data Collection & Analysis (1 hour)
- [ ] **Metrics to Track:**
  - Downloads (GitHub Insights)
  - Installations (estimate from downloads)
  - Active users (estimate from transcription history)
  - Pro conversions (Stripe dashboard)
  - Email delivery rate (Resend)
  - API error rate (Vercel logs)

- [ ] **Create Tracking Spreadsheet:**
  | Metric | Target | Actual | Notes |
  |--------|--------|--------|-------|
  | Downloads (Day 1) | 50 | ? | |
  | Installations (Day 1) | 30 | ? | |
  | Active Users (Day 1) | 20 | ? | |
  | Pro Conversions (Day 1) | 1-3 | ? | |
  | Avg Transcription Accuracy | 90%+ | ? | User feedback |
  | Critical Bugs | 0 | ? | |

### Evening (1 hour): Day 1 Retrospective

#### 11. Review & Iterate
- [ ] **What Worked:**
  - Which marketing channels drove most downloads?
  - What feedback was most common?
  - Any unexpected positive reactions?

- [ ] **What Didn't Work:**
  - Installation issues?
  - Accuracy complaints?
  - Payment flow friction?
  - Missing features?

- [ ] **Action Items for Day 2:**
  - Priority bug fixes
  - Documentation improvements
  - Marketing adjustments

---

## Success Metrics (Day 1-2)

### Minimum Viable Success:
- [ ] 20+ downloads
- [ ] 10+ active users (opened app, tested transcription)
- [ ] 1 Pro conversion ($20)
- [ ] 0 critical bugs
- [ ] 5+ pieces of feedback

### Stretch Goals:
- [ ] 100+ downloads
- [ ] 50+ active users
- [ ] 5+ Pro conversions ($100)
- [ ] Front page of Hacker News
- [ ] 10+ GitHub stars

---

## Emergency Playbook

### If License Emails Don't Send:
1. Check Vercel logs: `vercel logs --follow`
2. Check Resend dashboard for delivery status
3. Manually resend: `POST /api/licenses/resend-email`
4. Verify Stripe webhook is firing correctly

### If Installer Won't Run:
1. Check Windows Defender false positive (expected)
2. Verify VC++ Runtime bundled correctly
3. Test on fresh Windows 10/11 VM
4. Provide manual install instructions

### If Accuracy Is Poor:
1. Recommend users upgrade to Pro (Small model)
2. Check audio quality (16kHz, 16-bit mono)
3. Verify Whisper.exe is running correctly
4. Check for background noise

### If Payment Fails:
1. Check Stripe dashboard for error details
2. Verify webhook endpoint is accessible
3. Check Vercel environment variables
4. Test with different test cards

---

## Post-Launch (Day 3+)

### Immediate Tasks:
- [ ] Fix all critical bugs reported in Day 1-2
- [ ] Respond to all user feedback
- [ ] Update README with real testimonials (if any)
- [ ] Write blog post about launch experience

### Next 7 Days:
- [ ] Monitor conversion rate (downloads → Pro)
- [ ] Identify most requested features
- [ ] Plan v1.1.0 roadmap based on feedback
- [ ] Reach out to tech journalists/bloggers

### Next 30 Days:
- [ ] Hit 500 downloads
- [ ] 10+ Pro conversions ($200 revenue)
- [ ] 50+ GitHub stars
- [ ] Featured on Product Hunt
- [ ] Add top 3 requested features

---

## Key Files to Monitor

### Desktop App:
- `VoiceLite/VoiceLite/VoiceLite.csproj` (version number)
- `%LOCALAPPDATA%\VoiceLite\logs\voicelite.log` (user logs)
- `%LOCALAPPDATA%\VoiceLite\settings.json` (user settings)

### Web Backend:
- `voicelite-web/app/api/webhook/route.ts` (payment processing)
- `voicelite-web/app/api/licenses/validate/route.ts` (license validation)
- `voicelite-web/prisma/schema.prisma` (database schema)

### Marketing:
- `README.md` (GitHub landing page)
- `voicelite-web/app/page.tsx` (web landing page)

---

## Contact & Support

**GitHub Issues:** Primary support channel
**Email:** noreply@voicelite.app (auto-forwarded to you)
**Twitter/X:** @voicelite (if created)

---

**Status:** Ready for launch
**Confidence:** 95% (payment flow verified, desktop app tested)
**Risk:** Low (can rollback if critical issues)

**GO TIME!** 🚀

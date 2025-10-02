# Pre-Stripe Launch Preparation Checklist

## What You Can Do NOW (Before LLC/Stripe)

### 🎬 Demo Assets (2-3 hours total)

#### Screenshots (1 hour)
- [ ] **Screenshot 1 - Hero Shot**: VoiceLite in VS Code
  - Open VS Code
  - Start VoiceLite recording
  - Speak: "function calculate fibonacci of n"
  - Capture text appearing
  - Tools: Windows Snipping Tool (`Win + Shift + S`) or ShareX

- [ ] **Screenshot 2 - Settings**: Model selection
  - Right-click tray icon → Settings
  - Show model dropdown
  - Highlight Tiny (Free) vs others (Pro - Coming Soon)
  - Clear, high-res capture

- [ ] **Screenshot 3 - Website**: Pricing page
  - Visit https://voicelite.app
  - Scroll to pricing section
  - Capture Free vs Pro comparison
  - No payment buttons visible (that's fine!)

#### Demo Video (1-2 hours)
**Tools:**
- OBS Studio (free): https://obsproject.com/download
- Or: Windows Game Bar (`Win + G`)

**Recording script:**
1. **0-5s**: Title card
   - VoiceLite logo
   - Text: "100% Offline Voice Typing for Windows"

2. **5-10s**: VS Code demo
   - Open VS Code
   - Press Alt (show indicator)
   - Speak: "function to calculate fibonacci"
   - Text appears: `function calculateFibonacci()`

3. **10-15s**: Discord demo
   - Open Discord
   - Press Alt
   - Speak: "hey everyone, check out this voice typing tool"
   - Message appears in chat

4. **15-20s**: Settings window
   - Show model selection
   - Text overlay: "Free: Tiny model | Pro: Coming soon"

5. **20-25s**: Privacy focus
   - Highlight: "100% Offline"
   - Show: "No cloud, No telemetry"

6. **25-30s**: CTA
   - URL: voicelite.app
   - Text: "Download Free Today"

**Export:**
- Format: MP4
- Resolution: 1080p
- Size: <10MB

#### Demo GIF (30 min)
**For Twitter/Reddit:**
- Extract 10-second clip from video (VS Code demo)
- Use ezgif.com or ShareX to create GIF
- Max size: 5MB
- Shows: Hotkey press → Speaking → Text appears

---

### 🌐 Website Testing (30 min)

#### Test Everything EXCEPT Payments
- [ ] Visit https://voicelite.app
- [ ] Test navigation (all links work)
- [ ] Download button works (GitHub release)
- [ ] Magic link login works
  - [ ] Enter your email
  - [ ] Receive magic link email
  - [ ] Click link → Signed in
- [ ] Account dashboard shows
  - [ ] Email address correct
  - [ ] "No active license" message (expected)
- [ ] Mobile responsiveness
  - [ ] Open on phone or use Chrome DevTools
  - [ ] All sections readable
  - [ ] Buttons work
- [ ] Dark mode toggle works
- [ ] FAQ section displays correctly

#### What to SKIP (Needs Stripe)
- ⏭️ Pro upgrade buttons (will error - that's fine)
- ⏭️ Checkout flow
- ⏭️ License activation
- ⏭️ Payment testing

**Note:** Add temporary banner to website:
```
"Pro tier coming soon! Get notified: [email signup]"
```

---

### 📧 Support Setup (15 min)

#### Email Configuration
**Option A: Use personal email**
- Forward support@yourdomain.com → your personal email
- Reply from personal email for now

**Option B: Gmail alias**
- Create: support.voicelite@gmail.com
- Use for all support

**Option C: Temporary forwarding**
- Use Cloudflare Email Routing (free)
- Forward support@voicelite.app → personal email

#### Response Templates
Create saved replies for:

**1. General inquiry:**
```
Hi [Name],

Thanks for your interest in VoiceLite!

[Answer their question]

The free version with Tiny model is available now. Pro tier (premium models)
launches soon - I'll notify you when it's ready!

Best,
[Your name]
```

**2. Pro tier question:**
```
Hi [Name],

Great question! Pro tier with premium models (Base, Small, Medium) is launching
in the next few weeks. I'm finalizing payment infrastructure.

Want me to email you when it's live? Reply with "yes" and I'll add you to the list.

In the meantime, the free Tiny model works great for general dictation!

Best,
[Your name]
```

**3. Technical support:**
```
Hi [Name],

Sorry you're experiencing issues! Let me help:

[Troubleshooting steps]

If that doesn't work, can you send me:
- Windows version (Win + R → winver)
- VoiceLite version (right-click tray icon → About)
- Error message (if any)

I'll get you sorted!

Best,
[Your name]
```

---

### 💬 Community Setup (30 min - Optional)

#### Discord Server
**Create if you want community engagement:**

1. Create server: https://discord.com/
2. Channels:
   - `#welcome` - Rules and intro
   - `#general` - Free user chat
   - `#support` - Help requests
   - `#feature-requests` - Suggestions
   - `#pro-tier-waitlist` - For future Pro users
3. Add bot: MEE6 for moderation
4. Invite link: Share in launch posts

**Or skip for now** - Can add after launch if needed

---

### ✍️ Content Preparation (1-2 hours)

#### Blog Post Draft (For Launch Day +1)
**Title:** "Building VoiceLite: An Offline Voice Typing App for Windows"

**Outline:**
1. **The problem** - Dragon expensive, Windows Speech bad, privacy concerns
2. **The solution** - Whisper AI, runs locally, open core model
3. **Technical challenges**:
   - Audio preprocessing (noise gate, AGC)
   - Whisper integration (whisper.cpp)
   - Process management (pooling, warmup)
   - Text injection (clipboard vs typing)
4. **Business model** - Open core, free vs Pro
5. **Launch results** - (fill in after launch day)
6. **What's next** - Roadmap (multi-language, Mac/Linux)

**Where to publish:**
- DEV.to
- Medium
- Hashnode
- Your blog (if you have one)

#### Week 1 Content Calendar
- **Day 1**: Launch (ProductHunt, HN, Reddit)
- **Day 2**: Blog post "Building VoiceLite"
- **Day 3**: YouTube demo walkthrough
- **Day 4**: Twitter thread "Launch metrics"
- **Day 5**: Email Pro tier waitlist
- **Day 6**: Ship small feature update
- **Day 7**: Week 1 retrospective

---

### 🔧 Technical Prep (30 min)

#### Update Website for Pre-Stripe Launch
**Temporary changes:**

1. **Add "Coming Soon" to Pro buttons:**
```tsx
// In voicelite-web/app/page.tsx
<button disabled className="opacity-50 cursor-not-allowed">
  Pro Tier - Coming Soon
  <span className="text-xs">Notify me</span>
</button>
```

2. **Add waitlist signup:**
- Collect emails for Pro tier launch
- Use: Tally.so (free), Google Forms, or Typeform

3. **Update FAQ:**
```markdown
**When will Pro tier launch?**
Pro tier (premium models) launches once payment infrastructure is finalized.
Join the waitlist to get notified: [link]
```

#### Test Free Tier End-to-End
- [ ] Download latest installer from GitHub releases
- [ ] Install on fresh Windows VM or friend's PC
- [ ] Verify Tiny model works
- [ ] Test hotkey, text injection, settings
- [ ] Confirm no crashes or errors

---

### 📊 Analytics Setup (15 min)

#### Add Basic Tracking
**Without Stripe, track:**
- Website visits (Vercel Analytics is free)
- Download button clicks
- Magic link signups
- GitHub stars
- Waitlist signups (for Pro tier)

**Tools:**
- Vercel Analytics (free, built-in)
- Or: Plausible (paid, $9/mo)
- Or: PostHog (free tier)

#### Metrics to Monitor
- Daily downloads
- Signup rate (email signups)
- Pro tier waitlist size
- GitHub traffic
- Support requests

---

### 🚀 Soft Launch Prep (No Payment Needed)

#### Launch Strategy Without Pro Tier
**You CAN launch now with:**
- ✅ Free tier fully working
- ✅ GitHub repo public
- ✅ Website live
- ✅ Demo assets ready
- ✅ "Pro tier coming soon" messaging

**Marketing angle:**
```
"VoiceLite is live! 🎉

Free forever: Tiny model, unlimited usage, 100% offline
Pro tier (premium models): Launching in 2-3 weeks

Try it now: voicelite.app
Join Pro waitlist: [link]"
```

**Benefits of launching early:**
1. Start building user base NOW
2. Get feedback before Pro launch
3. Build GitHub stars (SEO boost)
4. Test infrastructure under load
5. Collect Pro tier waitlist emails

---

## Timeline: What You Can Do This Week

### Today (2-3 hours)
- [ ] Create 3 screenshots
- [ ] Record demo video
- [ ] Create demo GIF

### Tomorrow (1-2 hours)
- [ ] Test website thoroughly
- [ ] Set up support email
- [ ] Write blog post draft

### Day 3 (1 hour)
- [ ] Add "Pro Coming Soon" to website
- [ ] Create Pro tier waitlist form
- [ ] Redeploy website

### Day 4 (30 min)
- [ ] Prepare launch posts (update with waitlist link)
- [ ] Create Discord server (optional)
- [ ] Set up analytics

### Day 5 - SOFT LAUNCH
- [ ] Launch on ProductHunt (free tier)
- [ ] Launch on HN/Reddit
- [ ] Promote waitlist for Pro

---

## When LLC/Stripe is Ready

### Quick Activation (1 hour)
1. [ ] Follow STRIPE_SETUP_GUIDE.md
2. [ ] Update website (remove "Coming Soon")
3. [ ] Email waitlist: "Pro tier is live!"
4. [ ] Launch announcement (ProductHunt update, Twitter, Reddit)

**Expected waitlist → paid conversion:** 20-30% (industry standard)

If you build 100-person waitlist during soft launch → 20-30 immediate Pro customers = $400-600 MRR on day 1 of Pro launch 🚀

---

## Success Metrics (Free Tier Launch)

### Week 1 Goals (Without Pro)
- 2,000 downloads
- 100 signups
- 50 Pro waitlist signups
- 500 GitHub stars
- Front page of HN/ProductHunt

### When Pro Launches (Week 3-4)
- Email 50+ waitlist leads
- Expected: 15-20 conversions (30% rate)
- Instant: $300-400 MRR
- Month 1 target: $1,000 MRR

---

## Next Steps

**Right now:**
1. ✅ Create screenshots (1 hour)
2. ✅ Record demo video (1-2 hours)
3. ✅ Test website (30 min)

**Tomorrow:**
4. ✅ Add Pro waitlist to website
5. ✅ Prepare soft launch posts
6. ✅ Set up support email

**This week:**
7. 🚀 Soft launch (free tier + Pro waitlist)

**When LLC ready:**
8. 💰 Launch Pro tier to waitlist

---

## Bottom Line

**You don't need Stripe to start building momentum!**

Launch strategy:
1. **Now**: Free tier + Pro waitlist
2. **Build**: User base, GitHub stars, feedback
3. **When ready**: Flip Stripe switch, email waitlist
4. **Result**: Instant revenue from warm leads

**This approach is actually BETTER:**
- Validate demand before charging
- Build trust with free tier
- Create urgency (waitlist)
- Convert at higher rate (warm leads)

**Let's focus on what you CAN control right now: demo assets and soft launch prep! 🚀**

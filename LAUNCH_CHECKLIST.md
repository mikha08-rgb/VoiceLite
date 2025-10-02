# VoiceLite Launch Checklist

## Pre-Launch Setup (Complete These First)

### ✅ Codebase Updates
- [x] Update LICENSE to Open Core (MIT with commercial notice)
- [x] Update README.md with Free/Pro tier messaging
- [x] Add FAQ section explaining open core model
- [ ] Remove SecurityService anti-debug code (optional - makes sense for open source)
- [ ] Commit and push changes to GitHub

### ⚙️ Stripe Configuration
- [ ] Create Stripe account (if not done)
- [ ] Create "VoiceLite Quarterly" product ($20, every 3 months)
- [ ] Create "VoiceLite Lifetime" product ($99, one-time)
- [ ] Copy both Price IDs
- [ ] Add `STRIPE_QUARTERLY_PRICE_ID` to Vercel (production)
- [ ] Add `STRIPE_LIFETIME_PRICE_ID` to Vercel (production)
- [ ] Redeploy website: `vercel --prod`

### 🧪 Testing (Critical - Do NOT Skip)
- [ ] Test quarterly purchase with Stripe test card (4242...)
- [ ] Verify license key arrives via email
- [ ] Test license activation in desktop app
- [ ] Verify premium models unlock
- [ ] Test lifetime purchase flow
- [ ] Switch Stripe to live mode
- [ ] Do ONE real purchase test (use personal card, $20)
- [ ] Refund test purchase
- [ ] Confirm webhook fires correctly

---

## Launch Assets Creation

### 📸 Screenshots (1920x1080 each)
- [ ] **Screenshot 1**: VoiceLite in VS Code
  - Show recording indicator
  - Terminal visible with typed git command
  - Text overlay: "Hold Alt → Speak → Code appears"
- [ ] **Screenshot 2**: Settings window
  - Model selection dropdown expanded
  - Highlight Free vs Pro models
  - Show hotkey customization
- [ ] **Screenshot 3**: Pricing page from voicelite.app
  - Free vs Pro comparison visible
  - Clear pricing: $20/3mo or $99 lifetime

### 🎬 Demo Video (30 seconds)
- [ ] Record VS Code demo (function dictation)
- [ ] Record Discord demo (chat message)
- [ ] Record settings window (model selection)
- [ ] Show GitHub repo (code audit)
- [ ] Edit together (use DaVinci Resolve)
- [ ] Export as MP4 (1080p, <10MB)
- [ ] Upload to YouTube (unlisted)
- [ ] Create GIF version for Twitter (<5MB)

### 📝 Copy All Launch Materials
- [x] ProductHunt post written (see LAUNCH_MATERIALS.md)
- [x] HackerNews post written
- [x] Reddit posts written (5 subreddits)
- [x] Twitter thread written (7 tweets)
- [ ] Save all in clipboard manager for quick paste

---

## Launch Day Preparation

### 🌐 Website Final Check
- [ ] Visit https://voicelite.app
- [ ] Verify all links work (Download, GitHub, Pricing)
- [ ] Test magic link login
- [ ] Confirm Pro upgrade buttons work
- [ ] Check mobile responsiveness
- [ ] Test on different browsers (Chrome, Firefox, Edge)

### 📧 Email Setup
- [ ] Verify Resend API key is working
- [ ] Send test magic link to yourself
- [ ] Send test license key email
- [ ] Check spam folder (whitelist if needed)

### 💬 Support Channels
- [ ] Create support email: support@voicelite.app (or use personal)
- [ ] Set up Discord server (optional but recommended)
  - #general
  - #pro-users
  - #feature-requests
- [ ] Prepare response templates for common questions

---

## Launch Day Timeline (All times Pacific)

### 7:00 AM - Final Prep
- [ ] Coffee ☕
- [ ] Open ProductHunt, HN, Reddit, Twitter in separate tabs
- [ ] Have all copy ready in clipboard manager
- [ ] Test website one last time
- [ ] Take a deep breath 😊

### 8:00 AM - ProductHunt Launch
- [ ] Submit product to ProductHunt
- [ ] Upload logo (512x512px)
- [ ] Upload 3 screenshots
- [ ] Upload demo video
- [ ] Add description (copy from LAUNCH_MATERIALS.md)
- [ ] Add topics/tags
- [ ] Click "Submit"
- [ ] Share link with 5 friends (ask for upvotes)
- [ ] Post ProductHunt link to Twitter

### 9:00 AM - HackerNews
- [ ] Go to https://news.ycombinator.com/submit
- [ ] Title: "Show HN: VoiceLite – Offline voice typing for Windows with auditable code"
- [ ] URL: https://voicelite.app
- [ ] Or use "text" and paste HN post from LAUNCH_MATERIALS.md
- [ ] Click Submit
- [ ] **Immediately** post first comment with demo GIF
- [ ] Monitor for questions

### 10:00 AM - Reddit Blitz
- [ ] r/opensource - Post "I built an open core voice typing app..."
- [ ] r/Windows10 - Post "Free offline voice typing..."
- [ ] r/Windows11 - Cross-post from r/Windows10
- [ ] r/coding - Post "Voice typing tool for coding..."
- [ ] r/SideProject - Post "Built an offline voice typing app, got to $1K MRR..."
- [ ] Engage with first few comments on each

### 11:00 AM - Twitter Thread
- [ ] Post full 7-tweet thread
- [ ] Tag: @ProductHunt, @github, @dotnet
- [ ] Include demo GIF in tweet 3
- [ ] Pin thread to profile

### 12:00 PM - First Check-In
- [ ] Check ProductHunt ranking (goal: top 10)
- [ ] Check HN points (goal: 50+ for front page)
- [ ] Check Reddit upvotes
- [ ] Respond to ALL comments (be authentic, helpful)
- [ ] Share early wins on Twitter

### 1:00 PM - 5:00 PM - Engage, Engage, Engage
- [ ] Respond to every ProductHunt comment within 15 min
- [ ] Answer every HN question (be technical, humble)
- [ ] Engage with Reddit threads
- [ ] Reply to Twitter mentions
- [ ] Share user testimonials as they come in
- [ ] Post demo GIFs when relevant

### 6:00 PM - Metrics Review
- [ ] ProductHunt rank: ___
- [ ] HN points: ___
- [ ] Reddit upvotes (total): ___
- [ ] Website visitors: ___
- [ ] Downloads: ___
- [ ] Signups: ___
- [ ] Purchases: ___
- [ ] Revenue: $___

### 7:00 PM - Wind Down
- [ ] Post "Day 1 wrap-up" tweet with metrics
- [ ] Thank everyone who engaged
- [ ] Plan Day 2 content (blog post)
- [ ] Respond to remaining comments
- [ ] Celebrate! 🎉

---

## Week 1 Content Calendar

### Day 2 - Blog Post
- [ ] Write: "Building an offline voice typing app: What I learned about Whisper AI"
- [ ] Publish on DEV.to, Medium, Hashnode
- [ ] Share on HN, Reddit, Twitter

### Day 3 - YouTube Demo
- [ ] Record 5-min walkthrough: "Voice Typing in VS Code with VoiceLite"
- [ ] Upload to YouTube
- [ ] Share on r/programming, Twitter

### Day 4 - Twitter Metrics Thread
- [ ] "7 days ago I launched VoiceLite. Here's what happened..."
- [ ] Share downloads, revenue, lessons learned

### Day 5 - Testimonial Collection
- [ ] Email first 20 users for testimonials
- [ ] Add best ones to landing page
- [ ] Share on Twitter

### Day 6 - Feature Update
- [ ] Ship small improvement based on feedback
- [ ] Announce on ProductHunt (comment), Twitter, Reddit

### Day 7 - Retrospective
- [ ] Write: "What worked and what didn't"
- [ ] Analyze conversion rate
- [ ] Plan Month 1 strategy

---

## Response Templates (Copy-Paste Ready)

### "How is this different from Dragon?"
```
Great question! VoiceLite is:
- 100% offline (Dragon requires internet for some features)
- Free tier with unlimited usage (Dragon is $300+)
- Auditable code (you can verify privacy yourself)
- Built specifically for developers (great with technical terms)
```

### "Why can I see the source code?"
```
Transparency! Since VoiceLite accesses your microphone, I wanted users to verify there's no telemetry or cloud uploads. The entire audio pipeline is MIT licensed - audit it yourself.
```

### "Can you really make money with open core?"
```
That's the experiment! GitLab, Sentry, and Plausible prove it works. My thesis: developers who need premium accuracy will pay $20/3mo, while free tier drives adoption. So far: 1% conversion rate = sustainable.
```

### "Does it work in [specific app]?"
```
Yes! VoiceLite works in any Windows application with a text field. I personally use it in [specific example]. Give it a try and let me know how it works for you!
```

### "What about Linux/Mac?"
```
Windows-only for now (uses WPF + Win32 APIs). Mac/Linux versions are on the roadmap! Star the GitHub repo to follow progress.
```

---

## Success Criteria

### Day 1 Goals
- [ ] ProductHunt: Top 5 of the day (200+ upvotes)
- [ ] HackerNews: Front page (100+ points)
- [ ] Reddit: 500+ combined upvotes
- [ ] Website: 1,000+ visitors
- [ ] Downloads: 500+
- [ ] Signups: 50+
- [ ] Purchases: 5+ ($200 revenue)

### Week 1 Goals
- [ ] 5,000 downloads
- [ ] 200 signups
- [ ] 20 purchases ($800 revenue)
- [ ] 500 GitHub stars
- [ ] ProductHunt badge on website

### Month 1 Goals
- [ ] $1,000 MRR (50 paid users)
- [ ] 1,000 GitHub stars
- [ ] 10,000 downloads
- [ ] 5+ testimonials
- [ ] Featured in newsletter/blog

---

## Emergency Contacts

### If Something Breaks
- **Vercel down**: Check https://vercel.com/status
- **Stripe issues**: https://status.stripe.com
- **Resend email failing**: Check https://resend.com/status

### Support Channels
- **ProductHunt DMs**: For urgent PH questions
- **Twitter DMs**: For press/partnership inquiries
- **Email**: support@voicelite.app
- **GitHub Issues**: For bug reports

---

## Post-Launch (Weeks 2-4)

### Week 2: SEO & Content
- [ ] Blog: "Best Offline Voice Typing Software 2025"
- [ ] Blog: "VoiceLite vs Dragon: Detailed Comparison"
- [ ] Submit to directories: AlternativeTo, Slant, G2

### Week 3: Conversion Optimization
- [ ] A/B test: $15 vs $20 quarterly
- [ ] A/B test: 7-day Pro trial
- [ ] Add social proof (testimonials)

### Week 4: Feature Development
- [ ] Ship most-requested feature
- [ ] Announce on ProductHunt (update)
- [ ] Cross-post to HN/Reddit

---

## Final Pre-Launch Checklist ⚡

**Do these in order, right before launch:**

1. [ ] Stripe products created and tested ✅
2. [ ] Website deployed and working ✅
3. [ ] All launch materials ready (copy, screenshots, video) ✅
4. [ ] Response templates prepared ✅
5. [ ] Support channels set up ✅
6. [ ] GitHub repo up-to-date ✅
7. [ ] Got 6+ hours to dedicate to launch day ✅
8. [ ] Feeling confident and excited ✅

---

**You've got this! The product is ready. The infrastructure is ready. Now it's time to tell the world. 🚀**

**Launch when ready. Good luck!**

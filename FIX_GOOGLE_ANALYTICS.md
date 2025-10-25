# Fix Google Analytics Setup

## Problem

Google Analytics says it **"cannot access the website"** and is not collecting data.

**Root Cause:** The measurement ID in your code (`G-03SN26ZD3Q`) might not match your actual GA4 property, or the data stream isn't configured correctly.

---

## Solution: Complete GA4 Setup (10 minutes)

### Step 1: Get the Correct Measurement ID

**In your GA dashboard (screenshot you showed):**

1. ✅ You're in the right place: "Set up a Google tag"
2. Click **"Install manually"** (already selected)
3. Look at the code shown - it should display your measurement ID like `G-XXXXXXXXXX`
4. The ID shown in the screenshot appears to be `G-M0PY7YHYB` (different from your current `G-03SN26ZD3Q`)

**Or find it this way:**

1. In GA dashboard, click **Admin** (bottom left gear icon)
2. Under **Property** column, click **Data Streams**
3. Click on **"VoiceLite - Web (MVP)"** (the one showing "https://voicelite.app")
4. Copy the **Measurement ID** (top right, format: `G-XXXXXXXXXX`)

### Step 2: Update Your Environment Variables

```bash
cd voicelite-web

# Update local env file
# Edit .env.local and change:
NEXT_PUBLIC_GA_MEASUREMENT_ID="G-XXXXXXXXXX"  # Use the ID from Step 1

# Update Vercel production
vercel env rm NEXT_PUBLIC_GA_MEASUREMENT_ID production
vercel env add NEXT_PUBLIC_GA_MEASUREMENT_ID production
# When prompted, paste the new measurement ID
```

### Step 3: Redeploy to Production

```bash
# From voicelite-web/ directory
vercel deploy --prod
```

Wait ~2 minutes for deployment to complete.

### Step 4: Verify It Works

**Test 1: Check the Script**
```bash
# Should show YOUR measurement ID from Step 1
curl -s https://voicelite.app | grep -i "gtag/js?id="
```

**Test 2: Real-Time Test**
1. Go to GA dashboard
2. Click **Reports** → **Realtime** (left sidebar)
3. Open https://voicelite.app in new tab
4. Within 30 seconds, you should see **1 active user** in GA dashboard

**Test 3: Browser DevTools**
1. Visit https://voicelite.app
2. Press **F12** → **Console** tab
3. Type: `dataLayer`
4. Should show: `Array [ {…}, {…} ]` with GA config

---

## Alternative: Use Plausible Instead (Simpler Setup)

If GA continues to have issues, **Plausible is a better choice for lean startup**:

### Why Plausible is Better for You:

**Pros:**
- ✅ **5-minute setup** (vs GA's complicated property/stream setup)
- ✅ **Privacy-first** (aligns with VoiceLite's "100% offline" brand)
- ✅ **Simple dashboard** (one page, all data visible)
- ✅ **No cookie banner needed** (GDPR-compliant)
- ✅ **Lightweight** (~1KB script vs GA's 45KB)
- ✅ **Just works** (no "cannot access website" errors)

**Cons:**
- ❌ Costs $9/month
- ❌ Less detailed than GA (no demographics, less filtering)

### Plausible Setup (5 minutes):

**Option A: Cloud Hosted ($9/month)**

1. Go to https://plausible.io/register
2. Create account (14-day free trial, no credit card)
3. Add website: `voicelite.app`
4. Copy the script snippet shown
5. Update your code:

```bash
cd voicelite-web
npm install next-plausible
```

```typescript
// app/layout.tsx
import PlausibleProvider from 'next-plausible'

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning>
      <head>
        <PlausibleProvider domain="voicelite.app" />
      </head>
      <body>{children}</body>
    </html>
  )
}
```

6. Deploy: `vercel deploy --prod`
7. Visit voicelite.app → Check Plausible dashboard → See real-time visitor

**Option B: Self-Hosted (FREE)**

Requires Docker + server, not recommended for launch day.

---

## My Recommendation

### **For Launch Tomorrow:**

**Choose ONE:**

### Option 1: Fix GA4 (10 min effort, FREE forever)
**Choose this if:**
- ✅ You want detailed analytics (demographics, conversions, funnels)
- ✅ FREE is important
- ✅ You're okay with GA's complexity
- ✅ You can spend 10 min fixing it now

**Steps:**
1. Get correct measurement ID from GA dashboard
2. Update Vercel env vars
3. Redeploy
4. Verify in Realtime

---

### Option 2: Switch to Plausible (5 min effort, $9/mo)
**Choose this if:**
- ✅ You want analytics that "just works"
- ✅ Privacy is a core brand value
- ✅ Simple dashboard is preferred
- ✅ $9/month is acceptable (14-day free trial)

**Steps:**
1. Sign up at plausible.io
2. Add website domain
3. Install `next-plausible` package
4. Update layout.tsx
5. Deploy

---

### Option 3: Launch Without Analytics (0 min, FREE)
**Choose this if:**
- ✅ You want to launch RIGHT NOW
- ✅ Manual tracking is acceptable for Day 1
- ✅ You'll fix analytics on Day 2

**What you'll track manually:**
- Downloads: GitHub Releases page
- Revenue: Stripe Dashboard
- Traffic: Can't track (but not critical for Day 1)

**Add analytics on Day 2** when you have time.

---

## Quick Decision Matrix

| Criteria | Fix GA4 | Switch to Plausible | Launch Without |
|----------|---------|---------------------|----------------|
| **Setup Time** | 10 min | 5 min | 0 min |
| **Monthly Cost** | FREE | $9 | FREE |
| **Complexity** | Medium | Low | N/A |
| **Data Quality** | Excellent | Good | Manual only |
| **Privacy-Focused** | No | Yes | Yes |
| **Risk** | Low | None | Miss Day 1 data |

---

## What I'd Do If I Were You

**Launch tomorrow morning?** → **Option 3** (launch without analytics, fix on Day 2)

**Launch tomorrow afternoon?** → **Option 2** (switch to Plausible, 5-min setup)

**Launch next week?** → **Option 1** (fix GA4 properly)

---

## Step-by-Step: Fix GA4 Right Now

If you choose to fix GA4, here's exactly what to do:

### 1. Get Your Measurement ID (2 min)

In the screenshot you showed:
- The code snippet shows a measurement ID
- Look for the line: `gtag('config', 'G-XXXXXXXXXX');`
- Copy that `G-XXXXXXXXXX` ID

Or:
1. GA dashboard → **Admin** (gear icon)
2. **Property** column → **Data Streams**
3. Click **"VoiceLite - Web (MVP)"**
4. Copy **Measurement ID** (top right)

### 2. Update Vercel (3 min)

```bash
cd voicelite-web

# Remove old ID
vercel env rm NEXT_PUBLIC_GA_MEASUREMENT_ID production

# Add new ID
vercel env add NEXT_PUBLIC_GA_MEASUREMENT_ID production
# Paste: G-XXXXXXXXXX (from Step 1)

# Also update .env.local for local testing
echo 'NEXT_PUBLIC_GA_MEASUREMENT_ID="G-XXXXXXXXXX"' >> .env.local
```

### 3. Redeploy (2 min)

```bash
vercel deploy --prod
```

### 4. Test (3 min)

**Browser test:**
1. Clear browser cache (Ctrl+Shift+Del)
2. Visit https://voicelite.app
3. Open DevTools (F12) → Network tab
4. Filter: `gtag`
5. Should see request to `googletagmanager.com/gtag/js?id=G-XXXXXXXXXX`

**GA dashboard test:**
1. GA → Reports → Realtime
2. Keep voicelite.app open in another tab
3. Should see "1" under active users
4. May take 30-60 seconds to appear

**If still not working:**
- Check GA property settings → Data collection → "Data collection for website" is ON
- Check Data Stream → "Enhanced measurement" is ON
- Wait 24 hours (GA can be slow to start)

---

## Alternative: Simple Custom Analytics

If both GA and Plausible fail, create your own simple tracker:

```typescript
// lib/simple-analytics.ts
export async function trackPageView(page: string) {
  if (typeof window === 'undefined') return;

  await fetch('/api/analytics', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      page,
      referrer: document.referrer,
      timestamp: new Date().toISOString()
    })
  });
}

// app/api/analytics/route.ts
import { NextRequest, NextResponse } from 'next/server';

export async function POST(request: NextRequest) {
  const data = await request.json();

  // Log to console (or save to database)
  console.log('Page view:', data);

  return NextResponse.json({ success: true });
}

// Use in app/layout.tsx
useEffect(() => {
  trackPageView(window.location.pathname);
}, []);
```

This gives you basic tracking without third-party services.

---

## Final Recommendation

**Given your timeline (launching in 2 days):**

### **Best Option: Plausible Analytics**

**Why:**
1. ✅ **5-minute setup** (less risk than fixing GA)
2. ✅ **14-day free trial** (no credit card, no risk)
3. ✅ **Privacy-first** (aligns with VoiceLite brand)
4. ✅ **Just works** (no configuration headaches)
5. ✅ **Simple dashboard** (one page, all data)

**Setup now:**

```bash
cd voicelite-web
npm install next-plausible

# Sign up at plausible.io (30 seconds)
# Add domain: voicelite.app
# Get your Plausible script

# Update app/layout.tsx - remove GA code, add:
import PlausibleProvider from 'next-plausible'

<PlausibleProvider domain="voicelite.app" />

# Deploy
vercel deploy --prod

# Test: Visit voicelite.app → Check Plausible dashboard
```

**After 14-day trial:**
- If you like it: Pay $9/month
- If too expensive: Switch back to GA (you have 2 weeks to fix it)
- If need more features: Try PostHog (free tier)

---

## What You Need for Launch

**Minimum viable analytics:**
- ✅ **Traffic count** (how many visitors)
- ✅ **Traffic sources** (Reddit, HN, Twitter, direct)
- ✅ **Page views** (which pages popular)

**You DON'T need for Day 1:**
- ❌ Demographics (age, gender)
- ❌ Advanced funnels
- ❌ Session replay
- ❌ Custom events

**Plausible gives you everything you need, nothing you don't.**

---

## Action Plan (Choose One)

### Plan A: Launch with Plausible (Recommended)
- [ ] Sign up: https://plausible.io/register
- [ ] Add domain: voicelite.app
- [ ] Install: `npm install next-plausible`
- [ ] Update layout.tsx with Plausible code
- [ ] Deploy: `vercel deploy --prod`
- [ ] Test: Visit site → Check Plausible dashboard
- **Time:** 5-10 minutes
- **Cost:** FREE for 14 days

### Plan B: Fix GA4
- [ ] Get measurement ID from GA dashboard
- [ ] Update Vercel env vars
- [ ] Redeploy
- [ ] Verify in Realtime
- **Time:** 10-15 minutes
- **Cost:** FREE forever

### Plan C: Launch Without Analytics (Fix Day 2)
- [ ] Skip analytics setup
- [ ] Track manually (GitHub downloads, Stripe revenue)
- [ ] Fix on Day 2 after launch
- **Time:** 0 minutes
- **Cost:** FREE

---

**What do you want to do?** I can help with whichever option you choose.

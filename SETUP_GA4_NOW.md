# Fix Google Analytics 4 - Step by Step

**Goal:** Get GA4 working in the next 10 minutes

---

## Step 1: Get Your Correct Measurement ID (2 min)

### Method A: From the Screenshot You Showed

In your GA dashboard screenshot, I can see you're in **"Set up a Google tag"**.

Look at the code snippet displayed on the right side. Find this line:
```javascript
gtag('config', 'G-XXXXXXXXXX');
```

**Copy that `G-XXXXXXXXXX` value** - that's your correct measurement ID.

---

### Method B: From Data Streams

If you can't see it clearly in the setup screen:

1. In GA dashboard, click **Admin** (gear icon, bottom left)
2. Make sure you're in the right account/property:
   - **Property:** Should say something like "VoiceLite Website" or similar
3. Under **Property** column, click **Data Streams**
4. Click on **"VoiceLite - Web (MVP)"** (the one showing `https://voicelite.app`)
5. You'll see **Measurement ID** in the top right corner
6. Copy it (format: `G-XXXXXXXXXX`)

**Important:** Make sure it's a **G-** ID (GA4), not a **UA-** ID (old Universal Analytics)

---

## Step 2: Update Your Environment Variables (3 min)

Open terminal in your project root:

```bash
cd voicelite-web

# Update local environment file
# Open .env.local in your editor and change line 10:
```

**Edit `voicelite-web/.env.local`:**

Change:
```env
NEXT_PUBLIC_GA_MEASUREMENT_ID="G-03SN26ZD3Q"
```

To:
```env
NEXT_PUBLIC_GA_MEASUREMENT_ID="G-XXXXXXXXXX"  # Your new ID from Step 1
```

Save the file.

---

## Step 3: Update Vercel Production (2 min)

```bash
# Still in voicelite-web/ directory

# Remove the old measurement ID from production
vercel env rm NEXT_PUBLIC_GA_MEASUREMENT_ID production

# When prompted:
# "Remove NEXT_PUBLIC_GA_MEASUREMENT_ID from Production?" → Type: y

# Add the new measurement ID
vercel env add NEXT_PUBLIC_GA_MEASUREMENT_ID production

# When prompted:
# "What's the value of NEXT_PUBLIC_GA_MEASUREMENT_ID?"
# Paste: G-XXXXXXXXXX (your new ID from Step 1)
# Press Enter
```

**Expected output:**
```
✅ Added Environment Variable NEXT_PUBLIC_GA_MEASUREMENT_ID to Project voicelite-web [Production]
```

---

## Step 4: Redeploy to Production (2 min)

```bash
# From voicelite-web/ directory
vercel deploy --prod
```

**Wait for deployment to complete** (~1-2 minutes)

You'll see:
```
✅ Deployment ready [2m]
🔍 Inspect: https://vercel.com/...
✅ Production: https://voicelite.app
```

---

## Step 5: Verify It's Working (3 min)

### Test 1: Check Script in Source Code

```bash
# Run this command:
curl -s https://voicelite.app | grep -o 'gtag/js?id=[^"]*'
```

**Expected output:**
```
gtag/js?id=G-XXXXXXXXXX
```

**Should match** the measurement ID from Step 1.

If it still shows `G-03SN26ZD3Q`, wait 1 minute and try again (Vercel cache).

---

### Test 2: Real-Time Dashboard Test

**This is the most important test!**

1. Open GA dashboard: https://analytics.google.com
2. Click **Reports** in left sidebar
3. Click **Realtime** (should be first option under Reports)
4. You should see a screen showing "Users in the last 30 minutes"

**Now in a separate browser tab/window:**
5. Visit https://voicelite.app
6. Keep the page open for 30 seconds
7. Click around (scroll, click links)

**Back in GA Realtime:**
8. Within 30-60 seconds, you should see:
   - **"1"** under "Users by page title and screen name"
   - Your page view appear in the activity feed
   - Location showing where you are

**If you see this** ✅ **GA is working!**

---

### Test 3: Browser Console Test

1. Visit https://voicelite.app
2. Press **F12** (open DevTools)
3. Click **Console** tab
4. Type: `dataLayer`
5. Press Enter

**Expected output:**
```javascript
Array(2) [ {…}, {…} ]
```

Click to expand it - you should see:
```javascript
[
  { 0: "js", 1: Date },
  { 0: "config", 1: "G-XXXXXXXXXX" }  // Your measurement ID
]
```

---

## Step 6: Enable Enhanced Measurement (Optional but Recommended)

While in GA dashboard:

1. Go to **Admin** (gear icon)
2. **Data Streams** → Click your stream
3. Scroll down to **Enhanced measurement**
4. Make sure it's **ON** (toggle should be blue)
5. This tracks:
   - Page views ✅
   - Scrolls
   - Outbound clicks
   - Site search
   - File downloads

Click **Save** if you changed anything.

---

## Troubleshooting

### Issue: "Still seeing old measurement ID in source"

**Solution:** Clear Vercel cache
```bash
cd voicelite-web
vercel deploy --prod --force
```

---

### Issue: "No users showing in Realtime after 2 minutes"

**Check these:**

1. **Is the tag loading?**
   ```bash
   curl -s https://voicelite.app | grep googletagmanager
   ```
   Should see script loading.

2. **Is browser blocking it?**
   - Disable ad blockers (uBlock Origin, AdBlock)
   - Try in Incognito mode
   - Try different browser

3. **Is Data Collection enabled?**
   - GA Admin → Property Settings → Data Settings → Data Collection
   - "Google signals data collection" should be ON

4. **Wait 24 hours**
   Sometimes GA takes time to start. If script is loading correctly, data will come.

---

### Issue: "Vercel env command not found"

**Install Vercel CLI:**
```bash
npm install -g vercel
vercel login
```

Then try Step 3 again.

---

### Issue: "Wrong property/measurement ID"

**Make sure you're in the right property:**

1. GA dashboard → Click property selector (top left, next to "VoiceLite")
2. Make sure it says **GA4** property (not Universal Analytics)
3. Look for "Data streams" in Admin settings (GA4 only feature)
4. If you see "Tracking Info" instead → You're in old UA property (wrong one)

---

## What Success Looks Like

After completing all steps:

✅ **Source code check:**
```bash
curl -s https://voicelite.app | grep -o 'gtag.*config.*G-[A-Z0-9]*'
```
Shows: `gtag('config', 'G-XXXXXXXXXX');` with YOUR new ID

✅ **Realtime check:**
- GA Realtime dashboard shows active users when you visit site

✅ **Console check:**
- `dataLayer` shows your measurement ID

✅ **No errors:**
- No red errors in browser console related to gtag/analytics

---

## After It's Working

### Track These Events on Launch Day

GA4 automatically tracks:
- ✅ Page views
- ✅ Sessions
- ✅ User locations
- ✅ Traffic sources

**View your data:**
- **Realtime:** Reports → Realtime (for launch day monitoring)
- **Traffic sources:** Reports → Acquisition → Traffic acquisition
- **Pages:** Reports → Engagement → Pages and screens
- **Conversions:** Set up later (checkout clicks, downloads)

---

## Timeline

**If everything goes smoothly:**
- Step 1: 2 min (get measurement ID)
- Step 2: 1 min (update .env.local)
- Step 3: 2 min (update Vercel)
- Step 4: 2 min (deploy)
- Step 5: 3 min (verify)

**Total: ~10 minutes**

**If there are issues:**
- Allow 30 minutes for troubleshooting
- Check each step carefully
- Ask for help if stuck

---

## Quick Reference Commands

```bash
# All commands in one place:

# 1. Update local env
cd voicelite-web
# Edit .env.local, change NEXT_PUBLIC_GA_MEASUREMENT_ID

# 2. Update Vercel
vercel env rm NEXT_PUBLIC_GA_MEASUREMENT_ID production
vercel env add NEXT_PUBLIC_GA_MEASUREMENT_ID production
# Paste your new measurement ID when prompted

# 3. Deploy
vercel deploy --prod

# 4. Verify
curl -s https://voicelite.app | grep -o 'gtag/js?id=[^"]*'

# 5. Test in browser
# Visit voicelite.app → Check GA Realtime
```

---

## Need Help?

**If stuck, check:**
1. Is measurement ID format correct? (`G-XXXXXXXXXX`, starts with G-)
2. Did you update BOTH .env.local AND Vercel production?
3. Did deployment complete successfully?
4. Did you wait at least 30 seconds after visiting site?
5. Is ad blocker disabled?

**Common mistakes:**
- Using old UA measurement ID (UA-XXXXXX) instead of GA4 (G-XXXXXX)
- Only updating .env.local, forgetting Vercel production
- Not waiting for deployment to finish
- Ad blocker preventing gtag script

---

**Let's do this! Start with Step 1 and let me know what measurement ID you find.** 🚀
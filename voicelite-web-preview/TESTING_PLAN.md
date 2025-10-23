# VoiceLite Production Testing Plan

**Deployment Status:** ✅ Live at https://voicelite.app
**Last Updated:** 2025-09-30
**Version:** 1.0.8

---

## Pre-Testing Checklist

✅ Desktop client built: `VoiceLite-Setup-1.0.8.exe` (129MB)
✅ Web app deployed to Vercel production
✅ All 15 environment variables configured
✅ Database schema migrated (10 tables, 15 indexes)
✅ Stripe webhook configured: `https://voicelite.app/api/webhook`
✅ CSRF protection updated for correct domain

---

## Test 1: Homepage & Static Content (5 min)

**URL:** https://voicelite.app

### Steps:
1. Visit homepage in browser
2. Verify all sections load correctly:
   - Hero section with "Turn Your Voice Into Text Instantly"
   - Download button (GitHub link)
   - Authentication card with email input
   - "Why VoiceLite?" features section
   - Pricing plans (Quarterly $20/3mo, Lifetime $99)
   - Feature descriptions
3. Check for any console errors (F12 → Console tab)
4. Verify SSL certificate is valid (padlock icon in address bar)

### Expected Results:
- ✅ Page loads without errors
- ✅ All images and icons render
- ✅ Download link points to: `https://github.com/mikha08-rgb/VoiceLite/releases/download/v1.0.8/VoiceLite-Setup-1.0.8.exe`
- ✅ SSL certificate valid for `voicelite.app`

---

## Test 2: Authentication Flow - Magic Link (15 min)

**Endpoint:** `/api/auth/request`

### Steps:
1. Enter your email address in the authentication card
2. Click "Email me a magic link"
3. Check your inbox for email from "VoiceLite <noreply@voicelite.com>"
4. Verify email contents:
   - Subject line appropriate
   - Magic link present
   - 8-digit OTP code present
5. Click the magic link in email OR enter OTP on website
6. Verify successful login

### Expected Results:
- ✅ Email arrives within 30 seconds (via Resend)
- ✅ Magic link format: `https://voicelite.app/api/auth/callback?token=...`
- ✅ OTP is 8 numeric digits
- ✅ After clicking link or entering OTP, you see "Signed in as [email]"
- ✅ "Sign out" button appears
- ✅ Message: "No licenses linked yet. Choose a plan below."

### Troubleshooting:
- **No email received?** Check spam folder
- **Email fails to send?** Check Resend dashboard: https://resend.com/emails
- **Magic link doesn't work?** Try OTP code instead
- **OTP rejected?** Check if code expired (10 min timeout)

---

## Test 3: Database Session Verification (5 min)

**Tool:** Supabase SQL Editor

### Steps:
1. Go to Supabase project: https://supabase.com/dashboard/project/kkjfmnwjchlugzxlqipw
2. Navigate to SQL Editor
3. Run query:
```sql
SELECT * FROM "User" ORDER BY "createdAt" DESC LIMIT 5;
SELECT * FROM "Session" ORDER BY "createdAt" DESC LIMIT 5;
```
4. Verify your user and session exist

### Expected Results:
- ✅ User record with your email exists
- ✅ Session record with valid `expiresAt` timestamp
- ✅ `revokedAt` is NULL (session active)

---

## Test 4: Stripe Checkout - Quarterly Plan (15 min)

**Endpoint:** `/api/checkout`
**Test Mode:** Using Stripe test keys

### Steps:
1. **While logged in**, scroll to pricing section
2. Click "Upgrade now" on **Quarterly** plan ($20/3 months)
3. Verify redirect to Stripe Checkout
4. Fill in test card details:
   - **Card:** `4242 4242 4242 4242`
   - **Expiry:** Any future date (e.g., `12/25`)
   - **CVC:** Any 3 digits (e.g., `123`)
   - **Name:** Your name
   - **Email:** Should be pre-filled with your account email
5. Click "Subscribe"
6. Wait for redirect back to voicelite.app

### Expected Results:
- ✅ Checkout session opens with correct plan details
- ✅ Price shows: "$20.00 every 3 months"
- ✅ Email pre-filled from your account
- ✅ Payment succeeds with test card
- ✅ Redirect to success URL (TBD - check what URL it goes to)

### Troubleshooting:
- **Checkout fails?** Check Stripe Dashboard → Payments: https://dashboard.stripe.com/test/payments
- **Wrong price?** Verify `STRIPE_QUARTERLY_PRICE_ID` in Vercel env vars
- **Email not pre-filled?** Check that you're logged in before clicking upgrade

---

## Test 5: Stripe Webhook & License Generation (10 min)

**Endpoint:** `/api/webhook`

### Steps:
1. After completing checkout, wait 10 seconds
2. Check Stripe Dashboard → Developers → Webhooks: https://dashboard.stripe.com/test/webhooks
3. Find webhook event for `checkout.session.completed`
4. Verify webhook delivery:
   - Status: "Succeeded" (green checkmark)
   - Response code: 200
5. Go to Supabase SQL Editor and run:
```sql
SELECT
  l.id,
  l."licenseKey",
  l.type,
  l.status,
  l."stripeSubscriptionId",
  l."expiresAt",
  u.email
FROM "License" l
JOIN "User" u ON l."userId" = u.id
ORDER BY l."createdAt" DESC
LIMIT 5;
```
6. Verify license was created for your purchase

### Expected Results:
- ✅ Webhook delivered successfully (200 response)
- ✅ License record created in database
- ✅ `licenseKey` is a readable string (format: `VL-XXXX-XXXX-XXXX-XXXX`)
- ✅ `type` = "SUBSCRIPTION"
- ✅ `status` = "ACTIVE"
- ✅ `stripeSubscriptionId` matches subscription ID from Stripe
- ✅ `expiresAt` is ~3 months in future

### Troubleshooting:
- **Webhook failed?** Check Vercel logs: `npx vercel logs --prod`
- **No license created?** Check webhook response body in Stripe Dashboard
- **Wrong license type?** Verify plan metadata in webhook payload

---

## Test 6: License Display on Website (5 min)

**Page:** https://voicelite.app

### Steps:
1. Refresh the homepage while logged in
2. Check the Account card
3. Verify license is displayed

### Expected Results:
- ✅ Account card shows: "Signed in as [email]"
- ✅ License listed with format: "License VL-XXXX-XXXX-XXXX-XXXX — subscription (active)"
- ✅ License key is copyable (can select and copy)

---

## Test 7: Desktop Client Installation (10 min)

**Installer:** `VoiceLite-Setup-1.0.8.exe` (129MB)

### Steps:
1. Download installer from GitHub release OR from website download button
2. Run installer (may need to allow in Windows Defender)
3. Follow installation wizard:
   - Accept EULA
   - Choose install location (default: `C:\Program Files\VoiceLite\`)
   - Create desktop shortcut (optional)
4. Launch VoiceLite after installation
5. Verify startup diagnostics complete successfully

### Expected Results:
- ✅ Installer runs without errors
- ✅ App launches and shows system tray icon
- ✅ No "missing DLL" errors (if error: install VC++ Runtime from https://aka.ms/vs/17/release/vc_redist.x64.exe)
- ✅ Startup diagnostics pass:
  - ✅ Whisper.exe found
  - ✅ Tiny model found
  - ✅ Audio devices detected
  - ✅ Hotkey registered successfully

### Troubleshooting:
- **VCRUNTIME140_1.dll missing?** Install VC++ Runtime: https://aka.ms/vs/17/release/vc_redist.x64.exe
- **App won't start?** Check logs: `%APPDATA%\VoiceLite\logs\voicelite.log`
- **Hotkey not working?** Try different key in Settings

---

## Test 8: Desktop License Activation (15 min)

**Feature:** License Management

### Steps:
1. In VoiceLite desktop app, click "Settings" (system tray → Settings OR main window)
2. Navigate to "License" tab
3. Copy license key from website (from Test 6)
4. Paste into "License Key" field
5. Click "Activate"
6. Wait for activation to complete

### Expected Results:
- ✅ License validates successfully
- ✅ Shows: "License Type: Subscription"
- ✅ Shows: "Status: Active"
- ✅ Shows: "Expires: [date 3 months from now]"
- ✅ Machine ID generated and sent to server
- ✅ Activation saved in database

### Database Verification:
Run in Supabase SQL Editor:
```sql
SELECT
  la.id,
  la."machineId",
  la."machineLabel",
  la.status,
  la."activatedAt",
  l."licenseKey"
FROM "LicenseActivation" la
JOIN "License" l ON la."licenseId" = l.id
ORDER BY la."activatedAt" DESC
LIMIT 5;
```

### Expected Database Results:
- ✅ LicenseActivation record created
- ✅ `machineId` is a unique hash
- ✅ `machineLabel` shows Windows username/hostname
- ✅ `status` = "ACTIVE"

### Troubleshooting:
- **"Invalid license key"?** Check that key was copied correctly (no spaces)
- **"License already activated"?** Desktop client may already be activated from previous test
- **"Server connection failed"?** Check internet connection, verify API endpoint responding
- **Activation limit reached?** Check license allows 3 devices (default limit)

---

## Test 9: Voice Transcription (5 min)

**Feature:** Core voice typing functionality

### Steps:
1. Open any text editor (Notepad, VS Code, Word, etc.)
2. Click in text field
3. Hold Left Alt key (default hotkey)
4. Speak clearly: "This is a test of VoiceLite voice transcription"
5. Release Left Alt
6. Wait for transcription to appear

### Expected Results:
- ✅ Visual feedback while recording (border color change OR indicator)
- ✅ Transcription appears in ~200ms after releasing key
- ✅ Text injected at cursor position
- ✅ Accuracy matches spoken words (95%+ for clear speech)
- ✅ No crashes or errors

### Troubleshooting:
- **No transcription?** Check microphone is working (Windows Sound Settings)
- **Wrong microphone?** Change device in VoiceLite Settings → Audio
- **Poor accuracy?** Try downloading better model (Settings → Models → Medium)
- **Slow transcription?** Check if using tiny model (default) vs larger models

---

## Test 10: Stripe Checkout - Lifetime Plan (Optional, 15 min)

**Note:** Only test if you want to verify lifetime purchase flow

### Steps:
Same as Test 4, but click "Upgrade now" on **Lifetime** plan ($99)

### Expected Results:
- ✅ Checkout shows: "$99.00" (one-time payment, not subscription)
- ✅ Payment mode: "payment" (not "subscription")
- ✅ License created with `type` = "LIFETIME"
- ✅ `expiresAt` is NULL (never expires)
- ✅ `stripePaymentIntentId` populated (not `stripeSubscriptionId`)

---

## Test 11: End-to-End User Journey (30 min)

**Complete flow from discovery to activation**

### Steps:
1. Sign up with NEW email address
2. Complete authentication (magic link OR OTP)
3. Purchase Quarterly plan with test card
4. Verify license received via email (if email sending configured)
5. Copy license key
6. Download and install desktop client
7. Activate license in desktop app
8. Test voice transcription

### Expected Results:
- ✅ Entire flow completes without manual intervention
- ✅ All steps work seamlessly
- ✅ User receives license within 1 minute of purchase
- ✅ Desktop activation works on first try
- ✅ Voice typing works immediately after activation

---

## Test 12: Error Handling & Edge Cases (15 min)

### Test Cases:

#### 12.1: Invalid License Key
- Enter random key: `VL-FAKE-FAKE-FAKE-FAKE`
- Expected: "Invalid license key" error message

#### 12.2: Expired Magic Link
- Request magic link, wait 15 minutes, then try to use it
- Expected: "Link expired" error, prompt to request new link

#### 12.3: Rate Limiting
- Request 10 magic links in rapid succession (< 10 seconds apart)
- Expected: "Too many requests" error after 3-5 attempts

#### 12.4: Duplicate License Activation
- Activate same license key twice on same machine
- Expected: Success (should recognize same machine)

#### 12.5: License Activation Limit
- Activate license on 4 different machines (requires 4 test VMs)
- Expected: 4th activation fails with "Maximum devices reached"

#### 12.6: Invalid Email
- Try authentication with `not-an-email`
- Expected: Client-side validation prevents submission

#### 12.7: Checkout Without Login
- Log out, try to click "Upgrade now"
- Expected: "Please sign in before upgrading" error

---

## Production Readiness Checklist

Before switching Stripe to LIVE mode:

- [ ] All tests above pass successfully
- [ ] No errors in Vercel logs for 24 hours
- [ ] Stripe webhook delivery success rate > 99%
- [ ] Email delivery rate > 95% (check Resend dashboard)
- [ ] Database backups configured (Supabase automatic backups enabled)
- [ ] SSL certificate valid and auto-renewing
- [ ] CSRF protection tested and working
- [ ] Rate limiting prevents abuse
- [ ] Error handling covers all edge cases
- [ ] Desktop client code signing certificate obtained (optional but recommended)

---

## Switching to Stripe LIVE Mode

Once all tests pass, follow these steps:

1. **Get Stripe LIVE API keys:**
   - Go to https://dashboard.stripe.com/apikeys
   - Copy LIVE keys (they start with `sk_live_` and `pk_live_`)

2. **Update Vercel environment variables:**
```bash
cd voicelite-web
printf 'sk_live_YOUR_LIVE_KEY_HERE' | npx vercel env add STRIPE_SECRET_KEY production
printf 'pk_live_YOUR_LIVE_KEY_HERE' | npx vercel env add NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY production
```

3. **Create LIVE webhook endpoint:**
   - Go to https://dashboard.stripe.com/webhooks
   - Create new endpoint: `https://voicelite.app/api/webhook`
   - Select events:
     - `checkout.session.completed`
     - `customer.subscription.updated`
     - `customer.subscription.deleted`
     - `charge.refunded`
   - Copy webhook signing secret

4. **Update webhook secret:**
```bash
printf 'whsec_YOUR_LIVE_WEBHOOK_SECRET' | npx vercel env add STRIPE_WEBHOOK_SECRET production
```

5. **Redeploy:**
```bash
npx vercel --prod --yes
```

6. **Test with real card:**
   - Use real credit card (yours)
   - Complete full purchase flow
   - Verify license generation works
   - **IMPORTANT:** Immediately refund the test purchase in Stripe Dashboard

7. **Monitor for 24 hours:**
   - Watch Vercel logs for errors
   - Check Stripe webhook deliveries
   - Monitor Resend email delivery rate

---

## Support & Monitoring

### Logs & Debugging:
- **Vercel Logs:** `npx vercel logs --prod`
- **Desktop Logs:** `%APPDATA%\VoiceLite\logs\voicelite.log`
- **Stripe Dashboard:** https://dashboard.stripe.com
- **Supabase Dashboard:** https://supabase.com/dashboard
- **Resend Dashboard:** https://resend.com/emails

### Key Metrics to Monitor:
- **Uptime:** Vercel should be 99.9%+
- **Response Time:** API routes < 500ms average
- **Error Rate:** < 0.1% of requests
- **Webhook Success Rate:** > 99%
- **Email Delivery Rate:** > 95%
- **License Activation Success:** > 95%

---

## Next Steps After Testing

1. **Fix any issues found during testing**
2. **Document known limitations** (if any)
3. **Create support documentation** for common user issues
4. **Set up monitoring/alerting** (optional: Sentry, Vercel Analytics)
5. **Plan marketing/launch strategy**
6. **Consider code signing certificate** for Windows installer (reduces false positive virus warnings)
7. **Switch to Stripe LIVE mode** when ready for real customers

---

**Testing started:** _____________
**Testing completed:** _____________
**Issues found:** _____________
**Status:** ⬜ PASS | ⬜ FAIL | ⬜ BLOCKED

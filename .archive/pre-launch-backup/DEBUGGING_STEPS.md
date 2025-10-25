# Debugging License Email Issue

## Current Status

✅ **Payment received** with customer ID: `cus_TIZfFoZo4Epfrt`
✅ **Email present**: `mikhail.lev08@gmail.com`
✅ **Customer creation working**: `customer_creation: 'always'`
✅ **Email service working**: Manual test email sent (ID: `012f756a-a665-45e6-9723-be4ac705659f`)
❌ **License email not arriving** after payment

## Most Recent Payment
```
Event ID: evt_1SLyVoB71coZaXSZhqbap4SD
Session ID: cs_live_b1bOdFybpfzdrypeeg9dDTnZAgxpDIYvYHlSTNdUbWzYJ5OTS5KvrYPtze
Created: 2025-10-25T03:51:28.000Z
Email: mikhail.lev08@gmail.com
Customer ID: cus_TIZfFoZo4Epfrt ✅
Payment Intent: pi_3SLyVmB71coZaXSZ08PE4zp4
Payment Status: paid ✅
```

## Possible Issues

### 1. Webhook Not Being Delivered by Stripe
**Symptoms:** Stripe event fires but doesn't call voicelite.app/api/webhook

**How to Check:**
1. Go to https://dashboard.stripe.com/webhooks
2. Click on `https://voicelite.app/api/webhook`
3. Click "Events & logs" tab
4. Find event `evt_1SLyVoB71coZaXSZhqbap4SD`
5. Check:
   - Is there an entry for this event?
   - What's the response code? (200 = success, 500 = error)
   - What's the response body?

**If no entry:** Webhook endpoint is not configured correctly in Stripe
**If 500 error:** Webhook received but code is failing
**If 200 success:** Webhook succeeded - email should have been sent (check other issues)

### 2. Webhook Failing Silently
**Symptoms:** Webhook is called but fails before sending email

**How to Check:**
1. Check Vercel function logs:
   ```bash
   cd voicelite-web
   vercel logs --limit 100
   ```

2. Filter for webhook events:
   ```bash
   vercel logs --limit 100 | grep -i webhook
   ```

3. Look for these messages:
   - ✅ Good: `📧 Attempting to send license email to mikhail.lev08@gmail.com`
   - ✅ Good: `✅ License email sent successfully`
   - ❌ Bad: `❌ Failed to send license email`
   - ❌ Bad: `Missing customer email or ID`

### 3. Webhook Environment Variables Missing
**Symptoms:** Webhook endpoint can't access Resend API key

**How to Check:**
1. Go to https://vercel.com (your project)
2. Click "Settings" → "Environment Variables"
3. Verify these are set for **Production**:
   - `RESEND_API_KEY`
   - `RESEND_FROM_EMAIL`
   - `DATABASE_URL`
   - `DIRECT_DATABASE_URL`

### 4. Webhook Endpoint Not Deployed
**Symptoms:** Old code running without the fix

**Already Verified:** ✅ We confirmed `customer_creation: 'always'` is live

But let's verify webhook handler is also deployed:
```bash
curl https://voicelite.app/api/webhook -X POST \
  -H "Content-Type: application/json" \
  -d '{"test": true}'
```

Should return: `{"error":"Missing signature"}` (this means endpoint exists)

### 5. Database Connection Issues
**Symptoms:** Webhook can't create license record

**How to Check:**
Look at Vercel logs for:
- Database connection errors
- Prisma errors
- `Can't reach database server`

## Immediate Actions

### Step 1: Check if Test Email Arrived
Did you receive the test email with subject "TEST: Your VoiceLite Pro License Key"?

- **YES** → Email service works, problem is webhook delivery
- **NO** → Email service has issues (domain/DNS/API key)

### Step 2: Check Stripe Webhook Dashboard
1. https://dashboard.stripe.com/webhooks
2. Click endpoint
3. Find event `evt_1SLyVoB71coZaXSZhqbap4SD`
4. Screenshot the response

### Step 3: Check Vercel Logs
```bash
cd voicelite-web
vercel logs --limit 200 > webhook-logs.txt
```

Then search for:
- Event ID: `evt_1SLyVoB71coZaXSZhqbap4SD`
- Customer ID: `cus_TIZfFoZo4Epfrt`
- Email: `mikhail.lev08@gmail.com`

### Step 4: Manually Trigger Webhook (If Needed)
If Stripe didn't deliver the webhook, you can manually trigger it:

1. In Stripe Dashboard → Webhooks
2. Click your endpoint
3. Click "Send test webhook"
4. Select `checkout.session.completed`
5. Click "Send test event"

## Quick Fixes

### If Webhook Wasn't Delivered
**Problem:** Stripe isn't calling your webhook

**Fix:**
1. Check webhook URL is exactly: `https://voicelite.app/api/webhook` (no trailing slash)
2. Check webhook is enabled
3. Check events include `checkout.session.completed`
4. Resend the event from Stripe dashboard

### If Webhook Failed (500 Error)
**Problem:** Code error in webhook handler

**Fix:**
1. Check Vercel logs for exact error
2. Check environment variables are set
3. Check database is accessible
4. Redeploy if needed

### If Webhook Succeeded (200) But No Email
**Problem:** Email sending failed or was skipped

**Fix:**
1. Check Vercel logs for email sending errors
2. Verify Resend API key is valid
3. Check Resend domain is verified
4. Check email wasn't caught in spam

## Manual Workaround

If you need to send the license email immediately for this customer:

```bash
# Use the resend-email endpoint
curl -X POST https://voicelite.app/api/licenses/resend-email \
  -H "Content-Type: application/json" \
  -d '{"email":"mikhail.lev08@gmail.com"}'
```

This will look up the license and resend the email.

## What to Report Back

Please check and provide:
1. **Did test email arrive?** (subject: "TEST: Your VoiceLite Pro License Key")
2. **Stripe webhook log:** What does it show for event `evt_1SLyVoB71coZaXSZhqbap4SD`?
3. **Vercel logs:** Any errors for `/api/webhook`?

This will help me pinpoint exactly where the issue is!

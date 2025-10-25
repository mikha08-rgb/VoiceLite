# Webhook Email Issue Debug Guide

## Issue Summary
Test payments complete successfully, but license emails are not being sent.

## What We Know ✅
1. **Email sending works** - Test script successfully sent email via Resend
2. **Webhook endpoint exists** - `/api/webhook/route.ts` is deployed
3. **Webhook endpoint is accessible** - Returns 400 for missing signature (expected)
4. **Environment variables configured** - Resend API key, Stripe webhook secret all set

## Root Cause Analysis 🔍

The issue is likely one of the following:

### 1. **Stripe Webhook Not Configured** (MOST LIKELY)
Your Stripe webhook may not be configured to send events to your production URL.

**To Fix:**
1. Go to [Stripe Dashboard → Webhooks](https://dashboard.stripe.com/webhooks)
2. Check if you have a webhook endpoint configured
3. The endpoint URL should be: `https://voicelite.app/api/webhook`
4. Events to listen for:
   - `checkout.session.completed` ⭐ (CRITICAL - triggers email)
   - `customer.subscription.updated`
   - `customer.subscription.deleted`
   - `charge.refunded`

**Expected Configuration:**
```
Endpoint URL: https://voicelite.app/api/webhook
Events:
  ✓ checkout.session.completed
  ✓ customer.subscription.updated
  ✓ customer.subscription.deleted
  ✓ charge.refunded
Signing Secret: whsec_3OJ1MYaHYfAVHHG787ki3fWuxUHBReID
```

### 2. **Using Test Mode vs Live Mode Mismatch**
Your environment uses **LIVE** Stripe keys (`sk_live_...`, `pk_live_...`), but you might be testing with test mode checkout.

**To Fix:**
- Use LIVE mode in Stripe Dashboard when testing
- OR switch to test keys in `.env.local` if you want to use test mode

### 3. **Webhook Secret Mismatch**
The webhook secret in `.env.local` might not match the one in Stripe.

**To Fix:**
1. Go to Stripe Dashboard → Webhooks
2. Click on your webhook endpoint
3. Click "Reveal" on the signing secret
4. Compare with `STRIPE_WEBHOOK_SECRET` in `.env.local`
5. Update if different and redeploy

## Quick Test 🧪

### Test Email Sending (Already Passed ✅)
```bash
node test-email-send.js
```

### Test Webhook Manually
You can trigger a test webhook event from Stripe Dashboard:

1. Go to [Stripe Webhooks](https://dashboard.stripe.com/webhooks)
2. Click your webhook endpoint
3. Click "Send test webhook"
4. Select `checkout.session.completed`
5. Click "Send test event"
6. Check logs in Vercel or your email

## Deployment Checklist ✅

When you update `.env.local` or fix webhook configuration:

1. **Redeploy to Vercel:**
   ```bash
   cd voicelite-web
   vercel deploy --prod
   ```

2. **Set environment variables in Vercel:**
   ```bash
   vercel env pull
   # Or set via Vercel Dashboard
   ```

3. **Verify deployment:**
   ```bash
   curl https://voicelite.app/api/webhook -X POST -H "Content-Type: application/json" -d '{}' -v
   # Should return: {"error":"Missing signature"}
   ```

## Testing the Full Flow 🔄

1. Go to your live site: https://voicelite.app
2. Click "Get Pro" or equivalent
3. Complete a test payment (use test card `4242 4242 4242 4242` if in test mode)
4. Check:
   - Stripe Dashboard → Webhooks → Events (see if event was delivered)
   - Email inbox for license key
   - Vercel logs for webhook processing

## Expected Logs (When Working)

In Vercel logs, you should see:
```
📧 Attempting to send license email to [email] (License: [key])
✅ License email sent successfully to [email] (MessageID: [id])
```

If you see:
```
❌ Failed to send license email to [email]: [error]
```

Then the webhook IS working, but email sending failed (different issue).

## Next Steps 🎯

1. **Check Stripe Webhooks:** Go to dashboard and verify webhook is configured
2. **Test with Real Payment:** Use test mode checkout and verify webhook fires
3. **Check Vercel Logs:** `vercel logs --limit 100` after test payment
4. **If still failing:** Share Stripe webhook event logs and Vercel logs for debugging

---

**TLDR:** Most likely your Stripe webhook is not configured or pointing to wrong URL. Fix in Stripe Dashboard.

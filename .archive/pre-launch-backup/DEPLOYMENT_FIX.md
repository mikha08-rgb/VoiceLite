# License Email Fix - Deployment Instructions

## 🎯 The Fix

Added `customer_creation: 'always'` to the Stripe checkout session in [app/api/checkout/route.ts:46](voicelite-web/app/api/checkout/route.ts#L46).

**What was wrong:**
- Checkout didn't create a Stripe Customer record
- Webhook handler expected `session.customer` to exist
- When missing, webhook threw error and email was never sent

**What's fixed:**
- Checkout now creates customer record automatically
- Webhook receives customer ID
- Email sending works properly

## 📦 Deployment

### Option 1: Auto-deployment (Recommended)
Since you pushed to GitHub (`master` branch), Vercel should auto-deploy.

1. Check deployment status:
   - Go to https://vercel.com/mishas-projects-0509f3dc/voicelite
   - Look for deployment triggered by commit `bad4d98`
   - Wait for it to complete (~2-3 minutes)

### Option 2: Manual Deployment
If auto-deployment is disabled:

1. Go to https://vercel.com/mishas-projects-0509f3dc/voicelite/settings
2. Click "Deployments"
3. Click "Redeploy" on latest deployment
4. Select "Use existing Build Cache: No"

### Option 3: Vercel CLI (if settings fixed)
The Vercel CLI is looking for wrong path. Fix the root directory setting:

1. Go to https://vercel.com/mishas-projects-0509f3dc/voicelite/settings
2. Under "Root Directory", ensure it's set to `voicelite-web`
3. Then run: `vercel deploy --prod`

## ✅ Testing After Deployment

1. **Make a test payment:**
   - Go to https://voicelite.app
   - Click "Get Pro"
   - Use test card: `4242 4242 4242 4242`
   - Use any future expiry date
   - Use any CVC

2. **Check email:**
   - Should arrive within 30 seconds
   - Check spam folder if not in inbox

3. **Verify in Stripe:**
   - Go to [Stripe Events](https://dashboard.stripe.com/test/events)
   - Find latest `checkout.session.completed` event
   - Click "Webhook attempts"
   - Should show 200 response (not 500)

## 🔍 Debugging If Still Failing

If emails still don't arrive after deployment:

1. **Check Vercel deployment logs:**
   ```
   Visit: https://vercel.com/mishas-projects-0509f3dc/voicelite
   Click latest deployment
   Click "Functions" tab
   Find /api/webhook logs
   ```

2. **Look for these log messages:**
   - ✅ Good: `📧 Attempting to send license email`
   - ✅ Good: `✅ License email sent successfully`
   - ❌ Bad: `❌ Failed to send license email`
   - ❌ Bad: `Missing customer email or ID`

3. **Check Stripe webhook logs:**
   - Go to https://dashboard.stripe.com/webhooks
   - Click your webhook endpoint
   - Click "Events" tab
   - Click latest event
   - Should show 200 response with `{"received":true,"eventId":"evt_..."}`

## 📝 What Changed

**Commit:** `bad4d98`
**File:** `voicelite-web/app/api/checkout/route.ts`
**Change:**
```diff
  success_url: `${baseUrl}/checkout/success?session_id={CHECKOUT_SESSION_ID}`,
  cancel_url: `${baseUrl}/checkout/cancel`,
  allow_promotion_codes: true,
  billing_address_collection: 'auto',
+ customer_creation: 'always', // CRITICAL: Create customer so webhook can access customer ID
});
```

This ensures every checkout creates a Customer record in Stripe, which the webhook needs to send the license email.

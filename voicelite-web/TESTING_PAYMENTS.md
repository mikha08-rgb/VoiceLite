# Testing VoiceLite Payments (Stripe Test Mode)

**Status:** ✅ Already configured for test mode - no LLC or activated Stripe account required!

Your current `.env.local` uses Stripe test keys (`sk_test_...`), which means you can test the entire payment flow without moving real money.

## Quick Start (5 Minutes)

### 1. Start Development Server

```bash
cd voicelite-web
npm run dev
```

### 2. Navigate to Checkout

Open http://localhost:3000 and click "Download Free" or Pro upgrade button.

### 3. Test Payment with Magic Card

Use these test card details (Stripe test mode):

```
Card Number:    4242 4242 4242 4242
Expiration:     12/34 (any future date)
CVC:            123 (any 3 digits)
ZIP:            12345 (any valid ZIP)
```

### 4. Watch Console for License Key

Since `RESEND_API_KEY` is not configured, emails will be logged to the terminal console:

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📧 LICENSE EMAIL (Development Mode)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
To: your-email@example.com
Subject: Your VoiceLite license
Plan: Quarterly Subscription

🔑 LICENSE KEY: VL-XXXX-XXXX-XXXX-XXXX
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

Copy the license key from the terminal output!

---

## Test Card Scenarios

Stripe provides test cards for different scenarios:

| Card Number         | Scenario                      |
|---------------------|-------------------------------|
| `4242424242424242` | ✅ Successful payment          |
| `4000000000000002` | ❌ Card declined               |
| `4000002500003155` | ❌ Requires authentication     |
| `4000000000009995` | ❌ Insufficient funds          |

Full list: https://docs.stripe.com/testing#cards

---

## Testing Webhooks (Subscription Updates, Refunds)

Webhooks allow Stripe to notify your app about payment events (subscription renewed, payment failed, refund issued, etc.). To test webhooks locally:

### Option 1: Stripe CLI (Recommended)

1. **Install Stripe CLI:**
   ```bash
   # Windows (winget)
   winget install stripe.stripe-cli

   # Or download from https://docs.stripe.com/stripe-cli
   ```

2. **Login to Stripe:**
   ```bash
   stripe login
   ```
   This opens a browser to authenticate.

3. **Forward Webhooks to Local Server:**
   ```bash
   stripe listen --forward-to localhost:3000/api/webhook
   ```

   This will output a webhook signing secret like:
   ```
   > Ready! Your webhook signing secret is whsec_abc123...
   ```

4. **Update `.env.local`:**
   Copy the `whsec_...` secret and update:
   ```bash
   STRIPE_WEBHOOK_SECRET="whsec_abc123..."
   ```

5. **Restart Dev Server:**
   ```bash
   npm run dev
   ```

6. **Trigger Test Events:**
   In another terminal:
   ```bash
   # Simulate successful payment
   stripe trigger checkout.session.completed

   # Simulate subscription renewal
   stripe trigger customer.subscription.updated

   # Simulate refund
   stripe trigger charge.refunded
   ```

   Watch the webhook logs in your terminal!

### Option 2: Manual Testing (No CLI)

Without Stripe CLI, you can still test the checkout flow:

1. Complete checkout with test card `4242424242424242`
2. License will be created in database
3. License key logged to console (development mode)
4. Webhook events won't be received locally (but work in production)

---

## Testing Full End-to-End Flow

### Test Quarterly Subscription ($20/3 months)

1. **Checkout:**
   - Navigate to http://localhost:3000
   - Click "Upgrade to Pro"
   - Select "Quarterly" plan
   - Use test card `4242424242424242`
   - Click "Subscribe"

2. **Verify License Creation:**
   ```bash
   # Check database (Prisma Studio)
   npx prisma studio
   ```
   - Navigate to `License` table
   - Find your license with `type: SUBSCRIPTION`
   - Note the `licenseKey`

3. **Verify Email (Console):**
   Check terminal output for:
   ```
   🔑 LICENSE KEY: VL-XXXX-XXXX-XXXX-XXXX
   ```

4. **Test License Activation (Desktop App):**
   - Open VoiceLite desktop app
   - Go to Settings → License
   - Enter license key from console
   - Should activate successfully

### Test Lifetime License ($99 one-time)

Same steps as above, but select "Lifetime" plan during checkout.

---

## Testing Subscription Management

### Simulate Subscription Cancellation

With Stripe CLI running:

```bash
# Simulate subscription deletion
stripe trigger customer.subscription.deleted
```

This should:
1. Update license status to `canceled` in database
2. Revoke license in desktop app (next sync)

### Simulate Charge Refund

```bash
# Simulate refund
stripe trigger charge.refunded
```

This should:
1. Revoke license immediately
2. Add license to Certificate Revocation List (CRL)
3. Desktop app shows "License revoked" on next validation

---

## Viewing Payment Data

### 1. Stripe Dashboard (Test Mode)

Visit: https://dashboard.stripe.com/test/payments

You'll see:
- All test payments
- Customer records
- Subscription details
- Webhook delivery logs

### 2. Database (Prisma Studio)

```bash
cd voicelite-web
npx prisma studio
```

Tables to inspect:
- `License` - All issued licenses
- `User` - User accounts
- `WebhookEvent` - Webhook processing log (idempotency)

### 3. Application Logs

Watch terminal output for:
- Checkout session creation
- Webhook event processing
- License generation
- Email delivery (console logs)

---

## Email Delivery Options

### Option A: Development Mode (Current Setup)

**Pros:**
- Zero setup required
- Works immediately
- Logs license keys to console

**Cons:**
- No actual email delivery
- Manual copying of license keys

**Usage:**
Just run `npm run dev` - emails are automatically logged to console.

### Option B: Resend (Recommended for Realistic Testing)

**Pros:**
- Free tier: 3,000 emails/month
- No credit card required
- Test domain: `onboarding.resend.dev`
- Real email delivery

**Setup:**
1. Sign up at https://resend.com
2. Get API key from dashboard
3. Update `.env.local`:
   ```bash
   RESEND_API_KEY="re_abc123..."
   RESEND_FROM_EMAIL="VoiceLite <noreply@resend.dev>"
   ```
4. Restart server: `npm run dev`

### Option C: Production Email (After LLC)

When ready for production:
1. Verify custom domain (voicelite.app) in Resend
2. Update `.env.local`:
   ```bash
   RESEND_FROM_EMAIL="VoiceLite <noreply@voicelite.app>"
   ```
3. Test email delivery
4. Update Vercel environment variables

---

## Switching to Production Mode (After LLC)

When your LLC is ready and Stripe account activated:

### 1. Get Live API Keys

In Stripe Dashboard:
1. Toggle from "Test mode" to "Live mode" (top right)
2. Go to Developers → API keys
3. Copy:
   - Secret key (starts with `sk_live_...`)
   - Publishable key (starts with `pk_live_...`)

### 2. Create Production Price IDs

1. Go to Products in Stripe Dashboard (Live mode)
2. Create product: "VoiceLite Pro Quarterly"
   - Recurring: Every 3 months
   - Price: $20.00 USD
   - Copy price ID: `price_live_...`
3. Create product: "VoiceLite Pro Lifetime"
   - One-time payment
   - Price: $99.00 USD
   - Copy price ID: `price_live_...`

### 3. Update Vercel Environment Variables

In Vercel Dashboard (vercel.com):
1. Select your project
2. Settings → Environment Variables
3. Update (for Production environment):
   ```
   STRIPE_SECRET_KEY=sk_live_...
   NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY=pk_live_...
   STRIPE_QUARTERLY_PRICE_ID=price_live_...
   STRIPE_LIFETIME_PRICE_ID=price_live_...
   ```

### 4. Configure Production Webhook

1. Go to Stripe Dashboard → Developers → Webhooks (Live mode)
2. Add endpoint: `https://voicelite.app/api/webhook`
3. Select events:
   - `checkout.session.completed`
   - `customer.subscription.updated`
   - `customer.subscription.deleted`
   - `charge.refunded`
4. Copy webhook signing secret (starts with `whsec_...`)
5. Add to Vercel environment variables:
   ```
   STRIPE_WEBHOOK_SECRET=whsec_...
   ```

### 5. Test Production Flow

1. Deploy to Vercel: `vercel --prod`
2. Make test purchase on production site
3. Verify webhook delivery in Stripe Dashboard
4. Verify license creation in production database

---

## Troubleshooting

### "Email service not configured" Error (in Production)

**Cause:** `RESEND_API_KEY` not set or running in production mode without Resend.

**Fix:**
- Development: Emails will log to console automatically (no error)
- Production: Set `RESEND_API_KEY` in Vercel environment variables

### Webhook Signature Verification Failed

**Cause:** `STRIPE_WEBHOOK_SECRET` doesn't match Stripe CLI or production webhook secret.

**Fix:**
- Local: Copy secret from `stripe listen` output
- Production: Copy secret from Stripe Dashboard → Webhooks

### "Stripe price not configured" Error

**Cause:** Price IDs contain "placeholder" or are missing.

**Fix:**
1. Create products in Stripe Dashboard
2. Copy price IDs (start with `price_`)
3. Update `.env.local` or Vercel environment variables

### License Email Not Received

**Check:**
1. Console logs (development mode shows license key in terminal)
2. Resend logs (if using Resend API): https://resend.com/logs
3. Spam folder (for production emails)
4. Database (license is created even if email fails): `npx prisma studio`

---

## Summary: Current Test Capabilities

| Feature | Status | Notes |
|---------|--------|-------|
| ✅ Checkout Flow | Ready | Use card `4242424242424242` |
| ✅ License Generation | Ready | Check console logs or database |
| ✅ Quarterly Subscriptions | Ready | Test mode supports subscriptions |
| ✅ Lifetime Payments | Ready | One-time payments work |
| ⚠️ Email Delivery | Console logs | Upgrade to Resend for real emails |
| ⚠️ Webhooks | CLI required | Use `stripe listen` for local testing |
| ✅ Database Records | Ready | View with `npx prisma studio` |
| ✅ Desktop App Activation | Ready | Copy license key from console |

**Next Steps:**
1. ✅ Test checkout flow now (no setup required!)
2. Optional: Set up Resend for email delivery (~5 min)
3. Optional: Set up Stripe CLI for webhook testing (~10 min)
4. Wait for LLC before switching to production mode

**Questions?** Check:
- Stripe Test Cards: https://docs.stripe.com/testing
- Stripe CLI Docs: https://docs.stripe.com/stripe-cli
- Resend Docs: https://resend.com/docs

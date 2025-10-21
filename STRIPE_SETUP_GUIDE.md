# Stripe Setup Guide for VoiceLite

**Last Updated**: October 18, 2025
**Status**: Test keys configured, need production setup

---

## Current Status

✅ Stripe test keys are already configured in `.env.local`
✅ Stripe integration code is complete
⚠️ Need to create product and configure webhook
⚠️ Need production keys for live deployment

---

## Table of Contents

1. [Quick Start (Test Mode)](#quick-start-test-mode)
2. [Create Stripe Product](#create-stripe-product)
3. [Configure Webhook](#configure-webhook)
4. [Test the Integration](#test-the-integration)
5. [Production Setup](#production-setup)
6. [Troubleshooting](#troubleshooting)

---

## Quick Start (Test Mode)

You already have test keys configured! Here's what you need to do:

### Step 1: Verify Your Test Keys

Your `.env.local` already contains:
```env
STRIPE_SECRET_KEY="sk_test_YOUR_SECRET_KEY_HERE"
NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY="pk_test_YOUR_PUBLISHABLE_KEY_HERE"
STRIPE_WEBHOOK_SECRET="whsec_YOUR_WEBHOOK_SECRET_HERE"
```

### Step 2: Log into Stripe Dashboard

1. Go to [https://dashboard.stripe.com](https://dashboard.stripe.com)
2. Make sure you're in **Test mode** (toggle in top right)
3. Your test keys should match what's in `.env.local`

---

## Create Stripe Product

### Option A: Let the Code Create the Product (Automatic)

The checkout API ([voicelite-web/app/api/checkout/route.ts](voicelite-web/app/api/checkout/route.ts#L67-L88)) already creates the product on-the-fly:

```typescript
line_items: [
  {
    price_data: {
      currency: 'usd',
      product_data: {
        name: 'VoiceLite Pro',
        description: 'One-time purchase - Lifetime access to VoiceLite Pro features',
      },
      unit_amount: 2000, // $20.00
    },
    quantity: 1,
  },
]
```

**This is the recommended approach** - no manual product creation needed!

### Option B: Create Product Manually (For Reusability)

If you want to reuse the same product across checkouts:

1. Go to [https://dashboard.stripe.com/test/products](https://dashboard.stripe.com/test/products)
2. Click **+ Add Product**
3. Fill in:
   - **Name**: VoiceLite Pro
   - **Description**: One-time purchase - Lifetime access to VoiceLite Pro features
   - **Pricing**: One-time payment
   - **Price**: $20.00 USD
4. Click **Save product**
5. Copy the **Price ID** (starts with `price_`)
6. (Optional) Update checkout code to use the price ID instead of creating on-the-fly

---

## Configure Webhook

Webhooks are critical for receiving payment notifications and issuing licenses.

### Local Development (Using Stripe CLI)

1. **Install Stripe CLI**:
   ```bash
   # Windows (PowerShell)
   winget install stripe.stripe-cli

   # macOS
   brew install stripe/stripe-cli/stripe

   # Or download from https://stripe.com/docs/stripe-cli
   ```

2. **Login to Stripe**:
   ```bash
   stripe login
   ```

3. **Start webhook forwarding**:
   ```bash
   cd voicelite-web
   stripe listen --forward-to localhost:3000/api/webhook
   ```

4. **Copy the webhook secret** (starts with `whsec_`):
   ```bash
   # Output will show:
   # > Ready! Your webhook signing secret is whsec_abc123...
   ```

5. **Update `.env.local`**:
   ```env
   STRIPE_WEBHOOK_SECRET="whsec_abc123..."
   ```

6. **Start your dev server** (in a new terminal):
   ```bash
   cd voicelite-web
   npm run dev
   ```

### Production (Vercel)

1. Deploy your app to Vercel first
2. Go to [https://dashboard.stripe.com/webhooks](https://dashboard.stripe.com/webhooks)
3. Click **+ Add endpoint**
4. Fill in:
   - **Endpoint URL**: `https://voicelite.app/api/webhook`
   - **Events to send**: Select these events:
     - `checkout.session.completed`
     - `charge.refunded`
5. Click **Add endpoint**
6. Copy the **Signing secret** (starts with `whsec_`)
7. Add to Vercel environment variables:
   ```bash
   vercel env add STRIPE_WEBHOOK_SECRET
   # Paste the signing secret when prompted
   ```

---

## Test the Integration

### Test 1: Create a Checkout Session

```bash
# Start the dev server
cd voicelite-web
npm run dev
```

Then open your browser to [http://localhost:3000](http://localhost:3000) and:

1. Click **Buy VoiceLite Pro** (or however you trigger checkout)
2. You should be redirected to Stripe Checkout
3. Use test card: `4242 4242 4242 4242`
   - Expiry: Any future date
   - CVC: Any 3 digits
   - ZIP: Any 5 digits
4. Complete the payment

### Test 2: Verify Webhook Receipt

Check your terminal where `stripe listen` is running:

```
2025-10-18 20:00:00 --> checkout.session.completed [evt_abc123]
2025-10-18 20:00:01 <-- [200] POST http://localhost:3000/api/webhook
```

### Test 3: Verify License Creation

1. Check your email inbox for the license email
2. Or check the database:
   ```bash
   cd voicelite-web
   npx prisma studio
   # Navigate to License table
   # You should see a new license record
   ```

### Test 4: Verify Desktop App License Activation

1. Open VoiceLite desktop app
2. Click **Activate Pro License**
3. Paste the license key from the email
4. Click **Activate**
5. You should see "License activated successfully!"

---

## Production Setup

When you're ready to go live:

### Step 1: Switch to Live Mode

1. Go to [https://dashboard.stripe.com](https://dashboard.stripe.com)
2. Toggle from **Test mode** to **Live mode** (top right)
3. Complete Stripe account verification if needed

### Step 2: Get Production API Keys

1. Go to [https://dashboard.stripe.com/apikeys](https://dashboard.stripe.com/apikeys)
2. Copy your **Publishable key** (starts with `pk_live_`)
3. Reveal and copy your **Secret key** (starts with `sk_live_`)

### Step 3: Create Production Webhook

1. Go to [https://dashboard.stripe.com/webhooks](https://dashboard.stripe.com/webhooks)
2. Click **+ Add endpoint**
3. Fill in:
   - **Endpoint URL**: `https://voicelite.app/api/webhook`
   - **Events**: `checkout.session.completed`, `charge.refunded`
4. Copy the **Signing secret** (starts with `whsec_`)

### Step 4: Update Vercel Environment Variables

```bash
# Set production Stripe keys
vercel env add STRIPE_SECRET_KEY production
# Paste: sk_live_...

vercel env add NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY production
# Paste: pk_live_...

vercel env add STRIPE_WEBHOOK_SECRET production
# Paste: whsec_... (from production webhook)

# Redeploy
vercel --prod
```

### Step 5: Test with Real Payment

⚠️ **WARNING**: This will charge a real card $20!

1. Use a real credit card
2. Complete the checkout flow
3. Verify:
   - Email with license received
   - License key activates in desktop app
   - Money appears in Stripe dashboard

### Step 6: Refund Test Transaction

1. Go to [https://dashboard.stripe.com/payments](https://dashboard.stripe.com/payments)
2. Find the test payment
3. Click **Refund**
4. Verify license is revoked in database

---

## Troubleshooting

### Problem: "Stripe not configured" error

**Cause**: Missing or invalid Stripe keys

**Solution**:
```bash
# Check your .env.local
cd voicelite-web
cat .env.local | grep STRIPE

# Verify keys start with:
# STRIPE_SECRET_KEY="sk_test_..." or "sk_live_..."
# STRIPE_WEBHOOK_SECRET="whsec_..."
```

### Problem: Webhook signature verification failed

**Cause**: Webhook secret mismatch

**Solution**:
1. If using Stripe CLI locally:
   ```bash
   stripe listen --forward-to localhost:3000/api/webhook
   # Copy the new whsec_... and update .env.local
   ```

2. If in production:
   - Go to Stripe Dashboard > Webhooks
   - Click on your webhook endpoint
   - Copy the **Signing secret**
   - Update Vercel env var: `vercel env add STRIPE_WEBHOOK_SECRET`

### Problem: License email not received

**Possible causes**:
1. **Resend API not configured**: Check `RESEND_API_KEY` in `.env.local`
2. **Email in spam**: Check spam folder
3. **Invalid email**: Check Stripe checkout email

**Debug**:
```bash
# Check Vercel logs
vercel logs --follow

# Look for:
# "CRITICAL: License email failed to send"
```

**Manual recovery**:
```bash
# Find the license in database
cd voicelite-web
npx prisma studio
# Navigate to License table
# Copy the licenseKey
# Send manually to customer
```

### Problem: Checkout session creation fails

**Cause**: Invalid price or product configuration

**Solution**:
```bash
# Check error logs
cd voicelite-web
npm run dev

# Try creating checkout session
# Check terminal for Stripe API errors

# Common issues:
# - Invalid currency code
# - Invalid amount (must be positive integer in cents)
# - Invalid product description
```

### Problem: "Event too old" webhook error

**Cause**: Webhook event older than 5 minutes (replay attack protection)

**Solution**: This is expected behavior for security. Stripe will retry the webhook.

---

## Stripe Dashboard Locations

Quick reference for common Stripe dashboard pages:

- **API Keys**: [https://dashboard.stripe.com/apikeys](https://dashboard.stripe.com/apikeys)
- **Webhooks**: [https://dashboard.stripe.com/webhooks](https://dashboard.stripe.com/webhooks)
- **Products**: [https://dashboard.stripe.com/products](https://dashboard.stripe.com/products)
- **Payments**: [https://dashboard.stripe.com/payments](https://dashboard.stripe.com/payments)
- **Customers**: [https://dashboard.stripe.com/customers](https://dashboard.stripe.com/customers)
- **Test Cards**: [https://stripe.com/docs/testing#cards](https://stripe.com/docs/testing#cards)

---

## Next Steps

After Stripe is fully configured:

1. ✅ Test checkout flow locally
2. ✅ Verify webhook processing
3. ✅ Test license activation in desktop app
4. ✅ Deploy to production
5. ✅ Create production webhook
6. ✅ Test with real payment (then refund)
7. 🚀 Launch!

---

## Security Checklist

Before going live, verify:

- [ ] Production webhook secret is set in Vercel
- [ ] Production API keys are set in Vercel (not test keys!)
- [ ] Webhook endpoint uses HTTPS only
- [ ] Rate limiting is enabled on all API endpoints
- [ ] Event timestamp validation is active (5-minute window)
- [ ] Idempotency checks prevent duplicate license creation
- [ ] Email failures don't block license creation
- [ ] Test mode is disabled in production

---

## Additional Resources

- **Stripe Documentation**: [https://stripe.com/docs](https://stripe.com/docs)
- **Stripe Testing**: [https://stripe.com/docs/testing](https://stripe.com/docs/testing)
- **Stripe Webhooks**: [https://stripe.com/docs/webhooks](https://stripe.com/docs/webhooks)
- **Stripe CLI**: [https://stripe.com/docs/stripe-cli](https://stripe.com/docs/stripe-cli)

---

**Questions?** Check [TROUBLESHOOTING.md](TROUBLESHOOTING.md) or email support@voicelite.app

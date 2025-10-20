# Stripe Webhook Setup Instructions

**IMPORTANT**: Follow these steps to complete your Stripe integration.

---

## Step 1: Create Webhook Endpoint in Stripe

1. **Go to Stripe Webhooks Dashboard**:
   [https://dashboard.stripe.com/webhooks](https://dashboard.stripe.com/webhooks)

2. **Make sure you're in LIVE mode** (toggle in top right corner)

3. **Click "+ Add endpoint"**

4. **Fill in the form**:
   - **Endpoint URL**: `https://voicelite.app/api/webhook`
   - **Description**: VoiceLite Production Webhook

5. **Select events to listen to**:
   Click "Select events" and choose:
   - ✅ `checkout.session.completed` (when payment succeeds)
   - ✅ `charge.refunded` (when payment is refunded)

6. **Click "Add endpoint"**

7. **Copy the Signing Secret**:
   - After creating, you'll see the webhook details
   - Click "Reveal" next to "Signing secret"
   - Copy the secret (starts with `whsec_`)

---

## Step 2: Update Your .env.local File

Open `voicelite-web\.env.local` and replace this line:

```env
STRIPE_WEBHOOK_SECRET="whsec_PLACEHOLDER_UPDATE_AFTER_WEBHOOK_SETUP"
```

With your actual webhook secret:

```env
STRIPE_WEBHOOK_SECRET="whsec_YOUR_ACTUAL_SECRET_HERE"
```

---

## Step 3: Test the Webhook (Optional but Recommended)

### Option A: Test with Stripe CLI (Local)

```bash
# Install Stripe CLI if not already installed
winget install stripe.stripe-cli

# Login to Stripe
stripe login

# Test webhook forwarding
stripe listen --forward-to localhost:3000/api/webhook

# In another terminal, trigger a test event
stripe trigger checkout.session.completed
```

### Option B: Test in Production (After Deploy)

1. Make a real test purchase (you can refund it after)
2. Check webhook logs in Stripe Dashboard
3. Verify license was created in database

---

## Webhook Events We Handle

### `checkout.session.completed`
- **Triggered**: When customer completes payment
- **What we do**:
  1. Extract customer email and payment details
  2. Create license in database
  3. Send license key via email
  4. Log success/failure

### `charge.refunded`
- **Triggered**: When payment is refunded
- **What we do**:
  1. Find license by payment ID
  2. Revoke license (set status to inactive)
  3. Log the revocation

---

## Troubleshooting

### Webhook signature verification failed

**Cause**: Wrong webhook secret or stale event

**Fix**:
1. Go to Stripe Dashboard > Webhooks
2. Click on your endpoint
3. Copy the signing secret again
4. Update `.env.local`
5. Restart your dev server

### Event too old error

**This is expected!** We reject events older than 5 minutes for security.
Stripe will automatically retry.

### License not created after payment

**Debug steps**:
1. Check Stripe Dashboard > Webhooks > Your endpoint > Recent deliveries
2. Click on the failed event to see error details
3. Check your server logs (Vercel logs or `npm run dev` console)
4. Verify `DATABASE_URL` and `RESEND_API_KEY` are set

---

## Security Features

Our webhook handler includes:

- ✅ **Signature verification** - Only accepts requests signed by Stripe
- ✅ **Timestamp validation** - Rejects events older than 5 minutes
- ✅ **Idempotency checks** - Prevents duplicate license creation
- ✅ **Email failure handling** - License still created if email fails

---

## What's Next?

After setting up the webhook:

1. ✅ Add webhook secret to `.env.local`
2. 🔲 Get database URL from Supabase
3. 🔲 Get Resend API key for emails
4. 🔲 Test locally with `npm run dev`
5. 🔲 Deploy to Vercel
6. 🔲 Update Vercel environment variables
7. 🔲 Test with real payment

---

**Once you have the webhook secret, paste it here and I'll update your `.env.local` file!**
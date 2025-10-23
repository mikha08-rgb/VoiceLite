# Stripe Setup Guide for VoiceLite

## Prerequisites
- Stripe account (create at https://stripe.com)
- Vercel CLI installed (`npm i -g vercel`)
- Access to voicelite-web environment variables

## Step 1: Create Stripe Products

### 1.1 Login to Stripe Dashboard
1. Go to https://dashboard.stripe.com/test/products (use test mode first)
2. Click "Add product"

### 1.2 Create Quarterly Subscription
1. **Product name**: VoiceLite Quarterly
2. **Description**: "Premium Whisper models and priority support"
3. **Pricing**:
   - **Price**: $20.00 USD
   - **Billing period**: Every 3 months
   - **Recurring**: Yes
4. Click "Save product"
5. **Copy the Price ID** (starts with `price_...`)
   - Example: `price_1QXnF2BkZ9rwClq1HfK3Qz8L`

### 1.3 Create Lifetime License
1. Click "Add product" again
2. **Product name**: VoiceLite Lifetime
3. **Description**: "Lifetime access to all premium features"
4. **Pricing**:
   - **Price**: $99.00 USD
   - **Billing period**: One-time
   - **Recurring**: No
5. Click "Save product"
6. **Copy the Price ID** (starts with `price_...`)
   - Example: `price_1QXnG3BkZ9rwClq1AbC4Xy9M`

## Step 2: Add Environment Variables to Vercel

### 2.1 Add Quarterly Price ID
```bash
cd voicelite-web
vercel env add STRIPE_QUARTERLY_PRICE_ID production
# Paste: price_1QXnF2BkZ9rwClq1HfK3Qz8L (your actual quarterly price ID)
```

### 2.2 Add Lifetime Price ID
```bash
vercel env add STRIPE_LIFETIME_PRICE_ID production
# Paste: price_1QXnG3BkZ9rwClq1AbC4Xy9M (your actual lifetime price ID)
```

### 2.3 Verify Environment Variables
```bash
vercel env ls
# Should show:
# - STRIPE_QUARTERLY_PRICE_ID (production)
# - STRIPE_LIFETIME_PRICE_ID (production)
```

## Step 3: Redeploy Website
```bash
vercel --prod
# Wait for deployment to complete
```

## Step 4: Test Payment Flow (Test Mode)

### 4.1 Visit Website
1. Go to https://voicelite.app
2. Sign in with magic link (use your email)

### 4.2 Test Quarterly Purchase
1. Click "Upgrade to Pro" → Select "Quarterly"
2. Use Stripe test card:
   - **Card number**: `4242 4242 4242 4242`
   - **Expiry**: Any future date (e.g., 12/25)
   - **CVC**: Any 3 digits (e.g., 123)
   - **ZIP**: Any 5 digits (e.g., 12345)
3. Complete checkout
4. Verify:
   - Redirected to success page
   - License key received via email
   - License visible in account dashboard

### 4.3 Test Lifetime Purchase
1. Sign out and sign back in (to test fresh flow)
2. Click "Upgrade to Pro" → Select "Lifetime"
3. Use same test card: `4242 4242 4242 4242`
4. Complete checkout
5. Verify same as above

### 4.4 Test License Activation in Desktop App
1. Download latest VoiceLite installer
2. Install and open app
3. Go to Settings → License
4. Enter license key from email
5. Click "Activate"
6. Verify:
   - "License activated successfully"
   - Premium models unlock (Base, Small, Medium visible)
   - Settings show "Pro" status

## Step 5: Switch to Live Mode (When Ready for Production)

### 5.1 Create Live Products
1. Toggle Stripe dashboard to **Live mode** (top left switch)
2. Repeat Step 1 (create products) in live mode
3. Copy new **live price IDs**

### 5.2 Update Environment Variables
```bash
# Remove test price IDs
vercel env rm STRIPE_QUARTERLY_PRICE_ID production
vercel env rm STRIPE_LIFETIME_PRICE_ID production

# Add live price IDs
vercel env add STRIPE_QUARTERLY_PRICE_ID production
# Paste: price_LIVE_1QXnF2... (your actual live quarterly price ID)

vercel env add STRIPE_LIFETIME_PRICE_ID production
# Paste: price_LIVE_1QXnG3... (your actual live lifetime price ID)
```

### 5.3 Update Stripe Secret Key (if needed)
1. Get live secret key from https://dashboard.stripe.com/apikeys
2. Update in Vercel:
```bash
vercel env rm STRIPE_SECRET_KEY production
vercel env add STRIPE_SECRET_KEY production
# Paste: sk_live_... (your live secret key)
```

### 5.4 Redeploy
```bash
vercel --prod
```

### 5.5 Test Live Payment
1. Use a REAL card (start with a small test purchase)
2. Verify webhook fires correctly
3. Check Stripe dashboard for successful payment
4. Confirm license key is generated and emailed

## Step 6: Webhook Configuration (If Not Already Set Up)

### 6.1 Get Webhook Endpoint URL
Your webhook is at: `https://voicelite.app/api/webhook`

### 6.2 Configure in Stripe
1. Go to https://dashboard.stripe.com/webhooks
2. Click "Add endpoint"
3. **Endpoint URL**: `https://voicelite.app/api/webhook`
4. **Events to send**:
   - `checkout.session.completed`
   - `customer.subscription.created`
   - `customer.subscription.updated`
   - `customer.subscription.deleted`
   - `invoice.paid`
   - `invoice.payment_failed`
5. Click "Add endpoint"
6. Copy the **Signing secret** (starts with `whsec_...`)

### 6.3 Add Webhook Secret to Vercel
```bash
vercel env add STRIPE_WEBHOOK_SECRET production
# Paste: whsec_... (your webhook signing secret)
```

### 6.4 Redeploy
```bash
vercel --prod
```

## Troubleshooting

### Issue: "Stripe price not configured" error
**Solution**: Verify environment variables are set correctly:
```bash
vercel env ls
# Should show STRIPE_QUARTERLY_PRICE_ID and STRIPE_LIFETIME_PRICE_ID
```

### Issue: Checkout fails with no error message
**Solution**: Check Stripe dashboard logs:
1. Go to https://dashboard.stripe.com/logs
2. Look for failed API requests
3. Check error messages

### Issue: License key not received via email
**Solution**:
1. Check spam folder
2. Verify Resend API key is set: `vercel env ls | grep RESEND`
3. Check Resend dashboard for delivery logs

### Issue: Webhook not firing
**Solution**:
1. Verify webhook URL matches: `https://voicelite.app/api/webhook`
2. Check Stripe webhook logs for delivery attempts
3. Ensure STRIPE_WEBHOOK_SECRET is set correctly

## Quick Reference

### Test Cards (Test Mode Only)
- **Success**: `4242 4242 4242 4242`
- **Requires authentication**: `4000 0025 0000 3155`
- **Declined**: `4000 0000 0000 9995`

### Environment Variables Checklist
- [ ] `STRIPE_SECRET_KEY` (sk_test_... or sk_live_...)
- [ ] `STRIPE_QUARTERLY_PRICE_ID` (price_...)
- [ ] `STRIPE_LIFETIME_PRICE_ID` (price_...)
- [ ] `STRIPE_WEBHOOK_SECRET` (whsec_...)
- [ ] `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` (pk_test_... or pk_live_...)

### Commands
```bash
# List all env vars
vercel env ls

# Add env var
vercel env add VAR_NAME production

# Remove env var
vercel env rm VAR_NAME production

# Redeploy
vercel --prod
```

## Next Steps

Once Stripe is configured and tested:
1. ✅ Mark "Configure Stripe" task as complete
2. ✅ Proceed to launch materials creation
3. ✅ Prepare ProductHunt/HN/Reddit posts
4. ✅ Launch!

---

**Need help?** Check Stripe documentation at https://stripe.com/docs or Vercel docs at https://vercel.com/docs

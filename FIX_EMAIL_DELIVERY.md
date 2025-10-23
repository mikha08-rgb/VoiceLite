# Fix Email Delivery - 2 Quick Steps

## Problem
Stripe webhooks are failing with **500 errors** → No license emails being sent.

**Root Causes Found:**
1. ❌ Database tables don't exist (migrations never run)
2. ❌ `STRIPE_PRO_PRICE_ID` missing from Vercel

---

## Fix #1: Create Database Tables (5 minutes)

**Go to**: https://supabase.com → Your project → SQL Editor

**Run this SQL**:

```sql
CREATE TABLE IF NOT EXISTS "User" (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::text,
  email TEXT UNIQUE NOT NULL,
  "createdAt" TIMESTAMP NOT NULL DEFAULT NOW(),
  "updatedAt" TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "License" (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::text,
  "userId" TEXT NOT NULL REFERENCES "User"(id) ON DELETE CASCADE,
  "licenseKey" TEXT UNIQUE NOT NULL,
  type TEXT NOT NULL DEFAULT 'LIFETIME',
  status TEXT NOT NULL DEFAULT 'ACTIVE',
  "stripeCustomerId" TEXT,
  "stripeSubscriptionId" TEXT UNIQUE,
  "stripePaymentIntentId" TEXT UNIQUE,
  "activatedAt" TIMESTAMP,
  "expiresAt" TIMESTAMP,
  "createdAt" TIMESTAMP NOT NULL DEFAULT NOW(),
  "updatedAt" TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "WebhookEvent" (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::text,
  "eventId" TEXT UNIQUE NOT NULL,
  "createdAt" TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "LicenseEvent" (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::text,
  "licenseId" TEXT NOT NULL REFERENCES "License"(id) ON DELETE CASCADE,
  type TEXT NOT NULL,
  metadata JSONB,
  "createdAt" TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "UserActivity" (
  id TEXT PRIMARY KEY DEFAULT gen_random_uuid()::text,
  "userId" TEXT NOT NULL REFERENCES "User"(id) ON DELETE CASCADE,
  "activityType" TEXT NOT NULL,
  metadata JSONB,
  "createdAt" TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "License_userId_idx" ON "License"("userId");
CREATE INDEX IF NOT EXISTS "License_stripeCustomerId_idx" ON "License"("stripeCustomerId");
```

Click **"Run"** → Should say "Success. No rows returned"

---

## Fix #2: Add Missing Environment Variable (2 minutes)

### Step A: Get Your Stripe Price ID

1. Go to: https://dashboard.stripe.com/products
2. Click on **"VoiceLite Pro"** product
3. Copy the **Price ID** (starts with `price_...`)
   - Example: `price_1AbC2dEfGhIj3KlM4nO`

### Step B: Add to Vercel

1. Go to: https://vercel.com/mishas-projects-0509f3dc/voicelite/settings/environment-variables
2. Click **"Add New"**
3. Name: `STRIPE_PRO_PRICE_ID`
4. Value: Paste the price ID from Step A
5. Click **Save**

### Step C: Redeploy

```bash
cd voicelite-web
vercel deploy --prod
```

Wait ~2 minutes for deployment to complete.

---

## Test the Fix (1 minute)

### Option 1: Resend Failed Webhook in Stripe

1. Go to: https://dashboard.stripe.com/webhooks
2. Click on your webhook (`voicelite.app/api/webhooks/stripe`)
3. Click **"Event deliveries"** tab
4. Find a recent failed `checkout.session.completed` event
5. Click **"..."** → **"Resend event"**
6. **Should now return 200 OK** ✅
7. Check your email - license should arrive!

### Option 2: Make a Test Purchase

Use Stripe test mode with card: `4242 4242 4242 4242`

---

## Verify Everything Works

After applying both fixes:

1. ✅ Stripe webhook returns **200 OK** (not 500)
2. ✅ Email arrives within 1-2 minutes
3. ✅ License key is UUID format (e.g., `a1b2c3d4-e5f6-...`)
4. ✅ Desktop app activation works

---

## If Email Still Doesn't Arrive

Check Vercel function logs:

```bash
cd voicelite-web
vercel logs --prod
```

Look for:
- ✅ `=== STRIPE WEBHOOK RECEIVED ===`
- ✅ `Webhook signature verified successfully`
- ✅ `✅ License email sent successfully!`

If you see ❌ errors, check:
- **Resend domain verified?** → https://resend.com/domains
- **RESEND_API_KEY correct?** → Vercel environment variables
- **RESEND_FROM_EMAIL matches verified domain?** → Should be `noreply@voicelite.app`

---

## Emergency: Manual License for Customer

If webhook still fails and customer is waiting:

1. Go to **Supabase SQL Editor**
2. Generate UUID at: https://www.uuidgenerator.net/
3. Run the SQL in [CREATE_LICENSE_MANUAL.sql](./CREATE_LICENSE_MANUAL.sql)
4. Email customer their license key manually

---

## Summary

**Before Fix:**
- Stripe webhook: ❌ 500 error
- Database tables: ❌ Don't exist
- Environment variables: ❌ Missing STRIPE_PRO_PRICE_ID
- Email delivery: ❌ None

**After Fix:**
- Stripe webhook: ✅ 200 OK
- Database tables: ✅ Created
- Environment variables: ✅ Complete
- Email delivery: ✅ Working

**Total Time**: ~8 minutes

🎯 **Do both fixes, then test with Stripe webhook resend!**

# Webhook Debugging Guide - Email Not Received

## Quick Checklist

### 1. Check Stripe Webhook Delivery

Go to: **Stripe Dashboard** → **Developers** → **Webhooks** → Click on your webhook (`https://voicelite.app/api/webhooks/stripe`)

Look for recent webhook events:
- ✅ **200 OK** = Webhook processed successfully
- ❌ **400/500** = Error (click to see details)
- 🔁 **Multiple attempts** = Webhook is retrying

**Action**: Screenshot the most recent `checkout.session.completed` event and check the response.

---

### 2. Check Vercel Function Logs

**Option A: Via Vercel Dashboard**
1. Go to https://vercel.com/mishas-projects-0509f3dc/voicelite
2. Click **Deployments** → Click the latest deployment
3. Click **Functions** tab
4. Look for `/api/webhooks/stripe`
5. Click **View Logs**

**Option B: Via CLI**
```bash
cd voicelite-web
vercel logs voicelite-ndauh2oig-mishas-projects-0509f3dc.vercel.app
```

**What to look for:**
- `License created and email sent` = Success ✅
- `Failed to send license email` = Resend issue ❌
- `No customer email` = Stripe session issue ❌
- `Invalid signature` = Webhook secret mismatch ❌

---

### 3. Check Resend Configuration

**Required environment variables in Vercel:**
- `RESEND_API_KEY` = Your Resend API key
- `RESEND_FROM_EMAIL` = Must be verified domain (e.g., `noreply@voicelite.app`)

**Verify:**
1. Go to https://resend.com/emails
2. Check if any emails were sent (check Last Hour filter)
3. If email shows as "Delivered" but you didn't receive it → **Check spam folder**
4. If email shows as "Failed" → Click to see error (likely domain not verified)

**Common Resend errors:**
- `domain_not_verified` → Add DNS records for your domain
- `invalid_from_address` → Use verified domain email
- `rate_limit_exceeded` → Wait or upgrade Resend plan

---

### 4. Manual Workaround (Get Your License Now)

Since payment succeeded but email failed, here's how to generate a license manually:

**Step 1: Generate License Key**
```bash
cd voicelite-web
node manual-license.mjs your-email@example.com
```

This will output a UUID license key like:
```
8a7f3b2e-4d9c-1f6e-8b3a-5c9d2e7f4a1b
```

**Step 2: Test Validation**

Test if it works with the validation API:
```bash
curl -X POST https://voicelite.app/api/licenses/validate \
  -H "Content-Type: application/json" \
  -d "{\"licenseKey\": \"YOUR-KEY-HERE\"}"
```

Expected response:
```json
{
  "valid": false,
  "tier": "free"
}
```

**Why `valid: false`?** Because the license isn't in the database yet (webhook didn't run). We need to manually add it.

---

### 5. Check Database Directly (Supabase)

**Via Supabase Dashboard:**
1. Go to https://supabase.com/dashboard
2. Select your project
3. Go to **SQL Editor**
4. Run this query:

```sql
-- Check recent licenses
SELECT l.*, u.email
FROM "License" l
JOIN "User" u ON l."userId" = u.id
ORDER BY l."createdAt" DESC
LIMIT 5;

-- Check webhook events
SELECT * FROM "WebhookEvent"
ORDER BY "createdAt" DESC
LIMIT 5;
```

**What this tells you:**
- If **no licenses** → Webhook never ran successfully
- If **license exists** but no email → Resend issue
- If **no webhook events** → Stripe webhook not firing

---

## Root Cause Analysis

### Most Likely Issues:

1. **Resend Domain Not Verified** (90% probability)
   - **Solution**: Go to Resend → Domains → Add voicelite.app → Add DNS records

2. **RESEND_FROM_EMAIL Environment Variable Wrong**
   - **Check**: Vercel dashboard → Project Settings → Environment Variables
   - **Should be**: `noreply@voicelite.app` (or other verified email)

3. **Webhook Secret Mismatch**
   - **Check**: `STRIPE_WEBHOOK_SECRET` in Vercel matches Stripe dashboard
   - **Fix**: Copy signing secret from Stripe → Update in Vercel → Redeploy

4. **Stripe Webhook Not Sending `checkout.session.completed`**
   - **Check**: Stripe dashboard → Webhooks → Edit → Ensure event is selected
   - **Fix**: Select `checkout.session.completed` event

---

## Quick Fix: Manual License Issuance via Supabase

**If you need to issue a license RIGHT NOW without waiting for debugging:**

1. Go to Supabase SQL Editor
2. Run this (replace with actual email):

```sql
-- Create user if doesn't exist
INSERT INTO "User" (id, email, "createdAt", "updatedAt")
VALUES (gen_random_uuid(), 'customer@email.com', NOW(), NOW())
ON CONFLICT (email) DO NOTHING;

-- Get user ID
WITH user_id AS (
  SELECT id FROM "User" WHERE email = 'customer@email.com'
)
-- Create license
INSERT INTO "License" (
  id,
  "userId",
  "licenseKey",
  type,
  status,
  "activatedAt",
  "createdAt",
  "updatedAt"
)
SELECT
  gen_random_uuid(),
  id,
  'PASTE-UUID-HERE', -- Generate at uuidgenerator.net
  'LIFETIME',
  'ACTIVE',
  NOW(),
  NOW(),
  NOW()
FROM user_id;

-- Return the license
SELECT l."licenseKey", u.email
FROM "License" l
JOIN "User" u ON l."userId" = u.id
WHERE u.email = 'customer@email.com';
```

3. Copy the license key from output
4. Manually email it to customer (or use yourself if testing)

---

## Testing After Fix

Once you've fixed the root cause:

1. **Test webhook manually** in Stripe dashboard:
   - Find a past `checkout.session.completed` event
   - Click "..." → "Resend"
   - Check Vercel logs for success

2. **Test email sending** with test endpoint:
```bash
curl -X POST https://voicelite.app/api/test-email \
  -H "Content-Type: application/json" \
  -d "{\"email\": \"your-email@example.com\", \"licenseKey\": \"test-123\"}"
```

3. **Do a real $1 test purchase** (change Stripe price temporarily):
   - Create test product for $1
   - Complete checkout
   - Verify email arrives

---

## Next Steps

1. **Check Stripe webhook delivery status** (most important)
2. **Check Vercel function logs** for errors
3. **Verify Resend configuration** and domain verification
4. **If stuck**: Use manual license issuance SQL above

Let me know what you find! 🔍

# VoiceLite Credential Rotation Guide

**Date**: October 19, 2025
**Status**: IN PROGRESS
**Reason**: Security incident - exposed credentials in git history

---

## Overview

All production credentials must be rotated in the following order:

1. ✅ Stripe Webhook Secret (do this first - most critical)
2. ⏳ Supabase Database Password
3. ⏳ Resend API Key
4. ⏳ Upstash Redis Token

---

## 1. Stripe Webhook Secret (CRITICAL - DO FIRST)

### Why Critical?
Exposed webhook secret allows attackers to forge payment events and create unlimited Pro licenses without paying.

### Steps

#### A. Generate New Secret in Stripe Dashboard

1. Go to: https://dashboard.stripe.com/webhooks
2. Find your webhook endpoint: `https://voicelite.app/api/webhook`
3. Click the endpoint to open details
4. Click the "..." menu (top right)
5. Select "Roll signing secret"
6. Click "Roll secret" to confirm
7. Copy the new secret (starts with `whsec_`)

**Screenshot location**: Settings → Webhooks → [Your endpoint] → ... → Roll signing secret

#### B. Update Vercel Environment Variable

```bash
cd voicelite-web

# Remove old secret
vercel env rm STRIPE_WEBHOOK_SECRET production

# Add new secret (you'll be prompted to paste it)
vercel env add STRIPE_WEBHOOK_SECRET production
# When prompted, paste: whsec_YOUR_NEW_SECRET_HERE
```

#### C. Update Local Environment Files

Create new `.env.production` file:
```bash
# Copy from template
cp .env.production.template .env.production

# Edit and add the new webhook secret
STRIPE_WEBHOOK_SECRET="whsec_YOUR_NEW_SECRET_HERE"
```

Also update `.env.local` if you test webhooks locally:
```bash
STRIPE_WEBHOOK_SECRET="whsec_YOUR_NEW_SECRET_HERE"
```

#### D. Redeploy to Production

```bash
vercel --prod
```

Wait for deployment to complete (usually 1-2 minutes).

#### E. Test the Webhook

1. Go to: https://dashboard.stripe.com/webhooks
2. Click your webhook endpoint
3. Click "Send test webhook" button
4. Select event type: `checkout.session.completed`
5. Click "Send test webhook"
6. Verify response: **200 OK** (green checkmark)

If you see 401 Unauthorized, the secret wasn't updated correctly. Repeat steps B-D.

#### F. Verify in Production

Make a real test purchase:
```bash
# Open your website
# Complete a checkout flow
# Verify you receive license email
# Check Supabase database for new license record
```

**Status**: ⏳ PENDING

---

## 2. Supabase Database Password (CRITICAL)

### Why Critical?
Exposed database password gives full read/write access to your production database.

### Steps

#### A. Reset Password in Supabase Dashboard

1. Go to: https://supabase.com/dashboard/project/lvocjzqjqllouzyggpqm/settings/database
2. Scroll to "Database Password" section
3. Click "Reset Database Password"
4. Copy the new password (save it securely - it's only shown once)

#### B. Update Connection Strings

You need to update TWO environment variables with the new password:

**DATABASE_URL** (pooler connection - used for most queries):
```
postgresql://postgres.lvocjzqjqllouzyggpqm:NEW_PASSWORD_HERE@aws-1-us-east-1.pooler.supabase.com:6543/postgres?pgbouncer=true&connection_limit=1
```

**DIRECT_DATABASE_URL** (direct connection - used for migrations):
```
postgresql://postgres:NEW_PASSWORD_HERE@db.dzgqyytpkvjguxlhcpgl.supabase.co:5432/postgres
```

**Note**: URL-encode special characters in password:
- `!` → `%21`
- `@` → `%40`
- `#` → `%23`
- `$` → `%24`
- `%` → `%25`
- `&` → `%26`

#### C. Update Vercel Environment Variables

```bash
cd voicelite-web

# Remove old variables
vercel env rm DATABASE_URL production
vercel env rm DIRECT_DATABASE_URL production

# Add new variables
vercel env add DATABASE_URL production
# Paste: postgresql://postgres.lvocjzqjqllouzyggpqm:NEW_PASSWORD@...

vercel env add DIRECT_DATABASE_URL production
# Paste: postgresql://postgres:NEW_PASSWORD@...
```

#### D. Update Local Environment Files

Update `.env.production`:
```env
DATABASE_URL="postgresql://postgres.lvocjzqjqllouzyggpqm:NEW_PASSWORD@..."
DIRECT_DATABASE_URL="postgresql://postgres:NEW_PASSWORD@..."
```

Update `.env.local`:
```env
DATABASE_URL="postgresql://postgres.lvocjzqjqllouzyggpqm:NEW_PASSWORD@..."
```

#### E. Test Database Connection

```bash
cd voicelite-web
npx prisma db pull
```

Expected output: "Introspection completed successfully" ✅

#### F. Redeploy to Production

```bash
vercel --prod
```

#### G. Verify API Works

```bash
curl -X POST https://voicelite.app/api/license/validate \
  -H "Content-Type: application/json" \
  -d '{"licenseKey":"test","hardwareId":"test"}'
```

Expected: Should get JSON response (not 500 error) ✅

**Status**: ⏳ PENDING

---

## 3. Resend API Key (HIGH PRIORITY)

### Why Important?
Exposed API key allows attackers to send emails from your domain (voicelite.app) for phishing.

### Steps

#### A. Revoke Old Key in Resend Dashboard

1. Go to: https://resend.com/api-keys
2. Find the old key (should show partial match: `re_Vn4JijC8_...`)
3. Click "Delete" to revoke it

#### B. Create New API Key

1. Click "Create API Key" button
2. Name: `VoiceLite Production`
3. Permission: **Full Access**
4. Click "Add"
5. Copy the new key (starts with `re_`)

#### C. Update Vercel Environment Variable

```bash
cd voicelite-web

# Remove old key
vercel env rm RESEND_API_KEY production

# Add new key
vercel env add RESEND_API_KEY production
# Paste: re_YOUR_NEW_KEY_HERE
```

#### D. Update Local Environment Files

Update `.env.production`:
```env
RESEND_API_KEY="re_YOUR_NEW_KEY_HERE"
```

Update `.env.local`:
```env
RESEND_API_KEY="re_YOUR_NEW_KEY_HERE"
```

#### E. Redeploy to Production

```bash
vercel --prod
```

#### F. Test Email Delivery

Send a test license email:
```bash
# Make a test purchase OR
# Use Stripe test mode to trigger checkout.session.completed webhook
```

Verify:
- Email arrives in inbox ✅
- Email comes from `VoiceLite <noreply@voicelite.app>` ✅
- License key is included ✅

**Status**: ⏳ PENDING

---

## 4. Upstash Redis Token (MEDIUM PRIORITY)

### Why Important?
Exposed Redis token allows attackers to bypass rate limiting on your API.

### Steps

#### A. Rotate Token in Upstash Dashboard

1. Go to: https://console.upstash.com/redis/golden-ibex-26450
2. Click the "Details" tab
3. Scroll to "REST API" section
4. Click "Rotate Token" button
5. Confirm rotation
6. Copy the new token

#### B. Update Vercel Environment Variable

```bash
cd voicelite-web

# Remove old token
vercel env rm UPSTASH_REDIS_REST_TOKEN production

# Add new token
vercel env add UPSTASH_REDIS_REST_TOKEN production
# Paste: YOUR_NEW_TOKEN_HERE
```

#### C. Update Local Environment Files

Update `.env.production`:
```env
UPSTASH_REDIS_REST_TOKEN="YOUR_NEW_TOKEN_HERE"
```

Update `.env.local`:
```env
UPSTASH_REDIS_REST_TOKEN="YOUR_NEW_TOKEN_HERE"
```

#### D. Redeploy to Production

```bash
vercel --prod
```

#### E. Test Rate Limiting

Make multiple rapid requests to a rate-limited endpoint:
```bash
for i in {1..10}; do
  curl -X POST https://voicelite.app/api/license/validate \
    -H "Content-Type: application/json" \
    -d '{"licenseKey":"test","hardwareId":"test"}' \
    -w "\nStatus: %{http_code}\n"
done
```

Expected behavior:
- First requests: 200 or 400 (normal responses)
- After ~5 requests: 429 (rate limit exceeded) ✅

**Status**: ⏳ PENDING

---

## Post-Rotation Verification

After rotating ALL credentials, verify the entire system:

### 1. Health Check
```bash
curl https://voicelite.app/api/health
```

Expected: `{"status":"ok"}` ✅

### 2. License Validation
```bash
curl -X POST https://voicelite.app/api/license/validate \
  -H "Content-Type: application/json" \
  -d '{"licenseKey":"TEST_LICENSE","hardwareId":"TEST_HW"}'
```

Expected: JSON response with validation result ✅

### 3. Database Query
```bash
cd voicelite-web
npx prisma studio
```

Expected: Should open and show tables ✅

### 4. Test Purchase Flow
1. Go to https://voicelite.app
2. Click "Get Pro"
3. Complete checkout (use Stripe test card: `4242 4242 4242 4242`)
4. Verify webhook fires (check Stripe dashboard)
5. Verify email arrives
6. Verify license appears in database

Expected: Full flow works end-to-end ✅

---

## Security Monitoring (Next 7 Days)

Monitor these services for unauthorized access:

### Stripe Dashboard
- https://dashboard.stripe.com/events
- Look for suspicious `checkout.session.completed` events
- Verify all charges have corresponding database entries

### Supabase Logs
- Project Settings → Logs → Database
- Look for unusual queries or IP addresses
- Check for unexpected license modifications

### Resend Logs
- https://resend.com/logs
- Verify all emails are legitimate
- Check for unusual sending patterns

### Upstash Metrics
- https://console.upstash.com/redis/golden-ibex-26450
- Monitor request volume
- Check for unusual spikes

---

## Rollback Plan

If anything breaks during rotation:

### Database Connection Issues
1. Restore old password from Supabase dashboard
2. Update Vercel env vars with old password
3. Redeploy: `vercel --prod`

### Webhook Verification Failures
1. Go to Stripe dashboard → Webhooks
2. Check webhook secret matches Vercel env var
3. Use "Test webhook" feature to debug

### Email Delivery Failures
1. Check Resend API key is correctly set in Vercel
2. Verify domain DNS records (shouldn't change)
3. Check Resend logs for error details

### Emergency Contact
- Email: support@voicelite.app
- Supabase Support: https://supabase.com/dashboard/support
- Stripe Support: https://support.stripe.com/

---

## Completion Checklist

- [ ] Stripe webhook secret rotated and tested
- [ ] Database password rotated and tested
- [ ] Resend API key rotated and tested
- [ ] Upstash Redis token rotated and tested
- [ ] Health check passes
- [ ] License validation works
- [ ] Database connection works
- [ ] Test purchase flow completes
- [ ] Email delivery works
- [ ] Rate limiting works
- [ ] All old credentials removed from local files
- [ ] Monitoring setup for next 7 days

---

**Last Updated**: October 19, 2025
**Status**: IN PROGRESS
**Next Action**: Rotate Stripe webhook secret (most critical)

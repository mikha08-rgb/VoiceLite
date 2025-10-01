# VoiceLite Production Deployment Guide

Complete step-by-step guide to deploy VoiceLite to production.

---

## Prerequisites

- [ ] GitHub account with repository access
- [ ] Vercel account (or alternative hosting)
- [ ] Supabase account
- [ ] Stripe account
- [ ] Resend account
- [ ] Upstash account
- [ ] Domain name configured (voicelite.com, app.voicelite.com)

---

## Phase 1: Database Setup (Supabase)

### 1.1 Create Supabase Project

1. Go to https://app.supabase.com
2. Click "New Project"
3. Choose organization and set:
   - **Name**: VoiceLite Production
   - **Database Password**: Generate strong password (save to password manager)
   - **Region**: Choose closest to your users (e.g., us-west-1)
4. Wait for project to be provisioned (~2 minutes)

### 1.2 Get Database Connection String

1. Go to Project Settings → Database
2. Copy **Connection String** (URI format)
3. Replace `[YOUR-PASSWORD]` with your database password
4. Save this as `DATABASE_URL` in your .env.local

**Example**:
```
DATABASE_URL="postgresql://postgres.abcdefgh:YourPassword123@aws-0-us-west-1.pooler.supabase.com:6543/postgres"
```

### 1.3 Run Database Migrations

From your local machine:

```bash
cd voicelite-web

# Set DATABASE_URL temporarily
export DATABASE_URL="postgresql://postgres..."

# Run migrations
npx prisma migrate deploy

# Generate Prisma client
npx prisma generate
```

**Expected output**: "All migrations have been successfully applied"

### 1.4 Verify Database Schema

```bash
# Open Prisma Studio to verify tables
npx prisma studio
```

Should see tables: User, Session, MagicLinkToken, License, LicenseActivation, WebhookEvent

---

## Phase 2: Stripe Setup (Payments)

### 2.1 Get API Keys

1. Go to https://dashboard.stripe.com/apikeys
2. Toggle to **Live mode** (top right)
3. Copy:
   - **Secret key** (sk_live_...) → `STRIPE_SECRET_KEY`
   - **Publishable key** (pk_live_...) → `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY`

### 2.2 Create Products

1. Go to https://dashboard.stripe.com/products
2. Create **Quarterly Subscription**:
   - **Name**: VoiceLite Pro (Quarterly)
   - **Description**: 3-month subscription to VoiceLite Pro
   - **Pricing**: $20.00 USD, Recurring every 3 months
   - **Billing Period**: Every 3 months
   - Copy **Price ID** (price_...) → `STRIPE_PRICE_QUARTERLY`

3. Create **Lifetime License**:
   - **Name**: VoiceLite Pro (Lifetime)
   - **Description**: One-time payment for lifetime access
   - **Pricing**: $99.00 USD, One-time
   - Copy **Price ID** (price_...) → `STRIPE_PRICE_LIFETIME`

### 2.3 Configure Webhook Endpoint

**Important**: Do this AFTER deploying to Vercel (Step 4)

1. Go to https://dashboard.stripe.com/webhooks
2. Click "Add endpoint"
3. **Endpoint URL**: `https://app.voicelite.com/api/webhook`
4. **Events to listen to**:
   - `checkout.session.completed`
   - `customer.subscription.updated`
   - `customer.subscription.deleted`
   - `charge.refunded`
5. Click "Add endpoint"
6. Copy **Signing secret** (whsec_...) → `STRIPE_WEBHOOK_SECRET`

---

## Phase 3: Email Setup (Resend)

### 3.1 Create Resend Account

1. Go to https://resend.com/signup
2. Verify your email address

### 3.2 Add & Verify Domain

1. Go to https://resend.com/domains
2. Click "Add Domain"
3. Enter your domain: `voicelite.com`
4. Add the DNS records to your domain registrar:
   - **TXT record** for domain verification
   - **CNAME records** for DKIM (email authentication)
5. Wait for verification (~5-30 minutes)

### 3.3 Get API Key

1. Go to https://resend.com/api-keys
2. Click "Create API Key"
3. **Name**: VoiceLite Production
4. **Permission**: Full Access (or Sending Access only)
5. Copy **API Key** (re_...) → `RESEND_API_KEY`

### 3.4 Configure From Address

Set in .env.local:
```
RESEND_FROM_EMAIL="VoiceLite <noreply@voicelite.com>"
```

---

## Phase 4: Redis Setup (Upstash - Rate Limiting)

### 4.1 Create Upstash Database

1. Go to https://console.upstash.com/redis
2. Click "Create Database"
3. **Name**: voicelite-prod-ratelimit
4. **Type**: Regional (cheaper) or Global (better performance)
5. **Region**: Same as Vercel deployment (e.g., us-east-1)
6. Click "Create"

### 4.2 Get REST API Credentials

1. Open your database
2. Scroll to **REST API** section
3. Copy:
   - **UPSTASH_REDIS_REST_URL**: `https://xxx.upstash.io`
   - **UPSTASH_REDIS_REST_TOKEN**: Long token string

---

## Phase 5: Generate Cryptographic Keys

### 5.1 Generate Ed25519 Keypairs

From your local machine:

```bash
cd voicelite-web
npm run keygen
```

**Output**:
```
📝 License Signing Keys:
LICENSE_SIGNING_PRIVATE_B64="kgh68w4YfLQQmn5BsimTKscDvr70FlzYbhV76t-uKik"
LICENSE_SIGNING_PUBLIC_B64="_izLpBoUKYz9rwClq1WIJFz5DrmISEbyG1esLEwK-ms"

📝 CRL Signing Keys:
CRL_SIGNING_PRIVATE_B64="PF-gFncB9ADmHXMbwcIQX0jUc5I1xTasI8-QN-d0RYQ"
CRL_SIGNING_PUBLIC_B64="TSnzHX-auBPNqJF8P6vRS4ukfl7WcqZeAVHW9pnrD-0"
```

### 5.2 Save Keys to .env.local

Copy all 4 keys to your `.env.local` file

### 5.3 Update Desktop Client

**CRITICAL**: Copy `LICENSE_SIGNING_PUBLIC_B64` to desktop client:

1. Open `VoiceLite/VoiceLite/Services/Licensing/LicenseService.cs`
2. Find line ~34: `private const string LICENSE_PUBLIC_KEY`
3. Replace with your `LICENSE_SIGNING_PUBLIC_B64` value:

```csharp
private const string LICENSE_PUBLIC_KEY = "_izLpBoUKYz9rwClq1WIJFz5DrmISEbyG1esLEwK-ms";
```

4. Save the file

---

## Phase 6: Deploy Web Application (Vercel)

### 6.1 Push Code to GitHub

```bash
# Commit all changes (make sure .env.local is in .gitignore!)
git add .
git commit -m "Production deployment configuration"
git push origin main
```

### 6.2 Connect Vercel to GitHub

1. Go to https://vercel.com/new
2. Import your GitHub repository
3. Configure project:
   - **Framework Preset**: Next.js
   - **Root Directory**: `voicelite-web`
   - **Build Command**: `npm run build` (default)
   - **Output Directory**: `.next` (default)

### 6.3 Add Environment Variables

In Vercel dashboard, go to Settings → Environment Variables

Add ALL variables from `.env.production.template`:

**Database**:
- `DATABASE_URL`

**Ed25519 Keys**:
- `LICENSE_SIGNING_PRIVATE_B64`
- `LICENSE_SIGNING_PUBLIC_B64`
- `CRL_SIGNING_PRIVATE_B64`
- `CRL_SIGNING_PUBLIC_B64`

**Stripe**:
- `STRIPE_SECRET_KEY`
- `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY`
- `STRIPE_WEBHOOK_SECRET` (get after first deployment)
- `STRIPE_PRICE_QUARTERLY`
- `STRIPE_PRICE_LIFETIME`

**Resend**:
- `RESEND_API_KEY`
- `RESEND_FROM_EMAIL`

**Upstash**:
- `UPSTASH_REDIS_REST_URL`
- `UPSTASH_REDIS_REST_TOKEN`

**App URLs**:
- `NEXT_PUBLIC_APP_URL` = `https://app.voicelite.com`
- `NEXT_PUBLIC_DOWNLOAD_URL` = `https://voicelite.com/download`
- `MAGIC_LINK_APP_DEEP_LINK` = `voicelite://auth/callback`

### 6.4 Configure Custom Domain

1. Go to Settings → Domains
2. Add domain: `app.voicelite.com`
3. Configure DNS:
   - **Type**: CNAME
   - **Name**: app
   - **Value**: cname.vercel-dns.com

### 6.5 Deploy

1. Click "Deploy"
2. Wait for build to complete (~2-3 minutes)
3. Verify deployment at https://app.voicelite.com

### 6.6 Complete Stripe Webhook Setup

Now that app is deployed, go back to **Phase 2.3** and configure Stripe webhook

---

## Phase 7: Build Desktop Client

### 7.1 Build Release Version

From Windows machine with .NET 8 SDK:

```bash
cd VoiceLite/VoiceLite

# Publish self-contained release
dotnet publish VoiceLite.csproj -c Release -r win-x64 --self-contained

# Output location:
# VoiceLite/VoiceLite/bin/Release/net8.0-windows/win-x64/publish/
```

### 7.2 Verify Public Key

Check that `LicenseService.cs` has the correct production public key:
```csharp
private const string LICENSE_PUBLIC_KEY = "_izLpBoUKYz9rwClq1WIJFz5DrmISEbyG1esLEwK-ms";
```

### 7.3 Build Installer

```bash
# Run Inno Setup compiler
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "VoiceLite/Installer/VoiceLiteSetup_Simple.iss"

# Output: VoiceLite-Setup-{version}.exe in root directory
```

### 7.4 Test Installer

1. Install on a test Windows machine
2. Verify app launches
3. Test license activation with a real license key

---

## Phase 8: Testing & Verification

### 8.1 Test Authentication Flow

1. Go to https://app.voicelite.com
2. Click "Sign In" or "Get Started"
3. Enter your email address
4. Check email for magic link
5. Click magic link → should redirect and log you in
6. Verify /api/me returns your user data

**Alternative**: Test OTP flow
1. Request magic link
2. Check email for 8-digit OTP code
3. Enter OTP code in app
4. Verify login works

### 8.2 Test Checkout Flow (Stripe Test Mode First!)

**Switch Stripe to Test Mode**:
1. In Vercel, temporarily change:
   - `STRIPE_SECRET_KEY` → `sk_test_...`
   - `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` → `pk_test_...`
2. Redeploy

**Test Checkout**:
1. Log in to app
2. Go to pricing/upgrade page
3. Click "Subscribe to Quarterly" or "Buy Lifetime"
4. Use Stripe test card: `4242 4242 4242 4242`, any future date, any CVC
5. Complete checkout
6. Verify redirect to success page
7. Check Stripe dashboard → Payment should appear
8. Check database → License should be created
9. Check email → License email should be sent

### 8.3 Test Desktop Client License Activation

1. Download installer from your download page
2. Install VoiceLite on Windows machine
3. Launch app
4. Log in with your account (magic link or OTP)
5. App should automatically activate license
6. Verify in /api/me → activations array should have entry
7. Test transcription to ensure app fully works

### 8.4 Test Webhook Processing

1. In Stripe dashboard, go to Webhooks
2. Click on your webhook endpoint
3. Click "Send test webhook"
4. Choose event: `checkout.session.completed`
5. Check Vercel logs → Should see webhook processed
6. Check database → Should see WebhookEvent entry

### 8.5 Switch to Live Mode

**When ready for production**:
1. In Vercel, switch back to live keys:
   - `STRIPE_SECRET_KEY` → `sk_live_...`
   - `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` → `pk_live_...`
2. Redeploy
3. Test ONE real payment with your own card
4. Verify everything works end-to-end

---

## Phase 9: Launch & Monitoring

### 9.1 Pre-Launch Checklist

- [ ] All environment variables set correctly
- [ ] Database migrations applied
- [ ] Stripe webhook configured and tested
- [ ] Email sending working (check spam folder)
- [ ] Desktop client built with correct public key
- [ ] Test payment completed successfully
- [ ] License activation tested from desktop client
- [ ] DNS records configured for app.voicelite.com
- [ ] SSL certificate valid (Vercel provides automatically)
- [ ] Error tracking configured (optional: Sentry)

### 9.2 Launch

1. Announce availability
2. Monitor Vercel logs for first 1 hour
3. Check error rates in dashboard

### 9.3 Post-Launch Monitoring (First 48 Hours)

**Check every 2-4 hours**:
- [ ] Vercel error logs (Functions tab)
- [ ] Stripe payments dashboard
- [ ] Database query performance (Supabase dashboard)
- [ ] Email delivery rates (Resend dashboard)
- [ ] Redis usage (Upstash dashboard)

**Watch for**:
- Authentication failures
- Payment failures
- Webhook processing errors
- Email delivery failures
- High rate limit hits
- Desktop client activation failures

### 9.4 Common Issues & Fixes

**Issue**: Users not receiving emails
- Check Resend dashboard for bounces/spam
- Verify DKIM records in DNS
- Check spam folder
- Verify RESEND_FROM_EMAIL matches verified domain

**Issue**: Stripe webhook failing
- Verify STRIPE_WEBHOOK_SECRET matches Stripe dashboard
- Check webhook signature verification logs
- Ensure webhook URL is correct: https://app.voicelite.com/api/webhook

**Issue**: Desktop client can't activate license
- Verify LICENSE_PUBLIC_KEY in LicenseService.cs matches LICENSE_SIGNING_PUBLIC_B64 in .env
- Check /api/licenses/issue endpoint returns signed license
- Check desktop client can reach https://app.voicelite.com

**Issue**: Environment validation errors on build
- This is expected during build - validation only runs at runtime
- Verify all required vars are set in Vercel environment variables

---

## Phase 10: Ongoing Maintenance

### Database Backups
- Supabase automatically backs up daily
- Can enable Point-in-Time Recovery for critical data
- Test restore process quarterly

### Key Rotation
- Rotate Ed25519 keys annually or if compromised
- Process:
  1. Generate new keypair
  2. Deploy new public key to desktop clients (requires update)
  3. Keep old private key for validating existing licenses
  4. Use new private key for issuing new licenses

### Stripe Subscription Management
- Monitor failed payments
- Handle subscription cancellations
- Update license status based on webhook events

### Security Updates
- Review SECURITY_FIXES_APPLIED.md quarterly
- Update dependencies: `npm audit fix`
- Monitor security advisories for Stripe, Supabase, Next.js

---

## Emergency Rollback

If critical issue discovered post-deployment:

1. **Immediate**: Revert to previous Vercel deployment
   - Go to Deployments tab
   - Click on last working deployment
   - Click "Promote to Production"

2. **Desktop Client**: Temporarily disable license requirement
   - Option A: Issue universal license key
   - Option B: Push hotfix build with license check disabled

3. **Database**: Restore from Supabase backup
   - Go to Database → Backups
   - Select restore point
   - Restore to new database (test first!)

---

## Support & Troubleshooting

**Logs**:
- Vercel: https://vercel.com/dashboard → Functions → Logs
- Supabase: Project dashboard → Logs
- Stripe: Dashboard → Developers → Logs

**Documentation**:
- Next.js: https://nextjs.org/docs
- Prisma: https://www.prisma.io/docs
- Stripe: https://stripe.com/docs/api

**Get Help**:
- VoiceLite issues: GitHub Issues
- Vercel support: https://vercel.com/support
- Stripe support: https://support.stripe.com

---

## Appendix: Environment Variable Reference

See `.env.production.template` for complete list with examples.

**Critical Variables** (app won't start without these):
- DATABASE_URL
- LICENSE_SIGNING_PRIVATE_B64
- LICENSE_SIGNING_PUBLIC_B64
- UPSTASH_REDIS_REST_URL
- UPSTASH_REDIS_REST_TOKEN

**Required for Full Functionality**:
- STRIPE_SECRET_KEY
- STRIPE_WEBHOOK_SECRET
- RESEND_API_KEY
- STRIPE_PRICE_QUARTERLY
- STRIPE_PRICE_LIFETIME

**Optional**:
- Analytics keys
- Monitoring tools
- Feature flags

---

**Deployment Guide Version**: 1.0
**Last Updated**: 2025-01-XX
**Author**: VoiceLite Development Team

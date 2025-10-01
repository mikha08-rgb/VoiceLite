# VoiceLite Production Deployment Guide

Complete step-by-step guide to deploy VoiceLite authentication, licensing, and payment system to production.

## Prerequisites

- Node.js 18+ installed
- Accounts created:
  - [Supabase](https://supabase.com) (PostgreSQL database)
  - [Stripe](https://stripe.com) (payments)
  - [Resend](https://resend.com) (email)
  - [Upstash](https://upstash.com) (Redis for rate limiting)
  - [Vercel](https://vercel.com) (hosting)

---

## Phase 1: Generate Cryptographic Keys

### 1.1 Generate Ed25519 Keypairs

```bash
cd voicelite-web
npm run keygen
```

**Output:**
```
LICENSE_SIGNING_PRIVATE_B64="..."
LICENSE_SIGNING_PUBLIC_B64="..."
CRL_SIGNING_PRIVATE_B64="..."
CRL_SIGNING_PUBLIC_B64="..."
```

⚠️ **CRITICAL**: Save the **public keys** - you'll need `LICENSE_SIGNING_PUBLIC_B64` for the desktop client.

📝 **Note**: Keep private keys secret! Never commit them to git.

---

## Phase 2: Database Setup (Supabase)

### 2.1 Create Supabase Project

1. Go to [Supabase Dashboard](https://app.supabase.com)
2. Click "New Project"
3. Choose organization and region (pick closest to your users)
4. Set database password (save it!)
5. Wait for project provisioning (~2 minutes)

### 2.2 Get Database Connection String

1. Go to Project Settings → Database
2. Copy the connection string under "Connection string" → "URI"
3. Replace `[YOUR-PASSWORD]` with your database password
4. Save as `DATABASE_URL` in `.env.local`

**Format:**
```
DATABASE_URL="postgresql://postgres:PASSWORD@db.PROJECT-REF.supabase.co:5432/postgres"
```

### 2.3 Run Database Migrations

```bash
cd voicelite-web
npm run db:migrate
```

This creates all tables: User, Session, License, LicenseActivation, Product, Purchase, LicenseEvent, WebhookEvent, ApiKey.

### 2.4 Seed Products

```bash
npm run db:seed
```

This creates:
- `voicelite-pro` (quarterly subscription)
- `voicelite-lifetime` (one-time purchase)

---

## Phase 3: Stripe Setup (Payments)

### 3.1 Get API Keys

1. Go to [Stripe Dashboard](https://dashboard.stripe.com)
2. Navigate to Developers → API keys
3. Copy:
   - **Publishable key** → `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY`
   - **Secret key** → `STRIPE_SECRET_KEY`

⚠️ Use **test keys** (pk_test_... / sk_test_...) first, then switch to live keys when ready.

### 3.2 Create Products & Prices

#### Option A: Via Stripe Dashboard (Recommended)

1. Go to Products → Add product

**Product 1: VoiceLite Pro (Quarterly)**
- Name: `VoiceLite Pro`
- Description: `Professional speech-to-text with premium features`
- Pricing:
  - Model: `Recurring`
  - Price: `$20 USD`
  - Billing period: `Every 3 months`
- Copy the **Price ID** (starts with `price_...`) → `STRIPE_PRICE_QUARTERLY`

**Product 2: VoiceLite Lifetime**
- Name: `VoiceLite Lifetime`
- Description: `Lifetime access to VoiceLite Pro`
- Pricing:
  - Model: `One time`
  - Price: `$99 USD`
- Copy the **Price ID** (starts with `price_...`) → `STRIPE_PRICE_LIFETIME`

#### Option B: Via Stripe CLI

```bash
# Create quarterly subscription price
stripe prices create \
  --unit-amount=2000 \
  --currency=usd \
  --recurring[interval]=month \
  --recurring[interval_count]=3 \
  --product-data[name]="VoiceLite Pro"

# Create lifetime price
stripe prices create \
  --unit-amount=9900 \
  --currency=usd \
  --product-data[name]="VoiceLite Lifetime"
```

### 3.3 Setup Webhook Endpoint

**After deploying to Vercel** (Phase 6):

1. Go to Developers → Webhooks
2. Click "Add endpoint"
3. Endpoint URL: `https://app.voicelite.com/api/webhook`
4. Events to listen to:
   - `checkout.session.completed`
   - `invoice.paid`
   - `invoice.payment_failed`
   - `customer.subscription.deleted`
   - `charge.refunded`
5. Copy **Signing secret** → `STRIPE_WEBHOOK_SECRET`

---

## Phase 4: Email Setup (Resend)

### 4.1 Get API Key

1. Go to [Resend Dashboard](https://resend.com)
2. Navigate to API Keys
3. Create new API key
4. Copy API key → `RESEND_API_KEY`

### 4.2 Verify Domain (Optional but Recommended)

1. Go to Domains → Add Domain
2. Add your domain (e.g., `voicelite.com`)
3. Add DNS records as instructed
4. Wait for verification (~5 minutes)
5. Set `RESEND_FROM_EMAIL="VoiceLite <noreply@voicelite.com>"`

**Without verified domain:** Use `RESEND_FROM_EMAIL="VoiceLite <onboarding@resend.dev>"`

---

## Phase 5: Redis Setup (Upstash)

### 5.1 Create Redis Database

1. Go to [Upstash Console](https://console.upstash.com/redis)
2. Click "Create database"
3. Name: `voicelite-ratelimit`
4. Region: Choose closest to your users
5. Type: Regional (free tier works fine)
6. Click "Create"

### 5.2 Get Connection Details

1. Click on your database
2. Go to "REST API" tab
3. Copy:
   - **UPSTASH_REDIS_REST_URL** → `UPSTASH_REDIS_URL`
   - **UPSTASH_REDIS_REST_TOKEN** → `UPSTASH_REDIS_TOKEN`

---

## Phase 6: Deploy to Vercel

### 6.1 Push to GitHub

```bash
cd voicelite-web
git add .
git commit -m "Production-ready licensing system"
git push origin main
```

### 6.2 Deploy to Vercel

1. Go to [Vercel Dashboard](https://vercel.com)
2. Click "Add New" → "Project"
3. Import your GitHub repository
4. Configure project:
   - **Framework Preset**: Next.js
   - **Root Directory**: `voicelite-web`
   - **Build Command**: `npm run build`
   - **Output Directory**: `.next`

### 6.3 Add Environment Variables

In Vercel project settings → Environment Variables, add ALL variables from `.env.local`:

```
DATABASE_URL=postgresql://...
STRIPE_SECRET_KEY=sk_live_...
NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY=pk_live_...
STRIPE_WEBHOOK_SECRET=whsec_...
STRIPE_PRICE_QUARTERLY=price_...
STRIPE_PRICE_LIFETIME=price_...
RESEND_API_KEY=re_...
RESEND_FROM_EMAIL=VoiceLite <noreply@voicelite.com>
UPSTASH_REDIS_URL=https://...
UPSTASH_REDIS_TOKEN=...
LICENSE_SIGNING_PRIVATE_B64=...
LICENSE_SIGNING_PUBLIC_B64=...
CRL_SIGNING_PRIVATE_B64=...
CRL_SIGNING_PUBLIC_B64=...
NEXT_PUBLIC_APP_URL=https://app.voicelite.com
```

### 6.4 Deploy

Click "Deploy" and wait for build to complete (~2 minutes).

### 6.5 Setup Custom Domain (Optional)

1. Go to Settings → Domains
2. Add domain: `app.voicelite.com`
3. Configure DNS as instructed

---

## Phase 7: Update Desktop Client

### 7.1 Add Public Key to Desktop Client

Open `VoiceLite/VoiceLite/Services/Licensing/LicenseService.cs`:

```csharp
private const string LICENSE_PUBLIC_KEY = "A8aHG17W1d2u6uMU3bomtJGM12Gr897zGhoKVDM9rUQ";
```

Replace with your **LICENSE_SIGNING_PUBLIC_B64** value from Phase 1.

### 7.2 Update API Base URL

Open `VoiceLite/VoiceLite/Services/Auth/ApiClient.cs`:

```csharp
BaseAddress = new Uri(Environment.GetEnvironmentVariable("VOICELITE_API_BASE_URL")
                       ?? "https://app.voicelite.com"),
```

Change default from `https://app.voicelite.com` to your production URL if different.

### 7.3 Build Desktop Client

```bash
cd VoiceLite
dotnet publish VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained
```

### 7.4 Build Installer

```bash
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" VoiceLite/Installer/VoiceLiteSetup_Simple.iss
```

Output: `VoiceLite-Setup-1.0.X.exe`

---

## Phase 8: Testing

### 8.1 Test Authentication Flow

1. Open desktop app
2. Click "Account" button
3. Request magic link with your email
4. Check email inbox
5. Enter OTP code
6. Verify sign-in successful

### 8.2 Test Checkout Flow

1. While signed in, visit: `https://app.voicelite.com`
2. Click "Upgrade to Pro" or similar CTA
3. Complete Stripe checkout (use test card: `4242 4242 4242 4242`)
4. Verify redirect to success page
5. Check desktop app shows "Pro license active"

### 8.3 Test License Validation

1. Close and reopen desktop app
2. Should show "Pro license active (offline)" immediately
3. Verify works without internet connection
4. Reconnect internet - should sync and show "Pro license active"

### 8.4 Test CRL

1. In Supabase, manually revoke a license:
```sql
UPDATE "License"
SET status = 'REVOKED'
WHERE id = 'your-license-id';
```
2. Restart desktop app
3. Should detect revocation and show appropriate message

---

## Phase 9: Go Live

### 9.1 Switch to Live Stripe Keys

1. In Vercel environment variables, update:
   - `STRIPE_SECRET_KEY` → use `sk_live_...`
   - `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` → use `pk_live_...`
   - `STRIPE_PRICE_QUARTERLY` → use live price ID
   - `STRIPE_PRICE_LIFETIME` → use live price ID
2. Redeploy application

### 9.2 Enable Stripe Webhook

1. Create webhook in live mode (not test mode)
2. Update `STRIPE_WEBHOOK_SECRET` with live webhook secret
3. Redeploy

### 9.3 Monitor

- **Vercel**: Check deployment logs
- **Supabase**: Monitor database queries
- **Stripe**: Watch for payments
- **Resend**: Check email deliverability
- **Upstash**: Monitor Redis operations

---

## Troubleshooting

### "Invalid signature" on Stripe webhook

- Verify `STRIPE_WEBHOOK_SECRET` matches webhook in Stripe Dashboard
- Check webhook URL is exactly `https://yourdomain.com/api/webhook`
- Ensure no trailing slashes

### Emails not sending

- Verify Resend API key is valid
- Check domain is verified in Resend
- Look for bounces/errors in Resend dashboard

### Rate limit errors

- Check Upstash Redis is accessible
- Verify `UPSTASH_REDIS_URL` and `UPSTASH_REDIS_TOKEN` are correct
- Check Redis has free operations remaining

### Desktop client "License invalid"

- Verify `LICENSE_PUBLIC_KEY` in desktop client matches backend
- Check license was issued for correct device fingerprint
- Look for errors in `%APPDATA%\VoiceLite\logs\voicelite.log`

### Database connection errors

- Verify `DATABASE_URL` is correct
- Check Supabase project is not paused
- Ensure IP allowlist includes your deployment (Vercel uses dynamic IPs - allow all)

---

## Security Checklist

- [ ] Private keys never committed to git
- [ ] `.env.local` in `.gitignore`
- [ ] Stripe webhook secret configured
- [ ] HTTPS enabled on all endpoints
- [ ] Database connection uses SSL
- [ ] Rate limiting configured
- [ ] CSRF protection enabled (SameSite=strict cookies)
- [ ] Email verification required for auth

---

## Maintenance

### Rotate Signing Keys

1. Generate new keypair: `npm run keygen`
2. Update environment variables with new private keys
3. Keep `key_version` field to support old licenses
4. Update desktop client with new public key
5. Release new desktop version

### Backup Database

```bash
# Via Supabase Dashboard: Settings → Database → Backup Now
# Or via pg_dump:
pg_dump $DATABASE_URL > backup.sql
```

### Monitor CRL Size

If CRL grows large (>1000 revoked licenses), consider pagination or archive old revocations.

---

## Support

For issues, check:
1. Vercel deployment logs
2. Supabase database logs
3. Desktop client logs: `%APPDATA%\VoiceLite\logs\voicelite.log`
4. Stripe webhook logs
5. Resend email logs

---

**Deployment complete! 🎉**

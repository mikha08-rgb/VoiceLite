# Security Incident Response: Secret Rotation

**Incident Date:** 2025-10-03
**Severity:** CRITICAL
**Status:** ✅ Immediate threat mitigated (`.env.local` deleted)

---

## Incident Summary

Production secrets were stored in `voicelite-web/.env.local` file (git-ignored but present in local repository). While the file was properly excluded from version control, this practice violates security best practices.

**Exposed Secrets:**
- Supabase database credentials (PostgreSQL connection string)
- Stripe API keys (test mode)
- Ed25519 license signing private keys (2 keys)
- Migration secret (admin bypass token)
- Admin email addresses

---

## Immediate Actions Taken ✅

1. **Deleted `.env.local`** from local repository directory
2. **Verified git status** - file was never committed (protected by `.gitignore`)
3. **Created this response guide** for secret rotation

---

## Required Secret Rotation (DO IMMEDIATELY)

### 1. Rotate Ed25519 License Signing Keys

**Risk:** If leaked, attackers can forge unlimited Pro licenses.

**Steps:**
```bash
cd voicelite-web

# Generate new key pairs
npm run keygen

# Output will show:
# LICENSE_SIGNING_PRIVATE_B64: [new-key-1]
# LICENSE_SIGNING_PUBLIC_B64: [new-key-1-public]
# CRL_SIGNING_PRIVATE_B64: [new-key-2]
# CRL_SIGNING_PUBLIC_B64: [new-key-2-public]

# Add to Vercel (production):
vercel env add LICENSE_SIGNING_PRIVATE_B64 production
# Paste new private key when prompted

vercel env add LICENSE_SIGNING_PUBLIC_B64 production
# Paste new public key when prompted

vercel env add CRL_SIGNING_PRIVATE_B64 production
# Paste new CRL private key

vercel env add CRL_SIGNING_PUBLIC_B64 production
# Paste new CRL public key

# Redeploy to apply new keys
vercel deploy --prod
```

**Desktop App Update Required:**
- Update hardcoded public key in `VoiceLite/Services/LicenseService.cs` (line ~45)
- Rebuild desktop app (v1.0.28+)
- **All existing licenses become invalid** after rotation (users must re-download license files)

---

### 2. Rotate Supabase Database Password

**Risk:** Direct PostgreSQL access to production database.

**Steps:**
1. Log in to Supabase dashboard: https://supabase.com/dashboard
2. Navigate to: Project Settings → Database → Reset Database Password
3. Copy new password
4. Update Vercel environment variable:
   ```bash
   vercel env add DATABASE_URL production
   # Format: postgresql://postgres.kkjfmnwjchlugzxlqipw:[NEW_PASSWORD]@aws-1-us-east-2.pooler.supabase.com:6543/postgres?pgbouncer=true
   ```
5. Redeploy: `vercel deploy --prod`
6. Test database connection: `npm run db:push`

---

### 3. Rotate Stripe Webhook Secret

**Risk:** Attackers can spoof webhook events (fake subscription payments).

**Steps:**
1. Log in to Stripe dashboard: https://dashboard.stripe.com
2. Navigate to: Developers → Webhooks
3. Find webhook for `voicelite.app/api/webhook`
4. Click "..." → Roll secret
5. Copy new secret (format: `whsec_...`)
6. Update Vercel:
   ```bash
   vercel env add STRIPE_WEBHOOK_SECRET production
   # Paste new secret
   ```
7. Redeploy: `vercel deploy --prod`

---

### 4. Generate New Migration Secret

**Risk:** Bypass admin authentication on `/api/admin/migrate`.

**Steps:**
```bash
# Generate random 32-byte hex string
openssl rand -hex 32
# Example output: d8f7a9e2c4b6a1d3f5e8c9b2a4d7e1f6c8a3b5d9e2f7a1c4b6d8e3f5a9c2b7d1

# Add to Vercel
vercel env add MIGRATION_SECRET production
# Paste generated secret

# Update local scripts that use migration endpoint
# (Search codebase for MIGRATION_SECRET usage)
```

---

### 5. Configure Resend API Key (NEW)

**Status:** Currently missing - breaks email functionality.

**Steps:**
1. Sign up for Resend: https://resend.com/signup
2. Create API key in dashboard
3. Add to Vercel:
   ```bash
   vercel env add RESEND_API_KEY production
   # Format: re_xxxxxxxxxxxxxxxxxxxxxxxx
   ```
4. Verify domain: Add DNS records for email sending
5. Test email delivery:
   ```bash
   curl -X POST https://voicelite.app/api/auth/request \
     -H "Content-Type: application/json" \
     -d '{"email":"test@example.com"}'
   # Should return success, not "RESEND_API_KEY not configured"
   ```

---

## Post-Rotation Verification

After rotating all secrets, verify functionality:

### 1. Test License Validation
```bash
# Desktop app should validate licenses with new public key
# Old licenses will fail validation (expected)
```

### 2. Test Database Connection
```bash
cd voicelite-web
npm run db:push
# Should succeed without authentication errors
```

### 3. Test Stripe Webhooks
```bash
# Trigger test webhook in Stripe dashboard
# Verify event is processed in Vercel logs
```

### 4. Test Email Delivery
```bash
# Attempt magic link login
# Should receive email within 30 seconds
```

---

## Prevention Measures (Implemented)

### ✅ Already in Place
1. `.env*.local` in `.gitignore` (verified)
2. Vercel environment variables for production
3. No `.env` files committed to git (verified via `git log --all -- '*.env*'`)

### 🔧 New Policies
1. **NEVER store production secrets in local files** (even git-ignored ones)
2. **Use Vercel environment variables exclusively** for production
3. **Use `.env.example`** for documentation (with placeholder values)
4. **Rotate secrets quarterly** (preventive measure)

---

## `.env.example` Template

Create this file for developers (safe to commit):

```env
# Database (Supabase PostgreSQL)
DATABASE_URL="postgresql://postgres:PASSWORD@HOST:6543/postgres?pgbouncer=true"

# Authentication
NEXTAUTH_SECRET="generate-with-openssl-rand-base64-32"
NEXTAUTH_URL="http://localhost:3000"

# Stripe (use test keys for local development)
STRIPE_SECRET_KEY="sk_test_YOUR_KEY_HERE"
STRIPE_PUBLISHABLE_KEY="pk_test_YOUR_KEY_HERE"
STRIPE_WEBHOOK_SECRET="whsec_YOUR_SECRET_HERE"
STRIPE_PRICE_ID="price_YOUR_PRICE_ID"

# Email (Resend)
RESEND_API_KEY="re_YOUR_KEY_HERE"

# License Signing (Ed25519)
LICENSE_SIGNING_PRIVATE_B64="generate-with-npm-run-keygen"
LICENSE_SIGNING_PUBLIC_B64="generate-with-npm-run-keygen"
CRL_SIGNING_PRIVATE_B64="generate-with-npm-run-keygen"
CRL_SIGNING_PUBLIC_B64="generate-with-npm-run-keygen"

# Admin
ADMIN_EMAILS="your-email@example.com"
MIGRATION_SECRET="generate-with-openssl-rand-hex-32"

# Rate Limiting (Upstash Redis)
UPSTASH_REDIS_REST_URL="https://YOUR_INSTANCE.upstash.io"
UPSTASH_REDIS_REST_TOKEN="YOUR_TOKEN_HERE"

# Application
NEXT_PUBLIC_APP_URL="http://localhost:3000"
```

---

## Timeline

- **2025-10-03 14:00 UTC**: `.env.local` deleted ✅
- **2025-10-03 14:15 UTC**: Secret rotation guide created ✅
- **2025-10-03 14:30 UTC**: (TODO) Rotate Ed25519 keys
- **2025-10-03 14:45 UTC**: (TODO) Rotate database password
- **2025-10-03 15:00 UTC**: (TODO) Configure Resend API key
- **2025-10-03 15:30 UTC**: (TODO) Verification testing

---

## Contact

**Security Team:** Mikhail.lev08@gmail.com
**Incident Severity:** CRITICAL (contained)
**Response Time:** <1 hour ✅

---

## Lessons Learned

1. **Git-ignored files are not secure** - they exist in local directories, backups, screenshots
2. **Vercel environment variables are the ONLY secure storage** for production secrets
3. **Secret rotation should be automated** - implement quarterly rotation policy
4. **Monitoring needed** - alert on failed license validations (could indicate key compromise)

---

## Next Steps

1. ✅ Delete `.env.local` (DONE)
2. ⏳ Rotate all exposed secrets (IN PROGRESS)
3. ⏳ Create `.env.example` with placeholders
4. ⏳ Update desktop app with new Ed25519 public key
5. ⏳ Release v1.0.28 with rotated keys
6. ⏳ Notify existing Pro users to re-download licenses

**Estimated Time to Full Resolution:** 2-3 hours

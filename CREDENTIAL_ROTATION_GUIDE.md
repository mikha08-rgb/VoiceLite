# Credential Rotation Guide - VoiceLite

**Date**: 2025-10-13
**Status**: Ready to execute
**Duration**: 1-2 hours

---

## ✅ What's Done

- [x] Git history scrubbed (secrets replaced with `***REMOVED***`)
- [x] New Ed25519 keypairs generated
- [x] New migration secret generated
- [x] Credentials saved to: `NEW_CREDENTIALS_2025-10-13T03-20-07-008Z.txt`

---

## 📋 Step-by-Step Rotation Process

### Phase 1: Add New Keys to Vercel (20 minutes)

**Navigate to voicelite-web directory:**
```bash
cd voicelite-web
```

**Add each credential to Vercel production:**

```bash
# License signing keys
echo "MC4CAQAwBQYDK2VwBCIEIFYRlsMSPSdSthPe7qxK/VGJOGGhtrwtwcJdSt6FyqqQ" | vercel env add LICENSE_SIGNING_PRIVATE_B64 production

echo "MCowBQYDK2VwAyEABVJcMaWK7CVweHgNoRo67Zvpg7ejqrXSQwY4buglHmw=" | vercel env add LICENSE_SIGNING_PUBLIC_B64 production

# CRL signing keys
echo "MC4CAQAwBQYDK2VwBCIEIOc4ZAHrjfZ5yC1yFs9PQ3l7obHahI0fVN/id49V9Zfx" | vercel env add CRL_SIGNING_PRIVATE_B64 production

echo "MCowBQYDK2VwAyEAWXaL3lNxtkwkv9wNXmDEcRdIBAztm1kG+ReaDEpwGz4=" | vercel env add CRL_SIGNING_PUBLIC_B64 production

# Migration secret
echo "10e6bda2ff0df5834eeb4515adbaa33f8234c7d48bd040f7f622130fd9e991cf" | vercel env add MIGRATION_SECRET production
```

**Verify credentials were added:**
```bash
vercel env ls
```

Should show:
- LICENSE_SIGNING_PRIVATE_B64 (production)
- LICENSE_SIGNING_PUBLIC_B64 (production)
- CRL_SIGNING_PRIVATE_B64 (production)
- CRL_SIGNING_PUBLIC_B64 (production)
- MIGRATION_SECRET (production)

---

### Phase 2: Rotate Database Password (15 minutes)

**Why**: Old password was in deleted files

**Steps**:

1. Log in to Supabase: https://supabase.com/dashboard
2. Navigate to: **Project Settings → Database**
3. Click: **Reset Database Password**
4. Copy the new password
5. Update Vercel:

```bash
# Replace [NEW_PASSWORD] with actual password
echo "postgresql://postgres.kkjfmnwjchlugzxlqipw:[NEW_PASSWORD]@aws-1-us-east-2.pooler.supabase.com:6543/postgres?pgbouncer=true" | vercel env add DATABASE_URL production
```

6. Test connection:

```bash
cd voicelite-web
npm run db:push
```

Expected: "Database schema is up to date"

---

### Phase 3: Rotate Stripe Webhook Secret (10 minutes)

**Why**: Old secret was in deleted files

**Steps**:

1. Log in to Stripe: https://dashboard.stripe.com
2. Navigate to: **Developers → Webhooks**
3. Find webhook for `voicelite.app/api/webhook`
4. Click **"..."** → **Roll secret**
5. Copy new secret (format: `whsec_...`)
6. Update Vercel:

```bash
# Replace with actual secret
echo "whsec_NEW_SECRET_HERE" | vercel env add STRIPE_WEBHOOK_SECRET production
```

---

### Phase 4: Configure Resend API Key (15 minutes)

**Status**: ❌ **MISSING** - Email functionality currently broken

**Steps**:

1. Sign up/login: https://resend.com
2. Create API key in dashboard
3. Add to Vercel:

```bash
echo "re_YOUR_KEY_HERE" | vercel env add RESEND_API_KEY production
```

4. Verify domain (add DNS records if needed)
5. Test email delivery:

```bash
curl -X POST https://voicelite.app/api/auth/request \
  -H "Content-Type: application/json" \
  -d '{"email":"your-email@example.com"}'
```

Expected: Email sent successfully

---

### Phase 5: Rotate Upstash Redis Token (10 minutes)

**Why**: Preventive security measure

**Steps**:

1. Log in to Upstash: https://console.upstash.com
2. Navigate to your Redis database
3. Click: **Details → REST API**
4. Click: **Rotate Token** (or create new token)
5. Copy new token
6. Update Vercel:

```bash
echo "YOUR_NEW_TOKEN" | vercel env add UPSTASH_REDIS_REST_TOKEN production
echo "https://your-db.upstash.io" | vercel env add UPSTASH_REDIS_REST_URL production
```

---

### Phase 6: Deploy to Production (5 minutes)

**Deploy with new credentials:**

```bash
cd voicelite-web
vercel deploy --prod
```

**Expected output**:
- Building...
- Deploying...
- ✓ Production deployment complete
- https://voicelite.app

**Verify deployment:**
```bash
curl https://voicelite.app/api/health
```

Expected: `{"status":"ok"}`

---

## 🧪 Testing Checklist

After deployment, test these critical flows:

### 1. License Validation

```bash
# Test with invalid key
curl -X POST https://voicelite.app/api/licenses/validate \
  -H "Content-Type: application/json" \
  -d '{"licenseKey":"VL-INVALID-123456-789ABC"}'
```

Expected: `{"valid":false,"error":"License not found"}`

### 2. Magic Link Authentication

```bash
curl -X POST https://voicelite.app/api/auth/request \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com"}'
```

Expected: `{"success":true,"message":"Magic link sent"}`

### 3. Stripe Webhook

1. Go to Stripe dashboard
2. Developers → Webhooks → voicelite.app webhook
3. Click **Send test webhook**
4. Select event: `checkout.session.completed`
5. Expected: **200 OK** response

---

## 🔐 Desktop App Update

**Good news**: Desktop app doesn't need updates!

The app calls the API for validation - server-side verification handles the new keys automatically.

**No code changes required** ✅

---

## 📊 Impact Assessment

### What Breaks After Deployment:

1. ❌ **All existing Pro licenses become invalid**
   - Users will see: "License validation failed"
   - Fix: Users must re-purchase or contact support

2. ❌ **CRL checks use new signing key**
   - Old CRL signatures won't validate
   - Fix: Regenerate CRL with new key (automatic on next check)

### What Keeps Working:

1. ✅ **Desktop app continues working** (no update needed)
2. ✅ **Free tier unchanged** (no licensing)
3. ✅ **Stripe payments work** (new webhook secret)
4. ✅ **Database access works** (new password)
5. ✅ **Rate limiting works** (new Redis token)

---

## 🚨 Rollback Plan (if needed)

If deployment fails:

1. **Revert Vercel deployment:**
```bash
cd voicelite-web
vercel rollback
```

2. **Check error logs:**
```bash
vercel logs voicelite.app --prod
```

3. **Verify environment variables:**
```bash
vercel env ls
```

---

## ✅ Success Criteria

Deployment is successful when:

- [ ] All 5 new Ed25519 credentials added to Vercel
- [ ] Database password rotated
- [ ] Stripe webhook secret rotated
- [ ] Resend API key configured (email works)
- [ ] Upstash Redis token rotated
- [ ] Production deployment completes without errors
- [ ] Health check returns `{"status":"ok"}`
- [ ] License validation API returns responses
- [ ] Magic link emails send successfully
- [ ] Stripe webhooks return 200 OK

---

## 📞 Support

**If issues occur:**

1. Check Vercel logs: `vercel logs voicelite.app --prod`
2. Check Supabase logs: https://supabase.com/dashboard
3. Check Stripe webhooks: https://dashboard.stripe.com/webhooks
4. Email: Mikhail.lev08@gmail.com

---

## 📁 Files to Keep/Delete

**Keep (in password manager):**
- ✅ `NEW_CREDENTIALS_2025-10-13T03-20-07-008Z.txt`

**Delete after deployment:**
- ❌ `generate-new-keys.js` (no longer needed)
- ❌ `secrets-to-redact.txt` (old secrets)
- ❌ `scrub.bat` (task complete)
- ❌ All `SIMPLE_COMMANDS.txt`, `QUICK_COMMANDS.txt`, etc. (task complete)

---

## 🎯 Next Steps

1. **Execute Phase 1-6** (follow steps above)
2. **Test all critical flows** (use testing checklist)
3. **Monitor for 24 hours** (check error logs)
4. **Notify Pro users** (licenses need reactivation)

**Estimated total time**: 1-2 hours

---

**Generated**: 2025-10-13
**Status**: Ready to execute
**Phase 1 (Git Scrubbing)**: ✅ Complete
**Phase 2 (Credential Rotation)**: ⏸️ Ready to start

# 🚀 Deploy New Secrets - Quick Reference

**Date:** 2025-10-03
**Status:** ✅ Secrets generated, ready for deployment

---

## ⚡ Quick Deploy (Copy-Paste Commands)

### Step 1: Add to Vercel (5 minutes)

```bash
cd voicelite-web

# License signing keys
echo "vS89Zv4vrDNoM9zXm5aAsba-FwFq_zb9maVey2V7L5k" | vercel env add LICENSE_SIGNING_PRIVATE_B64 production
echo "fRR5l40q-wt8ptAFcOGsWIBHtLDBjnb_T3Z9HMLwgCc" | vercel env add LICENSE_SIGNING_PUBLIC_B64 production

# CRL signing keys
echo "qmXC7vEDAK1XLsSHttTbAa_L71JDmJW_zeNcsPOhWZE" | vercel env add CRL_SIGNING_PRIVATE_B64 production
echo "19Y5ul1S-ISjja7f827O5epfupvaBBMyhb_uVWLLf8M" | vercel env add CRL_SIGNING_PUBLIC_B64 production

# Migration secret
echo "443ed3297b3a26ba4684129e59c72c6b6ce4a944344ef2579df2bdeba7d54210" | vercel env add MIGRATION_SECRET production
```

### Step 2: Deploy to Production

```bash
vercel deploy --prod
```

**Wait 2-3 minutes for deployment to complete.**

---

## 🔴 CRITICAL: Update Desktop App

### Edit LicenseService.cs

**File:** `VoiceLite/VoiceLite/Services/LicenseService.cs`

**Find line ~15-20 and replace:**

```csharp
// OLD (INVALID after rotation)
private const string LICENSE_PUBLIC_KEY_B64 = "[old-key]";

// NEW (MUST UPDATE)
private const string LICENSE_PUBLIC_KEY_B64 = "fRR5l40q-wt8ptAFcOGsWIBHtLDBjnb_T3Z9HMLwgCc";
```

### Rebuild and Release

```bash
cd VoiceLite
dotnet build VoiceLite.sln -c Release
dotnet publish VoiceLite/VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained

# Build installer (requires Inno Setup)
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" VoiceLite\Installer\VoiceLiteSetup_Simple.iss
```

**Important:** All existing Pro licenses become invalid. Users must re-download licenses.

---

## ⚠️ STILL REQUIRED (Manual Steps)

### 1. Configure Resend API (CRITICAL - Email Broken)

1. Sign up: https://resend.com
2. Create API key
3. Add to Vercel:
   ```bash
   vercel env add RESEND_API_KEY production
   # Paste your re_... key
   ```
4. Redeploy: `vercel deploy --prod`

**Test email:**
```bash
curl -X POST https://voicelite.app/api/auth/request \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com"}'
```

---

### 2. Rotate Supabase Password (Recommended)

1. https://supabase.com/dashboard → Project Settings → Database → Reset Password
2. Copy new password
3. Update Vercel:
   ```bash
   vercel env add DATABASE_URL production
   # Format: postgresql://postgres.kkjfmnwjchlugzxlqipw:[NEW_PASSWORD]@aws-1-us-east-2.pooler.supabase.com:6543/postgres?pgbouncer=true
   ```
4. Redeploy: `vercel deploy --prod`

---

### 3. Rotate Stripe Webhook (Recommended)

1. https://dashboard.stripe.com → Developers → Webhooks
2. Find `voicelite.app/api/webhook` → Roll secret
3. Update Vercel:
   ```bash
   vercel env add STRIPE_WEBHOOK_SECRET production
   # Paste whsec_... secret
   ```
4. Redeploy: `vercel deploy --prod`

---

## ✅ Checklist

- [ ] Add 5 environment variables to Vercel (Step 1)
- [ ] Deploy to production (Step 2)
- [ ] Update desktop app public key
- [ ] Build v1.0.28 installer
- [ ] Configure Resend API (CRITICAL)
- [ ] Rotate Supabase password (optional)
- [ ] Rotate Stripe webhook (optional)
- [ ] Test license validation
- [ ] Test email delivery

---

## 📄 Full Documentation

- **Complete Guide:** [voicelite-web/SECRET_ROTATION_COMPLETE.md](voicelite-web/SECRET_ROTATION_COMPLETE.md)
- **Security Incident:** [voicelite-web/SECURITY_INCIDENT_RESPONSE.md](voicelite-web/SECURITY_INCIDENT_RESPONSE.md)
- **Code Fixes:** [CODE_QUALITY_FIXES.md](CODE_QUALITY_FIXES.md)

---

**Generated:** 2025-10-03
**Time to Deploy:** ~15 minutes (automated) + ~30 minutes (manual Resend/DB/Stripe)

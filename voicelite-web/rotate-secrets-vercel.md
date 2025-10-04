# 🔄 Rotate Secrets in Vercel - Step-by-Step Guide

**Date:** 2025-10-03
**Project:** voicelite (https://voicelite.app)

---

## ⚠️ IMPORTANT: Manual Steps Required

Vercel CLI doesn't support piping secret values directly. You'll need to **manually copy-paste** each secret when prompted.

---

## 🔐 New Secrets to Add

Keep this window open - you'll copy-paste these values when Vercel prompts you.

### 1. LICENSE_SIGNING_PRIVATE_B64
```
***REMOVED***
```

### 2. LICENSE_SIGNING_PUBLIC_B64
```
***REMOVED***
```

### 3. CRL_SIGNING_PRIVATE_B64
```
***REMOVED***
```

### 4. CRL_SIGNING_PUBLIC_B64
```
***REMOVED***
```

### 5. MIGRATION_SECRET
```
***REMOVED***
```

---

## 📋 Step-by-Step Instructions

### Step 1: Remove Old Secrets

Open your terminal in the `voicelite-web` directory and run:

```bash
cd voicelite-web

# Remove old license signing keys
vercel env rm LICENSE_SIGNING_PRIVATE_B64 production
vercel env rm LICENSE_SIGNING_PUBLIC_B64 production

# Remove old CRL signing keys
vercel env rm CRL_SIGNING_PRIVATE_B64 production
vercel env rm CRL_SIGNING_PUBLIC_B64 production

# Remove old migration secret
vercel env rm MIGRATION_SECRET production
```

**Confirm each removal** by typing the variable name when prompted.

---

### Step 2: Add New Secrets

For each command below, **copy the value from above** and paste when prompted:

```bash
# Add LICENSE_SIGNING_PRIVATE_B64
vercel env add LICENSE_SIGNING_PRIVATE_B64 production
# Paste: ***REMOVED***

# Add LICENSE_SIGNING_PUBLIC_B64
vercel env add LICENSE_SIGNING_PUBLIC_B64 production
# Paste: ***REMOVED***

# Add CRL_SIGNING_PRIVATE_B64
vercel env add CRL_SIGNING_PRIVATE_B64 production
# Paste: ***REMOVED***

# Add CRL_SIGNING_PUBLIC_B64
vercel env add CRL_SIGNING_PUBLIC_B64 production
# Paste: ***REMOVED***

# Add MIGRATION_SECRET
vercel env add MIGRATION_SECRET production
# Paste: ***REMOVED***
```

---

### Step 3: Deploy to Production

```bash
vercel deploy --prod
```

**Wait 2-3 minutes** for deployment to complete.

---

### Step 4: Verify Deployment

Check that the deployment succeeded:

```bash
vercel env ls production | grep LICENSE
vercel env ls production | grep CRL
vercel env ls production | grep MIGRATION
```

You should see all 5 variables listed (values will show as "Encrypted").

---

## ✅ Checklist

- [ ] Removed 5 old environment variables
- [ ] Added 5 new environment variables
- [ ] Deployed to production
- [ ] Verified variables exist in Vercel
- [ ] **Next: Update desktop app public key** (see Step 2 in main guide)
- [ ] **Next: Configure Resend API** (CRITICAL - if not already done)

---

## 🔴 If Resend API Key is Missing

Check if RESEND_API_KEY exists and has a value:

```bash
vercel env ls production | grep RESEND_API_KEY
```

If it shows "Encrypted" but you're not sure if it has a real value, test email functionality:

```bash
curl -X POST https://voicelite.app/api/auth/request \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com"}'
```

If it fails with "RESEND_API_KEY not configured":

1. Sign up at https://resend.com
2. Create API key
3. Add to Vercel:
   ```bash
   vercel env rm RESEND_API_KEY production  # Remove empty one
   vercel env add RESEND_API_KEY production  # Add real key
   # Paste: re_... (your real Resend API key)
   ```
4. Redeploy: `vercel deploy --prod`

---

## 📞 Support

If you encounter issues:
- Vercel dashboard: https://vercel.com/mishas-projects-0509f3dc/voicelite/settings/environment-variables
- Check deployment logs: `vercel logs --prod`
- Full guide: [SECRET_ROTATION_COMPLETE.md](SECRET_ROTATION_COMPLETE.md)

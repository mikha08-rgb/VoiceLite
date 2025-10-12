# 🚀 Complete Production Deployment - Manual Steps

**Follow these steps in order. This will take about 30 minutes total.**

---

## Part 1: Update Vercel Secrets (15 minutes)

### Option A: Using Vercel Dashboard (RECOMMENDED - Easier)

1. **Go to:** https://vercel.com/mishas-projects-0509f3dc/voicelite/settings/environment-variables

2. **For each variable below, click "Edit" → Update value:**

   **LICENSE_SIGNING_PRIVATE_B64** → Replace with:
   ```
   vS89Zv4vrDNoM9zXm5aAsba-FwFq_zb9maVey2V7L5k
   ```

   **LICENSE_SIGNING_PUBLIC_B64** → Replace with:
   ```
   fRR5l40q-wt8ptAFcOGsWIBHtLDBjnb_T3Z9HMLwgCc
   ```

   **CRL_SIGNING_PRIVATE_B64** → Replace with:
   ```
   qmXC7vEDAK1XLsSHttTbAa_L71JDmJW_zeNcsPOhWZE
   ```

   **CRL_SIGNING_PUBLIC_B64** → Replace with:
   ```
   19Y5ul1S-ISjja7f827O5epfupvaBBMyhb_uVWLLf8M
   ```

   **MIGRATION_SECRET** → Replace with:
   ```
   443ed3297b3a26ba4684129e59c72c6b6ce4a944344ef2579df2bdeba7d54210
   ```

3. **Check RESEND_API_KEY:**
   - If it exists and has a value (shows encrypted): ✅ Skip to Part 2
   - If it's empty or missing: ⚠️ You MUST configure this (see Part 1B below)

---

### Part 1B: Configure Resend API (If Missing) - CRITICAL

**Check if you have Resend configured:**

Open terminal:
```bash
curl -X POST https://voicelite.app/api/auth/request \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com"}'
```

**If you get an error about RESEND_API_KEY:**

1. **Sign up at Resend:**
   - Go to: https://resend.com/signup
   - Create account with your email

2. **Create API Key:**
   - Dashboard → API Keys → Create API Key
   - Name it: "VoiceLite Production"
   - Copy the key (starts with `re_...`)

3. **Add to Vercel:**
   - Go to: https://vercel.com/mishas-projects-0509f3dc/voicelite/settings/environment-variables
   - Find **RESEND_API_KEY**
   - If exists: Click "Edit" and paste your new key
   - If missing: Click "Add New" → Name: `RESEND_API_KEY`, Value: `re_...`, Environment: Production

4. **Verify domain (optional but recommended):**
   - Resend Dashboard → Domains → Add Domain
   - Add DNS records for `voicelite.app`
   - This improves email deliverability

---

### Part 1C: Deploy Changes

After updating all secrets in Vercel dashboard:

1. **Redeploy from terminal:**
   ```bash
   cd voicelite-web
   vercel deploy --prod
   ```

2. **Wait for deployment** (2-3 minutes)

3. **Verify deployment:**
   ```bash
   curl -I https://voicelite.app
   # Should return: HTTP/2 200
   ```

---

## Part 2: Update Desktop App (10 minutes)

### Step 2A: Update Public Key in Code

1. **Open file:** `VoiceLite/VoiceLite/Services/LicenseService.cs`

2. **Find this line** (around line 15-20):
   ```csharp
   private const string LICENSE_PUBLIC_KEY_B64 = "...old key...";
   ```

3. **Replace with:**
   ```csharp
   private const string LICENSE_PUBLIC_KEY_B64 = "fRR5l40q-wt8ptAFcOGsWIBHtLDBjnb_T3Z9HMLwgCc";
   ```

4. **Save the file**

---

### Step 2B: Build and Test

```bash
cd VoiceLite

# Build solution
dotnet build VoiceLite.sln -c Release

# Run tests
dotnet test VoiceLite.Tests/VoiceLite.Tests.csproj
```

**Expected result:** 0 errors, 262 tests passing

---

### Step 2C: Create Installer (Optional - For Distribution)

```bash
# Publish self-contained executable
dotnet publish VoiceLite/VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained

# Build installer (requires Inno Setup installed)
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" VoiceLite\Installer\VoiceLiteSetup_Simple.iss
```

**Output:** `VoiceLite-Setup-1.0.28.exe` (or current version)

---

## Part 3: Final Verification (5 minutes)

### Test 1: Web Backend

```bash
# Test API is responding
curl https://voicelite.app/api/health

# Test email functionality (should succeed now with Resend configured)
curl -X POST https://voicelite.app/api/auth/request \
  -H "Content-Type: application/json" \
  -d '{"email":"mikhail.lev08@gmail.com"}'
```

**Expected:** Email sent successfully

---

### Test 2: Desktop App License Validation

1. **Run desktop app** (Debug or Release)
2. **Try to activate a Pro license** (if you have one)
3. **Expected result:** License validates successfully with new public key

**Note:** Old licenses from before rotation will be INVALID. Users must re-download licenses from https://voicelite.app

---

### Test 3: Check Vercel Environment Variables

```bash
cd voicelite-web
vercel env ls production | grep -E "(LICENSE|CRL|MIGRATION|RESEND)"
```

**Expected output:**
```
LICENSE_SIGNING_PRIVATE_B64       Encrypted    Production
LICENSE_SIGNING_PUBLIC_B64        Encrypted    Production
CRL_SIGNING_PRIVATE_B64           Encrypted    Production
CRL_SIGNING_PUBLIC_B64            Encrypted    Production
MIGRATION_SECRET                  Encrypted    Production
RESEND_API_KEY                    Encrypted    Production
```

---

## ✅ Production Deployment Checklist

### Vercel Backend
- [ ] Updated LICENSE_SIGNING_PRIVATE_B64
- [ ] Updated LICENSE_SIGNING_PUBLIC_B64
- [ ] Updated CRL_SIGNING_PRIVATE_B64
- [ ] Updated CRL_SIGNING_PUBLIC_B64
- [ ] Updated MIGRATION_SECRET
- [ ] Configured RESEND_API_KEY (if missing)
- [ ] Deployed to production (`vercel deploy --prod`)
- [ ] Verified deployment (https://voicelite.app returns 200)

### Desktop App
- [ ] Updated LICENSE_PUBLIC_KEY_B64 in LicenseService.cs
- [ ] Built solution (0 errors)
- [ ] Ran tests (262 passing)
- [ ] Created installer (optional)

### Testing
- [ ] Web API responds
- [ ] Email delivery works (Resend configured)
- [ ] License validation works with new key
- [ ] All environment variables present in Vercel

### Post-Deployment (If releasing v1.0.28)
- [ ] Tag release: `git tag v1.0.28 && git push --tags`
- [ ] Create GitHub release
- [ ] Upload installer to GitHub
- [ ] Update download link on website
- [ ] Notify Pro users to re-download licenses

---

## 🔴 Common Issues

### "RESEND_API_KEY not configured"
**Fix:** Go back to Part 1B and configure Resend

### "License validation failed"
**Fix:** Make sure desktop app has new public key (`fRR5l40q-wt8ptAFcOGsWIBHtLDBjnb_T3Z9HMLwgCc`)

### "Vercel deployment failed"
**Fix:** Check logs with `vercel logs --prod`

### Tests failing
**Fix:** Run `dotnet clean && dotnet build` and try again

---

## 📞 Need Help?

- **Vercel Dashboard:** https://vercel.com/mishas-projects-0509f3dc/voicelite
- **Check Logs:** `vercel logs --prod`
- **Email Issues:** Check Resend Dashboard: https://resend.com/emails

---

**Total Time:** ~30 minutes
**Difficulty:** Medium (requires manual copy-paste in Vercel dashboard)
**Status:** Ready to execute

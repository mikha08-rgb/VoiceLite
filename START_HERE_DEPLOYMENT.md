# 🚀 START HERE: Complete Production Deployment

**Last Updated:** 2025-10-03
**Estimated Time:** 30 minutes
**Status:** All secrets generated, ready for manual deployment

---

## 📋 Overview

You have **3 main tasks** to complete:

1. ✅ **Update Vercel secrets** (web backend) - 15 minutes
2. ✅ **Update desktop app** (license validation) - 10 minutes
3. ✅ **Test everything** - 5 minutes

---

## 🎯 TASK 1: Update Vercel Secrets (15 minutes)

### Step 1.1: Open Vercel Dashboard

**Click this link:** [Vercel Environment Variables](https://vercel.com/mishas-projects-0509f3dc/voicelite/settings/environment-variables)

You should see a list of environment variables.

---

### Step 1.2: Update 5 Variables

For each variable below:
1. Find it in the list
2. Click the "••• " menu on the right
3. Click "Edit"
4. Replace the value with the new one below
5. Click "Save"

---

#### Variable 1: LICENSE_SIGNING_PRIVATE_B64

**Find:** `LICENSE_SIGNING_PRIVATE_B64`
**Click:** Edit
**Replace value with:**
```
vS89Zv4vrDNoM9zXm5aAsba-FwFq_zb9maVey2V7L5k
```
**Click:** Save

---

#### Variable 2: LICENSE_SIGNING_PUBLIC_B64

**Find:** `LICENSE_SIGNING_PUBLIC_B64`
**Click:** Edit
**Replace value with:**
```
fRR5l40q-wt8ptAFcOGsWIBHtLDBjnb_T3Z9HMLwgCc
```
**Click:** Save

---

#### Variable 3: CRL_SIGNING_PRIVATE_B64

**Find:** `CRL_SIGNING_PRIVATE_B64`
**Click:** Edit
**Replace value with:**
```
qmXC7vEDAK1XLsSHttTbAa_L71JDmJW_zeNcsPOhWZE
```
**Click:** Save

---

#### Variable 4: CRL_SIGNING_PUBLIC_B64

**Find:** `CRL_SIGNING_PUBLIC_B64`
**Click:** Edit
**Replace value with:**
```
19Y5ul1S-ISjja7f827O5epfupvaBBMyhb_uVWLLf8M
```
**Click:** Save

---

#### Variable 5: MIGRATION_SECRET

**Find:** `MIGRATION_SECRET`
**Click:** Edit
**Replace value with:**
```
443ed3297b3a26ba4684129e59c72c6b6ce4a944344ef2579df2bdeba7d54210
```
**Click:** Save

---

### Step 1.3: Check Resend API Key

**Find:** `RESEND_API_KEY` in the list

**If it shows "(1 environment)"** → ✅ It's configured, proceed to Step 1.4

**If it's missing or empty** → 🔴 **CRITICAL - You must configure this:**

1. Go to: https://resend.com/signup
2. Sign up and verify your email
3. Dashboard → API Keys → "Create API Key"
4. Copy the key (starts with `re_...`)
5. Back in Vercel:
   - If variable exists: Click Edit → Paste key → Save
   - If missing: Click "Add New"
     - Name: `RESEND_API_KEY`
     - Value: `re_...` (your key)
     - Environments: Select "Production"
     - Click "Save"

---

### Step 1.4: Deploy to Production

Open your terminal:

```bash
cd voicelite-web
vercel deploy --prod
```

**Expected output:**
```
🔍  Inspect: https://vercel.com/...
✅  Production: https://voicelite.app [2m]
```

**Wait 2-3 minutes** for deployment to complete.

---

### Step 1.5: Verify Vercel Deployment

```bash
curl -I https://voicelite.app
```

**Expected:** First line shows `HTTP/1.1 200 OK` or `HTTP/2 200`

✅ **TASK 1 COMPLETE!**

---

## 🎯 TASK 2: Update Desktop App (10 minutes)

### Step 2.1: Open LicenseService.cs

**File path:** `VoiceLite/VoiceLite/Services/LicenseService.cs`

Open this file in your code editor (VS Code, Visual Studio, etc.)

---

### Step 2.2: Find and Replace Public Key

**Search for:** `LICENSE_PUBLIC_KEY_B64`

You should find a line like:
```csharp
private const string LICENSE_PUBLIC_KEY_B64 = "...some old key...";
```

**Replace the ENTIRE LINE with:**
```csharp
private const string LICENSE_PUBLIC_KEY_B64 = "fRR5l40q-wt8ptAFcOGsWIBHtLDBjnb_T3Z9HMLwgCc";
```

**Save the file** (Ctrl+S or Cmd+S)

---

### Step 2.3: Build the Desktop App

Open terminal in the VoiceLite directory:

```bash
cd VoiceLite
dotnet build VoiceLite.sln -c Release
```

**Expected output:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

### Step 2.4: Run Tests

```bash
dotnet test VoiceLite.Tests/VoiceLite.Tests.csproj
```

**Expected output:**
```
Passed!  - Failed:     0, Passed:   262, Skipped:    11
```

✅ **TASK 2 COMPLETE!**

---

## 🎯 TASK 3: Final Testing (5 minutes)

### Test 3.1: Verify Vercel Variables

```bash
cd voicelite-web
vercel env ls production | grep -E "(LICENSE|CRL|MIGRATION)"
```

**Expected:** You should see all 5 variables listed as "Encrypted"

---

### Test 3.2: Verify Website

Open browser: https://voicelite.app

**Expected:** Website loads correctly

---

### Test 3.3: Test Desktop App (Optional)

1. Run the desktop app:
   ```bash
   cd VoiceLite/VoiceLite/bin/Release/net8.0-windows
   ./VoiceLite.exe
   ```
2. App should start without errors
3. Try recording some audio to test core functionality

✅ **TASK 3 COMPLETE!**

---

## ✅ Final Checklist

### Vercel (Backend)
- [ ] Updated 5 environment variables in Vercel dashboard
- [ ] Checked RESEND_API_KEY (configured or added)
- [ ] Deployed to production (`vercel deploy --prod`)
- [ ] Verified deployment (https://voicelite.app returns 200)

### Desktop App
- [ ] Updated LICENSE_PUBLIC_KEY_B64 in LicenseService.cs
- [ ] Built solution (0 warnings, 0 errors)
- [ ] Ran tests (262 passing)

### Testing
- [ ] Verified environment variables exist in Vercel
- [ ] Website loads at https://voicelite.app
- [ ] Desktop app runs without errors

---

## 🎉 You're Done!

**Production is now fully updated with:**
- ✅ New Ed25519 license signing keys
- ✅ New CRL signing keys
- ✅ New migration secret
- ✅ Desktop app updated to match

---

## 📚 Additional Documentation

If you need more details:

- **Detailed walkthrough:** [voicelite-web/MANUAL_DEPLOYMENT_STEPS.md](voicelite-web/MANUAL_DEPLOYMENT_STEPS.md)
- **Secret rotation details:** [voicelite-web/SECRET_ROTATION_COMPLETE.md](voicelite-web/SECRET_ROTATION_COMPLETE.md)
- **Security incident report:** [voicelite-web/SECURITY_INCIDENT_RESPONSE.md](voicelite-web/SECURITY_INCIDENT_RESPONSE.md)
- **Code quality fixes:** [CODE_QUALITY_FIXES.md](CODE_QUALITY_FIXES.md)

---

## 🔴 Troubleshooting

### "vercel: command not found"
```bash
npm install -g vercel
vercel login
```

### "Build failed"
```bash
dotnet clean VoiceLite.sln
dotnet restore VoiceLite.sln
dotnet build VoiceLite.sln -c Release
```

### "Can't find LicenseService.cs"
The file is at: `VoiceLite/VoiceLite/Services/LicenseService.cs`

### "Website returns error"
Check Vercel logs:
```bash
cd voicelite-web
vercel logs --prod
```

---

## ⏱️ Time Tracker

- ✅ TASK 1 (Vercel): 15 minutes
- ✅ TASK 2 (Desktop): 10 minutes
- ✅ TASK 3 (Testing): 5 minutes
- **Total:** 30 minutes

---

**Ready to start?** → Begin with TASK 1 above! 🚀

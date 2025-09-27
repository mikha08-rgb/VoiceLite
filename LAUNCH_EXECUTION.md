# 🚀 VoiceLite Launch Execution - Professional Deployment

## Critical Pre-Launch Security Audit

### ⚠️ STOP - Verify These First:

1. **License Server Security**
   - [ ] No hardcoded API keys in `server.js`
   - [ ] `.gitignore` includes `*.db`, `node_modules`, `.env`
   - [ ] No customer data in git history
   - [ ] README doesn't contain sensitive info

2. **App Security**
   - [ ] SecurityService.cs integrity check disabled for now
   - [ ] No test license keys in code
   - [ ] API endpoints use HTTPS only
   - [ ] License validation works offline

3. **Financial Security**
   - [ ] Stripe account verified
   - [ ] Refund policy defined
   - [ ] Terms of service ready
   - [ ] EULA in place

---

## Phase 1: License Server Deployment (45 minutes)

### Step 1.1: Generate Secure API Keys

**Generate two 32-character keys** (CRITICAL - Don't use weak keys!)

Go to: https://www.uuidgenerator.net/version4
Generate 2 UUIDs and remove hyphens for your keys.

**Your Keys** (Write these down NOW):
```
API_KEY: ________________________________
ADMIN_KEY: ________________________________
```

⚠️ **NEVER share these keys. NEVER commit them to git.**

### Step 1.2: Create Private GitHub Repository

1. Open https://github.com/new
2. Repository name: `voicelite-license-server`
3. **CRITICAL**: Select "Private" (not Public!)
4. Do NOT initialize with README
5. Click "Create repository"
6. Copy the repository URL: `https://github.com/USERNAME/voicelite-license-server.git`

### Step 1.3: Push License Server

```bash
cd license-server
git remote add origin https://github.com/YOUR_USERNAME/voicelite-license-server.git
git branch -M main
git push -u origin main
```

**Verify**: Check GitHub - repository should show as Private with lock icon 🔒

### Step 1.4: Deploy to Railway

1. Go to https://railway.app
2. Sign up/Login with GitHub
3. Click "New Project"
4. Select "Deploy from GitHub repo"
5. **IMPORTANT**: Authorize only the `voicelite-license-server` repo
6. Select the repository
7. Railway starts building automatically

### Step 1.5: Configure Railway Environment

While deployment is running:

1. Click on the deployment
2. Go to "Variables" tab
3. Click "Raw Editor"
4. Paste EXACTLY (replace with your keys from Step 1.1):

```
API_KEY=your-actual-api-key-from-step-1-1
ADMIN_KEY=your-actual-admin-key-from-step-1-1
PORT=3000
DATABASE_PATH=./data/licenses.db
NODE_ENV=production
```

5. Click "Update Variables"
6. Service will redeploy automatically

### Step 1.6: Generate Production Domain

1. Go to "Settings" tab
2. Under "Networking" → "Public Networking"
3. Click "Generate Domain"
4. Copy your URL: `https://xxxxx.up.railway.app`

**Your Server URL**: _______________________________

### Step 1.7: Verify Deployment

Test your server (replace with your URL):
```bash
curl https://your-app.up.railway.app/api/check
```

Expected response:
```json
{"status":"ok","timestamp":"...","version":"1.0.0"}
```

✅ **Checkpoint**: Server is live and responding

---

## Phase 2: Update VoiceLite Application (30 minutes)

### Step 2.1: Update Production URLs

**File 1**: `VoiceLite\VoiceLite\Services\LicenseManager.cs`
```csharp
// Line 21 - Replace:
private const string LICENSE_SERVER_URL = "http://localhost:3000";
// With:
private const string LICENSE_SERVER_URL = "https://your-app.up.railway.app";

// Line 22 - Replace:
private const string API_KEY = "your-secret-api-key-change-this";
// With:
private const string API_KEY = "your-actual-api-key-from-step-1-1";
```

**File 2**: `VoiceLite\VoiceLite\Services\PaymentProcessor.cs`
```csharp
// Line 20 - Replace:
private const string LICENSE_SERVER_URL = "http://localhost:3000";
// With:
private const string LICENSE_SERVER_URL = "https://your-app.up.railway.app";
```

### Step 2.2: Rebuild Application

```bash
cd VoiceLite
dotnet clean
dotnet build VoiceLite.sln -c Release
```

**Verify**: Build succeeds with no errors

### Step 2.3: Test License Activation

1. Generate test license:
```bash
cd license-server
node admin.js generate test@voicelite.com Personal
```

2. Copy the license key: PERS-XXXX-XXXX-XXXX

3. Run VoiceLite:
```bash
cd VoiceLite\VoiceLite\bin\Release\net8.0-windows
.\VoiceLite.exe
```

4. Help → Enter License → Enter test license
5. Should show "License Activated Successfully"

✅ **Checkpoint**: Production license system working

---

## Phase 3: Payment System (30 minutes)

### Step 3.1: Create Stripe Account

1. Go to https://stripe.com
2. Click "Start now"
3. Enter business email (use professional email if possible)
4. Create strong password
5. Verify email immediately

### Step 3.2: Complete Stripe Activation

1. Dashboard → Complete your account setup
2. Enter business details:
   - Business type: Individual/Sole Proprietor
   - Product description: "Desktop software - speech to text application"
   - Website: https://yourusername.github.io/voicelite (we'll create this)

### Step 3.3: Create Products and Payment Links

**Personal License ($29.99)**:
1. Products → Add product
2. Name: "VoiceLite Personal License"
3. Price: $29.99, One-time
4. Tax behavior: "Exclusive of tax"
5. Save product
6. Click "Create payment link"
7. After payment: Don't show confirmation
8. Copy payment link URL

**Your Personal Link**: _______________________________

**Professional License ($59.99)**:
1. Repeat above with:
2. Name: "VoiceLite Professional License"
3. Price: $59.99

**Your Professional Link**: _______________________________

**Business License ($199.99)**:
1. Repeat above with:
2. Name: "VoiceLite Business License"
3. Price: $199.99

**Your Business Link**: _______________________________

### Step 3.4: Update Landing Page

Edit `docs\index.html`:

```html
<!-- Line 436 - Personal -->
<a href="YOUR_PERSONAL_STRIPE_LINK" class="buy-btn">

<!-- Line 451 - Professional -->
<a href="YOUR_PROFESSIONAL_STRIPE_LINK" class="buy-btn">

<!-- Line 466 - Business -->
<a href="YOUR_BUSINESS_STRIPE_LINK" class="buy-btn">
```

✅ **Checkpoint**: Payment links integrated

---

## Phase 4: Landing Page Deployment (20 minutes)

### Step 4.1: Create Public Repository

1. Go to https://github.com/new
2. Repository name: `voicelite` (or `voicelite-site`)
3. **PUBLIC** repository (for GitHub Pages)
4. Do NOT initialize
5. Create repository

### Step 4.2: Deploy Landing Page

```bash
cd docs
git init
git add .
git commit -m "VoiceLite - Professional Speech-to-Text for Windows"
git branch -M main
git remote add origin https://github.com/YOUR_USERNAME/voicelite.git
git push -u origin main
```

### Step 4.3: Enable GitHub Pages

1. Go to repository Settings
2. Scroll to "Pages" section
3. Source: Deploy from branch
4. Branch: main
5. Folder: / (root)
6. Click Save
7. Wait 2-3 minutes for deployment

**Your Site**: https://YOUR_USERNAME.github.io/voicelite

### Step 4.4: Test Complete Flow

1. Visit your landing page
2. Click "Download Free Trial"
3. Click a "Buy" button - goes to Stripe
4. Use test card: 4242 4242 4242 4242
5. Complete test purchase
6. Should redirect to success page

✅ **Checkpoint**: Complete purchase flow working

---

## Phase 5: Production Installer (15 minutes)

### Step 5.1: Create Release Build

```bash
cd VoiceLite
dotnet publish VoiceLite\VoiceLite.csproj -c Release -r win-x64 --self-contained false
```

### Step 5.2: Build Installer

1. Open Inno Setup
2. Load `VoiceLite.iss`
3. Update line with download URL to your GitHub pages
4. Compile (F9)
5. Output: `Output\VoiceLite-Setup.exe`

### Step 5.3: Create GitHub Release

1. Go to your main VoiceLite repo
2. Releases → Create new release
3. Tag: v1.0.0
4. Title: "VoiceLite 1.0.0 - Launch Release"
5. Attach `VoiceLite-Setup.exe`
6. Publish release
7. Copy download URL

### Step 5.4: Update Landing Page Download Link

Update `docs\index.html`:
```html
<a href="YOUR_GITHUB_RELEASE_URL" class="cta secondary">Download Free Trial</a>
```

Push update:
```bash
cd docs
git add .
git commit -m "Update download link"
git push
```

✅ **Checkpoint**: Installer available for download

---

## Phase 6: Final Security & Launch Checklist

### Security Verification:
- [ ] License server repository is PRIVATE
- [ ] API keys are strong and unique
- [ ] No sensitive data in public repos
- [ ] All URLs use HTTPS
- [ ] Test license has been deleted from server

### Functionality Testing:
- [ ] Landing page loads
- [ ] Download button works
- [ ] All buy buttons go to Stripe
- [ ] Installer downloads and runs
- [ ] App starts in trial mode
- [ ] License activation works
- [ ] Speech-to-text functions

### Business Readiness:
- [ ] Support email ready
- [ ] License email template saved
- [ ] Refund policy defined
- [ ] First day marketing plan ready

---

## 🎉 LAUNCH ANNOUNCEMENT TEMPLATE

Once everything above is checked:

**LinkedIn/Professional:**
> Excited to announce the launch of VoiceLite - a professional speech-to-text tool for Windows that works completely offline.
>
> After months of development, it's finally ready. Using OpenAI's Whisper technology, it achieves 99% accuracy while keeping your voice data 100% private.
>
> Launch week special: 30% off all licenses.
>
> Learn more: [your-link]

**Twitter/Social:**
> 🚀 Just launched VoiceLite!
>
> 🎤 Speech-to-text that actually works
> 🔒 100% offline - your voice stays private
> ⚡ One hotkey to transcribe anywhere
> 💰 No subscriptions, pay once
>
> Launch week: 30% off
>
> Try free: [your-link]

**Reddit/Forums:**
> [Dev] I built a Windows speech-to-text app that runs completely offline
>
> Hey everyone, I just launched VoiceLite - it uses OpenAI's Whisper models locally on your machine. Press Alt, speak, release, and your words appear wherever your cursor is.
>
> No cloud, no subscriptions, your voice never leaves your computer.
>
> Would love feedback from early users. Offering 30% off this week.
>
> Details: [your-link]

---

## 🚨 Emergency Contacts & Support

**If something goes wrong:**

1. **Railway down**: Check https://railway.app/status
2. **Stripe issues**: https://status.stripe.com
3. **GitHub Pages slow**: Normal, wait 10 minutes
4. **License not working**: Check Railway logs
5. **Customer can't download**: Provide direct GitHub release link

**Your Quick Reference:**
- License Server: _______________________
- Landing Page: ________________________
- Stripe Dashboard: https://dashboard.stripe.com
- Railway Dashboard: https://railway.app/dashboard

---

## YOU'RE READY TO LAUNCH! 🚀

When you complete all checkboxes above, you're officially in business.

Remember: The first 48 hours are crucial. Be responsive to feedback, fix issues quickly, and keep momentum going.

Good luck with your launch!
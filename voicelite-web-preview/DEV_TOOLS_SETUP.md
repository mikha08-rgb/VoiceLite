# VoiceLite Dev Tools Setup Guide

Quick setup guide for essential development tools.

---

## **Automated Setup (Recommended)**

### **Step 1: Run Setup Script**

Right-click PowerShell and select **"Run as Administrator"**, then:

```powershell
cd "C:\Users\mishk\Codingprojects\SpeakLite\HereWeGoAgain v3.3 Fuck"
.\setup-dev-tools.ps1
```

This will automatically install:
- ✅ Scoop (package manager)
- ✅ Stripe CLI
- ✅ Bruno download link

---

## **Manual Setup (If Script Fails)**

### **1. Stripe CLI** (5 minutes)

**Option A: Using Scoop (Recommended)**
```powershell
# Install Scoop
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
Invoke-RestMethod -Uri https://get.scoop.sh | Invoke-Expression

# Install Stripe CLI
scoop bucket add stripe https://github.com/stripe/scoop-stripe-cli.git
scoop install stripe
```

**Option B: Direct Download**
1. Download: https://github.com/stripe/stripe-cli/releases/latest
2. Extract `stripe.exe` to `C:\Program Files\Stripe\`
3. Add to PATH: `C:\Program Files\Stripe\`

**Login to Stripe:**
```bash
stripe login
# Opens browser, click "Allow access"
```

**Test it works:**
```bash
stripe --version
# Should show: stripe version X.X.X
```

---

### **2. Bruno API Client** (3 minutes)

1. Download: https://www.usebruno.com/downloads
2. Run installer (`Bruno-Setup-X.X.X.exe`)
3. Open Bruno

**Load VoiceLite API Collection:**
1. Click "Open Collection"
2. Navigate to: `voicelite-web\bruno\voicelite-api`
3. Click "Select Folder"

**You now have pre-configured API requests!**

---

### **3. Prisma Studio** (Already Installed!)

```bash
cd voicelite-web
npm run db:studio
```

Opens http://localhost:5555 - Browse your database visually.

---

### **4. Password Manager** (5 minutes)

**Option 1: Bitwarden (Free)**
1. Download: https://bitwarden.com/download/
2. Install desktop app + browser extension
3. Create free account
4. Store secrets:
   - Stripe Secret Key
   - Stripe Webhook Secret
   - Supabase Database URL
   - Resend API Key

**Option 2: 1Password ($3/month)**
1. Download: https://1password.com/downloads
2. Start 14-day trial
3. Install + browser extension
4. Store secrets

---

## **How to Use**

### **Test Stripe Webhooks Locally**

**Terminal 1 (Dev Server):**
```bash
cd voicelite-web
npm run dev
```

**Terminal 2 (Webhook Forwarding):**
```bash
stripe listen --forward-to localhost:3000/api/webhook

# You'll see:
# > Ready! Your webhook signing secret is whsec_xxx...
# Copy this to .env.local
```

**Terminal 3 (Trigger Test Payment):**
```bash
stripe trigger payment_intent.succeeded
```

**Check Results:**
1. Terminal 2 shows webhook received
2. Terminal 1 shows console logs
3. Open Prisma Studio → Check License table for new license

---

### **Test API Endpoints with Bruno**

1. Open Bruno
2. Select "VoiceLite API" collection
3. Choose "Local" environment (top-right dropdown)
4. Click any request (e.g., "Health Check")
5. Click "Send"
6. View response

**Pre-configured requests:**
- Health Check → Test API is running
- Create Checkout Session → Test Stripe checkout
- Get Stripe Products → View pricing
- Download Installer → Test download tracking

---

### **Browse Database with Prisma Studio**

```bash
cd voicelite-web
npm run db:studio
```

**Common tasks:**
- View licenses: Click "License" table
- Check activations: Click "LicenseActivation" table
- Debug webhooks: Click "WebhookEvent" table
- Search by email: Use filter box

---

## **Tools Reference**

| Tool | Command | URL |
|------|---------|-----|
| **Stripe CLI** | `stripe --version` | https://stripe.com/docs/stripe-cli |
| **Bruno** | Launch app | https://www.usebruno.com/downloads |
| **Prisma Studio** | `npm run db:studio` | http://localhost:5555 |
| **Dev Server** | `npm run dev` | http://localhost:3000 |

---

## **Troubleshooting**

### **Stripe CLI: "command not found"**
```bash
# Check if installed
stripe --version

# If not found, reinstall
scoop install stripe

# Or add to PATH manually
# Add: C:\Users\YOUR_USERNAME\scoop\shims
```

### **Prisma Studio: Port 5555 already in use**
```bash
# Kill existing process
npx kill-port 5555

# Or use different port
npx prisma studio --port 5556
```

### **Bruno: Can't find collection**
```bash
# Check folder exists
dir voicelite-web\bruno\voicelite-api

# If missing, collection files are in this folder
# Open Bruno → "Open Collection" → Select folder
```

---

## **Next Steps**

1. ✅ Run `setup-dev-tools.ps1` (or manual setup)
2. ✅ Test Stripe CLI: `stripe login`
3. ✅ Download Bruno: https://www.usebruno.com/downloads
4. ✅ Test Prisma Studio: `npm run db:studio`
5. ✅ Test webhook flow (see "How to Use" section)

**You're ready to develop!** 🚀
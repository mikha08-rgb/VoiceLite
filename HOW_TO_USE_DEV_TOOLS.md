# How to Use Your Dev Tools

Quick reference for using Stripe CLI, Bruno, and Prisma Studio.

---

## **1. Test Stripe Webhooks Locally** 🔔

### **What This Does**
Test your Stripe payment flow on your local machine (before deploying to production).

### **Step-by-Step**

**Open 3 PowerShell terminals:**

#### **Terminal 1: Start Dev Server**
```bash
cd voicelite-web
npm run dev
```
Leave this running. Your app is now at http://localhost:3000

#### **Terminal 2: Forward Webhooks**
```bash
stripe listen --forward-to localhost:3000/api/webhook
```

You'll see:
```
> Ready! Your webhook signing secret is whsec_xxxxxxxxxxxxx
```

**IMPORTANT**: Copy that `whsec_xxx` value!

#### **Terminal 3: Trigger Test Payment**
```bash
stripe trigger payment_intent.succeeded
```

### **What Happens**
1. Terminal 3 sends fake payment to Stripe
2. Stripe sends webhook to Terminal 2
3. Terminal 2 forwards to your local app
4. Your app creates a license
5. Check Prisma Studio to see the new license!

### **Common Commands**
```bash
# Login to Stripe (first time only)
stripe login

# List available test events
stripe trigger --help

# Trigger checkout completed
stripe trigger checkout.session.completed

# Trigger subscription created
stripe trigger customer.subscription.created

# Trigger payment failed
stripe trigger payment_intent.payment_failed
```

---

## **2. Test APIs with Bruno** 🔌

### **What This Does**
Test your API endpoints without writing code. Like Postman but simpler.

### **Step-by-Step**

#### **1. Open Bruno**
- Launch Bruno app
- Click "Open Collection"
- Navigate to: `C:\Users\mishk\Codingprojects\SpeakLite\HereWeGoAgain v3.3 Fuck\voicelite-web\bruno\voicelite-api`
- Click "Select Folder"

#### **2. Select Environment**
Top-right dropdown:
- **Local** → Test on localhost:3000
- **Production** → Test on voicelite.app

#### **3. Test Your First Endpoint**

Click **"Health Check"** → Click **"Send"**

You should see:
```json
{
  "status": "ok",
  "timestamp": "2025-01-20T12:34:56.789Z"
}
```

#### **4. Test Checkout Flow**

Click **"Create Checkout Session"** → Click **"Send"**

You should see:
```json
{
  "sessionId": "cs_test_xxxxx",
  "url": "https://checkout.stripe.com/c/pay/cs_test_xxxxx"
}
```

Copy that URL and open it in your browser → Fake Stripe checkout page!

### **Pre-configured Requests**

| Request | What It Does |
|---------|-------------|
| **Health Check** | Test if API is running |
| **Create Checkout Session** | Create Stripe checkout |
| **Get Stripe Products** | View pricing plans |
| **Download Installer** | Test download tracking |

### **How to Add New Requests**

1. Right-click collection → "New Request"
2. Name: "Test License Activation"
3. Method: POST
4. URL: `{{baseUrl}}/api/license/activate`
5. Body → JSON:
```json
{
  "licenseKey": "VL-XXXX-XXXX-XXXX-XXXX",
  "machineId": "test-machine-123"
}
```
6. Click "Send"

---

## **3. Browse Database with Prisma Studio** 🗄️

### **What This Does**
Visual database browser. See licenses, activations, webhooks without SQL.

### **Step-by-Step**

#### **1. Start Prisma Studio**
```bash
cd voicelite-web
npm run db:studio
```

Opens: http://localhost:5555

#### **2. Browse Tables**

Click any table on the left:
- **License** → All licenses (email, key, status)
- **LicenseActivation** → All activated devices
- **WebhookEvent** → Stripe webhook history

#### **3. Common Tasks**

**Find License by Email:**
1. Click "License" table
2. Click filter icon (funnel)
3. Field: `email`
4. Operator: `equals`
5. Value: `customer@example.com`
6. Click "Apply"

**See All Active Licenses:**
1. Click "License" table
2. Filter: `status` equals `ACTIVE`

**Check Device Activations:**
1. Click "LicenseActivation" table
2. See all activated devices
3. Click a row → See full details (machineId, lastValidatedAt)

**Manual License Creation (Testing):**
1. Click "License" table
2. Click "Add record" button
3. Fill in:
   - email: `test@example.com`
   - licenseKey: `VL-TEST-1234-5678-9012`
   - type: `LIFETIME`
   - status: `ACTIVE`
4. Click "Save"

#### **4. Edit Data**

1. Click any row → Click "Edit" icon
2. Change fields
3. Click "Save"

**Example: Deactivate a license**
1. Find license
2. Click row → Edit
3. Change `status` from `ACTIVE` to `CANCELED`
4. Save

---

## **Common Workflows**

### **Workflow 1: Test Full Payment Flow**

```bash
# Terminal 1: Start dev server
cd voicelite-web
npm run dev

# Terminal 2: Forward webhooks
stripe listen --forward-to localhost:3000/api/webhook

# Terminal 3: Trigger payment
stripe trigger checkout.session.completed
```

Then:
1. Open Prisma Studio
2. Check "License" table
3. Should see new license created!
4. Check "WebhookEvent" table
5. Should see webhook logged

### **Workflow 2: Debug "Payment Worked But No License"**

1. **Check Stripe Dashboard**: https://dashboard.stripe.com/test/payments
   - Was payment successful?

2. **Check Webhook Logs** in Prisma Studio:
   - Click "WebhookEvent" table
   - Was webhook received?

3. **Check Vercel Logs**: https://vercel.com/dashboard
   - Functions → Logs
   - Search for "webhook"
   - Any errors?

4. **Check License Table** in Prisma Studio:
   - Search by customer email
   - License created?

### **Workflow 3: Test API Endpoint Before Deploying**

1. Make code changes
2. Run `npm run dev`
3. Open Bruno
4. Test endpoint
5. Fix any errors
6. Deploy when working

---

## **Quick Reference Commands**

### **Stripe CLI**
```bash
stripe login                     # First time setup
stripe listen --forward-to ...   # Forward webhooks
stripe trigger [event]           # Trigger test event
stripe logs tail                 # Watch Stripe events
```

### **Prisma Studio**
```bash
npm run db:studio                # Start studio
npx prisma studio --port 5556    # Use different port
```

### **Dev Server**
```bash
npm run dev                      # Start Next.js dev
npm run build                    # Test production build
```

---

## **Troubleshooting**

### **Stripe CLI: "No such webhook endpoint"**
```bash
# Make sure dev server is running first
cd voicelite-web
npm run dev

# THEN start webhook forwarding
stripe listen --forward-to localhost:3000/api/webhook
```

### **Bruno: "Failed to fetch"**
- Check dev server is running (`npm run dev`)
- Check you selected "Local" environment (not Production)
- Check URL in Bruno matches your dev server port

### **Prisma Studio: "Can't connect to database"**
```bash
# Check DATABASE_URL in .env.local
cat voicelite-web/.env.local | grep DATABASE_URL

# Test connection
cd voicelite-web
npx prisma db pull
```

### **Prisma Studio: Port 5555 in use**
```bash
# Kill process on port 5555
npx kill-port 5555

# Or use different port
npx prisma studio --port 5556
```

---

## **Pro Tips**

### **Stripe CLI**
- Use `stripe trigger --help` to see all available events
- Use `stripe logs tail` to watch live Stripe events
- Keep webhook forwarding running in background terminal

### **Bruno**
- Use `Ctrl+Enter` to send request (faster than clicking)
- Right-click request → "Duplicate" to create similar requests
- Use environments to switch between local/production easily

### **Prisma Studio**
- Use filters heavily (the funnel icon)
- Click column headers to sort
- Hold Ctrl+Click to select multiple rows

---

## **Next Steps**

1. ✅ Test webhook flow (see Workflow 1 above)
2. ✅ Browse database in Prisma Studio
3. ✅ Test API with Bruno
4. ✅ Read [DEV_TOOLS_SETUP.md](DEV_TOOLS_SETUP.md) for installation help

**You're ready to develop!** 🚀
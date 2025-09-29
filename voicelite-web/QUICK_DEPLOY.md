# 🚀 VoiceLite.app - Quick Deploy Guide

## Your Domain: voicelite.app ✅

### 1️⃣ Set Up Stripe (5 min)
1. Go to https://dashboard.stripe.com
2. Create Product: "VoiceLite Pro" → $7/month
3. Copy these 2 keys:
   - **Secret Key**: `sk_test_...` (API Keys section)
   - **Price ID**: `price_...` (from your product)

### 2️⃣ Set Up Resend (3 min)
1. Go to https://resend.com/signup
2. Add domain: `voicelite.app`
3. Add these DNS records to your domain:
   ```
   Type: MX
   Name: send
   Value: feedback-smtp.us-east-1.amazonses.com
   Priority: 10
   ```
4. Get your **API Key**: `re_...`

### 3️⃣ Create .env.local (1 min)
```bash
# In voicelite-web folder
STRIPE_SECRET_KEY=sk_test_YOUR_KEY
STRIPE_WEBHOOK_SECRET=whsec_TEMP (we'll update after deploy)
STRIPE_PRICE_ID=price_YOUR_PRICE_ID
RESEND_API_KEY=re_YOUR_KEY
NEXT_PUBLIC_URL=https://voicelite.app
```

### 4️⃣ Deploy to Vercel (5 min)
```bash
cd voicelite-web
vercel --prod
```
- Project name: `voicelite`
- Link to GitHub: Yes (recommended)

### 5️⃣ Configure Vercel (3 min)
1. Go to https://vercel.com/dashboard
2. Click your project → Settings → Environment Variables
3. Add all 5 variables from .env.local

### 6️⃣ Connect Domain (5 min)
In Vercel Dashboard → Settings → Domains:
1. Add domain: `voicelite.app`
2. Add these DNS records at your registrar:

   **Option A - Nameservers (Easiest):**
   ```
   ns1.vercel-dns.com
   ns2.vercel-dns.com
   ```

   **Option B - A/CNAME Records:**
   ```
   Type: A
   Name: @
   Value: 76.76.21.21

   Type: CNAME
   Name: www
   Value: cname.vercel-dns.com
   ```

### 7️⃣ Setup Stripe Webhook (2 min)
1. Stripe Dashboard → Webhooks → Add endpoint
2. URL: `https://voicelite.app/api/webhook`
3. Event: `checkout.session.completed`
4. Copy the **Signing secret**: `whsec_...`
5. Update in Vercel Environment Variables

### 8️⃣ Add Your .exe Files
Place in `public/` folder:
- `VoiceLite-Free.exe`
- `VoiceLite-Pro.exe`

Then redeploy:
```bash
vercel --prod
```

---

## ✅ You're LIVE!

Test at: https://voicelite.app

### Test Checklist:
- [ ] Landing page loads
- [ ] Free download works
- [ ] Stripe checkout opens (use test card: 4242 4242 4242 4242)
- [ ] Success page shows after payment
- [ ] Email arrives with license key

### Going Live with Real Payments:
1. Switch to Stripe live keys
2. Update Vercel env variables
3. Create new webhook for live mode
4. Test with real $7 purchase

---

**Total Time: ~25 minutes** ⏱️

Your domain `voicelite.app` is perfect and ready to go!
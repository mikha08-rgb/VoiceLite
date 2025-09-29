# 🚀 VoiceLite SaaS - Deployment Guide

## ✅ What's Been Built

1. **Landing Page** with hero, features, pricing comparison
2. **Stripe Checkout** integration for payments
3. **License System** with automatic email delivery
4. **Webhook Handler** for payment confirmation
5. **Success Page** for post-payment flow

## 📋 Before You Deploy - Required Accounts

### 1. Stripe Account Setup
1. Go to https://dashboard.stripe.com
2. Create a product called "VoiceLite Pro"
3. Set price to $7/month
4. Copy your **Price ID** (starts with `price_`)
5. Get your **Secret Key** from Developers → API Keys
6. Note your **Webhook Secret** (we'll get this after deployment)

### 2. Resend Account Setup
1. Go to https://resend.com
2. Sign up for free account
3. Add and verify your domain (voicelite.app)
4. Get your **API Key** from the dashboard

### 3. Vercel Account
1. Go to https://vercel.com
2. Sign up with GitHub
3. Install Vercel CLI (already done: `npm install -g vercel`)

## 🚀 Deployment Steps

### Step 1: Create .env.local file
```bash
# In the voicelite-web directory
cp .env.local.example .env.local
```

Edit `.env.local` and add your keys:
```
STRIPE_SECRET_KEY=sk_test_YOUR_KEY
STRIPE_WEBHOOK_SECRET=whsec_YOUR_SECRET (get this after deployment)
STRIPE_PRICE_ID=price_YOUR_PRICE_ID
RESEND_API_KEY=re_YOUR_KEY
NEXT_PUBLIC_URL=https://voicelite.app
```

### Step 2: Add Executable Files
Place your Windows executables in the `public` folder:
- `public/VoiceLite-Free.exe` (tiny model only)
- `public/VoiceLite-Pro.exe` (all models)

### Step 3: Deploy to Vercel
```bash
# In the voicelite-web directory
vercel --prod
```

Follow the prompts:
- Link to existing project? **No**
- What's your project name? **voicelite-web**
- Which directory? **./** (current)
- Want to override settings? **No**

### Step 4: Add Environment Variables in Vercel
1. Go to https://vercel.com/dashboard
2. Click on your project
3. Go to Settings → Environment Variables
4. Add all variables from your `.env.local`

### Step 5: Setup Stripe Webhook
1. Go to Stripe Dashboard → Webhooks
2. Click "Add endpoint"
3. Endpoint URL: `https://voicelite.app/api/webhook`
4. Select event: `checkout.session.completed`
5. Copy the **Signing secret** (starts with `whsec_`)
6. Update `STRIPE_WEBHOOK_SECRET` in Vercel Environment Variables

### Step 6: Connect Your Domain
1. In Vercel Dashboard → Settings → Domains
2. Add your domain: `voicelite.app`
3. Follow DNS instructions (usually add CNAME or A records)

## 🧪 Testing Your Deployment

### Test the Landing Page
1. Visit https://voicelite.app
2. Check that the page loads correctly
3. Download buttons should work

### Test Stripe Payment
1. Click "Get Pro - $7/month"
2. Use test card: `4242 4242 4242 4242`
3. Any future date, any CVC
4. Should redirect to success page
5. Check data/licenses.json for the license

### Test Email Delivery
1. Complete a test purchase
2. Check if email arrives (check spam folder)
3. Verify license key is in the email

## 🔴 Go Live Checklist

When ready for real customers:

1. **Switch Stripe to Live Mode**
   - Get live API keys from Stripe
   - Update Vercel environment variables
   - Create live webhook endpoint

2. **Update URLs**
   - Ensure NEXT_PUBLIC_URL is your real domain
   - Update Stripe customer portal links

3. **Add Real Executables**
   - Upload production VoiceLite-Free.exe
   - Upload production VoiceLite-Pro.exe

4. **Test Everything Again**
   - Make a real $7 purchase yourself
   - Verify email delivery
   - Test license activation in Windows app

## 📊 Monitor Your SaaS

- **Payments**: Stripe Dashboard
- **Licenses**: Check `data/licenses.json`
- **Traffic**: Vercel Analytics
- **Errors**: Vercel Functions logs

## 🆘 Troubleshooting

| Issue | Solution |
|-------|----------|
| Checkout not working | Check STRIPE_SECRET_KEY in Vercel |
| No email received | Verify RESEND_API_KEY and domain |
| Webhook failing | Check webhook secret and endpoint URL |
| Downloads failing | Ensure .exe files are in public folder |

## 🎯 Next Steps (After Launch)

1. **Add Analytics**: Plausible or Fathom
2. **Upgrade Storage**: Move from JSON to database at 100+ customers
3. **Add Features**: Customer portal, license management
4. **Marketing**: SEO, content, ads

---

**You're ready to launch!** 🚀 The simple, working SaaS is complete.
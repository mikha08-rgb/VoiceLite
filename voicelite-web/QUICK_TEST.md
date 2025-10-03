# Quick Payment Testing Guide

## ⚡ 30-Second Test (No Setup Required)

**You're already in Stripe test mode!** No LLC or activated account needed.

### 1. Start Dev Server
```bash
cd voicelite-web
npm run dev
```

### 2. Test Checkout

Visit http://localhost:3000 → Click "Upgrade to Pro"

**Test Card (Always Works):**
```
Card:  4242 4242 4242 4242
Exp:   12/34
CVC:   123
ZIP:   12345
```

### 3. Get License Key

Watch your terminal for:
```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📧 LICENSE EMAIL (Development Mode)
🔑 LICENSE KEY: VL-XXXX-XXXX-XXXX-XXXX
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

Copy the license key from console!

---

## 🧪 More Test Cards

| Card | Result |
|------|--------|
| `4242424242424242` | ✅ Success |
| `4000000000000002` | ❌ Declined |
| `4000002500003155` | ❌ Requires auth |

Full list: https://docs.stripe.com/testing#cards

---

## 📧 Real Email Delivery (Optional)

Current setup logs emails to console. To send real emails:

1. Sign up: https://resend.com (free tier, no CC)
2. Get API key
3. Add to `.env.local`:
   ```bash
   RESEND_API_KEY="re_..."
   RESEND_FROM_EMAIL="VoiceLite <noreply@resend.dev>"
   ```
4. Restart: `npm run dev`

---

## 🔧 Webhook Testing (Optional)

To test subscription updates, refunds, etc:

```bash
# Install Stripe CLI
winget install stripe.stripe-cli

# Login
stripe login

# Forward webhooks
stripe listen --forward-to localhost:3000/api/webhook

# Copy the whsec_... secret to .env.local
STRIPE_WEBHOOK_SECRET="whsec_..."

# Restart dev server
npm run dev

# Trigger events
stripe trigger checkout.session.completed
stripe trigger charge.refunded
```

---

## 📊 View Payment Data

**Stripe Dashboard:**
https://dashboard.stripe.com/test/payments

**Database (Prisma Studio):**
```bash
npx prisma studio
```
Check `License` table for generated licenses.

---

## 🚀 When Ready for Production (After LLC)

See [TESTING_PAYMENTS.md](./TESTING_PAYMENTS.md) for:
- Switching to live API keys
- Creating production price IDs
- Configuring production webhooks
- Deploying to Vercel

---

**Full documentation:** [TESTING_PAYMENTS.md](./TESTING_PAYMENTS.md)

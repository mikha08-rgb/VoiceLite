# VoiceLite Pro Licensing System - Setup Guide

## 🎉 Implementation Complete!

The VoiceLite Pro licensing system has been successfully implemented. Here's what was built:

### ✅ Backend (voicelite-web)
1. **Simplified Checkout API** (`/api/checkout`)
   - No authentication required
   - Creates Stripe checkout session
   - Stripe collects email automatically

2. **Stripe Webhook Handler** (`/api/webhooks/stripe`)
   - Listens for `checkout.session.completed`
   - Generates UUID license keys
   - Saves license to Supabase
   - Sends email via Resend

3. **License Validation API** (`/api/licenses/validate`)
   - Simple key lookup (no auth required)
   - Returns `{ valid: true/false, tier: "free"/"pro" }`

4. **Beautiful Email Template**
   - Professional HTML design
   - Clear activation instructions
   - Lists all Pro features

### ✅ Desktop App (VoiceLite)
1. **Settings Model Updated**
   - Added `LicenseKey` property
   - Added `IsProLicense` boolean flag

2. **New LicenseService**
   - Validates keys with backend API
   - Handles errors gracefully
   - 10-second timeout

3. **License Tab in Settings**
   - Shows current tier (Free/Pro)
   - License key input field
   - Activate button
   - Buy Pro button (opens website)
   - Pro features list

---

## 🔧 Required Setup Steps

### 1. Stripe Configuration

#### A. Create Product and Price
1. Go to [Stripe Dashboard](https://dashboard.stripe.com/products)
2. Click "Add Product"
   - Name: **VoiceLite Pro**
   - Description: **Lifetime access to all Pro features**
3. Add Price:
   - Type: **One time**
   - Price: **$20.00 USD**
   - Copy the Price ID (starts with `price_...`)

#### B. Set Up Webhook
1. Go to [Stripe Webhooks](https://dashboard.stripe.com/webhooks)
2. Click "Add endpoint"
   - Endpoint URL: `https://voicelite.app/api/webhooks/stripe`
   - Events to send: Select **`checkout.session.completed`**
3. Copy the Webhook Signing Secret (starts with `whsec_...`)

### 2. Resend Configuration

1. Go to [Resend Dashboard](https://resend.com/api-keys)
2. Create an API key
3. Verify your domain or use `resend.dev` for testing
4. Choose the "from" email address (e.g., `licenses@voicelite.app`)

### 3. Vercel Environment Variables

Add these environment variables in your [Vercel Dashboard](https://vercel.com/dashboard):

```env
# Stripe
STRIPE_SECRET_KEY=sk_live_...  # or sk_test_ for testing
STRIPE_WEBHOOK_SECRET=whsec_...
STRIPE_PRO_PRICE_ID=price_...

# Resend
RESEND_API_KEY=re_...
RESEND_FROM_EMAIL=licenses@voicelite.app

# Supabase (already configured)
DATABASE_URL=postgresql://...
DIRECT_DATABASE_URL=postgresql://...
```

**After adding variables, redeploy:**
```bash
cd voicelite-web
vercel deploy --prod
```

---

## 🧪 Testing the Complete Flow

### Test Mode (Recommended First)

1. **Use Stripe Test Mode**
   - Use test API keys (`sk_test_...`, `whsec_...`)
   - Test card: `4242 4242 4242 4242`, any future date, any CVC

2. **Test Checkout Flow**
   ```bash
   # Test the checkout endpoint
   curl -X POST https://voicelite.app/api/checkout
   # Should return: {"url": "https://checkout.stripe.com/..."}
   ```

3. **Complete Test Purchase**
   - Visit the checkout URL
   - Enter test card details
   - Use any email address
   - Complete payment

4. **Verify Webhook**
   - Check Supabase → `License` table → New license created
   - Check email inbox → License key received
   - Check Stripe Dashboard → Payment shows as succeeded

5. **Test License Activation**
   - Open VoiceLite desktop app
   - Go to Settings → License tab
   - Paste the license key from email
   - Click "Activate License"
   - Should show success message
   - Tier should change to "Pro ⭐"
   - Settings saved to `%LOCALAPPDATA%\VoiceLite\settings.json`

6. **Verify Persistence**
   - Close and reopen VoiceLite
   - Go to Settings → License
   - Should still show "Pro ⭐" tier
   - License key should be disabled (already activated)

### Production Mode

Once testing is complete:

1. Replace test keys with live keys (`sk_live_...`)
2. Update Stripe webhook to production endpoint
3. Redeploy Vercel with production environment variables
4. Test with real payment (can refund immediately)

---

## 📝 Database Schema

The system uses existing Supabase tables:

### License Table
```sql
CREATE TABLE License (
  id TEXT PRIMARY KEY,
  userId TEXT NOT NULL,
  licenseKey TEXT UNIQUE NOT NULL,  -- UUID format
  type TEXT NOT NULL,                -- 'LIFETIME'
  status TEXT NOT NULL,              -- 'ACTIVE', 'CANCELED', 'EXPIRED'
  stripeCustomerId TEXT,
  stripePaymentIntentId TEXT,
  activatedAt TIMESTAMP,
  expiresAt TIMESTAMP,
  createdAt TIMESTAMP DEFAULT NOW(),
  updatedAt TIMESTAMP DEFAULT NOW()
);
```

### User Table
```sql
CREATE TABLE User (
  id TEXT PRIMARY KEY,
  email TEXT UNIQUE NOT NULL,
  createdAt TIMESTAMP DEFAULT NOW(),
  updatedAt TIMESTAMP DEFAULT NOW()
);
```

---

## 🚀 How to Add Pro Features (Future)

The licensing system is designed to be future-proof. To add new Pro-only features:

### Example: Word Replacement Feature

1. **Check the license flag anywhere in the app:**
```csharp
if (settings.IsProLicense)
{
    // Enable Pro feature
    WordReplacementPanel.Visibility = Visibility.Visible;
}
else
{
    // Show upgrade prompt
    ShowUpgradeToProButton();
}
```

2. **That's it!** The `IsProLicense` boolean controls all Pro features.

### More Examples:
```csharp
// In any service or window:
if (settings.IsProLicense)
{
    // Unlock model downloads
    EnableModelDownloads();

    // Enable custom hotkeys
    EnableCustomHotkeys();

    // Enable cloud sync
    EnableCloudSync();
}
```

---

## 🔍 Troubleshooting

### Webhook Not Firing
- Check Stripe webhook logs in dashboard
- Verify endpoint URL is correct
- Ensure webhook secret matches environment variable
- Test webhook locally with Stripe CLI: `stripe listen --forward-to localhost:3000/api/webhooks/stripe`

### Email Not Sending
- Check Resend API key is valid
- Verify sender email is verified in Resend
- Check Resend logs for errors
- Test email template locally

### License Validation Failing
- Check network connectivity
- Verify API endpoint is accessible: `https://voicelite.app/api/licenses/validate`
- Check Supabase connection
- Look at browser/desktop app console for errors

### Desktop App Not Saving License
- Check `saveSettingsCallback` is being called
- Verify settings.json has write permissions
- Check `%LOCALAPPDATA%\VoiceLite\settings.json` for `IsProLicense: true`

---

## 📊 Monitoring

### Check License Stats
Query Supabase to see license activity:

```sql
-- Total licenses issued
SELECT COUNT(*) FROM "License" WHERE status = 'ACTIVE';

-- Recent licenses (last 7 days)
SELECT email, "licenseKey", "createdAt"
FROM "License" l
JOIN "User" u ON l."userId" = u.id
WHERE l."createdAt" > NOW() - INTERVAL '7 days'
ORDER BY l."createdAt" DESC;

-- Revenue estimate (assuming $20 per license)
SELECT COUNT(*) * 20 as estimated_revenue
FROM "License"
WHERE status = 'ACTIVE';
```

### Stripe Dashboard
- View successful payments
- Check webhook delivery logs
- Monitor failed payments
- Process refunds

---

## 🎯 Next Steps

1. **Test the full flow** with Stripe test mode
2. **Configure production** environment variables
3. **Deploy website update** pointing "Buy Pro" button to checkout
4. **Build desktop app** v1.0.75 with licensing UI
5. **Test end-to-end** with real payment (refund after testing)
6. **Go live!** 🚀

---

## 📞 Support

If users have issues:
- Email sent contains license key
- Can reply to license email for support
- Check spam folder for license email
- Re-send license via Resend dashboard if needed

---

**Implementation Date:** 2025-10-22
**Version:** 1.0.75 (adds licensing system)
**Status:** ✅ Ready for Testing

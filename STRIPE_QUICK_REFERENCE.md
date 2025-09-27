# 🚀 Stripe Quick Reference - VoiceLite

## Your Current Setup

### Test Payment Link
```
https://buy.stripe.com/test_bJe9AT2900gmceb6sDgbm00
```

### Files Updated
- ✅ `landing-page/index.html` - Payment buttons now link to Stripe
- ✅ `landing-page/success.html` - Thank you page after payment
- ✅ `generate-license.ps1` - PowerShell script to generate license keys

## When You Get a Sale

### 1. You'll receive a Stripe email notification
- Subject: "You have a new payment!"
- Shows customer email and amount

### 2. Generate a license key
```powershell
# For Personal license ($29.99)
.\generate-license.ps1 personal

# For Professional license ($59.99)
.\generate-license.ps1 professional

# For Business license ($199.99)
.\generate-license.ps1 business
```

The script will:
- Generate a valid license key
- Copy it to your clipboard
- Show an email template
- Log it to `generated-licenses.log`

### 3. Send the license to customer
Copy the email template from the script output and send to the customer's email

## Testing Your Setup

### Local Testing
1. Open `landing-page/index.html` in browser
2. Click any "Buy" button
3. You'll be redirected to Stripe test checkout
4. Use test card: `4242 4242 4242 4242`
5. Any future date, any CVC, any zip

### Test a License Key
1. Run: `.\generate-license.ps1 personal`
2. Copy the generated key (e.g., `PER-1A2B-3C4D-5E6F-7890`)
3. Open VoiceLite
4. System tray → License...
5. Paste the key and activate

## Deploy to Live Site

### Using Git (Automatic Vercel Deploy)
```bash
cd "C:\Users\mishk\Codingprojects\SpeakLite\HereWeGoAgain v3.2 Licence"
git add landing-page/
git commit -m "Add Stripe payment integration"
git push origin main
```

Vercel will auto-deploy to voicelite.app

### Manual Deploy (If needed)
1. Go to Vercel dashboard
2. Drag and drop `landing-page` folder
3. It will update voicelite.app

## Moving to Production

When ready to accept real payments:

### In Stripe Dashboard
1. Toggle OFF "Test mode" (top right)
2. Create real products with same prices
3. Generate production payment links
4. Update `landing-page/index.html` with production links

### Example Production Links Format
```javascript
const stripeLinks = {
    personal: 'https://buy.stripe.com/live_xxxxx',      // Replace with real
    professional: 'https://buy.stripe.com/live_yyyyy',  // Replace with real
    business: 'https://buy.stripe.com/live_zzzzz'       // Replace with real
};
```

## Current Status

✅ **Ready for Testing**
- Landing page has Stripe test link
- Success page created
- License generation script ready

⏳ **Next Steps**
1. Deploy to voicelite.app
2. Test the full flow
3. Create separate products for each tier in Stripe
4. Switch to production mode when ready

## Support Scripts

### View Generated Licenses
```powershell
Get-Content generated-licenses.log
```

### Test License Activation
```powershell
# Generate test key
.\generate-license.ps1 personal

# The key is automatically copied to clipboard
# Paste into VoiceLite license window
```

## Common Issues & Solutions

**Issue**: Payment link not working
**Solution**: Make sure you're using the full URL including `https://`

**Issue**: License key rejected
**Solution**: Keys must start with PER, PRO, or BUS and follow format XXX-XXXX-XXXX-XXXX-XXXX

**Issue**: Success page not showing email
**Solution**: Stripe needs to be configured to redirect with `?customer_email={email}`

## Contact for Issues
- Stripe Dashboard: https://dashboard.stripe.com
- Test Mode: Always test first!
- Your test link: https://buy.stripe.com/test_bJe9AT2900gmceb6sDgbm00
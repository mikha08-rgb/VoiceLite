# 💳 Stripe Payment Setup for VoiceLite

## Quick Setup (20 minutes)

### Step 1: Create Stripe Account
1. Go to https://stripe.com
2. Click "Start now"
3. Enter email and create password
4. Verify email
5. **Instant approval** - can accept payments immediately!

### Step 2: Create Payment Links

#### Personal License ($29.99)
1. Dashboard → Products → "+ Add product"
2. Name: "VoiceLite Personal License"
3. Price: $29.99 (one-time)
4. Description: "Speech-to-text for Windows - 1 device activation"
5. Click "Create product"
6. Click "Create payment link"
7. Copy the link (like: `https://buy.stripe.com/abc123`)

#### Professional License ($59.99)
1. Dashboard → Products → "+ Add product"
2. Name: "VoiceLite Professional License"
3. Price: $59.99 (one-time)
4. Description: "Speech-to-text for Windows - 3 device activations, commercial use"
5. Click "Create product"
6. Click "Create payment link"
7. Copy the link

#### Business License ($199.99)
1. Dashboard → Products → "+ Add product"
2. Name: "VoiceLite Business License"
3. Price: $199.99 (one-time)
4. Description: "Speech-to-text for Windows - 5 users, unlimited devices"
5. Click "Create product"
6. Click "Create payment link"
7. Copy the link

### Step 3: Update Landing Page

Edit `docs/index.html` and replace test URLs:

**Line 436:**
```html
<!-- OLD -->
<a href="https://buy.stripe.com/test_personal" class="buy-btn">

<!-- NEW -->
<a href="YOUR_PERSONAL_STRIPE_LINK" class="buy-btn">
```

**Line 451:**
```html
<!-- OLD -->
<a href="https://buy.stripe.com/test_professional" class="buy-btn">

<!-- NEW -->
<a href="YOUR_PROFESSIONAL_STRIPE_LINK" class="buy-btn">
```

**Line 466:**
```html
<!-- OLD -->
<a href="https://buy.stripe.com/test_business" class="buy-btn">

<!-- NEW -->
<a href="YOUR_BUSINESS_STRIPE_LINK" class="buy-btn">
```

### Step 4: Configure Payment Success

In Stripe Dashboard:
1. Go to Payment Links
2. Click on each link
3. Under "After payment" → "Don't show confirmation page"
4. Redirect to: `https://yourusername.github.io/voicelite/success.html`

Create `docs/success.html`:
```html
<!DOCTYPE html>
<html>
<head>
    <title>Thank You - VoiceLite</title>
    <style>
        body { font-family: Arial; text-align: center; padding: 50px; }
        .success { color: green; font-size: 24px; }
    </style>
</head>
<body>
    <h1 class="success">✅ Payment Successful!</h1>
    <h2>Thank you for purchasing VoiceLite!</h2>
    <p>You will receive your license key via email within 5 minutes.</p>
    <p>Please check your email: <strong id="email"></strong></p>
    <p>If you don't receive it, please contact: support@voicelite.com</p>

    <script>
        // Get email from URL parameter
        const params = new URLSearchParams(window.location.search);
        document.getElementById('email').textContent = params.get('email') || 'your email';
    </script>
</body>
</html>
```

## Manual License Delivery Process (Temporary)

### When You Get a Sale:

1. **Stripe Sends Email Notification**
   - Subject: "You have a new payment!"
   - Shows customer email and amount

2. **Generate License** (2 minutes)
   ```bash
   cd license-server
   node admin.js generate customer@email.com Personal
   # Output: PERS-XXXX-XXXX-XXXX
   ```

3. **Send License Email**
   ```
   Subject: Your VoiceLite License Key

   Thank you for purchasing VoiceLite!

   Your license information:
   -----------------------
   Email: customer@email.com
   License Type: Personal
   License Key: PERS-XXXX-XXXX-XXXX

   To activate:
   1. Open VoiceLite
   2. Click Help → Enter License
   3. Enter your email and license key
   4. Click Activate

   Download VoiceLite:
   https://yourusername.github.io/voicelite

   If you have any issues, reply to this email.

   Best regards,
   VoiceLite Team
   ```

## Stripe Fees
- **Per transaction:** 2.9% + 30¢
- **Personal ($29.99):** You get $28.82
- **Professional ($59.99):** You get $57.95
- **Business ($199.99):** You get $194.09

## Testing Your Setup

### Test Mode First:
1. Enable "Test mode" in Stripe (toggle in dashboard)
2. Create test payment links
3. Use test card: `4242 4242 4242 4242`
4. Any future date, any CVC
5. Complete a test purchase
6. Verify you receive notification

### Go Live:
1. Toggle off "Test mode"
2. Update landing page with real payment links
3. Make a real $1 test purchase yourself
4. Refund it after testing

## Future Automation (Week 2)

Once you have 10+ sales, automate with:
1. Stripe Webhooks → Your server
2. Auto-generate license on payment
3. Auto-email license to customer
4. Takes 2 hours to set up

## Launch Day Checklist

✅ **Stripe Setup:**
- [ ] Account created and verified
- [ ] 3 products created with prices
- [ ] 3 payment links generated
- [ ] Landing page updated with real links
- [ ] Success page created
- [ ] Test purchase completed
- [ ] Email template ready

✅ **Ready to Sell:**
- [ ] Can accept cards immediately
- [ ] Can process international payments
- [ ] Mobile-friendly checkout
- [ ] Secure PCI-compliant
- [ ] Instant payment notifications

## Quick Reference

**Your Payment Links:**
- Personal: ________________
- Professional: ________________
- Business: ________________

**Support Email Template:**
Save this for quick responses:
```
Thank you for your interest in VoiceLite!

[Answer their question]

Ready to purchase? Choose your plan:
- Personal ($29.99): [your-link]
- Professional ($59.99): [your-link]
- Business ($199.99): [your-link]

Best regards,
[Your name]
```

## You're Ready!

With Stripe configured, you can:
- ✅ Accept payments worldwide
- ✅ Get paid instantly
- ✅ Start selling TODAY!

Next: Deploy your landing page and share your first link!
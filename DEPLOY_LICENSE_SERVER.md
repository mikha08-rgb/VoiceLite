# VoiceLite License Server Deployment Guide

## Step 1: Generate Secure Keys

Run the PowerShell script to generate secure keys:
```powershell
.\generate-secure-keys.ps1
```

Save these keys securely - you'll need them for Railway.

## Step 2: Deploy to Railway

1. **Create Railway Account**
   - Go to https://railway.app
   - Sign up with GitHub

2. **Create New Project**
   - Click "New Project"
   - Choose "Deploy from GitHub repo"
   - Select your VoiceLite repository
   - Point to `/license-server` directory

3. **Set Environment Variables**
   In Railway dashboard, go to Variables and add:
   ```
   API_KEY=<your-generated-api-key>
   ADMIN_KEY=<your-generated-admin-key>
   STRIPE_WEBHOOK_SECRET=<from-stripe-dashboard>
   DATABASE_PATH=/data/licenses.db
   ```

4. **Generate Domain**
   - Go to Settings → Domains
   - Click "Generate Domain"
   - Copy the URL (e.g., `voicelite-license.up.railway.app`)

## Step 3: Configure Stripe Webhook

1. **Go to Stripe Dashboard**
   - Navigate to Developers → Webhooks
   - Click "Add endpoint"

2. **Set Endpoint URL**
   ```
   https://your-railway-domain.up.railway.app/api/webhook/stripe
   ```

3. **Select Events**
   - `checkout.session.completed`
   - `customer.subscription.deleted`
   - `payment_intent.succeeded`

4. **Copy Webhook Secret**
   - Copy the signing secret (starts with `whsec_`)
   - Add to Railway environment variables

## Step 4: Update Your App

1. **Update License Server URL in SimpleLicenseManager.cs**
   ```csharp
   private const string LICENSE_SERVER_URL = "https://your-railway-domain.up.railway.app";
   ```

2. **Update Stripe Checkout Success URL**
   In your Stripe payment link or checkout session:
   ```
   success_url: "https://voicelite.app/success?session_id={CHECKOUT_SESSION_ID}"
   ```

## Step 5: Test the Flow

1. **Generate Test License**
   ```bash
   curl -X POST https://your-railway-domain.up.railway.app/api/generate \
     -H "x-admin-key: YOUR_ADMIN_KEY" \
     -H "Content-Type: application/json" \
     -d '{"email": "test@example.com", "license_type": "Personal"}'
   ```

2. **Validate License**
   ```bash
   curl -X POST https://your-railway-domain.up.railway.app/api/validate \
     -H "x-api-key: YOUR_API_KEY" \
     -H "Content-Type: application/json" \
     -d '{"license_key": "PER-XXXX-XXXX-XXXX", "machine_id": "test-machine"}'
   ```

## Step 6: Update Landing Page

Update `docs/index.html` Stripe link to include metadata:
```javascript
// When creating Stripe checkout session, include:
metadata: {
  license_type: 'Pro' // or 'Personal', 'Business'
}
```

## Security Checklist

- [ ] API keys removed from source code
- [ ] Environment variables set in Railway
- [ ] Stripe webhook secret configured
- [ ] License server URL updated in app
- [ ] Database persisted with Railway volumes
- [ ] HTTPS enforced on all endpoints

## Monitoring

Check server health:
```
https://your-railway-domain.up.railway.app/api/check
```

View logs in Railway dashboard under Deployments → View Logs

## Troubleshooting

### Database not persisting
- Add Railway volume: Settings → Volumes → Mount to `/data`

### Webhook not receiving events
- Check Stripe webhook logs
- Verify endpoint URL is correct
- Ensure webhook secret matches

### License validation failing
- Check API key in app matches server
- Verify server is running (check health endpoint)
- Look at Railway logs for errors
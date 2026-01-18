# VoiceLite Landing Page & Payment System

A simple, production-ready landing page with Stripe payments for VoiceLite.

## Quick Start

### 1. Setup Environment Variables

Copy `.env.local.example` to `.env.local` and fill in your values:

```bash
cp .env.local.example .env.local
```

### 2. Install Dependencies

```bash
npm install
```

### 3. Run Development Server

```bash
npm run dev
```

Visit http://localhost:3000

## Deployment to Vercel

### 1. Deploy to Vercel

```bash
vercel --prod
```

### 2. Add Environment Variables in Vercel Dashboard

Go to your Vercel project settings and add:
- `STRIPE_SECRET_KEY`
- `STRIPE_WEBHOOK_SECRET`
- `STRIPE_PRICE_ID`
- `RESEND_API_KEY`
- `NEXT_PUBLIC_URL`

### 3. Setup Stripe Webhook

1. Go to Stripe Dashboard → Webhooks
2. Add endpoint: `https://yourdomain.com/api/webhook`
3. Select event: `checkout.session.completed`
4. Copy the signing secret to `STRIPE_WEBHOOK_SECRET`

### 4. Connect Custom Domain

In Vercel Dashboard → Settings → Domains, add your domain.

## File Structure

```
├── app/
│   ├── page.tsx          # Landing page
│   ├── api/
│   │   ├── checkout/     # Stripe checkout endpoint
│   │   └── webhook/      # Stripe webhook handler
├── lib/
│   ├── licenses.ts       # License storage (JSON file)
│   └── email.ts          # Email sending with Resend
├── public/
│   └── success.html      # Payment success page
```

## Testing Payments

Use Stripe test cards:
- Success: `4242 4242 4242 4242`
- Decline: `4000 0000 0000 0002`

## Adding Executables

Place your Windows executables in the `public` folder:
- `public/VoiceLite-Free.exe`
- `public/VoiceLite-Pro.exe`

## License Storage

Currently uses JSON file storage (`data/licenses.json`).
Upgrade to database when you have >100 customers.

## Support

Email: basementhustlellc@gmail.com

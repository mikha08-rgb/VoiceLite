# Notes & TODOs (Auth + Payments + Licensing)

- [ ] Provision Supabase Postgres database and supply pooled connection string in `DATABASE_URL`
      (`postgresql://USER:PASSWORD@HOST:PORT/dbname?pgbouncer=true&connection_limit=1`).
- [ ] Set `DIRECT_DATABASE_URL` / `SHADOW_DATABASE_URL` for Prisma migrations (non-pooled connection).
- [ ] Configure Stripe credentials:
      - `STRIPE_SECRET_KEY`
      - `STRIPE_WEBHOOK_SECRET`
      - `STRIPE_QUARTERLY_PRICE_ID` (Quarterly subscription – $20 every 3 months)
      - `STRIPE_LIFETIME_PRICE_ID` (Lifetime license – $99 one-time)
- [ ] Configure Resend (or replacement email provider) via `RESEND_API_KEY`.
- [ ] Define `NEXT_PUBLIC_APP_URL` for production (e.g., https://app.voicelite.com).
- [ ] Confirm custom protocol handler for desktop deep link (`voicelite://auth/callback`).
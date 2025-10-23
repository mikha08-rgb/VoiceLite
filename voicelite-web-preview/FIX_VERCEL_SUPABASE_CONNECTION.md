# Fix Vercel → Supabase Connection

## Problem
Vercel deployment fails with: "Authentication failed against database server, the provided database credentials for 'postgres' are not valid"

## Root Cause
Vercel serverless functions require **connection pooling** (pgBouncer). The direct database URL won't work.

## Solution

### Step 1: Update Environment Variables in Vercel

Run these commands in your terminal:

```bash
cd voicelite-web

# 1. Remove old DATABASE_URL
vercel env rm DATABASE_URL production

# 2. Add new DATABASE_URL with Session Pooler
vercel env add DATABASE_URL production
```

When prompted for the value, paste:
```
postgresql://postgres.lvocjzqjqllouzyggpqm:Gdqp0YThITxbTmhN@aws-0-us-east-1.pooler.supabase.com:6543/postgres?pgbouncer=true&connection_limit=1
```

```bash
# 3. Update DIRECT_DATABASE_URL (keep existing or update if needed)
vercel env rm DIRECT_DATABASE_URL production
vercel env add DIRECT_DATABASE_URL production
```

When prompted for the value, paste:
```
postgresql://postgres:Gdqp0YThITxbTmhN@db.lvocjzqjqllouzyggpqm.supabase.co:5432/postgres
```

### Step 2: Deploy Changes

```bash
# Commit the Prisma schema update
git add prisma/schema.prisma
git commit -m "fix: configure Prisma for Supabase connection pooling"
git push origin master
```

This will automatically trigger a new Vercel deployment.

## Key Changes Explained

### DATABASE_URL (Session Pooler)
- **Host**: `aws-0-us-east-1.pooler.supabase.com`
- **Port**: `6543` (pooler port, NOT 5432)
- **Username**: `postgres.lvocjzqjqllouzyggpqm` (project-specific format)
- **Parameters**: `pgbouncer=true&connection_limit=1`

### DIRECT_DATABASE_URL (Direct Connection)
- Used only for migrations
- **Host**: `db.lvocjzqjqllouzyggpqm.supabase.co`
- **Port**: `5432` (standard PostgreSQL port)
- **Username**: `postgres` (standard format)

### Prisma Schema Update
Added `directUrl` parameter to bypass pooling for migrations:
```prisma
datasource db {
  provider = "postgresql"
  url      = env("DATABASE_URL")
  directUrl = env("DIRECT_DATABASE_URL")
}
```

## Verification

### 1. Check Deployment Status
```bash
vercel ls --next 0
```

### 2. Test Database Connection
Visit: https://voicelite.app/api/health

Should return:
```json
{
  "status": "ok",
  "database": "connected"
}
```

### 3. Test License API
```bash
curl -X POST https://voicelite.app/api/license/validate \
  -H "Content-Type: application/json" \
  -d '{"licenseKey": "your-test-key", "machineId": "test-machine"}'
```

### 4. Test Stripe Webhook
Make a test purchase at: https://voicelite.app/checkout

Check Stripe Dashboard → Developers → Webhooks → Events

## Troubleshooting

### Still Getting Auth Errors?
1. **Verify password**: Double-check the password in Supabase Dashboard → Settings → Database
2. **Check pooler endpoint**: Should be `aws-0-us-east-1` (or your region)
3. **Confirm IPv4 addon**: Supabase → Project Settings → Add-ons → IPv4 should be enabled

### Connection Timeout?
- Supabase Pro plan required for production use
- IPv4 add-on must be enabled ($4/month)
- Check Supabase service status

### Migration Errors?
- Migrations use `DIRECT_DATABASE_URL`, not `DATABASE_URL`
- Run locally: `npm run db:push` to test
- Migrations should run on direct connection (port 5432)

## Reference Links
- [Supabase + Vercel Guide](https://supabase.com/docs/guides/integrations/vercel)
- [Prisma Connection Pooling](https://www.prisma.io/docs/guides/performance-and-optimization/connection-management#connection-pooling)
- [Vercel Serverless Functions](https://vercel.com/docs/functions/serverless-functions)

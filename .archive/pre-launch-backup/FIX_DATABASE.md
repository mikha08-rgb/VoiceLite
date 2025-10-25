# CRITICAL: Database Schema Mismatch

## THE ACTUAL PROBLEM

The webhook is failing because:
```
The column `License.email` does not exist in the current database.
```

The code expects a `License.email` column, but your Supabase production database doesn't have it!

## How to Fix - Run This SQL on Supabase

1. **Go to Supabase Dashboard:**
   - https://supabase.com/dashboard/project/lvocjzqjqllouzyggpqm
   - Click "SQL Editor"

2. **Run this SQL:**

```sql
-- Add email column to License table
ALTER TABLE "License" ADD COLUMN IF NOT EXISTS "email" TEXT NOT NULL DEFAULT '';

-- Add index on email for faster lookups
CREATE INDEX IF NOT EXISTS "License_email_idx" ON "License"("email");

-- Update existing records (if any) with placeholder email
-- You may need to update these based on Stripe data
UPDATE "License" SET "email" = 'unknown@example.com' WHERE "email" = '';
```

3. **Verify the fix:**

After running the SQL, test:
```bash
curl -X POST https://voicelite.app/api/licenses/resend-email \
  -H "Content-Type: application/json" \
  -d '{"email":"mikhail.lev08@gmail.com"}'
```

Should no longer return "column does not exist" error.

## Why This Happened

The Prisma migrations in `prisma/migrations/` were never applied to your production Supabase database. The migrations exist locally but weren't run on the live database.

## Alternative: Run Migrations from Vercel

If you have access to run commands on Vercel:

1. In Vercel dashboard → your project → Settings → Environment Variables
2. Make sure these are set for **Production**:
   - `DATABASE_URL`
   - `DIRECT_DATABASE_URL`

3. Then trigger a build that runs migrations:
   - Add this to `package.json` scripts:
     ```json
     "vercel-build": "prisma generate && prisma migrate deploy && next build"
     ```
   - Commit and push

## After Database is Fixed

Once the `email` column exists:

1. **Test the resend endpoint:**
   ```bash
   curl -X POST https://voicelite.app/api/licenses/resend-email \
     -H "Content-Type: application/json" \
     -d '{"email":"mikhail.lev08@gmail.com"}'
   ```

2. **Make a new test payment:**
   - Go to https://voicelite.app
   - Click "Get Pro"
   - Complete payment
   - Email should arrive!

## Why Emails Weren't Sent

1. ❌ Webhook received event
2. ❌ Tried to create license record in database
3. ❌ Database query failed: "column License.email does not exist"
4. ❌ Webhook returned "Processing error"
5. ❌ No email sent

After fixing the database:

1. ✅ Webhook receives event
2. ✅ Creates license record in database (email column exists)
3. ✅ Sends email via Resend
4. ✅ Customer gets license key!

---

**IMMEDIATE ACTION:** Run the SQL above in Supabase SQL Editor

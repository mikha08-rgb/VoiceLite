# THE ROOT CAUSE - DATABASE SCHEMA MISMATCH!

## What's Happening

**Error**: `Null constraint violation on the fields: (userId)`

Your **production database** has a `userId` column on the `License` table that is marked as **NOT NULL** (required).

But your **Prisma schema** (the code) does NOT have a `userId` field.

This means:
- Old database schema (with userId column)
- New code (without userId field)
- **SCHEMA MISMATCH** = Every insert fails!

## Why This Happened

Looking at your schema file, I see it was simplified (line 1 comment):
```
// Prisma schema for VoiceLite licensing (simplified - removed auth, analytics, unused models)
```

Someone removed the `userId` field from the schema to simplify it (no user accounts needed), but **the database was never migrated** to remove that column!

## The Fix

You have TWO options:

### Option 1: Make userId Optional in Database (RECOMMENDED)

Run this SQL migration:

```sql
ALTER TABLE "License" ALTER COLUMN "userId" DROP NOT NULL;
```

This makes the `userId` column optional so inserts can work without it.

### Option 2: Add userId Back to Schema (Not Recommended)

Add this to your Prisma schema:

```prisma
model License {
  ...existing fields...
  userId  String?  // Make it optional
}
```

Then run `npx prisma migrate dev` and `npx prisma generate`.

## How to Apply Option 1 (Quick Fix)

I'll create a migration SQL file that you can run via Supabase dashboard or Prisma.

1. **Via Supabase Dashboard:**
   - Go to https://supabase.com/dashboard
   - Find your project
   - Go to SQL Editor
   - Paste and run:
   ```sql
   ALTER TABLE "License" ALTER COLUMN "userId" DROP NOT NULL;
   ```

2. **Via Prisma:**
   I'll create a migration file for you.

Once this is done, all your webhooks will work and I can create all 8 licenses for you!

---

**This is why 0 licenses were created - every single webhook has been failing with this database constraint error for hours!**

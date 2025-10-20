# Analytics Setup - Next Steps

## ✅ Completed So Far

1. ✅ **Upstash Redis configured**
   - URL: `https://game-honeybee-16063.upstash.io`
   - Token: Added to `.env.local`

2. ✅ **Environment file created**
   - File: `voicelite-web/.env.local`
   - Upstash credentials: ✅ Added
   - Admin emails: ⚠️ Need to update

3. ✅ **Prisma client generated**
   - Analytics schema included
   - Ready for migration

## ⚠️ Action Required - Database Setup

### Step 1: Add Database Credentials

You need to update `voicelite-web/.env.local` with your actual database URLs.

**File to edit**: `voicelite-web/.env.local`

**Lines to update** (lines 10-11):
```env
# Replace these placeholder values:
DATABASE_URL="postgresql://postgres:[PASSWORD]@db.[PROJECT-REF].supabase.co:5432/postgres"
DIRECT_DATABASE_URL="postgresql://postgres:[PASSWORD]@db.[PROJECT-REF].supabase.co:5432/postgres"

# With your actual Supabase database URLs (should look like):
DATABASE_URL="postgresql://postgres.abcd1234xyz.supabase.co:5432/postgres?pgbouncer=true&connection_limit=1"
DIRECT_DATABASE_URL="postgresql://postgres:your_password@db.abcd1234xyz.supabase.co:5432/postgres"
```

**Where to find these:**
1. Go to https://app.supabase.com
2. Select your VoiceLite project
3. Go to Settings → Database
4. Copy:
   - **Connection string** (pooled) → `DATABASE_URL`
   - **Connection string** (direct) → `DIRECT_DATABASE_URL`

### Step 2: Update Admin Email

**File to edit**: `voicelite-web/.env.local`

**Line to update** (line 47):
```env
# Replace:
ADMIN_EMAILS="your-email@example.com"

# With your actual email:
ADMIN_EMAILS="your-actual-email@gmail.com"
```

### Step 3: Run Migration

Once you've updated the database URLs and admin email:

```bash
cd voicelite-web

# Run migration
npx prisma migrate dev --name add_analytics_events

# Expected output:
# ✔ Migration has been created successfully
# ✔ Database synchronized with Prisma schema
```

This will create the `AnalyticsEvent` table in your database.

### Step 4: Verify Migration

```bash
cd voicelite-web

# Open Prisma Studio to verify
npx prisma studio

# Look for:
# - AnalyticsEvent table (should exist)
# - AnalyticsEventType enum (should exist)
# - TierType enum (should exist)
```

## 📋 After Database Setup

Once migration completes, continue with:

1. **Update Privacy Policy** (5 min)
   - File: `voicelite-web/app/privacy/page.tsx`
   - See `ANALYTICS_SETUP_GUIDE.md` Step 3.1 for content

2. **Update README** (3 min)
   - File: `README.md`
   - See `ANALYTICS_SETUP_GUIDE.md` Step 3.2 for content

3. **Test Desktop App** (5 min)
   ```bash
   dotnet build VoiceLite/VoiceLite.sln -c Release
   dotnet run --project VoiceLite/VoiceLite/VoiceLite.csproj
   ```
   - Consent dialog should appear
   - Click "Enable Analytics"
   - Verify Settings → Privacy tab works

4. **Deploy to Vercel** (5 min)
   ```bash
   cd voicelite-web

   # Add env vars to Vercel
   vercel env add UPSTASH_REDIS_REST_URL
   # Paste: https://game-honeybee-16063.upstash.io

   vercel env add UPSTASH_REDIS_REST_TOKEN
   # Paste: [REDACTED-UPSTASH-TOKEN]

   vercel env add ADMIN_EMAILS
   # Paste: your-email@example.com

   # Deploy
   vercel deploy --prod
   ```

5. **Test Analytics Flow** (5 min)
   - Launch desktop app
   - Enable analytics
   - Make a transcription
   - Check admin API: `https://voicelite.app/api/admin/analytics?days=1`

## 🆘 Troubleshooting

### "Environment variable not found: DATABASE_URL"
**Solution**: You're seeing this now. Update `.env.local` with your Supabase credentials.

### "Cannot connect to database"
**Solution**:
1. Check your Supabase project is active
2. Verify database password is correct
3. Try the direct URL first (without pgbouncer)

### Where to get Supabase credentials?
1. Go to https://app.supabase.com
2. Project Settings → Database
3. Copy "Connection string" values

## ✅ Progress Checklist

- [x] Upstash Redis configured
- [x] `.env.local` created
- [x] Prisma client generated
- [ ] Database URLs added to `.env.local` ← **YOU ARE HERE**
- [ ] Admin email updated in `.env.local`
- [ ] Migration run successfully
- [ ] Privacy policy updated
- [ ] README updated
- [ ] Desktop app tested
- [ ] Backend deployed
- [ ] Analytics verified working

## 📚 Resources

- [ANALYTICS_SETUP_GUIDE.md](ANALYTICS_SETUP_GUIDE.md) - Full step-by-step guide
- [ANALYTICS_QUICK_START.md](ANALYTICS_QUICK_START.md) - Quick reference
- [ANALYTICS_IMPLEMENTATION_REVIEW.md](ANALYTICS_IMPLEMENTATION_REVIEW.md) - Technical details

---

**Next Action**: Update `voicelite-web/.env.local` with your Supabase database URLs and admin email, then run the migration!

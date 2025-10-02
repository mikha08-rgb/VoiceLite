# Analytics Quick Start - Do This Now

**Time Required**: 20-30 minutes
**Status**: Implementation complete, setup needed

---

## 🎯 What You Need to Do (In Order)

### 1️⃣ Get Upstash Redis (10 min)

**Why**: Rate limiting for analytics API

1. Go to https://upstash.com/
2. Sign up (free tier works)
3. Create new database (Redis)
4. Copy these two values:
   - `UPSTASH_REDIS_REST_URL`
   - `UPSTASH_REDIS_REST_TOKEN`

---

### 2️⃣ Set Environment Variables (2 min)

Create `voicelite-web/.env.local`:

```bash
cd voicelite-web

# Create .env.local
cat > .env.local << 'EOF'
# Your existing DATABASE_URL should already be here
DATABASE_URL="postgresql://..."
DIRECT_DATABASE_URL="postgresql://..."

# NEW - Add these for analytics:
UPSTASH_REDIS_REST_URL="https://YOUR-REDIS.upstash.io"
UPSTASH_REDIS_REST_TOKEN="YOUR_TOKEN_HERE"
ADMIN_EMAILS="your-email@example.com"
EOF
```

**Important**: Replace placeholders with real values from Step 1

---

### 3️⃣ Run Database Migration (5 min)

```bash
cd voicelite-web

# Generate Prisma client
npx prisma generate

# Create analytics table
npx prisma migrate dev --name add_analytics_events

# You should see: ✔ Database synchronized
```

---

### 4️⃣ Update Privacy Policy (5 min)

**File**: `voicelite-web/app/privacy/page.tsx`

Add this section (copy from [ANALYTICS_SETUP_GUIDE.md](ANALYTICS_SETUP_GUIDE.md), Step 3.1)

**Key points to include**:
- What analytics tracks (app version, OS, model, daily count)
- What analytics DOESN'T track (voice, transcriptions, PII)
- Opt-in consent required
- Users can disable anytime

---

### 5️⃣ Update README (3 min)

**File**: `README.md`

Add FAQ entries (copy from [ANALYTICS_SETUP_GUIDE.md](ANALYTICS_SETUP_GUIDE.md), Step 3.2):
- "Does VoiceLite collect any data?"
- Update "Does it need internet?"

---

### 6️⃣ Deploy Backend (3 min)

```bash
cd voicelite-web

# If using Vercel:
vercel env add UPSTASH_REDIS_REST_URL
vercel env add UPSTASH_REDIS_REST_TOKEN
vercel env add ADMIN_EMAILS

vercel deploy --prod
```

---

### 7️⃣ Test Desktop App (5 min)

```bash
# Build release
dotnet build VoiceLite/VoiceLite.sln -c Release

# Run it
dotnet run --project VoiceLite/VoiceLite/VoiceLite.csproj

# Expected:
# 1. Consent dialog appears after 1 sec
# 2. Click "Enable Analytics"
# 3. Check Settings → Privacy → See toggle
```

---

### 8️⃣ Verify Events (3 min)

After enabling analytics:

1. Make a transcription
2. Check admin API:
   ```bash
   curl https://voicelite.app/api/admin/analytics?days=1 \
     -H "Cookie: session=YOUR_SESSION"
   ```
3. Should see:
   - `APP_LAUNCHED` event
   - `TRANSCRIPTION_COMPLETED` event

---

## ✅ Success Criteria

You'll know everything works when:

- [x] Upstash Redis dashboard shows requests
- [x] Database has `AnalyticsEvent` table
- [x] Desktop app shows consent dialog on first run
- [x] Admin API returns analytics data
- [x] Privacy policy mentions analytics
- [x] README FAQ explains data collection

---

## 🆘 Common Issues

### "Cannot find module '@upstash/ratelimit'"
**Fix**: Already installed, just restart dev server

### "Prisma Client not found"
**Fix**: `npx prisma generate`

### Consent dialog doesn't show
**Fix**: Delete `%APPDATA%\VoiceLite\settings.json`

### Events not in database
**Fix**: Check logs at `%APPDATA%\VoiceLite\logs\voicelite.log`

---

## 📚 Full Documentation

- [ANALYTICS_SETUP_GUIDE.md](ANALYTICS_SETUP_GUIDE.md) - Complete step-by-step
- [ANALYTICS_IMPLEMENTATION_REVIEW.md](ANALYTICS_IMPLEMENTATION_REVIEW.md) - Technical review
- [CLAUDE.md](CLAUDE.md) - Project architecture

---

## 🎉 After Setup

Once everything is working:

1. **Monitor**: Check admin dashboard daily for first week
2. **Analyze**: Look for version adoption, model preferences
3. **Iterate**: Use insights to improve VoiceLite
4. **Privacy**: Ensure users feel informed and in control

**Expected Metrics** (after 30 days with 1000 users):
- Consent rate: 40-60%
- DAU/MAU: 0.3-0.5
- Events/day: 5,000-10,000
- Storage: 1-2MB/month
- Cost: $0-5/month

---

That's it! Start with Step 1 and work your way down. Each step is quick and the whole process takes about 30 minutes.

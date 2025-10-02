# Analytics Setup Guide - Complete Checklist

This guide walks you through the **complete setup** to get analytics fully working in VoiceLite.

---

## 📋 Prerequisites

- ✅ Backend code completed (done)
- ✅ Desktop app code completed (done)
- ✅ Build passes (verified)
- ⚠️ Database migration needed
- ⚠️ Environment variables needed
- ⚠️ Legal docs update needed

**Estimated Time**: 30-45 minutes

---

## Step 1: Backend Setup (20 minutes)

### 1.1 Setup Upstash Redis (10 min)

Analytics requires rate limiting to prevent abuse. You need a Redis instance from Upstash.

**Actions:**
1. Go to https://upstash.com/
2. Sign up/login (free tier is fine)
3. Click "Create Database"
4. Choose a region close to your users
5. Copy the REST URL and REST Token
6. Save these for Step 1.3

**Example:**
```
UPSTASH_REDIS_REST_URL=https://us1-steady-firefly-12345.upstash.io
UPSTASH_REDIS_REST_TOKEN=AXXXxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

### 1.2 Create/Update Environment Variables (2 min)

**File**: `voicelite-web/.env.local` (create if doesn't exist)

```bash
cd voicelite-web

# Create .env.local file
cat > .env.local << 'EOF'
# Database (you should already have these)
DATABASE_URL="your_postgres_connection_string"
DIRECT_DATABASE_URL="your_postgres_direct_connection_string"

# Upstash Redis (for rate limiting)
UPSTASH_REDIS_REST_URL="https://your-redis.upstash.io"
UPSTASH_REDIS_REST_TOKEN="your_redis_token_here"

# Admin Access (for analytics dashboard)
ADMIN_EMAILS="your-email@example.com"

# Stripe (you should already have these)
STRIPE_SECRET_KEY="sk_test_..."
NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY="pk_test_..."
STRIPE_WEBHOOK_SECRET="whsec_..."

# Other existing env vars...
EOF
```

**Note**: Replace placeholder values with your actual credentials.

### 1.3 Run Database Migration (5 min)

This creates the `AnalyticsEvent` table and enums in your database.

```bash
cd voicelite-web

# Generate Prisma client with new schema
npx prisma generate

# Create and apply migration
npx prisma migrate dev --name add_analytics_events

# Verify migration succeeded
npx prisma studio
# → Check if "AnalyticsEvent" table exists
```

**Expected Output:**
```
✔ Generated Prisma Client
✔ The migration has been created successfully
✔ Database synchronized with Prisma schema
```

### 1.4 Deploy Backend Changes (3 min)

If using Vercel:

```bash
cd voicelite-web

# Set environment variables in Vercel
vercel env add UPSTASH_REDIS_REST_URL
vercel env add UPSTASH_REDIS_REST_TOKEN
vercel env add ADMIN_EMAILS

# Deploy
vercel deploy --prod
```

**Alternative (manual deployment):**
1. Commit changes: `git add . && git commit -m "Add analytics backend"`
2. Push: `git push origin main`
3. Vercel auto-deploys (if connected)
4. Add environment variables in Vercel dashboard

---

## Step 2: Desktop App Setup (5 minutes)

### 2.1 Verify Build (1 min)

```bash
# From project root
dotnet build VoiceLite/VoiceLite.sln -c Release

# Expected: Build succeeded. 0 Warning(s)
```

### 2.2 Test Consent Flow Locally (4 min)

Before releasing, test the analytics consent flow:

```bash
# Run the app
dotnet run --project VoiceLite/VoiceLite/VoiceLite.csproj
```

**Test Steps:**
1. ✅ Delete `%APPDATA%\VoiceLite\settings.json` (reset settings)
2. ✅ Launch VoiceLite
3. ✅ Consent dialog should appear after ~1 second
4. ✅ Click "Enable Analytics"
5. ✅ Close app
6. ✅ Check `%APPDATA%\VoiceLite\settings.json`:
   ```json
   {
     "EnableAnalytics": true,
     "AnonymousUserId": "a1b2c3d4...",
     "AnalyticsConsentDate": "2025-10-02T..."
   }
   ```
7. ✅ Re-launch app → Consent dialog should NOT appear (already consented)
8. ✅ Open Settings → Privacy tab → See checkbox checked
9. ✅ Uncheck analytics → Save → Verify `EnableAnalytics: false` in settings.json

**If any step fails**: Check error logs in `%APPDATA%\VoiceLite\logs\voicelite.log`

---

## Step 3: Update Legal Documents (10 minutes)

### 3.1 Update Privacy Policy (REQUIRED)

**File**: `voicelite-web/app/privacy/page.tsx`

Add this section after the existing privacy content:

```tsx
{/* Add after existing sections */}
<section className="mb-8">
  <h2 className="text-2xl font-bold mb-4">Anonymous Usage Analytics</h2>

  <p className="mb-4">
    VoiceLite offers optional anonymous analytics to help us improve the application.
    This feature is completely opt-in and can be disabled at any time.
  </p>

  <h3 className="text-xl font-semibold mb-2">What We Track (If You Opt In)</h3>
  <ul className="list-disc pl-6 mb-4 space-y-1">
    <li>App version (e.g., v1.0.16)</li>
    <li>Operating system version (e.g., Windows 11)</li>
    <li>Whisper model used (Lite, Pro, Elite, Ultra)</li>
    <li>Daily transcription count (aggregated, no content)</li>
    <li>Anonymous user ID (SHA256 hash, irreversible)</li>
    <li>App launches and feature usage events</li>
  </ul>

  <h3 className="text-xl font-semibold mb-2">What We Do NOT Track</h3>
  <ul className="list-disc pl-6 mb-4 space-y-1">
    <li>Voice recordings or audio data</li>
    <li>Transcription text or content</li>
    <li>Personally identifiable information (name, email for free users)</li>
    <li>Keystrokes, screen content, or other sensitive data</li>
    <li>IP addresses (free tier only - anonymized for geo analytics)</li>
  </ul>

  <h3 className="text-xl font-semibold mb-2">Your Control</h3>
  <p className="mb-4">
    Analytics are opt-in only. You will be asked for consent on first launch, and you can:
  </p>
  <ul className="list-disc pl-6 mb-4 space-y-1">
    <li>Decline analytics and use VoiceLite 100% offline</li>
    <li>Enable or disable analytics anytime in Settings → Privacy</li>
    <li>Request deletion of your analytics data by contacting support</li>
  </ul>

  <h3 className="text-xl font-semibold mb-2">Data Retention</h3>
  <p className="mb-4">
    Anonymous analytics data is retained for up to 90 days for trend analysis,
    then automatically deleted. No analytics data is shared with third parties.
  </p>
</section>
```

### 3.2 Update README.md (5 min)

**File**: `README.md`

Update the FAQ section:

```markdown
## ❓ FAQ

<!-- Existing FAQs -->

<details>
<summary><b>Does VoiceLite collect any data?</b></summary>

**Voice Processing**: 100% offline. Your voice recordings and transcriptions NEVER leave your computer.

**Optional Analytics**: VoiceLite offers opt-in anonymous analytics to help us improve the app. On first launch, you'll be asked if you want to enable this.

**What analytics track** (if you opt in):
- App version and OS version
- Model used (Lite/Pro/Elite/Ultra)
- Daily transcription count (no content)
- Anonymous user ID (SHA256 hash)

**What analytics DON'T track**:
- Voice recordings or audio
- Transcription text/content
- Personal information (name, email)
- Keystrokes or screen content

You can disable analytics anytime in Settings → Privacy.
</details>

<details>
<summary><b>Does it need internet?</b></summary>

**For voice transcription**: No! VoiceLite processes your voice 100% offline using local Whisper AI.

**For optional features**:
- Pro tier license validation (one-time, then works offline)
- Optional anonymous analytics (if you opt in)

You can use VoiceLite completely offline by declining analytics and using the free tier.
</details>
```

---

## Step 4: Build & Release (5 minutes)

### 4.1 Update Version Number

**File**: `VoiceLite/VoiceLite/VoiceLite.csproj`

```xml
<Version>1.0.17</Version>
<AssemblyVersion>1.0.17.0</AssemblyVersion>
<FileVersion>1.0.17.0</FileVersion>
```

### 4.2 Build Release Installer

```bash
# Publish release build
dotnet publish VoiceLite/VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained

# Build installer (if you have Inno Setup)
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" VoiceLite\Installer\VoiceLiteSetup_Simple.iss

# Output: VoiceLite-Setup-1.0.17.exe
```

### 4.3 Test Installer

1. Install on clean machine (or VM)
2. Launch VoiceLite
3. Verify consent dialog appears
4. Test enabling/disabling analytics
5. Make a transcription
6. Check admin dashboard for events

---

## Step 5: Verify Analytics Flow (5 minutes)

### 5.1 Test Event Tracking

After deploying and installing:

1. **Enable analytics** in desktop app
2. **Make a transcription**
3. **Check backend API**:

```bash
# Get your admin session cookie first
# Then make request to admin analytics endpoint

curl -X GET \
  'https://voicelite.app/api/admin/analytics?days=1' \
  -H 'Cookie: session=your_session_cookie' \
  | jq
```

**Expected Response:**
```json
{
  "overview": {
    "totalEvents": 2,
    "dailyActiveUsers": 1,
    "monthlyActiveUsers": 1
  },
  "events": {
    "byType": {
      "APP_LAUNCHED": 1,
      "TRANSCRIPTION_COMPLETED": 1
    }
  },
  "users": {
    "tierDistribution": {
      "FREE": 1
    }
  },
  "versions": {
    "distribution": [
      { "version": "1.0.17.0", "count": 2 }
    ]
  }
}
```

### 5.2 Test Rate Limiting

```bash
# Send 101 events rapidly (should get rate limited)
for i in {1..101}; do
  curl -X POST https://voicelite.app/api/analytics/event \
    -H "Content-Type: application/json" \
    -d '{
      "anonymousUserId": "test-user-123",
      "eventType": "APP_LAUNCHED",
      "tier": "FREE",
      "appVersion": "1.0.17.0"
    }'
done

# Event 101 should return: 429 Too Many Requests
```

---

## Step 6: Optional - Admin Dashboard UI (30 min)

**Note**: This is optional. The API works without the UI.

Create `voicelite-web/app/admin/analytics/page.tsx`:

```tsx
'use client';

import { useEffect, useState } from 'react';

interface AnalyticsData {
  overview: {
    totalEvents: number;
    dailyActiveUsers: number;
    monthlyActiveUsers: number;
    dau_mau_ratio: string;
  };
  events: {
    byType: Record<string, number>;
  };
  users: {
    tierDistribution: Record<string, number>;
  };
  versions: {
    distribution: Array<{ version: string; count: number }>;
  };
  models: {
    distribution: Array<{ model: string; count: number }>;
  };
}

export default function AnalyticsPage() {
  const [data, setData] = useState<AnalyticsData | null>(null);
  const [days, setDays] = useState(30);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetch(`/api/admin/analytics?days=${days}`)
      .then(res => res.json())
      .then(setData)
      .catch(console.error)
      .finally(() => setLoading(false));
  }, [days]);

  if (loading) return <div className="p-8">Loading...</div>;
  if (!data) return <div className="p-8">No data available</div>;

  return (
    <div className="container mx-auto p-8">
      <h1 className="text-3xl font-bold mb-8">VoiceLite Analytics</h1>

      {/* Date Range Selector */}
      <div className="mb-6">
        <select
          value={days}
          onChange={(e) => setDays(Number(e.target.value))}
          className="px-4 py-2 border rounded"
        >
          <option value={7}>Last 7 Days</option>
          <option value={30}>Last 30 Days</option>
          <option value={90}>Last 90 Days</option>
        </select>
      </div>

      {/* Overview Cards */}
      <div className="grid grid-cols-4 gap-4 mb-8">
        <Card title="Total Events" value={data.overview.totalEvents} />
        <Card title="DAU" value={data.overview.dailyActiveUsers} />
        <Card title="MAU" value={data.overview.monthlyActiveUsers} />
        <Card title="DAU/MAU" value={data.overview.dau_mau_ratio} />
      </div>

      {/* Events by Type */}
      <div className="mb-8">
        <h2 className="text-2xl font-bold mb-4">Events by Type</h2>
        <div className="space-y-2">
          {Object.entries(data.events.byType).map(([type, count]) => (
            <div key={type} className="flex justify-between p-3 bg-gray-50 rounded">
              <span>{type}</span>
              <span className="font-bold">{count}</span>
            </div>
          ))}
        </div>
      </div>

      {/* Tier Distribution */}
      <div className="mb-8">
        <h2 className="text-2xl font-bold mb-4">Free vs Pro</h2>
        <div className="space-y-2">
          {Object.entries(data.users.tierDistribution).map(([tier, count]) => (
            <div key={tier} className="flex justify-between p-3 bg-gray-50 rounded">
              <span>{tier}</span>
              <span className="font-bold">{count}</span>
            </div>
          ))}
        </div>
      </div>

      {/* Version Distribution */}
      <div className="mb-8">
        <h2 className="text-2xl font-bold mb-4">Version Distribution</h2>
        <div className="space-y-2">
          {data.versions.distribution.map(({ version, count }) => (
            <div key={version} className="flex justify-between p-3 bg-gray-50 rounded">
              <span>v{version}</span>
              <span className="font-bold">{count} events</span>
            </div>
          ))}
        </div>
      </div>

      {/* Model Usage */}
      <div className="mb-8">
        <h2 className="text-2xl font-bold mb-4">Model Usage</h2>
        <div className="space-y-2">
          {data.models.distribution.map(({ model, count }) => (
            <div key={model} className="flex justify-between p-3 bg-gray-50 rounded">
              <span>{model}</span>
              <span className="font-bold">{count} events</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function Card({ title, value }: { title: string; value: number | string }) {
  return (
    <div className="p-6 bg-white border rounded-lg shadow">
      <div className="text-sm text-gray-600 mb-2">{title}</div>
      <div className="text-3xl font-bold">{value}</div>
    </div>
  );
}
```

---

## ✅ Final Checklist

Before marking analytics as "complete":

### Backend
- [ ] Upstash Redis account created
- [ ] Environment variables set (.env.local)
- [ ] Database migration run successfully
- [ ] Backend deployed to production
- [ ] Test API endpoint: `POST /api/analytics/event`
- [ ] Test admin endpoint: `GET /api/admin/analytics`

### Desktop App
- [ ] Build succeeds (`dotnet build`)
- [ ] Consent dialog tested
- [ ] Analytics toggle in Settings tested
- [ ] Events are being sent (check logs)
- [ ] Version number updated to 1.0.17
- [ ] Installer built and tested

### Legal
- [ ] Privacy policy updated with analytics disclosure
- [ ] README FAQ updated
- [ ] Terms of service reviewed (if applicable)

### Testing
- [ ] Fresh install shows consent dialog
- [ ] "Enable Analytics" sends events
- [ ] "No Thanks" prevents all events
- [ ] Settings toggle works
- [ ] Events appear in admin dashboard
- [ ] Rate limiting works (101st request fails)

### Documentation
- [ ] CLAUDE.md updated (add AnalyticsService to services list)
- [ ] ANALYTICS_IMPLEMENTATION_REVIEW.md reviewed
- [ ] This setup guide saved for reference

---

## 🚀 You're Done!

After completing all steps above, your analytics system is **fully operational**:

- ✅ Users see consent dialog on first run
- ✅ Anonymous events tracked (if opted in)
- ✅ Admin dashboard shows real-time metrics
- ✅ Rate limiting prevents abuse
- ✅ Privacy policy legally compliant
- ✅ Users have full control

**Next**: Monitor the admin dashboard over the next few days to see usage patterns emerge!

---

## 🆘 Troubleshooting

### "Rate limit error" when testing
- **Cause**: Upstash Redis not configured
- **Fix**: Set `UPSTASH_REDIS_REST_URL` and `UPSTASH_REDIS_REST_TOKEN`

### "Prisma Client not found"
- **Cause**: Migration not run
- **Fix**: `npx prisma generate && npx prisma migrate dev`

### Consent dialog doesn't appear
- **Cause**: Settings already exist
- **Fix**: Delete `%APPDATA%\VoiceLite\settings.json` and restart

### Events not appearing in database
- **Cause**: Desktop app can't reach backend
- **Fix**: Check `%APPDATA%\VoiceLite\logs\voicelite.log` for errors

### "Unauthorized" on admin endpoint
- **Cause**: Not logged in as admin
- **Fix**: Set your email in `ADMIN_EMAILS` env var, then login to voicelite.app

---

**Need help?** Check the error logs:
- Desktop: `%APPDATA%\VoiceLite\logs\voicelite.log`
- Backend: Vercel dashboard → Functions → Logs

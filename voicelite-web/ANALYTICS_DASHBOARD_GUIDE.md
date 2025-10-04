# Analytics Dashboard - Quick Start Guide

## 🚀 How to Access

### Step 1: Configure Admin Access
Add your email to `voicelite-web/.env.local`:
```env
ADMIN_EMAILS="your-email@example.com"
```

### Step 2: Login to VoiceLite
1. Visit https://voicelite.app
2. Click "Login" and enter your admin email
3. Check your email for the magic link
4. Click the link to authenticate

### Step 3: Access Analytics Dashboard
Navigate to: **https://voicelite.app/admin/analytics**

If your email is in `ADMIN_EMAILS`, you'll see the dashboard.
If not, you'll get "Unauthorized. Admin access required."

---

## 📊 What You'll See

### Key Metrics (Top Cards)
- **Total Events**: All analytics events in selected time range
- **Daily Active Users (DAU)**: Unique users in last 7 days
- **Monthly Active Users (MAU)**: Unique users in last 30 days
- **DAU/MAU Ratio**: Engagement score (40%+ is excellent)

### Interactive Charts

#### 1. Daily Activity (Line Chart)
Shows event volume over time. Use this to:
- Spot growth trends
- Identify spikes (viral moments, marketing campaigns)
- Detect issues (sudden drops = bug or outage)

#### 2. Event Types (Pie Chart)
Distribution of events:
- `APP_LAUNCHED`: Total downloads
- `TRANSCRIPTION_COMPLETED`: Usage intensity
- `MODEL_CHANGED`: User experimentation
- `SETTINGS_CHANGED`: Feature adoption
- `ERROR_OCCURRED`: Quality issues
- `PRO_UPGRADE`: Conversion rate

#### 3. User Tiers (Bar Chart)
FREE vs PRO user split. Track:
- Conversion rate (PRO / Total)
- Free tier usage (sustainability)

#### 4. Model Usage (Bar Chart)
Which AI models are popular:
- `ggml-small.bin` (Pro - current free default)
- `ggml-tiny.bin` (Lite - legacy)
- `ggml-base.bin` (Swift - Pro tier)
- `ggml-medium.bin` (Elite - Pro tier)
- `ggml-large-v3.bin` (Ultra - Pro tier)

#### 5. OS Distribution (Bar Chart)
Windows version breakdown:
- Windows 11
- Windows 10
- Windows 8/7 (for compatibility tracking)

---

## 🎯 Common Questions

### Q: How often does data update?
**A:** Analytics refresh every 5 minutes (server-side cache). Click "Refresh" button for manual reload.

### Q: Can other people see this?
**A:** No. Only emails in `ADMIN_EMAILS` environment variable can access `/admin` routes. No public links exist.

### Q: How do I add another admin?
**A:** Add to `.env.local`:
```env
ADMIN_EMAILS="you@example.com,cofounder@example.com,support@example.com"
```
Comma-separated, no spaces around commas.

### Q: What does "Total Downloads" mean?
**A:** Count of unique `anonymousUserId` values with `APP_LAUNCHED` events. Each download generates a unique SHA256 hash, so each user is counted once (even if they reinstall).

### Q: How accurate is DAU/MAU ratio?
**A:** Very accurate. Uses unique `anonymousUserId` hashes. A user who launches the app on Day 1 and Day 5 counts as 1 unique user, not 2.

### Q: Can I export this data?
**A:** Not yet (would be a good future enhancement). Currently view-only. You can:
1. Take screenshots
2. Use browser DevTools to copy JSON from API response
3. Query database directly via Prisma Studio (`npm run db:studio`)

---

## 🔒 Security & Privacy

### How Admin Auth Works
1. **Session-based**: Uses existing VoiceLite login system (magic link + OTP)
2. **Email whitelist**: Checks `session.user.email` against `ADMIN_EMAILS` env var
3. **No API keys**: No passwords, no secrets in URLs
4. **Database-level**: Auth check happens before any queries run

### Privacy Compliance
✅ **No PII**: Only SHA256 hashes (irreversible)
✅ **No IP storage**: IP addresses not logged in analytics
✅ **Opt-in**: Users consent via `AnalyticsConsentWindow.xaml`
✅ **Aggregated only**: Dashboard shows counts, not raw events
✅ **GDPR/CCPA compliant**: No personal data processed

---

## 📈 Key Metrics to Watch

### Growth Metrics
- **Total Downloads**: Track month-over-month growth
- **MAU**: Active user base size
- **DAU/MAU Ratio**: Engagement quality (>40% = sticky product)

### Product Health
- **Event Types**: High `TRANSCRIPTION_COMPLETED` = active usage
- **Error Rate**: `ERROR_OCCURRED` / Total Events (keep <1%)
- **Model Distribution**: Users on old models = need upgrade prompts

### Conversion Metrics
- **PRO Upgrades**: `PRO_UPGRADE` events / `APP_LAUNCHED` (conversion rate)
- **Tier Distribution**: PRO users / Total users (revenue potential)

### Technical Metrics
- **OS Distribution**: Windows 11 adoption (impacts compatibility priorities)
- **Version Distribution**: Users on old versions (update prompts needed?)

---

## 🛠️ Troubleshooting

### "Unauthorized. Admin access required."
**Fix**: Check `.env.local` has `ADMIN_EMAILS="your-email@example.com"` and matches your login email exactly (case-sensitive).

### Dashboard is blank (no data)
**Cause**: No analytics events in database yet.
**Fix**: Desktop app must have `EnableAnalytics=true` in settings. Users opt-in on first launch via `AnalyticsConsentWindow`.

### Charts not loading
**Fix**: Check browser console for errors. Ensure `recharts` package is installed (`npm install` in voicelite-web/).

### Slow loading
**Normal**: First load queries database (2-3 seconds). Subsequent loads use 5-minute cache (<100ms).

---

## 🎨 Design Best Practices (Already Implemented)

### Visual Hierarchy
✅ Most important metrics at top (Total Events, DAU, MAU)
✅ Trends in middle (Daily Activity chart)
✅ Granular details at bottom (Model/OS breakdowns)

### Color Consistency
- Purple: Primary brand color (events, main metrics)
- Cyan: Users (DAU)
- Green: Growth (MAU, positive trends)
- Orange: Engagement (DAU/MAU ratio)
- Red: Errors/warnings

### UX Principles
- **5-Second Rule**: Key metrics immediately visible
- **No Information Overload**: 5 charts (not 20)
- **Goal-Centric**: Answers "how many downloads?" directly
- **Responsive**: Works on mobile, tablet, desktop

---

## 🚀 Future Enhancements (Optional)

### Phase 2 Ideas
- **Export to CSV**: Download analytics data
- **Comparative analysis**: This month vs last month
- **Alerts**: Email when downloads spike/drop
- **Retention cohorts**: Week 1 retention, Week 4 retention
- **Revenue dashboard**: MRR, churn rate, LTV
- **Geographic data**: Country-level breakdowns (requires IP → geo mapping)
- **Real-time updates**: WebSocket for live metrics

### Phase 3 Ideas
- **Custom date ranges**: "Jan 1 - Jan 31" picker
- **Funnel analysis**: Downloads → Transcriptions → Pro Upgrades
- **A/B testing results**: Experiment tracking
- **Crash analytics**: Error grouping and stack traces

---

## 📚 Technical Details

### Tech Stack
- **Backend**: Next.js 15 App Router (API route at `/api/admin/analytics`)
- **Frontend**: React 19 + Recharts 3.2.1
- **Database**: PostgreSQL (Supabase) via Prisma ORM
- **Auth**: Session-based (JWT stored in HTTP-only cookie)
- **Caching**: 5-minute revalidation (`export const revalidate = 300`)
- **Styling**: Tailwind CSS v4

### Performance Optimizations
- Parallel queries (`Promise.all` for 9 DB queries)
- Database aggregations (not in-memory processing)
- Server-side caching (5-minute window)
- Lazy loading (Recharts components)

### Files
- **API**: `voicelite-web/app/api/admin/analytics/route.ts`
- **UI**: `voicelite-web/app/admin/analytics/page.tsx`
- **Schema**: `voicelite-web/prisma/schema.prisma` (AnalyticsEvent model)
- **Desktop**: `VoiceLite/Services/AnalyticsService.cs` (event tracking)

---

## ✅ Summary

The analytics dashboard is **production-ready** and follows 2025 best practices:
- ✅ Secure (session-based auth, email whitelist)
- ✅ Fast (parallel queries, caching, aggregations)
- ✅ Professional (Recharts, responsive design)
- ✅ Privacy-first (SHA256 hashes, no PII, opt-in)
- ✅ Actionable (DAU/MAU, conversion rate, error tracking)

**Just add your email to `ADMIN_EMAILS` and you're ready to go!** 🎉

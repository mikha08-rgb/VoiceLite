# VoiceLite Monitoring Setup Guide

**Created**: October 19, 2025
**Purpose**: Step-by-step instructions for setting up production monitoring (100% FREE tier)

---

## 1. Sentry Error Monitoring Setup (30 minutes)

### Step 1: Create Sentry Account
1. Go to https://sentry.io/signup/
2. Sign up with GitHub (recommended) or email
3. Choose "Create your own organization"
4. Organization name: `voicelite`
5. **Important**: Select **FREE "Developer" plan** (5,000 events/month)

### Step 2: Create Project
1. Click "Create Project"
2. Platform: **Next.js**
3. Project name: `voicelite-web`
4. Alert frequency: **Weekly** (default)
5. Click "Create Project"

### Step 3: Copy DSN
You'll see a DSN (Data Source Name) that looks like:
```
https://abc123def456@o123456.ingest.sentry.io/7891011
```
**Copy this - you'll need it in Step 4**

### Step 4: Install Sentry in Web Platform
```bash
cd voicelite-web
npm install @sentry/nextjs --save
npx @sentry/wizard@latest -i nextjs
```

When prompted:
- Paste the DSN from Step 3
- **Yes** to source maps upload
- **Yes** to performance monitoring (10% sample rate is fine)
- **Yes** to creating `.sentryclirc` file

###

 Step 5: Add Environment Variables
Add to `voicelite-web/.env.local`:
```bash
NEXT_PUBLIC_SENTRY_DSN=https://abc123def456@o123456.ingest.sentry.io/7891011
SENTRY_AUTH_TOKEN=your_auth_token_here  # Provided by wizard
```

### Step 6: Deploy to Vercel
```bash
cd voicelite-web

# Add environment variables to Vercel
vercel env add NEXT_PUBLIC_SENTRY_DSN
# Paste your DSN when prompted

vercel env add SENTRY_AUTH_TOKEN
# Paste your auth token when prompted

# Deploy
vercel --prod
```

### Step 7: Test Error Tracking
Visit: `https://voicelite.app/api/test-sentry` (create this endpoint):
```typescript
// app/api/test-sentry/route.ts
import * as Sentry from '@sentry/nextjs';
import { NextResponse } from 'next/server';

export async function GET() {
  // This will send a test error to Sentry
  Sentry.captureException(new Error('Test error from VoiceLite API'));
  return NextResponse.json({ message: 'Test error sent to Sentry' });
}
```

Check Sentry dashboard - you should see the error within 30 seconds!

### Step 8: Configure Alerts (Optional)
1. Go to Sentry Dashboard → Settings → Alerts
2. Create alert rule: **Email on any new issue**
3. Add your email: `mikhail.lev08@gmail.com`
4. Threshold: **First seen** (immediate alerts)

**Cost**: FREE (5,000 events/month = ~165/day, plenty for pre-launch)

---

## 2. UptimeRobot Monitoring Setup (15 minutes)

### Step 1: Create Account
1. Go to https://uptimerobot.com/
2. Click "Free Sign Up"
3. Use email: `mikhail.lev08@gmail.com`
4. Verify email (check inbox)

### Step 2: Add Monitors
Click "+ Add New Monitor" for each:

#### Monitor 1: Homepage
- **Monitor Type**: HTTP(S)
- **Friendly Name**: `VoiceLite Homepage`
- **URL**: `https://voicelite.app`
- **Monitoring Interval**: 5 minutes (free tier)
- **Monitor Timeout**: 30 seconds
- **Alert Contacts**: Your email
- Click "Create Monitor"

#### Monitor 2: Health Check API
- **Monitor Type**: HTTP(S)
- **Friendly Name**: `VoiceLite API Health`
- **URL**: `https://voicelite.app/api/health`
- **Monitoring Interval**: 5 minutes
- **Monitor Timeout**: 30 seconds
- **Advanced Settings** → **Search In Response**:
  - **Keyword**: `"status":"ok"`
  - **Keyword Type**: Exists
- **Alert Contacts**: Your email
- Click "Create Monitor"

#### Monitor 3: Stripe Webhook Endpoint
- **Monitor Type**: HTTP(S)
- **Friendly Name**: `VoiceLite Stripe Webhook`
- **URL**: `https://voicelite.app/api/webhook`
- **Monitoring Interval**: 5 minutes
- **Monitor Timeout**: 10 seconds
- **Advanced Settings** → **Custom HTTP Headers**:
  - (Leave empty - we just check if endpoint responds)
- **Expected HTTP Status Code**: 405 (Method Not Allowed for GET)
  - This is correct - webhook only accepts POST
- Click "Create Monitor"

### Step 3: Configure Alert Channels
1. Go to "My Settings" → "Alert Contacts"
2. Add email: `mikhail.lev08@gmail.com`
3. Verify email (check inbox, click confirmation link)
4. **Optional**: Add SMS alerts (100 SMS/month on free tier)

### Step 4: Setup Status Page (Optional but Recommended)
1. Go to "Public Status Pages"
2. Click "+ Add Status Page"
3. **Friendly Name**: `VoiceLite`
4. **Subdomain**: `voicelite` (creates `voicelite.statuspage.io`)
5. **Monitors**: Select all 3 monitors
6. **Settings**:
   - ✅ Show response times
   - ✅ Show uptime percentages
   - ❌ Hide monitors until they go down (uncheck - always show)
7. Click "Create Status Page"

**Public URL**: `https://voicelite.statuspage.io` (share with users!)

### Step 5: Test Alerts
1. **Pause one monitor** (click pause icon)
2. Wait 5-10 minutes
3. Check email - you should receive alert
4. **Resume monitor**
5. Check email - you should receive "back up" notification

**Cost**: FREE (50 monitors, 5-minute checks, unlimited)

---

## 3. Supabase Database Backups (Verify - Already Enabled)

### Step 1: Check Backup Status
1. Go to https://supabase.com/dashboard
2. Select your VoiceLite project
3. Navigate to **Database** → **Backups**

### Step 2: Verify Daily Backups
You should see:
- ✅ **Automatic daily backups** (enabled by default on free tier)
- ✅ **7-day retention**
- ✅ Last backup date (should be within last 24 hours)

If NOT enabled:
1. Contact Supabase support (shouldn't happen - enabled by default)
2. Alternatively, upgrade to Pro ($25/month) for PITR (Point-In-Time Recovery)

### Step 3: Test Restore (Optional - Do This Monthly)
1. Click on a backup (e.g., yesterday's)
2. Click "Download" → Saves SQL dump
3. **DO NOT RESTORE TO PRODUCTION** (dangerous!)
4. Instead, create test project and restore there:
   ```bash
   # Create test database
   createdb voicelite_test

   # Restore backup
   psql voicelite_test < backup_20251019.sql

   # Verify data
   psql voicelite_test -c "SELECT COUNT(*) FROM \"License\";"
   ```

**Cost**: FREE (included in Supabase free tier)

---

## 4. Monitoring Dashboard Overview

### What You'll See After Setup

#### Sentry Dashboard
- **Issues tab**: All errors/exceptions in real-time
- **Performance tab**: API response times (P50, P95, P99)
- **Releases tab**: Track errors by version
- **Alerts tab**: Email notifications when issues occur

**Key metrics to watch**:
- Error rate (should be <0.1%)
- New issue frequency (any new error = investigate)
- Performance degradation (P95 latency >1s = problem)

#### UptimeRobot Dashboard
- **Uptime %**: Should be 99.9%+ (5 nines = enterprise-grade)
- **Response time**: Homepage ~200-500ms, API ~100-300ms
- **Downtime history**: Any outages longer than 5 minutes = investigate

**Key metrics to watch**:
- Monthly uptime % (target: >99.9%)
- Average response time (target: <500ms)
- Alert frequency (0-1 alerts/month = healthy)

#### Status Page (voicelite.statuspage.io)
- Shows real-time status to users
- 90-day uptime history
- Incident history (post-mortems)

---

## 5. Monitoring Checklist (Weekly Review)

### Monday Morning Routine (10 minutes)
1. [ ] Check Sentry dashboard
   - Any new critical errors?
   - Error rate increasing?
   - Performance degrading?

2. [ ] Check UptimeRobot dashboard
   - Uptime % still >99.9%?
   - Any downtime incidents last week?
   - Response times stable?

3. [ ] Check Supabase backups
   - Last backup within 24 hours?
   - No backup failures?

4. [ ] Review Dependabot PRs
   - Any security vulnerabilities?
   - Merge safe dependency updates

### Monthly Deep Dive (30 minutes)
1. [ ] Sentry error trends
   - Most common errors (fix top 3)
   - Error rate by endpoint
   - Performance bottlenecks

2. [ ] UptimeRobot historical data
   - Uptime % last 30 days
   - Slowest response times (optimize endpoints)
   - Peak traffic times

3. [ ] Backup restore test
   - Download latest Supabase backup
   - Restore to test database
   - Verify data integrity

---

## 6. Alert Response Playbook

### Email Alert: "Sentry: New Issue - TypeError in /api/webhook"
**Priority**: HIGH (payment-related error)

**Action Steps**:
1. Click alert link → Opens Sentry issue page
2. Check **Breadcrumbs** tab (what happened before error)
3. Check **Stack Trace** (exact line of code that failed)
4. If affecting payments:
   - Check Stripe dashboard (any failed payments?)
   - Rollback deployment if needed: `vercel rollback`
5. Fix code, deploy, mark Sentry issue as "Resolved"

### Email Alert: "UptimeRobot: voicelite.app is DOWN"
**Priority**: CRITICAL (site outage)

**Action Steps**:
1. Verify outage (visit `https://voicelite.app` yourself)
2. Check Vercel dashboard (deployment failed?)
3. Check Supabase dashboard (database down?)
4. If Vercel issue: Rollback to last working deployment
5. If Supabase issue: Contact support (SLA: 30min response)
6. Update status page with incident details

### Email Alert: "UptimeRobot: API Health Check Failed"
**Priority**: MEDIUM (API degraded but site still up)

**Action Steps**:
1. Visit `https://voicelite.app/api/health` (check response)
2. If database error:
   - Check Supabase connection pool (>80% = issue)
   - Restart Supabase instance if needed
3. If timeout:
   - Check Vercel function logs (cold start issue?)
   - Increase function timeout in `vercel.json`

---

## 7. Upgrade Path (When to Pay)

### Sentry: Upgrade to Team ($26/mo) When:
- ✅ Exceeding 5k events/month (check dashboard)
- ✅ Need performance monitoring for 100% of requests (vs 10%)
- ✅ Want session replay (see user's screen during error)
- ✅ Team of 2+ developers

**Current usage**: 0 events/month → FREE tier sufficient for 6+ months

### UptimeRobot: Upgrade to Pro ($7/mo) When:
- ✅ Need 1-minute checks (vs 5-minute)
- ✅ Need SMS alerts (100/month on free tier)
- ✅ Need custom status page domain (status.voicelite.app)

**Current need**: FREE tier sufficient (5-min checks are fine)

### Supabase: Upgrade to Pro ($25/mo) When:
- ✅ Exceeding 500MB database size
- ✅ Need Point-In-Time Recovery (restore to any second)
- ✅ Need faster backups (every 2 hours vs daily)
- ✅ 1000+ concurrent users (better performance)

**Current usage**: <10MB database → FREE tier sufficient for 1+ year

---

## 8. Success Metrics

**After setup, you should have**:
- ✅ Real-time error visibility (Sentry)
- ✅ Uptime monitoring (UptimeRobot)
- ✅ Daily database backups (Supabase)
- ✅ Public status page (voicelite.statuspage.io)
- ✅ Email alerts on incidents
- ✅ 100% FREE ($0/month cost)

**Within 24 hours, you'll see**:
- First Sentry events (page views, API calls)
- UptimeRobot uptime % (should be 100%)
- Health check API returning "ok"

**Within 1 week, you'll have**:
- Baseline error rate (<0.1% = healthy)
- Average response times (<500ms = healthy)
- 7 days of uptime history (>99.9% = healthy)

---

## 9. Troubleshooting

### Sentry Not Receiving Events
**Problem**: Dashboard shows 0 events after 24 hours

**Solutions**:
1. Check DSN in `.env.local` (correct format?)
2. Verify `NEXT_PUBLIC_SENTRY_DSN` in Vercel environment variables
3. Test with `/api/test-sentry` endpoint (should send test error)
4. Check browser console (CORS errors?)
5. Redeploy: `vercel --prod`

### UptimeRobot Constantly Alerting "DOWN"
**Problem**: Getting false alerts every 5 minutes

**Solutions**:
1. Check URL (typo in domain?)
2. Verify expected response (200 OK vs 405 for webhook)
3. Increase timeout (30s → 60s)
4. Check Vercel function logs (cold start timing out?)
5. Temporarily **Pause** monitor while investigating

### Health Check API Returns 503
**Problem**: `/api/health` shows database disconnected

**Solutions**:
1. Check Supabase dashboard (project paused?)
2. Verify `DATABASE_URL` in Vercel environment variables
3. Check connection pool (Supabase dashboard → Database → Connections)
4. Restart Supabase project (Dashboard → Settings → Restart)
5. If persistent, check PostgreSQL logs

---

## 10. Next Steps

After completing this setup:
1. [ ] Bookmark dashboards (Sentry, UptimeRobot, Supabase)
2. [ ] Share status page URL with beta testers
3. [ ] Set calendar reminder for weekly monitoring review
4. [ ] Document any custom alert thresholds in RUNBOOK.md
5. [ ] Test alert workflow (pause monitor → verify email → resume)

**Estimated setup time**: 45 minutes (mostly waiting for verification emails)
**Ongoing maintenance**: 10 minutes/week (Monday routine)
**Cost**: $0/month (forever on free tiers for <500 users)

---

**Questions?** Check:
- Sentry docs: https://docs.sentry.io/platforms/javascript/guides/nextjs/
- UptimeRobot docs: https://uptimerobot.com/kb/
- Supabase docs: https://supabase.com/docs/guides/database/backups

**Report setup issues**: Document in `MONITORING_SETUP_ISSUES.md` for future reference

---

**Last Updated**: October 19, 2025
**Maintainer**: Mikhail Levashov (solo dev)
**Review Cadence**: Update after each monitoring tool upgrade

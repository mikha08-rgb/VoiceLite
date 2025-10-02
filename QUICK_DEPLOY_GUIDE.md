# Quick Deploy Guide - Feedback & Tracking

## 🚀 Deploy NOW (Without Upstash)

Rate limiting has been disabled so you can deploy immediately. You only need **one thing**:

### Required: Your Admin Email

Add this environment variable to Vercel:

```bash
ADMIN_EMAILS="your-email@example.com"
```

---

## 📋 Deployment Steps

### 1. Add Environment Variable to Vercel

**Option A: Via Vercel CLI**
```bash
vercel env add ADMIN_EMAILS
# When prompted, enter: your-email@example.com
# Select: Production, Preview, Development (all 3)
```

**Option B: Via Vercel Dashboard**
1. Go to https://vercel.com/your-project/settings/environment-variables
2. Add new variable:
   - **Key**: `ADMIN_EMAILS`
   - **Value**: `your-email@example.com`
   - **Environments**: Production, Preview, Development

### 2. Run Database Migration

```bash
cd voicelite-web
npm run db:migrate
```

When prompted for migration name, enter: `add_feedback_and_tracking`

### 3. Deploy to Vercel

```bash
vercel deploy --prod
```

### 4. Test It!

1. **Test Feedback Form**: Visit `https://voicelite.app/feedback`
   - Submit a test bug report
   - Should see success message

2. **Test Admin Dashboard**: Visit `https://voicelite.app/admin`
   - Sign in with your email (the one in ADMIN_EMAILS)
   - Should see stats dashboard
   - Should see your test feedback

3. **Test Desktop Integration**:
   - Rebuild desktop app
   - Open Settings window
   - Click "Send Feedback" button
   - Should open browser to feedback form with version/OS pre-filled

---

## ✅ What You Get (Without Rate Limiting)

**Working:**
- ✅ Feedback form (anyone can submit)
- ✅ Admin dashboard (view all stats)
- ✅ User tracking (logins, licenses, feedback)
- ✅ Desktop app feedback button

**Missing (Can Add Later):**
- ⏸️ Rate limiting (unlimited submissions per hour)

---

## 🔐 Admin Access

After deployment:
1. Sign in at `https://voicelite.app` with the email you set in `ADMIN_EMAILS`
2. Navigate to `https://voicelite.app/admin`
3. You'll see the full dashboard

Anyone else who visits `/admin` will see "Access Denied"

---

## 🛡️ Add Rate Limiting Later (Optional)

When you're ready to prevent spam:

### 1. Sign Up for Upstash (2 minutes)
- Go to https://console.upstash.com
- Create Redis database (free tier)
- Copy REST URL and REST TOKEN

### 2. Add to Vercel
```bash
vercel env add UPSTASH_REDIS_REST_URL
vercel env add UPSTASH_REDIS_REST_TOKEN
```

### 3. Uncomment Code
In `voicelite-web/app/api/feedback/submit/route.ts`:
- Uncomment lines 4-5 (imports)
- Uncomment lines 10-14 (ratelimit setup)
- Uncomment lines 35-48 (rate limit check)
- Uncomment lines 117-118 (rate limit headers)

### 4. Redeploy
```bash
vercel deploy --prod
```

---

## 🎯 What's Next?

### Immediate (Post-Deployment)
- [ ] Test feedback submission
- [ ] Test admin dashboard
- [ ] Test desktop feedback button
- [ ] Submit your first real feedback

### Soon (When You Have Time)
- [ ] Add Upstash Redis for rate limiting
- [ ] Add email notifications for high-priority feedback
- [ ] Export feedback to CSV
- [ ] Add charts to admin dashboard

### Later (Nice to Have)
- [ ] User dashboard (users see their own feedback)
- [ ] Feedback voting (upvote feature requests)
- [ ] In-app feedback form (no browser needed)

---

## 🐛 Troubleshooting

### "Environment variable not found: DIRECT_DATABASE_URL"
**Solution**: Add `DIRECT_DATABASE_URL` to your `.env` (same as `DATABASE_URL`)

### "Unauthorized" on /admin
**Solution**: Make sure you signed in with the exact email in `ADMIN_EMAILS`

### Migration fails
**Solution**:
1. Check database connection: `npm run db:studio`
2. Verify `DATABASE_URL` is correct
3. Try `npm run db:push` instead of `db:migrate`

### Feedback submission fails
**Solution**: Check Vercel logs for errors. Likely Prisma client issue - run `npx prisma generate`

---

## 📞 Need Help?

1. Check Vercel deployment logs
2. Check Supabase database (should have `Feedback` and `UserActivity` tables)
3. Test locally first: `npm run dev`
4. Check browser console for errors

---

## 🎉 You're Done!

Once deployed, you'll have:
- **Feedback system** for users to report bugs/request features
- **Admin dashboard** to view user stats and feedback
- **User tracking** to understand growth and engagement
- **Desktop integration** for easy feedback submission

All while maintaining VoiceLite's privacy promise: **Desktop stays 100% offline** ✅

# User Feedback & Tracking Implementation

## Overview

Successfully implemented a comprehensive user feedback and tracking system for VoiceLite while maintaining the core privacy promise: **Desktop app stays 100% offline with zero automatic telemetry**.

---

## ✅ What Was Implemented

### 1. Database Schema (Prisma)

**File**: `voicelite-web/prisma/schema.prisma`

Added three new models:

#### Feedback Model
```prisma
model Feedback {
  id        String           @id @default(cuid())
  userId    String?          // Optional - allows anonymous feedback
  user      User?            @relation(fields: [userId], references: [id])
  email     String?          // Optional email for follow-up
  type      FeedbackType     // BUG, FEATURE_REQUEST, GENERAL, QUESTION
  subject   String
  message   String
  metadata  String?          // JSON - browser, OS, version
  status    FeedbackStatus   // OPEN, IN_PROGRESS, RESOLVED, CLOSED
  priority  FeedbackPriority // LOW, MEDIUM, HIGH, CRITICAL
  createdAt DateTime
  updatedAt DateTime
}
```

#### UserActivity Model
```prisma
model UserActivity {
  id           String       @id @default(cuid())
  userId       String?
  user         User?
  activityType ActivityType // LOGIN, CHECKOUT_STARTED, LICENSE_ISSUED, etc.
  metadata     String?      // JSON - additional context
  ipAddress    String?
  userAgent    String?
  createdAt    DateTime
}
```

### 2. API Routes

#### Feedback Submission
**File**: `voicelite-web/app/api/feedback/submit/route.ts`

- **Endpoint**: `POST /api/feedback/submit`
- **Rate Limiting**: 5 submissions per hour per IP (using Upstash Redis)
- **Features**:
  - Accepts anonymous or authenticated feedback
  - Validates input with Zod schema
  - Auto-sets priority (BUG = HIGH, others = MEDIUM)
  - Tracks user activity if authenticated
  - Returns feedback ID for reference

#### Admin Feedback Management
**File**: `voicelite-web/app/api/admin/feedback/route.ts`

- **GET /api/admin/feedback**: List all feedback (admin-only)
  - Pagination support (max 100 per page)
  - Filter by status and type
  - Returns stats (counts by status/type)
- **PATCH /api/admin/feedback**: Update feedback status/priority

#### Admin Stats Dashboard
**File**: `voicelite-web/app/api/admin/stats/route.ts`

- **Endpoint**: `GET /api/admin/stats` (admin-only)
- **Metrics Provided**:
  - Users: total, new (7d/30d), active (30d), growth chart
  - Licenses: total, active, by type, device activations
  - Purchases: total count
  - Feedback: counts by status
  - Activity: recent events + breakdown by type

### 3. UI Components

#### Feedback Form Page
**File**: `voicelite-web/app/feedback/page.tsx`

- Beautiful feedback form with:
  - Type selector (Bug, Feature Request, Question, General)
  - Subject and message fields (with character limits)
  - Optional email for follow-up
  - Pre-filled system info from URL params (desktop integration)
  - Success screen with confirmation
  - Rate-limited to prevent spam

#### Admin Dashboard
**File**: `voicelite-web/app/admin/page.tsx`

- Protected admin-only dashboard showing:
  - Key metrics cards (total users, active licenses, active users, feedback count)
  - License distribution breakdown
  - Feedback status breakdown
  - Activity breakdown (last 30 days)
  - Refresh button for real-time data

#### Landing Page Integration
**File**: `voicelite-web/app/page.tsx`

- Added "Send Feedback" link in footer under Support section

### 4. Desktop App Integration

#### Settings Window (XAML)
**File**: `VoiceLite/SettingsWindow.xaml`

- Added "Send Feedback" button next to Save/Cancel buttons
- Tooltip: "Send feedback, report bugs, or request features"

#### Settings Window (Code-Behind)
**File**: `VoiceLite/SettingsWindow.xaml.cs`

- `SendFeedbackButton_Click` handler:
  - Gets app version from assembly
  - Gets OS version
  - Opens browser to feedback page with pre-filled URL params
  - Error handling with fallback message

---

## 🔒 Privacy & Compliance

### Desktop App (100% Offline)
✅ **Zero automatic telemetry** - Desktop app never sends usage data
✅ **No tracking** - Voice recordings stay local, never sent to server
✅ **Opt-in feedback** - User must click "Send Feedback" button
✅ **Pre-filled system info** - Only version and OS (visible to user)

### Web App (Privacy-First)
✅ **Authenticated tracking only** - Track logged-in user actions
✅ **No third-party trackers** - No Google Analytics, Facebook Pixel, etc.
✅ **Rate limiting** - Prevent spam/abuse (5 feedback per hour)
✅ **Anonymous feedback allowed** - Email optional

---

## 📊 User Tracking Events

The following events are tracked automatically on the **web app only** (when users are authenticated):

1. **USER_REGISTERED** - New user signs up via magic link
2. **USER_LOGIN** - User logs in
3. **USER_LOGOUT** - User logs out
4. **CHECKOUT_STARTED** - User initiates checkout (to be implemented)
5. **CHECKOUT_COMPLETED** - Successful purchase (to be implemented)
6. **LICENSE_ISSUED** - New license created
7. **LICENSE_ACTIVATED** - User activates license on device
8. **LICENSE_DEACTIVATED** - User deactivates device
9. **LICENSE_RENEWED** - Subscription renewed
10. **FEEDBACK_SUBMITTED** - User submits feedback

**Implementation Note**: Currently only `FEEDBACK_SUBMITTED` is hooked up. Other events need to be added to existing flows (checkout, license activation, etc.).

---

## 🚀 Deployment Steps

### 1. Set Environment Variables

Add to your `.env` file (or Vercel environment variables):

```bash
# Admin emails (comma-separated)
ADMIN_EMAILS="your-email@example.com,admin@voicelite.app"

# Upstash Redis (for rate limiting)
UPSTASH_REDIS_REST_URL="https://..."
UPSTASH_REDIS_REST_TOKEN="..."

# Database (already configured)
DATABASE_URL="postgres://..."
DIRECT_DATABASE_URL="postgres://..."
```

### 2. Run Database Migration

```bash
cd voicelite-web
npm run db:migrate
```

This will:
- Create `Feedback` table
- Create `UserActivity` table
- Add `feedback` and `activities` relations to `User` table

### 3. Generate Prisma Client

```bash
npm run db:push
```

### 4. Deploy to Vercel

```bash
vercel deploy --prod
```

### 5. Test Feedback Flow

1. Visit `https://voicelite.app/feedback`
2. Submit test feedback
3. Check admin dashboard at `https://voicelite.app/admin`

---

## 🧪 Testing Checklist

### Web App
- [ ] Submit anonymous feedback (without signing in)
- [ ] Submit authenticated feedback (while signed in)
- [ ] Verify rate limiting (submit 6 times in 1 hour, 6th should fail)
- [ ] Check feedback appears in admin dashboard
- [ ] Update feedback status as admin
- [ ] Verify admin stats page shows correct metrics

### Desktop App
- [ ] Click "Send Feedback" button in Settings
- [ ] Verify browser opens to feedback page
- [ ] Check URL params include version and OS
- [ ] Submit feedback and verify it appears in database

---

## 📈 Admin Access

### Accessing Admin Dashboard

1. Sign in with an email listed in `ADMIN_EMAILS` environment variable
2. Navigate to `https://voicelite.app/admin`
3. If not admin, you'll see "Access Denied" message

### Admin Capabilities

- View all feedback (filter by status/type)
- Update feedback status (OPEN → IN_PROGRESS → RESOLVED → CLOSED)
- Update feedback priority (LOW/MEDIUM/HIGH/CRITICAL)
- View user stats (total, active, new users)
- View license stats (total, active, by type)
- View activity breakdown (last 30 days)
- Refresh dashboard for real-time data

---

## 🔮 Future Enhancements

### Short-Term (Recommended)
1. **Email notifications** - Use Resend to notify admin of high-priority feedback
2. **Feedback response** - Allow admin to reply to feedback via email
3. **Export data** - CSV export for feedback and user stats
4. **Charts** - Add user growth chart to admin dashboard

### Medium-Term
1. **License validation tracking** - Track when desktop app validates licenses
2. **Crash reporting** - Optional opt-in crash reports from desktop app
3. **Feature voting** - Users can upvote feature requests
4. **Status page** - Public page showing feedback roadmap

### Long-Term
1. **Admin panel improvements** - Search, advanced filters, bulk actions
2. **User dashboard** - Let users see their own feedback submissions
3. **In-app feedback** - Feedback form inside desktop app (no browser required)
4. **Analytics dashboard** - Conversion funnel, retention, churn metrics

---

## 📂 Files Modified/Created

### Database
- ✅ `voicelite-web/prisma/schema.prisma` (modified)

### API Routes (5 new files)
- ✅ `voicelite-web/app/api/feedback/submit/route.ts` (created)
- ✅ `voicelite-web/app/api/admin/feedback/route.ts` (created)
- ✅ `voicelite-web/app/api/admin/stats/route.ts` (created)

### UI Pages (2 new files, 1 modified)
- ✅ `voicelite-web/app/feedback/page.tsx` (created)
- ✅ `voicelite-web/app/admin/page.tsx` (created)
- ✅ `voicelite-web/app/page.tsx` (modified - added feedback link)

### Desktop App (2 modified files)
- ✅ `VoiceLite/SettingsWindow.xaml` (modified - added button)
- ✅ `VoiceLite/SettingsWindow.xaml.cs` (modified - added click handler)

---

## 🎯 Success Metrics

Once deployed, you can track:

### User Growth
- Total registered users
- New users per week/month
- Active users (30-day)
- User retention rate

### License Metrics
- Total licenses issued
- Active vs. expired licenses
- License type distribution (SUBSCRIPTION vs. LIFETIME)
- Device activations per license

### Feedback Quality
- Total feedback submissions
- Feedback by type (Bug, Feature, Question, General)
- Average response time (OPEN → RESOLVED)
- Feedback trends over time

### Engagement
- Feedback submission rate (% of users submitting feedback)
- Repeat feedback submitters
- Most requested features

---

## ❓ FAQ

### Q: How do I become an admin?
**A:** Add your email to the `ADMIN_EMAILS` environment variable (comma-separated list).

### Q: Can users submit feedback without an account?
**A:** Yes! Feedback can be submitted anonymously. Email is optional.

### Q: Does the desktop app send telemetry?
**A:** No. The desktop app is 100% offline. Feedback is only sent when the user clicks "Send Feedback" and submits the form.

### Q: How do I add user tracking to checkout?
**A:** In your checkout completion handler, add:
```typescript
await prisma.userActivity.create({
  data: {
    userId: user.id,
    activityType: 'CHECKOUT_COMPLETED',
    metadata: JSON.stringify({ amount, plan }),
    ipAddress: req.headers.get('x-forwarded-for'),
    userAgent: req.headers.get('user-agent'),
  },
});
```

### Q: How do I export feedback data?
**A:** Future enhancement. For now, use Prisma Studio (`npm run db:studio`) or direct database queries.

---

## 🙏 Credits

- **Rate Limiting**: Upstash Redis + @upstash/ratelimit
- **Schema Validation**: Zod
- **Database**: Prisma + Supabase PostgreSQL
- **UI**: Next.js 15 + Tailwind CSS v4
- **Desktop**: WPF + .NET 8.0

---

## 📞 Support

If you encounter issues during deployment:
1. Check Vercel logs for errors
2. Verify all environment variables are set
3. Run database migration again
4. Check admin email is in `ADMIN_EMAILS`
5. Test rate limiting with Upstash Redis dashboard

Happy tracking! 🚀

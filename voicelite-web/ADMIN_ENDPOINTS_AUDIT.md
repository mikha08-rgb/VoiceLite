# Admin Endpoints Security Audit

**Date**: October 18, 2025
**Status**: ⚠️ **CRITICAL - Endpoints Will Fail in Production**

---

## Executive Summary

**Problem**: Multiple admin endpoints exist but reference database models that **don't exist** in the current schema. These endpoints will crash when called.

**Impact**:
- Production deployment will fail if these endpoints are accessed
- Potential security risk if not properly handled
- Confusing codebase with dead code

**Recommendation**: **Remove all admin endpoints** until User/Session authentication system is implemented.

---

## Affected Endpoints

### 1. `/api/admin/feedback` ⚠️
**File**: [app/api/admin/feedback/route.ts](app/api/admin/feedback/route.ts:1)

**References Missing Models**:
- `prisma.session` (line 14)
- `prisma.feedback` (line 77, 95, 99, 106, 170)
- `session.user` (line 16, 24)

**Functionality**:
- GET: List feedback submissions with filtering
- PATCH: Update feedback status/priority

**Status**: 🔴 **WILL CRASH** (Session and Feedback tables don't exist)

---

### 2. `/api/admin/analytics` ⚠️
**File**: [app/api/admin/analytics/route.ts](app/api/admin/analytics/route.ts:1)

**References Missing Models**:
- `prisma.session` (via verifyAdmin)
- `prisma.analyticsEvent` (lines 118-253)
- `prisma.user` (via verifyAdmin)

**Functionality**:
- GET: Returns analytics dashboard data
  - DAU/MAU metrics
  - Event type distribution
  - Tier distribution (FREE vs PRO)
  - App version distribution
  - Model usage stats
  - OS distribution
  - Time series data

**Status**: 🔴 **WILL CRASH** (AnalyticsEvent table doesn't exist)

---

### 3. `/api/admin/stats` ⚠️
**File**: [app/api/admin/stats/route.ts](app/api/admin/stats/route.ts:1)

**References Missing Models**:
- `prisma.user` (lines 78-95, 167)
- `prisma.session` (via verifyAdmin)
- `prisma.purchase` (line 117)
- `prisma.userActivity` (lines 120, 143, 178)
- `prisma.feedback` (line 135)
- `prisma.licenseActivation` with `status` field (lines 158-163)

**Functionality**:
- GET: Returns dashboard stats
  - User counts and growth
  - License counts by type/status
  - Purchase totals
  - Feedback counts
  - Recent activity feed
  - Activation metrics

**Status**: 🔴 **WILL CRASH** (Multiple missing tables)

---

## Supporting Infrastructure

### Admin Authentication Module ✅
**File**: [lib/admin-auth.ts](lib/admin-auth.ts:1)

**Functionality**:
- Session-based authentication
- Email whitelist via `ADMIN_EMAILS` env var
- Session expiry and revocation checks

**Status**: 🔴 **WILL CRASH** (References `prisma.session.findUnique()` which doesn't exist)

**Code Quality**: Excellent (well-documented, secure, proper error handling)

---

## Current Database Schema

**Existing Models** ([prisma/schema.prisma](prisma/schema.prisma:1)):
```prisma
✅ License
✅ LicenseActivation
✅ WebhookEvent
```

**Missing Models** (referenced by admin endpoints):
```prisma
❌ User
❌ Session
❌ Feedback
❌ UserActivity
❌ Purchase
❌ AnalyticsEvent
```

---

## Root Cause Analysis

### What Happened?

1. **Original Architecture**: VoiceLite had a full user authentication system with:
   - User accounts
   - Passwordless auth (OTP)
   - Session management
   - Feedback system
   - Analytics tracking
   - Admin dashboard

2. **Simplification** (Oct 2025): Schema was simplified to focus on core licensing:
   - Removed User/Session tables
   - Changed to email-only licensing (no accounts)
   - Archived old migrations with User references

3. **Orphaned Code**: Admin endpoints were **not removed** during simplification
   - They still reference old database models
   - They will crash if called
   - They create security confusion

---

## Security Analysis

### Authentication Security ✅

**If tables existed**, the authentication would be secure:
- ✅ Session-based auth with HTTP-only cookies
- ✅ Session expiry and revocation
- ✅ Email whitelist (`ADMIN_EMAILS`)
- ✅ Rate limiting (100 req/hr)
- ✅ Proper error handling
- ✅ SQL injection prevention
- ✅ No sensitive data exposure

**Code Quality**: A+ (well-architected, production-ready)

### Current Risk Level: 🟡 **MEDIUM**

**Why not critical?**
1. Endpoints will return 500 errors (Prisma will throw "model not found")
2. No sensitive data is exposed (database tables don't exist)
3. Authentication fails gracefully (returns 401/500)

**Why still concerning?**
1. 500 errors in production look unprofessional
2. Dead code increases attack surface
3. Confusing for developers
4. May break deployment if TypeScript compilation checks Prisma schema

---

## Impact Assessment

### If Endpoints Are Called in Production

```typescript
// User tries to access admin analytics
GET /api/admin/analytics

// Error response:
{
  "error": "Failed to fetch analytics. Please try again later.",
  "errorId": "analytics_1760750685856"
}
Status: 500 Internal Server Error
```

**Server Logs**:
```
[Analytics] Error: Unknown field `analyticsEvent` on model `Prisma`
  at PrismaClient.analyticsEvent.count()
```

### User Impact
- ❌ Admin dashboard won't work
- ❌ Can't view feedback
- ❌ Can't view analytics
- ✅ Core functionality (licensing) unaffected
- ✅ No data breach risk

---

## Recommendations

### Option 1: Remove All Admin Endpoints (RECOMMENDED) ✅

**Pros**:
- Clean codebase
- No dead code
- No 500 errors
- Faster deployment
- Reduced attack surface

**Cons**:
- No admin dashboard (but it doesn't work anyway)
- Must re-implement later if needed

**Implementation** (15 minutes):
```bash
cd voicelite-web

# Remove admin endpoints
rm -rf app/api/admin/

# Remove admin auth module
rm lib/admin-auth.ts

# Remove unused API endpoints (while we're at it)
rm -rf app/api/auth/
rm -rf app/api/feedback/
rm -rf app/api/analytics/
rm app/api/me/route.ts
rm app/api/billing/portal/route.ts
```

**Also remove** (depends on missing models):
- `app/api/licenses/mine/route.ts` (requires User authentication)
- `app/api/metrics/*` (AnalyticsEvent table)

---

### Option 2: Implement User/Session System (NOT RECOMMENDED)

**Pros**:
- Admin endpoints would work
- User authentication available
- Full-featured platform

**Cons**:
- **Weeks of work** (auth system, migrations, testing)
- Scope creep (delays production launch)
- Contradicts architecture decision to simplify
- Unnecessary for current business needs

**Estimated Time**: 40-80 hours

---

### Option 3: Stub Out Endpoints (TEMPORARY SOLUTION)

**Pros**:
- Quick fix (30 minutes)
- Keeps endpoints for future use
- Returns proper error responses

**Cons**:
- Still have dead code
- Confusing for future developers
- Attack surface remains

**Implementation**:
```typescript
// app/api/admin/stats/route.ts
export async function GET(req: NextRequest) {
  return NextResponse.json(
    {
      error: 'Admin features not yet implemented',
      message: 'Admin dashboard requires user authentication system'
    },
    { status: 501 } // Not Implemented
  );
}
```

---

## Action Plan (RECOMMENDED)

### Step 1: Remove Dead Code (15 min)

```bash
cd voicelite-web

# Remove admin endpoints
rm -rf app/api/admin/
rm lib/admin-auth.ts

# Remove auth endpoints (passwordless auth not implemented)
rm -rf app/api/auth/

# Remove feedback endpoints (Feedback table doesn't exist)
rm -rf app/api/feedback/

# Remove analytics endpoints (AnalyticsEvent table doesn't exist)
rm -rf app/api/analytics/
rm -rf app/api/metrics/

# Remove other endpoints that require authentication
rm app/api/me/route.ts
rm app/api/billing/portal/route.ts
rm app/api/licenses/mine/route.ts
```

### Step 2: Keep These Endpoints ✅

**Core licensing** (working, tested, production-ready):
- ✅ `/api/checkout` - Stripe checkout session
- ✅ `/api/webhook` - Stripe webhook handler
- ✅ `/api/licenses/activate` - License activation
- ✅ `/api/licenses/validate` - License validation
- ✅ `/api/licenses/crl` - Certificate revocation list

**Optional** (check if used):
- ⚠️ `/api/licenses/deactivate` - Manual deactivation (auth required?)
- ⚠️ `/api/licenses/renew` - License renewal (implemented?)
- ⚠️ `/api/licenses/issue` - Manual license generation (admin endpoint?)
- ⚠️ `/api/test-email` - Email testing (remove before production!)

### Step 3: Verify Remaining Endpoints

For each remaining endpoint, verify:
1. ✅ Does not reference missing database models
2. ✅ Has proper input validation
3. ✅ Has rate limiting (if public)
4. ✅ Is actually used by desktop app or website

### Step 4: Document API Surface

Create `API_ENDPOINTS.md`:
```markdown
# VoiceLite API Endpoints

## Public Endpoints
- POST /api/checkout - Create Stripe checkout session
- POST /api/webhook - Stripe webhook handler
- POST /api/licenses/activate - Activate license
- POST /api/licenses/validate - Validate license
- GET /api/licenses/crl - Certificate revocation list
- GET /api/docs - API documentation (Swagger)

## Removed Endpoints (Not Implemented)
- /api/admin/* - Admin dashboard (requires user auth)
- /api/auth/* - Passwordless authentication (not implemented)
- /api/feedback/* - Feedback system (not implemented)
- /api/analytics/* - Analytics dashboard (not implemented)
```

---

## Testing After Cleanup

### 1. Build Test
```bash
npm run build
# Should compile without errors
```

### 2. TypeScript Check
```bash
npx tsc --noEmit
# Should not reference missing Prisma models
```

### 3. Runtime Test
```bash
# Start dev server
npm run dev

# Test core endpoints
curl http://localhost:3000/api/checkout
curl http://localhost:3000/api/licenses/validate

# Verify removed endpoints return 404
curl http://localhost:3000/api/admin/stats
# Should return: 404 Not Found
```

### 4. Playwright Tests
```bash
npx playwright test
# Should pass (29/39 expected)
```

---

## Files to Remove Summary

```
app/api/admin/                     # Admin dashboard endpoints
  feedback/route.ts                # 195 lines
  analytics/route.ts               # 377 lines
  stats/route.ts                   # 273 lines

app/api/auth/                      # Passwordless auth (not implemented)
  request/route.ts
  verify/route.ts
  otp/route.ts
  logout/route.ts

app/api/feedback/                  # Feedback system (not implemented)
  submit/route.ts

app/api/analytics/                 # Analytics (not implemented)
  event/route.ts

app/api/metrics/                   # Metrics upload (not implemented)
  upload/route.ts
  dashboard/route.ts

app/api/me/route.ts                # Current user endpoint (requires auth)
app/api/billing/portal/route.ts   # Stripe customer portal (requires auth)
app/api/licenses/mine/route.ts    # User's licenses (requires auth)

lib/admin-auth.ts                  # Admin authentication module
```

**Total**: ~15 files, ~1500+ lines of unused code

---

## Future Considerations

### If User Authentication Needed Later

**Scenario**: You decide to add user accounts and admin dashboard

**Steps**:
1. Restore migrations from `prisma/migrations/_archive/`
2. Add User, Session, Feedback models to schema
3. Implement authentication endpoints
4. Re-add admin endpoints
5. Implement admin UI
6. Full security audit

**Timeline**: 4-6 weeks

**Recommendation**: Only do this if there's a clear business need (e.g., SaaS dashboard for customers to manage licenses)

---

## Conclusion

### Current Status: 🔴 **BLOCKER FOR PRODUCTION**

**Problem**: Dead code will cause 500 errors if accessed

**Solution**: Remove unused endpoints (15 min of work)

**After Cleanup**: ✅ Production ready

### Final Recommendation

**Remove all admin and auth endpoints NOW** before production deployment:

1. ✅ Eliminates 500 error risk
2. ✅ Cleaner codebase
3. ✅ Faster builds
4. ✅ Reduced attack surface
5. ✅ Matches current architecture (email-only licensing)

**The admin endpoints are well-written code** - save them in git history if you need them later, but don't deploy them to production in their current broken state.

---

**Next Steps**:
1. Review this audit
2. Make decision (Option 1, 2, or 3)
3. Execute cleanup
4. Test build and core endpoints
5. Proceed with production deployment

---

**Audit Completed**: October 18, 2025
**Auditor**: Claude (Sonnet 4.5)
**Recommendation**: ⚠️ **REMOVE BEFORE PRODUCTION**

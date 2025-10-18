# VoiceLite API Endpoints

**Last Updated**: October 18, 2025
**Version**: 1.0.68

---

## Production API Surface

VoiceLite has a minimal, focused API surface optimized for licensing and payment processing.

### Active Endpoints (5 total)

All endpoints are publicly accessible and do not require authentication.

---

## 1. Checkout & Payment

### POST `/api/checkout`
Create a Stripe checkout session for Pro license purchase.

**Rate Limit**: 5 requests per minute per IP

**Request Headers**:
```
Content-Type: application/json
Origin: https://voicelite.app (required for CSRF protection)
```

**Request Body**:
```json
{
  "successUrl": "https://voicelite.app/checkout/success",
  "cancelUrl": "https://voicelite.app/checkout/cancel"
}
```

**Success Response** (200):
```json
{
  "url": "https://checkout.stripe.com/c/pay/cs_test_..."
}
```

**Error Responses**:
- `403` - CSRF validation failed (invalid Origin header)
- `429` - Rate limit exceeded
- `500` - Server error

**Security Features**:
- ✅ CSRF protection (Origin/Referer validation)
- ✅ Rate limiting (5 req/min per IP)
- ✅ Input validation (Zod)
- ✅ Stripe session expiry (24 hours)

**File**: [app/api/checkout/route.ts](app/api/checkout/route.ts:1)

---

### POST `/api/webhook`
Stripe webhook handler for payment events.

**Authentication**: Stripe signature verification

**Headers**:
```
Stripe-Signature: t=...,v1=... (required)
```

**Events Handled**:
- `checkout.session.completed` - Create license after successful payment
- `payment_intent.succeeded` - Confirm payment success
- `payment_intent.failed` - Handle payment failure

**Response**: Always returns `200` (Stripe requirement)

**Security Features**:
- ✅ Webhook signature verification
- ✅ Idempotency (prevents duplicate processing)
- ✅ Event timestamp validation
- ✅ SQL injection prevention (Prisma ORM)

**File**: [app/api/webhook/route.ts](app/api/webhook/route.ts:1)

---

## 2. License Management

### POST `/api/licenses/activate`
Activate a license on a new device.

**Rate Limit**: 10 requests per hour per IP

**Request Body**:
```json
{
  "licenseKey": "VL-XXXXXX-XXXXXX-XXXXXX",
  "machineId": "CPU_12345_MB_67890",
  "machineLabel": "John's PC" (optional)
}
```

**Success Response** (200):
```json
{
  "success": true,
  "message": "License activated successfully",
  "activation": {
    "id": "clx...",
    "licenseId": "clx...",
    "machineId": "CPU_12345_MB_67890",
    "machineLabel": "John's PC",
    "activatedAt": "2025-10-18T01:00:00.000Z"
  }
}
```

**Error Responses**:
- `400` - Invalid request (missing fields, invalid format)
- `404` - License key not found
- `403` - Device limit exceeded (already activated on max devices)
- `429` - Rate limit exceeded (10 req/hr)
- `500` - Server error

**Security Features**:
- ✅ Rate limiting (10 req/hr per IP - prevents brute force)
- ✅ Input validation (license key format, machine ID)
- ✅ Device limit enforcement (maxDevices)
- ✅ Hardware fingerprinting (CPU + Motherboard ID)
- ✅ Unique constraint (one activation per device per license)

**File**: [app/api/licenses/activate/route.ts](app/api/licenses/activate/route.ts:1)

---

### POST `/api/licenses/validate`
Validate license status and check activation.

**Request Body**:
```json
{
  "licenseKey": "VL-XXXXXX-XXXXXX-XXXXXX"
}
```

**Success Response** (200):
```json
{
  "valid": true,
  "status": "ACTIVE",
  "type": "LIFETIME",
  "email": "user@example.com"
}
```

**Error Responses**:
- `400` - Invalid request
- `404` - License not found
- `500` - Server error

**Note**: Currently has no rate limiting. Consider adding if abuse detected.

**Security Features**:
- ✅ Input validation (Zod)
- ✅ SQL injection prevention
- ⚠️ No rate limiting (could be added if needed)

**File**: [app/api/licenses/validate/route.ts](app/api/licenses/validate/route.ts:1)

---

---

## 3. Documentation

### GET `/api/docs`
Interactive API documentation (Swagger UI).

**Purpose**: Developer reference for API endpoints

**Access**: Public (no authentication required)

**Response**: HTML page with Swagger UI

**File**: [app/api/docs/route.ts](app/api/docs/route.ts:1)

---

## Removed Endpoints (Not Implemented)

The following endpoints were **removed on October 18, 2025** because they depended on database models (User, Session, Feedback, etc.) that don't exist in the current simplified architecture:

### Admin Endpoints (Removed)
- ❌ `/api/admin/feedback` - Feedback dashboard (required User/Session/Feedback models)
- ❌ `/api/admin/analytics` - Analytics dashboard (required AnalyticsEvent model)
- ❌ `/api/admin/stats` - Admin statistics (required User/Purchase/UserActivity models)

### Authentication Endpoints (Removed)
- ❌ `/api/auth/request` - Request OTP (passwordless auth not implemented)
- ❌ `/api/auth/verify` - Verify OTP (passwordless auth not implemented)
- ❌ `/api/auth/otp` - OTP management (passwordless auth not implemented)
- ❌ `/api/auth/logout` - Logout (session auth not implemented)

### Feedback & Analytics (Removed)
- ❌ `/api/feedback/submit` - Submit feedback (Feedback model doesn't exist)
- ❌ `/api/analytics/event` - Track events (AnalyticsEvent model doesn't exist)
- ❌ `/api/metrics/upload` - Upload metrics (no telemetry)
- ❌ `/api/metrics/dashboard` - Metrics dashboard (no telemetry)

### User-Specific Endpoints (Removed)
- ❌ `/api/me` - Current user info (requires authentication)
- ❌ `/api/billing/portal` - Stripe customer portal (requires authentication)
- ❌ `/api/licenses/mine` - User's licenses (requires authentication)
- ❌ `/api/licenses/deactivate` - Deactivate device (requires authentication)
- ❌ `/api/licenses/renew` - Renew license (requires authentication)
- ❌ `/api/licenses/issue` - Issue license (admin endpoint)
- ❌ `/api/licenses/crl` - Certificate Revocation List (not implemented, removed Oct 18)

### Test Endpoints (Removed)
- ❌ `/api/test-email` - Email testing (development only)

**Reason for Removal**: VoiceLite simplified from a full user authentication system to email-only licensing. These endpoints referenced database models that were removed during that simplification.

**Git History**: Code is preserved in git history and can be restored if user authentication is implemented in the future.

---

## Architecture Decisions

### Why No Authentication?

VoiceLite uses a **license-key-based** system instead of user accounts:

**Advantages**:
- ✅ Simpler for users (no account creation, no passwords)
- ✅ Faster checkout (fewer steps = higher conversion)
- ✅ Privacy-focused (no user accounts = less data stored)
- ✅ Easier to support (no password resets, account recovery, etc.)
- ✅ Smaller attack surface (no session management, no auth bypasses)

**Trade-offs**:
- ❌ No user dashboard (can't view/manage licenses online)
- ❌ No self-service deactivation (must email support)
- ❌ No license history/invoices (Stripe only)

**Decision**: Trade-offs are acceptable for current business model (one-time purchase, single license per customer typically).

### Why No Admin Dashboard?

Admin tasks can be handled via:
- **Stripe Dashboard**: Payments, refunds, customer info
- **Prisma Studio**: Database queries (`npm run db:studio`)
- **Direct API calls**: For scripted admin tasks

**Future**: If admin dashboard is needed, restore endpoints from git history and implement User/Session models.

---

## Rate Limiting Details

### Implemented Limits

| Endpoint | Limit | Window | Purpose |
|----------|-------|--------|---------|
| `/api/checkout` | 5 requests | 1 minute | Prevent checkout spam |
| `/api/licenses/activate` | 10 requests | 1 hour | Prevent license key brute force |

### Technology

- **Storage**: Upstash Redis (distributed state across serverless functions)
- **Algorithm**: Sliding window (more accurate than fixed window)
- **Fallback**: In-memory limiter (if Redis unavailable)
- **Tracking**: Per-IP address (extracted from headers)

### IP Extraction Priority

1. `x-forwarded-for` (Vercel, standard proxies)
2. `cf-connecting-ip` (Cloudflare)
3. `x-real-ip` (other proxies)
4. `request.ip` (direct connection)
5. `127.0.0.1` (fallback)

### Response Headers

When rate limited (429 status):
```
X-RateLimit-Limit: 5
X-RateLimit-Remaining: 0
X-RateLimit-Reset: 1760752700000
Retry-After: 60
```

**Implementation**: [lib/ratelimit.ts](lib/ratelimit.ts:1)

---

## Security Best Practices

### Input Validation

All endpoints use Zod for request validation:

```typescript
const schema = z.object({
  licenseKey: z.string().regex(/^VL-[A-Z0-9]{6}-[A-Z0-9]{6}-[A-Z0-9]{6}$/),
  machineId: z.string().min(6),
});
```

**Benefits**:
- Type safety
- Format validation
- Automatic error messages
- Prevents injection attacks

### CSRF Protection

POST endpoints use Origin/Referer validation:

```typescript
const origin = request.headers.get('origin');
const allowedOrigins = ['https://voicelite.app', 'http://localhost:3000'];

if (!allowedOrigins.includes(origin)) {
  return 403; // Forbidden
}
```

**Protects Against**: Cross-site request forgery

### Stripe Webhook Security

Webhook endpoint verifies Stripe signatures:

```typescript
const signature = headers.get('stripe-signature');
const event = stripe.webhooks.constructEvent(body, signature, secret);
```

**Prevents**:
- Replay attacks
- Spoofed webhooks
- Unauthorized license creation

### Database Security

- ✅ Prisma ORM (SQL injection prevention)
- ✅ Parameterized queries
- ✅ Type-safe database operations
- ✅ Connection pooling (PgBouncer)

---

## Error Handling

### Standard Error Response Format

```json
{
  "error": "Error message",
  "message": "User-friendly explanation (optional)",
  "details": [] // Validation errors (optional)
}
```

### HTTP Status Codes

| Code | Meaning | Usage |
|------|---------|-------|
| `200` | Success | Valid request processed successfully |
| `400` | Bad Request | Invalid input (missing fields, wrong format) |
| `401` | Unauthorized | Authentication required (not used currently) |
| `403` | Forbidden | CSRF validation failed, device limit exceeded |
| `404` | Not Found | License key not found, endpoint doesn't exist |
| `429` | Too Many Requests | Rate limit exceeded |
| `500` | Server Error | Unexpected error (logged for debugging) |

---

## Environment Variables

### Required for Production

```bash
# Database
DATABASE_URL=postgresql://...

# Stripe
STRIPE_SECRET_KEY=sk_live_...
STRIPE_WEBHOOK_SECRET=whsec_...
STRIPE_LIFETIME_PRICE_ID=price_...

# Ed25519 Signing Keys
LICENSE_SIGNING_PRIVATE=base64...
LICENSE_SIGNING_PUBLIC=base64...
CRL_SIGNING_PRIVATE=base64...
CRL_SIGNING_PUBLIC=base64...

# Rate Limiting (optional but recommended)
UPSTASH_REDIS_REST_URL=https://...
UPSTASH_REDIS_REST_TOKEN=...

# Application
NEXT_PUBLIC_APP_URL=https://voicelite.app
```

---

## Testing

### Local Testing

```bash
# Start dev server
npm run dev

# Test checkout
curl -X POST http://localhost:3000/api/checkout \
  -H "Content-Type: application/json" \
  -H "Origin: http://localhost:3000" \
  -d '{"successUrl":"http://localhost:3000/checkout/success","cancelUrl":"http://localhost:3000/checkout/cancel"}'

# Test license validation
curl -X POST http://localhost:3000/api/licenses/validate \
  -H "Content-Type: application/json" \
  -d '{"licenseKey":"VL-TEST12-TEST12-TEST12"}'
```

### Automated Tests

```bash
# Run Playwright tests
npx playwright test

# Test rate limiting specifically
npx playwright test tests/rate-limit.spec.ts
```

**Test Coverage**: 29/39 tests passing (74.4%)
- Homepage: 14/14 (100%)
- Rate limiting: 3/3 (100%)
- Checkout flow: 6/8 (75% - rate limited as expected)

---

## API Versioning

**Current Version**: v1 (implicit, no version in URL)

**Future**: If breaking changes needed, introduce versioning:
- `/api/v2/licenses/activate`
- Keep v1 endpoints active for backward compatibility
- Deprecate v1 after 6-12 months

---

## Changelog

### October 18, 2025
- ✅ **REMOVED**: 15 unused endpoints (admin, auth, feedback, analytics, test)
- ✅ **ADDED**: Rate limiting on `/api/checkout` (5 req/min)
- ✅ **ADDED**: Rate limiting on `/api/licenses/activate` (10 req/hr)
- ✅ **IMPROVED**: Security with Upstash Redis distributed rate limiting
- ✅ **DOCUMENTED**: Complete API surface in this file

### October 9, 2025
- ✅ Security audit and secret rotation
- ✅ Ed25519 signing key rotation

### October 1, 2025
- ✅ Simplified schema (removed User/Session models)
- ✅ Email-only licensing system

---

## Support

### For Developers

- **Swagger Docs**: https://voicelite.app/api/docs
- **GitHub Issues**: (if open source)
- **Email**: dev@voicelite.app

### For Users

- **Email**: support@voicelite.app
- **Website**: https://voicelite.app
- **Privacy Policy**: https://voicelite.app/privacy
- **Terms**: https://voicelite.app/terms

---

## Migration Guide

### If Restoring User Authentication

If you need to restore user accounts and admin endpoints in the future:

1. **Restore Database Models**:
   ```bash
   # Move migrations back from archive
   mv prisma/migrations/_archive/* prisma/migrations/

   # Apply migrations
   npx prisma migrate deploy
   ```

2. **Restore Code Files**:
   ```bash
   # Check git history for removed files
   git log --all --full-history -- app/api/admin/

   # Restore from commit
   git checkout <commit-hash> -- app/api/admin/
   git checkout <commit-hash> -- lib/admin-auth.ts
   ```

3. **Update Schema**:
   - Add User, Session, Feedback models to `prisma/schema.prisma`
   - Create new migration
   - Update existing License model to reference User

4. **Test Thoroughly**:
   - All admin endpoints
   - Authentication flow
   - Session management
   - Security (OWASP top 10)

**Estimated Time**: 2-3 weeks (including testing)

---

**Document Version**: 1.0
**Last Review**: October 18, 2025
**Next Review**: After production deployment

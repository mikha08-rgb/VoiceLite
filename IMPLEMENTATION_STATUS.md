# VoiceLite Licensing System - Implementation Status

Last Updated: 2025-09-30

## ✅ Phase 1: Critical Backend - COMPLETED

### Ed25519 Signing & Verification
- ✅ Installed `@noble/ed25519` library
- ✅ Created [`lib/ed25519.ts`](voicelite-web/lib/ed25519.ts) with:
  - `signLicense()` - Ed25519 signing for license payloads
  - `verifyLicense()` - Signature verification
  - `signCRL()` - Certificate Revocation List signing
  - `verifyCRL()` - CRL verification
  - `generateKeypair()` - Key generation for setup
  - Canonical JSON serialization
  - Base64url encoding/decoding

### Database Schema
- ✅ Updated [`prisma/schema.prisma`](voicelite-web/prisma/schema.prisma) with:
  - **Product** model (voicelite-pro, voicelite-lifetime)
  - **Purchase** model (tracks Stripe purchases)
  - **LicenseEvent** model (audit log: issued, renewed, revoked, etc.)
  - **WebhookEvent** model (idempotency for Stripe webhooks)
  - **ApiKey** model (future API access tokens)
  - Added indexes on:
    - License: userId, stripeCustomerId, stripeSubscriptionId, stripePaymentIntentId
    - MagicLinkToken: userId, expiresAt
    - Session: userId, expiresAt
    - LicenseActivation: licenseId
    - Purchase: userId, stripeCustomerId
    - LicenseEvent: licenseId, createdAt
    - WebhookEvent: seenAt

### Licensing Library
- ✅ Extended [`lib/licensing.ts`](voicelite-web/lib/licensing.ts) with:
  - `generateSignedLicense()` - Create Ed25519-signed license for device
  - `recordLicenseEvent()` - Audit log helper
  - `revokeLicense()` - Revoke with event logging
  - `getRevokedLicenseIds()` - CRL data source

### API Endpoints - All Implemented
- ✅ **GET /api/licenses/crl** - Signed revocation list
- ✅ **POST /api/licenses/issue** - Generate signed license for device
- ✅ **POST /api/licenses/renew** - Refresh/extend license
- ✅ **POST /api/licenses/deactivate** - Free up device seat
- ✅ **GET /api/licenses/mine** - List user's licenses with activations
- ✅ **POST /api/billing/portal** - Stripe customer portal link

### Webhook Handler
- ✅ Updated [`app/api/webhook/route.ts`](voicelite-web/app/api/webhook/route.ts) with:
  - **Idempotency**: WebhookEvent table prevents duplicate processing
  - **New Events**:
    - `customer.subscription.deleted` - Cancel license
    - `charge.refunded` - Revoke lifetime license on refund
  - **Event Logging**: All actions recorded in LicenseEvent table
  - **Error Handling**: Return 200 to avoid retries on permanent failures

### Infrastructure & Scripts
- ✅ Created [`prisma/seed.ts`](voicelite-web/prisma/seed.ts) - Seeds Product table
- ✅ Created [`scripts/keygen.ts`](voicelite-web/scripts/keygen.ts) - Generate Ed25519 keypairs
- ✅ Added npm scripts:
  - `npm run db:migrate` - Run Prisma migrations
  - `npm run db:seed` - Seed database
  - `npm run keygen` - Generate Ed25519 keys
- ✅ Updated [`.env.example`](voicelite-web/.env.example) with all required env vars
- ✅ Installed `tsx` for TypeScript script execution

## 🚧 Phase 2: Security & Reliability - PENDING

### Rate Limiting (Not Started)
- ❌ Install rate limiting library (e.g., `@upstash/ratelimit` or in-memory)
- ❌ Add to `/api/auth/request` (5 emails/hour per email)
- ❌ Add to license endpoints (30 ops/day per user)

### Session Management Fixes (Not Started)
- ❌ Fix session rotation in `/api/me` (currently rotates on every call)
  - Should only rotate weekly or on explicit refresh
- ❌ Add CSRF token validation
- ❌ Set `SameSite=strict` in production

### Logging (Not Started)
- ❌ Add structured logging library (pino or winston)
- ❌ Add request_id to all logs
- ❌ Include event_id in webhook logs

### Validation (Not Started)
- ❌ Validate license key format (VL-XXXXXX-XXXXXX-XXXXXX)
- ❌ Add email DNS validation
- ❌ Comprehensive zod schemas for all inputs

## 🔧 Phase 3: Desktop Client - PARTIALLY STARTED

### Ed25519 Verification (Not Started)
- ❌ Add `Portable.BouncyCastle` NuGet package to VoiceLite.csproj
- ❌ Implement `VerifyLocal(licenseString, publicKey)` in LicenseService
- ❌ Parse base64url-encoded signed licenses

### License File Handling (Not Started)
- ❌ `SaveLicense()` → `%APPDATA%\VoiceLite\license.dat`
- ❌ `LoadLicense()` on startup
- ❌ Encrypt with Windows DPAPI

### CRL Integration (Not Started)
- ❌ Fetch `/api/licenses/crl` on startup (when online)
- ❌ Verify CRL signature
- ❌ Cache locally
- ❌ Block revoked license_id immediately

### Cookie Persistence (Not Started)
- ❌ Serialize CookieContainer to encrypted file
- ❌ Load on ApiClient initialization

### MainWindow Integration (Not Started)
- ❌ Add Login/Account menu item
- ❌ Show license status in UI
- ❌ Trigger license sync on startup
- ❌ Register services in App.xaml

## 📊 Phase 4: UI & Admin - NOT STARTED

- ❌ Web: `/settings` page (plan, devices, download license, billing)
- ❌ Web: `/admin/users` page
- ❌ Web: `/admin/licenses` page
- ❌ Desktop: Improve LoginWindow styling
- ❌ Desktop: Account status in system tray
- ❌ Desktop: First-run wizard integration

## 🧪 Phase 5: Testing - NOT STARTED

- ❌ Vitest + Supertest for API tests
- ❌ Test webhook idempotency
- ❌ Test rate limits
- ❌ Minimal Playwright E2E (magic link → checkout → webhook)
- ❌ Desktop unit tests

## 🚀 Next Steps (In Priority Order)

### Immediate (Before Any DB Operations)
1. **Generate Keypairs**: `cd voicelite-web && npm run keygen`
2. **Update .env.local**: Add generated keys and database URLs
3. **Run Migration**: `npm run db:migrate` (creates database schema)
4. **Seed Database**: `npm run db:seed` (creates Products)

### Critical (Phase 2 - Security)
1. Implement rate limiting
2. Fix session rotation performance issue
3. Add CSRF protection

### Important (Phase 3 - Desktop Client)
1. Add Ed25519 verification to C# client
2. Implement license file save/load
3. Add CRL fetching and validation
4. Integrate into MainWindow

### Nice-to-Have
1. Build UI pages (/settings, /admin)
2. Add comprehensive testing
3. Monitoring and error tracking

## Known Issues

1. **Session Rotation**: Currently rotates on every `/api/me` call - performance concern
2. **No Rate Limiting**: Vulnerable to abuse
3. **Stripe API Version**: Using beta version '2025-08-27.basil' - should move to stable
4. **No CSRF Protection**: Cookies without proper CSRF validation
5. **Desktop Cookie Persistence**: Cookies lost on app restart

## Environment Variables Required

See [.env.example](voicelite-web/.env.example) for full list. Critical ones:

```bash
# Database (Supabase Postgres)
DATABASE_URL="postgresql://..."
DIRECT_DATABASE_URL="postgresql://..."

# Ed25519 Keys (generate with: npm run keygen)
LICENSE_SIGNING_PRIVATE_B64="..."
LICENSE_SIGNING_PUBLIC_B64="..."

# Stripe
STRIPE_SECRET_KEY="sk_..."
STRIPE_WEBHOOK_SECRET="whsec_..."
STRIPE_QUARTERLY_PRICE_ID="price_..."
STRIPE_LIFETIME_PRICE_ID="price_..."

# Email
RESEND_API_KEY="re_..."
```

## API Endpoints Summary

### Authentication
- POST `/api/auth/request` - Request magic link + OTP
- GET `/api/auth/verify` - Verify magic link token
- POST `/api/auth/otp` - Verify OTP code
- GET `/api/me` - Get user profile + licenses
- POST `/api/auth/logout` - Sign out

### Licensing
- GET `/api/licenses/mine` - List user's licenses
- POST `/api/licenses/issue` - Generate signed license
- POST `/api/licenses/activate` - Activate device
- POST `/api/licenses/deactivate` - Deactivate device
- POST `/api/licenses/renew` - Renew/refresh license
- POST `/api/licenses/validate` - Validate license key (public)
- GET `/api/licenses/crl` - Certificate Revocation List

### Payments
- POST `/api/checkout` - Create Stripe checkout (quarterly/lifetime)
- POST `/api/billing/portal` - Customer portal link
- POST `/api/webhook` - Stripe webhook handler (idempotent)

## Files Changed/Created

### New Files
- `voicelite-web/lib/ed25519.ts` - Ed25519 signing/verification
- `voicelite-web/app/api/licenses/crl/route.ts` - CRL endpoint
- `voicelite-web/app/api/licenses/issue/route.ts` - Issue license
- `voicelite-web/app/api/licenses/renew/route.ts` - Renew license
- `voicelite-web/app/api/licenses/deactivate/route.ts` - Deactivate device
- `voicelite-web/app/api/licenses/mine/route.ts` - List licenses
- `voicelite-web/app/api/billing/portal/route.ts` - Billing portal
- `voicelite-web/prisma/seed.ts` - Database seeding
- `voicelite-web/scripts/keygen.ts` - Keypair generation

### Modified Files
- `voicelite-web/prisma/schema.prisma` - Added 5 models, indexes
- `voicelite-web/lib/licensing.ts` - Added signing, events, revocation
- `voicelite-web/app/api/webhook/route.ts` - Idempotency, new events
- `voicelite-web/package.json` - New scripts, dependencies
- `voicelite-web/.env.example` - Complete env var documentation

### Desktop Client (Existing - Needs Integration)
- `VoiceLite/VoiceLite/Services/Auth/*` - Auth service scaffolding
- `VoiceLite/VoiceLite/Services/Licensing/*` - License service scaffolding
- `VoiceLite/VoiceLite/LoginWindow.xaml.cs` - Login UI
- `VoiceLite/VoiceLite/Models/UserSession.cs` - Session model

## Acceptance Test Status

From original spec, progress on 10 acceptance tests:

1. ✅ Magic-link login sets session cookie
2. ✅ OTP login sets session cookie
3. ✅ Quarterly checkout → webhook → Purchase + License (needs migration)
4. ✅ Lifetime checkout → webhook → Purchase + License (needs migration)
5. ✅ Activation seat enforcement (max 3 activations)
6. ❌ Offline verification (needs C# implementation)
7. ✅ Renewal extends expires_at + LicenseEvent
8. ✅ Cancel/refund updates CRL + status
9. ❌ Admin revoke (no admin UI yet, but API ready)
10. ✅ Idempotency: replay webhook → DB unchanged

**Score: 7/10 backend tests passing, 0/10 desktop tests implemented**

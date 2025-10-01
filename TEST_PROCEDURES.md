# VoiceLite Test Procedures

Complete testing procedures for Phase 3 validation before production deployment.

---

## Prerequisites

### Local Development Environment Setup

1. **Start PostgreSQL Database** (Supabase or local):
   ```bash
   # Add to .env.local
   DATABASE_URL="postgresql://..."
   ```

2. **Start Next.js Development Server**:
   ```bash
   cd voicelite-web
   npm install
   npm run dev
   ```
   Server runs at: http://localhost:3000

3. **Configure Test Environment Variables**:
   ```bash
   # voicelite-web/.env.local (for testing)
   DATABASE_URL="postgresql://..."

   # Use TEST mode credentials:
   STRIPE_SECRET_KEY="sk_test_..."
   STRIPE_WEBHOOK_SECRET="whsec_test_..."
   STRIPE_PRICE_QUARTERLY="price_test_..."
   STRIPE_PRICE_LIFETIME="price_test_..."

   RESEND_API_KEY="re_..."
   RESEND_FROM_EMAIL="test@yourdomain.com"

   UPSTASH_REDIS_REST_URL="https://..."
   UPSTASH_REDIS_REST_TOKEN="..."

   LICENSE_SIGNING_PRIVATE_B64="..." (from npm run keygen)
   LICENSE_SIGNING_PUBLIC_B64="..."

   NEXT_PUBLIC_APP_URL="http://localhost:3000"
   ```

4. **Run Database Migrations**:
   ```bash
   npx prisma migrate deploy
   npx prisma generate
   ```

---

## Test 1: Authentication Flow (Magic Link)

### Test 1.1: Magic Link Request

**Endpoint**: `POST /api/auth/request`

**Test Case 1: Successful Magic Link Request**

```bash
curl -X POST http://localhost:3000/api/auth/request \
  -H "Content-Type: application/json" \
  -H "Origin: http://localhost:3000" \
  -d '{
    "email": "test@example.com"
  }'
```

**Expected Response**:
```json
{
  "ok": true
}
```

**Expected Side Effects**:
1. ✅ User created in database (if new)
2. ✅ MagicLinkToken created with 15-minute expiry
3. ✅ Email sent with magic link and 8-digit OTP
4. ✅ Check email inbox for message

**Manual Verification**:
```bash
# Check database
npx prisma studio
# Navigate to User table → verify user exists
# Navigate to MagicLinkToken table → verify token created with expiresAt > now
```

**Test Case 2: Rate Limiting (5 requests/hour)**

```bash
# Send 6 requests rapidly
for i in {1..6}; do
  curl -X POST http://localhost:3000/api/auth/request \
    -H "Content-Type: application/json" \
    -H "Origin: http://localhost:3000" \
    -d '{"email": "ratelimit@example.com"}'
  echo ""
done
```

**Expected**: 6th request returns 429 with rate limit headers

**Test Case 3: CSRF Protection**

```bash
# Missing Origin header
curl -X POST http://localhost:3000/api/auth/request \
  -H "Content-Type: application/json" \
  -d '{"email": "test@example.com"}'
```

**Expected Response**:
```json
{
  "error": "Invalid request origin",
  "message": "This request appears to come from an unauthorized source..."
}
```

**Status**: 403

**Test Case 4: Account Enumeration Protection**

```bash
# Try with invalid email that doesn't exist
curl -X POST http://localhost:3000/api/auth/request \
  -H "Content-Type: application/json" \
  -H "Origin: http://localhost:3000" \
  -d '{"email": "nonexistent@example.com"}'
```

**Expected**: Still returns `{"ok": true}` (no information leak)

---

### Test 1.2: Magic Link Verification

**Endpoint**: `GET /api/auth/verify?token={token}`

**Test Case 1: Valid Magic Link**

1. Get token from email or database:
   ```bash
   npx prisma studio
   # MagicLinkToken table → copy tokenHash
   # Note: You need the original token (before hashing), not the hash
   ```

2. Click magic link in email or:
   ```bash
   curl -v "http://localhost:3000/api/auth/verify?token={TOKEN_FROM_EMAIL}"
   ```

**Expected**:
- Redirect to success page or redirect URI
- Set-Cookie header with session token
- Session created in database

**Manual Verification**:
```bash
# Check Session table in Prisma Studio
# Verify session.userId matches the user
# Verify session.expiresAt is 30 days from now
```

**Test Case 2: Expired Token**

```bash
# Update token expiry to past in database
# Then try to verify
curl "http://localhost:3000/api/auth/verify?token={EXPIRED_TOKEN}"
```

**Expected**: Error message about expired token

**Test Case 3: Already Consumed Token**

```bash
# Use same token twice
curl "http://localhost:3000/api/auth/verify?token={SAME_TOKEN}"
curl "http://localhost:3000/api/auth/verify?token={SAME_TOKEN}"
```

**Expected**: Second request fails with error

---

### Test 1.3: OTP Verification

**Endpoint**: `POST /api/auth/otp`

**Test Case 1: Valid 8-Digit OTP**

1. Get OTP from email (8 digits now)

2. Submit OTP:
   ```bash
   curl -X POST http://localhost:3000/api/auth/otp \
     -H "Content-Type: application/json" \
     -H "Origin: http://localhost:3000" \
     -d '{
       "email": "test@example.com",
       "otp": "12345678"
     }'
   ```

**Expected Response**:
- 200 OK with Set-Cookie header
- Session created in database

**Test Case 2: Invalid OTP**

```bash
curl -X POST http://localhost:3000/api/auth/otp \
  -H "Content-Type: application/json" \
  -H "Origin: http://localhost:3000" \
  -d '{
    "email": "test@example.com",
    "otp": "00000000"
  }'
```

**Expected**: 400 Bad Request - "Invalid code"

**Test Case 3: OTP Length Validation**

```bash
# Try 6-digit OTP (should fail)
curl -X POST http://localhost:3000/api/auth/otp \
  -H "Content-Type: application/json" \
  -H "Origin: http://localhost:3000" \
  -d '{
    "email": "test@example.com",
    "otp": "123456"
  }'
```

**Expected**: 400 Bad Request - Validation error (must be 8 digits)

**Test Case 4: Timing Attack Protection**

```bash
# Test multiple wrong OTPs - timing should be constant
time curl -X POST http://localhost:3000/api/auth/otp \
  -H "Content-Type: application/json" \
  -H "Origin: http://localhost:3000" \
  -d '{"email": "test@example.com", "otp": "11111111"}'

time curl -X POST http://localhost:3000/api/auth/otp \
  -H "Content-Type: application/json" \
  -H "Origin: http://localhost:3000" \
  -d '{"email": "test@example.com", "otp": "99999999"}'
```

**Expected**: Similar response times (constant-time comparison working)

---

### Test 1.4: Session Management

**Endpoint**: `GET /api/me`

**Test Case 1: Valid Session**

```bash
# After logging in, get session cookie
curl http://localhost:3000/api/me \
  -H "Cookie: voicelite_session={SESSION_TOKEN}"
```

**Expected Response**:
```json
{
  "user": {
    "id": "...",
    "email": "test@example.com"
  },
  "licenses": []
}
```

**Test Case 2: Rate Limiting (100 requests/hour)**

```bash
# Send 101 requests with same session
for i in {1..101}; do
  curl http://localhost:3000/api/me \
    -H "Cookie: voicelite_session={SESSION_TOKEN}"
done
```

**Expected**: 101st request returns 429

**Test Case 3: Session Rotation**

```bash
# Check session.createdAt in database
# If older than 7 days, /api/me should rotate it
# Verify by checking if sessionHash changed after request
```

**Expected**: New session token returned in Set-Cookie header

---

## Test 2: Stripe Checkout & Webhooks

### Test 2.1: Checkout Session Creation

**Endpoint**: `POST /api/checkout`

**Prerequisites**:
1. Logged in (have valid session)
2. Stripe test keys configured
3. Test products created in Stripe

**Test Case 1: Quarterly Subscription**

```bash
curl -X POST http://localhost:3000/api/checkout \
  -H "Content-Type: application/json" \
  -H "Cookie: voicelite_session={SESSION_TOKEN}" \
  -H "Origin: http://localhost:3000" \
  -d '{
    "plan": "quarterly"
  }'
```

**Expected Response**:
```json
{
  "url": "https://checkout.stripe.com/c/pay/cs_test_..."
}
```

**Manual Steps**:
1. Copy the checkout URL
2. Open in browser
3. Use Stripe test card: `4242 4242 4242 4242`
4. Expiry: Any future date
5. CVC: Any 3 digits
6. Complete checkout

**Expected After Checkout**:
1. Redirected to success URL
2. Webhook fired (check Stripe dashboard → Webhooks)
3. License created in database
4. Email sent with license key

**Test Case 2: Lifetime License**

```bash
curl -X POST http://localhost:3000/api/checkout \
  -H "Content-Type: application/json" \
  -H "Cookie: voicelite_session={SESSION_TOKEN}" \
  -H "Origin: http://localhost:3000" \
  -d '{
    "plan": "lifetime"
  }'
```

**Follow same manual steps as above**

**Test Case 3: CSRF Protection**

```bash
curl -X POST http://localhost:3000/api/checkout \
  -H "Content-Type: application/json" \
  -H "Cookie: voicelite_session={SESSION_TOKEN}" \
  -d '{"plan": "quarterly"}'
  # Missing Origin header
```

**Expected**: 403 Forbidden

---

### Test 2.2: Webhook Processing

**Endpoint**: `POST /api/webhook`

**Test Setup**:

1. Install Stripe CLI:
   ```bash
   # Windows (via Scoop)
   scoop install stripe

   # Or download from: https://stripe.com/docs/stripe-cli
   ```

2. Forward webhooks to local server:
   ```bash
   stripe login
   stripe listen --forward-to localhost:3000/api/webhook
   ```

   Copy the webhook signing secret: `whsec_...`

3. Update `.env.local`:
   ```bash
   STRIPE_WEBHOOK_SECRET="whsec_..." # from stripe listen output
   ```

**Test Case 1: checkout.session.completed (Subscription)**

```bash
# Trigger test webhook
stripe trigger checkout.session.completed
```

**Expected in Logs**:
```
✓ Webhook processed successfully
✓ License created for subscription
✓ Email sent with license key
```

**Database Verification**:
```bash
npx prisma studio
# Check License table → new license with type=SUBSCRIPTION
# Check WebhookEvent table → event recorded
```

**Test Case 2: checkout.session.completed (Lifetime)**

```bash
stripe trigger checkout.session.completed --add checkout_session:mode=payment
```

**Expected**:
- License created with type=LIFETIME
- Email sent

**Test Case 3: customer.subscription.updated**

```bash
stripe trigger customer.subscription.updated
```

**Expected**:
- License status updated in database
- LicenseEvent recorded

**Test Case 4: Idempotency (Race Condition Prevention)**

```bash
# Send same webhook twice
stripe events resend evt_test_... # Get event ID from Stripe dashboard
stripe events resend evt_test_... # Same event
```

**Expected**:
- First request: Processes successfully
- Second request: Returns "cached: true", skips processing
- No duplicate licenses created

**Test Case 5: Invalid Signature**

```bash
curl -X POST http://localhost:3000/api/webhook \
  -H "Content-Type: application/json" \
  -H "stripe-signature: invalid" \
  -d '{"type": "checkout.session.completed"}'
```

**Expected**: 400 Bad Request - "Invalid signature"

---

## Test 3: Desktop Client License Validation

### Prerequisites

1. **Build Desktop Client**:
   ```bash
   cd VoiceLite/VoiceLite
   dotnet publish -c Release -r win-x64 --self-contained
   ```

   Output: `VoiceLite/VoiceLite/bin/Release/net8.0-windows/win-x64/publish/`

2. **Verify Public Key**:
   ```bash
   # Check VoiceLite/VoiceLite/Services/Licensing/LicenseService.cs
   # Verify LICENSE_PUBLIC_KEY matches LICENSE_SIGNING_PUBLIC_B64 from .env.local
   ```

3. **Run Desktop App**:
   ```bash
   cd VoiceLite/VoiceLite/bin/Release/net8.0-windows/win-x64/publish
   ./VoiceLite.exe
   ```

---

### Test 3.1: License Issue Flow

**Test Case 1: Issue License for Desktop Client**

1. From desktop app: Click "Sign In" or "Activate License"
2. App opens browser to: `http://localhost:3000` (or production URL)
3. Log in with magic link or OTP
4. Desktop app should automatically detect login and fetch license

**Backend Endpoint**: `POST /api/licenses/issue`

**What Happens**:
1. Desktop app sends `machineId` (device fingerprint)
2. Backend generates signed license file
3. Desktop app saves to `%APPDATA%\VoiceLite\license.dat`
4. License verified locally with Ed25519 signature

**Manual API Test**:
```bash
# Get authenticated session first
curl -X POST http://localhost:3000/api/licenses/issue \
  -H "Content-Type: application/json" \
  -H "Cookie: voicelite_session={SESSION_TOKEN}" \
  -H "Origin: http://localhost:3000" \
  -d '{
    "deviceFingerprint": "TEST-MACHINE-123"
  }'
```

**Expected Response**:
```json
{
  "signedLicense": "eyJsaWNlbnNlSWQ...base64payload.base64signature"
}
```

**Test Case 2: Verify Signed License Format**

```bash
# Decode the license payload
echo "eyJsaWNlbnNlSWQ..." | base64 -d
```

**Expected JSON**:
```json
{
  "licenseId": "lic_...",
  "userId": "usr_...",
  "type": "SUBSCRIPTION" or "LIFETIME",
  "deviceFingerprint": "TEST-MACHINE-123",
  "issuedAt": "2025-01-XX...",
  "expiresAt": "2025-04-XX..." or null
}
```

---

### Test 3.2: License Activation

**Endpoint**: `POST /api/licenses/activate`

**Test Case 1: First Activation**

```bash
curl -X POST http://localhost:3000/api/licenses/activate \
  -H "Content-Type: application/json" \
  -H "Cookie: voicelite_session={SESSION_TOKEN}" \
  -H "Origin: http://localhost:3000" \
  -d '{
    "licenseKey": "lic_...",
    "machineId": "MACHINE-001",
    "machineLabel": "Work Laptop"
  }'
```

**Expected Response**:
```json
{
  "success": true,
  "activation": {
    "id": "act_...",
    "status": "active",
    "machineLabel": "Work Laptop"
  }
}
```

**Test Case 2: Activation Limit (Max 3 Devices)**

```bash
# Activate 4 times with different machineIds
for i in {1..4}; do
  curl -X POST http://localhost:3000/api/licenses/activate \
    -H "Content-Type: application/json" \
    -H "Cookie: voicelite_session={SESSION_TOKEN}" \
    -H "Origin: http://localhost:3000" \
    -d "{\"licenseKey\": \"lic_...\", \"machineId\": \"MACHINE-00$i\"}"
done
```

**Expected**: 4th activation fails with error about activation limit

**Test Case 3: Re-activation (Same Machine)**

```bash
# Activate same machineId twice
curl -X POST http://localhost:3000/api/licenses/activate \
  -H "Content-Type: application/json" \
  -H "Cookie: voicelite_session={SESSION_TOKEN}" \
  -H "Origin: http://localhost:3000" \
  -d '{"licenseKey": "lic_...", "machineId": "MACHINE-001"}'
```

**Expected**: Success (doesn't count against limit)

---

### Test 3.3: Certificate Revocation List (CRL)

**Endpoint**: `GET /api/licenses/crl`

**Test Case 1: Fetch CRL**

```bash
curl http://localhost:3000/api/licenses/crl \
  -H "Cookie: voicelite_session={SESSION_TOKEN}"
```

**Expected Response**:
```json
{
  "crl": "eyJyZXZva2VkTGljZW5zZUlkcyI6W10...signature",
  "updatedAt": "2025-01-XX..."
}
```

**Test Case 2: Verify CRL Signature**

From desktop client:
1. App fetches CRL automatically (every 24 hours)
2. Verifies signature with CRL_SIGNING_PUBLIC_KEY
3. Checks if local license is revoked

**Manual Verification**:
```csharp
// In LicenseService.cs
var crlPayload = VerifyAndParseCRL(signedCRL);
if (crlPayload != null) {
    bool isRevoked = crlPayload.RevokedLicenseIds.Contains("lic_...");
    // isRevoked should be false for valid licenses
}
```

---

## Test 4: End-to-End Integration Tests

### Test 4.1: Complete User Journey

**Steps**:

1. **User signs up**:
   - Go to http://localhost:3000
   - Enter email
   - Verify magic link email received
   - Click link → logged in

2. **User purchases subscription**:
   - Click "Upgrade to Quarterly"
   - Complete Stripe checkout (test card)
   - Verify redirect to success page
   - Check email for license

3. **Desktop app activation**:
   - Launch VoiceLite desktop app
   - Click "Sign In"
   - Browser opens, user already logged in
   - App automatically fetches and activates license
   - Verify app shows "Pro" status

4. **User uses app**:
   - Test voice typing with global hotkey
   - Verify transcription works
   - App should work fully without restrictions

5. **User checks account**:
   - Go back to http://localhost:3000
   - Verify license shown
   - Verify activation count (1/3)

**Expected**: Entire flow works seamlessly

---

### Test 4.2: Negative Test Cases

**Test Case 1: Expired License**

```bash
# Update license.expiresAt to past in database
npx prisma studio
# Set expiresAt = yesterday
```

**Expected**:
- Desktop app shows "License expired"
- User can't use Pro features
- Renewal flow should work

**Test Case 2: Revoked License**

```bash
# Add license to CRL
# Update in database or via admin panel
```

**Expected**:
- Desktop app checks CRL
- Shows "License revoked"
- Blocks usage

**Test Case 3: Invalid Signature**

```bash
# Tamper with license file
# Modify %APPDATA%\VoiceLite\license.dat
```

**Expected**:
- Signature verification fails
- App treats as unlicensed

---

## Test Results Checklist

### Authentication
- [ ] Magic link email received
- [ ] Magic link works and creates session
- [ ] 8-digit OTP works
- [ ] Rate limiting works (5 requests/hour)
- [ ] CSRF protection blocks invalid origins
- [ ] Account enumeration protection (always returns success)
- [ ] Session created with 30-day expiry

### Stripe & Payments
- [ ] Quarterly checkout creates Stripe session
- [ ] Lifetime checkout creates Stripe session
- [ ] Test payment completes successfully
- [ ] Webhook received and processed
- [ ] License created in database
- [ ] Email sent with license key
- [ ] Idempotency prevents duplicate processing

### Desktop Client
- [ ] Desktop app builds successfully
- [ ] Public key matches backend
- [ ] License issued with valid signature
- [ ] License activation works (first device)
- [ ] License activation limit enforced (max 3)
- [ ] Re-activation same device works
- [ ] CRL fetched and verified
- [ ] App works with valid license
- [ ] App blocked with expired/revoked license

### Integration
- [ ] Complete user journey works end-to-end
- [ ] No errors in server logs
- [ ] No errors in desktop app logs
- [ ] Database state correct after all operations

---

## Common Issues & Troubleshooting

### Issue: Email not received
- Check Resend dashboard for delivery status
- Verify RESEND_FROM_EMAIL domain is verified
- Check spam folder
- Verify RESEND_API_KEY is correct

### Issue: Webhook not firing
- Verify STRIPE_WEBHOOK_SECRET matches Stripe dashboard
- Check `stripe listen` is running
- Verify endpoint URL is correct
- Check webhook signature in logs

### Issue: Desktop app can't fetch license
- Verify NEXT_PUBLIC_APP_URL is correct
- Check desktop app can reach backend (firewall?)
- Verify session cookie is set
- Check browser console for errors

### Issue: License signature invalid
- Verify LICENSE_PUBLIC_KEY in LicenseService.cs matches LICENSE_SIGNING_PUBLIC_B64
- Rebuild desktop client after key change
- Check base64url encoding (no padding, URL-safe)

---

## Automated Testing (Future)

**Recommended Test Frameworks**:

1. **E2E Testing**: Playwright
   ```bash
   npm install -D @playwright/test
   ```

2. **API Testing**: Jest + Supertest
   ```bash
   npm install -D jest supertest @types/jest
   ```

3. **Desktop Testing**: C# xUnit (already in project)
   ```bash
   dotnet test VoiceLite/VoiceLite.Tests/
   ```

---

**Test Procedures Version**: 1.0
**Last Updated**: 2025-01-XX
**Status**: Ready for manual testing

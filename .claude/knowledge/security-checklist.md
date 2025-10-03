# Security Audit Checklist for VoiceLite

## Critical Security Issues (BLOCK RELEASE)

### Hardcoded Secrets
**Pattern**: API keys, passwords, tokens in source code
**Risk**: Credentials exposed in version control, public repos
**Search Patterns**:
```regex
(sk_live|pk_live|api_key\s*=|password\s*=|secret\s*=|token\s*=)["']?[A-Za-z0-9_-]{20,}
stripe_secret_key\s*=\s*["']sk_
STRIPE_SECRET_KEY\s*=\s*["']sk_live
```

**Examples**:
```typescript
// ❌ CRITICAL - Hardcoded Stripe key
const stripeKey = 'sk_live_abc123xyz...';

// ✅ CORRECT
const stripeKey = process.env.STRIPE_SECRET_KEY!;
```

**Fix**: Move to environment variables (`.env.local`, `.env`)

### SQL Injection
**Pattern**: Unsanitized user input in SQL queries
**Risk**: Database breach, data theft
**Search Patterns**:
```csharp
ExecuteRawSql
FromSqlRaw
SqlCommand.*CommandText.*\+
```

**Examples**:
```csharp
// ❌ CRITICAL - SQL injection vulnerability
var sql = $"SELECT * FROM Users WHERE Email = '{email}'";
db.ExecuteRawSql(sql);

// ✅ CORRECT - Parameterized query
var sql = "SELECT * FROM Users WHERE Email = @email";
db.ExecuteRawSql(sql, new { email });
```

**Fix**: Always use parameterized queries or ORM

### Cross-Site Scripting (XSS)
**Pattern**: Unsanitized user input rendered as HTML
**Risk**: Session hijacking, credential theft
**Search Patterns**:
```typescript
innerHTML
dangerouslySetInnerHTML
document.write
eval\(
```

**Examples**:
```typescript
// ❌ CRITICAL - XSS vulnerability
element.innerHTML = userInput;

// ❌ CRITICAL - React XSS
<div dangerouslySetInnerHTML={{ __html: userInput }} />

// ✅ CORRECT - Escaped by default
<div>{userInput}</div>
```

**Fix**: Use framework escaping, sanitize HTML with DOMPurify

### Authentication Bypass
**Pattern**: Missing or broken auth checks
**Risk**: Unauthorized access to protected resources
**Check Points**:
- API routes without auth middleware
- Admin functions accessible to non-admins
- JWT/session validation skipped

**Examples**:
```typescript
// ❌ CRITICAL - No auth check
export async function GET(req: Request) {
  return getUserData(); // Anyone can access!
}

// ✅ CORRECT
export async function GET(req: Request) {
  const session = await getSession(req);
  if (!session) {
    return new Response('Unauthorized', { status: 401 });
  }
  return getUserData(session.userId);
}
```

---

## High Security Issues (FIX BEFORE RELEASE)

### Missing Rate Limiting
**Pattern**: No rate limiting on sensitive endpoints
**Risk**: Brute force attacks, DoS
**Vulnerable Endpoints**:
- Login/authentication
- Password reset
- Payment checkout
- API endpoints

**Fix**:
```typescript
import { Ratelimit } from '@upstash/ratelimit';

const limiter = Ratelimit.slidingWindow(5, '1 m'); // 5 requests per minute

export async function POST(req: Request) {
  const ip = req.headers.get('x-forwarded-for') ?? 'anonymous';
  const { success } = await limiter.limit(ip);

  if (!success) {
    return new Response('Too many requests', { status: 429 });
  }

  // Process request...
}
```

### Insecure Stripe Webhook
**Pattern**: Webhook without signature verification
**Risk**: Fake payment notifications, fraudulent licenses
**Check**:
```typescript
// ❌ HIGH RISK - No signature verification
export async function POST(req: Request) {
  const event = await req.json(); // Trusting unverified data!
}

// ✅ SECURE
const sig = req.headers.get('stripe-signature');
const event = stripe.webhooks.constructEvent(body, sig, webhookSecret);
```

### CSRF Vulnerabilities
**Pattern**: State-changing operations without CSRF protection
**Risk**: Unauthorized actions on behalf of user
**Fix**: Validate request origin
```typescript
const origin = req.headers.get('origin');
const allowedOrigins = [
  'https://voicelite.app',
  'http://localhost:3000', // Dev only
];

if (!allowedOrigins.includes(origin)) {
  return new Response('Forbidden', { status: 403 });
}
```

### Weak Password/Auth Policies
**Pattern**: Short passwords, no 2FA, no account lockout
**Risk**: Account takeover
**Requirements**:
- Minimum 12 characters (VoiceLite uses magic links - N/A)
- Account lockout after N failed attempts
- Session timeout after inactivity

### Unencrypted Sensitive Data
**Pattern**: Sensitive data stored in plaintext
**Risk**: Data breach impact
**Check**:
- License keys stored encrypted
- Payment info not stored (Stripe handles this)
- User PII encrypted at rest

---

## Medium Security Issues (FIX THIS SPRINT)

### Localhost URLs in Production
**Pattern**: `http://localhost` in production code
**Risk**: Broken functionality in production
**Search Pattern**: `http://localhost`

**Fix**:
```typescript
// ❌ MEDIUM - Hardcoded localhost
success_url: 'http://localhost:3000/success'

// ✅ CORRECT
success_url: `${process.env.NEXT_PUBLIC_BASE_URL}/success`
```

### Debug/Trace Logging in Production
**Pattern**: Console.WriteLine, console.log with sensitive data
**Risk**: Information disclosure
**Search Patterns**:
```csharp
Console.WriteLine
System.Diagnostics.Trace
Debug.WriteLine
console.log.*password|token|key
```

**Fix**:
```csharp
// ❌ MEDIUM - Debug logging in production
Console.WriteLine($"User password: {password}");

// ✅ CORRECT - Use ErrorLogger, redact sensitive data
ErrorLogger.Log($"Login attempt for user: {email}");
```

### Timeout Configuration Hardcoded
**Pattern**: Timeouts hardcoded instead of configurable
**Risk**: DoS via resource exhaustion
**Example**:
```csharp
// ❌ MEDIUM - Hardcoded timeout
var timeout = 30000; // 30 seconds

// ✅ BETTER - Configurable
var timeout = settings.TranscriptionTimeout ?? 30000;
```

### HTTP in Development Mode
**Pattern**: HTTP URLs in development that should be HTTPS
**Risk**: Accidental HTTP in production
**Fix**: Enforce HTTPS in all environments
```typescript
const baseUrl = process.env.NODE_ENV === 'production'
  ? 'https://voicelite.app'
  : 'https://localhost:3000'; // Use HTTPS even in dev
```

### Missing Input Validation
**Pattern**: User input not validated
**Risk**: Unexpected behavior, crashes
**Fix**: Use Zod/Joi schemas
```typescript
import { z } from 'zod';

const CheckoutSchema = z.object({
  email: z.string().email(),
  plan: z.enum(['subscription', 'lifetime']),
});

export async function POST(req: Request) {
  const body = await req.json();
  const { email, plan } = CheckoutSchema.parse(body); // Throws if invalid
  // ...
}
```

---

## Low Security Issues (BACKLOG)

### Missing Security Headers
**Pattern**: No Content-Security-Policy, X-Frame-Options
**Risk**: Clickjacking, XSS
**Fix** (Next.js):
```typescript
// next.config.js
module.exports = {
  async headers() {
    return [
      {
        source: '/:path*',
        headers: [
          { key: 'X-Frame-Options', value: 'DENY' },
          { key: 'X-Content-Type-Options', value: 'nosniff' },
          { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
        ],
      },
    ];
  },
};
```

### Error Messages Too Detailed
**Pattern**: Stack traces, internal paths exposed to users
**Risk**: Information disclosure
**Fix**:
```csharp
// ❌ LOW - Detailed error
catch (Exception ex) {
    MessageBox.Show($"Error: {ex.Message}\n{ex.StackTrace}");
}

// ✅ BETTER
catch (Exception ex) {
    ErrorLogger.Log(ex);
    MessageBox.Show("An error occurred. Please try again.");
}
```

### Outdated Dependencies
**Pattern**: Old packages with known CVEs
**Risk**: Exploitable vulnerabilities
**Fix**: Run dependency audits
```bash
# C#/.NET
dotnet list package --vulnerable

# Node.js/npm
npm audit
npm audit fix
```

### No Audit Logging
**Pattern**: Security-relevant events not logged
**Risk**: Can't detect or investigate breaches
**Events to Log**:
- Login attempts (success/failure)
- Password resets
- License activations
- Admin actions
- Payment events

**Example**:
```csharp
public async Task<bool> Login(string email)
{
    var success = await AuthService.SendMagicLink(email);

    // Audit log
    AuditLogger.Log(new AuditEvent
    {
        Type = "LOGIN_ATTEMPT",
        Email = email,
        Success = success,
        Timestamp = DateTime.UtcNow,
        IpAddress = GetClientIp(),
    });

    return success;
}
```

---

## Security Scan Procedure

### 1. Scan for Secrets
```bash
# Using grep
grep -r "sk_live" .
grep -r "api_key.*=" .
grep -r "password.*=" .

# Or use specialized tools
npm install -g @trufflesecurity/trufflehog
trufflehog git file://. --json
```

### 2. Scan for SQL Injection
```bash
# C# code
grep -r "ExecuteRawSql" VoiceLite/
grep -r "FromSqlRaw" VoiceLite/
grep -r "SqlCommand.*CommandText.*+" VoiceLite/
```

### 3. Scan for XSS
```bash
# TypeScript/React code
grep -r "innerHTML" voicelite-web/
grep -r "dangerouslySetInnerHTML" voicelite-web/
grep -r "document.write" voicelite-web/
```

### 4. Check Authentication
- Manual review of API routes
- Verify auth middleware on protected routes
- Test unauthenticated access attempts

### 5. Check Stripe Webhook Security
```bash
# Verify signature verification present
grep -A 10 "stripe.webhooks.constructEvent" voicelite-web/app/api/webhook/
```

### 6. Check Rate Limiting
```bash
# Check for rate limiting middleware
grep -r "Ratelimit\|rate-limit" voicelite-web/app/api/
```

---

## VoiceLite-Specific Security Considerations

### Desktop App Security
1. **Process Injection**: Whisper.exe spawned with validated paths
2. **File System Access**: Temp files in AppData, not Program Files
3. **Keyboard Injection**: May trigger antivirus (expected, not a vulnerability)
4. **Settings Storage**: JSON in AppData (not encrypted - low-risk local data)

### License Validation Security
1. **Ed25519 Signatures**: Cryptographically signed licenses
2. **CRL Checks**: Certificate Revocation List for revoked licenses
3. **Offline Validation**: Desktop app validates without server after initial fetch
4. **Machine Fingerprinting**: CPU ID + Machine GUID (SHA-256 hashed)

### Privacy Considerations
1. **No Recording Storage**: Audio deleted immediately after transcription
2. **No Transcription Storage**: Text not persisted on server
3. **Analytics Opt-In**: SHA-256 anonymous IDs, no PII
4. **Local History**: Transcription history stored locally only

---

## Security Testing Checklist

Before each release, verify:
- [ ] No hardcoded secrets (grep for `sk_live`, `api_key`, `password`)
- [ ] No SQL injection (check `ExecuteRawSql`, `FromSqlRaw`)
- [ ] No XSS vulnerabilities (check `innerHTML`, `dangerouslySetInnerHTML`)
- [ ] Stripe webhook signature verification present
- [ ] Rate limiting on authentication endpoints
- [ ] CSRF protection (origin validation)
- [ ] Input validation with Zod schemas
- [ ] HTTPS enforced (no HTTP URLs in production)
- [ ] No localhost URLs in production code
- [ ] No debug logging with sensitive data
- [ ] Dependencies up to date (`npm audit`, `dotnet list package --vulnerable`)
- [ ] Error messages don't expose stack traces
- [ ] Security headers configured (CSP, X-Frame-Options)
- [ ] Audit logging for security events

---

## Incident Response

### If Secrets Are Committed
1. **Immediately rotate** all exposed credentials
2. **Revoke** old credentials in service (Stripe, etc.)
3. **Notify** affected users if necessary
4. **Remove** from git history: `git filter-branch` or BFG Repo-Cleaner
5. **Audit** for unauthorized access using old credentials

### If Vulnerability Discovered
1. **Assess severity** (CRITICAL/HIGH/MEDIUM/LOW)
2. **Patch immediately** if CRITICAL or HIGH
3. **Test fix** thoroughly
4. **Deploy** via hotfix release
5. **Notify users** if data breach occurred (legal requirement)
6. **Document** in security advisory

---

## References
- OWASP Top 10: https://owasp.org/www-project-top-ten/
- Stripe Security: https://stripe.com/docs/security
- .NET Security: https://docs.microsoft.com/en-us/aspnet/core/security/
- Next.js Security Headers: https://nextjs.org/docs/advanced-features/security-headers

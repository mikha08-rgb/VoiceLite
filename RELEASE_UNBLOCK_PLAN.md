# VoiceLite Release Unblock Plan

**Created**: 2025-10-11
**Current Status**: ❌ **BLOCKED** - 2 critical issues preventing release
**Target**: Unblock release in 4-5 days

---

## 🚨 Blocking Issues Summary

### Issue 1: SEC-001 - Exposed Credentials in Git History ⚠️ CRITICAL
**Status**: Partially fixed (removed from working directory, still in git history)
**Risk**: High - Anyone with repo access can extract Ed25519 private keys from commit history
**Impact**: Attackers can forge unlimited VoiceLite Pro licenses
**Timeline**: 24-48 hours to fix

**Exposed Secrets** (in commits `00f4f32` and earlier):
```
LICENSE_SIGNING_PRIVATE="vS89Zv4vrDNoM9zXm5aAsba-FwFq_zb9maVey2V7L5k"
LICENSE_SIGNING_PUBLIC="fRR5l40q-wt8ptAFcOGsWIBHtLDBjnb_T3Z9HMLwgCc"
CRL_SIGNING_PRIVATE="qmXC7vEDAK1XLsSHttTbAa_L71JDmJW_zeNcsPOhWZE"
CRL_SIGNING_PUBLIC="19Y5ul1S-ISjja7f827O5epfupvaBBMyhb_uVWLLf8M"
MIGRATION_SECRET="443ed3297b3a26ba4684129e59c72c6b6ce4a944344ef2579df2bdeba7d54210"
```

**Files in History**:
- `voicelite-web/add-secrets-to-vercel.sh`
- `voicelite-web/SECRET_ROTATION_COMPLETE.md`

---

### Issue 2: TEST-001 - Incomplete Test Coverage for Freemium System ⚠️ CRITICAL
**Status**: 30% complete (16/53 tests)
**Risk**: Medium - Untested license validation could fail in production
**Impact**: Users unable to activate Pro licenses, support tickets, lost revenue
**Timeline**: 2-3 days to fix

**Missing Tests**:
- LicenseValidator async validation: 12 tests (skipped, need HttpClient mocking)
- SimpleModelSelector license gating: 12 tests (0 written)
- Settings license persistence: 10 tests (0 written)
- Integration tests (end-to-end): 5 tests (0 written)
- UI validation flow: 4 tests (0 written)

---

## 📋 5-Phase Unblock Plan

### Phase 1: Secure the Repository (4-8 hours) ⚠️ URGENT

**Goal**: Remove exposed credentials from git history and rotate all secrets

#### Step 1.1: Push Current Commits (5 minutes)
```bash
cd "C:\Users\mishk\Codingprojects\SpeakLite\HereWeGoAgain v3.3 Fuck"
git push origin master
```

**Why**: Ensure security fixes and documentation updates are saved before history rewrite

---

#### Step 1.2: Scrub Git History with BFG Repo-Cleaner (1-2 hours)

**Install BFG** (if not already installed):
```bash
# Download from: https://rtyley.github.io/bfg-repo-cleaner/
# Or via Chocolatey:
choco install bfg-repo-cleaner
```

**Backup Repository First**:
```bash
cd ..
cp -r "HereWeGoAgain v3.3 Fuck" "HereWeGoAgain-v3.3-BACKUP"
cd "HereWeGoAgain v3.3 Fuck"
```

**Create File List to Delete**:
```bash
# Create deletions.txt
echo "add-secrets-to-vercel.sh" > deletions.txt
echo "SECRET_ROTATION_COMPLETE.md" >> deletions.txt
```

**Run BFG Repo-Cleaner**:
```bash
# Clone fresh mirror (required for BFG)
cd ..
git clone --mirror https://github.com/mikha08-rgb/VoiceLite.git VoiceLite-mirror

# Run BFG to delete files from history
cd VoiceLite-mirror
bfg --delete-files add-secrets-to-vercel.sh
bfg --delete-files SECRET_ROTATION_COMPLETE.md

# Clean up refs
git reflog expire --expire=now --all
git gc --prune=now --aggressive

# Push rewritten history (DESTRUCTIVE - CANNOT UNDO)
git push --force

# Return to working directory
cd "../HereWeGoAgain v3.3 Fuck"
git fetch origin
git reset --hard origin/master
```

**Verify Secrets Removed**:
```bash
# Search for exposed keys in history
git log --all --full-history -p -S "vS89Zv4vrDNoM9zXm5aAsba"
# Should return: nothing

git log --all --full-history -- "*add-secrets-to-vercel.sh"
# Should return: nothing
```

**⚠️ WARNING**: This rewrites git history. All collaborators must re-clone the repo.

---

#### Step 1.3: Generate New Ed25519 Keypairs (15 minutes)

**Option A: Using OpenSSL**:
```bash
cd voicelite-web

# Generate new license signing keypair
openssl genpkey -algorithm ed25519 -out license_private.pem
openssl pkey -in license_private.pem -pubout -out license_public.pem

# Extract base64-encoded keys
openssl pkey -in license_private.pem -outform DER | base64
openssl pkey -pubin -in license_public.pem -outform DER | base64

# Generate new CRL signing keypair
openssl genpkey -algorithm ed25519 -out crl_private.pem
openssl pkey -in crl_private.pem -pubout -out crl_public.pem

openssl pkey -in crl_private.pem -outform DER | base64
openssl pkey -pubin -in crl_public.pem -outform DER | base64
```

**Option B: Using Node.js Script** (Recommended):
```bash
cd voicelite-web

# Use existing keygen script
npm run keygen -- --rotate

# This generates:
# - New Ed25519 keypairs
# - New migration secret
# - Outputs to .env.local (gitignored)
```

**Save New Keys Securely**:
1. Copy to password manager (1Password, LastPass, etc.)
2. DO NOT commit to repository
3. Add to Vercel environment variables via UI
4. Add to local `.env.local` (gitignored)

---

#### Step 1.4: Rotate All Production Credentials (2-4 hours)

**Database (Supabase)**:
```bash
# 1. Log into Supabase dashboard: https://supabase.com/dashboard
# 2. Project → Settings → Database → Reset password
# 3. Copy new password to .env.local:
DATABASE_URL="postgresql://postgres:[NEW_PASSWORD]@db.xxx.supabase.co:5432/postgres"
DIRECT_DATABASE_URL="postgresql://postgres:[NEW_PASSWORD]@db.xxx.supabase.co:5432/postgres"
```

**Stripe API Keys**:
```bash
# 1. Log into Stripe: https://dashboard.stripe.com
# 2. Developers → API keys → Roll secret key
# 3. Copy new keys to .env.local:
STRIPE_SECRET_KEY="sk_live_NEW_KEY_HERE"
NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY="pk_live_NEW_KEY_HERE"

# 4. Update webhook secret:
# Developers → Webhooks → Endpoint → Signing secret
STRIPE_WEBHOOK_SECRET="whsec_NEW_SECRET_HERE"
```

**Resend API Key** (Email Service):
```bash
# 1. Log into Resend: https://resend.com/api-keys
# 2. Revoke old key
# 3. Create new key
RESEND_API_KEY="re_NEW_KEY_HERE"
```

**Upstash Redis** (Rate Limiting):
```bash
# 1. Log into Upstash: https://console.upstash.com
# 2. Database → Configuration → Rotate token
UPSTASH_REDIS_REST_URL="https://xxx.upstash.io"
UPSTASH_REDIS_REST_TOKEN="NEW_TOKEN_HERE"
```

**Update Vercel Environment Variables**:
```bash
# Option A: Via Vercel Dashboard
# 1. Go to: https://vercel.com/[your-project]/settings/environment-variables
# 2. Update each variable
# 3. Redeploy: vercel --prod

# Option B: Via Vercel CLI
vercel env add LICENSE_SIGNING_PRIVATE
vercel env add LICENSE_SIGNING_PUBLIC
vercel env add CRL_SIGNING_PRIVATE
vercel env add CRL_SIGNING_PUBLIC
vercel env add MIGRATION_SECRET
vercel env add DATABASE_URL
vercel env add STRIPE_SECRET_KEY
vercel env add RESEND_API_KEY
vercel env add UPSTASH_REDIS_REST_TOKEN

# Redeploy
vercel --prod
```

---

#### Step 1.5: Update Desktop App with New Public Keys (30 minutes)

**File**: `VoiceLite/VoiceLite/Services/LicenseValidator.cs`

**Current Code** (Lines 20-24 - hypothetical, if keys are hardcoded):
```csharp
private const string LICENSE_PUBLIC_KEY = "fRR5l40q-wt8ptAFcOGsWIBHtLDBjnb_T3Z9HMLwgCc"; // OLD KEY
private const string CRL_PUBLIC_KEY = "19Y5ul1S-ISjja7f827O5epfupvaBBMyhb_uVWLLf8M"; // OLD KEY
```

**Updated Code**:
```csharp
private const string LICENSE_PUBLIC_KEY = "NEW_PUBLIC_KEY_HERE"; // Generated in Step 1.3
private const string CRL_PUBLIC_KEY = "NEW_CRL_PUBLIC_KEY_HERE"; // Generated in Step 1.3
```

**If keys are fetched from API instead**: No desktop app changes needed, just deploy backend.

---

#### Step 1.6: Invalidate Old Licenses (15 minutes)

**Database Migration** (voicelite-web):
```sql
-- Mark all licenses issued with old keys as REVOKED
UPDATE "License"
SET status = 'REVOKED',
    "updatedAt" = NOW()
WHERE "createdAt" < '2025-10-12 00:00:00' -- Before key rotation
  AND status = 'ACTIVE';

-- Log revocation reason
INSERT INTO "AuditLog" ("userId", "action", "details", "createdAt")
SELECT "userId", 'LICENSE_REVOKED', 'Security incident: Ed25519 key rotation', NOW()
FROM "License"
WHERE "updatedAt" > '2025-10-11 21:00:00';
```

**Alternative**: Keep old licenses valid if you can verify they weren't forged (check payment records in Stripe).

---

### Phase 2: Complete Test Coverage (2-3 days)

**Goal**: Bring test coverage from 30% to 80%+ for freemium system

#### Step 2.1: Refactor LicenseValidator for Testability (4 hours)

**Problem**: Static HttpClient can't be mocked in tests (ARCH-002)

**Solution**: Use Dependency Injection with IHttpClientFactory

**Current Code** (LicenseValidator.cs):
```csharp
public class LicenseValidator
{
    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public static async Task<ValidationResponse> ValidateAsync(string licenseKey)
    {
        var response = await _httpClient.PostAsync(...);
        // ...
    }
}
```

**Refactored Code**:
```csharp
public interface ILicenseValidator
{
    Task<ValidationResponse> ValidateAsync(string licenseKey);
    bool IsValidFormat(string licenseKey);
}

public class LicenseValidator : ILicenseValidator
{
    private readonly HttpClient _httpClient;

    public LicenseValidator(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("LicenseAPI");
    }

    public async Task<ValidationResponse> ValidateAsync(string licenseKey)
    {
        var response = await _httpClient.PostAsync(...);
        // ...
    }

    public bool IsValidFormat(string licenseKey)
    {
        // Existing implementation
    }
}
```

**Register in DI Container** (if using, or create factory):
```csharp
// In Startup.cs or Program.cs (if .NET 8 minimal hosting)
services.AddHttpClient("LicenseAPI", client =>
{
    client.BaseAddress = new Uri("https://voicelite.app");
    client.Timeout = TimeSpan.FromSeconds(10);
});

services.AddSingleton<ILicenseValidator, LicenseValidator>();
```

**Update Callers** (SettingsWindowNew.xaml.cs):
```csharp
// Before:
var response = await LicenseValidator.ValidateAsync(licenseKey);

// After:
var validator = new LicenseValidator(httpClientFactory); // Or inject
var response = await validator.ValidateAsync(licenseKey);
```

---

#### Step 2.2: Add Mocked HttpClient Tests (4 hours)

**Create Mock Helper**:
```csharp
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responseFactory;

    public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory)
    {
        _responseFactory = responseFactory;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return _responseFactory(request);
    }
}
```

**Un-skip ValidateAsync Tests**:
```csharp
[Fact] // Remove Skip attribute
public async Task ValidateAsync_ValidLicenseKey_ReturnsValidTrue()
{
    // Arrange
    var mockHandler = new MockHttpMessageHandler(async request =>
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            valid = true,
            status = "ACTIVE",
            type = "LIFETIME",
            email = "test@example.com",
            expiresAt = (string)null
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };
    });

    var httpClient = new HttpClient(mockHandler)
    {
        BaseAddress = new Uri("https://voicelite.app")
    };

    var mockFactory = new Mock<IHttpClientFactory>();
    mockFactory.Setup(f => f.CreateClient("LicenseAPI")).Returns(httpClient);

    var validator = new LicenseValidator(mockFactory.Object);

    // Act
    var response = await validator.ValidateAsync("VL-VALID0-123456-789ABC");

    // Assert
    response.valid.Should().BeTrue();
    response.email.Should().Be("test@example.com");
}
```

**Implement All 12 Skipped Tests**: Network errors, timeouts, 404, 500, malformed JSON, etc.

---

#### Step 2.3: Add SimpleModelSelector Tests (5 hours)

**Create**: `VoiceLite.Tests/Controls/SimpleModelSelectorTests.cs`

**Test Cases** (12 total):
```csharp
[Fact] public void Initialize_NoLicense_DisablesProModel()
[Fact] public void Initialize_InvalidLicense_DisablesProModel()
[Fact] public void Initialize_ValidLicense_EnablesProModel()
[Fact] public void Initialize_ExpiredLicense_DisablesProModel()
[Fact] public void CheckLicenseGating_NullSettings_DoesNotCrash()
[Fact] public void CheckLicenseGating_EmptyLicenseKey_DisablesProModel()
[Fact] public void CheckLicenseGating_WhitespaceLicenseKey_DisablesProModel()
[Fact] public void CheckModelAvailability_ProModelMissing_DisablesRadio()
[Fact] public void CheckModelAvailability_ProModelExists_EnablesIfLicensed()
[Fact] public void ModelRadio_SelectProWithoutLicense_BlocksSelection()
[Fact] public void ModelRadio_SelectProWithValidLicense_Succeeds()
[Fact] public void UpdateTip_ProModel_ShowsAccurateDescription()
```

**Challenge**: WPF UI testing is difficult. Options:
1. Use TestStack.White (UI automation framework)
2. Extract logic to testable service
3. Manual testing checklist (documented in tests)

---

#### Step 2.4: Add Settings Persistence Tests (3 hours)

**Extend**: `VoiceLite.Tests/Models/SettingsTests.cs`

**Test Cases** (10 total):
```csharp
[Fact] public void Settings_LicenseKey_PersistsAcrossSerialization()
[Fact] public void Settings_LicenseIsValid_PersistsAcrossSerialization()
[Fact] public void Settings_LicenseValidatedAt_PersistsAcrossSerialization()
[Fact] public void Settings_ClearLicenseKey_ClearsValidationFlag()
[Fact] public void Settings_SetLicenseKey_DoesNotAutoValidate()
[Fact] public void Settings_JSONRoundTrip_IncludesLicenseFields()
[Fact] public void Settings_LicenseValidatedAt_UTC_Format()
[Fact] public void Settings_LicenseKey_TrimmedOnSave()
[Fact] public void Settings_LicenseIsValid_DefaultsFalse()
[Fact] public void Settings_LoadLegacySettings_MigratesLicenseFields()
```

---

#### Step 2.5: Add Integration Tests (4 hours)

**Create**: `VoiceLite.Tests/Integration/LicenseFlowTests.cs`

**Test Cases** (5 total):
```csharp
[Fact] public async Task FullLicenseFlow_ValidateAndSelectProModel_Succeeds()
[Fact] public async Task FullLicenseFlow_InvalidLicense_BlocksProModel()
[Fact] public async Task FullLicenseFlow_NetworkError_ShowsErrorKeepsCurrentModel()
[Fact] public async Task FullLicenseFlow_SettingsPersistence_LicenseDataIntact()
[Fact] public async Task FullLicenseFlow_AppRestart_RemembersValidLicense()
```

---

### Phase 3: Fix High-Priority Architecture Issues (1 week)

**Goal**: Address remaining HIGH priority issues from CRITICAL_ISSUES_REPORT.md

#### Step 3.1: Fix ARCH-003 - Thread Safety in Settings Save (4 hours)

**File**: `VoiceLite/VoiceLite/MainWindow.xaml.cs` (Lines 442-500)

**Problem**: Race condition between settings modification and serialization

**Solution**: Serialize inside lock, then write outside
```csharp
private async Task SaveSettingsInternalAsync()
{
    // Capture UI state
    bool minimizeToTray = false;
    await Dispatcher.InvokeAsync(() => {
        minimizeToTray = MinimizeCheckBox.IsChecked == true;
    });

    await saveSettingsSemaphore.WaitAsync();
    try
    {
        // CRITICAL FIX: Create JSON while holding lock
        string json;
        lock (settings.SyncRoot)
        {
            settings.MinimizeToTray = minimizeToTray;
            json = JsonSerializer.Serialize(settings, _jsonSerializerOptions);
        }
        // Lock released - write to disk without holding lock

        string tempPath = GetSettingsPath() + ".tmp";
        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, GetSettingsPath(), overwrite: true);
    }
    finally
    {
        saveSettingsSemaphore.Release();
    }
}
```

---

#### Step 3.2: Fix ARCH-004 - Whisper Process Memory Leak (6 hours)

**File**: `VoiceLite/VoiceLite/Services/PersistentWhisperService.cs` (Lines 272-526)

**Problem**: Semaphore never released if Dispose() called during transcription

**Solution**: Always release semaphore in finally block
```csharp
// In TranscribeAsync
finally
{
    if (semaphoreAcquired)
    {
        try
        {
            transcriptionSemaphore.Release();
        }
        catch (ObjectDisposedException)
        {
            ErrorLogger.LogWarning("Semaphore disposed during active transcription");
        }
    }
    process?.Dispose();
}
```

**Also Fix Disposal**:
```csharp
public void Dispose()
{
    if (isDisposed) return;
    isDisposed = true;

    // Wait for active transcription to complete
    bool acquired = transcriptionSemaphore.Wait(TimeSpan.FromSeconds(10));
    if (acquired)
    {
        transcriptionSemaphore.Release();
    }

    transcriptionSemaphore.Dispose();
    // ... other disposal
}
```

---

#### Step 3.3: Add SEC-003 - Rate Limiting to Validation Endpoint (2 hours)

**File**: `voicelite-web/app/api/licenses/validate/route.ts`

**Add Rate Limiting**:
```typescript
import { checkRateLimit } from '@/lib/ratelimit';

export async function POST(request: NextRequest) {
  const { licenseKey } = await request.json();

  // Rate limit: 100 validations/hour per license key
  const rateLimit = await checkRateLimit(`validate:${licenseKey}`, {
    limit: 100,
    window: 3600,
  });

  if (!rateLimit.allowed) {
    return NextResponse.json(
      { valid: false, error: 'Rate limit exceeded' },
      { status: 429 }
    );
  }

  // ... existing validation logic
}
```

---

### Phase 4: Manual Testing (1 day)

**Goal**: Verify all changes work end-to-end

#### Manual Test Checklist:

**License Validation**:
- [ ] Open Settings → Pro License
- [ ] Enter invalid license key → Shows error
- [ ] Enter valid license key → Shows success with email
- [ ] Close app → Reopen → License still valid
- [ ] Try to select Pro model without license → Blocked with message
- [ ] Validate license → Pro model unlocked

**Settings Persistence**:
- [ ] Validate license → Change other settings → Save → Restart app
- [ ] License key still present
- [ ] License validation timestamp preserved
- [ ] No corruption in settings.json

**Network Errors**:
- [ ] Disconnect internet → Try to validate → User-friendly timeout message
- [ ] Reconnect → Validation works

**Model Selection**:
- [ ] Free tier: Tiny model available
- [ ] Without license: Pro model grayed out with tooltip
- [ ] With license: Pro model selectable

---

### Phase 5: Deploy & Monitor (1 day)

#### Deployment Steps:

**Backend** (voicelite-web):
```bash
cd voicelite-web

# Verify all env vars set
vercel env ls

# Deploy to production
vercel --prod

# Verify deployment
curl https://voicelite.app/api/licenses/validate \
  -X POST \
  -H "Content-Type: application/json" \
  -d '{"licenseKey":"VL-TEST01-TEST02-TEST03"}'
```

**Desktop App**:
```bash
# Build release version
cd VoiceLite
dotnet publish VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained

# Compile installer
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Installer\VoiceLiteSetup_Simple.iss

# Test installer
# 1. Install on clean Windows VM
# 2. Verify Tiny model works
# 3. Validate test license
# 4. Verify Pro model unlocks
```

**GitHub Release**:
```bash
git tag v1.0.67
git push --tags

# GitHub Actions will auto-build and create release
```

---

## 📊 Success Criteria

**Release is UNBLOCKED when**:
- ✅ All exposed credentials scrubbed from git history
- ✅ Production credentials rotated (DB, Stripe, Resend, Upstash, Ed25519)
- ✅ Desktop app updated with new public keys
- ✅ Test coverage ≥80% for LicenseValidator (42/53 tests passing)
- ✅ Test coverage ≥50% for SimpleModelSelector (6/12 tests passing)
- ✅ Settings license persistence tested (5/10 tests passing)
- ✅ Manual testing checklist 100% complete
- ✅ Backend deployed with new credentials
- ✅ Desktop installer builds successfully
- ✅ No CRITICAL or HIGH security vulnerabilities remain

---

## ⏱️ Timeline Summary

| Phase | Tasks | Duration | Dependencies |
|-------|-------|----------|--------------|
| **Phase 1** | Scrub git history + rotate credentials | 4-8 hours | None (START NOW) |
| **Phase 2** | Complete test coverage | 2-3 days | Phase 1 complete |
| **Phase 3** | Fix architecture issues | 1 week | Can run parallel to Phase 2 |
| **Phase 4** | Manual testing | 1 day | Phase 2 complete |
| **Phase 5** | Deploy & monitor | 1 day | Phase 4 complete |
| **TOTAL** | **4-5 days** | | |

---

## 🎯 Next Actions (Priority Order)

**NOW** (Next 30 minutes):
1. ✅ Push commits to origin: `git push origin master`
2. ✅ Backup repository locally
3. ⏸️ Decide: Scrub git history immediately OR continue testing first?

**TODAY** (if scrubbing git history):
1. Install BFG Repo-Cleaner
2. Run history scrub
3. Verify secrets removed
4. Generate new Ed25519 keypairs

**THIS WEEK**:
1. Rotate all production credentials
2. Refactor LicenseValidator with IHttpClientFactory
3. Complete remaining 37 tests
4. Fix ARCH-003 and ARCH-004

**NEXT WEEK**:
1. Manual testing
2. Deploy to production
3. Release v1.0.67

---

## 🚨 Risk Assessment

**If We DON'T Scrub Git History**:
- ⚠️ Exposed Ed25519 keys remain accessible
- ⚠️ Attackers can forge unlimited Pro licenses
- ⚠️ Revenue loss, reputation damage
- ⚠️ Must disclose security incident to users

**If We DON'T Complete Tests**:
- ⚠️ License validation bugs in production
- ⚠️ Users unable to activate Pro licenses
- ⚠️ Support ticket surge, refund requests
- ⚠️ Delayed revenue realization

**If We DON'T Rotate Credentials**:
- ⚠️ Old credentials still work (even after git scrub)
- ⚠️ Attackers can use cached keys from earlier access
- ⚠️ Database compromise still possible

**Recommendation**: **Execute Phase 1 IMMEDIATELY** (security first), then proceed with testing.

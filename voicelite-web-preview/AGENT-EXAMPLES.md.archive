# Agent Examples & Quick Reference

Copy-paste examples for common VoiceLite development tasks using custom agents from [AGENTS.md](AGENTS.md).

## Table of Contents

1. [Quick Start Examples](#quick-start-examples)
2. [Pre-Commit Checks](#pre-commit-checks)
3. [Code Review Examples](#code-review-examples)
4. [Security Audit Examples](#security-audit-examples)
5. [Performance Optimization Examples](#performance-optimization-examples)
6. [Testing & Coverage Examples](#testing--coverage-examples)
7. [File-Specific Validation Examples](#file-specific-validation-examples)
8. [Domain Expert Examples](#domain-expert-examples)
9. [Documentation Generation Examples](#documentation-generation-examples)
10. [Troubleshooting Guide](#troubleshooting-guide)

---

## Quick Start Examples

### Example 1: Daily Development Check

**Copy-Paste Command**:
```
Run pre-commit-workflow to validate my changes before committing
```

**When to Use**: Before every `git commit`

**Expected Output**:
```
🔒 Pre-Commit Quality Gate

✅ Security Checks: PASS
   - No hardcoded secrets found
   - No localhost URLs in production code
   - No debug/trace logging

✅ Code Quality: PASS
   - No critical TODO/FIXME comments
   - No Console.WriteLine in production code

✅ Documentation: PASS
   - XML comments present on public APIs

✅ READY TO COMMIT
⏱️  Completed in 3.8 seconds
```

**If Issues Found**:
```
❌ BLOCKED - Issues Found

HIGH Issues (must fix):
  ⚠️  voicelite-web/app/api/checkout/route.ts:23
      Localhost URL in success_url
      Fix: Use process.env.NEXT_PUBLIC_BASE_URL

✅ Fix issues above, then re-run workflow
```

---

### Example 2: Prepare Release

**Copy-Paste Command**:
```
Use ship-to-production-workflow to prepare v1.0.12 release
```

**When to Use**: Before creating a git tag or publishing a new version

**Expected Output** (abbreviated):
```
🚀 Ship to Production Workflow - v1.0.12

Phase 1/6: Code Quality Gate ✅ (2m 15s)
Phase 2/6: Security Audit ✅ (3m 42s)
Phase 3/6: Test Coverage ✅ (4m 58s)
   Overall: 79.2% ✅
   Services: 83.4% ✅
Phase 4/6: Legal Compliance ✅ (52s)
Phase 5/6: Build & Package ✅ (2m 34s)
   Installer: VoiceLite-Setup-1.0.12.exe (442 MB)
Phase 6/6: Deployment Prep ✅ (48s)

🎉 READY FOR PRODUCTION
⏱️  Total: 14m 49s

Next Steps:
1. git tag v1.0.12
2. git push origin v1.0.12
3. Publish GitHub release
```

---

### Example 3: Weekly Security Audit

**Copy-Paste Command**:
```
Use security-audit-workflow to scan for vulnerabilities
```

**When to Use**: Every Monday morning, or after merging significant changes

**Expected Output**:
```
🔒 Security Audit Report

CRITICAL: 0 ✅
HIGH: 0 ✅
MEDIUM: 1
  ⚠️  webhook/route.ts:89
      Missing rate limiting on webhook endpoint
      Recommendation: Add rate-limit middleware

LOW: 2
  ℹ️  Settings.cs:45 - Timeout hardcoded (make configurable)
  ℹ️  checkout/route.ts:23 - HTTP in dev (enforce HTTPS)

✅ Overall Security Posture: GOOD
⏱️  Completed in 4m 18s
```

---

## Pre-Commit Checks

### Example 4: Check for Hardcoded Secrets

**Copy-Paste Command**:
```
Run pre-commit-workflow
```

**Scenario**: You accidentally left a Stripe API key in code

**Output**:
```
❌ CRITICAL: Hardcoded Secret Detected

voicelite-web/app/api/checkout/route.ts:12
  const stripeKey = 'sk_live_abc123...';
  ^^^^ CRITICAL: Stripe secret key hardcoded

Fix:
  const stripeKey = process.env.STRIPE_SECRET_KEY!;

❌ COMMIT BLOCKED - Remove secrets before committing
```

**Action**: Fix the issue, then re-run the workflow

---

### Example 5: Check for Debug Code

**Copy-Paste Command**:
```
Run pre-commit-workflow
```

**Scenario**: You left `Console.WriteLine` debug statements in production code

**Output**:
```
⚠️  MEDIUM: Debug Code Detected

VoiceLite/VoiceLite/Services/PersistentWhisperService.cs:89
  Console.WriteLine("Transcription result: " + result);
  ^^^^ Use ErrorLogger instead of Console.WriteLine

VoiceLite/VoiceLite/MainWindow.xaml.cs:145
  System.Diagnostics.Debugger.Break();
  ^^^^ Remove debugger breakpoint

✅ Quality gate: PASS (warnings only)
Recommendation: Fix MEDIUM issues before commit
```

---

## Code Review Examples

### Example 6: Review Modified Files

**Copy-Paste Command**:
```
Use code-reviewer to review all files I changed today
```

**Expected Output**:
```
📝 Code Review Report

Files Reviewed: 5
Overall Score: 8.7/10 ✅

VoiceLite/VoiceLite/Services/AudioRecorder.cs (9/10)
  ✅ Strong error handling
  ✅ Proper disposal pattern
  ⚠️  MEDIUM: Consider exposing buffer size as configurable property

VoiceLite/VoiceLite/MainWindow.xaml.cs (8.5/10)
  ✅ Good null safety checks
  ⚠️  HIGH: Line 234 - UI update from background thread (use Dispatcher.Invoke)
  ⚠️  MEDIUM: Consider extracting recording logic to separate service

voicelite-web/app/api/checkout/route.ts (8/10)
  ✅ Good input validation with Zod
  ✅ Proper error handling
  ⚠️  MEDIUM: Add rate limiting (5 requests/min per IP)

voicelite-web/app/api/webhook/route.ts (9.5/10)
  ✅ Excellent: Stripe signature verification
  ✅ Excellent: Idempotent event handling
  ✅ Returns 200 OK (Stripe best practice)

Settings.cs (8/10)
  ✅ Good default values
  ⚠️  LOW: Missing XML comments on new properties

Recommendations:
1. Fix HIGH issue in MainWindow.xaml.cs:234 (thread safety)
2. Add rate limiting to checkout endpoint
3. Document new Settings properties

⏱️  Review completed in 45 seconds
```

---

### Example 7: Review Specific File

**Copy-Paste Command**:
```
Use code-reviewer to review VoiceLite/VoiceLite/Services/PersistentWhisperService.cs
```

**Expected Output**:
```
📝 Code Review: PersistentWhisperService.cs

Score: 9/10 ✅

Strengths:
✅ Excellent semaphore usage for concurrency control
✅ Strong process lifecycle management (timeout, disposal)
✅ Good error handling with specific exceptions
✅ Path caching for performance

Improvements:
⚠️  MEDIUM: Warmup not awaited in constructor
   Line 45: Task.Run(WarmupWhisper)
   Risk: Race condition if first transcription starts before warmup completes
   Fix: Create async InitializeAsync() factory method

⚠️  LOW: Magic number timeout multiplier
   Line 142: timeout = audioLength * 3
   Recommendation: Extract to constant TIMEOUT_MULTIPLIER

Code Quality Metrics:
- Cyclomatic Complexity: 8 (Good)
- Lines of Code: 245 (Reasonable)
- Test Coverage: 85% (Excellent)

⏱️  Review completed in 12 seconds
```

---

## Security Audit Examples

### Example 8: Full Security Scan

**Copy-Paste Command**:
```
Use security-audit-workflow to perform comprehensive security scan
```

**Expected Output**:
```
🔒 Security Audit Report

Scanning:
  ✅ Desktop app (C# services)
  ✅ Web API routes (Next.js)
  ✅ Stripe webhooks
  ✅ License validation

Results by Severity:

CRITICAL: 0 ✅
  No critical vulnerabilities found

HIGH: 1 ⚠️
  🚨 voicelite-web/app/api/auth/route.ts:34
     Missing rate limiting on login endpoint
     Risk: Brute force attacks on magic link requests
     Fix: Add rate-limit middleware (5 requests/5min per email)
     Code:
       import rateLimit from '@/lib/rateLimit';
       const limiter = rateLimit({ max: 5, windowMs: 5 * 60 * 1000 });
       await limiter(req);

MEDIUM: 3
  ⚠️  PersistentWhisperService.cs:89
      Process timeout hardcoded (30s)
      Recommendation: Make configurable via Settings
      Risk: Low (timeout adequate for normal usage)

  ⚠️  checkout/route.ts:23
      Success URL uses HTTP in development
      Fix: Enforce HTTPS in all environments
      Risk: Low (only affects local dev)

  ⚠️  webhook/route.ts:67
      No rate limiting on webhook endpoint
      Recommendation: Add IP-based rate limit (100 req/min)
      Risk: Low (Stripe already rate-limits)

LOW: 2
  ℹ️  Settings.cs:45 - Consider encrypting sensitive settings
  ℹ️  MainWindow.xaml.cs:234 - Log user actions for audit trail

Security Posture: GOOD ✅
Risk Score: 2/10 (Low)

Priority Actions:
1. Add rate limiting to auth endpoint (HIGH priority)
2. Review and address MEDIUM issues this sprint

⏱️  Audit completed in 4m 32s
```

---

### Example 9: Webhook Security Check

**Copy-Paste Command**:
```
Use webhook-security-enforcer to validate Stripe webhook implementation
```

**Expected Output**:
```
🔒 Webhook Security Analysis

File: voicelite-web/app/api/webhook/route.ts

CRITICAL Checks:
✅ Stripe signature verification present (line 23)
   stripe.webhooks.constructEvent(body, sig, webhookSecret)

✅ Webhook secret from environment variable
   process.env.STRIPE_WEBHOOK_SECRET

✅ Returns 200 OK on success (line 145)
   (Stripe best practice to prevent retries)

Event Type Validation:
✅ Handles subscription lifecycle events
   - customer.subscription.created
   - customer.subscription.updated
   - customer.subscription.deleted
   - invoice.payment_failed

Security Features:
✅ No sensitive data in error responses (line 152)
✅ Event idempotency handled via database constraints
✅ Input sanitization via Zod schemas

Recommendations:
⚠️  MEDIUM: Add rate limiting
   While Stripe rate-limits their side, adding IP-based limits provides defense in depth

⚠️  LOW: Log webhook events for audit trail
   Consider logging event IDs to track processing

✅ Overall Security: EXCELLENT
⏱️  Validation completed in 8 seconds
```

---

## Performance Optimization Examples

### Example 10: Find Performance Bottlenecks

**Copy-Paste Command**:
```
Use performance-optimization-workflow to identify and fix performance issues
```

**Expected Output**:
```
⚡ Performance Optimization Report

Target Metrics:
  Startup: < 2s ✅ (currently 1.6s)
  Transcription: < 200ms ❌ (currently 340ms)
  Idle RAM: < 100MB ❌ (currently 152MB)
  Active RAM: < 300MB ✅ (currently 285MB)
  Idle CPU: < 5% ✅ (currently 2%)

🔍 Profiling Analysis:

HIGH Impact Optimizations:
1. ⚡ PersistentWhisperService.cs:134
   Issue: Synchronous process.WaitForExit blocks thread
   Impact: +120ms transcription latency
   Fix: Use await process.WaitForExitAsync()
   Expected improvement: 340ms → 220ms

2. 🧠 MainWindow.xaml.cs:89
   Issue: Event handlers not unsubscribed
   Impact: 12MB memory leak per hotkey change
   Fix: Unsubscribe in Dispose() method
   Expected improvement: 152MB → 140MB idle RAM

MEDIUM Impact Optimizations:
3. 📦 AudioPreprocessor.cs:67
   Issue: Large float[] buffer allocated per transcription
   Impact: 8MB allocated (GC cleans up, but causes pressure)
   Fix: Use ArrayPool<float>.Shared
   Expected improvement: Reduced GC pauses

4. 🔄 Settings.cs:234
   Issue: JSON file read on every Settings.Get() call
   Impact: 15ms per settings access
   Fix: Implement in-memory cache with file watcher
   Expected improvement: 15ms → <1ms

Implementation Plan:
✅ Applied fix #1 (WaitForExitAsync)
   Transcription latency: 340ms → 215ms ✅ Target met!

✅ Applied fix #2 (Event unsubscription)
   Idle RAM: 152MB → 138MB ⚠️ Still above target

⏭️  Recommend applying fix #3 and #4 in next sprint

📊 Summary:
  Before: 2/5 metrics met
  After:  4/5 metrics met
  Improvement: +40% target compliance

⏱️  Optimization completed in 6m 15s
```

---

### Example 11: Memory Leak Investigation

**Copy-Paste Command**:
```
Use performance-optimization-workflow with focus on memory usage
```

**Expected Output**:
```
🧠 Memory Analysis

Baseline Measurement:
  Startup RAM: 82MB
  After 10 transcriptions: 215MB
  After 50 transcriptions: 380MB
  After 100 transcriptions: 615MB

⚠️  CRITICAL: Memory leak detected (5.3MB per transcription)

🔍 Leak Sources:

CRITICAL:
1. 🚨 PersistentWhisperService.cs:156
   Issue: WaveFileWriter not disposed after warmup
   Impact: 45MB one-time leak on startup
   Fix: Wrap in using statement
   ```csharp
   using (var writer = new WaveFileWriter(warmupPath, waveFormat))
   {
       // warmup code
   }
   ```

2. 🚨 AudioRecorder.cs:89
   Issue: WaveInEvent.StopRecording doesn't dispose buffers
   Impact: 4.8MB leak per transcription
   Fix: Call Dispose() after StopRecording()
   ```csharp
   _waveIn.StopRecording();
   _waveIn.Dispose();
   _waveIn = null;
   ```

HIGH:
3. ⚠️  MainWindow.xaml.cs:234
   Issue: Timer not disposed when changing settings
   Impact: 500KB leak per settings change
   Fix: Store timer reference and dispose in Dispose()

Projected Fix Impact:
  Current: 615MB after 100 transcriptions
  After fixes: ~130MB after 100 transcriptions
  Reduction: -79% memory usage

✅ Applied all fixes
✅ Re-tested: 128MB after 100 transcriptions ✅

🎉 Memory leak resolved!
```

---

## Testing & Coverage Examples

### Example 12: Check Test Coverage

**Copy-Paste Command**:
```
Use voicelite-tester to run all tests and check coverage
```

**Expected Output**:
```
🧪 VoiceLite Test Suite

Running Tests...
✅ All tests passed (142/142)

Test Results by Project:
  VoiceLite.Tests: 98 passed
  VoiceLite.Integration.Tests: 32 passed
  VoiceLite.Web.Tests: 12 passed

Coverage Report:
  Overall: 78.4% ✅ (target: >= 75%)

  By Directory:
    Services/: 82.1% ✅ (target: >= 80%)
    Models/: 95.3% ✅
    Controls/: 68.2% ⚠️ (below 75% target)
    API Routes: 71.5% ⚠️

  Uncovered Areas:
    ❌ AudioPreprocessor.cs:89-103 (ApplyCompression method)
    ❌ SimpleModelSelector.xaml.cs:45-67 (OnModelChanged event)
    ❌ api/auth/route.ts:78-92 (error handling branch)

Recommendations:
1. Add tests for AudioPreprocessor.ApplyCompression
2. Add UI tests for SimpleModelSelector
3. Add error case tests for auth route

⏱️  Tests completed in 4m 58s
```

---

### Example 13: Generate Tests for New Code

**Copy-Paste Command**:
```
Use test-coverage-enforcer to suggest tests for VoiceLite/VoiceLite/Services/AudioPreprocessor.cs
```

**Expected Output**:
```
📋 Test Coverage Analysis: AudioPreprocessor.cs

Current Coverage: 68% (target: 80%)

Uncovered Methods:
1. ApplyCompression (lines 89-103) - 0% coverage
2. ApplyNoiseReduction (lines 145-167) - 45% coverage
3. ValidateAudioFormat (lines 201-215) - 0% coverage

Suggested Test Cases:

// AudioPreprocessorTests.cs

[Fact]
public async Task ApplyCompression_ReducesFileSize()
{
    // Arrange
    var preprocessor = new AudioPreprocessor();
    var inputAudio = GenerateTestAudio(1_000_000); // 1MB

    // Act
    var compressed = await preprocessor.ApplyCompression(inputAudio);

    // Assert
    compressed.Length.Should().BeLessThan(500_000); // < 500KB
}

[Fact]
public async Task ApplyCompression_PreservesAudioFormat()
{
    // Arrange
    var preprocessor = new AudioPreprocessor();
    var inputAudio = GenerateTestAudio(sampleRate: 16000, channels: 1);

    // Act
    var compressed = await preprocessor.ApplyCompression(inputAudio);

    // Assert
    var format = GetWaveFormat(compressed);
    format.SampleRate.Should().Be(16000);
    format.Channels.Should().Be(1);
}

[Fact]
public async Task ApplyNoiseReduction_RemovesBackgroundNoise()
{
    // Arrange
    var preprocessor = new AudioPreprocessor();
    var noisyAudio = LoadTestFile("speech_with_noise.wav");

    // Act
    var cleaned = await preprocessor.ApplyNoiseReduction(noisyAudio);

    // Assert
    var snr = CalculateSignalToNoiseRatio(cleaned);
    snr.Should().BeGreaterThan(20); // > 20dB SNR
}

[Theory]
[InlineData(16000, 16, 1, true)]  // Valid: 16kHz, 16-bit, mono
[InlineData(8000, 16, 1, false)]  // Invalid: 8kHz
[InlineData(16000, 8, 1, false)]  // Invalid: 8-bit
[InlineData(16000, 16, 2, false)] // Invalid: stereo
public void ValidateAudioFormat_ReturnsExpectedResult(
    int sampleRate, int bitDepth, int channels, bool expected)
{
    // Arrange
    var preprocessor = new AudioPreprocessor();
    var format = new WaveFormat(sampleRate, bitDepth, channels);

    // Act
    var isValid = preprocessor.ValidateAudioFormat(format);

    // Assert
    isValid.Should().Be(expected);
}

✅ Test stubs generated
📁 File: VoiceLite/VoiceLite.Tests/Services/AudioPreprocessorTests.cs

After implementing these tests:
  Projected coverage: 68% → 92% ✅
```

---

## File-Specific Validation Examples

### Example 14: Validate Whisper Service Changes

**Copy-Paste Command**:
```
Use whisper-service-guardian to validate PersistentWhisperService.cs
```

**When to Use**: After modifying `VoiceLite/VoiceLite/Services/PersistentWhisperService.cs`

**Expected Output**:
```
🛡️  Whisper Service Guardian Report

✅ CRITICAL CHECKS PASSED:

Semaphore Configuration (line 23):
  ✅ private readonly SemaphoreSlim _semaphore = new(1, 1);
  ✅ Correct: maxCount=1 prevents concurrent transcriptions
  ✅ Disposed in Dispose() method

Process Lifecycle:
  ✅ Process.Start() in try-catch (line 89)
  ✅ Timeout enforcement with WaitForExit(timeout) (line 134)
  ✅ Process.Dispose() in finally block (line 156)
  ✅ Process.Kill() on timeout (line 167)

Whisper Command Parameters (line 142):
  ✅ Model path: Validated
  ✅ Audio file path: Validated
  ✅ --no-timestamps: Present
  ✅ --language en: Present
  ✅ --temperature 0.2: Optimal for accuracy
  ✅ --beam-size 5: Optimal for quality
  ✅ --best-of 5: Present

Path Caching:
  ✅ whisper.exe path cached (line 34)
  ✅ Model path cached (line 45)
  ✅ Paths validated on startup

⚠️  WARNINGS:

MEDIUM: Warmup Async Safety (line 45)
  Issue: Task.Run(WarmupWhisper) not awaited
  Risk: Race condition if first transcription starts before warmup
  Impact: First transcription may be slow (no functional bug)
  Fix: Create async InitializeAsync() factory method
  ```csharp
  public static async Task<PersistentWhisperService> CreateAsync(Settings settings)
  {
      var service = new PersistentWhisperService(settings);
      await service.WarmupWhisper();
      return service;
  }
  ```

LOW: Magic Number (line 142)
  Issue: Timeout multiplier hardcoded (audioLength * 3)
  Recommendation: Extract to const TIMEOUT_MULTIPLIER = 3

📋 Summary:
  ✅ All critical checks passed
  ⚠️  1 MEDIUM warning (warmup race condition)
  ⚠️  1 LOW warning (magic number)

Recommendation: Address MEDIUM warning before next release

⏱️  Validation completed in 11 seconds
```

---

### Example 15: Validate Stripe Checkout Route

**Copy-Paste Command**:
```
Use stripe-checkout-guardian to validate checkout implementation
```

**When to Use**: After modifying `voicelite-web/app/api/checkout/route.ts`

**Expected Output**:
```
🛡️  Stripe Checkout Guardian Report

✅ SECURITY CHECKS:

Environment Variables (line 12):
  ✅ STRIPE_SECRET_KEY from process.env (not hardcoded)
  ✅ STRIPE_PUBLISHABLE_KEY from process.env
  ✅ No API keys in code

Input Validation (line 23):
  ✅ Zod schema present for request body
  ✅ Email validation with email regex
  ✅ Plan validation (subscription | lifetime)

CSRF Protection (line 34):
  ✅ validateOrigin() called
  ✅ Checks request origin against whitelist

✅ PRICING VALIDATION:

Subscription Plan (line 45):
  ✅ Price: $7/month (matches terms/page.tsx)
  ✅ Interval: 'month'
  ✅ Currency: 'usd'

Trial Period:
  ✅ No trial period configured ✅
  ✅ Consistent with legal docs (no trial after v1.0.0)

✅ URL VALIDATION:

Success URL (line 67):
  ✅ Uses process.env.NEXT_PUBLIC_BASE_URL
  ✅ No localhost in production
  ✅ HTTPS enforced

Cancel URL (line 68):
  ✅ Uses process.env.NEXT_PUBLIC_BASE_URL
  ✅ Returns to homepage

⚠️  RECOMMENDATIONS:

MEDIUM: Rate Limiting
  Issue: No rate limit on checkout endpoint
  Risk: Spam checkout sessions
  Fix: Add rate-limit middleware
  ```typescript
  import rateLimit from '@/lib/rateLimit';
  const limiter = rateLimit({ max: 10, windowMs: 60 * 1000 }); // 10/min
  await limiter(req);
  ```

LOW: Customer Portal Link
  Issue: Line 89 has /test in customer portal URL
  Fix: Remove '/test' for production
  ```typescript
  - configuration: 'bpc_test_123abc',
  + configuration: 'bpc_live_123abc',
  ```

📋 Summary:
  ✅ All critical checks passed
  ✅ Pricing accurate
  ✅ No trial period (correct)
  ⚠️  2 recommendations (rate limit, portal link)

⏱️  Validation completed in 14 seconds
```

---

### Example 16: Validate Legal Documentation

**Copy-Paste Command**:
```
Use legal-docs-sync-validator to check consistency across legal docs
```

**When to Use**: After modifying any of `privacy/page.tsx`, `terms/page.tsx`, or `EULA.txt`

**Expected Output**:
```
📜 Legal Documentation Sync Report

Checking Consistency Across:
  ✅ voicelite-web/app/privacy/page.tsx
  ✅ voicelite-web/app/terms/page.tsx
  ✅ VoiceLite/EULA.txt

✅ EMAIL CONSISTENCY:
  ✅ All docs use: contact@voicelite.app
  ✅ No references to old emails (privacy@, support@)

✅ PRICING CONSISTENCY:
  ✅ All docs state: $7/month for Pro subscription
  ✅ Terms match Stripe configuration

✅ TRIAL PERIOD CONSISTENCY:
  ✅ No trial period mentioned (correct as of v1.0.0)
  ✅ 7-day money-back guarantee mentioned (correct)

✅ DATA COLLECTION CONSISTENCY:
Cross-referencing Privacy Policy with actual code...

Privacy Policy Claims:
  ✅ "Email address for license delivery"
     → Verified: sendLicenseEmail in lib/email.ts
  ✅ "Device fingerprint (CPU ID, Machine GUID, SHA-256 hash)"
     → Verified: SimpleLicenseManager.cs:67
  ✅ "Payment info processed by Stripe (we don't store cards)"
     → Verified: No card storage in database schema
  ✅ "No audio recordings stored"
     → Verified: AudioRecorder.cs deletes temp files
  ✅ "No transcription text stored"
     → Verified: PersistentWhisperService.cs no persistence

⚠️  MINOR ISSUES:

LOW: Outdated "Last Updated" Date
  ✅ privacy/page.tsx: "Last Updated: January 2025"
  ⚠️  terms/page.tsx: "Last Updated: January 2025"
  ⚠️  EULA.txt: "Last Updated: December 2024" ← Outdated
  Fix: Update EULA.txt to January 2025

LOW: Jurisdiction Placeholder
  ⚠️  terms/page.tsx:220: "[Your State/Country]"
  ⚠️  EULA.txt:189: "[Your Jurisdiction]"
  Action: Fill in after LLC formation

📋 Summary:
  ✅ Email addresses consistent
  ✅ Pricing consistent
  ✅ Trial period consistent
  ✅ Data collection claims accurate
  ⚠️  2 minor issues (dates, jurisdiction)

Recommendation: Update EULA.txt date before next release

⏱️  Validation completed in 18 seconds
```

---

## Domain Expert Examples

### Example 17: Whisper Accuracy Troubleshooting

**Copy-Paste Command**:
```
Use whisper-model-expert to help debug poor accuracy on programming terms like useState and forEach
```

**Expected Output**:
```
🎙️  Whisper Model Expert Consultation

Issue: Poor transcription accuracy on technical terms

Analysis:

1. 🔍 Current Configuration:
   Model: Tiny (ggml-tiny.bin)
   Temperature: 0.2
   Beam size: 5
   Language: en
   Audio format: 16kHz, 16-bit, mono ✅

2. 🎯 Root Cause Analysis:

   PRIMARY ISSUE: Model Size
   ⚠️  Tiny model vocabulary is limited for programming jargon
   - General accuracy: 70-80%
   - Programming terms: ~50%
   - Common errors:
     • "useState" → "you state"
     • "forEach" → "for each"
     • "async/await" → "a sync await"
     • "npm install" → "N P M install"

   CONTRIBUTING FACTOR: Temperature
   ℹ️  Temperature 0.2 allows some randomness
   - For technical dictation, 0.0 is more deterministic
   - Less likely to "correct" technical terms to common words

3. 💡 Recommended Solutions (prioritized):

   HIGH Impact:
   ✅ Solution 1: Upgrade to Small or Medium model
      - Small model: 85-92% on programming terms
      - Medium model: 92-97% on programming terms
      - Trade-off: +2-4s latency, +300MB RAM

      Implementation:
      ```csharp
      // Settings.cs
      public string DefaultModel { get; set; } = "ggml-small.bin"; // was tiny
      ```

   ✅ Solution 2: Lower temperature to 0.0
      - More deterministic output
      - Less "autocorrection" of technical terms

      Implementation:
      ```csharp
      // PersistentWhisperService.cs:142
      - --temperature 0.2
      + --temperature 0.0
      ```

   MEDIUM Impact:
   ⚠️  Solution 3: Use prompt parameter (Whisper v3+)
      - Prime model with expected vocabulary
      - Example: --prompt "Programming terms: useState, useEffect, forEach, async, await"
      - Limitation: Max 244 tokens in prompt

      Implementation:
      ```csharp
      var techTerms = "React hooks useState useEffect forEach map reduce async await";
      var command = $"--prompt \"{techTerms}\"";
      ```

   LOW Impact:
   ℹ️  Solution 4: Post-processing corrections
      - Replace common mistakes in TranscriptionPostProcessor
      - Example: "you state" → "useState" if in code context
      - Limitation: Requires context detection

4. 📊 Expected Improvements:

   Current (Tiny, temp 0.2):
     "Create a new you state hook and for each loop"
     Accuracy: ~50% on technical terms

   After Solution 1+2 (Small, temp 0.0):
     "Create a new useState hook and forEach loop"
     Accuracy: ~90% on technical terms

   After Solution 1+2+3 (with prompt):
     "Create a new useState hook and forEach loop"
     Accuracy: ~95% on technical terms

5. 🧪 Testing Plan:

   Test Phrases:
   1. "useState and useEffect hooks"
   2. "forEach loop with async await"
   3. "npm install react router"
   4. "git commit and git push"
   5. "const axios equals require axios"

   Benchmark: Record accuracy before/after each solution

📋 Recommended Action:
1. Apply Solution 1 (upgrade to Small model)
2. Apply Solution 2 (temperature 0.0)
3. Test with phrases above
4. If <90% accuracy, apply Solution 3 (prompt)

Would you like me to implement Solutions 1 and 2?
```

---

### Example 18: WPF UI Best Practices

**Copy-Paste Command**:
```
Use wpf-ui-expert to review MainWindow.xaml.cs for thread safety issues
```

**Expected Output**:
```
🎨 WPF UI Expert Analysis

File: VoiceLite/VoiceLite/MainWindow.xaml.cs

🔍 Thread Safety Analysis:

❌ CRITICAL: UI Updates from Background Thread

Line 145:
```csharp
private async void OnTranscriptionComplete(string text)
{
    lblStatus.Content = "Processing..."; // ❌ UI update without Dispatcher
    await InjectText(text);
    lblStatus.Content = "Ready"; // ❌ UI update without Dispatcher
}
```

Issue: lblStatus is a UI element, but OnTranscriptionComplete
       is called from background thread (whisper service callback)

Fix:
```csharp
private async void OnTranscriptionComplete(string text)
{
    await Dispatcher.InvokeAsync(() => {
        lblStatus.Content = "Processing...";
    });

    await InjectText(text);

    await Dispatcher.InvokeAsync(() => {
        lblStatus.Content = "Ready";
    });
}
```

Alternative (better performance):
```csharp
private async void OnTranscriptionComplete(string text)
{
    // Only marshal back to UI thread for UI updates
    await InjectText(text); // Can run on background thread

    await Dispatcher.InvokeAsync(() => {
        lblStatus.Content = "Ready";
    });
}
```

⚠️  HIGH: Race Condition in Recording State

Line 234:
```csharp
private void ToggleRecording()
{
    if (_isRecording) // ❌ Not thread-safe
    {
        StopRecording();
    }
    else
    {
        StartRecording();
    }
}
```

Issue: _isRecording can be modified by multiple threads
       (hotkey handler + UI button click)

Fix:
```csharp
private readonly object _recordingLock = new object();

private void ToggleRecording()
{
    lock (_recordingLock)
    {
        if (_isRecording)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
    }
}
```

✅ GOOD PRACTICES FOUND:

1. Proper IDisposable implementation (line 456)
   ✅ Unsubscribes from events
   ✅ Disposes services
   ✅ Removes hotkeys

2. Null safety checks (line 89, 123, 167)
   ✅ All service references checked before use

3. Async/await usage (line 145, 201)
   ✅ Properly using async void only for event handlers
   ✅ Other methods use async Task

⚠️  RECOMMENDATIONS:

MEDIUM: Extract Business Logic
  Issue: MainWindow.xaml.cs has 567 lines (too large)
  Recommendation: Extract recording logic to RecordingService
  Benefits:
    - Easier to test
    - Better separation of concerns
    - Reduced MainWindow complexity

LOW: Use MVVM Pattern
  Issue: Code-behind has UI logic mixed with business logic
  Recommendation: Introduce ViewModel
  Benefits:
    - Better testability
    - Cleaner separation
    - Easier to maintain

📋 Action Items:
1. CRITICAL: Fix thread safety in OnTranscriptionComplete (line 145)
2. HIGH: Fix race condition in ToggleRecording (line 234)
3. MEDIUM: Consider refactoring to RecordingService

⏱️  Analysis completed in 15 seconds
```

---

### Example 19: Stripe Integration Best Practices

**Copy-Paste Command**:
```
Use stripe-integration-expert to explain how to test webhook events locally
```

**Expected Output**:
```
💳 Stripe Integration Expert Guide

Topic: Testing Webhook Events Locally

🔧 Setup Instructions:

1. Install Stripe CLI
```bash
# Windows (with Scoop)
scoop install stripe

# Or download from https://stripe.com/docs/stripe-cli
```

2. Login to Stripe
```bash
stripe login
# Follow browser authentication flow
```

3. Forward Webhooks to Local Server
```bash
# Start your Next.js dev server first
npm run dev

# In another terminal, forward webhooks
stripe listen --forward-to localhost:3000/api/webhook
```

Output:
```
> Ready! Your webhook signing secret is whsec_abc123...
> Listening for events on your Stripe account
```

4. Update .env.local
```bash
# Copy the webhook secret from above
STRIPE_WEBHOOK_SECRET=whsec_abc123...
```

🧪 Testing Webhook Events:

Test Subscription Created:
```bash
stripe trigger customer.subscription.created
```

Expected Output in Your App:
```
POST /api/webhook 200 OK
Event: customer.subscription.created
License created for customer cus_abc123
```

Test Payment Failed:
```bash
stripe trigger invoice.payment_failed
```

Expected Output:
```
POST /api/webhook 200 OK
Event: invoice.payment_failed
License status updated to 'grace_period'
Email sent to customer
```

Test Subscription Canceled:
```bash
stripe trigger customer.subscription.deleted
```

Expected Output:
```
POST /api/webhook 200 OK
Event: customer.subscription.deleted
License status updated to 'expired'
```

📋 Complete Test Scenarios:

Scenario 1: Successful Subscription Flow
```bash
# 1. Customer subscribes
stripe trigger customer.subscription.created

# Verify:
# - License created in database
# - License key generated
# - Welcome email sent
# - License status: 'active'

# 2. Customer uses app (desktop app validates license)
# 3. Subscription renews automatically (handled by Stripe)
```

Scenario 2: Failed Payment Recovery
```bash
# 1. Initial subscription successful
stripe trigger customer.subscription.created

# 2. First payment fails
stripe trigger invoice.payment_failed

# Verify:
# - License status: 'grace_period'
# - Grace period ends: 7 days from now
# - Payment failed email sent
# - Desktop app shows warning but allows Pro features

# 3. Customer updates payment method (manual in Stripe Dashboard)

# 4. Retry succeeds
stripe trigger invoice.payment_succeeded

# Verify:
# - License status: 'active'
# - Grace period cleared
```

Scenario 3: Complete Cancellation
```bash
# 1. Customer cancels subscription
stripe trigger customer.subscription.deleted

# Verify:
# - License status: 'expired'
# - Cancellation email sent
# - Desktop app reverts to Free tier

# 2. Customer tries to use Pro features
# Desktop app shows: "Your subscription has ended. Upgrade to Pro to continue."
```

🔍 Debugging Webhook Issues:

Check Webhook Logs:
```bash
# In Stripe Dashboard
Developers > Webhooks > [Your endpoint] > Events

# Or via CLI
stripe events list --limit 10
```

Test Signature Verification:
```bash
# Send test event with invalid signature
curl -X POST http://localhost:3000/api/webhook \
  -H "Content-Type: application/json" \
  -H "Stripe-Signature: invalid" \
  -d '{"type":"customer.subscription.created"}'

# Expected: 400 Bad Request (signature verification failed)
```

Replay Event:
```bash
# If an event failed, replay it
stripe events resend evt_abc123
```

⚠️  Common Issues:

1. 400 Bad Request: "No signatures found"
   Fix: Ensure stripe-signature header is present

2. 400 Bad Request: "Signature verification failed"
   Fix: Check STRIPE_WEBHOOK_SECRET matches CLI output

3. 500 Internal Server Error
   Fix: Check your webhook handler code for bugs
   Debug: Check server logs for error details

4. Events not being received
   Fix: Ensure `stripe listen` is running and forwarding to correct port

📚 Additional Resources:

- Stripe CLI docs: https://stripe.com/docs/stripe-cli
- Webhook testing: https://stripe.com/docs/webhooks/test
- Event types: https://stripe.com/docs/api/events/types

Would you like help implementing a specific webhook event handler?
```

---

## Documentation Generation Examples

### Example 20: Update Project Documentation

**Copy-Paste Command**:
```
Use claude-md-sync-agent to update CLAUDE.md after adding new services
```

**Expected Output**:
```
📝 CLAUDE.md Synchronization Report

🔍 Scanning Codebase for Changes...

Changes Detected:
1. ✅ New Service: VoiceLockService.cs
   - Location: VoiceLite/VoiceLite/Services/VoiceLockService.cs
   - Purpose: PIN-based app locking and privacy controls
   - Public methods: LockApp(), UnlockApp(), IsLocked()

2. ✅ Removed Service: WhisperProcessPool.cs (deprecated)
   - Replaced by: PersistentWhisperService.cs

3. ✅ Updated Dependency: NAudio 2.2.1 → 2.3.0
   - Breaking changes: None
   - New features: Improved noise suppression

📝 Updates Applied to CLAUDE.md:

Line 50: Service List Updated
```diff
  Services:
    - AudioRecorder
    - PersistentWhisperService
+   - VoiceLockService
-   - WhisperProcessPool (deprecated)
```

Line 127: Architecture Overview Updated
```diff
+ 3. **VoiceLockService** (`Services/VoiceLockService.cs`):
+    PIN-based app locking and privacy controls
+    - Lock/unlock application access
+    - Auto-lock on idle timeout
+    - Secure PIN storage with encryption
```

Line 245: Service Count Updated
```diff
- The app includes 17 services for modular functionality.
+ The app includes 18 services for modular functionality.
```

Line 312: Dependencies Updated
```diff
  Dependencies (NuGet Packages):
-   - `NAudio` (2.2.1): Audio recording and processing
+   - `NAudio` (2.3.0): Audio recording and processing
```

✅ Verification Checks:

1. Build Commands Tested:
   ✅ dotnet build VoiceLite/VoiceLite.sln (success)
   ✅ dotnet run --project VoiceLite/VoiceLite/VoiceLite.csproj (success)

2. File Paths Validated:
   ✅ All referenced files exist
   ✅ No broken links

3. Version Numbers Consistent:
   ✅ v1.0.12 across all mentions
   ✅ .NET 8.0 version correct

📊 Summary:
  - Services: 17 → 18
  - Dependencies: 1 updated
  - Documentation sections: 3 updated
  - All verification checks passed

Git Diff Preview:
```diff
diff --git a/CLAUDE.md b/CLAUDE.md
@@ -50,6 +50,7 @@ Services:
   - AudioRecorder
   - PersistentWhisperService
+  - VoiceLockService
-  - WhisperProcessPool
```

✅ CLAUDE.md is now synchronized with codebase

⏱️  Sync completed in 52 seconds
```

---

### Example 21: Generate API Documentation

**Copy-Paste Command**:
```
Use api-docs-generator to create comprehensive API documentation
```

**Expected Output**:
```
📚 API Documentation Generator

🔍 Discovering API Routes...

Found 6 API Routes:
  ✅ POST /api/auth/magic-link
  ✅ POST /api/auth/verify
  ✅ GET  /api/auth/session
  ✅ POST /api/checkout
  ✅ POST /api/webhook
  ✅ GET  /api/licenses/me

📄 Files Generated:

1. API-REFERENCE.md (412 lines)
   - Endpoint descriptions
   - Request/response schemas
   - Authentication requirements
   - Error codes
   - curl examples
   - TypeScript examples

2. openapi.json (OpenAPI 3.0 spec)
   - Machine-readable API spec
   - Import into Postman, Swagger UI, or Insomnia
   - Auto-generated from Zod schemas

3. postman-collection.json
   - Pre-configured API calls
   - Environment variables for dev/prod
   - Test scripts included

📋 Example from API-REFERENCE.md:

---
## Authentication

All protected endpoints require a valid session token.

Include in request:
```http
Cookie: session=eyJhbGc...
```

---
## POST /api/auth/magic-link

Send a magic link to user's email for passwordless authentication.

**Authentication**: None (public)

**Request Body**:
```json
{
  "email": "user@example.com"
}
```

**Response (200 OK)**:
```json
{
  "message": "Magic link sent to your email"
}
```

**Errors**:
- 400 Bad Request: Invalid email format
- 429 Too Many Requests: Rate limit exceeded (5 requests per 5 minutes)
- 500 Internal Server Error: Email service unavailable

**Example curl**:
```bash
curl -X POST https://voicelite.app/api/auth/magic-link \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com"}'
```

**Example TypeScript**:
```typescript
const response = await fetch('/api/auth/magic-link', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email: 'user@example.com' }),
});

if (!response.ok) {
  throw new Error('Failed to send magic link');
}

const data = await response.json();
console.log(data.message); // "Magic link sent to your email"
```

---
## POST /api/checkout

Create a Stripe Checkout session for Pro subscription.

**Authentication**: None (public)

**Request Body**:
```json
{
  "email": "user@example.com",
  "plan": "subscription"
}
```

**Validation Rules**:
- `email`: Valid email address (RFC 5322)
- `plan`: Must be "subscription" or "lifetime"

**Response (200 OK)**:
```json
{
  "url": "https://checkout.stripe.com/c/pay/cs_test_..."
}
```

**Errors**:
- 400 Bad Request: Invalid email or plan
- 429 Too Many Requests: Rate limit exceeded (10 requests per minute)
- 500 Internal Server Error: Stripe API error

**Example curl**:
```bash
curl -X POST https://voicelite.app/api/checkout \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","plan":"subscription"}'
```

---

📦 Import Instructions:

Postman:
1. Open Postman
2. File > Import
3. Select postman-collection.json
4. Create environment:
   - dev: http://localhost:3000
   - prod: https://voicelite.app

Swagger UI:
```bash
npx swagger-ui-serve openapi.json
# Opens at http://localhost:8080
```

Insomnia:
1. Open Insomnia
2. Create > Import from File
3. Select postman-collection.json

📋 Next Steps:
1. Review API-REFERENCE.md
2. Publish to docs.voicelite.app
3. Share Postman collection with team
4. Add OpenAPI spec to CI/CD validation

✅ API documentation generated successfully
⏱️  Generation completed in 1m 48s
```

---

## Troubleshooting Guide

### Issue: Agent Takes Too Long

**Symptom**: Agent runs for >5 minutes without completing

**Diagnosis**:
```
"Check the performance budget for {agent-name}"
```

**Solutions**:
1. For file-specific validators: Should complete in <30 seconds
   - If slow: File may be too large, agent may be doing unnecessary work
   - Action: Check agent definition in AGENTS.md for performance budget

2. For workflow orchestrators: Budget is <5 minutes
   - If slow: Check network connectivity (for Stripe API calls, npm registry)
   - Action: Run agent with verbose logging

3. For domain experts: No strict time limit (consultation-based)
   - If slow: Agent may be waiting for user input
   - Action: Provide additional context in your request

---

### Issue: Agent Fails with Error

**Symptom**: Agent returns error message instead of results

**Example Error**:
```
❌ ERROR: whisper-service-guardian failed
File not found: PersistentWhisperService.cs
```

**Diagnosis**:
1. Check file path is correct
2. Ensure file exists in expected location
3. Verify file has been read by Claude Code in current session

**Solution**:
```
"Read VoiceLite/VoiceLite/Services/PersistentWhisperService.cs, then run whisper-service-guardian"
```

---

### Issue: Agent Output is Too Verbose

**Symptom**: Agent returns pages of output, hard to find actionable items

**Solution**: Ask for summary
```
"Use {agent-name} and summarize only HIGH and CRITICAL issues"
```

---

### Issue: Agent Doesn't Understand Context

**Symptom**: Agent provides generic advice not specific to VoiceLite

**Example**:
```
You: "Help improve transcription accuracy"
Agent: "Try using a better microphone" (unhelpful)
```

**Solution**: Provide more context
```
"Use whisper-model-expert to help improve transcription accuracy on technical programming terms like useState and forEach. Currently using Tiny model with temperature 0.2."
```

---

### Issue: Agent Suggests Outdated Code

**Symptom**: Agent references deprecated services like WhisperProcessPool

**Solution**: Remind agent of current architecture
```
"Note: WhisperProcessPool is deprecated. We now use PersistentWhisperService. Please use whisper-service-guardian to validate my changes."
```

---

### Issue: Multiple Agents Conflict

**Symptom**: Agent A suggests one approach, Agent B suggests conflicting approach

**Example**:
- `code-reviewer` suggests extracting method
- `performance-optimization-workflow` suggests inlining method

**Solution**: Prioritize based on current goal
```
"I'm focused on performance right now. Prioritize performance-optimization-workflow recommendations over code-reviewer."
```

---

## Quick Command Reference

### Daily Development
```bash
"Run pre-commit-workflow"
"Use {file-validator} to check my changes"
"Use code-reviewer to review all modified files"
```

### Release Workflow
```bash
"Use ship-to-production-workflow to prepare v{version}"
"Use voicelite-installer to build release package"
"Use release-manager to generate changelog"
```

### Security & Performance
```bash
"Use security-audit-workflow"
"Use performance-optimization-workflow"
"Use dependency-upgrade-advisor to check for CVEs"
```

### Domain Experts
```bash
"Use whisper-model-expert to debug {issue}"
"Use wpf-ui-expert to review {component}"
"Use stripe-integration-expert to explain {topic}"
```

### Documentation
```bash
"Use claude-md-sync-agent to update CLAUDE.md"
"Use api-docs-generator"
"Use readme-generator"
```

### Testing
```bash
"Use voicelite-tester to run tests with coverage"
"Use test-coverage-enforcer to suggest tests for {file}"
```

---

**Last Updated**: January 2025 (v1.0.10)

For full agent definitions and instructions, see [AGENTS.md](AGENTS.md).
For workflow patterns and examples, see [WORKFLOWS.md](WORKFLOWS.md).

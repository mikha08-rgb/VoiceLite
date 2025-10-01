# VoiceLite Agent Workflows

This guide explains how to use the 20 custom agents defined in [AGENTS.md](AGENTS.md) for daily development, releases, and maintenance of VoiceLite.

## Table of Contents

1. [How Workflows Work](#how-workflows-work)
2. [Daily Development Flow](#daily-development-flow)
3. [Pre-Commit Quality Gates](#pre-commit-quality-gates)
4. [Release Pipeline](#release-pipeline)
5. [Security & Performance Audits](#security--performance-audits)
6. [File-Specific Auto-Validation](#file-specific-auto-validation)
7. [Domain Expert Consultations](#domain-expert-consultations)
8. [Documentation Maintenance](#documentation-maintenance)
9. [Chaining Agents Together](#chaining-agents-together)
10. [Error Handling Patterns](#error-handling-patterns)

---

## How Workflows Work

### Workflow Types

**1. Workflow Orchestrators** (4 agents)
- Coordinate multiple agents in sequence or parallel
- Handle complex multi-step processes
- Example: `ship-to-production-workflow` runs 6 phases with 10+ sub-agents

**2. File-Specific Validators** (8 agents)
- Auto-trigger when specific files are modified
- Fast validation (<30 seconds)
- Example: `whisper-service-guardian` validates PersistentWhisperService.cs changes

**3. Domain Experts** (5 agents)
- Deep expertise in specific areas (Whisper AI, WPF, Stripe, testing, dependencies)
- Provide recommendations and troubleshooting
- Example: `whisper-model-expert` helps debug transcription accuracy issues

**4. Documentation Agents** (3 agents)
- Keep documentation synchronized with code
- Auto-generate API docs and READMEs
- Example: `claude-md-sync-agent` updates CLAUDE.md when services change

### Invocation Methods

**Manual Invocation** (via Claude Code chat):
```
"Use ship-to-production-workflow to prepare v1.0.10"
"Run whisper-model-expert to help debug poor accuracy on technical terms"
```

**Auto-Trigger** (file-specific validators):
When you modify `PersistentWhisperService.cs`, Claude Code will automatically offer:
```
"I notice you modified PersistentWhisperService.cs. Should I run whisper-service-guardian to validate your changes?"
```

---

## Daily Development Flow

### Scenario 1: Adding a New Feature

**You**: "I want to add a keyboard shortcut to pause/resume recording"

**Recommended Workflow**:
1. **Start coding** - Modify `MainWindow.xaml.cs` and `HotkeyManager.cs`
2. **Auto-validation** - `mainwindow-coordinator-guard` auto-triggers when you save
   - Validates null safety for new hotkey registration
   - Checks thread safety in event handlers
   - Verifies resource disposal
3. **Before commit** - Run `pre-commit-workflow`
   - Checks for hardcoded keys, debug code, localhost URLs
   - Runs in <10 seconds
4. **Domain consultation** (if needed) - "Use wpf-ui-expert to review my XAML bindings"

**Example Output from mainwindow-coordinator-guard**:
```
✅ PASS: Null Safety
   - All service references checked before use

⚠️  MEDIUM: Thread Safety
   - Line 145: Hotkey callback accesses UI without Dispatcher.Invoke
   - Recommendation: Wrap lblStatus.Content = "Paused" in Dispatcher.Invoke

✅ PASS: Resource Disposal
   - New hotkey properly unregistered in Dispose()

✅ PASS: Error Handling
   - HotkeyManager.Register wrapped in try-catch
```

### Scenario 2: Fixing a Bug

**You**: "Transcription is cutting off the last word sometimes"

**Recommended Workflow**:
1. **Reproduce** - Test with various audio lengths
2. **Domain expert** - "Use whisper-model-expert to help debug transcription cutoff issues"
3. **Fix code** - Modify `AudioRecorder.cs` (increase buffer size)
4. **Auto-validation** - `audio-recorder-validator` checks your changes
5. **Test coverage** - "Use test-coverage-enforcer to suggest tests for my AudioRecorder changes"

**Example Output from whisper-model-expert**:
```
🔍 Analyzing Transcription Cutoff Issue

Likely Causes:
1. ⚠️  MEDIUM: Audio buffer too small
   - Current: 2048 bytes in AudioRecorder.cs:67
   - Recommendation: Increase to 4096 bytes
   - Reasoning: Whisper requires complete audio frames

2. ⚠️  LOW: Silence detection threshold
   - Current: -40dB noise gate in AudioPreprocessor.cs:34
   - May be cutting off soft trailing words
   - Test with -50dB threshold

3. ✅ NOT ISSUE: Whisper timeout
   - Current timeout: 30s (adequate for expected 5-10s clips)

Action Items:
1. Change AudioRecorder.cs:67
   FROM: new WaveInEvent { BufferMilliseconds = 50 }
   TO:   new WaveInEvent { BufferMilliseconds = 100 }

2. Run test case with phrase "testing one two three"
   - Verify "three" appears in transcription
```

### Scenario 3: Performance Tuning

**You**: "App feels sluggish on startup"

**Recommended Workflow**:
1. **Measure** - "Use performance-optimization-workflow to find startup bottlenecks"
2. **Implement fixes** - Based on agent recommendations
3. **Re-measure** - Agent automatically validates improvements
4. **Document** - "Use claude-md-sync-agent to update performance targets in CLAUDE.md"

---

## Pre-Commit Quality Gates

### Usage

**Before every commit**:
```
"Use pre-commit-workflow to validate my changes"
```

### What It Checks (in <10 seconds)

1. **Security**:
   - ❌ CRITICAL: Hardcoded API keys, passwords, connection strings
   - ❌ HIGH: Localhost URLs in production code
   - ❌ MEDIUM: Debug/trace logging in production paths

2. **Code Quality**:
   - ⚠️  MEDIUM: TODO/FIXME comments in critical services
   - ⚠️  LOW: Console.WriteLine in production code (use ErrorLogger)

3. **Documentation**:
   - ⚠️  LOW: Missing XML comments on public APIs

### Example Output

```
🔒 Pre-Commit Quality Gate

❌ BLOCKED - Critical Issues Found

CRITICAL Issues (must fix):
  None

HIGH Issues (should fix before commit):
  ⚠️  api/checkout/route.ts:12
      Localhost URL in success_url
      Fix: Use process.env.NEXT_PUBLIC_BASE_URL

MEDIUM Issues (can defer):
  ⚠️  PersistentWhisperService.cs:45
      TODO comment: "optimize warmup time"
      Suggestion: Create GitHub issue or remove

LOW Issues (informational):
  ℹ️  MainWindow.xaml.cs:89
      Console.WriteLine found (use ErrorLogger instead)

✅ Recommendation: Fix HIGH issues before committing
⏱️  Completed in 3.2 seconds
```

---

## Release Pipeline

### Full Production Release

**You**: "Prepare VoiceLite v1.0.10 for release"

**Command**:
```
"Use ship-to-production-workflow to prepare v1.0.10"
```

### What Happens (6 Phases)

**Phase 1: Code Quality Gate** (2 min)
- Spawns `code-reviewer` for all modified files
- Requires score >= 8/10
- Blocks if critical issues found

**Phase 2: Security Audit** (3 min)
- Runs `security-audit-workflow` (parallel execution):
  - `voicelite-security-auditor` scans C# code
  - `api-route-security-scanner` checks all API routes
  - `webhook-security-enforcer` validates Stripe webhooks
- Aggregates findings by severity
- Blocks on CRITICAL security issues

**Phase 3: Test Coverage** (5 min)
- Runs `voicelite-tester`
- Requires >= 75% overall, >= 80% on Services
- Generates coverage report

**Phase 4: Legal Compliance** (1 min)
- Runs `voicelite-legal-checker`
- Validates pricing consistency across docs
- Checks email addresses match
- Verifies no trial period references

**Phase 5: Build & Package** (3 min)
- Runs `voicelite-installer`
- Compiles Release build
- Generates installer with Inno Setup
- Validates installer size and dependencies

**Phase 6: Deployment Prep** (1 min)
- Runs `release-manager`
- Generates changelog from git commits
- Creates GitHub release draft
- Suggests download link updates

### Example Output

```
🚀 Ship to Production Workflow - v1.0.10

Phase 1/6: Code Quality Gate ✅
  └─ code-reviewer: 9.2/10 (18 files reviewed)
     ✅ Strong error handling in AudioRecorder.cs
     ⚠️  MEDIUM: Consider async/await in MainWindow.xaml.cs:234

Phase 2/6: Security Audit ✅
  ├─ voicelite-security-auditor: No critical issues
  ├─ api-route-security-scanner: 1 MEDIUM finding
  │  └─ webhook/route.ts:67 - Add rate limiting for webhook endpoint
  └─ webhook-security-enforcer: PASS

Phase 3/6: Test Coverage ✅
  └─ voicelite-tester
     Overall: 78.4% ✅ (target: 75%)
     Services: 82.1% ✅ (target: 80%)
     Missing: AudioPreprocessor.ApplyCompression (line 89-103)

Phase 4/6: Legal Compliance ✅
  └─ voicelite-legal-checker
     ✅ Pricing consistent ($7/month)
     ✅ No trial period references
     ✅ Emails match (contact@voicelite.app)

Phase 5/6: Build & Package ✅
  └─ voicelite-installer
     ✅ Release build successful
     ✅ Installer: VoiceLite-Setup-1.0.10.exe (442 MB)
     ✅ Dependencies: VCRUNTIME140_1.dll bundled

Phase 6/6: Deployment Prep ✅
  └─ release-manager
     ✅ Changelog generated (12 commits since v1.0.9)
     ✅ GitHub release draft created
     📋 TODO: Update download links in README.md, app/page.tsx

🎉 READY FOR PRODUCTION
⏱️  Total time: 15 minutes 23 seconds

Next Steps:
1. Review changelog at .github/release-draft-v1.0.10.md
2. git tag v1.0.10
3. git push origin v1.0.10
4. Publish GitHub release
5. Update website download links
```

---

## Security & Performance Audits

### Weekly Security Scan

**You**: "Run weekly security audit"

**Command**:
```
"Use security-audit-workflow to scan for vulnerabilities"
```

**What Happens** (parallel execution):
1. `voicelite-security-auditor` - Desktop app security
2. `api-route-security-scanner` - Web API security
3. `webhook-security-enforcer` - Stripe webhook validation

**Example Output**:
```
🔒 Security Audit Report

CRITICAL Findings: 0
HIGH Findings: 1
  ⚠️  api/auth/route.ts:34
      Missing rate limiting on login endpoint
      Risk: Brute force attacks
      Fix: Add rate-limit middleware (5 requests/minute)

MEDIUM Findings: 2
  ⚠️  PersistentWhisperService.cs:89
      Process timeout hardcoded (30s)
      Recommendation: Make configurable via Settings

  ⚠️  checkout/route.ts:23
      Success URL uses HTTP in development
      Fix: Enforce HTTPS in all environments

LOW Findings: 0

✅ Overall Security Posture: GOOD
📊 Total issues: 3 (down from 5 last week)
⏱️  Completed in 4 minutes 12 seconds
```

### Performance Bottleneck Analysis

**You**: "App is using too much RAM"

**Command**:
```
"Use performance-optimization-workflow to find memory leaks"
```

**What Happens**:
1. **Measure** - Captures baseline metrics
2. **Profile** - Runs memory profiler during typical workflow
3. **Analyze** - Identifies top consumers
4. **Optimize** - Suggests fixes
5. **Re-measure** - Validates improvements

**Example Output**:
```
⚡ Performance Optimization Report

Target Metrics:
  Startup: < 2s
  Transcription latency: < 200ms
  Idle RAM: < 100MB ❌ (currently 145MB)
  Active RAM: < 300MB ❌ (currently 380MB)
  Idle CPU: < 5%

🔍 Memory Profile Analysis:

HIGH Impact (fix first):
  1. ⚠️  PersistentWhisperService.cs:156
     Issue: WaveFileWriter not disposed after warmup
     Impact: 45MB leak per app session
     Fix: Add using statement around WaveFileWriter

  2. ⚠️  MainWindow.xaml.cs:234
     Issue: Event handlers not unsubscribed
     Impact: 12MB leak per hotkey change
     Fix: Unsubscribe in Dispose()

MEDIUM Impact:
  3. ℹ️  AudioPreprocessor.cs:67
     Issue: Large float[] buffer allocated per transcription
     Impact: 8MB per transcription (GC cleans up)
     Recommendation: Pool buffers with ArrayPool<float>

📊 Projected Improvements:
  If all HIGH issues fixed:
    Idle RAM: 145MB → 88MB ✅
    Active RAM: 380MB → 323MB ⚠️ (still above target)

Next Steps:
1. Fix PersistentWhisperService.cs:156 (add using statement)
2. Fix MainWindow.xaml.cs:234 (unsubscribe events)
3. Re-run workflow to validate
4. Consider ArrayPool for AudioPreprocessor if still needed
```

---

## File-Specific Auto-Validation

### How It Works

When you modify certain critical files, Claude Code will automatically offer to run the corresponding validator agent.

### Guarded Files

| File | Guardian Agent | Validation Time |
|------|---------------|-----------------|
| `PersistentWhisperService.cs` | `whisper-service-guardian` | <15s |
| `MainWindow.xaml.cs` | `mainwindow-coordinator-guard` | <20s |
| `AudioRecorder.cs` | `audio-recorder-validator` | <10s |
| `Settings.cs` | `settings-persistence-guard` | <10s |
| `api/checkout/route.ts` | `stripe-checkout-guardian` | <15s |
| `api/webhook/route.ts` | `webhook-security-enforcer` | <15s |
| `privacy/page.tsx`, `terms/page.tsx` | `legal-docs-sync-validator` | <20s |
| Any `api/**/route.ts` | `api-route-security-scanner` | <25s |

### Example Interaction

**You modify**: `VoiceLite/VoiceLite/Services/PersistentWhisperService.cs`

**Claude Code suggests**:
```
I notice you modified PersistentWhisperService.cs.
Should I run whisper-service-guardian to validate your changes?
```

**You**: "Yes"

**Output**:
```
🛡️  whisper-service-guardian Report

✅ PASS: Semaphore Configuration
   - Line 23: private readonly SemaphoreSlim _semaphore = new(1, 1);
   - Correct: maxCount=1 ensures single concurrent transcription

✅ PASS: Process Lifecycle
   - Process.Start() wrapped in try-catch (line 89)
   - process.WaitForExit(timeout) used (line 134)
   - Process.Dispose() in finally block (line 156)

⚠️  MEDIUM: Warmup Async Safety
   - Line 45: Task.Run(WarmupWhisper) not awaited
   - Potential race: First transcription may start before warmup completes
   - Fix: await Task.Run(WarmupWhisper) in constructor, or make constructor async factory

✅ PASS: Command Parameters
   - Temperature: 0.2 ✅
   - Beam size: 5 ✅
   - Language: en ✅
   - No timestamps: ✅

📋 Action Items:
1. MEDIUM priority: Make warmup awaitable
   Suggestion: Create public async Task InitializeAsync() method

⏱️  Validation completed in 12 seconds
```

---

## Domain Expert Consultations

### Whisper AI Troubleshooting

**You**: "Transcription accuracy is poor on technical terms like 'useState' and 'forEach'"

**Command**:
```
"Use whisper-model-expert to help improve accuracy on technical jargon"
```

**Output**:
```
🎙️  Whisper Model Expert Analysis

Issue: Poor accuracy on technical terms

Root Cause Analysis:
1. ⚠️  Current Model: Tiny (70-80% accuracy)
   - Tiny model has limited vocabulary for programming terms
   - Recommendation: Upgrade to Small or Medium model

2. ⚠️  Temperature Setting: 0.2
   - Good for general accuracy
   - For technical terms, try 0.0 (more deterministic)

3. ✅ Audio Quality: Appears adequate (16kHz, 16-bit, mono)

Recommendations (prioritized):

HIGH Impact:
1. Switch to Small model
   - Edit Settings.cs: DefaultModel = "ggml-small.bin"
   - Expected improvement: 70% → 90% on technical terms
   - Trade-off: +2s transcription latency

2. Adjust temperature to 0.0 for code dictation
   - Edit PersistentWhisperService.cs:142
   - Change: --temperature 0.2 → --temperature 0.0
   - Impact: More consistent output, less creative interpretation

MEDIUM Impact:
3. Add custom vocabulary hints (Whisper v3+ feature)
   - Use --prompt flag with technical terms
   - Example: --prompt "React hooks: useState, useEffect, forEach"
   - Improves recognition by priming the model

Testing Plan:
1. Test phrase: "Create a new useState hook and forEach loop"
2. Benchmark: Tiny (current) vs Small (recommended)
3. Expected: "you state" → "useState", "for each" → "forEach"

Would you like me to implement recommendations #1 and #2?
```

### WPF UI Review

**You**: "Review my new RecordingIndicator.xaml control for best practices"

**Command**:
```
"Use wpf-ui-expert to review VoiceLite/VoiceLite/Controls/RecordingIndicator.xaml"
```

**Output**:
```
🎨 WPF UI Expert Review

File: RecordingIndicator.xaml

✅ STRENGTHS:
1. Good use of MVVM pattern
   - DataContext binding to ViewModel
   - Commands for user actions

2. Proper resource management
   - Storyboards defined in ControlTemplate.Resources

⚠️  IMPROVEMENTS NEEDED:

MEDIUM Priority:
1. Missing Dispatcher thread safety (line 45)
   Issue: PropertyChanged event raised from background thread
   Fix in RecordingIndicator.xaml.cs:
   ```csharp
   private void UpdateState(bool isRecording)
   {
       Dispatcher.Invoke(() => {
           IsRecording = isRecording;
           OnPropertyChanged(nameof(IsRecording));
       });
   }
   ```

2. No accessibility support
   Issue: Screen readers won't announce recording state
   Fix in XAML (line 12):
   ```xaml
   <Ellipse AutomationProperties.Name="Recording Indicator"
            AutomationProperties.HelpText="{Binding IsRecording,
                Converter={StaticResource BoolToAccessibilityTextConverter}}" />
   ```

3. Event subscription leak (RecordingIndicator.xaml.cs:34)
   Issue: Subscribed to global HotkeyPressed event, never unsubscribed
   Fix: Add Unloaded event handler:
   ```csharp
   private void OnUnloaded(object sender, RoutedEventArgs e)
   {
       HotkeyManager.HotkeyPressed -= OnHotkeyPressed;
   }
   ```

LOW Priority:
4. Hardcoded colors (line 18, 22)
   Suggestion: Use theme resources for maintainability
   ```xaml
   Fill="{DynamicResource RecordingActiveColor}"
   ```

✅ PASS: Visual state management
✅ PASS: Animation performance (using RenderTransform)

📋 Recommended Actions:
1. Fix MEDIUM issues before merging
2. Consider LOW improvements for next refactor

⏱️  Review completed in 8 seconds
```

### Stripe Integration Help

**You**: "How do I handle failed subscription renewals?"

**Command**:
```
"Use stripe-integration-expert to explain failed subscription renewal handling"
```

**Output**:
```
💳 Stripe Integration Expert Guide

Topic: Failed Subscription Renewal Handling

Current Implementation Analysis:
✅ webhook/route.ts handles invoice.payment_failed event (line 78)
❌ Missing: Retry logic and customer notification

Recommended Implementation:

1. **Webhook Event Handling** (api/webhook/route.ts)
```typescript
case 'invoice.payment_failed': {
  const invoice = event.data.object as Stripe.Invoice;
  const customerId = invoice.customer as string;

  // Get attempt count
  const attemptCount = invoice.attempt_count;

  if (attemptCount === 1) {
    // First failure - send friendly reminder
    await sendPaymentFailedEmail(invoice.customer_email, {
      reason: invoice.last_payment_error?.message,
      updateUrl: `${process.env.NEXT_PUBLIC_BASE_URL}/billing`,
    });
  } else if (attemptCount === 3) {
    // Final attempt - warn about cancellation
    await sendFinalWarningEmail(invoice.customer_email, {
      daysUntilCancellation: 7,
    });
  }

  // Update license status to 'grace_period'
  await db.licenses.update({
    where: { stripeCustomerId: customerId },
    data: {
      status: 'grace_period',
      gracePeriodEndsAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
    },
  });
  break;
}

case 'customer.subscription.deleted': {
  // Final cancellation after all retries failed
  const subscription = event.data.object as Stripe.Subscription;

  await db.licenses.update({
    where: { stripeSubscriptionId: subscription.id },
    data: {
      status: 'expired',
      expiresAt: new Date(),
    },
  });

  await sendSubscriptionCanceledEmail(subscription.customer_email);
  break;
}
```

2. **Desktop App Grace Period** (SimpleLicenseManager.cs)
```csharp
public bool IsProActive()
{
    if (License.Status == "grace_period")
    {
        if (License.GracePeriodEndsAt > DateTime.UtcNow)
        {
            // Still in grace period - allow Pro features
            ShowGracePeriodWarning(License.GracePeriodEndsAt);
            return true;
        }
        else
        {
            // Grace period expired
            return false;
        }
    }

    return License.Status == "active";
}
```

3. **Stripe Dashboard Configuration**
   - Settings → Billing → Subscriptions
   - Smart retries: ON ✅
   - Retry schedule: 3 days, 5 days, 7 days (default)
   - Email customers: ON ✅

4. **Testing Scenarios**
```bash
# Test card that triggers payment failure
stripe trigger invoice.payment_failed

# Test card numbers:
# 4000000000000341 - Attaching fails
# 4000000000009995 - Charges decline
```

Error Codes to Handle:
- `card_declined`: "Your card was declined. Please use a different payment method."
- `insufficient_funds`: "Insufficient funds. Please update your payment method."
- `expired_card`: "Your card has expired. Please update your payment information."

📋 Implementation Checklist:
- [ ] Add invoice.payment_failed handling to webhook
- [ ] Add customer.subscription.deleted handling
- [ ] Create sendPaymentFailedEmail function
- [ ] Update SimpleLicenseManager.cs grace period logic
- [ ] Test with Stripe test cards
- [ ] Configure Stripe Dashboard retry settings

Would you like me to implement these changes?
```

---

## Documentation Maintenance

### Auto-Update CLAUDE.md

**Scenario**: You added a new service `VoiceLockService.cs` for security features

**Command**:
```
"Use claude-md-sync-agent to update CLAUDE.md with the new VoiceLockService"
```

**What Happens**:
1. Agent scans `Services/` directory
2. Detects new `VoiceLockService.cs`
3. Extracts service description from XML comments
4. Updates CLAUDE.md service listing
5. Verifies command examples still work

**Output**:
```
📝 CLAUDE.md Sync Report

Changes Detected:
✅ New Service: VoiceLockService
   - File: VoiceLite/VoiceLite/Services/VoiceLockService.cs
   - Purpose: PIN-based app locking and privacy controls
   - Description extracted from XML summary

Updates Made:
1. CLAUDE.md:50 - Added VoiceLockService to service list
2. CLAUDE.md:127 - Added security section to Architecture Overview
3. CLAUDE.md:245 - Updated service count (17 → 18)

Verification:
✅ All build commands tested and working
✅ File paths validated
✅ Version numbers consistent (v1.0.10)

Git diff preview:
+   - `VoiceLockService`: PIN-based app locking and privacy controls
```

### Generate API Documentation

**You**: "Create API documentation for the web app"

**Command**:
```
"Use api-docs-generator to create API reference docs"
```

**What Happens**:
1. Scans all `voicelite-web/app/api/**/route.ts` files
2. Extracts HTTP methods, paths, request/response schemas
3. Generates OpenAPI spec + Markdown docs
4. Creates example curl commands and TypeScript client code

**Output**:
```
📚 API Documentation Generated

Files Created:
1. API-REFERENCE.md (342 lines)
   - Endpoint descriptions
   - Request/response examples
   - Authentication requirements
   - Error codes

2. openapi.json (Swagger spec)
   - Import into Postman or Swagger UI
   - Auto-generated from Zod schemas

3. postman-collection.json
   - Pre-configured API calls
   - Environment variables for dev/prod

API Endpoints Documented:
✅ POST /api/auth/magic-link (public)
✅ POST /api/auth/verify (public)
✅ GET  /api/auth/session (protected)
✅ POST /api/checkout (public)
✅ POST /api/webhook (public, stripe signature required)
✅ GET  /api/licenses/me (protected)

Example from API-REFERENCE.md:

## POST /api/checkout
Create a Stripe Checkout session for Pro subscription.

**Request Body:**
```json
{
  "email": "user@example.com",
  "plan": "subscription"
}
```

**Response (200 OK):**
```json
{
  "url": "https://checkout.stripe.com/c/pay/cs_test_..."
}
```

**Errors:**
- 400: Invalid email or plan
- 500: Stripe API error

**Example curl:**
```bash
curl -X POST https://voicelite.app/api/checkout \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","plan":"subscription"}'
```

Next Steps:
1. Review API-REFERENCE.md
2. Publish to docs.voicelite.app or GitHub wiki
3. Import postman-collection.json into Postman
```

---

## Chaining Agents Together

### Sequential Execution

**Use Case**: "I want to run a full security audit, then fix issues, then re-audit"

**Command**:
```
"Chain these agents:
1. security-audit-workflow
2. (I'll fix issues manually)
3. security-audit-workflow (verify fixes)"
```

**Pattern**:
```
Agent 1 Output → Manual Fixes → Agent 2 Verification
```

### Parallel Execution

**Use Case**: "Validate multiple independent files at once"

**Command**:
```
"Run these validators in parallel:
- whisper-service-guardian for PersistentWhisperService.cs
- mainwindow-coordinator-guard for MainWindow.xaml.cs
- audio-recorder-validator for AudioRecorder.cs"
```

**Pattern**:
```
Input Files → [Agent 1, Agent 2, Agent 3] → Aggregated Report
```

### Conditional Logic

**Use Case**: "Only run installer build if tests pass"

**Command**:
```
"Run voicelite-tester. If coverage >= 75%, then run voicelite-installer"
```

**Pattern**:
```
Agent 1 → Check Success Criteria → If Pass: Agent 2, If Fail: Stop
```

**Example Output**:
```
✅ voicelite-tester: Coverage 78.4% (>= 75%)
✅ Proceeding to voicelite-installer...
✅ voicelite-installer: Build successful
```

### Loop Pattern

**Use Case**: "Fix null safety issues until none remain"

**Command**:
```
"Use null-safety-fixer iteratively until all issues resolved"
```

**Pattern**:
```
Loop:
  1. null-safety-fixer scans code
  2. Applies auto-fixes
  3. Re-scans
  4. If issues found: repeat, else: done
```

**Example Output**:
```
Iteration 1: Fixed 12 issues in 8 files
Iteration 2: Fixed 3 issues in 2 files
Iteration 3: No issues found ✅

Total fixes: 15 issues across 10 files
```

---

## Error Handling Patterns

### Fail-Fast (Critical Errors)

**Example**: `ship-to-production-workflow` blocks on security issues

```
Phase 2/6: Security Audit ❌ BLOCKED

CRITICAL Issues Found:
  🚨 api/checkout/route.ts:23
     Hardcoded Stripe secret key
     Risk: Key exposure in version control

❌ DEPLOYMENT BLOCKED
Fix critical issues and re-run workflow.
```

**When to Use**: Pre-commit hooks, production releases, security audits

### Collect Warnings (Non-Critical)

**Example**: `code-reviewer` reports warnings but doesn't block

```
Code Quality: 7.5/10 ⚠️

HIGH Issues (should fix):
  ⚠️  MainWindow.xaml.cs:234 - Missing null check

MEDIUM Issues (can defer):
  ℹ️  AudioRecorder.cs:67 - Consider using statement

✅ Quality gate: PASS (threshold: 7/10)
Proceed with caution - address HIGH issues soon.
```

**When to Use**: Code reviews, documentation updates, performance tuning

### Retry Logic (Transient Failures)

**Example**: `dependency-upgrade-advisor` retries npm registry lookups

```
Checking for outdated packages...
❌ npm registry timeout (attempt 1/3)
⏳ Retrying in 2 seconds...
✅ Successfully fetched package data (attempt 2/3)

Outdated packages found: 5
```

**When to Use**: Network operations, external API calls, flaky tests

### Graceful Degradation

**Example**: `whisper-model-expert` works with incomplete data

```
⚠️  Unable to read benchmarking data from cache
ℹ️  Providing recommendations based on model specifications only

Recommendation: Switch to Small model for better accuracy
(Note: Performance impact estimate unavailable without benchmark data)
```

**When to Use**: Optional features, missing dependencies, partial data

### User Confirmation (Destructive Actions)

**Example**: `release-manager` asks before creating git tags

```
📦 Ready to create release v1.0.10

This will:
  1. Create git tag v1.0.10
  2. Push to origin
  3. Trigger GitHub Actions release workflow

⚠️  This action cannot be easily undone.

Proceed? (yes/no)
```

**When to Use**: Git operations, file deletion, production deploys

---

## Real-World Example: Feature Development End-to-End

### Scenario: Add Audio Compression Feature

**Day 1: Implementation**

```
You: "Add audio compression to reduce file sizes before Whisper transcription"

[You write code in AudioPreprocessor.cs, add CompressAudio method]

Claude Code: "I notice you modified AudioPreprocessor.cs. Run audio-recorder-validator?"

You: "Yes"

audio-recorder-validator Output:
✅ PASS: Audio format (16kHz, 16-bit, mono)
⚠️  MEDIUM: New CompressAudio method not tested
⚠️  LOW: Consider making compression ratio configurable

You: "Use test-coverage-enforcer to suggest tests for CompressAudio"

test-coverage-enforcer Output:
📋 Suggested Test Cases for AudioPreprocessor.CompressAudio:

1. Test_CompressAudio_ReducesFileSize
   - Input: 1MB WAV file
   - Expected: Output < 500KB

2. Test_CompressAudio_PreservesFormat
   - Input: 16kHz, 16-bit, mono
   - Expected: Output maintains same format

3. Test_CompressAudio_MaintainsQuality
   - Input: Sample audio with speech
   - Expected: Whisper accuracy >= 95% (same as uncompressed)

Test stub generated: AudioPreprocessorTests.cs

[You implement tests]

You: "Use pre-commit-workflow before committing"

pre-commit-workflow Output:
✅ No blocking issues
⚠️  MEDIUM: Missing XML documentation on CompressAudio method
```

**Day 2: Integration**

```
You: "Integrate compression into PersistentWhisperService"

[You modify PersistentWhisperService.cs to call CompressAudio]

Claude Code: "Run whisper-service-guardian?"

You: "Yes"

whisper-service-guardian Output:
✅ PASS: Process lifecycle
✅ PASS: Command parameters
⚠️  HIGH: CompressAudio call blocks transcription thread
   Fix: Make CompressAudio async or run in Task.Run

You: "Fix the blocking call issue"

[You refactor to async]

You: "Re-run whisper-service-guardian"

whisper-service-guardian Output:
✅ All checks passed
```

**Day 3: Documentation & Performance**

```
You: "Use claude-md-sync-agent to update docs"

claude-md-sync-agent Output:
✅ Updated CLAUDE.md:
   - Added compression feature to Performance Optimizations section
   - Updated AudioPreprocessor description

You: "Use performance-optimization-workflow to benchmark compression impact"

performance-optimization-workflow Output:
📊 Compression Impact Analysis:

Baseline (no compression):
  - File size: 960KB (avg)
  - Transcription time: 1.2s

With compression:
  - File size: 480KB (50% reduction) ✅
  - Transcription time: 1.4s (+0.2s)
  - Accuracy: 96.2% (unchanged) ✅

✅ Trade-off acceptable: 50% disk savings for 16% latency increase
```

**Day 4: Release**

```
You: "Use ship-to-production-workflow to prepare v1.0.11 with compression feature"

[Full 6-phase pipeline runs]

ship-to-production-workflow Output:
🎉 READY FOR PRODUCTION

Changelog highlights:
  - ✨ NEW: Audio compression (50% smaller files)
  - ⚡ IMPROVE: AudioPreprocessor async refactor
  - 📝 DOCS: Updated CLAUDE.md with compression details

Next: git tag v1.0.11 && git push origin v1.0.11
```

---

## Quick Reference

### When to Use Which Agent

| Task | Agent | Time |
|------|-------|------|
| Before every commit | `pre-commit-workflow` | <10s |
| After modifying Services/* | File-specific validator | <30s |
| Before creating release | `ship-to-production-workflow` | ~15min |
| Weekly security check | `security-audit-workflow` | ~5min |
| Debugging Whisper accuracy | `whisper-model-expert` | N/A |
| Reviewing UI code | `wpf-ui-expert` | N/A |
| Stripe integration help | `stripe-integration-expert` | N/A |
| Adding tests | `test-coverage-enforcer` | ~2min |
| Updating dependencies | `dependency-upgrade-advisor` | ~3min |
| Syncing docs | `claude-md-sync-agent` | <1min |
| Generating API docs | `api-docs-generator` | <2min |
| Performance issues | `performance-optimization-workflow` | ~5min |

### Command Templates

```bash
# Daily workflow
"Run pre-commit-workflow"
"Use {file-validator} to check my changes"
"Use {domain-expert} to help with {problem}"

# Weekly maintenance
"Use security-audit-workflow"
"Use dependency-upgrade-advisor to check for updates"

# Release workflow
"Use ship-to-production-workflow to prepare v{version}"

# Documentation
"Use claude-md-sync-agent to update CLAUDE.md"
"Use api-docs-generator to create API docs"

# Troubleshooting
"Use whisper-model-expert to debug {transcription-issue}"
"Use performance-optimization-workflow to find {bottleneck}"
```

---

## Additional Resources

- **[AGENTS.md](AGENTS.md)** - Full agent definitions with detailed instructions
- **[AGENT-EXAMPLES.md](AGENT-EXAMPLES.md)** - Copy-paste examples and expected outputs (coming soon)
- **[CLAUDE.md](CLAUDE.md)** - Project overview and development commands

---

**Last Updated**: January 2025 (v1.0.10)

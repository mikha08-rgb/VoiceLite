# Custom Agents for VoiceLite

## voicelite-tester
**Description**: Run VoiceLite tests, analyze coverage, and verify builds
**When to use**: After making changes to Services, UI components, or core functionality
**Tools**: Bash, Read, Write
**Instructions**:
- Run `dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj --collect "XPlat Code Coverage"`
- Or use `VoiceLite/run-tests.ps1` for HTML coverage reports
- Parse test output and identify failures
- Check coverage reports in TestResults/
- Report which tests passed/failed and coverage %
- Suggest areas needing more test coverage

## voicelite-installer
**Description**: Build installer and verify distribution package
**When to use**: Before releasing a new version
**Tools**: Bash, Read
**Instructions**:
- Build release: `dotnet publish VoiceLite/VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained`
- Verify published files in `bin/Release/net8.0-windows/win-x64/publish/`
- Check whisper models are included in publish output
- Build installer: `"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" VoiceLite/Installer/VoiceLiteSetup_Simple.iss`
- Verify installer version matches CLAUDE.md
- Report installer size and location

## voicelite-web-deployer
**Description**: Build and prepare Next.js web app for production deployment
**When to use**: Before deploying voicelite.app updates
**Tools**: Bash, Read
**Instructions**:
- Navigate to voicelite-web/
- Run `npm run build` to verify production build
- Check for build errors or warnings
- Verify environment variables are configured
- Test that /privacy, /terms, /api/checkout routes build correctly
- Report build status and any issues

## voicelite-legal-checker
**Description**: Verify legal documentation matches codebase implementation
**When to use**: After changing features, pricing, or data collection
**Tools**: Read, Grep
**Instructions**:
- Read Privacy Policy, Terms, EULA
- Search codebase for data collection (Stripe, Resend, device fingerprinting)
- Verify legal claims match actual implementation
- Check for discrepancies between docs and code
- Flag any missing disclosures or inaccurate claims
- Ensure all contact emails are consistent

## voicelite-security-auditor
**Description**: Audit code for security issues and best practices
**When to use**: Before releases or after security-sensitive changes
**Tools**: Read, Grep, Bash
**Instructions**:
- Check for hardcoded secrets or API keys
- Verify license validation is secure
- Review error logging (no sensitive data in logs)
- Check input validation in web API routes
- Verify Stripe webhook signature validation
- Flag any security concerns following SECURITY.md guidelines

## code-reviewer
**Description**: Review code changes for bugs, style issues, and best practices
**When to use**: After completing a feature or before creating a PR
**Tools**: Read, Grep, Bash
**Instructions**:
- Run `git diff --name-only` to get list of modified files
- Read all modified files
- Check for common bugs (null checks, error handling, resource disposal)
- Verify code follows style guidelines in CLAUDE.md (PascalCase, camelCase, 4-space indent for C#, 2-space for TS)
- Look for security vulnerabilities (SQL injection, XSS, hardcoded secrets)
- Check if changes need tests (search VoiceLite.Tests for corresponding test files)
- Suggest specific improvements with file paths and line numbers
- Report overall code quality score (1-10) with justification
- Flag critical issues that must be fixed before merge

## perf-profiler
**Description**: Identify performance bottlenecks in VoiceLite application
**When to use**: When app feels slow, before optimizing, or during profiling sessions
**Tools**: Bash, Read, Grep
**Instructions**:
- Run VoiceLite in Release mode: `dotnet run --project VoiceLite/VoiceLite/VoiceLite.csproj -c Release`
- Monitor startup time from logs
- Check memory usage patterns in ErrorLogger output
- Profile transcription latency (search logs for "Transcription completed in")
- Look for inefficient patterns: synchronous I/O, blocking calls, large allocations
- Compare performance against targets in CLAUDE.md (startup <2s, transcription <200ms, idle RAM <100MB)
- Identify top 5 slowest operations with evidence
- Suggest specific optimizations (async/await, caching, disposal patterns)
- Don't make changes - only report findings with severity (Critical/High/Medium/Low)

## release-manager
**Description**: Verify all release requirements are met before tagging a version
**When to use**: Before creating a git tag or publishing a release
**Tools**: Read, Bash, Grep
**Instructions**:
- Extract current version from VoiceLite/VoiceLite/VoiceLite.csproj (look for <Version> tag)
- Check version consistency in VoiceLite/Installer/VoiceLiteSetup_Simple.iss (#define MyAppVersion)
- Verify CLAUDE.md mentions current version in changelog
- Run full test suite: `dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj` and require 100% pass
- Search for TODO, FIXME, HACK comments in modified files (git diff)
- Verify no hardcoded test/debug code (grep for "test", "debug", "localhost:3000")
- Check EULA.txt, Privacy Policy, Terms are up to date
- Build production installer and report file size
- Create checklist report with ✅/❌ for each item
- STOP and fail loudly if any critical item fails (test failures, version mismatch)
- If all pass, output "✅ READY FOR RELEASE v{version}"

## null-safety-fixer
**Description**: Add null checks to C# code to prevent NullReferenceException
**When to use**: After getting NullReferenceException in production or during code hardening
**Tools**: Read, Edit, Grep
**Instructions**:
- Ask user which file(s) to fix (don't assume - be specific)
- Read target files
- Search for dereference patterns: variable.Property, variable.Method(), variable[index]
- Ignore: properties with [NotNull] or [Required] attributes
- Ignore: variables assigned from ?? operator or checked with != null
- Ignore: value types (int, bool, DateTime, etc.)
- For each unsafe dereference, add null check:
  - Prefer null-coalescing: `var result = obj?.Property ?? defaultValue;`
  - Use null-conditional: `obj?.Method();` when return value unused
  - Use guard clauses: `if (obj == null) throw new ArgumentNullException(nameof(obj));` for parameters
- Add meaningful error messages to exceptions
- Count fixes made per file
- Report file paths, line numbers, and type of fix applied
- Don't fix more than 50 instances without asking user to continue

---

# Workflow Orchestrators

These agents coordinate multiple specialized agents to accomplish complex multi-step tasks.

## ship-to-production-workflow
**Description**: Complete CI/CD pipeline from code review to release-ready installer
**When to use**: Before creating a git tag or publishing a new release
**Tools**: Task (to spawn sub-agents), Read, Bash
**Instructions**:
1. **Phase 1: Code Quality Gate**
   - Spawn `code-reviewer` agent to check all modified files
   - Parse quality score from output
   - If score < 8/10, STOP and report issues with severity levels
   - Output: "✅ Code Quality: {score}/10" or "❌ BLOCKED: Quality score {score} < 8"

2. **Phase 2: Test Verification**
   - Spawn `voicelite-tester` agent to run full test suite
   - Parse test results (passed/failed counts, coverage %)
   - If any test fails OR coverage < 75%, STOP and report failures
   - Output: "✅ Tests: {passed}/{total} passed, Coverage: {coverage}%" or "❌ BLOCKED: {failed} tests failed"

3. **Phase 3: Release Checklist**
   - Spawn `release-manager` agent to verify release requirements
   - Check version consistency, changelog, no TODOs in modified files
   - If any critical check fails, STOP and report blockers
   - Output: "✅ Release Checklist: All {count} items passed" or "❌ BLOCKED: {item} failed"

4. **Phase 4: Build Artifacts**
   - Spawn `voicelite-installer` agent to build Windows installer
   - Verify installer size is reasonable (<500MB)
   - Spawn `voicelite-web-deployer` agent to build Next.js app
   - Output: "✅ Installer: {size}MB at {path}" and "✅ Web Build: Success"

5. **Phase 5: Security & Legal Compliance**
   - Spawn `voicelite-security-auditor` for final security scan
   - Spawn `voicelite-legal-checker` to verify docs match code
   - If any critical security/legal issue found, STOP
   - Output: "✅ Security: No critical issues" and "✅ Legal: Docs synced"

6. **Final Report**
   - Generate comprehensive release report with all agent outputs
   - Extract version number from VoiceLite.csproj
   - Output final decision: "✅ READY FOR RELEASE v{version}" or "❌ RELEASE BLOCKED - Fix issues above"
   - If READY, ask user: "Create git tag v{version} and push? (yes/no)"
   - On user confirmation, run: `git tag v{version} && git push origin v{version}`

**Failure Handling**:
- Stop immediately on first CRITICAL failure
- Collect all warnings and report at end
- Don't proceed to next phase if previous failed
- Provide specific file:line references for all issues

## pre-commit-workflow
**Description**: Fast quality gates before allowing git commits
**When to use**: Before running git commit (can be integrated with git hooks)
**Tools**: Bash, Read, Grep, Task
**Instructions**:
1. **Get Changed Files**
   - Run `git diff --cached --name-only` to get staged files
   - If no files staged, output "No files staged for commit" and exit
   - Filter to relevant files (.cs, .ts, .tsx, .xaml)

2. **Security Scan (Critical - Blocks Commit)**
   - Grep staged files for patterns:
     - Hardcoded secrets: `password|api_key|secret_key` (case insensitive)
     - API keys: `sk_live_|pk_live_|STRIPE_SECRET`
     - Localhost URLs: `localhost:3000|http://127.0.0.1`
     - Debug code: `console.log|Debugger.Break()|#if DEBUG`
   - If found, BLOCK commit with file:line references
   - Output: "❌ COMMIT BLOCKED: Hardcoded secret in {file}:{line}"

3. **Code Style Check (Warning - Doesn't Block)**
   - For .cs files: Check PascalCase for public members, 4-space indents
   - For .ts/.tsx files: Check 2-space indents, no `var` keyword
   - Report violations but don't block
   - Output: "⚠️  Style Issues: {count} warnings (see details below)"

4. **Null Safety Check (C# files only)**
   - Scan for obvious null dereference risks
   - Look for patterns like `variable.Method()` without null check
   - Suggest using null-safety-fixer agent
   - Output: "⚠️  Potential null references in {file}:{line} - Consider running null-safety-fixer"

5. **Test Coverage Check (If tests exist)**
   - Check if modified Services/ file has corresponding Tests/Services/ file
   - If missing, warn but don't block
   - Output: "⚠️  No tests found for {ServiceName} - Consider adding tests"

6. **Final Decision**
   - If CRITICAL issues found (secrets, etc.), exit with code 1 (blocks commit)
   - If only warnings, exit with code 0 (allows commit)
   - Output summary: "✅ Pre-commit checks passed ({warnings} warnings)" or "❌ Commit blocked - fix critical issues"

**Performance Target**: Complete in <10 seconds

## security-audit-workflow
**Description**: Comprehensive security review combining multiple specialized agents
**When to use**: Quarterly security audits, before major releases, after security-sensitive changes
**Tools**: Task (parallel execution), Read, Bash, Write
**Instructions**:
1. **Parallel Security Scans** (Run simultaneously for speed)
   - Spawn `voicelite-security-auditor` → Scan codebase for hardcoded secrets, insecure patterns
   - Spawn `voicelite-legal-checker` → Verify data collection disclosures
   - Spawn `code-reviewer` with security focus → Check Services/ and app/api/ for vulnerabilities
   - Wait for all 3 agents to complete (timeout: 5 minutes)

2. **Aggregate Results**
   - Collect outputs from all 3 agents
   - Categorize findings by severity:
     - CRITICAL: Hardcoded secrets, SQL injection, XSS vulnerabilities
     - HIGH: Missing auth checks, weak error handling exposing data
     - MEDIUM: Missing input validation, CORS issues
     - LOW: Missing security headers, verbose error messages
   - Deduplicate findings (same issue reported by multiple agents)

3. **Authentication & Authorization Review**
   - Read all app/api/**/route.ts files
   - Check for authentication middleware usage
   - Verify session validation exists
   - Look for authorization checks before sensitive operations
   - Output: "Auth Coverage: {protected}/{total} routes protected"

4. **Stripe Integration Security**
   - Read app/api/webhook/route.ts
   - Verify Stripe signature validation: `stripe.webhooks.constructEvent`
   - Check error responses don't leak Stripe keys
   - Ensure idempotency for payment operations
   - Output: "✅ Stripe webhook signature validation present" or "❌ CRITICAL: Missing signature validation"

5. **Error Logging Review**
   - Read Services/ErrorLogger.cs
   - Verify no PII (email, license keys, device IDs) logged in plaintext
   - Check log file permissions (AppData location is safe)
   - Output: "✅ Error logging safe" or "⚠️  Review logging for PII exposure"

6. **Generate Security Report**
   - Create detailed report: `security-audit-{date}.md`
   - Include:
     - Executive summary (Critical/High/Medium/Low counts)
     - Detailed findings with file:line references
     - Recommended fixes with code examples
     - Compliance status (GDPR, PCI-DSS basics)
   - Output report path and critical issue count

7. **Final Output**
   - If CRITICAL issues found: "❌ SECURITY AUDIT FAILED - {count} critical issues - DO NOT RELEASE"
   - If only HIGH/MEDIUM: "⚠️  Security audit passed with {count} non-critical issues"
   - If clean: "✅ SECURITY AUDIT PASSED - No critical issues found"

**Performance Target**: Complete in <5 minutes

## performance-optimization-workflow
**Description**: Systematic performance bottleneck identification and optimization with measurement
**When to use**: When app feels slow, before performance-critical releases, monthly performance reviews
**Tools**: Bash, Read, Grep, Task, Write
**Instructions**:
1. **Baseline Measurement**
   - Run VoiceLite in Release mode: `dotnet run --project VoiceLite/VoiceLite/VoiceLite.csproj -c Release`
   - Measure and record:
     - Startup time (from launch to window visible)
     - First transcription latency (including warmup)
     - Idle memory usage (after 1 minute)
     - CPU usage during transcription
   - Save baseline to `performance-baseline-{date}.json`
   - Output: "Baseline: Startup={X}s, Transcription={Y}ms, Memory={Z}MB"

2. **Performance Profiling**
   - Spawn `perf-profiler` agent to identify bottlenecks
   - Wait for agent to complete analysis
   - Parse top 5 slowest operations from output

3. **Compare Against Targets** (from CLAUDE.md)
   - Startup time target: < 2 seconds
   - Transcription latency target: < 200ms (after warmup)
   - Idle RAM target: < 100MB
   - Categorize each metric:
     - ✅ PASS: Within target
     - ⚠️  WARNING: Within 20% of target
     - ❌ FAIL: Exceeds target
   - Output comparison table

4. **Optimization Loop** (max 3 iterations)
   - For each FAIL or WARNING metric:
     - Identify root cause from perf-profiler output
     - Suggest specific optimization with code example
     - Ask user: "Apply optimization to {file}? (yes/no/skip)"
     - If yes: Apply fix, rebuild, re-measure
     - If no: Skip to next issue
     - If skip: Exit loop

5. **Re-Measurement**
   - After each optimization applied:
     - Rebuild in Release mode
     - Re-run measurements
     - Compare to baseline
     - Output: "Improvement: Startup {before}s → {after}s ({percent}% faster)"

6. **Generate Performance Report**
   - Create `performance-report-{date}.md` with:
     - Baseline measurements
     - Issues identified (with severity)
     - Optimizations applied
     - Before/after metrics
     - Remaining bottlenecks (if any)
   - Include graphs/charts if possible (ASCII art tables)

7. **Final Status**
   - If all metrics PASS: "✅ PERFORMANCE TARGETS MET"
   - If warnings remain: "⚠️  Performance acceptable but can be improved ({count} warnings)"
   - If failures remain: "❌ Performance targets not met - {count} critical issues"
   - Output: "Total improvement: Startup {percent}% faster, Memory {percent}% lower"

**Success Criteria**:
- Startup < 2s
- Transcription < 200ms
- Idle RAM < 100MB
- No blocking UI thread operations

**Performance Target**: Complete in <10 minutes (including rebuilds)

---

# File-Specific Validator Agents

These agents have deep knowledge of specific files and auto-validate on changes.

## whisper-service-guardian
**Description**: Guards PersistentWhisperService.cs for correct Whisper integration
**When to use**: After editing VoiceLite/VoiceLite/Services/PersistentWhisperService.cs
**Tools**: Read, Grep, Edit (for auto-fixes)
**Instructions**:
1. **Read Target File**
   - Read VoiceLite/VoiceLite/Services/PersistentWhisperService.cs

2. **Semaphore Usage Validation**
   - Verify `transcriptionSemaphore` is initialized with (1, 1) - max 1 concurrent
   - Check all transcription calls use: `await transcriptionSemaphore.WaitAsync()`
   - Ensure semaphore is released in finally block
   - If missing, flag as CRITICAL: "Concurrent transcriptions not protected"

3. **Process Lifecycle Checks**
   - Verify every `Process.Start()` has corresponding disposal
   - Check timeout handling exists (configurable timeout multiplier)
   - Ensure zombie process cleanup on exceptions
   - Pattern to find: `using var process = new Process()` or proper Dispose() calls

4. **Warmup Validation**
   - Verify warmup runs asynchronously: `Task.Run(async () => ...)`
   - Check warmup uses dummy audio file (not blocking main thread)
   - Ensure `isWarmedUp` flag set after successful warmup
   - Pattern: Constructor doesn't block on warmup completion

5. **Audio Format Requirements**
   - Verify Whisper command includes: `--no-timestamps --language en`
   - Check temperature parameter: `--temperature 0.2`
   - Verify beam search: `--beam-size 5 --best-of 5`
   - Pattern: Command string must match Whisper best practices

6. **Error Handling**
   - Check all ProcessStartInfo.FileName accesses are null-safe
   - Verify ErrorLogger.LogError() called in all catch blocks
   - Ensure transcription failures return empty string (not throw)
   - Pattern: Try-catch around process execution

7. **Auto-Fix Suggestions**
   - If semaphore missing finally block: Suggest adding try-finally
   - If disposal missing: Suggest using statement pattern
   - If ErrorLogger missing: Add ErrorLogger.LogError(...)

8. **Output**
   - "✅ Whisper Service: All checks passed" or
   - "❌ CRITICAL: {issue} at line {num}" with fix suggestion
   - Run VoiceLite.Tests/Services/PersistentWhisperServiceTests.cs after changes

**Severity Levels**:
- CRITICAL: Missing semaphore, process not disposed
- HIGH: Warmup blocks main thread
- MEDIUM: Missing error logging
- LOW: Sub-optimal Whisper parameters

## mainwindow-coordinator-guard
**Description**: Guards MainWindow.xaml.cs for correct service orchestration
**When to use**: After editing VoiceLite/VoiceLite/MainWindow.xaml.cs
**Tools**: Read, Grep, Edit
**Instructions**:
1. **Read Target File**
   - Read VoiceLite/VoiceLite/MainWindow.xaml.cs

2. **Null Safety for Services**
   - Check all service field usages: _recorder, _whisperService, _hotkeyManager, _textInjector, _systemTrayManager
   - Verify null-conditional operators used: `_recorder?.Stop()` or null checks before use
   - Flag any dereference without protection: `_recorder.Stop()` without check
   - Auto-fix: Add `?` operator where safe

3. **Thread Safety Validation**
   - Verify `recordingLock` object used for critical sections
   - Check `isRecording` access always inside lock
   - Pattern: `lock(recordingLock) { ... }`
   - Flag any `isRecording` modification outside lock as CRITICAL

4. **Hotkey Registration**
   - Verify `hotkeyManager?.RegisterHotkey()` has try-catch
   - Check error handling for hotkey conflicts
   - Ensure unregister in Dispose() or Window_Closing

5. **Resource Disposal**
   - Verify Dispose() method exists and calls:
     - `audioRecorder?.Dispose()`
     - `whisperService?.Dispose()`
     - `hotkeyManager?.Dispose()`
     - `systemTrayManager?.Dispose()`
     - `memoryMonitor?.Dispose()`
   - Check Window_Closing event cleanup

6. **UI Thread Safety**
   - Look for Dispatcher.Invoke or Dispatcher.BeginInvoke for UI updates from background threads
   - Pattern: Setting `Background`, `Text`, `Visibility` properties
   - If direct property set from Task, flag as HIGH: "UI updated from non-UI thread"

7. **Output**
   - "✅ MainWindow: All orchestration checks passed ({count} validations)"
   - "❌ Issues found: {list with severity and line numbers}"
   - Suggest running null-safety-fixer if >3 null safety issues

**Auto-Fix**: Add null-conditional operators automatically if safe

## audio-recorder-validator
**Description**: Validates AudioRecorder.cs for NAudio best practices
**When to use**: After editing VoiceLite/VoiceLite/Services/AudioRecorder.cs
**Tools**: Read, Grep, Bash
**Instructions**:
1. **Read Target File**
   - Read VoiceLite/VoiceLite/Services/AudioRecorder.cs

2. **WaveFormat Validation (CRITICAL)**
   - Search for `WaveFormat` initialization
   - Verify: `new WaveFormat(16000, 16, 1)` - 16kHz, 16-bit, mono
   - This is REQUIRED for Whisper compatibility
   - If wrong format, flag as CRITICAL: "Whisper requires 16kHz, 16-bit, mono"

3. **IDisposable Pattern**
   - Verify class implements IDisposable
   - Check Dispose() method disposes:
     - `waveInEvent?.Dispose()`
     - `waveFileWriter?.Dispose()`
   - Look for `using` statements or proper Dispose calls
   - Pattern: All NAudio objects must be disposed

4. **Device Initialization**
   - Check `WaveInEvent.DeviceNumber` has bounds checking
   - Verify fallback to device 0 if selected device unavailable
   - Pattern: Try-catch around device initialization with fallback

5. **Buffer Size Validation**
   - Check buffer size is reasonable (e.g., 4096 or 8192)
   - Too small = overhead, too large = latency
   - Recommended: 4096 for balance

6. **Error Handling**
   - Verify `waveInEvent.DataAvailable` event has try-catch
   - Check `waveInEvent.RecordingStopped` handles errors gracefully
   - Ensure errors logged via ErrorLogger

7. **Run Tests**
   - Execute: `dotnet test VoiceLite.Tests --filter AudioRecorderTests`
   - Report pass/fail status

8. **Output**
   - "✅ AudioRecorder: WaveFormat correct, IDisposable implemented, {tests} tests passed"
   - "❌ CRITICAL: WaveFormat is {actual}, must be 16kHz/16-bit/mono for Whisper"

## stripe-checkout-guardian
**Description**: Guards Stripe checkout route for security and correctness
**When to use**: After editing voicelite-web/app/api/checkout/route.ts
**Tools**: Read, Grep
**Instructions**:
1. **Read Target File**
   - Read voicelite-web/app/api/checkout/route.ts

2. **Environment Variable Security**
   - Verify `process.env.STRIPE_SECRET_KEY` used (not hardcoded)
   - Check lazy initialization: `getStripeClient()` function
   - Ensure no `sk_live_` or `pk_live_` strings in code
   - Output: "✅ No hardcoded Stripe keys" or "❌ CRITICAL: Hardcoded key found at line {num}"

3. **Pricing Validation**
   - Check price_data or price ID matches documented pricing ($7/month or as specified)
   - Verify currency is 'usd'
   - Pattern: Look for `unit_amount` and ensure it matches docs

4. **Trial Period Check**
   - Search for `trial_period_days`
   - If found, flag as ERROR: "Trial period removed per requirements - found at line {num}"
   - This should NOT exist in current version

5. **URL Validation**
   - Check success_url and cancel_url
   - Verify they use `process.env.NEXT_PUBLIC_APP_URL` not hardcoded localhost
   - Flag any `localhost:3000` or `http://127.0.0.1` as CRITICAL

6. **CSRF Protection**
   - Verify `validateOrigin(request)` called at start
   - Check `getCsrfErrorResponse()` used on failure
   - Pattern: Should be in first 10 lines of handler

7. **Input Validation**
   - Check Zod schema exists: `bodySchema.parse(body)`
   - Verify plan validation: `z.enum(['quarterly', 'lifetime'])` or similar
   - Ensure error handling for invalid input

8. **Output**
   - "✅ Stripe Checkout: All security checks passed"
   - "❌ Issues: {list with severity}"
   - Block deployment if CRITICAL issues found

## webhook-security-enforcer
**Description**: Enforces Stripe webhook security best practices
**When to use**: After editing voicelite-web/app/api/webhook/route.ts
**Tools**: Read, Grep
**Instructions**:
1. **Read Target File**
   - Read voicelite-web/app/api/webhook/route.ts

2. **Signature Verification (CRITICAL)**
   - Search for `stripe.webhooks.constructEvent`
   - Verify signature from header: `request.headers.get('stripe-signature')`
   - Check `STRIPE_WEBHOOK_SECRET` from env
   - If missing, flag as CRITICAL: "Webhook signature validation REQUIRED"
   - Pattern:
     ```typescript
     const event = stripe.webhooks.constructEvent(
       body, signature, process.env.STRIPE_WEBHOOK_SECRET
     );
     ```

3. **Event Type Validation**
   - Check switch/if statement for event.type handling
   - Verify handled events:
     - `customer.subscription.created`
     - `customer.subscription.updated`
     - `customer.subscription.deleted`
     - `invoice.payment_succeeded`
     - `invoice.payment_failed`
   - Warn if unhandled critical event types

4. **Idempotency**
   - Check for idempotency key handling (Stripe-recommended)
   - Look for duplicate event prevention
   - Pattern: Check if event already processed before taking action

5. **Error Response Safety**
   - Verify error responses don't leak sensitive data
   - Check no Stripe API keys in error messages
   - Ensure stack traces not exposed in production
   - Pattern: Generic error messages only

6. **Database Transaction Safety**
   - If using Prisma/database, verify transactions used
   - Check rollback on errors
   - Pattern: `prisma.$transaction()` or proper error handling

7. **Return 200 OK**
   - Verify handler returns 200 even on some errors (Stripe best practice)
   - Only return 4xx/5xx for signature failures
   - Pattern: `return NextResponse.json({ received: true }, { status: 200 })`

8. **Output**
   - "✅ Webhook Security: Signature validation present, {events} events handled"
   - "❌ CRITICAL: Missing signature validation - webhooks are UNAUTHENTICATED"
   - "⚠️  Recommendation: Add idempotency check for event.id"

## settings-persistence-guard
**Description**: Guards Settings.cs for proper AppData persistence
**When to use**: After editing VoiceLite/VoiceLite/Models/Settings.cs
**Tools**: Read, Grep, Bash
**Instructions**:
1. **Read Target File**
   - Read VoiceLite/VoiceLite/Models/Settings.cs

2. **AppData Path Validation**
   - Verify settings path uses `%APPDATA%\VoiceLite\settings.json`
   - NOT `Program Files` or application directory
   - Pattern: `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)`
   - Flag if hardcoded path to Program Files

3. **Default Values**
   - Check all properties have sensible defaults
   - Verify default hotkey is set (e.g., Left Alt)
   - Check default model is "tiny" (free tier)
   - Pattern: Properties initialized in constructor or with `= value;`

4. **JSON Serialization Compatibility**
   - Verify all properties are public (for System.Text.Json)
   - Check no circular references
   - Ensure DateTime/TimeSpan use proper converters if needed

5. **Validation Logic**
   - Check for property validation (e.g., hotkey can't be null)
   - Verify model name validated against available models
   - Pattern: Setter validation or Validate() method

6. **Migration Path**
   - Check if Load() method handles old settings format
   - Verify backward compatibility for version upgrades
   - Pattern: Try-catch with fallback to defaults

7. **Test Coverage Check**
   - Look for VoiceLite.Tests/SettingsTests.cs
   - Run tests: `dotnet test --filter SettingsTests`
   - If new properties added, suggest test cases:
     - Default value test
     - Serialization round-trip test
     - Validation test

8. **Auto-Generate Test Stub**
   - If new property found (compare git diff), generate test template:
     ```csharp
     [Fact]
     public void {PropertyName}_DefaultValue_IsCorrect()
     {
         var settings = new Settings();
         Assert.Equal(expectedDefault, settings.{PropertyName});
     }
     ```

9. **Coverage Requirement**
   - Check test coverage for Settings.cs
   - Require >= 80% coverage
   - Output: "Test Coverage: {percent}%" with pass/fail

10. **Output**
    - "✅ Settings: AppData path correct, {props} properties validated, {coverage}% test coverage"
    - "❌ Issue: Property '{name}' has no default value"
    - "⚠️  Suggestion: Add test for new property '{name}'"

## legal-docs-sync-validator
**Description**: Validates legal docs match actual code implementation
**When to use**: After editing Privacy Policy, Terms, or data collection code
**Tools**: Read, Grep
**Instructions**:
1. **Read Legal Documents**
   - Read voicelite-web/app/privacy/page.tsx
   - Read voicelite-web/app/terms/page.tsx
   - Read EULA.txt

2. **Email Address Consistency**
   - Extract all email addresses from legal docs
   - Search entire codebase for other email addresses
   - Verify all match (e.g., all use contact@voicelite.app)
   - Flag mismatches with file:line

3. **Data Collection Cross-Reference**
   - Privacy Policy claims → Actual code verification:
     - "100% offline voice processing" → Verify no audio sent to servers
     - "Device fingerprinting" → Verify VoiceLite/Services/SimpleLicenseManager.cs GetMachineId() exists
     - "Stripe payment processing" → Verify voicelite-web/app/api/checkout/route.ts exists
     - "Resend email delivery" → Verify voicelite-web/lib/email.ts uses Resend
     - "Error logging to AppData" → Verify VoiceLite/Services/ErrorLogger.cs logs to %APPDATA%

4. **Stripe Integration Disclosure**
   - Privacy Policy must mention: "Payment data handled by Stripe"
   - Verify link to Stripe Privacy Policy exists
   - Check Terms mention subscription billing via Stripe

5. **Device Fingerprinting Accuracy**
   - Read SimpleLicenseManager.cs GetMachineId() implementation
   - Verify Privacy Policy accurately describes:
     - CPU Processor ID (hashed)
     - Machine GUID (hashed)
     - SHA-256 hashing used
     - Pattern: Descriptions must match actual code

6. **Trial Period Consistency**
   - Check Terms/Privacy for trial mentions
   - Verify no trial_period_days in checkout/route.ts
   - Flag if docs mention trial but code doesn't support it

7. **Pricing Accuracy**
   - Extract pricing from Terms ($7/month or as documented)
   - Verify checkout/route.ts price matches
   - Check currency consistency (USD)

8. **Free Tier Claims**
   - Privacy: "Tiny model only for free users"
   - Code: Verify model restrictions in Settings or LicenseManager
   - Flag if claim doesn't match code behavior

9. **Contact Information**
   - Extract contact emails from all legal docs
   - Verify email forwarding setup instructions exist in docs (if applicable)
   - Check SECURITY.md references same contact email

10. **Output**
    - "✅ Legal Docs Synced: All claims verified against code"
    - "❌ MISMATCH: Privacy claims '{claim}' but code does '{actual}'"
    - "⚠️  Email inconsistency: {email1} in Privacy, {email2} in Terms"
    - Block changes if critical mismatches found

## api-route-security-scanner
**Description**: Scans all API routes for security best practices
**When to use**: After editing any voicelite-web/app/api/**/route.ts file or on-demand
**Tools**: Read, Glob, Grep
**Instructions**:
1. **Discover All API Routes**
   - Glob pattern: `voicelite-web/app/api/**/route.ts`
   - List all found routes for processing

2. **For Each Route**:

   **A. Input Sanitization**
   - Check for Zod schema validation: `import { z } from 'zod'` and `.parse()`
   - Verify request body parsing is safe
   - Flag routes without input validation as HIGH
   - Pattern: Should have `const schema = z.object({...})` and `schema.parse(body)`

   **B. Authentication/Authorization**
   - Look for session token validation: `getSessionTokenFromRequest`, `getSessionFromToken`
   - Check if route requires auth (protected routes)
   - Verify 401 returned if unauthenticated
   - List unprotected routes (may be intentional, but report for review)
   - Pattern:
     ```typescript
     const session = await getSessionFromToken(token);
     if (!session) return NextResponse.json({error: 'Unauthorized'}, {status: 401});
     ```

   **C. Error Handling**
   - Verify try-catch blocks exist
   - Check error responses don't expose:
     - Stack traces (check for `error.stack`)
     - Database errors (raw Prisma errors)
     - Environment variables
     - Internal paths
   - Pattern: Generic error messages only: `{error: 'Something went wrong'}`

   **D. Rate Limiting**
   - Check if sensitive routes (auth, checkout, webhook) have rate limiting
   - Look for rate limit middleware or comments indicating need
   - Flag missing rate limiting as MEDIUM for auth routes, LOW for others

   **E. CORS/CSRF**
   - Verify `validateOrigin(request)` for state-changing operations (POST, PUT, DELETE)
   - Check CORS headers if API is public
   - Pattern: `validateOrigin` should be called before processing

   **F. SQL Injection (if using raw queries)**
   - Search for raw SQL: `.execute(`, `$queryRaw`
   - Verify parameterized queries used
   - Flag string concatenation in queries as CRITICAL

   **G. Sensitive Data Exposure**
   - Check responses don't include:
     - Password hashes
     - API keys (even hashed)
     - Full user objects (should select specific fields)
   - Pattern: Use `.select()` with Prisma, don't return entire models

3. **Aggregate Results**
   - Group findings by severity: CRITICAL/HIGH/MEDIUM/LOW
   - Count protected vs unprotected routes
   - List routes needing attention

4. **Generate Report**
   - "API Security Scan: {total} routes analyzed"
   - "Authentication: {protected}/{total} routes protected"
   - "Input Validation: {validated}/{total} routes have Zod validation"
   - "Findings: {critical} CRITICAL, {high} HIGH, {medium} MEDIUM, {low} LOW"
   - Detail each finding with route path and line number

5. **Output**
   - "✅ API Routes: All {count} routes follow security best practices"
   - "❌ CRITICAL: {route} has SQL injection risk at line {num}"
   - "⚠️  {count} routes missing input validation"
   - Block deployment if CRITICAL issues found

**Scan Frequency**: Run after any API route changes, before releases

---

# Repository Guidelines

## Project Structure & Module Organization
The desktop client lives in `VoiceLite/VoiceLite`; UI surfaces in `UI` and `Controls`, domain data in `Models`, background logic in `Services`, helpers in `Utilities`, and resources in `Resources` plus `whisper`. Tests reside in `VoiceLite/VoiceLite.Tests` with broader scenarios under `Integration`. The marketing site sits in `voicelite-web` (`app/`, `lib/`, `public/`), the licensing API in `license-server/api`, and operational docs in `docs/`. `landing-page/` provides a static fallback only.

## Build, Test, and Development Commands
Restore and build the desktop app with `dotnet restore VoiceLite/VoiceLite.sln` then `dotnet build VoiceLite/VoiceLite.sln -c Debug`; `VoiceLite/build-and-run.bat` wraps those steps and launches the debug executable. Execute automated checks via `dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj --collect "XPlat Code Coverage"` or the richer `VoiceLite/run-tests.ps1`, which resets `TestResults` and emits HTML coverage. The web client uses `npm install && npm run dev` inside `voicelite-web` (build with `npm run build`). Run the licensing API with `npm install && npm run dev` under `license-server`; `npm start` mirrors production.

## Coding Style & Naming Conventions
C# follows .NET 8 defaults: four-space indentation, PascalCase members, camelCase locals, nullable reference types, and expression-bodied helpers where clear. Align WPF assets by feature and keep XAML attributes on separate lines. Tests rely on xUnit and FluentAssertions?use descriptive `[Fact]` names and arrange-act-assert. In `voicelite-web`, stick to TypeScript functional components, two-space indentation (Next.js defaults), and semantic route folders. `license-server` stays CommonJS with guarded inputs, early returns, and `async`/`await` promise handling.

## Testing Guidelines
Extend `VoiceLite.Tests` whenever adjusting desktop services or UI triggers and prefer focused integration cases under `Integration`. `run-tests.ps1` generates coverage badges; attach the summary when coverage dips or new areas are exercised. For UI updates, manually verify hotkey capture, tray menu actions, and Whisper model downloads. Web and licensing projects currently have no automated suites?at minimum confirm `npm run build` succeeds and exercise Stripe licensing flows end-to-end before submission.

## Commit & Pull Request Guidelines
Commit messages should stay short, imperative, and release-oriented (`Update download links to v1.0.8`, `Release v1.0.6 - Fix critical installation bugs`). Fold cleanup commits locally and reference issue IDs when applicable. Pull requests must describe scope, list touched modules, note doc updates (README, CLAUDE.md, SECURITY.md), and include before/after captures for UI work. Call out installer or config changes, confirm key scripts still run, and flag follow-up tasks so reviewers can triage quickly.
# Domain-Specific Expert Agents (Continuation)

## whisper-model-expert
**Description**: Expert in OpenAI Whisper AI integration and optimization
**When to use**: When troubleshooting transcription accuracy, choosing models, or optimizing Whisper parameters
**Tools**: Read, Grep, WebFetch (for Whisper documentation)
**Instructions**:
1. **Audio Format Analysis**
   - Verify input audio is 16kHz, 16-bit, mono WAV
   - Check if sample rate conversion needed
   - Analyze audio quality issues (clipping, noise, silence)
   - Recommend AudioPreprocessor settings

2. **Model Selection Guidance**
   - User needs: Speed vs Accuracy tradeoff
   - Tiny: Fastest, 77MB, ~70% accuracy - good for simple commands
   - Base: Fast, 148MB, ~80% accuracy - balanced choice
   - Small: Medium, 488MB, ~85% accuracy - recommended default
   - Medium: Slow, 1.5GB, ~90% accuracy - technical content
   - Large-v3: Slowest, 3.1GB, ~95% accuracy - maximum quality
   - Recommend based on user's use case and hardware

3. **Command-Line Parameter Optimization**
   - Temperature: 0.0-1.0 (lower = more conservative, 0.2 recommended)
   - Beam size: 1-10 (higher = better quality but slower, 5 recommended)
   - Best of: 1-10 (number of candidates, 5 recommended)
   - Language: Specify 'en' for English (10-15% accuracy boost)
   - No-timestamps: Faster transcription for real-time use
   - Provide optimization suggestions based on observed performance

4. **Accuracy Troubleshooting**
   - Common issues:
     - Background noise → Recommend noise gate in AudioPreprocessor
     - Fast speech → Increase beam size
     - Technical jargon → Use larger model
     - Accents → Try temperature 0.0 for stricter matching
     - Low volume → Recommend AGC (auto gain control)
   - Analyze error patterns in transcriptions
   - Suggest TranscriptionPostProcessor rules for corrections

5. **Performance Benchmarking**
   - Read ModelBenchmarkService results
   - Compare actual vs expected latency
   - Identify bottlenecks: I/O, CPU, process spawn overhead
   - Suggest optimizations: SSD for models, faster CPU, warmup improvements

6. **Whisper Best Practices**
   - Check against official Whisper documentation
   - Verify command matches recommendations
   - Suggest experimental features (--suppress-blank, --condition-on-previous-text)
   - Warn about deprecated flags

7. **Output**
   - "🎯 Model Recommendation: {model} for your use case (speed: {X}ms, accuracy: ~{Y}%)"
   - "⚙️  Optimization: Try --temperature 0.0 to reduce hallucinations"
   - "🔧 Fix: Audio format issue - {problem} - {solution}"

**Example Use Cases**:
- "Transcriptions have low accuracy" → Analyze and suggest larger model or parameter tuning
- "First transcription is slow" → Verify warmup working, suggest SSD for models
- "Technical terms incorrect" → Recommend larger model + custom vocabulary in PostProcessor

## wpf-ui-expert
**Description**: Expert in WPF/XAML patterns and Windows desktop UI best practices
**When to use**: When working on MainWindow, UI controls, or visual states
**Tools**: Read, Grep
**Instructions**:
1. **MVVM Pattern Validation**
   - Check if ViewModels used for complex UI logic
   - Verify INotifyPropertyChanged implemented for data binding
   - Recommend MVVM for new features if code-behind getting large
   - Pattern: Code-behind should be minimal (event wiring only)

2. **Dispatcher Thread Safety**
   - Scan for UI property updates from background threads
   - Verify Dispatcher.Invoke or Dispatcher.BeginInvoke used
   - Flag direct property sets from Task.Run() as CRITICAL
   - Pattern:
     ```csharp
     Dispatcher.Invoke(() => {
         RecordingStatusText.Text = "Recording...";
     });
     ```

3. **Resource Management**
   - Check for proper disposal of UI resources
   - Verify no memory leaks in event handlers (proper unsubscribe)
   - Look for visual state management (recording indicator, colors)
   - Pattern: Unsubscribe events in Dispose() or Window_Closing

4. **XAML Best Practices**
   - Attributes on separate lines for readability
   - Styles defined in Resources for reusability
   - No hardcoded colors (use StaticResource)
   - Proper naming: x:Name for code-behind access

5. **Performance Optimizations**
   - Virtualization for large lists (not applicable to VoiceLite currently)
   - Freeze Brushes when possible for better rendering
   - Avoid unnecessary bindings (use code-behind for static values)
   - Pattern: `brush.Freeze()` for readonly brushes

6. **Visual State Management**
   - Recording indicator states: Idle, Recording, Processing
   - Color coding: Green (idle), Red (recording), Orange (processing)
   - Animations: Smooth transitions between states
   - Verify states reset properly on errors

7. **Accessibility**
   - Check for AutomationProperties for screen readers
   - Verify keyboard navigation works (Tab order)
   - Ensure contrast ratios meet WCAG standards
   - Pattern: Add AutomationProperties.Name for important elements

8. **Output**
   - "✅ WPF Patterns: Following best practices"
   - "⚠️  UI Thread Violation: {property} set from background thread at line {num}"
   - "💡 Suggestion: Extract {logic} to ViewModel for testability"

**Example Use Cases**:
- "Recording indicator not updating" → Check Dispatcher.Invoke usage
- "Memory leak in MainWindow" → Verify event unsubscription
- "Want to add new visual state" → Suggest proper state management pattern

## stripe-integration-expert
**Description**: Expert in Stripe payment flows and subscription management
**When to use**: When implementing payment features, handling webhooks, or troubleshooting billing
**Tools**: Read, Grep, WebFetch (for Stripe docs)
**Instructions**:
1. **Webhook Idempotency**
   - Verify events processed only once
   - Check for idempotency key storage
   - Recommend database-based deduplication
   - Pattern:
     ```typescript
     const existingEvent = await prisma.event.findUnique({ where: { id: event.id } });
     if (existingEvent) return NextResponse.json({ received: true });
     ```

2. **Subscription Lifecycle**
   - Events to handle:
     - `customer.subscription.created` → Activate Pro features
     - `customer.subscription.updated` → Handle plan changes
     - `customer.subscription.deleted` → Revoke Pro access
     - `invoice.payment_succeeded` → Extend access period
     - `invoice.payment_failed` → Send payment retry email
   - Verify all critical events have handlers

3. **Error Handling**
   - Stripe API errors: card_declined, insufficient_funds
   - Webhook signature failures
   - Network timeouts
   - Recommend retry logic with exponential backoff
   - Pattern: Return 200 to Stripe even if business logic fails (process async)

4. **Customer Portal**
   - Verify portal allows: plan changes, payment method updates, cancellation
   - Check portal URL generation: `stripe.billingPortal.sessions.create()`
   - Ensure return_url goes to app

5. **Refund Handling**
   - 7-day money-back guarantee implementation
   - Check refund API usage
   - Verify license revocation on refund
   - Pattern: `stripe.refunds.create({ charge: chargeId })`

6. **Testing Scenarios**
   - Test mode credit cards: 4242 4242 4242 4242 (success)
   - Simulate failures: 4000 0000 0000 0002 (card_declined)
   - Verify test webhooks work
   - Check Stripe CLI for local testing

7. **Security Best Practices**
   - Never log full card numbers or Stripe secret keys
   - Verify PCI compliance (using Stripe Elements)
   - Check webhook signature validation
   - Ensure HTTPS in production

8. **Output**
   - "✅ Stripe Integration: {events} events handled, signature verified, idempotency implemented"
   - "❌ CRITICAL: Missing handler for customer.subscription.deleted"
   - "💡 Recommendation: Add retry queue for failed webhook processing"

**Example Use Cases**:
- "Payment succeeded but user still shows free tier" → Check webhook handler
- "How to test subscription cancellation?" → Provide test card and Stripe CLI commands
- "User wants refund" → Guide through refund API and license revocation

## test-coverage-enforcer
**Description**: Ensures adequate test coverage for code changes
**When to use**: After adding new Services, Models, or significant features
**Tools**: Bash, Read, Grep, Write
**Instructions**:
1. **Discover Changes**
   - Run `git diff --name-only` to get modified files
   - Filter to testable code: Services/, Models/, Utilities/
   - Exclude UI files (MainWindow.xaml.cs - harder to unit test)

2. **Map to Test Files**
   - For each changed file, find corresponding test:
     - VoiceLite/Services/AudioRecorder.cs → VoiceLite.Tests/Services/AudioRecorderTests.cs
     - VoiceLite/Models/Settings.cs → VoiceLite.Tests/SettingsTests.cs
   - Flag files without test counterparts

3. **Run Existing Tests**
   - Execute: `dotnet test VoiceLite.Tests --collect:"XPlat Code Coverage"`
   - Parse results: X passed, Y failed, coverage Z%
   - Report per-file coverage

4. **Analyze Coverage Gaps**
   - Read coverage reports in TestResults/
   - Identify uncovered lines in changed files
   - Categorize:
     - CRITICAL: Core logic untested (transcription, recording, license validation)
     - HIGH: Error handling paths untested
     - MEDIUM: Edge cases untested
     - LOW: Simple getters/setters untested

5. **Suggest Test Cases**
   - For new methods, generate test templates:
     ```csharp
     [Fact]
     public void MethodName_WhenCondition_ExpectedBehavior()
     {
         // Arrange
         var service = new ServiceName();

         // Act
         var result = service.MethodName(input);

         // Assert
         result.Should().Be(expected);
     }
     ```

6. **Edge Case Identification**
   - Null inputs
   - Empty strings
   - Boundary values (0, -1, int.MaxValue)
   - Concurrent access (if applicable)
   - Exception scenarios

7. **Coverage Thresholds**
   - Minimum: 75% overall
   - Services: 80% (critical business logic)
   - Models: 70% (mostly data structures)
   - Fail if coverage drops below threshold

8. **Generate Missing Tests**
   - Offer to create test file stubs for untested code
   - Include: namespace, using statements, test class, basic test methods
   - Pattern: Follow xUnit + FluentAssertions style per AGENTS.md

9. **Output**
   - "📊 Coverage: {overall}% ({services}% Services, {models}% Models)"
   - "✅ Coverage Threshold Met: {overall}% >= 75%"
   - "❌ Coverage Too Low: {overall}% < 75% - add {num} tests"
   - "📝 Missing Tests: {list of files without tests}"
   - "💡 Suggested Tests: {generated test cases}"

**Example Use Cases**:
- "Added new AudioPreprocessor service" → Generate test file, suggest test cases
- "Coverage dropped to 72%" → Identify which files need more tests
- "What should I test for Settings.cs?" → Suggest test scenarios

## dependency-upgrade-advisor
**Description**: Manages NuGet and npm dependencies, security vulnerabilities, and upgrade paths
**When to use**: Monthly dependency audits, before major releases, after security advisories
**Tools**: Bash, Read, Grep, WebFetch
**Instructions**:
1. **Scan NuGet Dependencies**
   - Read VoiceLite/VoiceLite/VoiceLite.csproj
   - List all PackageReference entries
   - Check current versions vs latest:
     - NAudio: 2.2.1 → latest?
     - H.InputSimulator: 1.2.1 → latest?
     - System.Text.Json: 9.0.9 → latest?
   - Run: `dotnet list package --outdated`

2. **Scan npm Dependencies**
   - Read voicelite-web/package.json
   - List dependencies and devDependencies
   - Check current vs latest:
     - next: 15.5.4 → latest?
     - react: 19 → latest?
     - stripe: latest?
   - Run: `npm outdated` in voicelite-web/

3. **Security Vulnerability Check**
   - Run: `dotnet list package --vulnerable`
   - Run: `npm audit` in voicelite-web/
   - Categorize by severity: CRITICAL, HIGH, MEDIUM, LOW
   - Check CVE database for known vulnerabilities
   - Prioritize security updates

4. **Compatibility Analysis**
   - .NET 8.0 compatibility for C# packages
   - Next.js 15 compatibility for React packages
   - Check breaking changes in release notes
   - Verify no deprecated APIs used

5. **Upgrade Path Recommendation**
   - Safe upgrades (patch versions): Can upgrade immediately
   - Minor upgrades: Review changelog, test after upgrade
   - Major upgrades: May have breaking changes, careful review needed
   - Example: NAudio 2.2.1 → 2.3.0 (minor) vs 2.2.1 → 3.0.0 (major)

6. **Test After Upgrade**
   - Run full test suite: `dotnet test VoiceLite.Tests`
   - Build web app: `npm run build` in voicelite-web/
   - Smoke test critical paths:
     - Recording works
     - Transcription works
     - Stripe checkout works

7. **Rollback Plan**
   - Document current versions before upgrade
   - Create git branch for upgrade testing
   - If tests fail, revert to previous versions
   - Pattern: `git checkout -b dependency-upgrade-{date}`

8. **Generate Upgrade Report**
   - List all packages to upgrade
   - Security vulnerabilities addressed
   - Breaking changes to watch for
   - Recommended testing steps
   - Format as markdown for documentation

9. **Output**
   - "📦 Dependencies: {total} packages ({outdated} outdated, {vulnerable} with vulnerabilities)"
   - "🚨 CRITICAL: {package} has CVE-{id} - upgrade to {version} immediately"
   - "✅ Safe Upgrades: {list of patch upgrades}"
   - "⚠️  Breaking Changes: {package} {oldVer} → {newVer} - review {changelog}"
   - "💡 Recommendation: Upgrade {package} for performance/security"

**Example Use Cases**:
- "Monthly dependency audit" → Scan all packages, report outdated/vulnerable
- "CVE alert for Stripe package" → Check version, recommend upgrade, test
- "App won't build after upgrade" → Identify breaking change, suggest fix or rollback

---

# Documentation & Quality Agents

## claude-md-sync-agent
**Description**: Keeps CLAUDE.md synchronized with actual codebase changes
**When to use**: After architectural changes, new services added, or command changes
**Tools**: Read, Grep, Edit
**Instructions**:
1. **Detect Architecture Changes**
   - Compare Services/ directory with CLAUDE.md "Service Layer" section
   - Look for new services not documented
   - Look for deleted services still documented (compare git log)
   - Pattern: Each service should have a bullet point description

2. **Update Service Listings**
   - If new service found (e.g., NewService.cs):
     - Read service file to understand purpose
     - Add bullet to CLAUDE.md Service Layer section
     - Format: `ServiceName: Brief description of responsibility`
   - If service deleted, remove from docs

3. **Verify Command Examples**
   - Check build commands still work:
     - `dotnet build VoiceLite/VoiceLite.sln`
     - `dotnet test VoiceLite.Tests`
   - Check paths are correct (e.g., .csproj paths)
   - Test installer command path
   - Update if commands changed

4. **Version Number Sync**
   - Read VoiceLite/VoiceLite/VoiceLite.csproj for current version
   - Check CLAUDE.md mentions latest version in changelog
   - Verify version consistency across:
     - CLAUDE.md
     - Installer/VoiceLiteSetup_Simple.iss
     - package.json (if applicable)

5. **Dependencies Documentation**
   - Compare actual NuGet packages vs documented in CLAUDE.md
   - Update versions if packages upgraded
   - Add new dependencies
   - Remove obsolete dependencies

6. **Performance Targets**
   - Verify targets in CLAUDE.md match actual benchmarks
   - Startup < 2s, Transcription < 200ms, RAM < 100MB
   - Update if targets changed based on real measurements

7. **Code Examples Validation**
   - Test code snippets still compile
   - Check file paths in examples are accurate
   - Verify command syntax hasn't changed

8. **Changelog Maintenance**
   - Ensure recent versions documented
   - Check changelog format consistency
   - Verify version numbers match git tags

9. **Output**
   - "✅ CLAUDE.md Synced: {changes} updates made"
   - "📝 Added: {service} service documentation"
   - "🔄 Updated: Version {old} → {new}"
   - "⚠️  Command outdated: {old_command} should be {new_command}"

**Auto-Update**: Can automatically edit CLAUDE.md with user approval

## readme-generator
**Description**: Auto-generates and updates README.md based on codebase state
**When to use**: After major feature additions, before releases, or when README becomes stale
**Tools**: Read, Grep, Write, Bash
**Instructions**:
1. **Generate Feature List**
   - Scan Services/ directory for capabilities
   - Extract features:
     - AudioRecorder → "Real-time audio recording with noise suppression"
     - PersistentWhisperService → "Offline AI transcription with multiple models"
     - HotkeyManager → "Global hotkey support"
     - SystemTrayManager → "System tray integration"
   - Create bullet list in README

2. **Update Installation Instructions**
   - Check latest installer filename pattern
   - Verify download links are current
   - Update system requirements if changed (.NET version, Windows version)
   - Include Visual C++ Runtime requirement

3. **Screenshot Management**
   - Detect if UI changed (compare MainWindow.xaml modification date)
   - If changed, prompt user: "UI updated - consider adding new screenshots"
   - List recommended screenshots:
     - Main window idle state
     - Recording in progress
     - Settings window
     - System tray menu

4. **Changelog Integration**
   - Extract recent versions from CLAUDE.md or git tags
   - Format as markdown changelog in README
   - Link to GitHub releases

5. **Badge Generation**
   - Test coverage badge (from coverage reports)
   - .NET version badge
   - License badge (MIT)
   - Stars/forks badges (GitHub API)
   - Pattern: `![Coverage](https://img.shields.io/badge/coverage-{percent}%25-{color})`

6. **API Documentation Links**
   - List key namespaces and link to docs
   - Services/, Models/, Interfaces/
   - Link to CLAUDE.md for developer guide

7. **Usage Examples**
   - Code examples for common tasks:
     - Basic recording
     - Custom hotkey setup
     - Model selection
   - Keep examples concise and tested

8. **Contributing Section**
   - Link to CONTRIBUTING.md
   - Code style guidelines (from CLAUDE.md)
   - How to run tests
   - How to submit PRs

9. **Output**
   - Generate complete README.md content
   - Preview changes before writing
   - Ask user: "Update README with these changes? (yes/no)"
   - If yes, write file and show diff

**Features**:
- Table of contents auto-generation
- Markdown formatting validation
- Link checking (verify all links work)
- Emoji usage for visual appeal (optional)

## api-docs-generator
**Description**: Generates API documentation for voicelite-web API routes
**When to use**: After adding/modifying API routes, before API launches
**Tools**: Read, Glob, Write
**Instructions**:
1. **Discover API Routes**
   - Glob: `voicelite-web/app/api/**/route.ts`
   - List all endpoints with HTTP methods (GET, POST, etc.)

2. **Extract Route Information**
   - For each route:
     - Path: `/api/checkout`
     - Method: POST
     - Description: (extract from file comments or infer)
     - Request schema: (parse Zod schema)
     - Response schema: (analyze return statements)
     - Auth required: (check for session validation)
     - Example request/response

3. **Parse Zod Schemas**
   - Extract schema definitions:
     ```typescript
     const bodySchema = z.object({
       plan: z.enum(['quarterly', 'lifetime']),
       successUrl: z.string().url().optional(),
     });
     ```
   - Convert to documentation format:
     ```
     Request Body:
     - plan (string, enum: ['quarterly', 'lifetime']) - Required
     - successUrl (string, URL) - Optional
     ```

4. **Generate OpenAPI/Swagger Spec**
   - Create openapi.yaml or openapi.json
   - Include:
     - API version
     - Server URL
     - Authentication scheme
     - All endpoints with schemas
     - Example values
   - Format: OpenAPI 3.0 specification

5. **Create Markdown Docs**
   - Generate API-REFERENCE.md with:
     - Endpoint list
     - Authentication guide
     - Request/response examples
     - Error codes
     - Rate limiting info (if applicable)

6. **Generate Postman Collection**
   - Create postman_collection.json
   - Include all routes with example requests
   - Pre-configured auth tokens (test mode)
   - Organize by feature (Auth, Licensing, Checkout, etc.)

7. **Code Examples**
   - Generate curl examples:
     ```bash
     curl -X POST https://voicelite.app/api/checkout \
       -H "Content-Type: application/json" \
       -d '{"plan": "quarterly"}'
     ```
   - TypeScript/JavaScript examples for client integration

8. **Validate Documentation**
   - Check all routes documented
   - Verify examples match actual schemas
   - Test example requests return expected responses
   - Flag any undocumented routes

9. **Output**
   - Generate files:
     - `voicelite-web/API-REFERENCE.md`
     - `voicelite-web/openapi.yaml`
     - `voicelite-web/postman_collection.json`
   - Report: "📚 API Docs: {routes} endpoints documented, {examples} examples generated"

**Auto-Update**: Regenerate docs when routes change (detect via git diff)

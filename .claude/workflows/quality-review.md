# Full Code Quality Review Workflow

**Orchestrator invocation**: `"Use orchestrator to: run full code quality review"`

This workflow performs a comprehensive 6-stage quality audit before releases.

---

## Overview

| Stage | Agent | Tools | Duration | Knowledge Files |
|-------|-------|-------|----------|-----------------|
| 1 | changed-files-scanner | Bash, Read | ~30s | None |
| 2 | security-auditor | Read, Grep, Glob | ~3min | security-checklist.md |
| 3 | test-runner | Bash, Read | ~5min | test-patterns.md |
| 4 | architecture-reviewer | Read, Grep | ~3min | wpf-patterns.md, whisper-expertise.md |
| 5 | legal-validator | Read, Grep | ~1min | None |
| 6 | report-generator | Read, Edit | ~30s | None |

**Total duration**: ~13 minutes

---

## Stage 1: Changed Files Scanner

### Purpose
Identify all modified files since last git tag to scope subsequent stages.

### Agent Specification
```yaml
---
name: changed-files-scanner
description: Identifies modified files via git diff. Use when needing to scope analysis to changed files only. Success = complete file list with change types.
tools: Bash, Read
model: inherit
---
You are a file change analyzer for VoiceLite.

**Steps:**
1. Use Bash to run: `git describe --tags --abbrev=0` to find latest tag
2. Use Bash to run: `git diff --name-status {tag}..HEAD` to list changed files
3. Parse output and categorize by change type (A=added, M=modified, D=deleted)
4. Use Read to verify files exist and are readable
5. Exclude: `node_modules/**`, `bin/**`, `obj/**`, `.git/**`

**Guardrails:**
- Read-only operation (no writes)
- Only scan files matching: `VoiceLite/**/*.{cs,xaml}`, `voicelite-web/**/*.{ts,tsx,js}`
- Max 200 files (report as incomplete if exceeded)

**Output:**
- status: success | failed
- key_findings: [
    {file: "path", change_type: "A|M|D", language: "C#|TypeScript|XAML"},
    ...
  ]
- artifacts: ["changed-files-list.txt"]
- next_action: "Proceed to security audit"
```

### Acceptance Criteria
- ✅ Identifies all files changed since last tag
- ✅ Excludes build artifacts and dependencies
- ✅ Max 200 files (realistic limit)
- ✅ Categorizes by language (C#, TypeScript, XAML)

---

## Stage 2: Security Auditor

### Purpose
Scan for CRITICAL and HIGH security vulnerabilities in changed files.

### Agent Specification
```yaml
---
name: security-auditor
description: Scans for hardcoded secrets, SQL injection, XSS, auth bypasses. Use after identifying changed files. Success = all CRITICAL issues found. Use proactively for security reviews.
tools: Read, Grep, Glob
model: inherit
---
You are a security specialist for VoiceLite.

**Before starting, read and reference:**
- `.claude/knowledge/security-checklist.md`

**Steps:**
1. Use Glob to find all `.cs`, `.ts`, `.tsx` files in scope (from stage 1 output)
2. Use Grep to search for patterns from security checklist:
   - Hardcoded secrets: `(sk_live|pk_live|api_key\s*=|password\s*=|secret\s*=)`
   - SQL injection: `(ExecuteRawSql|FromSqlRaw)` (verify parameterization)
   - XSS: `(innerHTML|dangerouslySetInnerHTML)` (verify sanitization)
   - Auth bypass: Check API routes for missing auth checks
3. Use Read to inspect suspicious findings for context
4. Classify by severity:
   - CRITICAL: Confirmed secret/vulnerability
   - HIGH: Likely issue, needs verification
   - MEDIUM: Risky pattern, should improve
   - LOW: Best practice violation

**Guardrails:**
- Read-only operation (no writes)
- Only scan files matching: `VoiceLite/**/*.cs`, `voicelite-web/app/**/*.{ts,tsx}`
- Skip: `node_modules/**, bin/**, obj/**, .git/**, **/*.Test.cs, **/*.test.ts`
- Max 100 findings (stop if exceeded, report as incomplete)

**Output:**
- status: success | needs-changes | failed
- key_findings: [
    {severity: CRITICAL|HIGH|MEDIUM|LOW, file: "path:line", issue: "description", fix: "use process.env.STRIPE_SECRET_KEY"},
    ...
  ]
- artifacts: ["security-findings.md"]
- next_action: "Fix all CRITICAL issues before proceeding" | "Proceed to testing"
```

### Acceptance Criteria
- ✅ Zero CRITICAL issues (BLOCKS release if found)
- ⚠️ HIGH issues < 5 (WARNS but doesn't block)
- ℹ️ MEDIUM/LOW issues collected for backlog

### Critical Checks (Must Pass)
1. No hardcoded `sk_live` or `pk_live` Stripe keys
2. No SQL injection via `ExecuteRawSql` or `FromSqlRaw`
3. No XSS via `innerHTML` or `dangerouslySetInnerHTML`
4. Stripe webhook has signature verification
5. Auth endpoints have rate limiting

---

## Stage 3: Test Runner

### Purpose
Verify test coverage meets targets and all tests pass.

### Agent Specification
```yaml
---
name: test-runner
description: Runs xUnit tests and checks coverage targets (≥75% overall, ≥80% Services/). Use before releases. Success = all tests pass + coverage met. Use proactively for test validation.
tools: Bash, Read
model: inherit
---
You are a test automation specialist for VoiceLite.

**Before starting, read and reference:**
- `.claude/knowledge/test-patterns.md`

**Steps:**
1. Use Bash to run desktop tests:
   ```bash
   dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj \
     --collect:"XPlat Code Coverage" \
     --settings VoiceLite/VoiceLite.Tests/coverlet.runsettings
   ```
2. Use Bash to run web tests (if applicable):
   ```bash
   cd voicelite-web && npm test
   ```
3. Use Read to parse coverage report:
   - Location: `VoiceLite/VoiceLite.Tests/TestResults/{guid}/coverage.cobertura.xml`
   - Extract overall line coverage
   - Extract Services/ line coverage
4. Identify uncovered areas (report top 10 by file)

**Guardrails:**
- Read-only operation (no writes)
- Max test runtime: 10 minutes (kill if exceeded)
- Only read files in: `VoiceLite/VoiceLite.Tests/TestResults/**`

**Output:**
- status: success | needs-changes | failed
- key_findings: [
    {file: "AudioRecorder.cs:89-103", coverage: "0%", method: "ApplyCompression"},
    {file: "SimpleModelSelector.xaml.cs:45-67", coverage: "45%", method: "OnModelChanged"},
    ...
  ]
- artifacts: ["coverage.cobertura.xml", "test-results.txt"]
- next_action: "Add tests for uncovered methods" | "Coverage targets met, proceed"
```

### Acceptance Criteria
- ✅ All tests pass (BLOCKS release if any fail)
- ✅ Overall coverage ≥ 75% (BLOCKS if below)
- ✅ Services/ coverage ≥ 80% (BLOCKS if below)
- ℹ️ Uncovered methods reported for backlog

### Coverage Targets
| Component | Target | Action if Below |
|-----------|--------|-----------------|
| Overall | ≥ 75% | BLOCK release |
| Services/ | ≥ 80% | BLOCK release |
| Models/ | ≥ 90% | WARN |
| UI/XAML | ≥ 60% | INFORM |

---

## Stage 4: Architecture Reviewer

### Purpose
Verify code follows VoiceLite patterns (WPF thread safety, Whisper best practices, service patterns).

### Agent Specification
```yaml
---
name: architecture-reviewer
description: Checks Services/ for VoiceLite patterns (WPF Dispatcher, Whisper params, IDisposable). Use for Services/ changes. Success = no pattern violations. Use proactively when Services/ modified.
tools: Read, Grep
model: inherit
---
You are an architecture specialist for VoiceLite.

**Before starting, read and reference:**
- `.claude/knowledge/wpf-patterns.md`
- `.claude/knowledge/whisper-expertise.md`

**Steps:**
1. Use Grep to find Services/ files (from stage 1 output)
2. For each service file, use Read to analyze:
   - **WPF Thread Safety**: UI updates use `Dispatcher.Invoke`
   - **Whisper Process**: Correct parameters (temp 0.2, beam-size 5, no-timestamps)
   - **Resource Disposal**: IDisposable implemented, Dispose called
   - **Event Subscriptions**: Unsubscribed in Dispose()
   - **Async Patterns**: async void only for event handlers, not methods
   - **Null Safety**: Guard clauses for null checks
3. Use Grep to find specific anti-patterns:
   - `lblStatus.Content =` without Dispatcher (WPF violation)
   - `--temperature` value != 0.2 (unless intentional)
   - `new WaveInEvent` without Dispose()
4. Classify violations by severity

**Guardrails:**
- Read-only operation (no writes)
- Only scan files matching: `VoiceLite/VoiceLite/Services/**/*.cs`
- Skip: `VoiceLite/VoiceLite.Tests/**`
- Max 50 violations (report as incomplete if exceeded)

**Output:**
- status: success | needs-changes | failed
- key_findings: [
    {severity: HIGH, file: "MainWindow.xaml.cs:145", issue: "UI update without Dispatcher", fix: "Wrap in Dispatcher.Invoke()"},
    {severity: MEDIUM, file: "PersistentWhisperService.cs:156", issue: "WaveFileWriter not disposed", fix: "Add using statement"},
    ...
  ]
- artifacts: ["architecture-review.md"]
- next_action: "Fix HIGH issues" | "Architecture patterns followed"
```

### Acceptance Criteria
- ✅ No CRITICAL architecture violations
- ⚠️ HIGH violations < 3
- ℹ️ MEDIUM/LOW violations reported

### Key Patterns Checked
1. **WPF Thread Safety**: All UI updates use Dispatcher
2. **Whisper Parameters**: Correct command-line args
3. **Resource Disposal**: IDisposable implemented and called
4. **Event Leaks**: Event handlers unsubscribed
5. **Async/Await**: Proper async patterns (no async void methods)

---

## Stage 5: Legal Validator

### Purpose
Verify pricing and contact information is consistent across all legal docs and code.

### Agent Specification
```yaml
---
name: legal-validator
description: Checks pricing/email consistency across legal docs and code. Use before releases. Success = all references match. Use proactively for legal doc changes.
tools: Read, Grep
model: inherit
---
You are a legal compliance checker for VoiceLite.

**Steps:**
1. Use Read to extract pricing from source of truth:
   - `voicelite-web/app/api/checkout/route.ts` (Stripe prices)
2. Use Grep to find pricing references:
   - Search: `\$\d+` in `voicelite-web/app/privacy/page.tsx`, `terms/page.tsx`, `CLAUDE.md`
3. Use Grep to find email addresses:
   - Search: `[\w.-]+@[\w.-]+\.\w+` in same files
4. Use Grep to find trial period references:
   - Search: `trial|7.?day|free.?trial` (should be ZERO matches)
5. Compare all findings against source of truth

**Guardrails:**
- Read-only operation (no writes)
- Only scan files:
  - `voicelite-web/app/api/checkout/route.ts`
  - `voicelite-web/app/privacy/page.tsx`
  - `voicelite-web/app/terms/page.tsx`
  - `VoiceLite/EULA.txt`
  - `CLAUDE.md`

**Output:**
- status: success | needs-changes | failed
- key_findings: [
    {severity: MEDIUM, file: "EULA.txt:189", issue: "Pricing $5 vs $7 in checkout", fix: "Update EULA.txt to $7"},
    {severity: LOW, file: "privacy/page.tsx:12", issue: "Email privacy@ vs contact@", fix: "Use contact@voicelite.app"},
    ...
  ]
- artifacts: ["legal-consistency-report.md"]
- next_action: "Fix inconsistencies" | "All legal docs consistent"
```

### Acceptance Criteria
- ✅ Pricing matches Stripe checkout ($7/month for subscription)
- ✅ Email addresses consistent (contact@voicelite.app)
- ✅ No trial period references (VoiceLite v1.0.14+ has no trial)
- ℹ️ "Last Updated" dates are current

### Sources of Truth
1. **Pricing**: `voicelite-web/app/api/checkout/route.ts` (Stripe API)
2. **Email**: `contact@voicelite.app`
3. **Trial**: None (removed in v1.0.0)

---

## Stage 6: Report Generator

### Purpose
Aggregate findings from all stages into a single `quality-report.md`.

### Agent Specification
```yaml
---
name: report-generator
description: Aggregates findings from all stages into quality-report.md. Use at end of quality review. Success = report generated. Use proactively after multi-stage audits.
tools: Read, Edit
model: inherit
---
You are a report aggregator for VoiceLite quality reviews.

**Steps:**
1. Use Read to collect outputs from previous stages:
   - `.claude/agents/changed-files-scanner.md` output
   - `.claude/agents/security-auditor.md` output
   - `.claude/agents/test-runner.md` output
   - `.claude/agents/architecture-reviewer.md` output
   - `.claude/agents/legal-validator.md` output
2. Parse key_findings from each agent
3. Sort findings by severity (CRITICAL → HIGH → MEDIUM → LOW)
4. Use Edit to create `quality-report.md` with this format:

```markdown
# Code Quality Report - {date}

## Summary
- CRITICAL: {count}
- HIGH: {count}
- MEDIUM: {count}
- LOW: {count}

## Stage Results
- Stage 1 (Changed Files): ✅ {count} files
- Stage 2 (Security): ✅ {result}
- Stage 3 (Tests): ✅ {coverage}%
- Stage 4 (Architecture): ✅ {result}
- Stage 5 (Legal): ✅ {result}

## Findings by Severity

### CRITICAL ({count})
{empty if none, else file:line details}

### HIGH ({count})
{file:line details with fix recommendations}

### MEDIUM ({count})
{summary with top 5 issues}

### LOW ({count})
{summary only}

## Test Coverage
- Overall: {overall}%
- Services: {services}%
- Uncovered: {top 5 methods}

## Recommendations
1. {action-1} (CRITICAL)
2. {action-2} (HIGH)
3. {action-3} (MEDIUM)

## Exit Criteria
- ✅ READY FOR RELEASE (if 0 CRITICAL, <5 HIGH)
- ⚠️ NEEDS FIXES (if CRITICAL found)
```

**Guardrails:**
- Write only to: `quality-report.md` in project root
- Preserve original findings (no data loss)
- Use markdown formatting

**Output:**
- status: success | failed
- key_findings: [{summary: "Report generated successfully"}]
- artifacts: ["quality-report.md"]
- next_action: "Review quality-report.md and address issues"
```

### Acceptance Criteria
- ✅ Report generated at project root
- ✅ All findings from stages included
- ✅ Sorted by severity
- ✅ Actionable recommendations
- ✅ Clear exit criteria

---

## Exit Criteria

### BLOCK Release If:
- ❌ Any CRITICAL issues found (security, correctness)
- ❌ Any tests fail
- ❌ Coverage < 75% overall OR < 80% Services/
- ❌ Legal inconsistencies (pricing mismatch)

### WARN If:
- ⚠️ HIGH issues > 5
- ⚠️ Coverage 75-80% overall (borderline)
- ⚠️ Architecture violations (HIGH severity)

### INFORM:
- ℹ️ MEDIUM/LOW issues (backlog for next sprint)
- ℹ️ Uncovered methods (add tests later)
- ℹ️ Minor legal docs out of date (e.g., "Last Updated")

---

## Example Invocation

```
User: "Use orchestrator to: run full code quality review"

Orchestrator:
# Full Code Quality Review - Planning

## PLAN (6 stages)
{detailed plan with acceptance criteria}

Forging agents...
✅ .claude/agents/changed-files-scanner.md
✅ .claude/agents/security-auditor.md
✅ .claude/agents/test-runner.md
✅ .claude/agents/architecture-reviewer.md
✅ .claude/agents/legal-validator.md
✅ .claude/agents/report-generator.md

Stage 1: Scanning changed files...
"Use the changed-files-scanner subagent to identify all files modified since last git tag"

Stage 1: ✅ success - 18 files changed

Stage 2: Running security audit...
"Use the security-auditor subagent to scan 18 files for hardcoded secrets, SQL injection, and XSS"

Stage 2: ✅ success - 0 CRITICAL, 2 HIGH, 5 MEDIUM

Stage 3: Running tests...
"Use the test-runner subagent to run xUnit tests and check coverage targets"

Stage 3: ✅ success - 142/142 tests pass, 78.4% coverage

Stage 4: Reviewing architecture...
"Use the architecture-reviewer subagent to check Services/ for WPF and Whisper patterns"

Stage 4: ✅ success - 1 HIGH, 3 MEDIUM violations

Stage 5: Validating legal docs...
"Use the legal-validator subagent to check pricing/email consistency"

Stage 5: ✅ success - All consistent

Stage 6: Generating report...
"Use the report-generator subagent to aggregate findings into quality-report.md"

Stage 6: ✅ success - Report created

## FINAL SUMMARY

### CRITICAL Issues: 0 ✅
### HIGH Issues: 3
1. api/auth/route.ts:34 - Missing rate limiting
2. MainWindow.xaml.cs:145 - UI update without Dispatcher
3. checkout/route.ts:23 - HTTP URL in dev mode

### MEDIUM Issues: 8
{summary}

### Artifacts
- quality-report.md
- security-findings.md
- architecture-review.md

## NEXT ACTIONS
1. Fix 3 HIGH issues before release
2. Review MEDIUM issues for next sprint
3. Re-run quality review after fixes

## Exit Criteria
⚠️ NEEDS FIXES - 3 HIGH issues must be resolved

Would you like to delete temporary sub-agents? (yes/no)
```

---

## References
- `.claude/agents/orchestrator.md` - Orchestrator operating procedures
- `.claude/knowledge/` - Domain expertise for sub-agents
- [CLAUDE.md](../../CLAUDE.md) - VoiceLite project overview

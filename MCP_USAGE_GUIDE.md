# MCP Usage Guide for VoiceLite

This guide shows practical examples of using each MCP in your VoiceLite development workflow.

---

## **1. GitHub MCP** 🐙

**What it does**: Interact with GitHub API (issues, PRs, releases, CI/CD)

### Example Use Cases

#### Check CI/CD Status
```
"Show me the status of the latest GitHub Actions run"
"List recent failed workflow runs"
"Get logs for the release workflow"
```

#### Manage Releases
```
"What's the latest release version?"
"Show me release notes for v1.0.62"
"List all releases from the last 3 months"
```

#### Issue Management
```
"Create an issue: 'Add support for custom wake words'"
"List open issues with label 'bug'"
"Show issues created in the last week"
```

#### Pull Requests
```
"Show open pull requests"
"Get PR diff for PR #42"
"Check PR review status"
```

**When to use**: Release management, CI/CD debugging, issue tracking

---

## **2. Stripe MCP** 💳

**What it does**: Interact with Stripe API (payments, subscriptions, webhooks)

### Example Use Cases

#### Payment Debugging
```
"Show recent failed payments"
"Get details for payment intent pi_xxx"
"List charges from the last 24 hours"
```

#### Subscription Management
```
"Show active Pro subscriptions"
"List subscriptions that will renew this week"
"Get customer subscription history for cus_xxx"
```

#### Webhook Monitoring
```
"Show recent webhook events"
"Get webhook event evt_xxx details"
"List failed webhook deliveries"
```

#### Analytics
```
"Show revenue for the last 30 days"
"Count active Pro subscribers"
"List customers who churned this month"
```

**When to use**: Payment debugging, subscription analytics, webhook troubleshooting

---

## **3. ast-grep MCP** 🔍

**What it does**: Semantic code search using AST patterns (see [AST_GREP_EXAMPLES.md](./AST_GREP_EXAMPLES.md))

### Example Use Cases

#### Find Static Singletons
```
"Use ast-grep to find all ApiClient.Client usages"
"Use ast-grep to find all ErrorLogger static calls"
"Use ast-grep to find all PersistentWhisperService references"
```

#### Map Service Dependencies
```
"Use ast-grep to find all service instantiations in MainWindow"
"Use ast-grep to find all coordinator method calls"
"Use ast-grep to map recording workflow dependencies"
```

#### Analyze State Machine
```
"Use ast-grep to find all TransitionTo calls"
"Use ast-grep to find all state assignments"
"Use ast-grep to find all state checks in RecordingStateMachine"
```

#### Find UI Thread Violations
```
"Use ast-grep to find all Dispatcher.Invoke calls"
"Use ast-grep to find all Dispatcher.BeginInvoke patterns"
"Use ast-grep to find Application.Current.Dispatcher usages"
```

**When to use**: Architecture audit, refactoring safety, dependency mapping

**See**: [AST_GREP_EXAMPLES.md](./AST_GREP_EXAMPLES.md) for 10+ detailed examples

---

## **4. Supabase MCP** 🗄️

**What it does**: Database management for PostgreSQL (Prisma schema, migrations, queries)

### Example Use Cases

#### Schema Management
```
"Show me the current Prisma schema"
"Create a migration to add a 'device_fingerprint' column to licenses table"
"Generate TypeScript types from current schema"
"Show all tables in the analytics database"
```

#### Database Queries
```
"Query analytics events from the last 30 days"
"Count transcriptions by model type"
"Show top 10 users by transcription count"
"Find all Pro licenses expiring in the next 7 days"
```

#### Migrations
```
"Create migration to add indexes on analytics.created_at"
"Generate migration for new 'metrics' table"
"Show pending migrations"
"Apply migrations to development database"
```

#### Data Analysis
```
"Calculate average transcription length by model"
"Show analytics opt-in rate by day"
"Find users with >1000 transcriptions"
"Analyze error rates by service"
```

**When to use**: Database schema changes, analytics queries, migration management

**Security**: Use read-only mode for production queries

---

## **5. Vibe Check MCP** 🧠

**What it does**: AI meta-mentor that prevents tunnel-vision and over-engineering

### Example Use Cases

#### Refactoring Sanity Checks
```
"Vibe check: Should I extract SettingsManager now or after WindowManager?"
"Vibe check: Is creating 5 new coordinators over-engineering this refactoring?"
"Vibe check: Should I add dependency injection now or extract services first?"
```

#### Decision Validation
```
"Vibe check: Is this service interface design too complex?"
"Vibe check: Should I keep this feature flag or delete old code?"
"Vibe check: Is this the right time to refactor the state machine?"
```

#### Architecture Review
```
"Vibe check: Am I creating too many abstractions in this refactoring?"
"Vibe check: Should I split MainWindow into 3 files or extract to services?"
"Vibe check: Is this dependency chain getting too deep?"
```

#### Risk Assessment
```
"Vibe check: Is extracting AudioCoordinator safe with current test coverage?"
"Vibe check: Should I refactor now or write more tests first?"
"Vibe check: Am I introducing new bugs with this change?"
```

**When to use**: Complex refactoring, architecture decisions, preventing over-engineering

**Research shows**: 2x success improvement (27% → 54%), 50% reduction in harmful actions

---

## **6. Semgrep MCP** 🔒

**What it does**: Security vulnerability scanning (5,000+ rules)

### Example Use Cases

#### Security Scanning
```
"Use Semgrep to scan all API routes for vulnerabilities"
"Use Semgrep to check for SQL injection in database queries"
"Use Semgrep to find hardcoded secrets in the codebase"
"Use Semgrep to scan Stripe integration for security issues"
```

#### Code Quality
```
"Use Semgrep to find potential XSS vulnerabilities"
"Use Semgrep to detect authentication bypass patterns"
"Use Semgrep to check for insecure deserialization"
"Use Semgrep to find unsafe file operations"
```

#### License Validation Security
```
"Use Semgrep to audit license validation logic"
"Use Semgrep to check Ed25519 signature verification"
"Use Semgrep to find timing attack vulnerabilities"
"Use Semgrep to scan CRL implementation"
```

#### AI-Generated Code Validation
```
"Use Semgrep to validate this refactored code for security issues"
"Use Semgrep to check this new service for vulnerabilities"
"Use Semgrep to scan AI-generated code before committing"
```

**When to use**: Pre-commit security checks, API route audits, payment security validation

**Free tier**: 5,000+ rules, unlimited scans

---

## **Combined Workflow Examples**

### Example 1: Architecture Audit
```
1. "Use ast-grep to find all static singletons"
   → Identifies ApiClient.Client, ErrorLogger, PersistentWhisperService

2. "Vibe check: Should I refactor all 3 singletons at once or one at a time?"
   → Suggests starting with ErrorLogger (lowest risk)

3. "Use Semgrep to scan MainWindow.xaml.cs for code smells"
   → Finds complexity hotspots, coupling issues

4. "Use Supabase to query analytics: which services crash most often?"
   → Identifies high-risk services to refactor carefully
```

### Example 2: Database Migration
```
1. "Use Supabase to show current schema for 'licenses' table"
   → Reviews current structure

2. "Vibe check: Should I add indexes before or after adding new columns?"
   → Suggests order to minimize downtime

3. "Use Supabase to create migration for new 'grace_period_days' column"
   → Generates Prisma migration

4. "Use Semgrep to scan the new migration for SQL injection risks"
   → Validates migration is safe
```

### Example 3: Payment Security Review
```
1. "Use Semgrep to scan voicelite-web/app/api/checkout/ for vulnerabilities"
   → Finds potential issues

2. "Use Stripe MCP to show recent failed webhook events"
   → Identifies payment flow issues

3. "Vibe check: Should I refactor webhook handling or add more error logging first?"
   → Suggests prioritization

4. "Use GitHub MCP to create issue for webhook refactoring"
   → Tracks work
```

### Example 4: Refactoring Safety Check
```
1. "Use ast-grep to find all usages of SettingsManager"
   → Lists 47 usages across 12 files

2. "Vibe check: Can I safely extract SettingsManager with 47 usages?"
   → Suggests incremental approach

3. "Use Semgrep to check if extraction introduces security issues"
   → Validates no new vulnerabilities

4. "Use Supabase to query: how many users hit settings-related errors?"
   → Assesses risk of breaking settings
```

---

## **Environment Setup**

### Required API Keys

Create `.env` file (if doesn't exist) with:

```bash
# Supabase (for database operations)
SUPABASE_URL=your_supabase_project_url
SUPABASE_SERVICE_KEY=your_service_role_key

# Vibe Check (optional - uses free tier by default)
# No API key required - multi-provider routing

# Semgrep (optional - for AppSec Platform features)
SEMGREP_APP_TOKEN=your_semgrep_token  # Optional

# GitHub (likely already configured via GitHub Copilot)
# No additional setup needed

# Stripe (likely already configured)
# No additional setup needed for MCP
```

### Getting API Keys

**Supabase**:
1. Go to https://supabase.com/dashboard
2. Select your project
3. Settings → API → Copy Project URL and service_role key
4. ⚠️ **Use development project only** - never connect to production

**Semgrep** (optional):
1. Go to https://semgrep.dev/
2. Sign up for free account
3. Generate API token in Settings

**Vibe Check**: No API key needed (uses free tier)

---

## **Security Best Practices**

### Supabase MCP
- ⚠️ **NEVER connect to production database**
- Use read-only mode for any real data queries
- Scope to specific project only
- Store credentials in `.env` (never commit to git)

### Semgrep MCP
- Review scan results before auto-fixing
- Use in CI/CD pipeline for all PRs
- Don't ignore critical/high severity findings

### Vibe Check MCP
- No security concerns - runs locally
- No API keys required
- No data sent to external servers

---

## **Troubleshooting**

### Supabase MCP not connecting
```bash
# Verify package is installed
npm list -g @supabase/mcp-server-supabase

# Check environment variables
echo $SUPABASE_URL
echo $SUPABASE_SERVICE_KEY

# Test connection manually
npx @supabase/mcp-server-supabase --help
```

### Vibe Check not responding
```bash
# Verify package is installed
npm list -g vibe-check-mcp

# Check if server starts
npx vibe-check-mcp --stdio
```

### Semgrep scan fails
```bash
# Check hosted server status
curl https://mcp.semgrep.ai/health

# Verify network connectivity
ping mcp.semgrep.ai
```

### ast-grep returns no results
```bash
# Verify package is installed
npm list -g @notprolands/ast-grep-mcp

# Test pattern locally
npx @ast-grep/cli --pattern 'ApiClient.$_' VoiceLite/
```

---

## **Next Steps**

1. **Restart Claude Code** to activate new MCPs
   - Press `Ctrl+Shift+P` → "Developer: Reload Window"

2. **Verify MCPs are active**
   - Try: "Use Vibe Check to validate this refactoring plan"
   - Try: "Use Supabase to show schema for licenses table"
   - Try: "Use Semgrep to scan API routes"

3. **Start architecture audit**
   - Use improved architecture audit prompt
   - Leverage ast-grep for dependency mapping
   - Use Vibe Check for refactoring decisions
   - Use Semgrep for security validation

4. **Set up environment variables**
   - Create `.env` with Supabase credentials
   - Use development database only
   - Never commit `.env` to git

---

## **Summary**

You now have **6 powerful MCPs** configured:

| MCP | Purpose | When to Use |
|-----|---------|-------------|
| **github** | CI/CD, releases, issues | Release management, workflow debugging |
| **stripe** | Payments, subscriptions | Payment debugging, analytics |
| **ast-grep** | Semantic code search | Architecture audit, refactoring |
| **supabase** | Database management | Schema changes, analytics queries |
| **vibe-check** | AI meta-mentor | Refactoring decisions, preventing over-engineering |
| **semgrep** | Security scanning | Pre-commit checks, API security |

**All MCPs work together** to support your architecture audit and refactoring workflow! 🚀

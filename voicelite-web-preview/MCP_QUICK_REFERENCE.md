# MCP Quick Reference Card

Fast lookup for all 6 configured MCPs in VoiceLite.

---

## **🔍 ast-grep** - Semantic Code Search

**What**: Find code patterns using AST (not just text)

**Common Commands**:
```
"Use ast-grep to find all ApiClient.Client usages"
"Use ast-grep to map service dependencies in MainWindow"
"Use ast-grep to find all state transitions"
```

**See**: [AST_GREP_EXAMPLES.md](./AST_GREP_EXAMPLES.md)

---

## **🗄️ Supabase** - Database Management

**What**: PostgreSQL/Prisma schema, migrations, queries

**Common Commands**:
```
"Use Supabase to show schema for licenses table"
"Use Supabase to query analytics events from last 30 days"
"Use Supabase to create migration for new column"
```

**Setup**: Requires `SUPABASE_URL` and `SUPABASE_SERVICE_KEY` in `.env`

---

## **🧠 Vibe Check** - AI Meta-Mentor

**What**: Prevents over-engineering, validates refactoring decisions

**Common Commands**:
```
"Vibe check: Should I extract SettingsManager now?"
"Vibe check: Is creating 5 coordinators over-engineering?"
"Vibe check: Is this refactoring safe with current test coverage?"
```

**Research**: 2x success rate (27% → 54%), 50% fewer harmful actions

---

## **🔒 Semgrep** - Security Scanning

**What**: Find vulnerabilities (5,000+ rules)

**Common Commands**:
```
"Use Semgrep to scan API routes for vulnerabilities"
"Use Semgrep to find hardcoded secrets"
"Use Semgrep to check Stripe integration security"
```

**Free tier**: Unlimited scans, 5,000+ rules

---

## **🐙 GitHub** - CI/CD & Releases

**What**: GitHub API integration

**Common Commands**:
```
"Show latest GitHub Actions run status"
"List open issues with label 'bug'"
"Create issue: 'Add custom wake words support'"
```

---

## **💳 Stripe** - Payments & Subscriptions

**What**: Stripe API integration

**Common Commands**:
```
"Show recent failed payments"
"List active Pro subscriptions"
"Get webhook event details for evt_xxx"
```

---

## **Combined Workflows**

### Architecture Audit
1. `"Use ast-grep to find all static singletons"`
2. `"Vibe check: Should I refactor all 3 at once?"`
3. `"Use Semgrep to scan MainWindow for code smells"`

### Database Migration
1. `"Use Supabase to show current schema"`
2. `"Use Supabase to create migration for new column"`
3. `"Use Semgrep to scan migration for SQL injection"`

### Security Review
1. `"Use Semgrep to scan API routes"`
2. `"Use Stripe to show failed webhook events"`
3. `"Vibe check: Should I refactor webhooks or add logging first?"`

---

## **Installation Status**

✅ All 6 MCPs installed and configured

**Restart Required**: Press `Ctrl+Shift+P` → "Developer: Reload Window"

---

## **Full Documentation**

- **Complete Guide**: [MCP_USAGE_GUIDE.md](./MCP_USAGE_GUIDE.md)
- **Configuration**: [MCP_CONFIGURATION_SUMMARY.md](./MCP_CONFIGURATION_SUMMARY.md)
- **ast-grep Examples**: [AST_GREP_EXAMPLES.md](./AST_GREP_EXAMPLES.md)

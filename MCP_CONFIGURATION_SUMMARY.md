# MCP Configuration Summary

## Changes Made

### ❌ Removed
- **`filesystem` MCP** - Redundant with built-in Read/Write/Edit tools

### ✅ Kept
- **`github` MCP** - Essential for CI/CD workflow (create PRs, issues, view releases)
- **`stripe` MCP** - Essential for payment backend (checkout, subscriptions, webhooks)

### ➕ Added
- **`ast-grep` MCP** - Semantic code search for architecture audit
- **`supabase` MCP** - Database management for PostgreSQL/Prisma backend
- **`vibe-check` MCP** - AI meta-mentor to prevent over-engineering
- **`semgrep` MCP** - Security vulnerability scanning

---

## Current MCP Configuration

**File**: `.claude/mcp.json`

```json
{
  "mcpServers": {
    "github": {
      "url": "https://api.githubcopilot.com/mcp/"
    },
    "stripe": {
      "url": "https://mcp.stripe.com"
    },
    "ast-grep": {
      "command": "npx",
      "args": ["-y", "@notprolands/ast-grep-mcp"]
    },
    "supabase": {
      "command": "npx",
      "args": ["-y", "@supabase/mcp-server-supabase"]
    },
    "vibe-check": {
      "command": "npx",
      "args": ["-y", "vibe-check-mcp", "--stdio"]
    },
    "semgrep": {
      "url": "https://mcp.semgrep.ai"
    }
  }
}
```

---

## Active MCPs (Built-in)

These MCPs are already active in your Claude Code instance:

1. **sequential-thinking** - Multi-step reasoning for complex analysis
2. **context7** - Up-to-date .NET/WPF/Next.js documentation
3. **augments** - Framework documentation cache
4. **magic (21st.dev)** - UI component library

---

## Installation Completed

✅ Installed `@notprolands/ast-grep-mcp@1.1.1` (90 packages)
✅ Installed `@supabase/mcp-server-supabase@0.5.5` (97 packages)
✅ Installed `vibe-check-mcp@latest` (1 package)
✅ Connected to hosted `semgrep` MCP at https://mcp.semgrep.ai

---

## Next Steps

1. **Restart Claude Code** to activate all 6 MCPs
   - Press `Ctrl+Shift+P` → "Developer: Reload Window"

2. **Review Usage Guides**
   - [MCP_USAGE_GUIDE.md](./MCP_USAGE_GUIDE.md) - Comprehensive examples for all 6 MCPs
   - [AST_GREP_EXAMPLES.md](./AST_GREP_EXAMPLES.md) - 10+ ast-grep examples

3. **Set up environment variables** (for Supabase)
   - Create `.env` with `SUPABASE_URL` and `SUPABASE_SERVICE_KEY`
   - Use development database only (never production)

4. **Start architecture audit** using the improved prompt

---

## Quick Start Examples

After restarting Claude Code, try:

**ast-grep** (Semantic search):
```
"Use ast-grep to find all static singleton usages in the codebase"
"Use ast-grep to map service dependencies in MainWindow.xaml.cs"
```

**Supabase** (Database management):
```
"Use Supabase to show schema for licenses table"
"Use Supabase to query analytics events from last 30 days"
```

**Vibe Check** (Refactoring mentor):
```
"Vibe check: Should I extract SettingsManager now or after WindowManager?"
"Vibe check: Is creating 5 new coordinators over-engineering?"
```

**Semgrep** (Security scanning):
```
"Use Semgrep to scan API routes for vulnerabilities"
"Use Semgrep to find hardcoded secrets in the codebase"
```

See [MCP_USAGE_GUIDE.md](./MCP_USAGE_GUIDE.md) for complete examples and workflows.

---

## Why These MCPs?

### ✅ github (Keep)
- **Used for**: Creating releases, viewing CI/CD runs, managing issues
- **Value**: You have `.github/workflows/release.yml` for automated releases
- **Frequency**: High (every release, PR check)

### ✅ stripe (Keep)
- **Used for**: Payment processing, subscription management
- **Value**: You have `voicelite-web/` with Stripe checkout integration
- **Frequency**: Medium (payment webhook debugging, subscription checks)

### ✅ ast-grep (Added)
- **Used for**: Architecture audit, dependency mapping, refactoring safety
- **Value**: 2,183-line MainWindow needs systematic analysis
- **Frequency**: High during refactoring phase, low during maintenance

### ✅ supabase (Added)
- **Used for**: Database schema management, migrations, analytics queries
- **Value**: PostgreSQL backend with Prisma ORM needs schema work
- **Frequency**: Medium (schema changes, analytics)

### ✅ vibe-check (Added)
- **Used for**: Refactoring sanity checks, preventing over-engineering
- **Value**: Complex architecture refactoring needs meta-mentor validation
- **Frequency**: High during refactoring, low during maintenance
- **Research**: 2x success improvement (27% → 54%)

### ✅ semgrep (Added)
- **Used for**: Security scanning, vulnerability detection
- **Value**: Payment backend needs security validation
- **Frequency**: High (pre-commit checks, API audits)
- **Rules**: 5,000+ security rules (free tier)

### ❌ filesystem (Removed)
- **Why removed**: Claude Code already has Read/Write/Edit/Glob built-in
- **Conflict risk**: Duplicate tools can cause permission issues
- **No value**: Everything filesystem does is covered by native tools

---

## Architecture Audit Workflow

With 6 MCPs configured, your enhanced workflow is:

1. **Analyze** (ast-grep, semgrep, supabase)
   - Map dependencies with ast-grep
   - Find static singletons with ast-grep
   - Scan for security issues with semgrep
   - Query error metrics with supabase

2. **Plan** (vibe-check, sequential-thinking, context7)
   - Use vibe-check to validate refactoring approach
   - Use sequential-thinking for step-by-step analysis
   - Reference .NET DI patterns from context7
   - Create refactoring roadmap

3. **Execute** (Read, Edit, Write, vibe-check)
   - Extract classes incrementally
   - Vibe check each extraction decision
   - Test after each extraction
   - Use ast-grep to verify all usages updated

4. **Verify** (dotnet test, semgrep, supabase)
   - Run tests after each change
   - Scan with semgrep for security regressions
   - Check error rates with supabase analytics
   - Validate functionality unchanged

5. **Database Changes** (supabase, semgrep)
   - Create migrations with supabase
   - Scan SQL with semgrep
   - Test migrations on dev database
   - Deploy to production

---

## Troubleshooting

### MCPs not showing up?
**Solution**: Restart Claude Code (Ctrl+Shift+P → "Developer: Reload Window")

### ast-grep queries failing?
```bash
npm list -g @notprolands/ast-grep-mcp
```

### Supabase MCP not connecting?
```bash
# Verify installation
npm list -g @supabase/mcp-server-supabase

# Check environment variables
echo $SUPABASE_URL
echo $SUPABASE_SERVICE_KEY
```

### Vibe Check not responding?
```bash
npm list -g vibe-check-mcp
npx vibe-check-mcp --stdio
```

### Semgrep scan fails?
```bash
curl https://mcp.semgrep.ai/health
```

### GitHub MCP not authenticating?
**Solution**: Check GitHub Copilot subscription is active

### Stripe MCP returning 403?
**Solution**: Verify Stripe API keys in environment variables

---

## Additional Resources

- **MCP Usage Guide**: [MCP_USAGE_GUIDE.md](./MCP_USAGE_GUIDE.md) - Complete examples for all 6 MCPs
- **ast-grep Examples**: [AST_GREP_EXAMPLES.md](./AST_GREP_EXAMPLES.md) - 10+ semantic search patterns
- **ast-grep Documentation**: https://ast-grep.github.io/
- **Supabase MCP Docs**: https://supabase.com/docs/guides/getting-started/mcp
- **Vibe Check GitHub**: https://github.com/PV-Bhat/vibe-check-mcp-server
- **Semgrep Docs**: https://semgrep.dev/docs/mcp
- **Architecture Audit Prompt**: See improved prompt in conversation history
- **Context7 Usage**: Ask "Use context7 to get .NET dependency injection best practices"

---

## Summary

**Before**:
- 4 MCPs (filesystem, github, stripe, + built-ins)
- Redundant filesystem MCP conflicting with native tools
- No semantic code search capability
- No database management tools
- No refactoring validation
- No security scanning

**After**:
- 6 MCPs (github, stripe, ast-grep, supabase, vibe-check, semgrep + built-ins)
- Clean configuration with no redundancy
- Semantic code search for architecture audit
- Database management for PostgreSQL/Prisma
- AI meta-mentor to prevent over-engineering
- Security scanning with 5,000+ rules
- Ready for systematic refactoring

**Impact**:
- ✅ Removed conflicts (filesystem)
- ✅ Added semantic search (ast-grep)
- ✅ Added database tools (supabase)
- ✅ Added refactoring mentor (vibe-check)
- ✅ Added security scanning (semgrep)
- ✅ Kept essential integrations (github, stripe)
- ✅ Ready for architecture audit phase with enhanced tooling

**Total MCPs**: 6 custom + 4 built-in = **10 powerful tools** 🚀

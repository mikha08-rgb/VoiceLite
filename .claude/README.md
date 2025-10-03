# VoiceLite Orchestrator System

This directory contains the **orchestrator meta-agent system** for VoiceLite development.

Instead of 20+ static agents, we use a single orchestrator that dynamically forges minimally-scoped sub-agents on demand.

---

## Directory Structure

```
.claude/
├── agents/
│   └── orchestrator.md           # Meta-agent that spawns sub-agents
├── knowledge/
│   ├── whisper-expertise.md      # Whisper AI patterns (models, params, tuning)
│   ├── wpf-patterns.md           # WPF/XAML best practices (thread safety, MVVM, disposal)
│   ├── stripe-integration.md     # Stripe webhooks, security, testing
│   ├── security-checklist.md     # Security audit rules (secrets, injection, XSS)
│   ├── performance-targets.md    # Performance budgets and optimization
│   └── test-patterns.md          # Test generation and coverage standards
├── workflows/
│   └── quality-review.md         # Full code quality review (6 stages)
└── README.md                     # This file
```

---

## How It Works

### Traditional Static Agents (Old System)
```
AGENTS.md defines:
- whisper-model-expert (fixed scope, fixed tools)
- wpf-ui-expert (fixed scope, fixed tools)
- stripe-integration-expert (fixed scope, fixed tools)
- voicelite-security-auditor (fixed scope, fixed tools)
... 20+ more agents
```

**Problems**:
- Large context (1400+ lines)
- Inflexible (can't combine or customize)
- Tool over-provisioning (all agents get same tools)
- Duplication (similar agents for similar tasks)

### Orchestrator System (New)
```
1. User: "Use orchestrator to: run full code quality review"

2. Orchestrator:
   - Reads workflow definition (.claude/workflows/quality-review.md)
   - Plans 6 stages with acceptance criteria
   - Forges 6 sub-agents (.claude/agents/{name}.md)
   - Each agent gets ONLY the tools it needs (Read, Grep, Bash, etc.)
   - Injects domain knowledge (.claude/knowledge/*.md)

3. Orchestrator invokes each sub-agent:
   "Use the security-auditor subagent to scan for secrets"

4. Orchestrator supervises:
   - Retries on failure (max 1 retry)
   - Escalates if still failing

5. Orchestrator summarizes:
   - Aggregates findings by severity
   - Generates quality-report.md
   - Recommends next actions
```

**Benefits**:
- Smaller context (knowledge split into reusable files)
- Flexible (forge agents for any workflow)
- Minimal tool grants (least privilege)
- No duplication (knowledge reused)

---

## Usage

### Basic Invocation
```
"Use orchestrator to: {goal}"
```

### Example: Code Quality Review
```
"Use orchestrator to: run full code quality review"
```

**What happens**:
1. Orchestrator reads `.claude/workflows/quality-review.md`
2. Plans 6 stages: changed-files → security → tests → architecture → legal → report
3. Forges 6 sub-agents with minimal tools and domain knowledge
4. Runs each stage sequentially
5. Generates `quality-report.md` with findings
6. Asks if you want to delete temporary sub-agents

### Example: Security Audit
```
"Use orchestrator to: run security audit on modified files"
```

**What happens**:
1. Plans 3 stages: changed-files → secrets-scanner → vulnerability-scanner
2. Injects `security-checklist.md` knowledge
3. Reports CRITICAL/HIGH/MEDIUM/LOW findings
4. Recommends fixes

### Example: Performance Audit
```
"Use orchestrator to: find performance bottlenecks"
```

**What happens**:
1. Plans 4 stages: metric-collector → bottleneck-analyzer → optimizer → re-measure
2. Injects `performance-targets.md` and `wpf-patterns.md` knowledge
3. Profiles code, identifies slow paths
4. Validates improvements

---

## Knowledge Base

Domain expertise is split into reusable files:

### whisper-expertise.md
- Model selection guide (Tiny → Large)
- Command parameters (temperature, beam-size)
- Audio format requirements (16kHz, 16-bit, mono)
- Process lifecycle best practices
- Common transcription issues

### wpf-patterns.md
- Thread safety (Dispatcher.Invoke)
- MVVM pattern
- Resource disposal (IDisposable)
- Async/await best practices
- Common WPF gotchas

### stripe-integration.md
- Webhook security (signature verification)
- Idempotent event handling
- Checkout session creation
- Subscription lifecycle events
- Testing with Stripe CLI

### security-checklist.md
- Hardcoded secrets detection
- SQL injection patterns
- XSS vulnerabilities
- Authentication bypass checks
- Security scan procedures

### performance-targets.md
- Performance metrics (startup, latency, memory, CPU)
- Optimization techniques (pooling, caching, async)
- Common performance issues
- Profiling methods

### test-patterns.md
- xUnit test structure (AAA pattern)
- Mocking with Moq
- FluentAssertions
- Coverage targets (≥75% overall, ≥80% Services/)
- Test data management

---

## Workflows

Pre-defined multi-stage workflows:

### quality-review.md
**6 stages**: changed-files → security → tests → architecture → legal → report

**Purpose**: Comprehensive pre-release quality gate

**Exit Criteria**:
- BLOCK: CRITICAL issues, test failures, coverage < 75%
- WARN: HIGH issues > 5, coverage 75-80%
- INFORM: MEDIUM/LOW issues

**Invocation**: `"Use orchestrator to: run full code quality review"`

---

## Creating Custom Workflows

### Option 1: Inline Goal (Simple)
```
"Use orchestrator to: scan for memory leaks in Services/ directory"
```

Orchestrator will:
1. Plan stages automatically (e.g., file-scanner → leak-detector → report)
2. Forge agents on-the-fly
3. Execute and summarize

### Option 2: Workflow File (Reusable)
Create `.claude/workflows/my-workflow.md`:

```markdown
# My Custom Workflow

**Orchestrator invocation**: `"Use orchestrator to: run my custom workflow"`

## Stage 1: {Name}
**Agent**: {agent-name}
**Tools**: Read, Grep
**Knowledge**: security-checklist.md
**Acceptance**: {criteria}

## Stage 2: {Name}
...
```

Then invoke:
```
"Use orchestrator to: run my custom workflow"
```

---

## Agent Forging

Orchestrator creates sub-agents using this template:

```yaml
---
name: {kebab-name}
description: {purpose}. Use proactively when {trigger}.
tools: Read, Grep, Glob  # Minimal set only
model: inherit
---
You are a specialist for {scope}.

**Before starting, read and reference:**
- `.claude/knowledge/{relevant}.md`

**Steps:**
1. {exact step}
2. {exact step}

**Guardrails:**
- Only access files matching: {glob}
- Show diffs for changes
- Max {N} findings

**Output:**
- status: success | needs-changes | failed
- key_findings: [{severity, file:line, issue, fix}]
- artifacts: [paths]
- next_action: {recommendation}
```

**Key Points**:
- **Minimal tools**: Only grant what's needed (Read, Grep, Glob, Bash, Edit)
- **Knowledge injection**: Agents read knowledge files explicitly
- **Structured output**: JSON-like format for parsing
- **Guardrails**: Limit file access, show diffs, cap findings

---

## Comparison: Old vs New

### Old System (AGENTS.md)
```markdown
## whisper-model-expert
Description: Whisper AI troubleshooting
Tools: ??? (implicit)
Usage: "Use whisper-model-expert to debug accuracy issues"
Knowledge: Embedded in agent description
```

**Problems**:
- 1400+ lines of agent definitions
- Unclear tool grants
- Knowledge duplication
- Can't customize

### New System (.claude/)
```markdown
## orchestrator + knowledge + workflows
orchestrator.md: Plans, forges, invokes, supervises, summarizes
knowledge/whisper-expertise.md: Reusable Whisper knowledge
workflows/quality-review.md: Pre-defined 6-stage workflow

Usage: "Use orchestrator to: run full code quality review"
```

**Benefits**:
- Smaller context (200-300 lines per file)
- Explicit minimal tool grants
- Knowledge reused across agents
- Workflows composable

---

## Troubleshooting

### Issue: Orchestrator doesn't forge agents
**Cause**: Goal too vague or ambiguous
**Fix**: Be more specific:
```
❌ "Use orchestrator to: improve code"
✅ "Use orchestrator to: run full code quality review"
✅ "Use orchestrator to: scan for hardcoded secrets in modified files"
```

### Issue: Sub-agent fails with "file not found"
**Cause**: File path incorrect or file doesn't exist
**Fix**: Orchestrator should read file first, then retry. If still failing, check file path.

### Issue: Sub-agent output format wrong
**Cause**: Agent template doesn't match expected structure
**Fix**: Orchestrator should regenerate agent with correct template.

### Issue: Workflow takes too long
**Cause**: Too many files, large codebase
**Fix**: Narrow scope with glob patterns (e.g., `VoiceLite/Services/**/*.cs` instead of `**/*.cs`)

---

## Migration from Old System

If you have old AGENTS.md references:

### Before (Old System)
```
"Use whisper-model-expert to debug poor accuracy"
"Run pre-commit-workflow"
"Use ship-to-production-workflow to prepare v1.0.23"
```

### After (New System)
```
"Use orchestrator to: analyze Whisper transcription accuracy issues"
"Use orchestrator to: run full code quality review"  # Includes pre-commit checks
"Use orchestrator to: run full code quality review"  # Replaces ship-to-production
```

### Archived Files
- `AGENTS.md.archive` - Original 20+ static agents (reference only)
- `WORKFLOWS.md.archive` - Original workflow examples (reference only)
- `AGENT-EXAMPLES.md.archive` - Original copy-paste examples (reference only)

---

## Best Practices

### 1. Use Specific Goals
```
✅ "Use orchestrator to: scan for SQL injection in API routes"
❌ "Use orchestrator to: check security"
```

### 2. Reference Workflows
```
✅ "Use orchestrator to: run full code quality review" (defined workflow)
✅ "Use orchestrator to: scan for memory leaks in Services/" (inline goal)
```

### 3. Let Orchestrator Plan
```
✅ "Use orchestrator to: prepare v1.0.23 release"
   (Orchestrator decides stages automatically)
❌ "Create security-auditor agent and run it"
   (Manual agent creation, bypasses orchestrator)
```

### 4. Review Before Deleting
```
After workflow completes:
- Review quality-report.md
- Check forged agents for insights
- THEN delete if you don't need them
```

### 5. Add to Workflows
```
If you run the same multi-stage task repeatedly:
1. Create .claude/workflows/{name}.md
2. Document stages with acceptance criteria
3. Reuse with: "Use orchestrator to: run {name}"
```

---

## Contributing

### Adding Knowledge
Create `.claude/knowledge/{topic}.md`:
```markdown
# {Topic} Expertise for VoiceLite

## {Section 1}
{content with examples}

## {Section 2}
{content with examples}

## References
{links}
```

### Adding Workflows
Create `.claude/workflows/{name}.md`:
```markdown
# {Name} Workflow

**Orchestrator invocation**: `"Use orchestrator to: run {name}"`

## Stage 1: {Name}
**Agent**: {agent-name}
**Tools**: {tools}
**Knowledge**: {knowledge-files}
**Acceptance**: {criteria}

## Stage 2: {Name}
...

## Exit Criteria
{when to block/warn/inform}
```

---

## References

- [orchestrator.md](agents/orchestrator.md) - Meta-agent operating procedures
- [quality-review.md](workflows/quality-review.md) - Full code quality review workflow
- [knowledge/](knowledge/) - Domain expertise files
- [CLAUDE.md](../CLAUDE.md) - VoiceLite project overview

---

**Version**: 1.0.0
**Last Updated**: January 2025
**Maintained By**: VoiceLite Development Team

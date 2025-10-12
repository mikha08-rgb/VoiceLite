# BMAD Quickstart for VoiceLite

**Goal**: Ship production-ready v1.0 in 2 weeks using BMAD Method

---

## Setup Complete ✅

You now have:
- ✅ **PRD**: [docs/prd.md](prd.md) - 6 epics, 2-week timeline
- ✅ **Architecture**: [docs/architecture.md](architecture.md) - Technical overview
- ✅ **BMAD Core**: `.bmad-core/` configured for VoiceLite
- ✅ **217 tests** - Need to measure coverage %

---

## How to Use BMAD with Claude Code

### Step 1: Start a Story (Today!)

**First Story: Measure Test Coverage**

```
/BMad:sm *draft
```

This will create your first story in `docs/stories/`. The Scrum Master (SM) agent will:
1. Look at Epic 2.1 (Service Layer Test Coverage)
2. Create a story for measuring actual coverage %
3. Break it into tasks

### Step 2: Implement the Story

```
/BMad:dev *develop-story {story-file}
```

The Dev agent will:
1. Read the story
2. Execute tasks sequentially
3. Write tests
4. Update the story with progress

### Step 3: QA Review (Optional but Recommended)

```
/BMad:qa *review {story-file}
```

The QA agent will:
1. Check test coverage
2. Review code quality
3. Create quality gate decision
4. Suggest improvements

---

## BMAD Agents Available

### Scrum Master (`/BMad:sm`)
- `*draft` - Create next story from PRD epics
- `*review` - Validate story against PRD

### Developer (`/BMad:dev`)
- `*develop-story {story}` - Implement story end-to-end
- Writes code, tests, updates docs

### QA / Test Architect (`/BMad:qa`)
- `*risk {story}` - Identify risks BEFORE development
- `*design {story}` - Create test strategy
- `*trace {story}` - Verify test coverage during dev
- `*nfr {story}` - Check quality attributes
- `*review {story}` - Full quality assessment
- `*gate {story}` - Update quality gate decision

### Product Owner (`/BMad:po`)
- Validates alignment with PRD
- Manages epics
- Quality gates

### Architect (`/BMad:architect`)
- Design complex features
- Review architecture decisions

---

## Your 2-Week Plan

### Week 1: Testing & Code Quality
**Days 1-2** (Today - Tomorrow):
1. `/BMad:sm *draft` → "Measure Test Coverage"
2. `/BMad:dev *develop-story` → Implement
3. `/BMad:qa *review` → Validate

**Days 3-5**:
- Epic 2.1: Service layer test coverage
- Epic 1.1: Code cleanup (remove BUG-XXX comments)

**Days 6-7**:
- Epic 2.2: UI tests
- Epic 3.1: Error message audit

### Week 2: Polish & Ship
**Days 8-14**:
- Documentation
- Edge case handling
- Final QA
- **SHIP v1.0** 🚀

---

## Quick Tips

### 1. One Story at a Time
Don't try to do everything at once. BMAD works best with focused stories.

### 2. Use QA Early
Run `/BMad:qa *risk` and `/BMad:qa *design` BEFORE coding complex stories.

### 3. Keep Context Clean
BMAD agents load minimal context. If you switch agents, start a new conversation.

### 4. Trust the Process
The agents will guide you through the workflow. Follow the prompts.

### 5. Review Before Committing
Always review agent changes before committing to git.

---

## Common Workflows

### Full Story Workflow
```bash
# 1. Create story
/BMad:sm *draft

# 2. (Optional) Risk assessment
/BMad:qa *risk {story}

# 3. Implement
/BMad:dev *develop-story {story}

# 4. Review
/BMad:qa *review {story}

# 5. Commit
git add .
git commit -m "feat: {story-title}"
```

### Quick Bug Fix
```bash
# 1. Create bug story manually in docs/stories/
# 2. Implement
/BMad:dev *develop-story {story}
# 3. Commit
```

### Just Need Code Review
```bash
/BMad:qa *review {story-file}
```

---

## File Locations

```
docs/
├── prd.md                    # Product requirements
├── architecture.md           # Technical architecture
├── stories/                  # User stories (created by SM)
│   └── epic-2.1-story-1.md  # Example story
├── qa/
│   ├── assessments/         # QA analysis reports
│   └── gates/               # Quality gate decisions
└── BMAD-QUICKSTART.md       # This file

.bmad-core/
├── core-config.yaml         # BMAD configuration
├── agents/                  # BMAD agent definitions
├── tasks/                   # BMAD task templates
└── templates/               # Document templates
```

---

## Getting Unstuck

### "I don't know what story to create"
```
/BMad:sm *draft
```
The SM will look at the PRD and suggest the next logical story.

### "The agent is confused"
Start a new conversation. BMAD agents are stateless.

### "I need to change the PRD"
Edit [docs/prd.md](prd.md) directly. BMAD reads it fresh each time.

### "Tests are failing"
That's normal for vibe-coded projects! Use `/BMad:dev` to fix them.

---

## Next Actions

### Right Now (Day 1):
1. ✅ Review [docs/prd.md](prd.md) - Make sure 2-week plan looks good
2. ✅ Review [docs/architecture.md](architecture.md) - Understand the system
3. **Start first story**: `/BMad:sm *draft`
4. **Implement it**: `/BMad:dev *develop-story {story}`

### Today's Goal:
**Measure actual test coverage %** so we know where we stand.

---

## Resources

- **BMAD User Guide**: `.bmad-core/user-guide.md`
- **BMAD Workflow**: `.bmad-core/enhanced-ide-development-workflow.md`
- **VoiceLite Dev Guide**: `CLAUDE.md`
- **PRD**: `docs/prd.md`
- **Architecture**: `docs/architecture.md`

---

**Ready to ship? Let's do this! 🚀**

Start with: `/BMad:sm *draft`

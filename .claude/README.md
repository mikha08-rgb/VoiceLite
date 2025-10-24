# .claude/ Configuration

This directory contains Claude Code configuration for the VoiceLite project.

## Directory Structure

```
.claude/
├── knowledge/           # Domain expertise files
│   ├── whisper-expertise.md  # Whisper AI model selection, parameters, troubleshooting
│   └── wpf-patterns.md       # WPF/XAML best practices (thread safety, MVVM, disposal)
├── mcp.json            # MCP server configuration (Supabase + GitHub) - GITIGNORED
├── mcp.json.example    # Example MCP configuration (safe to commit)
├── settings.local.json # Local permissions and session hooks - GITIGNORED
└── README.md           # This file
```

## Knowledge Files

### whisper-expertise.md
- Whisper model selection guide (Tiny/Small/Base/Medium/Large)
- Command parameters and greedy decoding settings
- Audio format requirements (16kHz, 16-bit mono WAV)
- Performance optimization and troubleshooting

### wpf-patterns.md
- WPF thread safety with Dispatcher
- MVVM pattern best practices
- Resource disposal patterns
- Common WPF pitfalls and solutions

## Settings

### Permissions
Pre-approved commands:
- `git log` - View commit history
- `git branch` - Check current branch
- `git status` - View working tree status

### Session Start Hook
Displays on every Claude Code session start:
- Current git branch
- Last 3 commits
- Uncommitted changes

## MCP Servers

### Configured Servers
- **Supabase** - Database access, migrations, security/performance advisors
- **GitHub** - Issue management, PR creation/review, workflow status

### Setup
Copy `mcp.json.example` to `mcp.json` and add your credentials:
```bash
cp .claude/mcp.json.example .claude/mcp.json
# Edit mcp.json with your GitHub PAT
```

**Note**: `mcp.json` is gitignored to protect secrets.

## Usage

Knowledge files are automatically available to Claude Code. Reference them when:
- Debugging Whisper transcription issues
- Working on WPF UI code
- Optimizing performance
- Troubleshooting thread safety issues

MCP servers enable commands like:
- "Create a GitHub issue for this bug"
- "Show me all licenses created this week" (Supabase)
- "Did the release workflow pass?"

## Future Additions

When you need new configuration:
- **Slash commands**: Create `.claude/commands/{name}.md`
- **Workflows**: Create `.claude/workflows/{name}.md`
- **Knowledge**: Add `.claude/knowledge/{topic}.md`

Keep it minimal - only add what you actually use!

# Claude Code + VS Code Optimization Guide

**For Developers Who Exclusively Use Claude Code**

This guide optimizes VS Code for seamless Claude Code collaboration, eliminating file-locking issues and maximizing AI-assisted development efficiency.

---

## Quick Setup (5 Minutes)

### ✅ **Already Completed:**
1. ✅ Windows Defender exclusion added for `C:\Users\mishk\Codingprojects`
2. ✅ VS Code workspace settings created (`.vscode/settings.json`)

### 🔧 **What the Settings Do:**

#### **Critical Settings (Fix File-Locking):**
```json
"files.autoSave": "off"                    // Claude can edit without interference
"editor.formatOnSave": false               // No auto-formatting conflicts
"files.watcherExclude": {...}              // Reduce file watching overhead
```

#### **Performance Boosts:**
- Disabled unnecessary Intellisense/autocomplete (Claude handles this)
- Excluded `bin/`, `obj/`, `node_modules/` from file watching
- Disabled telemetry and auto-fetch

#### **Kept Enabled (Useful):**
- Line numbers, minimap, rulers
- Trailing whitespace trimming
- Git integration (minimal)

---

## Recommended Extensions (Install These)

### **Essential for Claude Code:**

1. **C# Dev Kit** (Microsoft)
   - Purpose: Syntax highlighting, build support
   - Config: Already optimized in settings.json

2. **Pascal** (Alessandro Fragnani)
   - Purpose: Inno Setup `.iss` file support
   - Config: Auto-associated in settings.json

### **Optional but Helpful:**

3. **Git Graph** (mhutchie)
   - Purpose: Visualize commits (better than GitLens for Claude Code users)
   - Why: Lightweight, doesn't auto-modify files

4. **Error Lens** (Alexander)
   - Purpose: Inline error highlighting
   - Why: See errors without opening Problems panel

### **Disable/Remove These:**
- ❌ **Prettier** (conflicts with Claude formatting)
- ❌ **ESLint** (auto-fix conflicts with Claude edits)
- ❌ **GitLens** (too intrusive, use Git Graph instead)
- ❌ **C# XML Documentation** (auto-modifies files)

---

## Workflow Best Practices

### **Before Starting Claude Code Session:**

1. **Close unnecessary files** in VS Code
   - Reduces file watchers and memory usage
   - Faster Claude edits

2. **Run `git status`** to see uncommitted changes
   - Helps Claude understand current state

3. **Ensure no builds running**
   - Close Solution Explorer if using Visual Studio simultaneously

### **During Claude Code Session:**

1. **Don't manually edit files** while Claude is working
   - Let Claude finish current task before making manual changes

2. **Review Claude's changes in diff view**
   - VS Code shows side-by-side comparisons
   - Easy to spot unintended changes

3. **Use VS Code's integrated terminal**
   - Claude can run commands directly
   - Faster than switching to external terminal

### **After Claude Code Session:**

1. **Review all changes with `git diff`**
   - Verify Claude made expected changes

2. **Run tests before committing**
   - `dotnet test` for C# projects
   - Ensures no breaking changes

3. **Manually format if needed**
   - `Shift + Alt + F` (format document)
   - Only format files you personally reviewed

---

## Keyboard Shortcuts (Claude Code + VS Code)

### **Essential Shortcuts:**

| Shortcut | Action | Why Useful |
|----------|--------|------------|
| `Ctrl + Shift + P` | Command Palette | Quick access to any command |
| `Ctrl + P` | Quick Open File | Navigate without mouse |
| `Ctrl + Shift + F` | Search in Files | Find text across project |
| `Ctrl + K, Ctrl + 0` | Fold All | Collapse all code sections |
| `Ctrl + K, Ctrl + J` | Unfold All | Expand all code sections |
| `Alt + ↑/↓` | Move Line Up/Down | Quick code reordering |
| `Ctrl + /` | Toggle Comment | Comment/uncomment lines |
| `F12` | Go to Definition | Navigate to code definitions |
| `Shift + F12` | Find All References | See where code is used |

### **Git Shortcuts:**

| Shortcut | Action |
|----------|--------|
| `Ctrl + Shift + G` | Open Source Control panel |
| `Ctrl + Enter` | Commit (in SCM panel) |

### **Claude Code Specific:**

| Action | Method |
|--------|--------|
| Ask Claude to edit file | Just chat in Claude Code panel |
| Review Claude's changes | Check Source Control diff |
| Undo Claude's changes | `Ctrl + Z` or git revert |

---

## Common Issues & Solutions

### **Issue 1: File-Locking Errors**
**Symptom:** Claude says "file has been unexpectedly modified"

**Solutions:**
1. ✅ Ensure `.vscode/settings.json` is present (already done)
2. ✅ Verify Windows Defender exclusion (already done)
3. Close other IDEs (Visual Studio, Rider) if open
4. Restart VS Code if issue persists

---

### **Issue 2: Slow Intellisense/Autocomplete**
**Symptom:** VS Code lags when typing

**Solution:**
- Settings already disabled autocomplete
- If still slow, disable C# extension temporarily:
  - Extensions → C# → Disable (Workspace)

---

### **Issue 3: Git Conflicts When Claude Commits**
**Symptom:** Claude's commits create merge conflicts

**Solution:**
1. Always run `git pull` before starting Claude session
2. Commit frequently (small, focused commits)
3. Use feature branches for large changes

---

### **Issue 4: Build Errors After Claude Edits**
**Symptom:** Project won't build after Claude changes

**Solution:**
1. Run `dotnet clean` then `dotnet build`
2. Check Claude's changes in git diff
3. Ask Claude to fix specific build errors (paste error message)

---

## Performance Monitoring

### **Check VS Code Performance:**

1. Open Command Palette (`Ctrl + Shift + P`)
2. Type: `Developer: Show Running Extensions`
3. Review CPU/Memory usage
4. Disable high-usage extensions

### **Optimal VS Code Stats:**
- **Memory:** <500 MB (with Claude Code)
- **CPU:** <5% when idle
- **Startup:** <10 seconds

If higher, review installed extensions and disable unnecessary ones.

---

## Advanced: Global VS Code Settings

**Location:** `C:\Users\mishk\AppData\Roaming\Code\User\settings.json`

**Recommended Global Settings:**
```json
{
  // Apply optimizations to ALL workspaces
  "files.autoSave": "off",
  "editor.formatOnSave": false,
  "telemetry.telemetryLevel": "off",

  // Theme (personal preference)
  "workbench.colorTheme": "Dark+ (default dark)",

  // Font (better readability)
  "editor.fontSize": 14,
  "editor.fontFamily": "Consolas, 'Courier New', monospace",

  // Terminal
  "terminal.integrated.defaultProfile.windows": "PowerShell",
  "terminal.integrated.fontSize": 12
}
```

---

## Troubleshooting Checklist

**If Claude Code has issues editing files:**

- [ ] Windows Defender exclusion added? (`Get-MpPreference | Select-Object -ExpandProperty ExclusionPath`)
- [ ] VS Code auto-save disabled? (Check `.vscode/settings.json`)
- [ ] No other IDEs open? (Close Visual Studio, Rider)
- [ ] No builds running? (`tasklist | grep -i msbuild`)
- [ ] Git working directory clean? (`git status`)
- [ ] VS Code extensions minimal? (Only C# Dev Kit + Pascal)

---

## Summary: Why These Settings Matter

### **Before Optimization:**
- ❌ File-locking errors (VS Code auto-save conflicts)
- ❌ Slow performance (unnecessary file watchers)
- ❌ Auto-formatting conflicts (Prettier vs Claude)
- ❌ Intellisense lag (redundant with Claude)

### **After Optimization:**
- ✅ Zero file-locking errors
- ✅ Fast, responsive VS Code
- ✅ Claude has full control over formatting
- ✅ Minimal distractions, maximum productivity

---

## Next Steps

1. **Restart VS Code** to apply all settings
2. **Test Claude Code** - try editing a file
3. **Monitor performance** - check CPU/memory usage
4. **Adjust as needed** - tweak settings.json if needed

---

## Questions?

**Common Questions:**

**Q: Can I still manually format code?**
A: Yes! Use `Shift + Alt + F` to manually format when needed.

**Q: Will this break my existing workflow?**
A: No - these are workspace-specific settings. Other projects unaffected.

**Q: What if I need autocomplete?**
A: Temporarily enable it: `"editor.quickSuggestions": true` in settings.json

**Q: Can I use GitLens?**
A: Yes, but disable code lens: `"gitlens.codeLens.enabled": false`

---

**Created:** 2025-10-08
**For:** VoiceLite project + all future Claude Code projects
**Maintained by:** Claude Code optimizations team 🤖

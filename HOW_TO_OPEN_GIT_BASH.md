# How to Open Git Bash

## Method 1: From Start Menu (Easiest)

1. Press **Windows Key**
2. Type: `git bash`
3. Click **"Git Bash"** from search results
4. A black terminal window opens with `$` prompt

## Method 2: From File Explorer

1. Open File Explorer
2. Navigate to: `C:\Users\mishk\Codingprojects\SpeakLite\HereWeGoAgain v3.3 Fuck`
3. **Right-click in empty space** (not on a file)
4. Click **"Git Bash Here"** from context menu
5. Git Bash opens already in the correct directory!

## Method 3: From Windows Terminal

1. Open Windows Terminal
2. Click the **dropdown arrow** (▼) next to the + button
3. Select **"Git Bash"** from the profiles list

---

## ✅ How to Know You're in Git Bash (Not PowerShell)

**Git Bash looks like this:**
```
mishk@DESKTOP-ABC123 MINGW64 ~/Codingprojects/SpeakLite/HereWeGoAgain v3.3 Fuck
$
```

**PowerShell looks like this (WRONG):**
```
PS C:\Users\mishk\Codingprojects\SpeakLite\HereWeGoAgain v3.3 Fuck>
```

**Key differences:**
- Git Bash: Has `$` prompt, shows `MINGW64`
- PowerShell: Has `PS` prefix, ends with `>`

---

## 🚀 Once Git Bash is Open

You can now paste the commands from **QUICK_COMMANDS.txt**!

Start with command #1:
```bash
cd "C:\Users\mishk\Codingprojects\SpeakLite\HereWeGoAgain v3.3 Fuck"
```

Then continue with the rest of the commands one by one.

---

## ⚠️ Why NOT PowerShell?

PowerShell won't work because:
- ❌ Java is not in PowerShell's PATH (gives "command not found")
- ❌ Different command syntax (uses `\` instead of `/`)
- ❌ Git commands behave differently

Git Bash works because:
- ✅ Java works (you already tested this earlier)
- ✅ Git commands work perfectly
- ✅ Unix-style paths work
- ✅ BFG Repo-Cleaner will run without issues

---

## 🎯 Next Step

Open Git Bash using one of the methods above, then follow **QUICK_COMMANDS.txt**!

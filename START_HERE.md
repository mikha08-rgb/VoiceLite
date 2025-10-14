# Git History Scrubbing - START HERE

**Current Status**: Scripts ready, need to install Java + BFG

---

## Quick Decision Tree

### Do you have Java installed?

Check by running: `java -version`

**If YES** (Java is installed):
```powershell
# Run the manual setup (faster, no Chocolatey needed)
powershell -ExecutionPolicy Bypass -File manual-bfg-setup.ps1
```

**If NO** (Java not installed):

**Option A: Install Java manually** (5 minutes)
1. Download: https://adoptium.net/temurin/releases/?version=11
2. Run installer
3. Then run: `powershell -ExecutionPolicy Bypass -File manual-bfg-setup.ps1`

**Option B: Use Chocolatey** (10 minutes, installs everything)
1. Install Chocolatey first (copy command from setup-bfg.ps1 output)
2. Run: `powershell -ExecutionPolicy Bypass -File setup-bfg.ps1`

---

## After Setup Completes

Once you have BFG installed, run the scrubbing script:

```powershell
# If using Chocolatey BFG:
powershell -ExecutionPolicy Bypass -File scrub-git-history.ps1

# If using manual BFG (downloaded to Downloads folder):
powershell -ExecutionPolicy Bypass -File scrub-git-history.ps1 -BfgPath "$env:USERPROFILE\Downloads\bfg.jar"
```

---

## What Happens Next

1. **Safety checks** - Script verifies you're ready
2. **Confirmation** - You'll need to type "DELETE HISTORY" to confirm
3. **Git mirror clone** - Creates backup copy
4. **BFG scrubbing** - Deletes sensitive files from ALL commits
5. **Verification** - Confirms secrets removed
6. **Force push** - Rewrites GitHub history

**Duration**: ~10 minutes after BFG is installed

---

## Files Available

- `manual-bfg-setup.ps1` - Fast setup (Java only, no Chocolatey)
- `setup-bfg.ps1` - Full setup (installs Chocolatey + Java + BFG)
- `scrub-git-history.ps1` - Main scrubbing script
- `QUICK_START_SCRUB.md` - Detailed guide
- `GIT_HISTORY_SCRUB_INSTRUCTIONS.md` - Manual step-by-step

---

## Current Recommendation

**Fastest path for you right now:**

1. Check if Java is installed: `java -version`
2. If not, download and install Java: https://adoptium.net/temurin/releases/?version=11
3. Run manual setup: `powershell -ExecutionPolicy Bypass -File manual-bfg-setup.ps1`
4. Run scrubbing: `powershell -ExecutionPolicy Bypass -File scrub-git-history.ps1 -BfgPath "$env:USERPROFILE\Downloads\bfg.jar"`

---

## Need Help?

- See `QUICK_START_SCRUB.md` for detailed instructions
- See `GIT_HISTORY_SCRUB_INSTRUCTIONS.md` for manual steps
- All scripts include safety checks and dry-run modes

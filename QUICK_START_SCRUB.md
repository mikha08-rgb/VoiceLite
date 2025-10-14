# Git History Scrubbing - Quick Start Guide

**Status**: ⏸️ Ready to execute
**Duration**: ~1 hour
**Risk Level**: HIGH (destructive operation)

---

## ⚡ TL;DR - Two Commands to Fix Everything

```powershell
# Step 1: Install prerequisites (5 minutes)
powershell -ExecutionPolicy Bypass -File setup-bfg.ps1

# Step 2: Scrub git history (10 minutes)
powershell -ExecutionPolicy Bypass -File scrub-git-history.ps1
```

---

## 📋 Pre-Flight Checklist

Before running the scripts, verify:

- [x] ✅ Commits pushed to GitHub (verified: `b0a9b17` is on origin)
- [ ] ⏸️ Java installed (will be auto-installed by setup script)
- [ ] ⏸️ BFG Repo-Cleaner installed (will be auto-installed)
- [ ] ⚠️ No important uncommitted changes (check `git status`)
- [ ] ⚠️ Collaborators notified (if any)

---

## 🚀 Option 1: Automated Scripts (Recommended)

### Step 1: Install Prerequisites

Open **PowerShell as Administrator** and run:

```powershell
cd "C:\Users\mishk\Codingprojects\SpeakLite\HereWeGoAgain v3.3 Fuck"
powershell -ExecutionPolicy Bypass -File setup-bfg.ps1
```

**What this does**:
- Checks for Chocolatey (installs if needed)
- Installs Java 11 (required for BFG)
- Installs BFG Repo-Cleaner
- Verifies installation

**Duration**: ~5 minutes

---

### Step 2: Scrub Git History

After prerequisites are installed, run:

```powershell
# Dry run first (recommended)
powershell -ExecutionPolicy Bypass -File scrub-git-history.ps1 -DryRun

# If dry run looks good, execute for real
powershell -ExecutionPolicy Bypass -File scrub-git-history.ps1
```

**What this does**:
1. Creates git mirror clone
2. Runs BFG to delete sensitive files from ALL commits
3. Cleans git references (reflog expire + gc)
4. Verifies secrets removed
5. Force pushes to GitHub

**Duration**: ~10 minutes

**Safety features**:
- Dry run mode available (`-DryRun`)
- Multiple safety checks before execution
- Requires typing "DELETE HISTORY" to confirm
- Verification step before force push

---

## 🛠️ Option 2: Manual Steps (Fallback)

If automated scripts fail, follow detailed manual instructions in:

**[GIT_HISTORY_SCRUB_INSTRUCTIONS.md](GIT_HISTORY_SCRUB_INSTRUCTIONS.md)**

---

## ✅ Verification After Scrubbing

After the script completes, verify secrets are gone:

```bash
# Search local repo
git log --all --full-history -p -S "vS89Zv4vrDNoM9zXm5aAsba"
# Should return: NOTHING

# Search GitHub (via browser)
# https://github.com/mikha08-rgb/VoiceLite/search?q=vS89Zv4vrDNoM9zXm5aAsba&type=commits
# Should return: NO RESULTS
```

---

## 🔄 Update Local Repository

After force push completes, update your working directory:

```bash
cd "C:\Users\mishk\Codingprojects\SpeakLite\HereWeGoAgain v3.3 Fuck"
git fetch origin
git reset --hard origin/master
git log --oneline -5  # Verify commit hashes changed
```

---

## 📊 Progress Tracking

**Phase 1 - Secure Repository** (Total: 4-8 hours)

- [x] ✅ Step 1.1: Push commits to origin (COMPLETED)
- [ ] ⏸️ Step 1.2: Backup repository (skipped - git mirror is sufficient)
- [ ] **→ Step 1.3: Scrub git history (IN PROGRESS)**
- [ ] ⏸️ Step 1.4: Generate new Ed25519 keypairs
- [ ] ⏸️ Step 1.5: Rotate production credentials
- [ ] ⏸️ Step 1.6: Update desktop app
- [ ] ⏸️ Step 1.7: Test and verify

---

## ❌ Rollback Plan (if needed)

If something goes wrong during scrubbing:

```bash
# The old history is still on GitHub until you force push
# Just delete the mirror and try again:
rm -rf "C:\Users\mishk\Codingprojects\SpeakLite\VoiceLite-mirror.git"

# Your working directory is safe - nothing changes until force push
```

---

## 🔐 What Gets Deleted from History

These files will be PERMANENTLY removed from ALL commits:

1. `add-secrets-to-vercel.sh`
   - Contains: LICENSE_SIGNING_PRIVATE, CRL_SIGNING_PRIVATE, MIGRATION_SECRET
   - Impact: Attackers can forge unlimited Pro licenses

2. `SECRET_ROTATION_COMPLETE.md`
   - Contains: Documentation of exposed keys
   - Impact: Confirms which keys are compromised

**These secrets will NO LONGER be accessible via**:
- `git log`
- `git show <commit>`
- GitHub search
- GitHub commit history
- Git reflog

---

## 📞 Troubleshooting

### Script fails with "Java not found"
**Solution**: Install Java manually from https://adoptium.net/, then re-run setup script

### Script fails with "Chocolatey not found"
**Solution**: Run setup script as Administrator, it will install Chocolatey

### BFG says "commit is protected"
**Solution**: This is NORMAL - BFG won't modify HEAD (we already deleted files in commit `00f4f32`)

### Force push rejected
**Solution**: Check GitHub branch protection rules at https://github.com/mikha08-rgb/VoiceLite/settings/branches

---

## 🎯 Next Steps After Scrubbing

Once git history is clean:

1. ✅ Verify secrets removed (search GitHub + local)
2. ⏭️ Generate new Ed25519 keypairs (see RELEASE_UNBLOCK_PLAN.md Phase 1 Step 1.4)
3. ⏭️ Rotate production credentials (Phase 1 Step 1.5)
4. ⏭️ Update desktop app with new public keys (Phase 1 Step 1.6)
5. ⏭️ Deploy backend with new credentials

---

## 📚 Additional Resources

- **Full Manual Instructions**: [GIT_HISTORY_SCRUB_INSTRUCTIONS.md](GIT_HISTORY_SCRUB_INSTRUCTIONS.md)
- **Complete Action Plan**: [RELEASE_UNBLOCK_PLAN.md](RELEASE_UNBLOCK_PLAN.md)
- **BFG Documentation**: https://rtyley.github.io/bfg-repo-cleaner/
- **GitHub Guide**: https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/removing-sensitive-data-from-a-repository

---

**Ready to proceed?** Run the setup script in an Administrator PowerShell window.

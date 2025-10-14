# Git History Scrubbing Instructions - MANUAL STEPS REQUIRED

**CRITICAL**: This must be done manually as it requires destructive git operations.

**Status**: ✅ Commits pushed to GitHub, ready for history rewrite

---

## ⚠️ WARNING

This operation will:
- **REWRITE ALL GIT HISTORY** (irreversible)
- **BREAK ALL FORKS** (collaborators must re-clone)
- **CHANGE ALL COMMIT HASHES** (references will break)

**Before proceeding**: Ensure all important work is pushed to GitHub.

---

## Step 1: Verify Exposed Secrets Still in History

```bash
cd "C:\Users\mishk\Codingprojects\SpeakLite\HereWeGoAgain v3.3 Fuck"

# Search for the exposed Ed25519 private key
git log --all --full-history -p -S "vS89Zv4vrDNoM9zXm5aAsba"

# If this returns results, secrets are still in history
```

**Expected Result**: Should find commits containing the exposed keys.

---

## Step 2: Install BFG Repo-Cleaner

**Option A: Download Manually**
1. Go to: https://rtyley.github.io/bfg-repo-cleaner/
2. Download `bfg-1.14.0.jar` (or latest version)
3. Save to: `C:\Users\mishk\Downloads\bfg.jar`

**Option B: Via Chocolatey** (if installed):
```bash
choco install bfg-repo-cleaner
```

**Verify Installation**:
```bash
java -jar C:\Users\mishk\Downloads\bfg.jar --version
# Should output: bfg 1.14.0
```

---

## Step 3: Create Fresh Git Mirror

BFG requires a bare clone (mirror) to operate on.

```bash
cd C:\Users\mishk\Codingprojects\SpeakLite

# Clone bare repository
git clone --mirror https://github.com/mikha08-rgb/VoiceLite.git VoiceLite-mirror.git

cd VoiceLite-mirror.git
```

**What This Does**: Creates a complete copy of the repository including all branches, tags, and history.

---

## Step 4: Run BFG to Delete Sensitive Files

```bash
# Delete files from ALL history
java -jar C:\Users\mishk\Downloads\bfg.jar --delete-files add-secrets-to-vercel.sh

java -jar C:\Users\mishk\Downloads\bfg.jar --delete-files SECRET_ROTATION_COMPLETE.md
```

**Expected Output**:
```
Using repo : C:\Users\mishk\Codingprojects\SpeakLite\VoiceLite-mirror.git

Found 2 objects to protect
Found 3 commit-pointing refs : HEAD, refs/heads/master, refs/tags/v1.0.66

Protected commits
-----------------

These are your protected commits, and so their contents will NOT be altered:

 * commit 12345678 (protected by 'HEAD')

Cleaning
--------

Found 125 commits
Cleaning commits:       100% (125/125)
Cleaning commits completed in 2,341 ms.

Updating references:    100% (3/3)
...

BFG run is complete! When ready, run: git reflog expire --expire=now --all && git gc --prune=now --aggressive
```

---

## Step 5: Clean Up Git References

```bash
# Still in VoiceLite-mirror.git directory

# Expire all reflog entries immediately
git reflog expire --expire=now --all

# Aggressive garbage collection
git gc --prune=now --aggressive
```

**What This Does**: Removes all references to the old commits containing secrets, making them permanently unrecoverable.

**Expected Output**:
```
Enumerating objects: 15234, done.
Counting objects: 100% (15234/15234), done.
Delta compression using up to 16 threads
Compressing objects: 100% (12456/12456), done.
Writing objects: 100% (15234/15234), done.
```

---

## Step 6: Verify Secrets Removed

```bash
# Search for the exposed key again
git log --all --full-history -p -S "vS89Zv4vrDNoM9zXm5aAsba"

# Should return: NOTHING (no results)

# Search for the deleted files
git log --all --full-history -- "*add-secrets-to-vercel.sh"

# Should return: NOTHING
```

**If you still see results**: BFG didn't work correctly. DO NOT PROCEED. Contact support.

---

## Step 7: Force Push Rewritten History

**⚠️ POINT OF NO RETURN**: This will permanently rewrite GitHub history.

```bash
# Push to GitHub (overwrites ALL branches)
git push --force --all

# Push tags
git push --force --tags
```

**Expected Output**:
```
+ 12345678...abcdef12 master -> master (forced update)
```

**Confirmation**: Visit https://github.com/mikha08-rgb/VoiceLite/commits/master and verify commit hashes have changed.

---

## Step 8: Update Local Working Directory

```bash
cd "C:\Users\mishk\Codingprojects\SpeakLite\HereWeGoAgain v3.3 Fuck"

# Fetch rewritten history
git fetch origin

# Hard reset to match rewritten history
git reset --hard origin/master

# Verify
git log --oneline -5
```

**Expected Result**: Commit hashes should be different from before.

---

## Step 9: Final Verification

```bash
# Search local repo for secrets
git log --all --full-history -p -S "vS89Zv4vrDNoM9zXm5aAsba"

# Search GitHub (via browser)
# Go to: https://github.com/mikha08-rgb/VoiceLite/search?q=vS89Zv4vrDNoM9zXm5aAsba&type=commits

# Both should return: NO RESULTS
```

---

## Step 10: Notify Collaborators (if any)

If anyone else has cloned the repository:

**Send This Message**:
```
IMPORTANT: Git history has been rewritten to remove exposed secrets.

ACTION REQUIRED:
1. Delete your local clone
2. Re-clone from GitHub: git clone https://github.com/mikha08-rgb/VoiceLite.git
3. Do NOT merge old history with new history

All commit hashes have changed. Existing pull requests may need to be recreated.
```

---

## Common Issues & Solutions

### Issue 1: BFG Doesn't Delete File
**Symptom**: File still appears in history after BFG run

**Cause**: File is in the HEAD commit (protected by default)

**Solution**: Delete from working directory first (we already did this), or use `--no-blob-protection`:
```bash
java -jar bfg.jar --delete-files "add-secrets-to-vercel.sh" --no-blob-protection
```

### Issue 2: "Protected Commits" Message
**Symptom**: BFG says "commit is protected"

**Cause**: Latest commit on current branch is protected

**Solution**: This is NORMAL. BFG won't modify HEAD (we already deleted files from HEAD in commit 00f4f32).

### Issue 3: Force Push Rejected
**Symptom**: `git push --force` says "pre-receive hook declined"

**Cause**: GitHub branch protection rules enabled

**Solution**:
1. Go to: https://github.com/mikha08-rgb/VoiceLite/settings/branches
2. Temporarily disable branch protection
3. Force push
4. Re-enable protection

---

## After Scrubbing: Next Steps

Once git history is clean:

1. ✅ **Verify secrets removed** (Step 9 above)
2. ⏭️ **Generate new Ed25519 keypairs** (see RELEASE_UNBLOCK_PLAN.md Phase 1 Step 1.3)
3. ⏭️ **Rotate production credentials** (Phase 1 Step 1.4)
4. ⏭️ **Update desktop app** with new public keys (Phase 1 Step 1.5)
5. ⏭️ **Deploy backend** with new credentials

---

## Rollback Plan (if something goes wrong)

**If you need to undo**:
```bash
# You backed up to GitHub before scrubbing
# Create a new branch from old history:
git branch old-history <OLD_COMMIT_HASH>
git push origin old-history

# Then investigate what went wrong before trying again
```

---

## Timeline

- **Step 1-2**: 10 minutes (install tools)
- **Step 3-5**: 30 minutes (run BFG, clean refs)
- **Step 6-9**: 20 minutes (verify and push)
- **Step 10**: 5 minutes (notify collaborators)
- **Total**: ~1 hour

---

## Questions?

- Read BFG docs: https://rtyley.github.io/bfg-repo-cleaner/
- GitHub guide: https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/removing-sensitive-data-from-a-repository
- See also: [RELEASE_UNBLOCK_PLAN.md](RELEASE_UNBLOCK_PLAN.md) for full context

---

**Status**: ⏸️ Ready to execute (manual steps required)
**Next**: Follow steps 1-9 above, then mark as complete

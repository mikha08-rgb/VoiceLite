# Manual Git History Scrubbing - Step by Step

**Duration**: 10 minutes
**Risk**: High (irreversible)
**Requirement**: Copy/paste each command one at a time

---

## Step 1: Navigate to Project Directory

```bash
cd "C:\Users\mishk\Codingprojects\SpeakLite\HereWeGoAgain v3.3 Fuck"
```

---

## Step 2: Remove Old Mirror (if exists)

```bash
rm -rf "C:\Users\mishk\Codingprojects\SpeakLite\VoiceLite-mirror.git"
```

---

## Step 3: Clone Fresh Mirror from GitHub

```bash
git clone --mirror https://github.com/mikha08-rgb/VoiceLite.git "C:\Users\mishk\Codingprojects\SpeakLite\VoiceLite-mirror.git"
```

**Wait for this to complete** (~30 seconds)

---

## Step 4: Navigate to Mirror Directory

```bash
cd "C:\Users\mishk\Codingprojects\SpeakLite\VoiceLite-mirror.git"
```

---

## Step 5: Run BFG to Redact Secrets

```bash
java -jar "C:\Users\mishk\Downloads\bfg.jar" --replace-text "../HereWeGoAgain v3.3 Fuck/secrets-to-redact.txt" .
```

**Expected output**:
- "Using repo: ..."
- "Found X commits"
- "Protected commits are: [ ... ]"
- "Cleaning commits: 100% done"

**Wait for this to complete** (~1 minute)

---

## Step 6: Clean Git References

```bash
git reflog expire --expire=now --all
```

Then:

```bash
git gc --prune=now --aggressive
```

**Wait for gc to complete** (~2 minutes)

---

## Step 7: Verify Secrets Are Gone

```bash
git log --all --full-history -p -S "vS89Zv4vrDNoM9zXm5aAsba" --oneline
```

**Expected output**: **NOTHING** (empty result)

**If you see commits listed**: ❌ **STOP! DO NOT PROCEED!** Something went wrong.

**If empty**: ✅ Continue to Step 8

---

## Step 8: Review What BFG Did

```bash
cat ..bfg-report/*/cache-stats.txt
```

Should show statistics about cleaned commits.

---

## Step 9: Force Push to GitHub (IRREVERSIBLE!)

⚠️ **FINAL WARNING**: This will permanently rewrite ALL git history on GitHub!

Type this command but **READ THE WARNING FIRST**:

```bash
git push --force --all
```

**You will be prompted for GitHub credentials**

After push completes, also push tags:

```bash
git push --force --tags
```

---

## Step 10: Update Your Local Repository

```bash
cd "C:\Users\mishk\Codingprojects\SpeakLite\HereWeGoAgain v3.3 Fuck"
git fetch origin
git reset --hard origin/master
```

---

## Step 11: Final Verification

Search for the secret locally:

```bash
git log --all --full-history -p -S "vS89Zv4vrDNoM9zXm5aAsba" --oneline
```

**Expected**: **NOTHING**

Search on GitHub (open in browser):
https://github.com/mikha08-rgb/VoiceLite/search?q=vS89Zv4vrDNoM9zXm5aAsba&type=commits

**Expected**: "We couldn't find any code matching 'vS89Zv4vrDNoM9zXm5aAsba'"

---

## ✅ Success Criteria

- [ ] Local git log shows NO commits with secrets
- [ ] GitHub search shows "No results"
- [ ] Commit hashes changed (compare before/after)
- [ ] All files still present in working directory

---

## ❌ Rollback (if needed before Step 9)

If something goes wrong BEFORE you push:

```bash
cd "C:\Users\mishk\Codingprojects\SpeakLite\HereWeGoAgain v3.3 Fuck"
# Your working directory is unchanged
# Just delete the mirror and try again:
rm -rf "C:\Users\mishk\Codingprojects\SpeakLite\VoiceLite-mirror.git"
```

**AFTER Step 9 (force push)**: Cannot rollback! History is rewritten on GitHub.

---

## 🆘 Troubleshooting

### "java: command not found"
**Fix**: Run this first to find Java:
```bash
find /c/Program\ Files -name "java.exe" 2>/dev/null | head -1
```
Then use the full path in Step 5.

### "secrets still found in history"
**Cause**: BFG didn't run or failed silently
**Fix**: Check BFG output for errors, re-run Step 5

### "failed to push"
**Cause**: GitHub branch protection enabled
**Fix**: Disable branch protection at https://github.com/mikha08-rgb/VoiceLite/settings/branches

---

## 📋 What Gets Replaced

BFG will replace these 5 strings with `***REMOVED***` in ALL commits:

1. `vS89Zv4vrDNoM9zXm5aAsba-FwFq_zb9maVey2V7L5k` (LICENSE_SIGNING_PRIVATE)
2. `qmXC7vEDAK1XLsSHttTbAa_L71JDmJW_zeNcsPOhWZE` (CRL_SIGNING_PRIVATE)
3. `443ed3297b3a26ba4684129e59c72c6b6ce4a944344ef2579df2bdeba7d54210` (MIGRATION_SECRET)
4. `fRR5l40q-wt8ptAFcOGsWIBHtLDBjnb_T3Z9HMLwgCc` (LICENSE_SIGNING_PUBLIC)
5. `19Y5ul1S-ISjja7f827O5epfupvaBBMyhb_uVWLLf8M` (CRL_SIGNING_PUBLIC)

**Files affected**: `CRITICAL_ISSUES_REPORT.md`, `SECRET_ROTATION_COMPLETE.md`, `add-secrets-to-vercel.sh`

---

## ⏭️ Next Steps After Success

1. ✅ Generate new Ed25519 keypairs (I can help with this)
2. ✅ Rotate Supabase database password
3. ✅ Rotate Stripe API keys + webhook secret
4. ✅ Rotate Resend API key
5. ✅ Rotate Upstash Redis token
6. ✅ Update desktop app with new public keys
7. ✅ Deploy backend with new credentials
8. ✅ Test license validation works

**Total time for Phase 1**: 4-6 hours after scrubbing completes

---

**Ready?** Start with Step 1 in your Git Bash terminal!

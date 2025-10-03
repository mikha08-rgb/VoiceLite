# Test File Cleanup Agent

## Purpose
Identifies and cleans up test artifacts, duplicate test files, stale test results, and obsolete test coverage reports to maintain a lean test suite.

## When to Use
- After large test refactoring
- When TestResults/ directory grows too large (>100MB)
- Before creating pull requests (clean git history)
- During disk space cleanup
- When test execution slows down due to stale files

## Capabilities

1. **Test Result Cleanup**: Remove old TestResults/ directories
2. **Coverage Report Cleanup**: Remove outdated coverage.cobertura.xml files
3. **Duplicate Test Detection**: Find tests with identical or very similar logic
4. **Obsolete Test Detection**: Find tests for code that no longer exists
5. **Test Naming Consistency**: Detect tests with non-standard naming

## Cleanup Categories

### Category 1: Test Artifacts (Always Safe)
```bash
# TestResults/ directories (excluded from git)
VoiceLite/VoiceLite.Tests/TestResults/**/*.xml
VoiceLite/VoiceLite.Tests/TestResults/**/*.cobertura
VoiceLite/VoiceLite.Tests/TestResults/**/*.json

# Typical size: 5-50MB per test run
# Safe to delete: Regenerated on every test run
```

### Category 2: Backup Files
```bash
# .bak files from failed edits
**/*.cs.bak
**/*Tests.cs.bak

# Safe to delete after verifying no needed changes
```

### Category 3: Duplicate/Obsolete Tests
```csharp
// Example: Same test written twice
[Fact]
public void Constructor_InitializesDefaults_V1() { ... }

[Fact]
public void Constructor_SetsDefaultValues() { ... }
// ^ These likely test the same thing
```

## Workflow

### Phase 1: Safe Artifact Cleanup

```bash
# 1. Remove TestResults directories
find VoiceLite/VoiceLite.Tests/TestResults -type d -delete

# 2. Remove coverage reports
find . -name "coverage.*.xml" -type f -delete
find . -name "*.cobertura.xml" -type f -delete

# 3. Remove backup files (with confirmation)
find . -name "*.cs.bak" -type f -ls  # List first
# User confirms, then delete

# 4. Clean bin/obj directories (optional)
find . -name "bin" -type d -prune -exec rm -rf {} \;
find . -name "obj" -type d -prune -exec rm -rf {} \;
```

### Phase 2: Duplicate Test Detection

```bash
# Find tests with similar names
grep -rh "^\s*\[Fact\]" VoiceLite/VoiceLite.Tests/ -A 1 | sort | uniq -d

# Find tests with identical bodies (using hash)
for file in VoiceLite/VoiceLite.Tests/**/*Tests.cs; do
  # Extract test methods
  # Hash test bodies
  # Report duplicates
done
```

### Phase 3: Obsolete Test Detection

```bash
# Find tests for classes that no longer exist
grep -h "public class.*Tests" VoiceLite/VoiceLite.Tests/**/*.cs | \
  sed 's/Tests.*//' | \
  while read class; do
    # Check if $class exists in main codebase
    grep -r "public class $class" VoiceLite/VoiceLite/ || echo "Obsolete: ${class}Tests"
  done
```

## Safety Rules

1. **Always backup before deletion**: `git stash` or create branch
2. **Never delete test files without analysis**: Could be testing edge cases
3. **Confirm duplicates manually**: Similar != duplicate (might test different aspects)
4. **Keep historical context**: Some "obsolete" tests document past bugs
5. **Preserve failing tests**: Might be documenting known issues

## Detection Patterns

### Duplicate Tests (Similar Names)
```regex
# Pattern: Same concept, different naming
Constructor_Initializes.*
Constructor_Sets.*
Constructor_Creates.*

# Action: Review and consolidate
```

### Obsolete Tests
```csharp
// Test file: WhisperServiceTests.cs
// But: WhisperService.cs was deleted, replaced by PersistentWhisperService.cs

// Action: LIKELY OBSOLETE
// Verify: Check git history, see if tests were migrated
// Decision: Delete if functionality is covered by PersistentWhisperServiceTests.cs
```

### Test Artifacts (Always Safe)
```bash
# Pattern 1: Test result directories
VoiceLite/VoiceLite.Tests/TestResults/09a9f9a5-.../coverage.cobertura.xml
VoiceLite/VoiceLite.Tests/TestResults/0acf7c1e-.../coverage.cobertura.xml

# Pattern 2: Backup files
LicenseServiceTests.cs.bak
WhisperErrorRecoveryTests.cs.bak

# Action: DELETE (always safe, not in git)
```

## Output Format

```markdown
# Test File Cleanup Report

## Summary
- Test artifact size: 247 MB
- Backup files found: 3
- Potential duplicate tests: 4 pairs
- Obsolete test files: 1
- Estimated cleanup: 250 MB

## Safe to Remove (Auto-approved)

### Test Artifacts (247 MB)
```
VoiceLite/VoiceLite.Tests/TestResults/
├── 09a9f9a5-e2de-4afc-b952-80d311229adc/ (45 MB)
├── 0acf7c1e-6348-4d5a-910d-1a45419bde6f/ (52 MB)
├── 1ac91e7e-6411-41cf-bd78-6e1c523bdc06/ (68 MB)
├── ba5fea64-d828-4e02-9f6e-58ecfe80138e/ (41 MB)
└── c2d73b91-793f-4034-a990-7b5eb7cfd60e/ (41 MB)
```

**Action**: `rm -rf VoiceLite/VoiceLite.Tests/TestResults/`

### Backup Files (3 files, 125 KB)
1. `LicenseServiceTests.cs.bak` (42 KB)
2. `WhisperErrorRecoveryTests.cs.bak` (58 KB)
3. `DependencyCheckerTests.cs.bak` (25 KB)

**Action**: Delete after verifying no needed changes

## Requires Review (Potential Duplicates)

### Duplicate Pair 1
**File**: `Services/LicenseServiceTests.cs`
- `VerifySignedLicense_ValidSignature_ReturnsTrue()` (line 12)
- `VerifySignedLicense_WithCorrectSignature_Succeeds()` (line 67)

**Similarity**: 95% (both test valid signature verification)
**Recommendation**: Keep first one, remove second (redundant)

### Duplicate Pair 2
**File**: `Services/DependencyCheckerTests.cs`
- `DependencyCheckResult_HasAnyIssues_WhenAntivirusIssuesTrue()` (line 38)
- `DiagnosticResult_HasAnyIssues_WhenPermissionIssuesTrue()` (line 52)

**Similarity**: 85% (same pattern, different property)
**Recommendation**: KEEP BOTH (testing different properties)

## Potential Obsolete Tests

### WhisperServiceTests.cs (DELETED in v1.0.5)
- File no longer exists: `VoiceLite/VoiceLite/Services/WhisperService.cs`
- Replaced by: `PersistentWhisperService.cs` with `PersistentWhisperServiceTests.cs`
- Tests exist: `VoiceLite/VoiceLite.Tests/Services/WhisperServiceTests.cs` (NOT FOUND)

**Conclusion**: Already cleaned up ✅

## Recommendations

1. **Delete TestResults/** (247 MB saved)
   ```bash
   rm -rf VoiceLite/VoiceLite.Tests/TestResults/
   ```

2. **Delete backup files** (125 KB saved)
   ```bash
   rm VoiceLite/VoiceLite.Tests/Services/*.bak
   ```

3. **Review 2 duplicate pairs manually** (5 min)
   - Consolidate if truly redundant
   - Add comment explaining difference if keeping both

4. **Update .gitignore** to prevent future TestResults commits
   ```gitignore
   **/TestResults/
   **/*.bak
   **/coverage.*.xml
   ```

## Total Impact
- Disk space saved: ~247 MB
- Files cleaned: 5 directories + 3 backups
- Reduced noise in git status
- Faster test discovery (fewer files to scan)
```

## Automation Script

```bash
#!/bin/bash
# cleanup-test-artifacts.sh

echo "=== Test File Cleanup ==="

# 1. Find TestResults directories
TESTRESULTS=$(find . -type d -name "TestResults" 2>/dev/null)
if [ -n "$TESTRESULTS" ]; then
  echo "Found TestResults directories:"
  echo "$TESTRESULTS" | while read dir; do
    SIZE=$(du -sh "$dir" | cut -f1)
    echo "  $dir ($SIZE)"
  done

  read -p "Delete all TestResults directories? (y/n) " -n 1 -r
  echo
  if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo "$TESTRESULTS" | xargs rm -rf
    echo "✅ Deleted TestResults directories"
  fi
else
  echo "✅ No TestResults directories found"
fi

# 2. Find backup files
BACKUPS=$(find . -name "*.cs.bak" -type f 2>/dev/null)
if [ -n "$BACKUPS" ]; then
  echo "Found backup files:"
  echo "$BACKUPS"

  read -p "Delete backup files? (y/n) " -n 1 -r
  echo
  if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo "$BACKUPS" | xargs rm -f
    echo "✅ Deleted backup files"
  fi
else
  echo "✅ No backup files found"
fi

# 3. Find old coverage reports
COVERAGE=$(find . -name "coverage.*.xml" -o -name "*.cobertura.xml" 2>/dev/null | grep -v node_modules)
if [ -n "$COVERAGE" ]; then
  echo "Found old coverage reports:"
  echo "$COVERAGE"

  read -p "Delete coverage reports? (y/n) " -n 1 -r
  echo
  if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo "$COVERAGE" | xargs rm -f
    echo "✅ Deleted coverage reports"
  fi
else
  echo "✅ No old coverage reports found"
fi

echo ""
echo "=== Cleanup Complete ==="
```

## Integration with CI/CD

```yaml
# .github/workflows/cleanup-test-artifacts.yml
name: Cleanup Test Artifacts
on:
  schedule:
    - cron: '0 2 * * 0'  # Weekly at 2 AM on Sunday
  workflow_dispatch:

jobs:
  cleanup:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Remove test artifacts
        run: |
          find . -type d -name "TestResults" -exec rm -rf {} + || true
          find . -name "*.cs.bak" -type f -delete || true
          find . -name "coverage.*.xml" -delete || true

      - name: Create PR if changes
        run: |
          if [ -n "$(git status --porcelain)" ]; then
            git config user.name "github-actions"
            git config user.email "github-actions@github.com"
            git checkout -b cleanup/test-artifacts
            git add -A
            git commit -m "chore: cleanup test artifacts"
            git push origin cleanup/test-artifacts
            gh pr create --title "Cleanup test artifacts" --body "Automated cleanup"
          fi
```

## Best Practices

1. **Add to .gitignore**: Prevent committing artifacts
   ```gitignore
   **/TestResults/
   **/bin/
   **/obj/
   **/*.bak
   **/coverage.*.xml
   ```

2. **Regular cleanup schedule**: Weekly or before releases

3. **CI artifacts retention**: Set short retention (7 days) in GitHub Actions

4. **Test naming conventions**: Enforce consistent naming to avoid duplicates
   ```csharp
   // Good
   [Fact]
   public void MethodName_Scenario_ExpectedBehavior()

   // Bad (inconsistent)
   [Fact]
   public void Test_MethodName_Works()
   [Fact]
   public void MethodName_Should_Work()
   ```

5. **Code review checklist**: Check for duplicate tests in PRs

## Success Metrics

After cleanup:
- ✅ All tests still pass (262 tests)
- ✅ Disk space reclaimed (measure before/after)
- ✅ No git-tracked test artifacts
- ✅ Faster test discovery (fewer files)
- ✅ Cleaner git status output

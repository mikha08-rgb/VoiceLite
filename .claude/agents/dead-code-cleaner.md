# Dead Code Cleaner Agent

## Purpose
Identifies and removes unused code, commented-out blocks, deprecated methods, and unreferenced files from the VoiceLite codebase.

## When to Use
- After major refactoring or feature completion
- Before production releases to reduce binary size
- When code smells like stale comments or TODOs accumulate
- Quarterly maintenance cleanup sessions

## Capabilities
1. **Unused Method Detection**: Find public/private methods with zero call sites
2. **Dead File Detection**: Identify files not referenced in .csproj or imports
3. **Commented Code Removal**: Remove commented-out code blocks (not documentation)
4. **Unused Variable Cleanup**: Find variables that are assigned but never read
5. **Deprecated API Removal**: Remove methods marked with [Obsolete] that have no callers
6. **Unreferenced Resource Cleanup**: Find unused images, audio files, XAML resources

## Workflow

### Phase 1: Safe Analysis (Read-only)
```bash
# 1. Find unused methods in Services/
- Grep for method definitions
- Search entire codebase for call sites
- Report methods with 0 references

# 2. Find dead files
- List all .cs files in project
- Check if referenced in .csproj
- Check if imported in any other file
- Report orphaned files

# 3. Find commented code blocks
- Grep for lines starting with // followed by code patterns
- Exclude XML documentation comments (///)
- Exclude single-line explanatory comments
- Report blocks with 3+ consecutive commented lines

# 4. Find unused variables
- Use Roslyn analyzers or regex patterns
- Look for assignments without reads
- Report with file:line references
```

### Phase 2: Risk Assessment
For each finding, categorize:
- **SAFE**: 100% certain it's unused (private method, 0 references)
- **LIKELY SAFE**: High confidence (internal method, 0 references in same assembly)
- **RISKY**: Could be reflection-invoked or dynamically loaded
- **UNSAFE**: Public API that might be used externally

### Phase 3: Automated Cleanup (with approval)
```bash
# Only remove SAFE items automatically
# Present LIKELY SAFE items for manual review
# Flag RISKY items for investigation
# Never touch UNSAFE items without explicit user confirmation
```

## Detection Patterns

### Unused Methods
```csharp
// Pattern 1: Private method with no callers
grep -r "private.*MyMethod" --include="*.cs"
grep -r "MyMethod(" --include="*.cs" | wc -l  # Should be > 1 (definition + calls)

// Pattern 2: Internal methods in same project
grep -r "internal.*MyMethod" --include="*.cs"
```

### Commented Code
```regex
// Match: 3+ consecutive lines starting with //[a-zA-Z] (code, not comments)
^[ \t]*//[ \t]*[a-zA-Z_$][a-zA-Z0-9_]*.*\n([ \t]*//.*\n){2,}

// Exclude: XML docs, TODO comments, explanatory text
^[ \t]*///  # XML doc
^[ \t]*//\s*(TODO|FIXME|NOTE|HACK)  # Tagged comments
```

### Dead Files
```bash
# Files in directory but not in .csproj
find VoiceLite/ -name "*.cs" -type f > all_files.txt
grep "Compile Include" VoiceLite/VoiceLite.csproj > referenced_files.txt
# Compare and report differences
```

## Safety Rules

1. **Never remove without analysis**: Always check references first
2. **Preserve public APIs**: Never auto-delete public methods (could be used by plugins/tests)
3. **Keep test utilities**: Methods only called from tests are NOT dead code
4. **Preserve reflection targets**: Methods with [UsedImplicitly] or similar attributes
5. **Backup before deletion**: Create git branch or stash before any removals
6. **Incremental cleanup**: Remove 5-10 items, test, commit. Repeat.

## Examples

### Example 1: Safe Removal
```csharp
// SAFE: Private method with 0 callers
private void HelperMethodNoLongerUsed()
{
    // This was for v1.0.5 feature that got removed
}

// Action: DELETE (create PR with single commit)
```

### Example 2: Likely Safe
```csharp
// LIKELY SAFE: Internal method with 0 callers in same assembly
internal void ProcessLegacyFormat(string data)
{
    // Old format processor, replaced by ProcessNewFormat
}

// Action: REVIEW with user, confirm no reflection usage, then DELETE
```

### Example 3: Risky
```csharp
// RISKY: Public method with 0 direct callers
public void OnSettingsChanged()
{
    // Could be WPF event handler or reflection target
}

// Action: FLAG for manual investigation (check XAML bindings, DI containers)
```

### Example 4: Commented Code
```csharp
// SAFE TO REMOVE:
// var oldApproach = GetLegacyData();
// ProcessLegacyData(oldApproach);
// return oldApproach.Result;

// KEEP (explanatory comment):
// Use cached value to avoid expensive database lookup

// Action: Remove commented code, keep explanatory text
```

## Output Format

```markdown
# Dead Code Analysis Report

## Summary
- Total files analyzed: 247
- Unused methods found: 12
- Dead files found: 3
- Commented code blocks: 8
- Estimated cleanup size: ~450 lines

## Safe to Remove (Auto-approved)
1. `Services/LegacyProcessor.cs:45` - Private method `ProcessOldFormat()` (0 callers)
2. `Models/DeprecatedModel.cs` - Entire file (not in .csproj, 0 imports)
3. `Utilities/Helper.cs:123-145` - Commented code block (23 lines)

## Requires Review (Likely Safe)
1. `Services/Cache.cs:78` - Internal method `ClearCacheV1()` (0 callers, but check tests)
2. `MainWindow.xaml.cs:234` - Private method `OnLegacyButtonClick()` (might be XAML event)

## Risky (Manual Investigation)
1. `Services/PluginLoader.cs:56` - Public method `LoadPlugin()` (reflection target?)

## Recommendations
1. Remove 5 SAFE items first (create branch `cleanup/dead-code-batch1`)
2. Run full test suite after each removal
3. Investigate 2 RISKY items manually
4. Estimated time: 30 minutes
5. Estimated binary size reduction: ~15KB
```

## Integration with CI/CD

Can be integrated into GitHub Actions:
```yaml
# .github/workflows/dead-code-check.yml
name: Dead Code Analysis
on:
  schedule:
    - cron: '0 0 * * 0'  # Weekly on Sunday
  workflow_dispatch:

jobs:
  analyze:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Run dead code analysis
        run: |
          # Run agent analysis
          # Create issue if findings > threshold
```

## Success Metrics

After cleanup, verify:
- ✅ All tests still pass (262 passing)
- ✅ Desktop app builds successfully
- ✅ No new compiler warnings
- ✅ Binary size reduced (measure before/after)
- ✅ Code coverage % unchanged or improved (not decreased)

## Limitations

**Cannot detect**:
- Methods called via reflection (Assembly.GetType().GetMethod())
- XAML event handlers (unless explicitly searched)
- Dynamically generated code
- Methods used by external assemblies (plugins, tests in separate projects)

**Use manual review for**:
- Public APIs (could be used externally)
- Methods with specific attributes ([UsedImplicitly], [Export])
- WPF event handlers
- Interface implementations (might be DI-injected)

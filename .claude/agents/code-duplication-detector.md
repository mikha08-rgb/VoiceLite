# Code Duplication Detector Agent

## Purpose
Identifies duplicated code blocks, similar logic patterns, and opportunities for refactoring to improve maintainability and reduce technical debt.

## When to Use
- Before major releases (technical debt reduction)
- After rapid feature development (cleanup phase)
- During code reviews (identify refactoring opportunities)
- When bug fixes require changes in multiple places (DRY violation)

## Capabilities

1. **Exact Duplication**: Find identical code blocks (copy-paste detection)
2. **Structural Similarity**: Find code with same structure but different variables
3. **Logic Pattern Matching**: Identify similar algorithms with minor variations
4. **Cross-File Duplication**: Find duplicated code across multiple files
5. **Extract Method Opportunities**: Suggest helper methods for repeated patterns

## Detection Levels

### Level 1: Exact Duplicates (High Confidence)
```csharp
// File A
if (string.IsNullOrEmpty(settings.Language))
{
    settings.Language = "en";
}

// File B (EXACT DUPLICATE)
if (string.IsNullOrEmpty(settings.Language))
{
    settings.Language = "en";
}

// Action: Extract to helper method
private static string GetLanguageOrDefault(string language)
{
    return string.IsNullOrEmpty(language) ? "en" : language;
}
```

### Level 2: Structural Similarity (Medium Confidence)
```csharp
// File A
try
{
    await SaveSettingsAsync();
}
catch (Exception ex)
{
    ErrorLogger.LogError("SaveSettings", ex);
}

// File B (SIMILAR STRUCTURE)
try
{
    await LoadSettingsAsync();
}
catch (Exception ex)
{
    ErrorLogger.LogError("LoadSettings", ex);
}

// Action: Consider generic try-catch wrapper
private static async Task<T> ExecuteWithLoggingAsync<T>(
    string operation,
    Func<Task<T>> action)
{
    try
    {
        return await action();
    }
    catch (Exception ex)
    {
        ErrorLogger.LogError(operation, ex);
        throw;
    }
}
```

### Level 3: Logic Pattern (Low Confidence - Manual Review)
```csharp
// File A
public bool IsValidLicense(string license)
{
    if (string.IsNullOrEmpty(license)) return false;
    if (license.Length < 10) return false;
    if (!license.Contains("-")) return false;
    return true;
}

// File B (SIMILAR VALIDATION PATTERN)
public bool IsValidEmail(string email)
{
    if (string.IsNullOrEmpty(email)) return false;
    if (email.Length < 5) return false;
    if (!email.Contains("@")) return false;
    return true;
}

// Action: Consider generic validation framework
// BUT: These might be intentionally separate (domain-specific logic)
```

## Analysis Workflow

### Phase 1: Exact Duplicate Detection

```bash
# 1. Extract all methods from codebase
find VoiceLite/ -name "*.cs" -exec grep -A 20 "^\s*public\|^\s*private" {} \;

# 2. Hash method bodies (ignore whitespace/comments)
# Use MD5/SHA256 to identify identical blocks

# 3. Group by hash
# Report methods with same hash (exact duplicates)

# Example output:
# Hash: abc123def456
#   - VoiceLite/Services/AudioRecorder.cs:45-67
#   - VoiceLite/Services/PersistentWhisperService.cs:123-145
```

### Phase 2: Structural Similarity Detection

```bash
# 1. Parse C# AST (Abstract Syntax Tree)
# Tools: Roslyn analyzer, or regex patterns

# 2. Normalize code:
#    - Replace variable names with placeholders
#    - Remove comments/whitespace
#    - Standardize formatting

# 3. Compare normalized blocks
# Report similarity scores (>80% = likely duplicate)
```

### Phase 3: Manual Review & Refactoring

```bash
# For each duplicate:
# 1. Verify it's truly duplicated (not coincidentally similar)
# 2. Identify common abstraction
# 3. Propose refactoring (extract method, create base class, etc.)
# 4. Create TODO items for team
```

## Detection Patterns

### Pattern 1: Copy-Paste with Minor Changes
```csharp
// BEFORE: Duplicated validation in 3 places
public void ValidateSettings()
{
    if (settings == null)
        throw new ArgumentNullException(nameof(settings));
    if (string.IsNullOrEmpty(settings.Language))
        throw new ArgumentException("Language required");
    // ... more validation
}

public void SaveSettings()
{
    if (settings == null)
        throw new ArgumentNullException(nameof(settings));
    if (string.IsNullOrEmpty(settings.Language))
        throw new ArgumentException("Language required");
    // ... save logic
}

// AFTER: Extract common validation
private void ValidateSettings(Settings settings)
{
    if (settings == null)
        throw new ArgumentNullException(nameof(settings));
    if (string.IsNullOrEmpty(settings.Language))
        throw new ArgumentException("Language required");
}
```

### Pattern 2: Repeated Error Handling
```csharp
// BEFORE: Same try-catch in 10+ methods
public async Task<string> TranscribeAsync(string audioPath)
{
    try
    {
        // transcription logic
    }
    catch (Exception ex)
    {
        ErrorLogger.LogError("TranscribeAsync", ex);
        throw;
    }
}

// AFTER: Generic error handling wrapper
private async Task<T> WithErrorLoggingAsync<T>(
    string operationName,
    Func<Task<T>> operation)
{
    try
    {
        return await operation();
    }
    catch (Exception ex)
    {
        ErrorLogger.LogError(operationName, ex);
        throw;
    }
}

// Usage
public async Task<string> TranscribeAsync(string audioPath)
{
    return await WithErrorLoggingAsync(
        nameof(TranscribeAsync),
        async () => { /* transcription logic */ });
}
```

### Pattern 3: Similar Builders/Factories
```csharp
// BEFORE: Duplicated builder logic
public static ProcessStartInfo BuildWhisperProcess(string modelPath, string audioPath)
{
    return new ProcessStartInfo
    {
        FileName = whisperExePath,
        Arguments = $"-m \"{modelPath}\" -f \"{audioPath}\"",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };
}

public static ProcessStartInfo BuildFFmpegProcess(string inputPath, string outputPath)
{
    return new ProcessStartInfo
    {
        FileName = ffmpegExePath,
        Arguments = $"-i \"{inputPath}\" \"{outputPath}\"",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };
}

// AFTER: Generic process builder
public static ProcessStartInfo BuildProcess(
    string exePath,
    string arguments,
    bool redirectOutput = true)
{
    return new ProcessStartInfo
    {
        FileName = exePath,
        Arguments = arguments,
        UseShellExecute = false,
        RedirectStandardOutput = redirectOutput,
        RedirectStandardError = redirectOutput,
        CreateNoWindow = true
    };
}
```

## Tools & Techniques

### Manual Analysis (Simple)
```bash
# Find repeated code blocks (4+ lines)
# Simple heuristic: sort all 4-line blocks, find duplicates

# Example
find VoiceLite/ -name "*.cs" -exec \
  awk '/^[^\/]/ {line=$0; for(i=1;i<=3;i++) {getline; line=line"\n"$0} print line}' {} \; | \
  sort | uniq -d
```

### Roslyn Analyzer (Advanced)
```csharp
// Custom Roslyn analyzer to detect duplicates
// Can be integrated into build process
// Outputs warnings in Visual Studio
```

### Third-Party Tools
- **SonarQube**: Detects code clones (exact + structural)
- **NDepend**: Code duplication analysis with metrics
- **ReSharper**: Built-in duplicate code detection
- **PMD CPD**: Cross-language copy-paste detector

## Output Format

```markdown
# Code Duplication Report

## Summary
- Files analyzed: 87
- Total lines of code: 12,450
- Duplicated lines: 340 (2.7%)
- Duplicate blocks found: 12
- Refactoring opportunities: 5 high-priority

## High-Priority Duplicates (>20 lines)

### Duplicate 1: Error Logging Wrapper
**Severity**: HIGH (appears 8 times)
**Files**:
- `Services/AudioRecorder.cs:123-145` (23 lines)
- `Services/PersistentWhisperService.cs:234-256` (23 lines)
- `Services/TextInjector.cs:89-111` (23 lines)
- ... 5 more occurrences

**Code Sample**:
```csharp
try
{
    // operation logic
}
catch (Exception ex)
{
    ErrorLogger.LogError("OperationName", ex);
    throw;
}
```

**Recommendation**: Extract to `WithErrorLoggingAsync<T>()` helper method
**Estimated effort**: 30 minutes
**Impact**: -184 lines, improved maintainability

### Duplicate 2: Settings Validation
**Severity**: MEDIUM (appears 3 times)
**Files**:
- `Services/SettingsManager.cs:45-58` (14 lines)
- `MainWindow.xaml.cs:234-247` (14 lines)
- `SettingsWindow.xaml.cs:123-136` (14 lines)

**Code Sample**:
```csharp
if (settings == null)
    throw new ArgumentNullException(nameof(settings));
if (string.IsNullOrEmpty(settings.Language))
    throw new ArgumentException("Language required");
// ... more validation
```

**Recommendation**: Extract to `ValidateSettings()` method in `SettingsManager`
**Estimated effort**: 15 minutes
**Impact**: -28 lines, centralized validation logic

## Medium-Priority Duplicates (10-20 lines)

### Duplicate 3: ProcessStartInfo Builder
**Severity**: LOW (appears 2 times)
**Files**:
- `Services/PersistentWhisperService.cs:305-318` (14 lines)
- `Services/AudioPreprocessor.cs:156-169` (14 lines)

**Recommendation**: Extract to generic `BuildProcess()` helper
**Estimated effort**: 10 minutes
**Impact**: -14 lines

## False Positives (Keep As-Is)

### Similar Pattern: Disposal Methods
**Files**:
- `Services/AudioRecorder.cs:Dispose()` (12 lines)
- `Services/PersistentWhisperService.cs:Dispose()` (16 lines)

**Reason**: Similar structure, but dispose different resources
**Action**: NO REFACTORING (intentionally separate)

## Refactoring Plan

### Phase 1: High-Priority (1 hour)
1. Extract error logging wrapper → `WithErrorLoggingAsync<T>()`
2. Extract settings validation → `ValidateSettings()`
3. Run tests, verify no regressions

### Phase 2: Medium-Priority (30 min)
1. Extract process builder → `BuildProcess()`
2. Refactor 2 call sites
3. Run tests

### Phase 3: Verification
1. Re-run duplication analysis
2. Measure code reduction
3. Update documentation

## Metrics

**Before Refactoring**:
- Total lines: 12,450
- Duplicated lines: 340 (2.7%)

**After Refactoring (Estimated)**:
- Total lines: 12,224 (-226 lines)
- Duplicated lines: 114 (0.9%)
- Duplication reduction: 66%

## Success Criteria
- ✅ All tests pass after refactoring
- ✅ Code coverage unchanged or improved
- ✅ Duplication reduced below 1.5%
- ✅ No new compiler warnings
- ✅ Build time unchanged
```

## Safety Guidelines

1. **Verify tests exist**: Don't refactor code without test coverage
2. **Incremental changes**: Extract one duplicate at a time
3. **Semantic equivalence**: Ensure refactored code behaves identically
4. **Review with team**: Get buy-in on abstractions
5. **Preserve intent**: Don't over-abstract (YAGNI principle)

## When NOT to Refactor

1. **Domain-specific logic**: Similar code might represent different business rules
2. **Performance-critical paths**: Abstraction can add overhead
3. **Rare code paths**: If code is rarely executed, duplication is acceptable
4. **Clear intent**: Sometimes duplication is clearer than abstraction
5. **External dependencies**: Code that must match external APIs

## Integration with CI/CD

```yaml
# .github/workflows/duplication-check.yml
name: Code Duplication Analysis
on:
  pull_request:
    branches: [master]
  schedule:
    - cron: '0 0 1 * *'  # Monthly

jobs:
  analyze:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Run PMD CPD
        run: |
          wget https://github.com/pmd/pmd/releases/download/pmd_releases%2F6.55.0/pmd-bin-6.55.0.zip
          unzip pmd-bin-6.55.0.zip
          ./pmd-bin-6.55.0/bin/run.sh cpd \
            --minimum-tokens 50 \
            --files VoiceLite/VoiceLite \
            --language cs \
            --format markdown > duplication-report.md

      - name: Comment on PR
        uses: actions/github-script@v6
        with:
          script: |
            const fs = require('fs');
            const report = fs.readFileSync('duplication-report.md', 'utf8');
            github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body: report
            });
```

## Best Practices

1. **Rule of Three**: Don't extract until duplication appears 3+ times
2. **Clear abstractions**: Extracted methods should have clear, single purpose
3. **Minimal parameters**: If extraction requires 5+ parameters, reconsider
4. **Testable**: Extracted code should be independently testable
5. **Document intent**: Add comments explaining why extraction was done

## VoiceLite-Specific Patterns

Common duplication sources in this codebase:
- Error logging wrappers (Services/)
- ProcessStartInfo builders (Whisper integration)
- Settings validation (multiple windows)
- Null checks and argument validation
- Dispose patterns (resource cleanup)

**High-Value Refactorings**:
1. Generic error logging decorator
2. Settings validation framework
3. Process builder utility class
4. Dispose pattern base class

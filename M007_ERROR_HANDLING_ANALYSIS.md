# M-007: Error Handling Standardization - Analysis & Plan

**Date**: 2025-10-30
**Effort**: ~6 hours
**Priority**: Medium (sets foundation for H-002 MVVM extraction)
**Branch**: test-reliability-improvements

---

## Executive Summary

analyzed 4 core services (AudioRecorder, PersistentWhisperService, TextInjector, LicenseService) + ErrorLogger infrastructure.

**key findings:**
- ErrorLogger API: robust, thread-safe, level-based filtering (7 methods)
- inconsistencies: mix of LogError/LogWarning/LogMessage, throw vs return patterns, silent catches
- test coverage: strong for happy path, gaps in error scenarios
- impact: ~50 error handling locations need standardization

**outcome after M-007:**
- consistent throw/return patterns across services
- standardized ErrorLogger usage (Error/Warning/Message/Debug hierarchy)
- zero silent catches (all errors logged)
- clear user-facing error messages
- foundation for clean H-002 MVVM extraction

---

## ErrorLogger API Analysis

### Available Methods

```csharp
// CRITICAL ERRORS (always logged, includes stack trace)
ErrorLogger.LogError(string context, Exception ex)
// Example: ErrorLogger.LogError("TranscribeAsync failed", ex);

// WARNINGS (logged in Release, for expected but problematic situations)
ErrorLogger.LogWarning(string message)
// Example: ErrorLogger.LogWarning("VAD model not found, continuing without VAD");

// INFO (logged based on MinimumLogLevel)
ErrorLogger.LogMessage(string message)
ErrorLogger.Log(LogLevel.Info, message)

// DEBUG (only in DEBUG builds)
ErrorLogger.LogDebug(string message)
ErrorLogger.Log(LogLevel.Debug, message)
```

### Current Issues

1. **Inconsistent severity mapping:**
   - Some critical errors logged as LogMessage (AudioRecorder.cs:147)
   - Expected warnings logged as LogError (over-logging)
   - Mix of LogMessage vs LogInfo

2. **Context missing:**
   - Some LogError calls lack descriptive context
   - Stack traces lost when using LogWarning for exceptions

3. **Level confusion:**
   - LogMessage used for warnings (should use LogWarning)
   - LogDebug not used consistently for verbose output

---

## Current State by Service

### 1. AudioRecorder.cs (750 LOC)

**Error Handling Locations:** ~18

#### ✅ Good Patterns

```csharp
// Line 264: Clear error message, actionable
throw new InvalidOperationException("No microphone detected. Please connect a microphone and try again.");

// Lines 316-327: Comprehensive error handling with cleanup
catch (Exception ex)
{
    CleanupRecordingState();
    isRecording = false;

    if (ex.Message.Contains("device") || ex.Message.Contains("audio"))
    {
        throw new InvalidOperationException("Failed to access the microphone...", ex);
    }
    throw;
}

// Line 672-673: Validation returns false (not critical)
if (deviceCount == 0)
{
    RecordingError?.Invoke(this, new InvalidOperationException("No audio recording devices found"));
    return false;
}
```

#### ⚠️ Inconsistencies

```csharp
// Line 101: Should use LogWarning (expected failure, not error)
ErrorLogger.LogWarning($"Failed to delete old audio file {file}: {deleteEx.Message}");
// ✓ CORRECT (already using LogWarning)

// Line 147: Should use LogError (includes exception)
ErrorLogger.LogMessage($"DisposeWaveInCompletely: Error stopping recording - {ex.Message}");
// ✗ WRONG - should be: ErrorLogger.LogError("DisposeWaveInCompletely", ex);

// Lines 98-103: Silent catch in CleanupStaleAudioFiles
catch (Exception deleteEx)
{
    // Log but don't fail cleanup for individual file errors
    ErrorLogger.LogWarning($"Failed to delete old audio file {file}: {deleteEx.Message}");
}
// ✗ PARTIAL - loses stack trace, should log full exception for debugging
```

**Pattern Inconsistencies:**
- 6 locations: LogMessage used for errors (should be LogError with exception)
- 3 locations: Exception info logged as string (loses stack trace)
- 2 locations: Silent catch blocks (no logging)

---

### 2. PersistentWhisperService.cs (~900 LOC)

**Error Handling Locations:** ~25

#### ✅ Good Patterns

```csharp
// Line 730-733: Clear throw on critical error
if (isDisposed)
    throw new ObjectDisposedException(nameof(PersistentWhisperService));

if (!File.Exists(audioFilePath))
    throw new FileNotFoundException($"Audio file not found: {audioFilePath}");

// Line 354-356: Proper error logging + rethrow
catch (Exception ex)
{
    ErrorLogger.LogError("PersistentWhisperService.TranscribeAsync", ex);
    throw;
}

// Lines 449-450: Descriptive exception with context
throw new ExternalException($"Whisper process failed with exit code {process.ExitCode}", process.ExitCode);
```

#### ⚠️ Inconsistencies

```csharp
// Line 238-240: Inconsistent - catches exception but only logs as message
catch (Exception ex)
{
    ErrorLogger.LogError("WarmUpWhisperAsync", ex);
}
// ✓ CORRECT (using LogError)

// Lines 827-830: Silent catch (returns Unknown on error)
catch (Exception)
{
    return "Unknown";
}
// ✗ WRONG - should log exception before returning

// Line 304: Fallback but logs as error (should be warning)
ErrorLogger.LogError("Preprocessing failed, continuing with unprocessed audio", preprocessEx);
// ✗ WRONG - should be LogWarning (expected fallback behavior)

// Line 476: Warning logged for exception (loses stack trace)
ErrorLogger.LogWarning($"Text post-processing failed, using raw output: {postProcessEx.Message}");
// ✗ PARTIAL - should use LogError or include full exception details
```

**Pattern Inconsistencies:**
- 4 locations: Silent or inadequate logging (GetWhisperVersion, various catches)
- 3 locations: LogWarning used for exceptions (loses stack trace)
- 2 locations: LogError used for expected fallbacks (should be LogWarning)

---

### 3. TextInjector.cs (652 LOC)

**Error Handling Locations:** ~15

#### ✅ Good Patterns

```csharp
// Line 37: Constructor validation
this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

// Lines 101-116: Comprehensive fallback chain with logging
catch (Exception ex)
{
    ErrorLogger.LogError("TextInjector.InjectText", ex);
    // Fallback to clipboard method if typing fails
    try
    {
        PasteViaClipboard(text);
    }
    catch
    {
        throw new InvalidOperationException($"Failed to inject text: {ex.Message}", ex);
    }
}

// Lines 406-409: Proper error logging with fallback decision
catch (Exception ex)
{
    ErrorLogger.LogMessage($"BUG-007: Failed to check current clipboard: {ex.Message}");
    clipboardCheckFailed = true; // Restore anyway - better safe than sorry
}
```

#### ⚠️ Inconsistencies

```csharp
// Lines 354-365: Conditional logging via preprocessor
catch (Exception
#if DEBUG
ex
#endif
)
{
#if DEBUG
    ErrorLogger.LogMessage($"Clipboard read attempt {attempt + 1} failed: {ex.Message}");
#endif
    if (attempt < 1)
        Thread.Sleep(CLIPBOARD_RETRY_DELAY_MS);
}
// ✗ WRONG - errors should be logged in both DEBUG and RELEASE

// Lines 201-204: Silent catch (no logging)
catch
{
    return false;
}
// ✗ WRONG - should log exception before returning

// Line 568-572: Good error handling but uses LogError then rethrows
catch (Exception ex)
{
    ErrorLogger.LogError("SimulateCtrlV failed", ex);
    throw;
}
// ✓ CORRECT (LogError + rethrow is acceptable pattern)
```

**Pattern Inconsistencies:**
- 6 locations: Conditional logging (#if DEBUG) - should log in Release too
- 2 locations: Silent catch blocks
- 3 locations: LogMessage used for warnings (should be LogWarning)

---

### 4. LicenseService.cs (317 LOC)

**Error Handling Locations:** ~8

#### ✅ Good Patterns

```csharp
// Lines 200-215: Proper error handling with fallback
catch (HttpRequestException ex)
{
    ErrorLogger.LogError("License validation HTTP error", ex);

    // Try to use cached result even on network failure
    if (_cachedValidationResult != null && _storedLicenseKey == licenseKey.Trim())
    {
        ErrorLogger.LogWarning("Network error - using cached license validation result");
        return _cachedValidationResult;
    }

    return new LicenseValidationResult
    {
        IsValid = false,
        ErrorMessage = "Connection error. Please check your internet connection."
    };
}

// Lines 125-134: Comprehensive retry logging
ErrorLogger.LogWarning(
    $"License API unreachable after 3 retries. Using cached result (lifetime license). " +
    $"Exception: {retryEx.Message}"
);
```

#### ⚠️ Inconsistencies

```csharp
// Lines 232-239: Catch-all Exception (too broad)
catch (Exception ex)
{
    ErrorLogger.LogError("License validation failed", ex);
    return new LicenseValidationResult
    {
        IsValid = false,
        ErrorMessage = $"Validation error: {ex.Message}"
    };
}
// ⚠️ ACCEPTABLE but could be more specific (TaskCanceledException, JsonException, etc.)
```

**Pattern Inconsistencies:**
- 1 location: Overly broad catch (acceptable for API boundary)
- Generally consistent (best of the 4 services)

---

## Identified Inconsistencies (Summary)

### Critical Issues (Fix First)

1. **Silent Catches** (8 locations)
   - AudioRecorder: 2 locations (disposal cleanup)
   - PersistentWhisperService: 3 locations (GetWhisperVersion, etc.)
   - TextInjector: 2 locations (IsInSecureField, etc.)
   - LicenseService: 1 location (Dispose)

2. **Lost Stack Traces** (12 locations)
   - Logging `ex.Message` instead of full exception
   - Using LogWarning/LogMessage for exceptions (should use LogError)

3. **Conditional Logging** (6 locations in TextInjector)
   - `#if DEBUG` blocks prevent Release build diagnostics
   - Critical errors hidden from production logs

### Medium Issues (Fix Second)

4. **Inconsistent Severity** (15 locations)
   - LogMessage used for warnings (should be LogWarning)
   - LogError used for expected fallbacks (should be LogWarning)
   - LogDebug not used for verbose diagnostics

5. **Unclear Error Messages** (8 locations)
   - Generic "Error occurred" messages
   - Missing context about operation or state
   - No actionable guidance for users

6. **Inconsistent Return vs Throw** (6 locations)
   - Similar validation methods use different patterns
   - No clear rule about when to throw vs return

### Low Priority Issues

7. **Overly Broad Catches** (4 locations)
   - `catch (Exception ex)` where specific types expected
   - Acceptable at API boundaries, should be specific elsewhere

---

## Standardized Patterns to Apply

### 1. Logging Decision Tree

```
Is this an exception object?
├─ YES → Use ErrorLogger.LogError(context, ex)
└─ NO → Is this expected/recoverable?
    ├─ YES → Use ErrorLogger.LogWarning(message)
    └─ NO → Use ErrorLogger.LogMessage(message) or LogDebug(message)
```

### 2. Throw vs Return Decision Tree

```
Is this a critical error (no recovery possible)?
├─ YES → THROW specific exception with context
│         Examples: missing dependencies, invalid state, disposed object
└─ NO → Is this validation/detection failure?
    ├─ YES → RETURN empty/false, but LOG WARNING
    │         Examples: no microphone, empty file, secure field detection
    └─ NO → RETURN result normally
```

### 3. Catch Block Template

```csharp
// GOOD: Specific exception, full logging, proper cleanup
try
{
    // operation
}
catch (FileNotFoundException ex)
{
    ErrorLogger.LogError("Operation context", ex);
    return false; // or throw, depending on criticality
}
catch (IOException ex)
{
    ErrorLogger.LogError("Operation context", ex);
    // fallback logic
}
catch (Exception ex)
{
    ErrorLogger.LogError("Operation context - unexpected error", ex);
    throw; // or return default after logging
}
finally
{
    // cleanup (always runs)
}
```

### 4. Error Message Template

```csharp
// User-facing (thrown exceptions)
throw new InvalidOperationException(
    "Clear problem statement. " +
    "What to try:\n" +
    "1. Actionable step 1\n" +
    "2. Actionable step 2",
    innerException
);

// Developer-facing (logs)
ErrorLogger.LogError("ComponentName.MethodName: What failed and why", ex);
ErrorLogger.LogWarning($"Component.Method: Falling back to X because {reason}");
```

### 5. Disposal Pattern

```csharp
public void Dispose()
{
    if (isDisposed)
        return;

    isDisposed = true; // Set FIRST

    try
    {
        // Cleanup logic
    }
    catch (Exception ex)
    {
        ErrorLogger.LogError("Dispose cleanup failed", ex);
        // Continue disposal - don't throw from Dispose
    }

    GC.SuppressFinalize(this);
}

// In other methods:
if (isDisposed)
    throw new ObjectDisposedException(nameof(ServiceName));
```

---

## Implementation Plan

### Phase 1: AudioRecorder.cs (~2 hours)

**Changes:** 11 error handling improvements

1. **Replace LogMessage with LogError** (6 locations)
   - Lines 147, 158, 175, 205, 553, 556
   - Pattern: `LogMessage($"Error: {ex.Message}")` → `LogError("Context", ex)`

2. **Add logging to silent catches** (2 locations)
   - Lines 98-103 (CleanupStaleAudioFiles)
   - Pattern: Add `ErrorLogger.LogWarning($"Context: {ex.Message}")` before continue

3. **Improve error messages** (3 locations)
   - Lines 264, 285, 324
   - Add suggested actions to exception messages

**Testing:**
- Run AudioRecorderTests.cs (verify all 150+ tests pass)
- Manual test: Start/stop recording, device errors, disposal

---

### Phase 2: PersistentWhisperService.cs (~2 hours)

**Changes:** 9 error handling improvements

1. **Add logging to silent catches** (3 locations)
   - Line 827-830 (GetWhisperVersion)
   - Line 228 (SetPriority)
   - Line 623 (SetPriorityClass)
   - Pattern: Add `ErrorLogger.LogWarning("Context", ex)` before return

2. **Replace LogWarning with LogError for exceptions** (3 locations)
   - Line 304 (preprocessEx - keep as warning, is fallback)
   - Line 476 (postProcessEx - keep as warning, is fallback)
   - Verify logging consistency throughout

3. **Improve timeout error messages** (2 locations)
   - Lines 558-569 (first-run timeout)
   - Line 572 (general timeout)
   - Already good, verify actionable

4. **Add missing context** (1 location)
   - Line 330-333 (diagnostic logging failures)
   - Already silent by design (OK)

**Testing:**
- Run WhisperErrorRecoveryTests.cs (verify error scenarios)
- Run WhisperServiceTests.cs (verify happy path unchanged)

---

### Phase 3: TextInjector.cs (~1.5 hours)

**Changes:** 11 error handling improvements

1. **Remove conditional logging** (6 locations)
   - Lines 354-365, 429-446 (clipboard operations)
   - Pattern: Remove `#if DEBUG`, log in both builds
   - Use LogDebug for verbose, LogWarning for errors

2. **Add logging to silent catches** (2 locations)
   - Lines 201-204 (IsInSecureField)
   - Pattern: Add `ErrorLogger.LogDebug("Secure field detection failed")` before return

3. **Improve fallback logging** (3 locations)
   - Lines 103-116 (already good)
   - Lines 406-409 (already good)
   - Verify consistency

**Testing:**
- Run TextInjectorTests.cs (verify injection modes work)
- Manual test: Type vs paste, secure fields, clipboard restore

---

### Phase 4: LicenseService.cs (~0.5 hours)

**Changes:** 2 error handling improvements

1. **Verify catch specificity** (1 location)
   - Lines 232-239 (catch Exception)
   - DECISION: Keep as-is (API boundary acceptable)

2. **Add missing disposal logging** (1 location)
   - Lines 286-296 (Dispose)
   - Already has comment, no logging needed (intentional static HttpClient)

**Testing:**
- Run license validation tests
- Manual test: Valid/invalid keys, network errors, cached results

---

### Phase 5: Documentation & Testing (~1 hour)

1. **Update error handling guidelines** (20 min)
   - Add to CLAUDE.md or create ERROR_HANDLING_GUIDE.md
   - Document decision trees and templates

2. **Create error scenario tests** (40 min)
   - Add missing test coverage for error paths
   - Verify all exceptions logged correctly
   - Test error message clarity

3. **Final verification** (10 min)
   - Run full test suite: `dotnet test`
   - Build Release config (verify no warnings)
   - Review error logs from manual testing

---

## Testing Strategy

### Automated Testing

```bash
# Run all tests
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj

# Run error-specific tests
dotnet test --filter "Category=ErrorHandling|FullyQualifiedName~ErrorRecovery"

# Expected: 311/311 passing (maintain current pass rate)
```

### Manual Verification Checklist

**AudioRecorder:**
- [ ] Start recording without microphone → clear error message
- [ ] Stop recording after disposal → logs but doesn't crash
- [ ] Device change during recording → proper error logged

**PersistentWhisperService:**
- [ ] Transcribe non-existent file → logs error, throws FileNotFoundException
- [ ] Timeout scenario → user-friendly message with steps
- [ ] Empty audio file → returns empty string, logs warning

**TextInjector:**
- [ ] Inject text with clipboard locked → logs error, falls back
- [ ] Secure field detection → logs debug info (visible in DEBUG)
- [ ] Clipboard restore after user copies → logs reasoning

**LicenseService:**
- [ ] Validate with network down → logs error, uses cache if available
- [ ] Invalid license key → clear user message
- [ ] Activation limit hit → actionable error message

### Log Review

Check `%LOCALAPPDATA%\VoiceLite\logs\voicelite.log`:
- [ ] All errors have stack traces (ErrorLogger.LogError used)
- [ ] Warnings don't include stack traces (LogWarning for expected issues)
- [ ] No "silent" errors (every catch has logging)
- [ ] Error context is clear (operation, component, state)

---

## Risk Assessment

### Low Risk Changes (80% of work)

- Adding logging to silent catches
- Changing LogMessage → LogWarning/LogError
- Improving error message text
- Removing #if DEBUG conditional logging

**Mitigation:** Comprehensive test coverage (311 tests) catches regressions

### Medium Risk Changes (15% of work)

- Changing throw vs return patterns
- Modifying exception types thrown
- Changing fallback behavior

**Mitigation:** Manual testing of error scenarios, verify callers handle exceptions correctly

### High Risk Changes (5% of work)

- None identified (all changes are refinements, not behavior changes)

---

## Success Criteria

### Code Quality

- [ ] Zero silent catch blocks (all have logging)
- [ ] Consistent ErrorLogger usage (Error for exceptions, Warning for expected issues)
- [ ] All error messages include context
- [ ] User-facing errors are actionable
- [ ] No #if DEBUG conditional error logging

### Testing

- [ ] All existing tests pass (311/311)
- [ ] No new warnings in Release build
- [ ] Error logs reviewed and verified clear
- [ ] Manual error scenarios tested

### Documentation

- [ ] Error handling patterns documented
- [ ] Decision trees added to CLAUDE.md or separate guide
- [ ] Examples provided for common scenarios

---

## Post-M-007 Benefits

1. **Easier debugging:** Consistent logging makes issues easier to diagnose
2. **Better UX:** Clear error messages guide users to solutions
3. **Cleaner H-002:** MVVM extraction simpler with consistent error patterns
4. **Maintainability:** New code follows established patterns
5. **Production confidence:** All errors logged in Release builds

---

## Time Estimate Breakdown

| Phase | Task | Time |
|-------|------|------|
| 1 | AudioRecorder fixes | 2h |
| 2 | PersistentWhisperService fixes | 2h |
| 3 | TextInjector fixes | 1.5h |
| 4 | LicenseService verification | 0.5h |
| 5 | Documentation + testing | 1h |
| **Total** | | **~7h** |

**Updated estimate:** 7 hours (was 6h in handoff doc) due to deeper analysis revealing more locations

---

## Next Steps

1. **Review this analysis** - confirm approach before implementation
2. **Create branch checkpoint** - commit "M-007: error handling analysis"
3. **Implement Phase 1** - AudioRecorder.cs fixes
4. **Run tests after each phase** - catch regressions early
5. **Final review** - verify all criteria met before merge

---

**Ready to begin implementation?** Confirm approach or request modifications to plan.

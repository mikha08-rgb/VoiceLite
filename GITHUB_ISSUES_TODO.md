# GitHub Issues - Technical Debt from Code Review

Generated: 2025-10-29
Source: Architecture review and TODO scan

## Priority: Medium

### Issue 1: Add CancelCurrentTranscriptionAsync to ITranscriptionController
**File**: `VoiceLite/VoiceLite/Presentation/ViewModels/MainViewModel.cs:355`
**Type**: Enhancement
**Description**: Interface is missing async cancellation method
**Impact**: Cannot properly cancel in-progress transcriptions from ViewModels
**Effort**: Low (~2 hours)

```csharp
// Current workaround in MainViewModel.cs:355
// TODO: Add CancelCurrentTranscriptionAsync to ITranscriptionController
```

**Suggested Implementation**:
```csharp
public interface ITranscriptionController
{
    Task CancelCurrentTranscriptionAsync();
    // ... other methods
}
```

---

### Issue 2: Implement Audio Test Feature
**File**: `VoiceLite/VoiceLite/Presentation/ViewModels/SettingsViewModel.cs:520`
**Type**: Feature
**Description**: Settings UI has placeholder for audio test functionality
**Impact**: Users cannot verify microphone before recording
**Effort**: Medium (~4 hours)

**User Story**: As a user, I want to test my microphone in Settings to ensure it's working before I start transcribing.

**Acceptance Criteria**:
- Button plays back recorded audio or shows waveform
- Visual indication of audio levels
- Tests selected microphone device

---

### Issue 3: Implement Windows Startup Registration
**File**: `VoiceLite/VoiceLite/Presentation/ViewModels/SettingsViewModel.cs:613`
**Type**: Feature
**Description**: "Run at Windows startup" checkbox not functional
**Impact**: Users must manually start app each time
**Effort**: Low (~3 hours)

**Technical Notes**:
- Add registry key: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- Or use Task Scheduler for better reliability
- Requires elevated permissions prompt

**Reference**: Windows startup best practices for desktop apps

---

### Issue 4: Implement Secure Storage for License Keys
**File**: `VoiceLite/VoiceLite/Services/LicenseService.cs:256`
**Type**: Security
**Description**: License keys currently stored in plain text (in-memory only)
**Impact**: License keys not persisted between sessions (minor inconvenience)
**Effort**: Medium (~4 hours)

**Current Behavior**: User must re-enter license key after restart

**Suggested Approach**:
1. Use Windows Credential Manager (CredentialManagement API)
2. Or: Windows Data Protection API (DPAPI)
3. Or: Encrypted settings file with machine-specific key

**References**:
- https://docs.microsoft.com/en-us/windows/win32/api/wincred/
- https://www.nuget.org/packages/CredentialManagement/

---

### Issue 5: Investigate TextPattern for UI Automation Fallback
**File**: `VoiceLite/VoiceLite/Services/TextInjector.cs:240`
**Type**: Performance Optimization
**Description**: Potential performance improvement for text injection
**Impact**: Low (current implementation works well)
**Effort**: Medium (~6 hours research + implementation)
**Priority**: Low - "Nice to have"

**Context**: Current UI Automation fallback works but is slower than clipboard paste. TextPattern might be faster for direct text insertion in supported controls.

**Research Tasks**:
- Benchmark TextPattern vs current approach
- Test compatibility with major apps (Chrome, Word, VS Code)
- Assess stability and error handling

---

## Summary

| Priority | Count | Est. Total Effort |
|----------|-------|-------------------|
| High     | 0     | 0 hours          |
| Medium   | 4     | 13 hours         |
| Low      | 1     | 6 hours          |
| **Total**| **5** | **19 hours**     |

## Recommended Action

Create 4 GitHub issues immediately (skip #5 for now):
1. CancelCurrentTranscriptionAsync interface method
2. Audio test feature in Settings
3. Windows startup registration
4. Secure license key storage

Keep Issue #5 (TextPattern investigation) as future optimization.

---

## Notes

- All TODOs in production code have been documented
- Pro feature TODOs were cleaned up (visibility infrastructure remains)
- Timeout constants extracted (no more magic numbers)
- xUnit test warnings fixed (async/await patterns)

## Next Steps

1. Create issues in GitHub with labels: `enhancement`, `good first issue`, `performance`
2. Prioritize based on user feedback
3. Consider for next sprint planning

# Week 1 Manual Verification Checklist

## Quick Test (5 minutes)

### 1. Build Test
```powershell
dotnet build VoiceLite/VoiceLite.sln -c Release
```
**Expected:** Build succeeds with 0 errors

### 2. Run Tests
```powershell
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj
```
**Expected:** All tests pass (or at least compile)

### 3. Check Key Files Exist
- [ ] `VoiceLite/VoiceLite/Helpers/AsyncHelper.cs`
- [ ] `VoiceLite/VoiceLite.Tests/Resources/ResourceLeakTests.cs`
- [ ] `VoiceLite/VoiceLite.Tests/Integration/EndToEndTests.cs`

## Detailed Test (15 minutes)

### 1. Test HttpClient Fix
Run this test specifically:
```powershell
dotnet test --filter "LicenseService_MultipleInstances_ShouldNotCreateMultipleHttpClients"
```
**Expected:** Test passes (HttpClient is now static)

### 2. Test Timer Management
Run the app and:
1. Start recording (Alt+R or button)
2. Stop recording
3. Repeat 10 times rapidly
4. Check Task Manager - memory should be stable

**Expected:** No memory growth, no crashes

### 3. Test Async Safety
Run the app and:
1. Click buttons rapidly
2. Close during transcription
3. Start/stop recording while transcribing

**Expected:** App doesn't crash, shows error messages gracefully

### 4. Memory Leak Test
```powershell
# Run stress test
$env:RUN_STRESS_TESTS="true"
dotnet test --filter "StressTest"
```
**Expected:** Memory stays under control

## What to Look For

### ✅ GOOD Signs:
- Build completes without errors
- Tests run (even if some fail)
- App starts without immediate crash
- Memory doesn't grow continuously
- Errors show messages instead of crashing

### ❌ BAD Signs:
- Build fails
- App crashes on startup
- Memory grows rapidly
- Tests won't even compile
- Unhandled exceptions

## Quick Fix Guide

### If build fails:
```powershell
# Check for missing using statements
dotnet build VoiceLite/VoiceLite.sln --verbosity detailed
```

### If tests fail:
```powershell
# Run specific test for details
dotnet test --filter "TestName" --logger "console;verbosity=detailed"
```

### If app crashes:
1. Check Event Viewer for crash details
2. Run in Visual Studio debugger
3. Check `%LOCALAPPDATA%\VoiceLite\logs\voicelite.log`

## Verification Results

Run one of these commands:

**PowerShell (Recommended):**
```powershell
.\TEST_WEEK1.ps1
```

**Command Prompt:**
```cmd
TEST_WEEK1.bat
```

**Manual:**
Follow the checklist above and mark each item.

---

## When You're Ready

If all tests pass:
✅ Week 1 is working! Continue to Week 2

If tests fail:
❌ Let's fix the issues before continuing

Common issues:
1. **Missing AsyncHelper** - The using statement might be wrong
2. **HttpClient not static** - Check if changes were saved
3. **Tests not found** - Check if test files are in the project
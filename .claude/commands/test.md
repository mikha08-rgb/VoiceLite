---
description: Run VoiceLite tests with coverage analysis
---

# Run VoiceLite Tests

Execute comprehensive test suite:

1. Check if VoiceLite.exe is running (kill if needed)
2. Run dotnet test with coverage:
   ```
   dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj --collect:"XPlat Code Coverage"
   ```
3. Verify coverage targets:
   - Overall: ≥75%
   - Services/: ≥80%
4. Report any failing tests with details
5. Highlight tests related to recent changes
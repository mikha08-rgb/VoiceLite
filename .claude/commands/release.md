---
description: Create a new VoiceLite release with proper versioning
argument-hint: <version>
---

# Release VoiceLite $1

Update version to $1 and prepare for release:

1. Update version in VoiceLite/VoiceLite/VoiceLite.csproj
2. Update version in voicelite-web/package.json
3. Run all tests
4. Build installer with Inno Setup
5. Create git tag v$1
6. Push tag to trigger GitHub Actions
7. Verify GitHub Actions workflow succeeds
8. Update download links on website
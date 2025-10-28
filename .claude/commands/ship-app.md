---
description: Ship new desktop app version (auto-increments, builds installer, tags, pushes)
argument-hint: [--force]
---

# Ship Desktop App

Automate the entire desktop app release process:

## What This Does

1. **Checks git status** - Aborts if uncommitted changes (unless --force flag used)
2. **Reads current version** from VoiceLite/VoiceLite/VoiceLite.csproj
3. **Auto-increments patch version** (e.g., 1.1.51 → 1.1.52)
4. **Test builds first** - Runs dotnet build to catch errors before changing anything
5. **Updates version in 2 files (5 locations)**:
   - VoiceLite/VoiceLite/VoiceLite.csproj (3 locations: Version, AssemblyVersion, FileVersion)
   - VoiceLite/Installer/VoiceLiteSetup.iss (2 locations: AppVersion, OutputBaseFilename)
6. **Builds installer** with Inno Setup
7. **Commits version changes** with message "chore: Bump version to X.X.X"
8. **Creates git tag** (e.g., v1.1.52)
9. **Pushes to GitHub** (triggers GitHub Actions to build & release)

**NOTE**: This does NOT update website download links. Use `/update-download-links <version>` after GitHub release is live (~5-7 min).

## Usage

```bash
# Normal (aborts if uncommitted changes)
/ship-app

# Force (ships even with uncommitted changes)
/ship-app --force
```

## Implementation Steps

Execute these steps in order, stopping on any error:

### 1. Check for --force flag
- Parse arguments to see if "--force" is present
- Store in variable for later use

### 2. Check git status
```bash
git status --porcelain
```
- If output is not empty AND --force NOT used:
  - Print error: "❌ Error: Uncommitted changes detected. Commit them first, or use /ship-app --force"
  - List the uncommitted files
  - ABORT
- If uncommitted changes AND --force used:
  - Print warning: "⚠️  Warning: Shipping with uncommitted changes (--force used)"

### 3. Read current version
- Read VoiceLite/VoiceLite/VoiceLite.csproj
- Extract version from `<Version>X.X.X</Version>` line using regex: `<Version>(\d+\.\d+\.\d+)</Version>`
- Validate format is X.X.X (three numbers separated by dots)
- If invalid format:
  - Print error: "❌ Error: Invalid version format in VoiceLite.csproj. Expected X.X.X, found: {version}"
  - ABORT
- Parse into major.minor.patch
- Store current version
- Print: "✓ Current version: {version}"

### 4. Auto-increment version
- Increment patch number by 1
- New version = major.minor.(patch+1)
- Print: "✓ Current version: X.X.X → Bumping to Y.Y.Y"

### 5. Test build first (BEFORE changing any files)
```bash
dotnet clean VoiceLite/VoiceLite.sln
dotnet build VoiceLite/VoiceLite.sln -c Release
```
- If build fails:
  - Print error: "❌ Build failed! Fix errors before shipping."
  - Show build output
  - ABORT (no files have been changed yet)
- If build succeeds:
  - Print: "✓ Test build succeeded"

### 6. Update version in all files

**File 1: VoiceLite/VoiceLite/VoiceLite.csproj (3 updates)**
- Update `<Version>X.X.X</Version>` to new version
- Update `<AssemblyVersion>X.X.X.0</AssemblyVersion>` to new version + ".0"
- Update `<FileVersion>X.X.X.0</FileVersion>` to new version + ".0"

**File 2: VoiceLite/Installer/VoiceLiteSetup.iss (2 updates)**
- Update `AppVersion=X.X.X` to new version
- Update `OutputBaseFilename=VoiceLite-Setup-X.X.X` to new version

Print: "✓ Updated version in all files"

### 7. Publish and build installer
```bash
dotnet publish VoiceLite/VoiceLite/VoiceLite.csproj -c Release -r win-x64 --self-contained
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" VoiceLite/Installer/VoiceLiteSetup.iss
```
- If dotnet publish fails:
  - Print error: "❌ Publish failed!"
  - Show error output
  - ABORT
- If ISCC.exe not found:
  - Print error: "❌ Inno Setup not found at C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
  - Print: "Install from: https://jrsoftware.org/isdl.php"
  - ABORT
- If ISCC fails:
  - Print error: "❌ Installer build failed!"
  - Show error output
  - ABORT
- If succeeds:
  - Expected installer location: `VoiceLite-Setup-{version}.exe` (root directory)
  - Verify file exists using exact path
  - If not found:
    - Print error: "❌ Installer file not found at expected location"
    - ABORT
  - Get file size in MB
  - Print: "✓ Installer built: VoiceLite-Setup-{version}.exe ({size} MB)"

### 8. Commit version changes
```bash
git add VoiceLite/VoiceLite/VoiceLite.csproj VoiceLite/Installer/VoiceLiteSetup.iss
git commit -m "chore: Bump version to X.X.X"
```
- Print: "✓ Committed version bump"

### 9. Create git tag
```bash
git tag vX.X.X
```
- Print: "✓ Created tag vX.X.X"

### 10. Push to GitHub
```bash
git push
git push --tags
```
- Print: "✓ Pushed to GitHub"
- Print: "✓ GitHub Actions will build and release in ~5-7 minutes"

### 11. Final instructions
Print:
```
✅ SHIP COMPLETE!

Desktop app v{version} is now shipping:
- ✓ Installer built locally: VoiceLite-Setup-{version}.exe
- ✓ Tag v{version} pushed to GitHub
- ✓ GitHub Actions triggered

Next steps:
1. Monitor GitHub Actions: https://github.com/mikha08-rgb/VoiceLite/actions
   (Build typically takes 5-7 minutes)

2. Once workflow succeeds, verify release:
   https://github.com/mikha08-rgb/VoiceLite/releases/tag/v{version}

3. After release is live, update website:
   /update-download-links {version}

⚠️  IMPORTANT: Website download links NOT updated yet
   (This prevents users getting 404 errors while GitHub Actions builds)
```

## Error Handling

- At ANY step failure, STOP immediately and report error
- Do NOT continue if build fails
- Do NOT update files if test build fails
- Do NOT push if local build fails

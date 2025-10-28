---
description: Ship new website version (auto-increments, deploys to Vercel)
argument-hint: [--force]
---

# Ship Website

Automate the website release process.

## What This Does

1. **Checks git status** - Aborts if uncommitted changes (unless --force)
2. **Reads current version** from voicelite-web/package.json
3. **Auto-increments patch version** (e.g., 0.1.0 → 0.1.1)
4. **Updates package.json** with new version
5. **Commits version changes** with message "chore: Bump web version to X.X.X"
6. **Creates git tag** (e.g., web-v0.1.1)
7. **Pushes to GitHub**
8. **Deploys to Vercel** (production)

## Usage

```bash
# Normal (aborts if uncommitted changes)
/ship-web

# Force (ships even with uncommitted changes)
/ship-web --force
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
  - Print error: "❌ Error: Uncommitted changes detected. Commit them first, or use /ship-web --force"
  - List the uncommitted files
  - ABORT
- If uncommitted changes AND --force used:
  - Print warning: "⚠️  Warning: Shipping with uncommitted changes (--force used)"

### 3. Read current version
- Read voicelite-web/package.json
- Extract version from `"version": "X.X.X"` line
- Parse into major.minor.patch
- Store current version

### 4. Auto-increment version
- Increment patch number by 1
- New version = major.minor.(patch+1)
- Print: "✓ Current version: X.X.X → Bumping to Y.Y.Y"

### 5. Update voicelite-web/package.json
- Update `"version": "X.X.X"` to new version
- Print: "✓ Updated package.json"

### 6. Commit version changes
```bash
git add voicelite-web/package.json
git commit -m "chore: Bump web version to X.X.X"
```
- Print: "✓ Committed version bump"

### 7. Create git tag
```bash
git tag web-vX.X.X
```
- Print: "✓ Created tag web-vX.X.X"

### 8. Push to GitHub
```bash
git push
git push --tags
```
- Print: "✓ Pushed to GitHub"

### 9. Deploy to Vercel
```bash
cd voicelite-web
vercel deploy --prod
```
- Print deployment output
- If succeeds:
  - Print: "✓ Deployed to Vercel (production)"
- If fails:
  - Print error: "❌ Vercel deployment failed!"
  - Show error output
  - Note: "Version was still tagged and pushed to GitHub"

### 10. Final instructions
Print:
```
✅ WEBSITE SHIPPED!

Website v{version} is now live:
- ✓ Version bumped in package.json
- ✓ Tag pushed to GitHub
- ✓ Deployed to Vercel

Live at: https://voicelite.app

Verify:
- Check homepage loads
- Check pricing page
- Check API endpoints working
```

## Error Handling

- At ANY step failure, STOP immediately and report error
- If git operations fail, report error and abort
- If Vercel deployment fails, note that git changes were still made

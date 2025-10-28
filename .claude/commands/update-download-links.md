---
description: Update website download links to point to new version
argument-hint: <version>
---

# Update Download Links

Updates voicelite.app download links after GitHub release is live.

## What This Does

Updates version references in 3 locations in voicelite-web:
1. `voicelite-web/app/page.tsx` - Free tier download link (line ~378)
2. `voicelite-web/app/page.tsx` - Footer download link (line ~555)
3. `voicelite-web/app/api/download/route.ts` - Default version fallback (line ~5)

Then commits and pushes changes to deploy to Vercel.

## Usage

```bash
/update-download-links 1.1.52
```

**IMPORTANT**: Only run this AFTER GitHub release is live (check https://github.com/mikha08-rgb/VoiceLite/releases)

## Implementation Steps

### 1. Validate version argument
- Check if version argument provided
- If not: Print error "❌ Error: Version required. Usage: /update-download-links <version>" and ABORT
- Validate format (X.X.X)
- Store version in variable

### 2. Verify GitHub release exists (optional but recommended)
- Fetch: `https://api.github.com/repos/mikha08-rgb/VoiceLite/releases/tags/v{version}`
- If 404: Print warning "⚠️  Warning: GitHub release v{version} not found. Continue anyway? (y/n)"
- If user says no, ABORT

### 3. Update voicelite-web/app/page.tsx (2 locations)

**Search and replace pattern**:
- Find: `href="/api/download?version=` followed by any version number (regex: `version=\d+\.\d+\.\d+"`)
- Replace with: `version={new_version}"`
- Should find exactly 2 occurrences

**Verify locations**:
- Free tier download button (around line 378)
- Footer download link (around line 555)

If not exactly 2 matches found:
- Print warning: "⚠️  Expected 2 matches in page.tsx, found {count}"
- Show matches for manual verification

Print: "✓ Updated page.tsx (2 locations)"

### 4. Update voicelite-web/app/api/download/route.ts

**Search and replace pattern**:
- Find: `searchParams.get('version') || '` followed by version number (regex: `\|\| '\d+\.\d+\.\d+'`)
- Replace with: `|| '{new_version}'`
- Should find exactly 1 occurrence

**Verify location**:
- Default version fallback (around line 5)

If not exactly 1 match found:
- Print warning: "⚠️  Expected 1 match in route.ts, found {count}"
- Show match for manual verification

Print: "✓ Updated download/route.ts"

### 5. Commit changes
```bash
git add voicelite-web/app/page.tsx voicelite-web/app/api/download/route.ts
git commit -m "chore: Update download links to v{version}"
```
Print: "✓ Committed download link updates"

### 6. Push to GitHub
```bash
git push
```
Print: "✓ Pushed to GitHub"
Print: "✓ Vercel will auto-deploy in ~2 minutes"

### 7. Final message
Print:
```
✅ DOWNLOAD LINKS UPDATED!

Website will point to v{version} after Vercel deploys (~2 min).

Verify at: https://voicelite.app
- Check "Download Free" button
- Check footer download link
- Test actual download works
```

## Error Handling

- If version not provided, show usage and abort
- If files not found or can't be updated, report error
- If git operations fail, report error and abort

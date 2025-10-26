# Version Update Locations

When releasing a new version, update these files in order:

## 1. Desktop Application (C#)
**File**: `VoiceLite/VoiceLite/VoiceLite.csproj`
```xml
<PropertyGroup>
    <Version>1.0.97</Version>
    <FileVersion>1.0.97.0</FileVersion>
    <AssemblyVersion>1.0.97.0</AssemblyVersion>
</PropertyGroup>
```

## 2. Installer Script
**File**: `VoiceLite/Installer/VoiceLiteSetup.iss`
```ini
#define AppVersion "1.0.97"
AppVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
OutputBaseFilename=VoiceLite-Setup-{#AppVersion}
```

## 3. Web API Download Endpoint
**File**: `voicelite-web/app/api/download/route.ts`
```typescript
// Update both constants
const CURRENT_VERSION = 'v1.0.97';
const INSTALLER_FILENAME = `VoiceLite-Setup-1.0.97.exe`;
```

## 4. Homepage Download Links
**File**: `voicelite-web/components/home/hero-section.tsx`
```tsx
<Link href="/api/download?version=1.0.97">
  Download v1.0.97
</Link>
```

**File**: `voicelite-web/components/home/cta-section.tsx`
```tsx
<Link href="/api/download?version=1.0.97">
  Download VoiceLite v1.0.97
</Link>
```

## 5. Documentation
**File**: `CLAUDE.md`
```markdown
**Current Desktop**: v1.0.97
```

## 6. GitHub Release Tag
```bash
git tag v1.0.97
git push origin v1.0.97
```

## Version Check Command
Use this to verify all versions match:
```bash
echo "Desktop:" && grep -oP '(?<=<Version>)[^<]+' VoiceLite/VoiceLite/VoiceLite.csproj
echo "Installer:" && grep -oP '(?<=AppVersion=)[^\r\n]+' VoiceLite/Installer/VoiceLiteSetup.iss
echo "Web API:" && grep -oP "(?<=CURRENT_VERSION = ')[^']+" voicelite-web/app/api/download/route.ts
echo "CLAUDE.md:" && grep -oP '(?<=Current Desktop\*\*: v)[0-9.]+' CLAUDE.md
```
# Unused Dependency Finder Agent

## Purpose
Identifies and removes unused NuGet packages (desktop) and npm packages (web) that are installed but never imported or referenced in the codebase.

## When to Use
- Before production releases (reduce attack surface)
- After removing features or refactoring
- During security audits (fewer dependencies = fewer vulnerabilities)
- When investigating slow build times or large bundle sizes

## Capabilities

### Desktop (.NET/NuGet)
1. **Package Analysis**: List all NuGet packages from .csproj
2. **Import Detection**: Search codebase for `using` statements
3. **Assembly Usage**: Check if DLLs are actually referenced
4. **Transitive Dependencies**: Identify packages only needed by removed dependencies

### Web (Node.js/npm)
1. **Package Analysis**: List all packages from package.json (dependencies + devDependencies)
2. **Import Detection**: Search for `import`/`require` statements
3. **Build Tool Usage**: Check if used in webpack/next.config/scripts
4. **Bundle Analysis**: Identify packages not in final bundle

## Workflow

### Phase 1: Desktop App Analysis

```bash
# 1. Extract installed packages
grep 'PackageReference Include' VoiceLite/VoiceLite/VoiceLite.csproj

# Example output:
# - NAudio (2.2.1)
# - BouncyCastle.Cryptography (2.4.0)
# - H.InputSimulator (1.2.1)

# 2. For each package, find imports
grep -r "using NAudio" VoiceLite/VoiceLite/**/*.cs
grep -r "using BouncyCastle" VoiceLite/VoiceLite/**/*.cs

# 3. Check assembly references
# Look for actual usage of types from the package

# 4. Report unused packages
```

### Phase 2: Web App Analysis

```bash
# 1. Extract installed packages
cd voicelite-web
cat package.json | jq '.dependencies, .devDependencies'

# 2. Search for imports
grep -r "from '@upstash/ratelimit'" app/**/*.{ts,tsx,js,jsx}
grep -r "import.*zod" app/**/*.{ts,tsx,js,jsx}

# 3. Check build configs
grep -r "@vercel/analytics" next.config.js app/**/*.{ts,tsx}

# 4. Run depcheck tool (if available)
npx depcheck --json

# 5. Report unused packages
```

### Phase 3: Risk Assessment

For each unused dependency, categorize:
- **SAFE TO REMOVE**: No imports, no runtime usage, not in bundled output
- **BUILD TOOL ONLY**: Used by webpack/next/typescript (keep in devDependencies)
- **TRANSITIVE**: Auto-installed by another package (removing parent will remove this)
- **RISKY**: Might be runtime-loaded or used via reflection

## Detection Logic

### .NET Package Detection
```bash
# Safe indicators:
1. No "using [Namespace]" statements anywhere
2. No direct type references (class, interface, struct)
3. Not in project file <Reference> tags
4. Not copied to output directory

# Keep if:
1. Runtime dependencies (loaded via Assembly.Load)
2. Referenced by other packages you need
3. Used for code generation at build time
```

### npm Package Detection
```bash
# Safe indicators:
1. No import/require statements
2. Not in next.config.js or build scripts
3. Not in webpack config
4. Not in bundle (check with webpack-bundle-analyzer)

# Keep if:
1. Peer dependency of installed package
2. Used in package.json scripts
3. Type definitions (@types/*) for TypeScript
4. Build tools (typescript, eslint, prettier)
```

## Safety Rules

1. **Never remove peer dependencies**: Check package.json peerDependencies
2. **Keep build tools**: typescript, eslint, prettier, etc.
3. **Keep type definitions**: @types/* packages (even if no direct imports)
4. **Test after removal**: Run full test suite + build
5. **One package at a time**: Remove, test, commit. Repeat.
6. **Check transitive dependencies**: Use `npm ls <package>` or `dotnet list package --include-transitive`

## Known Packages (VoiceLite)

### Desktop App - Likely USED
```xml
<!-- Core functionality -->
<PackageReference Include="NAudio" Version="2.2.1" /> <!-- Audio recording -->
<PackageReference Include="NAudio.Vorbis" Version="1.5.0" /> <!-- OGG sound effects -->
<PackageReference Include="H.InputSimulator" Version="1.2.1" /> <!-- Text injection -->
<PackageReference Include="Hardcodet.NotifyIcon.Wpf" Version="2.0.1" /> <!-- System tray -->
<PackageReference Include="System.Text.Json" Version="9.0.9" /> <!-- Settings persistence -->
<PackageReference Include="System.Management" Version="8.0.0" /> <!-- System info -->
<PackageReference Include="BouncyCastle.Cryptography" Version="2.4.0" /> <!-- License signatures -->
```

### Web App - Check These
```json
{
  "dependencies": {
    "@upstash/ratelimit": "^1.0.1",  // Used in feedback/submit/route.ts
    "@upstash/redis": "^1.28.0",     // Used in feedback/submit/route.ts
    "zod": "^4.0.0",                 // Schema validation (check all routes)
    "zod-to-openapi": "^8.1.1",      // API docs generation
    "@prisma/client": "^6.3.0",      // Database ORM
    "stripe": "^17.5.0",             // Payment processing
    "resend": "^4.0.1",              // Email sending
    "@noble/ed25519": "^2.1.0"       // License signing
  },
  "devDependencies": {
    "prisma": "^6.3.0",              // Database migrations
    "typescript": "^5.7.2",          // Type checking
    "eslint": "^9.20.0",             // Linting
    "@types/node": "^24.0.0",        // Node.js types
    "@types/react": "^19.0.0"        // React types
  }
}
```

## Output Format

```markdown
# Unused Dependencies Report

## Desktop App (.NET)

### ✅ All NuGet Packages In Use
- NAudio: 47 imports across 12 files
- BouncyCastle.Cryptography: 8 imports in LicenseService.cs
- H.InputSimulator: 5 imports in TextInjector.cs
- System.Text.Json: 12 imports across 6 files

### 🔍 Manual Review Required
None found.

## Web App (Node.js)

### ✅ Production Dependencies In Use
- @upstash/ratelimit: Used in app/api/feedback/submit/route.ts
- zod: Used in 22 route files for validation
- stripe: Used in 3 checkout/webhook routes
- @prisma/client: Used in 18 API routes

### ❌ Unused Dependencies (Safe to Remove)
1. **react-syntax-highlighter** (prod dependency, 2.5MB)
   - Only used by swagger-ui-react
   - swagger-ui-react should be in devDependencies
   - Move to devDependencies or remove if /docs not used

### 🔍 Manual Review Required
None found.

## Recommendations

### Desktop App
- ✅ No unused NuGet packages found
- All 7 packages actively used in codebase
- Consider adding package justification comments

### Web App
1. **Move swagger-ui-react to devDependencies** (if /docs route not used in production)
   - Saves ~3MB in production bundle
   - Command: `npm install --save-dev swagger-ui-react`

2. **Verify @vercel/analytics usage**
   - If not using Vercel Analytics, can remove
   - Check: grep -r "@vercel/analytics" app/

## Estimated Impact
- Desktop: 0 packages removed, no size reduction
- Web: 1 package moved to dev, ~3MB bundle size reduction
- Security: Reduced attack surface (fewer prod dependencies)
```

## Tools Integration

### Automatic Checkers
```bash
# For Node.js projects
npm install -g depcheck
cd voicelite-web
depcheck --json

# For .NET projects (manual analysis required)
# No built-in tool, use grep/analysis
```

### CI/CD Integration
```yaml
# .github/workflows/dependency-audit.yml
name: Unused Dependency Check
on:
  schedule:
    - cron: '0 0 1 * *'  # Monthly
  workflow_dispatch:

jobs:
  check-deps:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      # Check Node.js dependencies
      - name: Check npm dependencies
        working-directory: voicelite-web
        run: |
          npm install -g depcheck
          depcheck --json > dep-report.json

      # Check .NET dependencies (custom script)
      - name: Check NuGet dependencies
        run: |
          # Run custom analysis script
          ./scripts/check-nuget-deps.sh
```

## Success Metrics

After cleanup:
- ✅ All tests pass (262 tests)
- ✅ Desktop app builds successfully
- ✅ Web app builds successfully
- ✅ Bundle size reduced (measure with webpack-bundle-analyzer)
- ✅ No new security vulnerabilities from removed packages
- ✅ Build time unchanged or improved

## Common False Positives

**Desktop App**:
- Packages used for code generation (not in source code)
- Runtime dependencies loaded via reflection
- Packages required by WPF/XAML at runtime

**Web App**:
- Type definitions (@types/*) - no import but needed for TypeScript
- Packages used in next.config.js or middleware
- Peer dependencies of other packages
- PostCSS plugins in Tailwind config

## Best Practices

1. **Document package purposes**: Add comments in package.json/csproj
2. **Regular audits**: Run quarterly to prevent drift
3. **Lock file maintenance**: Update lock files after removals
4. **Bundle analysis**: Use webpack-bundle-analyzer to verify removals
5. **Incremental approach**: Remove 1-2 packages, test, commit

# Repository Guidelines

## Project Structure & Module Organization
VoiceLite/VoiceLite hosts the desktop WPF app; keep XAML views in Views/, long-running services in Services/, and shared helpers in Helpers/. Automated checks live in VoiceLite/VoiceLite.Tests with artifacts under VoiceLite/VoiceLite.Tests/TestResults/. Web and marketing work sits in voicelite-web/, promo assets stay in landing-page/, and licensing logic resides in license-server/. Process docs belong in docs/ alongside top-level guides such as WORKFLOWS.md.

## Build, Test, and Development Commands
- `dotnet build VoiceLite/VoiceLite/VoiceLite.csproj` compiles the Windows client for smoke checks.
- `dotnet run --project VoiceLite/VoiceLite/VoiceLite.csproj -c Debug` launches the app with live logging.
- `dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj --collect "XPlat Code Coverage"` runs the xUnit suite and saves Cobertura output to TestResults/.
- `pwsh VoiceLite/build-installer.ps1` bundles a signed installer with Whisper models.
- In `voicelite-web/`, run `npm install && npm run dev` during feature work, then `npm run build` before shipping.

## Coding Style & Naming Conventions
Use 4-space indents in C#. Stick to PascalCase for types and methods, camelCase for locals, and prefix interfaces with I. Align namespaces with folders, prefer guard clauses for null checks, and route diagnostics through ErrorLogger. For TypeScript and React, keep 2-space indents, rely on const or let, and name route directories in kebab-case. Lint web code with `npx next lint`.

## Testing Guidelines
Follow xUnit naming like `MethodName_ShouldExpectedBehavior`, and keep reusable fixtures in VoiceLite.Tests/TestData. Maintain >= 75% line coverage on touched C# files and commit the coverage summary produced by the test command. Web changes should include component or API tests where practical and must pass `npm run build`.

## Commit & Pull Request Guidelines
Author concise, imperative commits (for example `Polish frontend design`). Pull requests should summarize changes, enumerate verification steps, link relevant issues, and attach screenshots or recordings for UI updates. Note installer status when `build-installer.ps1` runs and call out any security or licensing impacts.

## Security & Configuration Tips
Store secrets in `.env.local` for web and `appsettings.Development.json` for desktop builds; rotate them using `pwsh tools/generate-secure-keys.ps1`. Review SECURITY.md and the voicelite-security-auditor checklist before modifying payments, licensing, or logging.
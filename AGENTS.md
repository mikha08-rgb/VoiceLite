# Repository Guidelines

## Project Structure & Module Organization
- Desktop source lives in `VoiceLite/VoiceLite`; WPF views sit in `Views/`, services in `Services/`, and utilities in `Helpers/`.
- Automated checks reside in `VoiceLite/VoiceLite.Tests` with coverage output in `VoiceLite/VoiceLite.Tests/TestResults/`.
- Web and marketing work is in `voicelite-web/`; static promo assets remain in `landing-page/`, while licensing logic is isolated in `license-server/`.
- Operational docs and workflow playbooks are collected in `docs/` plus top-level guides like `WORKFLOWS.md` and `CLAUDE.md`.

## Build, Test, and Development Commands
- `dotnet build VoiceLite/VoiceLite/VoiceLite.csproj` - compile the Windows client.
- `dotnet run --project VoiceLite/VoiceLite/VoiceLite.csproj -c Debug` - launch the app with live logging.
- `dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj --collect "XPlat Code Coverage"` - run the xUnit suite and export Cobertura coverage.
- `pwsh VoiceLite/build-installer.ps1` - generate the self-contained installer and bundle whisper models.
- `npm install && npm run dev` in `voicelite-web/` - start the Next.js site locally; finish with `npm run build` before shipping.

## Coding Style & Naming Conventions
- C# uses 4-space indents, `PascalCase` types/methods, `camelCase` locals, and `I`-prefixed interfaces; keep namespaces aligned with folders.
- Favor guard clauses for null checks and route logging through `ErrorLogger` instead of console writes.
- TypeScript and React code keeps 2-space indents, `const` or `let`, kebab-case route folders (for example `/app/api/checkout`), and should pass `npx next lint`.

## Testing Guidelines
- Follow xUnit naming like `MethodName_ShouldExpectedBehavior`; store fixtures under `VoiceLite.Tests/TestData` for repeatability.
- Maintain at least 75 percent line coverage on modified files and commit the coverage summary from the command above.
- Web features should include component or API route tests where practical and must succeed with `npm run build`.

## Commit & Pull Request Guidelines
- Commits mirror the existing history: concise, imperative, and scoped (for example `Polish frontend design`).
- Run desktop and web test commands before every PR, note installer status if touched, and attach coverage figures.
- PR templates should list the change summary, verification steps, linked issues, and screenshots or screen recordings for UI updates.

## Security & Configuration Tips
- Keep secrets outside the repo using `.env.local` (web) and `appsettings.Development.json` (desktop); rotate them with `pwsh tools/generate-secure-keys.ps1`.
- Review `SECURITY.md` and the `voicelite-security-auditor` checklist whenever payment flows, licensing, or logging changes.

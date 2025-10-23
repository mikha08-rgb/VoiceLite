# Maintenance Guide

## Build & Test Loop
1. Ensure Windows .NET SDK 8.0 is installed and accessible at /mnt/c/Program Files/dotnet/dotnet.exe.
2. From repo root run: /mnt/c/Program\ Files/dotnet/dotnet.exe build VoiceLite/VoiceLite.sln.
3. For quick smoke of transcription pipeline, run VoiceLite/build-and-run.bat (uses latest build output).

## Lint & Hygiene
- Run dotnet format --verify-no-changes (via Windows SDK path) before committing to catch unused usings or style drift.
- Keep the helper Utilities/HotkeyDisplayHelper as the single source for hotkey text; new views should consume it instead of duplicating logic.

## Asset Management
- Keep generated bin/ and obj/ folders out of versioned directories; use VoiceLite/whisper/ for bundled binaries and prune stale copies under repo root.

## Coordination Notes
- HotkeyManager now depends on dispatcher affinity; test hotkey registration after threading changes.
- If MetricsTracker becomes unused long term, capture telemetry and schedule removal; for now it stays internal to avoid accidental API promises.

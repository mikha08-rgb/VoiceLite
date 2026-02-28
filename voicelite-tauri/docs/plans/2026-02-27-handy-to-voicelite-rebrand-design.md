# Handy to VoiceLite Rebrand Design

**Date**: 2026-02-27
**Status**: Approved

## Context

VoiceLite cloned the open-source Handy project (Tauri 2.x speech-to-text app by CJ Pais, MIT licensed). This design covers the full rebrand from Handy to VoiceLite.

## Decisions

- **Full rebrand** — all Handy identity replaced with VoiceLite
- **Keep all features** — all 9+ transcription engines, LLM post-processing, i18n, CLI, history, audio feedback
- **Free and open source** — no licensing/paywall system
- **Keep Handy CDN** — model download URLs (blob.handy.computer) stay for now, migrate later
- **MIT attribution** — credit Handy/CJ Pais in README per MIT requirements

## 1. Identity Renames

| What | From | To |
|------|------|----|
| Tauri product name | `Handy` | `VoiceLite` |
| Bundle identifier | `com.pais.handy` | `com.voicelite.app` |
| NPM package name | `handy-app` | `voicelite-app` |
| Rust crate name | `handy` | `voicelite` |
| Rust lib name | `handy_app_lib` | `voicelite_app_lib` |
| Window title | `Handy` | `VoiceLite` |
| HTML page title | `handy` | `VoiceLite` |
| Log file name | `handy` | `voicelite` |

## 2. URL Updates

| What | Action |
|------|--------|
| `blob.handy.computer/*` | Leave as-is (migrate later) |
| `handy.computer` | Replace with `voicelite.app` |
| `github.com/cjpais/Handy` | Replace with VoiceLite repo URL |
| HTTP User-Agent/Referer/X-Title | Update to `VoiceLite/1.0` |
| Donation links | Remove or replace |

## 3. Assets

- Replace all icon files in `src-tauri/icons/` (17 files)
- Replace tray icons in `src-tauri/resources/`
- New VoiceLite artwork needed

## 4. Documentation

- Rewrite README.md with VoiceLite positioning
- Update BUILD.md, CONTRIBUTING.md, CLAUDE.md, AGENTS.md
- Remove/update .github templates (issue templates, funding, PR template)

## 5. Legal

- Keep MIT LICENSE file
- Add attribution in README: "Based on Handy by CJ Pais, MIT License"

## 6. Not Touched

- All Rust business logic
- All React components and UI
- i18n translations
- Model download URLs (blob.handy.computer)
- Cargo.toml [patch] section (custom tauri fork)
- Full feature set (engines, LLM post-processing, CLI, history)

# Handy → VoiceLite Rebrand Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Rebrand the cloned Handy codebase to VoiceLite, replacing all identity/branding while keeping all features intact.

**Architecture:** Surgical find-and-replace organized by category. Each task touches one logical group of files. Build verification after Rust changes to catch breakage early.

**Tech Stack:** Tauri 2.x (Rust + React/TypeScript), Cargo, bun, Vite

---

### Task 1: Rename Rust crate identity

**Files:**
- Modify: `voicelite-tauri/src-tauri/Cargo.toml:2,4,8,19`
- Modify: `voicelite-tauri/src-tauri/src/main.rs:5,17`
- Modify: `voicelite-tauri/src-tauri/src/lib.rs:377`
- Modify: `voicelite-tauri/src-tauri/src/cli.rs:4`

**Step 1: Update Cargo.toml**

In `voicelite-tauri/src-tauri/Cargo.toml`:
- Line 2: `name = "handy"` → `name = "voicelite"`
- Line 4: `description = "Handy"` → `description = "VoiceLite - Speech to Text"`
- Line 8: `default-run = "handy"` → `default-run = "voicelite"`
- Line 19: `name = "handy_app_lib"` → `name = "voicelite_app_lib"`

**Step 2: Update main.rs imports**

In `voicelite-tauri/src-tauri/src/main.rs`:
- Line 5: `use handy_app_lib::CliArgs;` → `use voicelite_app_lib::CliArgs;`
- Line 17: `handy_app_lib::run(cli_args)` → `voicelite_app_lib::run(cli_args)`

**Step 3: Update lib.rs log filename**

In `voicelite-tauri/src-tauri/src/lib.rs`:
- Line 377: `file_name: Some("handy".into())` → `file_name: Some("voicelite".into())`

**Step 4: Update cli.rs command name**

In `voicelite-tauri/src-tauri/src/cli.rs`:
- Line 4: `#[command(name = "handy", about = "Handy - Speech to Text")]` → `#[command(name = "voicelite", about = "VoiceLite - Speech to Text")]`

**Step 5: Verify Rust compiles**

Run: `cd voicelite-tauri/src-tauri && cargo check`
Expected: Compiles with no errors. Warnings OK.

**Step 6: Commit**

```bash
git add voicelite-tauri/src-tauri/Cargo.toml voicelite-tauri/src-tauri/src/main.rs voicelite-tauri/src-tauri/src/lib.rs voicelite-tauri/src-tauri/src/cli.rs
git commit -m "refactor: rename Rust crate from handy to voicelite"
```

---

### Task 2: Update Tauri app config

**Files:**
- Modify: `voicelite-tauri/src-tauri/tauri.conf.json:3,5,17,73,80`

**Step 1: Update tauri.conf.json**

- Line 3: `"productName": "Handy"` → `"productName": "VoiceLite"`
- Line 5: `"identifier": "com.pais.handy"` → `"identifier": "com.voicelite.app"`
- Line 17: `"title": "Handy"` → `"title": "VoiceLite"`
- Line 73: sign command `-d Handy` → `-d VoiceLite`
- Line 80: updater URL `cjpais/Handy` → your VoiceLite GitHub repo (or comment out updater for now)

**Step 2: Commit**

```bash
git add voicelite-tauri/src-tauri/tauri.conf.json
git commit -m "refactor: update Tauri app identity to VoiceLite"
```

---

### Task 3: Update frontend identity

**Files:**
- Modify: `voicelite-tauri/package.json:2`
- Modify: `voicelite-tauri/index.html:6`

**Step 1: Update package.json**

- Line 2: `"name": "handy-app"` → `"name": "voicelite-app"`

**Step 2: Update index.html**

- Line 6: `<title>handy</title>` → `<title>VoiceLite</title>`

**Step 3: Commit**

```bash
git add voicelite-tauri/package.json voicelite-tauri/index.html
git commit -m "refactor: update frontend package name and page title to VoiceLite"
```

---

### Task 4: Update tray and UI branding

**Files:**
- Modify: `voicelite-tauri/src-tauri/src/tray.rs:97,99`
- Modify: `voicelite-tauri/src-tauri/src/llm_client.rs:58,62,64`
- Modify: `voicelite-tauri/src/components/settings/about/AboutSettings.tsx:32,69`

**Step 1: Update tray.rs labels**

- Line 97: `format!("Handy v{} (Dev)"` → `format!("VoiceLite v{} (Dev)"`
- Line 99: `format!("Handy v{}"` → `format!("VoiceLite v{}"`

**Step 2: Update llm_client.rs HTTP headers**

- Line 58: Referer header `"https://github.com/cjpais/Handy"` → `"https://github.com/MishkaMN/VoiceLite"` (or your repo URL)
- Line 62: User-Agent `"Handy/1.0 (+https://github.com/cjpais/Handy)"` → `"VoiceLite/1.0 (+https://github.com/MishkaMN/VoiceLite)"`
- Line 64: X-Title `"Handy"` → `"VoiceLite"`

**Step 3: Update AboutSettings.tsx**

- Line 32: `"https://handy.computer/donate"` → `"https://voicelite.app"` (or remove donate button)
- Line 69: `"https://github.com/cjpais/Handy"` → `"https://github.com/MishkaMN/VoiceLite"`

**Step 4: Verify Rust compiles**

Run: `cd voicelite-tauri/src-tauri && cargo check`
Expected: Compiles with no errors.

**Step 5: Commit**

```bash
git add voicelite-tauri/src-tauri/src/tray.rs voicelite-tauri/src-tauri/src/llm_client.rs voicelite-tauri/src/components/settings/about/AboutSettings.tsx
git commit -m "refactor: update tray labels, HTTP headers, and about page to VoiceLite"
```

---

### Task 5: Update CI/CD workflows

**Files:**
- Modify: `voicelite-tauri/.github/workflows/release.yml:77`
- Modify: `voicelite-tauri/.github/workflows/build-test.yml:41`
- Modify: `voicelite-tauri/.github/FUNDING.yml:2`

**Step 1: Update release.yml**

- Line 77: `asset-prefix: "handy"` → `asset-prefix: "voicelite"`

**Step 2: Update build-test.yml**

- Line 41: `asset-prefix: "handy-test"` → `asset-prefix: "voicelite-test"`

**Step 3: Update or remove FUNDING.yml**

Either remove the file or update the URLs to VoiceLite equivalents.

**Step 4: Commit**

```bash
git add voicelite-tauri/.github/
git commit -m "refactor: update CI/CD asset prefixes and funding to VoiceLite"
```

---

### Task 6: Update Nix flake

**Files:**
- Modify: `voicelite-tauri/flake.nix:31,58,59,191,196`

**Step 1: Update all handy references in flake.nix**

- Line 31: `pname = "handy-bun-deps"` → `pname = "voicelite-bun-deps"`
- Line 58: `handy = pkgs.rustPlatform.buildRustPackage {` → `voicelite = pkgs.rustPlatform.buildRustPackage {`
- Line 59: `pname = "handy"` → `pname = "voicelite"`
- Line 191: `mainProgram = "handy"` → `mainProgram = "voicelite"`
- Line 196: `default = self.packages.${system}.handy` → `default = self.packages.${system}.voicelite`

**Step 2: Commit**

```bash
git add voicelite-tauri/flake.nix
git commit -m "refactor: update Nix flake package names to voicelite"
```

---

### Task 7: Add MIT attribution and update documentation

**Files:**
- Modify: `voicelite-tauri/README.md` (rewrite)
- Modify: `voicelite-tauri/CLAUDE.md` (update references)
- Modify: `voicelite-tauri/AGENTS.md` (update references)
- Modify: `voicelite-tauri/BUILD.md` (update references)
- Modify: `voicelite-tauri/CONTRIBUTING.md` (update references)

**Step 1: Add attribution block to README.md**

At the top or bottom of README.md, add:

```markdown
## Attribution

VoiceLite is based on [Handy](https://github.com/cjpais/Handy) by [CJ Pais](https://github.com/cjpais), licensed under the [MIT License](LICENSE).
```

**Step 2: Replace "Handy" with "VoiceLite" in all documentation files**

For each .md file, replace:
- "Handy" → "VoiceLite" (preserving case where appropriate)
- `handy.computer` → `voicelite.app`
- `cjpais/Handy` → your repo name
- `com.pais.handy` → `com.voicelite.app`

Leave `blob.handy.computer` URLs untouched (CDN stays for now).
Leave `handy-keys` references untouched (external crate name).
Leave `handy-2.9.1` branch references untouched (custom fork).

**Step 3: Update .github issue templates**

Replace "Handy" with "VoiceLite" in all issue template files under `.github/ISSUE_TEMPLATE/`.

**Step 4: Commit**

```bash
git add voicelite-tauri/README.md voicelite-tauri/CLAUDE.md voicelite-tauri/AGENTS.md voicelite-tauri/BUILD.md voicelite-tauri/CONTRIBUTING.md voicelite-tauri/.github/
git commit -m "docs: rebrand documentation from Handy to VoiceLite with MIT attribution"
```

---

### Task 8: Verify full build

**Step 1: Run cargo check**

Run: `cd voicelite-tauri/src-tauri && cargo check`
Expected: Compiles cleanly.

**Step 2: Run bun install + type check**

Run: `cd voicelite-tauri && bun install && bun run check` (or `npx tsc --noEmit`)
Expected: No TypeScript errors related to renaming.

**Step 3: Grep for remaining "handy" references (excluding allowed ones)**

Run: Search for case-insensitive "handy" in voicelite-tauri/, excluding:
- `blob.handy.computer` (CDN, deferred)
- `handy-keys` / `handy_keys` (external crate)
- `handy-2.9.1` (fork branch)
- `node_modules/`, `target/`, `.git/`, `bun.lock`, `Cargo.lock`
- `CHANGELOG.md` (historical)

If any unexpected matches found, fix them.

**Step 4: Final commit if any stragglers fixed**

```bash
git add -A
git commit -m "refactor: fix remaining Handy references missed in rebrand"
```

---

## Notes

**Things intentionally NOT changed:**
- `blob.handy.computer` model download URLs in `managers/model.rs` — migrate CDN later
- `handy-keys` crate references — this is an external dependency name
- `handy-2.9.1` branch in Cargo.toml `[patch]` — custom Tauri fork
- Icon/image files — need new VoiceLite artwork (separate task)
- i18n translation files — no "Handy" branding in translation strings (app name comes from Tauri config)

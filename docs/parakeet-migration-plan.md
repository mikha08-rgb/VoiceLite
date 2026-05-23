# Migrate ASR Engine: Whisper.net → Sherpa-ONNX + Parakeet v3

## Context

**Problem.** VoiceLite currently runs OpenAI Whisper (via `Whisper.net 1.9.0`) on GGML model files in five tiers (base/small/medium/large-v3-turbo/large-v3). Whisper.net updates have slowed, Whisper's seq2seq architecture is prone to hallucinations on silence ("Thanks for watching!"), and even Whisper Large v3 is no longer the SOTA bar. NVIDIA's **Parakeet TDT 0.6B v3** (released 2026) is multilingual (25 European languages), beats Whisper Large v3 at 6.34% avg WER on the HF Open ASR Leaderboard, is ~2–3× faster on CPU, and — critically — uses a transducer architecture that cannot hallucinate tokens not aligned to audio frames.

**Why now.** The user reports the current accuracy ceiling feels sub-par. Sherpa-ONNX (the k2-fsa runtime) ships first-class C# bindings (`org.k2fsa.sherpa.onnx 1.13.2`) plus pre-packaged Parakeet v3 ONNX models, removing the only realistic blocker to running Parakeet in-process from .NET. ONNX Runtime is already a dependency in `VoiceLite.csproj` (used for Silero VAD), so there is no net-new runtime to ship.

**Outcome.** A single `v2.0.0-beta` release where the speech engine is Parakeet v3 via Sherpa-ONNX. Whisper.net is removed. Existing paying-user license validation continues to work. Tier monetization strategy is **deferred** to a follow-up — this PR neutralizes "5 AI models" copy without making new feature promises.

---

## Status & Current State (as of this planning re-entry)

Branch `v2.0.0-parakeet-migration` (uncommitted changes). 8 files modified + 1 new file. Working tree is clean otherwise (`master` is 2 commits ahead of origin from before this work began).

- ✅ **Phase A — Engine core swap** — DONE
- ✅ **Phase B — Model lineup + settings plumbing** — DONE
- 🛑 **Verification gate (current step)** — user is pausing here to confirm Phase A+B compile and run on Windows before any further file changes.
- ⬜ **Phase C — UI (downloader, first-launch, dev-term dictionary)** — BLOCKED on verification
- ⬜ **Phase D — Packaging + attribution + release prep** — BLOCKED on verification
- ⬜ **Phase E — Test rewrites** — BLOCKED on verification (8 of 26 test files need updates)
- ⬜ **Phase F — Manual E2E on Windows** — BLOCKED on Phase C/D/E

### 🛑 Verification Gate Checklist (user-owned, run on a Windows machine)

The user is on macOS and `dotnet` isn't installed there — everything below runs on a Windows machine (their dev box or a VM). Goal: confirm Phase A+B compiles AND the new Sherpa-ONNX-backed engine transcribes correctly with a manually placed model.

**1. Sync the branch to Windows.**
   - Working tree changes are uncommitted on this Mac. Either (a) commit + push the branch from Mac and pull on Windows, or (b) rsync/scp the working directory across. Recommend (a) — gives a checkpoint to roll back to.

**2. Restore + build.**
```powershell
cd C:\path\to\voicelite
git checkout v2.0.0-parakeet-migration
dotnet restore VoiceLite\VoiceLite.sln
dotnet build VoiceLite\VoiceLite.sln -c Debug
```
Expected: clean build. **One known harmless warning** — CS0414 on `ModelResolverService._proFeatureService` (unused field, kept for ctor compat). Any other error means we missed something — surface back to me with the message.

**3. Confirm `dotnet test` compiles** (assertions WILL fail — that's Phase E).
```powershell
dotnet test VoiceLite\VoiceLite.Tests\VoiceLite.Tests.csproj --no-build --filter "FullyQualifiedName!~Whisper" 2>&1 | Select-Object -First 50
```
We're not running the Whisper-coupled tests here, just confirming the test assembly itself compiles.

**4. Manually place the Parakeet model files.** Download the tarball directly from the upstream Sherpa-ONNX GitHub Release:
```powershell
curl -L -o parakeet.tar.bz2 https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8.tar.bz2
tar -xjf parakeet.tar.bz2
# This produces a folder `sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8` with the four files we need.
# Rename and place where the resolver looks (paths resolved relative to AppDomain.BaseDirectory):
mkdir VoiceLite\VoiceLite\bin\Debug\net8.0-windows\models\parakeet-v3
move sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8\encoder.int8.onnx VoiceLite\VoiceLite\bin\Debug\net8.0-windows\models\parakeet-v3\
move sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8\decoder.int8.onnx VoiceLite\VoiceLite\bin\Debug\net8.0-windows\models\parakeet-v3\
move sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8\joiner.int8.onnx VoiceLite\VoiceLite\bin\Debug\net8.0-windows\models\parakeet-v3\
move sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8\tokens.txt VoiceLite\VoiceLite\bin\Debug\net8.0-windows\models\parakeet-v3\
```

**5. Run and test.**
```powershell
dotnet run --project VoiceLite\VoiceLite\VoiceLite.csproj
```
   - App opens. Press hotkey, dictate a short sentence, release. Text should inject into the foreground app.
   - **Hallucination smoke test**: hold hotkey for 5 seconds of silence. Output should be empty or near-empty (Parakeet's transducer architecture shouldn't generate "Thanks for watching!").
   - **Settings migration**: open `%LocalAppData%\VoiceLite\settings.json` (or wherever Settings serializes). Confirm `WhisperModel: "parakeet-tdt-0.6b-v3-int8"`. If an older v1.x settings.json was present, confirm it got rewritten on first run.
   - **License preservation**: if Pro license is configured, confirm `ProFeatureService.IsProUser == true` (e.g., check the tier name in Settings UI — should still say "Pro ⭐").

**6. Report back.** If all six steps pass → unblock Phase C–E. If any step fails → surface the exact error / behavior so we can patch before piling on more changes.

### Deviations from the original Phase A/B sketch (intentional)

- **Class kept, not renamed.** Plan said create `ParakeetTranscriptionService` and delete `PersistentWhisperService`. Instead the file was rewritten in place keeping the `PersistentWhisperService` class name. Rationale: zero touch on MainWindow / tests, smaller PR, rename is already in "Out of Scope (Deferred)".
- **`MaxActivePaths` (not `NumActivePaths`).** Sherpa's `OfflineRecognizerConfig` field is named `MaxActivePaths` per the actual NuGet source — the plan sketched `NumActivePaths` from earlier docs. Code uses the correct name.
- **`BeamSize` kept as compat alias.** `WhisperPresetConfig.BeamSize` is now `=> MaxActivePaths` so `Settings.BeamSize` and any other reader keeps compiling. Cheaper than tracking every reference.
- **Migration wired into `MainWindow.LoadSettings`, not `App.xaml.cs`.** Settings deserialization lives in MainWindow, so the migration call is co-located there. Same correctness, less invasive.
- **`ValidateWhisperModel` gutted in place.** Plan implied a redesign; the simpler move was to delete the model-file existence checks (resolver handles those now) and keep only the license-tamper / legacy-LicenseKey cleanup.
- **`ModelResolverService` probes both `models/parakeet-v3` and `whisper/parakeet-v3`.** Keeps installer-folder compat without a separate naming decision.
- **`WhisperModelInfo.LegacyGgmlFileNames` is the shared source of truth** for "what does a pre-Parakeet settings.json look like" — consumed by `SettingsMigration` and `WhisperModelInfo.GetDisplayName` (which renders `(legacy)` for any of them).
- **`ProFeatureService._proFeatureService` field on `ModelResolverService` is now unused** (will emit CS0414 warning, not error). Kept on the ctor signature for caller compat; will be cleaned up alongside the eventual class rename.

---

## Architectural Decisions

1. **Single model lineup post-swap.** Parakeet TDT 0.6B v3 int8 (~640MB). The five-tier GGML lineup collapses to one. `IProFeatureService.CanUseModel` becomes a no-op (returns `true`). License `isPro` flag remains in `Settings` and DB unchanged — we keep the seam, we just don't gate the model.
2. **Pro monetization deferred.** Don't rewrite the Pro pitch to promise Voice Shortcuts / Export History / Custom Dictionary until those ship. Update `ShowUpgradePrompt` and `TierDescription` to vague language ("Pro tier features coming — your license is preserved") to avoid promising features that don't exist yet.
3. **TranscriptionPreset is repurposed, not removed.** Keep the enum (Speed/Balanced/Accuracy) and the existing settings UI combo, but `WhisperPresetConfig.GetPresetConfig` returns a new `SherpaDecodingConfig { DecodingMethod, NumActivePaths }` instead of Whisper sampling params. Mapping: Speed→`greedy_search`, Balanced→`modified_beam_search` (beam=2), Accuracy→`modified_beam_search` (beam=4). Zero user-visible churn for this control.
4. **Install size: post-install download.** Installer stays at current ~150MB. On first launch, if the Parakeet model isn't present at the expected path, surface a progress UI (reuse existing `ModelDownloadControl`) that downloads `sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8.tar.bz2` (~640MB) from a mirror on the project's GitHub Releases (avoids Hugging Face IP rate limits on launch day). Extract in-place, then continue startup.
5. **Settings migration: silent overwrite.** On startup, if `settings.WhisperModel` matches any of the six legacy GGML filenames or is null/empty, overwrite with `"parakeet-tdt-0.6b-v3-int8"` and persist. Log via `ErrorLogger.LogWarning`. No user-facing prompt.
6. **Initial prompt loss: accept + dictionary mitigation.** Transducers can't accept prompts. The current hardcoded "VoiceLite, GitHub, JavaScript..." vocab biasing (`PersistentWhisperService.cs:119-122`) gets replaced by a case-insensitive word-boundary replacement map in `TextPostProcessor.cs` covering the same ~12 dev terms.
7. **Threading.** Sherpa-ONNX `OfflineRecognizer.Decode()` is blocking, unlike Whisper.net's `IAsyncEnumerable`. Wrap the call in `await Task.Run(...)` inside the new service so `MainWindow.OnAudioFileReady` (an `async void` on UI thread) doesn't freeze.
8. **Audio handoff.** `AudioRecorder` continues producing 16kHz/16-bit/mono WAV — unchanged. The new service decodes the WAV to `float[]` at the Sherpa boundary using NAudio's `WaveFileReader.ToSampleProvider()` (NAudio already a dep).
9. **Migration cadence.** Full swap on a single branch, released as `v2.0.0-beta` to opt-in beta users. No dual-engine in-binary. Rollback = revert tag. Promote to stable only after soak.

---

## File-Level Change List (Execution Order)

### ✅ Phase A — Engine core swap — DONE

Implemented:
1. `VoiceLite/VoiceLite/VoiceLite.csproj` — Whisper.net packages → sherpa-onnx packages; `ggml-*.bin` Content include removed; `silero_vad_v5.onnx` kept.
2. `VoiceLite/VoiceLite/Services/PersistentWhisperService.cs` — rewritten in place using `SherpaOnnx.OfflineRecognizer`. Wraps blocking `Decode()` in `Task.Run`. Decodes WAV→`float[]` with `WaveFileReader.ToSampleProvider()` + `StereoToMonoSampleProvider` fallback.
3. `VoiceLite/VoiceLite/Services/ModelResolverService.cs` — returns Parakeet directory; Pro gating stripped; probes `models/parakeet-v3`, `whisper/parakeet-v3`, `%LocalAppData%/VoiceLite/models/parakeet-v3`.
4. `VoiceLite/VoiceLite/Models/Settings.cs` — `WhisperPresetConfig` repurposed for Sherpa (`DecodingMethod` + `MaxActivePaths`); default `WhisperModel` → `parakeet-tdt-0.6b-v3-int8`; `BeamSize` kept as compat alias.
5. `VoiceLite/VoiceLite/MainWindow.xaml.cs:495` — stale "Whisper.net in-process" comment updated.

### ✅ Phase B — Model lineup + settings plumbing — DONE

Implemented:
6. `VoiceLite/VoiceLite/Models/WhisperModelInfo.cs` — collapsed to single Parakeet entry; `LegacyGgmlFileNames` table added.
7. `VoiceLite/VoiceLite/Services/ProFeatureService.cs` — `CanUseModel` / `IsModelAvailable` return `true` always; `GetAvailableModels` returns single entry; `TierDescription` + `ShowUpgradePrompt` copy neutralized.
8. `VoiceLite/VoiceLite/Services/DownloadEndpoints.cs` — single URL (`ParakeetV3Int8`) pointing at k2-fsa GitHub Releases; with comment noting the mirror should move to VoiceLite Releases before launch.
9. `VoiceLite/VoiceLite/Services/SettingsMigration.cs` — **NEW FILE**; `Migrate(Settings)` rewrites legacy GGML filenames to Parakeet id.
10. `VoiceLite/VoiceLite/MainWindow.xaml.cs` — `LoadSettings` calls `SettingsMigration.Migrate(settings)` and saves on change; `ValidateWhisperModel` gutted to license-tamper checks only.

### ⬜ Phase C — UI (downloader + first-launch + dev-term dictionary)

This is the **shippability gate** — without it, the app crashes with `FileNotFoundException` on first launch because no model files are bundled (Decision 4: post-install download). All Phase C work lands on the existing branch.

11. **Add `org.k2fsa.sherpa.onnx 1.13.2`-compatible tar.bz2 extraction** — there's no existing tarball-decompression dependency in `VoiceLite.csproj` (confirmed by grep). Add `SharpCompress 0.36.0` (or current) `<PackageReference>`. `SharpCompress.Archives.Tar` handles `.tar.bz2` natively.

12. **Rewrite `VoiceLite/VoiceLite/Controls/ModelDownloadControl.xaml` + `.xaml.cs`** — replace the existing 5-model grid with a **single Parakeet card** that:
    - Shows total size (`~640MB`), current state (Not installed / Downloading / Extracting / Installed), and a Download button.
    - Calls `DownloadEndpoints.ParakeetV3Int8`, streams to a temp `.tar.bz2`, reports progress percent.
    - On 100%, extracts via `SharpCompress` into `%LocalAppData%/VoiceLite/models/parakeet-v3/`, then verifies the four required files (`encoder.int8.onnx`, `decoder.int8.onnx`, `joiner.int8.onnx`, `tokens.txt`) exist.
    - On failure (network, disk space, partial download), surfaces a retry button.
    - **Reuse**: `Infrastructure/Resilience/RetryPolicies.cs` (Polly) for the download itself.

13. **First-launch hook in `VoiceLite/VoiceLite/App.xaml.cs`** — before `new MainWindow()`, probe `ModelResolverService.GetAvailableModelPaths()`. If empty, show a modal window wrapping the rewritten `ModelDownloadControl`. Block `MainWindow` creation until the model is installed or user explicitly cancels (in which case exit with a friendly "VoiceLite needs the speech model to run" dialog).
    - **Implementation note**: `MainWindow` ctor and `LoadSettings` need to be reentrant-safe — the model download window will create a temporary `Settings` instance to access `WhisperModel`.

14. **Simplify `VoiceLite/VoiceLite/SettingsWindowNew.xaml` + `.xaml.cs`** — model selector becomes a read-only "Engine: Parakeet v3 (multilingual, ~640MB)" status row. The TranscriptionPreset combo box stays (Decision 3). Keep the AI Models tab; surface re-download / verify-integrity actions there.

15. **Delete `VoiceLite/VoiceLite/Controls/SimpleModelSelector.xaml`** + `.xaml.cs` and **`ModelComparisonControl.xaml`** + `.xaml.cs` — confirmed unreferenced from any other XAML or code-behind. They're orphan multi-model UIs.

16. **`VoiceLite/VoiceLite/Services/TextPostProcessor.cs`** — add a `_devTermDictionary` `Dictionary<string,string>` covering the original initial-prompt vocab (`github→GitHub`, `javascript→JavaScript`, `typescript→TypeScript`, `python→Python`, `voicelite→VoiceLite`, `.net→.NET`, `node.js→Node.js`, `api→API`, `json→JSON`, `sql→SQL`, `react→React`, `c sharp→C#`). Apply via case-insensitive word-boundary regex after the existing capitalization pass. Cheap mitigation for Parakeet's missing prompt-bias feature.

### ⬜ Phase D — Packaging + attribution + release prep

17. **`VoiceLite/Installer/VoiceLiteSetup.iss:43`** — drop the `whisper\*` source line entirely (no model files in installer). Keep `silero_vad_v5.onnx`. Bump installer version to `2.0.0`. Verify the publish output is now small enough to keep the installer under 200MB.

18. **`build-installer.ps1` + `build-release.ps1`** — drop GGML copy steps. No model bundling. Just .NET publish output + Sherpa native DLLs (the runtime NuGet brings them) + Silero VAD ONNX.

19. **`CLAUDE.md`** — rewrite "Why Whisper.net in-process" → "Why Sherpa-ONNX + Parakeet v3 in-process". Replace the "Available Models" 5-row table with single Parakeet entry. Update "Pro Feature Gating" section to note model gating is removed; Pro features deferred.

20. **CC-BY-4.0 attribution (mandatory)** — Parakeet v3 is CC-BY-4.0, so:
    - Add NVIDIA attribution line to `EULA.txt`.
    - Add a "Third-party licenses" section / button to the About dialog (need to locate or add About UI — check `SettingsWindowNew.xaml` for an existing About tab).
    - Add an attribution paragraph to `README.md`.
    - Ship a `LICENSES/parakeet-v3-CC-BY-4.0.txt` file with the installer payload.

21. **VC++ redistributable probe** — Sherpa-ONNX native DLLs require VC++ runtime. At app startup, catch the first `DllNotFoundException` (or test-load a small Sherpa API call inside a try/catch) and show a friendly "Install VC++ redistributable" dialog with a download link, instead of letting the user see a raw P/Invoke crash.

22. **Release tag `v2.0.0-beta`** — push as a prerelease tag through the existing GitHub Actions workflow. Beta channel = opt-in users notified via Discord/email. Promote to stable after soak period.

---

## Reused Functions & Utilities

- **`NAudio.Wave.WaveFileReader.ToSampleProvider()`** — already used by `Services/Audio/AudioPreprocessor.cs` to convert WAV bytes to `float[]` samples. Reuse the same pattern in `ParakeetTranscriptionService` at the WAV→Sherpa boundary.
- **`Services/Audio/HighPassFilter.cs`, `SimpleNoiseGate.cs`, `AutomaticGainControl.cs`** — operate on `ISampleProvider` / `float[]` PCM, engine-agnostic. **No changes needed.**
- **`Services/AudioRecorder.cs`** — 16kHz/16-bit/mono WAV output is exactly what Sherpa wants after one decode. **No changes needed.**
- **`Controls/ModelDownloadControl.xaml.cs`** — existing progress UI + chunked download + checksum logic gets repointed at one URL instead of five. Same control, simpler config.
- **`Services/ErrorLogger.cs`** — `LogWarning` / `LogError` / `LogMessage` semantics from CLAUDE.md ("release-visible logs") apply unchanged.
- **`Infrastructure/Resilience/RetryPolicies.cs`** (Polly) — reuse for the first-launch model download.
- **`ProFeatureService` future-feature stubs** (lines 41–63): `VoiceShortcutsTabVisibility`, `ExportHistoryButtonVisibility`, `CustomDictionaryTabVisibility`, `AdvancedSettingsVisibility` — already gated on `IsProUser`. Stay as-is; they're how Pro stays meaningful when model gating goes away.
- **`Infrastructure` cancellation/timeout pattern** in `PersistentWhisperService.cs:160–266` — port verbatim into the new service.

---

## Verification

### ⬜ Phase E — Test rewrites

`dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj` must pass before commit (per CLAUDE.md). **8 of 26 test files** reference the changed types and need updates (confirmed by grep against the new branch state):

- **`VoiceLite.Tests/Services/WhisperServiceTests.cs`** — class name kept (`PersistentWhisperService`), so the type still resolves. Rewrite assertions about model files / `GetWhisperVersion()` to expect Sherpa-ONNX + Parakeet. Reuse 16kHz mono WAV fixtures in `VoiceLite.Tests/Resources/`. The disposal tests stay.
- **`VoiceLite.Tests/Services/ModelResolverServiceTests.cs`** — drop the six GGML `NormalizeModelName` cases. Add Parakeet-directory-resolution tests (assert `ResolveModelPath` returns a directory containing the four files; assert `FileNotFoundException` when missing). Mock `IProFeatureService` — `CanUseModel` mock should now never gate.
- **`VoiceLite.Tests/Services/ProFeatureServiceTests.cs`** — flip the gating assertions: `CanUseModel("anything")` returns true; `IsModelAvailable("anything")` returns true; `GetAvailableModels()` returns single Parakeet entry. Drop the "free user can only use base" assertions.
- **`VoiceLite.Tests/Models/WhisperModelInfoTests.cs`** — reduce 5-model assertions to single Parakeet entry. `GetDisplayName("ggml-base.bin")` now returns `"(legacy)"`, not `"Swift"` — update expectations.
- **`VoiceLite.Tests/Integration/AudioPipelineTests.cs`** — service type still `PersistentWhisperService`, so compile path holds. The WAV→float[]→Sherpa→text round-trip needs a model directory in the test fixture path or the test must skip when model is absent.
- **`VoiceLite.Tests/Resources/ResourceLifecycleTests.cs`** — `OfflineRecognizer.Dispose()` must release native handles (critical per CLAUDE.md "Disposal tests are critical"). Adjust the test to construct a service, force a recognizer load, dispose, assert handles released. Skip-or-mock if no model files present.
- **`VoiceLite.Tests/Services/WhisperErrorRecoveryTests.cs`** — adapt cancellation/timeout scenarios for the blocking `Decode()` wrapped in `Task.Run`. Some tests may be skipped when model files are absent.
- **`VoiceLite.Tests/Stress/WhisperRecoveryStressTests.cs`** — rename test descriptions, adapt to Sherpa lifecycle. Disposal stress is critical (factory/recognizer dispose race).
- **`VoiceLite.Tests/Services/TranscriptionHistoryServiceTests.cs`** — flagged by grep but check whether actual changes needed (may only have incidental references).

**Add 2 new test files:**
- `VoiceLite.Tests/Services/SettingsMigrationTests.cs` — table-driven: each of `LegacyGgmlFileNames` + null + empty + already-migrated value → confirm correct mapping.
- `VoiceLite.Tests/Services/TextPostProcessorDevTermTests.cs` — case-insensitive substitution table for the dev-term dictionary added in Phase C (step 16).

### Manual end-to-end (clean Windows VM, before publishing the beta tag)

1. **Fresh install path.** Run new installer on clean Windows 10/11 VM. First launch → model download progress UI appears, downloads ~640MB tarball, extracts, app opens. Dictate one sentence with hotkey → text appears in target app via `TextInjector`.
2. **Upgrade path.** Install v1.4.x on a fresh VM, configure Pro license via Settings → AI Models, set `WhisperModel = "ggml-small.bin"`. Run new installer over it. First launch → `SettingsMigration` rewrites `WhisperModel` to Parakeet, model dir is missing → download UI fires → success. License still validates (`IsProLicense` survives untouched).
3. **License-restored path.** With Pro license active, confirm `ProFeatureService.IsProUser == true` and `ShowUpgradePrompt` no longer mentions "5 AI models." Confirm the future-Pro-feature visibilities (`VoiceShortcutsTabVisibility` etc.) still behave correctly.
4. **Offline first-launch.** Disconnect network on first launch. Model download fails → user sees a clear retry UI, not a crash. App doesn't hang.
5. **Hallucination smoke test.** Record 5 seconds of silence with hotkey. Confirm output is empty or near-empty (Parakeet shouldn't emit "Thanks for watching!" the way Whisper does).
6. **Multilingual smoke test.** Dictate a French or German sentence (Parakeet v3 is multilingual). Confirm reasonable transcription.
7. **Cancellation under load.** Start a long recording (30s+), cancel mid-transcription via the cancel hotkey. Confirm `OfflineRecognizer` cancellation works (the `Task.Run` wrapper carries the linked CTS).
8. **VC++ redistributable failure path.** On a Windows VM with no VC++ redistributable installed, confirm a friendly dialog appears instead of a `DllNotFoundException` crash (Sherpa-ONNX native depends on it).

### Release gate

- All `dotnet test` green.
- Manual E2E passes on Windows 11 + Windows 10 22H2.
- Installer size ≤ 200MB.
- First-launch model download tested over typical home broadband (≥ 50 Mbps) and slow connection (10 Mbps) — download completes within reasonable time.
- Push `v2.0.0-beta` tag; GitHub Actions builds and publishes as **prerelease**, not stable.

---

## Risk Callouts

- **Existing Pro customers paid for "5 AI models."** After this swap there's one model. Required: in-app changelog on first launch post-upgrade, explaining (a) Parakeet v3 outperforms their old Ultra model on WER, (b) their license is preserved and continues to validate, (c) Pro feature roadmap update is coming. Skipping this risks refund requests and Discord backlash.
- **CC-BY-4.0 attribution is mandatory.** Must ship the license file with installer, credit NVIDIA in About dialog + README. Non-compliance is a real licensing violation.
- **Sherpa-ONNX native DLL load failures** on Windows N/KN editions or systems missing VC++ redistributable. Add an early probe + clear "Install VC++ redistributable" dialog with a download link. Don't let users hit a cryptic `DllNotFoundException`.
- **Hugging Face rate-limiting at launch.** Mirror the Parakeet tarball on **this project's GitHub Releases** (already the pattern for `ggml-medium.bin` per `DownloadEndpoints.cs:14`). Don't point launch-day traffic at `huggingface.co/k2-fsa/...` directly.
- **First-launch UX regression for offline users.** Anyone who installs without network connectivity will hit the download flow blocking app entry. Acceptable risk for a dictation app (audio APIs work offline post-download), but the failure UI must be clear and the retry path obvious.
- **Initial-prompt loss for power users.** Dev users who relied on Whisper's vocab-biasing for terms like "TypeScript" may notice regressions on words outside the dictionary mitigation. Escalation path is a user-editable custom dictionary, which `ProFeatureService.CustomDictionaryTabVisibility` is already scaffolded for.
- **Disposal regressions.** CLAUDE.md flags that interface removal silently dropped `IDisposable` in the past. The new `ParakeetTranscriptionService` must explicitly implement `IDisposable` and `OfflineRecognizer.Dispose()` must release native handles. Validated by `ResourceLifecycleTests`.

---

## Out of Scope (Deferred)

- **Streaming / real-time transcription.** Sherpa-ONNX supports it (Parakeet v3 has a streaming variant in progress per k2-fsa GitHub issue #2918), but enabling streaming changes the recording pipeline (`AudioRecorder` would need to emit chunks, not finalize a WAV). Track as a follow-up after the swap ships.
- **Pro tier monetization strategy.** Single model means model-gating is dead. The four scaffolded Pro features (Voice Shortcuts, Export History, Custom Dictionary, Advanced Settings) need to be designed and shipped before Pro means anything new. Separate roadmap conversation.
- **Class renames.** `WhisperModelInfo` → `AsrModelInfo`, `whisper/` folder → `models/`, `PersistentWhisperService` references in comments/logs. Stylistic, not blocking. Do as a sweep in a follow-up PR.
- **Parakeet fp16 / fp32 variants.** Higher-quality variants exist; int8 is the right default. Revisit only if accuracy complaints arrive.

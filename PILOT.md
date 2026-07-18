# VoiceLite Pilot Deployment Notes

Guidance for deploying VoiceLite in healthcare or other privacy-sensitive contexts.

## Privacy Posture

VoiceLite is designed to keep voice and transcribed text **on the user's device**.

- **Transcription is in-process.** Sherpa-ONNX loads the NVIDIA Parakeet model into the VoiceLite process (native library, no subprocess) and runs inference locally on CPU. No audio or text is sent to any external service for transcription.
- **No telemetry, no analytics, no crash reporter.** The app makes exactly three kinds of outbound network calls, none of which carry audio or transcribed text: a version check against GitHub on each launch (default on, can be switched off), license activation (only when a user clicks Activate), and a one-time speech-model download on first launch. Each is documented in "What Leaves the Device" below.
- **Temporary audio files** are written to `%TEMP%\VoiceLite\audio\` during recording, then cleaned on every app startup and via a 30-minute sweep timer while the app is running.
- **Transcription history** is stored locally in `%LOCALAPPDATA%\VoiceLite\settings.json`. It is capped at the 250 most recent items (~500KB–1MB at full capacity), and on every app startup items older than 7 days are purged. Items the user has **pinned** are exempt from both the cap and the 7-day purge — they persist until unpinned or deleted. For clinical deployments where on-device history is undesirable, set `"EnableHistory": false` in `settings.json` (default: `true`) — transcriptions will never be written to the history list. Alternatively, the settings file can be cleared on logoff via a Windows scheduled task.

### Australian Privacy Principles (APPs) relevance

For Australian deployment: VoiceLite does not constitute "disclosure to an overseas recipient" (APP 8) for the transcription pipeline because nothing in that pipeline leaves the user's device. The only outbound calls are the update version check, license activation, and the first-launch model download — see below for exactly what each sends.

## What Leaves the Device

### Update check (every launch — default on, can be disabled)

A few seconds after each launch, the app makes one anonymous HTTPS GET to:

- `https://api.github.com/repos/mikha08-rgb/VoiceLite/releases/latest`

to see whether a newer VoiceLite version has been published. The request carries the User-Agent `VoiceLite-Desktop/UpdateCheck` and nothing else — no license key, no hardware ID, no machine name, no user data of any kind. (As with any HTTPS request, GitHub's servers see the machine's public IP address.) If a newer version exists, a tray notification is shown; nothing downloads or installs automatically.

**Off switch:** Settings → System Settings → uncheck "Check for updates on startup", or set `"CheckForUpdates": false` in `%LOCALAPPDATA%\VoiceLite\settings.json` (default: `true`). When disabled, no request is made at all. For locked-down clinical fleets where updates are pushed by IT, disabling this is reasonable — the app is fully functional without it.

### License activation (only when the user clicks Activate — never automatic)

The app does **not** contact the license server on startup, on a timer, or on any schedule. The only time it calls `https://voicelite.app/api/licenses/validate` (HTTPS POST) is when a user clicks **Activate** in Settings. That request contains:

| Field | Source | Notes |
|---|---|---|
| `licenseKey` | User's license key | Issued to the pilot account |
| `machineId` | `HardwareIdService.GetMachineId()` | SHA-256 of CPU ID + motherboard serial |
| `machineLabel` | `Environment.MachineName` | **The Windows computer name** — see naming guidance below |
| `machineHash` | `HardwareIdService.GetMachineHash()` | SHA-256 of CPU ID + motherboard serial + BIOS serial |

After successful activation, Pro status is stored locally (an `IsProLicense` flag in `settings.json`, backed by the DPAPI-encrypted `license.dat` — the app verifies the two agree on startup). From that point the license works **fully offline indefinitely** — there is no periodic re-validation and no background license traffic. A 14-day validation cache exists but is in-memory only within a single app session (it de-duplicates repeated Activate clicks); it is not written to disk and never generates network activity on its own.

The validate endpoint is rate-limited server-side: 30 validations/hour per IP, 30/hour per license key, and 1000/hour globally.

### Machine naming guidance

`Environment.MachineName` is sent whenever a license is activated (or re-activated after a license reset). Avoid Windows computer names that contain patient, provider, or clinic-identifying detail. Recommended convention:

- ✅ `GPH-CLIN-01`, `GPH-RECEP-02`, `GPH-DICT-03`
- ❌ `Dr-Smith-PC`, `Reception-Bay4-Nurse-Jones`, `PaedClinic-MainComputer`

This is a one-time setup task done in Windows Settings → System → About → Rename this PC.

### Model download (first launch only)

The speech model (~640MB) is not bundled with the installer. On first launch, VoiceLite downloads it from:
- `https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/...` (the Sherpa-ONNX project's official model releases on GitHub)

This is an anonymous GET. No user data is sent. After this one-time download the model lives at `%LOCALAPPDATA%\VoiceLite\models\` and no further model traffic occurs. For fully air-gapped deployment, the model directory can be pre-seeded by IT before first launch.

## Logging

The app writes diagnostic logs to `%LOCALAPPDATA%\VoiceLite\logs\voicelite.log`.

**As of the pilot build, no transcribed text is ever written to the log file** — only metadata (length in characters, transcription duration in ms, recording state transitions). Earlier development builds did log transcription snippets; if you are auditing an existing install, verify the build is the pilot release or later before relying on this guarantee.

**Recommendation for clinic IT:**
- Exclude `%LOCALAPPDATA%\VoiceLite\logs\` from cloud backups by default — even without transcription content, hardware IDs and timestamps are present.
- Consider a scheduled task that prunes log files older than 30 days.

## Clipboard behavior

When VoiceLite delivers transcribed text, it places the text on the Windows clipboard. How long it stays there depends on the paste mode:

- **Auto-paste ON (default):** VoiceLite pastes into the active window, then **auto-clears the clipboard 2 seconds after paste**.
- **Auto-paste OFF (manual paste):** the text is **held on the clipboard for 30 seconds** so the user has time to click into the target field and paste it themselves, then cleared.
- **Re-copying from the History panel** also uses the 30-second hold, since the user needs time to navigate to the paste target.

In all cases the clear is "match-before-clear" — it only clears the clipboard if the transcribed text is still on it (if the user copies something else in between, their content is preserved). For shared clinical workstations, prefer auto-paste ON for the shorter clipboard exposure.

## Remote desktop / Citrix caveat

VoiceLite delivers text via the clipboard plus a synthesized Ctrl+V keystroke, followed by the timed clipboard clears described above. This behavior is **untested under RDP, Citrix, or other remote-desktop clipboard redirection**: redirected clipboards can sync with a delay (the timed clear may race the redirection), and the synthesized paste targets the local foreground window, which may not be the intended remote application. If pilot workstations access clinical software through RDP or Citrix, recommend turning **AutoPaste off** and pasting manually (30-second clipboard hold), and validate the full dictation workflow on a test workstation before rollout.

## Settings & License Storage

- `%LOCALAPPDATA%\VoiceLite\settings.json` — preferences (hotkey, language, UI options) and, if history is enabled, recent transcriptions. No license key.
- `%LOCALAPPDATA%\VoiceLite\license.dat` — DPAPI-encrypted license key + email, tied to the Windows user account. Cannot be read by other users on the same machine.

## Pilot Onboarding Checklist

1. **Confirm Windows version** — VoiceLite requires Windows 10/11.
2. **Rename each pilot workstation** per the naming guidance above (before first launch).
3. **Issue pilot license keys** to each user via the licensing dashboard (`voicelite-web/manual-license.mjs`).
4. **Install VoiceLite** from the signed installer; first launch should be done by an admin/IT user.
5. **Activate license** via Settings on first launch (one-time, requires internet; no further license traffic afterwards).
6. **Decide on the update check** — leave it on for tray-notified updates, or disable it (Settings → "Check for updates on startup") if IT manages updates centrally.
7. **Verify privacy expectations** with the user: no audio leaves the device, transcription is local, clipboard auto-clears after paste (2s in auto-paste mode; 30s hold in manual-paste mode).

## Support

- Log file location: `%LOCALAPPDATA%\VoiceLite\logs\voicelite.log`
- Issue tracker: https://github.com/mikha08-rgb/VoiceLite/issues
- For privacy or compliance questions during the pilot, contact the project owner directly rather than filing a public issue.

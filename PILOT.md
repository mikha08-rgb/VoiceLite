# VoiceLite Pilot Deployment Notes

Guidance for deploying VoiceLite in healthcare or other privacy-sensitive contexts.

## Privacy Posture

VoiceLite is designed to keep voice and transcribed text **on the user's device**.

- **Transcription is in-process.** Whisper.net loads the model into the VoiceLite process via P/Invoke (`WhisperFactory.FromPath()`) and runs inference locally. No audio or text is sent to any external service for transcription.
- **No telemetry, no analytics, no crash reporter.** The app does not phone home for any reason other than license validation (see below).
- **Temporary audio files** are written to `%TEMP%\VoiceLite\audio\` during recording, then cleaned on every app startup and via a 30-minute sweep timer while the app is running.
- **Transcription history** is stored locally in `%LOCALAPPDATA%\VoiceLite\settings.json`, capped at the 250 most recent items (~500KB–1MB at full capacity). For clinical deployments where on-device history is undesirable, set `"EnableHistory": false` in `settings.json` (default: `true`) — transcriptions will never be written to the history list. Alternatively, the settings file can be cleared on logoff via a Windows scheduled task.

### Australian Privacy Principles (APPs) relevance

For Australian deployment: VoiceLite does not constitute "disclosure to an overseas recipient" (APP 8) for the transcription pipeline because nothing leaves the user's device. License validation is the only outbound network call — see below for exactly what it sends.

## What Leaves the Device

### License validation
On startup (and once every 14 days while cached), the app makes one HTTPS POST to `https://voicelite.app/api/licenses/validate` containing:

| Field | Source | Notes |
|---|---|---|
| `licenseKey` | User's license key | Issued to the pilot account |
| `machineId` | `HardwareIdService.GetMachineId()` | SHA-256 of CPU ID + motherboard serial |
| `machineLabel` | `Environment.MachineName` | **The Windows computer name** — see naming guidance below |
| `machineHash` | `HardwareIdService.GetMachineHash()` | SHA-256 of CPU ID + motherboard serial + BIOS serial |

The license server is rate-limited (5 validations/hour/IP) and caches results for 14 days locally — so most app launches do not trigger network activity.

### Machine naming guidance

`Environment.MachineName` is sent on each license validation. Avoid Windows computer names that contain patient, provider, or clinic-identifying detail. Recommended convention:

- ✅ `GPH-CLIN-01`, `GPH-RECEP-02`, `GPH-DICT-03`
- ❌ `Dr-Smith-PC`, `Reception-Bay4-Nurse-Jones`, `PaedClinic-MainComputer`

This is a one-time setup task done in Windows Settings → System → About → Rename this PC.

### Model downloads (user-initiated only)

When a user downloads additional Whisper models from the app, the download is fetched from one of:
- `https://huggingface.co/ggerganov/whisper.cpp/resolve/main/...`
- `https://github.com/mikha08-rgb/VoiceLite/releases/download/...`

These are anonymous GETs. No user data is sent.

## Logging

The app writes diagnostic logs to `%LOCALAPPDATA%\VoiceLite\logs\voicelite.log`.

**As of the pilot build, no transcribed text is ever written to the log file** — only metadata (length in characters, transcription duration in ms, recording state transitions). Earlier development builds did log transcription snippets; if you are auditing an existing install, verify the build is the pilot release or later before relying on this guarantee.

**Recommendation for clinic IT:**
- Exclude `%LOCALAPPDATA%\VoiceLite\logs\` from cloud backups by default — even without transcription content, hardware IDs and timestamps are present.
- Consider a scheduled task that prunes log files older than 30 days.

## Clipboard behavior

When VoiceLite pastes transcribed text into another application, it briefly places the text on the Windows clipboard. **As of the pilot build, the clipboard is auto-cleared 2 seconds after paste** to prevent transcribed content from lingering on shared workstations. The auto-clear is "match-before-clear" — it only clears the clipboard if the transcribed text is still on it (so if the user copies something else in between, their content is preserved).

## Settings & License Storage

- `%LOCALAPPDATA%\VoiceLite\settings.json` — preferences (hotkey, model selection, UI options). No PII, no license key.
- `%LOCALAPPDATA%\VoiceLite\license.dat` — DPAPI-encrypted license key + email, tied to the Windows user account. Cannot be read by other users on the same machine.

## Pilot Onboarding Checklist

1. **Confirm Windows version** — VoiceLite requires Windows 10/11.
2. **Rename each pilot workstation** per the naming guidance above (before first launch).
3. **Issue pilot license keys** to each user via the licensing dashboard (`voicelite-web/manual-license.mjs`).
4. **Install VoiceLite** from the signed installer; first launch should be done by an admin/IT user.
5. **Activate license** on first launch (one-time, requires internet).
6. **Verify privacy expectations** with the user: no audio leaves the device, transcription is local, clipboard auto-clears 2s after paste.

## Support

- Log file location: `%LOCALAPPDATA%\VoiceLite\logs\voicelite.log`
- Issue tracker: https://github.com/mikha08-rgb/VoiceLite/issues
- For privacy or compliance questions during the pilot, contact the project owner directly rather than filing a public issue.

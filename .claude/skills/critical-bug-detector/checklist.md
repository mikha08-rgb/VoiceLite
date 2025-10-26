# Pre-Release Checklist

## Critical Files (MUST exist)
- [ ] `VoiceLite/whisper/ggml-tiny.bin` (42MB)
- [ ] `VoiceLite/whisper/whisper.exe`
- [ ] `VoiceLite/VoiceLite.exe` (after build)

## Version Numbers (MUST match)
- [ ] `VoiceLite/VoiceLite/VoiceLite.csproj` → `<Version>`
- [ ] `VoiceLite/Installer/VoiceLiteSetup.iss` → `AppVersion=`
- [ ] `voicelite-web/package.json` → `"version":`
- [ ] `voicelite-web/app/api/download/route.ts` → default version

## Installer Script (VoiceLiteSetup.iss)
- [ ] References `ggml-tiny.bin`
- [ ] Correct source paths for all files
- [ ] Output filename matches version

## Build Configuration
- [ ] Building in Release mode (not Debug)
- [ ] No `#if !DEBUG` around logging code
- [ ] Tests pass

## Git State
- [ ] All changes committed
- [ ] Tag doesn't already exist
- [ ] Branch is master (or release branch)

## Post-Build Verification
- [ ] Installer size ~100-150MB (includes model)
- [ ] Test on clean VM/machine
- [ ] Whisper transcription works immediately
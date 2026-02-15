# VoiceLite Troubleshooting Guide

## Common Transcription Issues

### Issue: Words Cut Off at End
**Symptoms**: Last word or two missing from transcription
**Cause**: Audio buffer too small or silence detection too aggressive
**Fix**:
```csharp
// AudioRecorder.cs - Increase buffer
new WaveInEvent { BufferMilliseconds = 100 } // was 50

// Reduce silence detection sensitivity if implemented
```

### Issue: Poor Accuracy on Technical Terms
**Symptoms**: "useState" → "use state", "forEach" → "for each", "npm" → "and PM"
**Cause**: Model too small or temperature too high
**Fix**:
1. Switch to Small model or higher (Pro feature)
2. Lower temperature to 0.0 for technical dictation:
   ```csharp
   // PersistentWhisperService.cs
   --temperature 0.0  // was 0.2
   ```
3. Use prompt with expected vocabulary (Whisper v3+):
   ```csharp
   --prompt "React useState useEffect forEach npm axios git"
   ```

### Issue: High Transcription Latency
**Symptoms**: Long delay after speaking before text appears
**Cause**: Process not warmed up, model too large, or timeout multiplier too high
**Fix**:
1. Ensure warmup runs on startup (already implemented)
2. Consider using smaller model for better UX
3. Check timeout calculation:
   ```csharp
   var timeout = (int)(audioLength * 2000); // 2x is usually enough
   ```

### Issue: Inconsistent Accuracy
**Symptoms**: Same phrase transcribed differently each time
**Cause**: Audio quality variation or preprocessing issues
**Fix**:
1. Ensure consistent audio format: 16kHz, 16-bit, mono WAV
2. Check microphone input level (not too low/high)
3. Verify no background noise interference

## Temperature Tuning Guide

| Temperature | Use Case | Example Output |
|-------------|----------|----------------|
| 0.0 | Technical dictation, code | `useState`, `forEach` (exact) |
| 0.2 | Balanced (default) | Good for general use |
| 0.3-0.4 | Natural speech | Better context understanding |
| 0.5+ | Creative writing | More variation (avoid for technical) |

## Audio Format Issues

| Problem | Symptom | Solution |
|---------|---------|----------|
| Wrong sample rate | Distorted/garbled transcription | Ensure 16kHz sampling |
| Stereo audio | Inconsistent results | Convert to mono |
| Low volume (<-40dB) | Missing words | Increase mic gain |
| Clipping (>0dB) | Garbled output | Reduce input gain |
| Background noise | Poor accuracy | Use noise gate or quieter environment |

## Test Phrases for Validation

Use these phrases to validate accuracy after changes:

**Technical Terms:**
1. "Create a new useState hook and forEach loop"
2. "npm install react router dom"
3. "git commit dash m initial commit"
4. "const axios equals require axios"
5. "import React comma useEffect from react"

**Expected Results (Small model or higher):**
1. ✅ `useState` (not "use state")
2. ✅ `forEach` (not "for each")
3. ✅ `npm` (not "NPM" or "and pm")
4. ✅ `git` (not "get")
5. ✅ Proper punctuation placement

## Build & Installation Issues

### Issue: "File is locked" during build
**Cause**: VoiceLite.exe still running
**Fix**:
```bash
taskkill /F /IM VoiceLite.exe
```

### Issue: "VCRUNTIME140_1.dll not found"
**Cause**: Missing Visual C++ Runtime
**Fix**: Installer auto-installs it, or manually install VC++ 2015-2022 x64

### Issue: Whisper model not found
**Cause**: Model file missing or path incorrect
**Fix**: Verify `VoiceLite/whisper/ggml-base.bin` exists (142MB)

## Performance Benchmarks

Expected transcription times (excluding model load):
- **Tiny (Q8_0)**: <0.8s for 5s audio
- **Small (Q8_0)**: ~3s for 5s audio
- **Medium**: ~12s for 5s audio
- **Large (Q8_0)**: ~8s for 5s audio

First transcription includes model load time (+2-5s).
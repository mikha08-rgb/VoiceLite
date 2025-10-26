# Quick Fixes for Transcription Issues

## No Text Appears (Silent Failure)

### Fix 1: Check Event Handler
```csharp
// MainWindow.xaml.cs
// ADD THIS if missing:
_audioRecorder.AudioFileReady += OnAudioFileReady;
```

### Fix 2: Enable Logging in Release
```csharp
// ErrorLogger.cs
// REMOVE any #if DEBUG blocks around logging
ErrorLogger.Log("Message"); // Should work in all builds
```

### Fix 3: Kill Zombie Processes
```bash
taskkill /F /IM whisper.exe
taskkill /F /IM VoiceLite.exe
# Then restart
```

## Poor Accuracy

### Fix 1: Lower Temperature
```csharp
// PersistentWhisperService.cs
--temperature 0.0  // For technical terms (was 0.2)
```

### Fix 2: Use Better Model (Pro)
- Tiny: 70-80% accuracy
- Small: 90-93% accuracy ← Recommended
- Medium: 93-95% accuracy

## Slow Transcription

### Fix 1: Check Model Size
```bash
# Using wrong model?
ls -lh VoiceLite/whisper/*.bin
# Tiny should be 42MB, not 466MB (Small)
```

### Fix 2: Optimize Parameters
```bash
--beam-size 1      # Greedy (fast)
--entropy-thold 3.0  # Skip silence
--no-fallback      # Don't retry
```

## Process Hangs

### Fix 1: Add Timeout
```csharp
var timeout = audioLength * 2000; // 2x audio length
if (!process.WaitForExit(timeout))
{
    process.Kill();
}
```

### Fix 2: Check Audio File
```bash
# Is audio file valid?
ffmpeg -i temp_audio.wav -f null -
# Should show: 16000 Hz, mono, s16
```

## Event Not Firing

### Fix 1: Add Diagnostic Logging
```csharp
// Add to every step:
ErrorLogger.Log("Step 1: Starting recording");
ErrorLogger.Log("Step 2: Stopped, file saved to: " + path);
ErrorLogger.Log("Step 3: AudioFileReady raised");
ErrorLogger.Log("Step 4: Transcription started");
```

### Fix 2: Check Disposal
```csharp
// Don't dispose services too early
// MainWindow.OnClosed() should dispose, not before
```

## Emergency Reset

If nothing works:
```bash
# 1. Kill all processes
taskkill /F /IM VoiceLite.exe
taskkill /F /IM whisper.exe

# 2. Clear temp files
del %TEMP%\voicelite_*.wav

# 3. Reset settings
del %LOCALAPPDATA%\VoiceLite\settings.json

# 4. Rebuild in Debug mode first
dotnet build VoiceLite/VoiceLite.sln -c Debug

# 5. Test with debug logging
# Then switch to Release
```
# Whisper AI Expertise for VoiceLite

## Model Selection Guide

### Available Models
| Model | Size | Accuracy | Speed | Use Case |
|-------|------|----------|-------|----------|
| Tiny (ggml-tiny.bin) | 75MB | 70-80% | Fastest | Legacy fallback only |
| Small (ggml-small.bin) | 466MB | 90-93% | Fast | **Default (ships with installer)** |
| Base (ggml-base.bin) | 142MB | 85-88% | Fast | Swift - good balance |
| Medium (ggml-medium.bin) | 1.5GB | 93-95% | Moderate | Elite - optional download |
| Large v3 (ggml-large-v3.bin) | 2.9GB | 95-97% | Slow | Ultra - manual download |

### Model Selection Criteria
- **Technical dictation** (code, commands): Use Small or Medium minimum
- **General dictation**: Small is sufficient
- **Low-resource systems**: Base acceptable, Tiny as last resort
- **Maximum accuracy**: Medium or Large

## Whisper Command Parameters

### Current Settings (v1.0.65+)
```bash
whisper.exe -m [model] -f [audio.wav] \
  --no-timestamps \
  --language en \
  --temperature 0.2 \
  --beam-size 1 \
  --best-of 1
```

**Note**: VoiceLite uses **greedy decoding** for 5x speed improvement over beam search.

### Parameter Explanations
- **--no-timestamps**: Removes timestamps from output (cleaner text)
- **--language en**: Forces English (supports 99 languages)
- **--temperature 0.2**: Balance between accuracy and creativity
  - `0.0`: Most deterministic (best for technical terms)
  - `0.2`: Balanced (default)
  - `0.5+`: More creative (worse for technical accuracy)
- **--beam-size 1**: Greedy decoding (fastest, v1.0.65+)
  - Legacy: `--beam-size 5` for beam search (slower but more accurate)
- **--best-of 1**: Single sampling (fastest, v1.0.65+)
  - Legacy: `--best-of 5` for multiple candidates (slower)

### Temperature Tuning
**Problem**: Poor accuracy on technical jargon (useState, forEach, npm)
**Solution**: Lower temperature to 0.0
```csharp
// PersistentWhisperService.cs
- --temperature 0.2
+ --temperature 0.0
```

**Problem**: Transcription too literal, missing context
**Solution**: Increase temperature to 0.3-0.4

### Prompt Engineering (Whisper v3+)
**Feature**: Prime model with expected vocabulary
```csharp
var techTerms = "React hooks useState useEffect forEach map reduce async await";
var command = $"--prompt \"{techTerms}\"";
```
**Limitation**: Max 244 tokens in prompt

## Audio Format Requirements

### Required Format for Whisper
- **Sample rate**: 16kHz (16000 Hz)
- **Bit depth**: 16-bit
- **Channels**: Mono (1 channel)
- **Format**: WAV (PCM)

### Common Audio Issues
| Problem | Symptom | Fix |
|---------|---------|-----|
| Wrong sample rate | Distorted transcription | Resample to 16kHz |
| Stereo audio | Inconsistent results | Convert to mono |
| Low volume | Missing words | Apply gain control |
| Background noise | Poor accuracy | Apply noise gate |
| Clipping | Garbled output | Reduce input gain |

## Process Lifecycle Best Practices

### Semaphore Control
```csharp
// Ensure only 1 transcription at a time
private readonly SemaphoreSlim _semaphore = new(1, 1);

public async Task<string> TranscribeAsync(string audioPath)
{
    await _semaphore.WaitAsync();
    try
    {
        return await RunWhisperProcess(audioPath);
    }
    finally
    {
        _semaphore.Release();
    }
}
```

### Timeout Management
```csharp
// Timeout based on audio length
var audioLength = GetAudioDuration(audioPath);
var timeout = (int)(audioLength * 3000); // 3x audio duration in ms

if (!process.WaitForExit(timeout))
{
    process.Kill();
    throw new TimeoutException($"Whisper timeout after {timeout}ms");
}
```

### Process Disposal
```csharp
Process process = null;
try
{
    process = new Process { StartInfo = startInfo };
    process.Start();
    // ... read output ...
}
finally
{
    process?.Dispose();
}
```

## Performance Optimization

### Path Caching
```csharp
private static string? _cachedWhisperPath;
private static string? _cachedModelPath;

private string GetWhisperPath()
{
    if (_cachedWhisperPath == null)
    {
        _cachedWhisperPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "whisper",
            "whisper.exe"
        );
    }
    return _cachedWhisperPath;
}
```

### Memory Management
- Dispose audio buffers immediately after use
- Don't hold references to transcription results
- Clear temporary WAV files after transcription

### Latency Targets
- **Warmup**: < 3 seconds on startup
- **Transcription**: < 200ms after audio processing (excluding Whisper model time)
- **Model load time**:
  - Small: ~2 seconds first time, <500ms warmed up
  - Medium: ~5 seconds first time, ~1s warmed up

## Common Transcription Issues

### Issue: Words Cut Off at End
**Cause**: Audio buffer too small or silence detection too aggressive
**Fix**:
```csharp
// AudioRecorder.cs - Increase buffer
new WaveInEvent { BufferMilliseconds = 100 } // was 50

// AudioPreprocessor.cs - Reduce noise gate threshold
noiseGate.Threshold = -50; // was -40 (dB)
```

### Issue: Technical Terms Wrong
**Cause**: Model too small or temperature too high
**Fix**:
1. Upgrade to Small or Medium model
2. Lower temperature to 0.0
3. Use prompt with technical vocabulary

### Issue: High Latency
**Cause**: Process not warmed up, or model too large
**Fix**:
1. Ensure warmup runs on startup
2. Consider smaller model for better UX
3. Check timeout multiplier (should be 2-3x audio length)

### Issue: Inconsistent Accuracy
**Cause**: Audio quality variation
**Fix**:
1. Implement noise gate and AGC in preprocessing
2. Validate audio format (16kHz, 16-bit, mono)
3. Check microphone input level

## Language Support

### Supported Languages (99 total)
Whisper supports 99 languages. Default is English (`en`).

### Changing Language
```csharp
// Settings.cs
public string Language { get; set; } = "en"; // ISO 639-1 code

// PersistentWhisperService.cs
--language {settings.Language}
```

### Common Language Codes
- `en`: English
- `es`: Spanish
- `fr`: French
- `de`: German
- `zh`: Chinese
- `ja`: Japanese
- `ko`: Korean

## Testing & Validation

### Test Phrases for Technical Accuracy
```
1. "Create a new useState hook and forEach loop"
2. "npm install react router dom"
3. "git commit and git push origin master"
4. "const axios equals require axios"
5. "import React comma use effect from react"
```

### Benchmarking Models
```csharp
// Run benchmark with standard test phrases
var testPhrases = LoadTestPhrases();
foreach (var model in models)
{
    var accuracy = MeasureAccuracy(model, testPhrases);
    var latency = MeasureLatency(model);
    Console.WriteLine($"{model}: {accuracy:P0} accuracy, {latency}ms latency");
}
```

### Expected Accuracy Ranges
- **Tiny**: 70-80% on general speech, 50-60% on technical terms
- **Small**: 90-93% on general speech, 85-90% on technical terms
- **Medium**: 93-95% on general speech, 92-95% on technical terms
- **Large**: 95-97% on general speech, 95-97% on technical terms

## References
- Whisper GitHub: https://github.com/openai/whisper
- Whisper.cpp (used by VoiceLite): https://github.com/ggerganov/whisper.cpp
- Model downloads: https://huggingface.co/ggerganov/whisper.cpp

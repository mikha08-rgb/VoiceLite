---
description: Benchmark Whisper model performance
argument-hint: [model-name]
---

# Benchmark Whisper Model

Test model performance with standard phrases:

## Test Audio
Create test audio with these phrases:
1. "Create a new useState hook and forEach loop"
2. "npm install react router dom"
3. "git commit dash m initial commit"
4. "const axios equals require axios"

## Run Benchmark
```bash
./whisper.exe -m ggml-$1.bin -f test.wav --no-timestamps --language en --temperature 0.0 --beam-size 1 --entropy-thold 3.0
```

## Report
- Transcription accuracy (% words correct)
- Processing time
- Memory usage
- Compare to expected results in troubleshooting.md
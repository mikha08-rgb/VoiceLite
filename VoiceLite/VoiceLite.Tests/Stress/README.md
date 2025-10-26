# VoiceLite Stress Tests

## Overview

Stress tests validate that VoiceLite can handle sustained, real-world usage without:
- Memory leaks
- Resource exhaustion (file handles, processes)
- Performance degradation
- Crashes

## Running Stress Tests

Stress tests are **skipped by default** because they take several minutes to run.

### Run All Stress Tests

```bash
dotnet test VoiceLite/VoiceLite.Tests/VoiceLite.Tests.csproj --filter "Category=Stress"
```

### Run Specific Stress Test

```bash
# Transcription stress tests
dotnet test --filter "FullyQualifiedName~TranscriptionStressTests"

# Recording stress tests
dotnet test --filter "FullyQualifiedName~RecordingStressTests"

# Whisper recovery tests
dotnet test --filter "FullyQualifiedName~WhisperRecoveryStressTests"
```

### Run Individual Test

```bash
dotnet test --filter "FullyQualifiedName~StressTest_100ConsecutiveTranscriptions_NoMemoryLeak"
```

## Test Categories

### Transcription Stress Tests
- **100 Consecutive Transcriptions**: Validates no memory leaks over 100 iterations
- **Concurrent Transcriptions**: Tests thread safety with 5 parallel tasks
- **Long Running Session**: Simulates a user's full work session (100 transcriptions with breaks)

**Time**: ~5-10 minutes
**Memory Limit**: 50MB growth
**Success Criteria**: ≥90% success rate, no crashes

### Recording Stress Tests
- **Rapid Start/Stop Cycles**: 100 quick start/stop cycles (100ms each)
- **Multiple Recorder Instances**: 50 sequential recorder instances
- **Start/Stop Without Recording**: Edge case test (immediate stop)
- **Dispose During Recording**: Cleanup validation

**Time**: ~2-5 minutes
**Memory Limit**: 20-30MB growth
**Success Criteria**: ≥90% success rate, proper disposal

### Whisper Recovery Stress Tests
- **Mixed Success/Failure**: Alternates between good and corrupted audio
- **Consecutive Failures**: 10 failures followed by 5 successes (recovery test)
- **Multiple Services Sequential**: 20 service instances (process cleanup test)
- **Empty/Tiny Audio Files**: Edge case handling

**Time**: ~3-8 minutes
**Memory Limit**: 25-40MB growth
**Success Criteria**: Graceful failure handling, service recovery

## Interpreting Results

### Passing Test Example

```
=== Stress Test Results ===
Total iterations: 100
Successes: 98
Failures: 2
Success rate: 98.0%
Duration - Avg: 850ms, Min: 720ms, Max: 1200ms
Performance degradation: 5.2% (first 10 avg: 820ms, last 10 avg: 863ms)

100 Consecutive Transcriptions memory check:
  Initial: 85.42 MB
  Final: 98.15 MB
  Growth: 12.73 MB
  Peak: 105.21 MB
  Limit: 50.00 MB
  Duration: 95.3s

✅ PASSED
```

**Analysis**: Excellent - only 12.73MB growth over 100 iterations, minimal performance degradation.

### Failing Test Example

```
=== Stress Test Results ===
Total iterations: 100
Successes: 100
Success rate: 100.0%

100 Consecutive Transcriptions memory check:
  Initial: 85.42 MB
  Final: 198.55 MB
  Growth: 113.13 MB ❌
  Peak: 215.78 MB
  Limit: 50.00 MB

❌ FAILED: Memory leak detected! Growth: 113.13 MB exceeds limit of 50.00 MB
```

**Analysis**: Memory leak - growing >1MB per iteration. Check for:
- Undisposed resources
- Event handler leaks
- File handle leaks
- Process cleanup failures

## Troubleshooting

### Tests Skip Automatically

**Cause**: Tests have `Skip` attribute
**Fix**: Remove the `Skip` parameter from the test, or use `--filter "Category=Stress"`

### Tests Fail Due to Missing Model

**Cause**: `ggml-tiny.bin` not found in test output directory
**Fix**: Copy model file to `VoiceLite.Tests/bin/Debug/net8.0-windows/whisper/ggml-tiny.bin`

```bash
mkdir -p "VoiceLite/VoiceLite.Tests/bin/Debug/net8.0-windows/whisper"
cp "VoiceLite/whisper/ggml-tiny.bin" "VoiceLite/VoiceLite.Tests/bin/Debug/net8.0-windows/whisper/"
```

### Memory Growth Exceeds Limit

**Investigation Steps**:
1. Run test again - is growth consistent?
2. Check for unclosed file handles (`Process Explorer > Handles`)
3. Check for zombie processes (`tasklist | findstr whisper`)
4. Review disposal code in failing component

### Tests Timeout

**Cause**: Whisper model too slow or system overloaded
**Fix**:
- Use `tiny` model instead of `small/medium`
- Close other applications
- Increase timeout in test code

## When to Run Stress Tests

### Before Release
- ✅ Run all stress tests
- ✅ Verify all pass with acceptable memory growth
- ✅ Check for performance degradation

### After Major Changes
- Phase 1-2: Resource management changes → Run all stress tests
- Audio recording changes → Run Recording stress tests
- Whisper service changes → Run Transcription + Recovery stress tests

### During Development
- Optional - stress tests are slow
- Run if you suspect memory leak or resource issue
- Focus on specific test category related to your change

## Performance Baselines (v1.0.96)

| Test | Iterations | Time | Memory Growth | Success Rate |
|------|-----------|------|---------------|--------------|
| 100 Consecutive Transcriptions | 100 | ~8min | ~15MB | 100% |
| Rapid Start/Stop | 100 | ~2min | ~10MB | 100% |
| Concurrent Transcriptions | 50 | ~5min | ~25MB | 98% |
| Multiple Services | 20 | ~3min | ~20MB | 100% |

**Note**: Baselines are with `tiny` model on Windows 10, Intel i7, 16GB RAM

## Contributing

When adding new stress tests:
1. Inherit from `StressTestBase`
2. Add `[Trait("Category", "Stress")]`
3. Add `Skip` attribute with descriptive message
4. Document expected behavior in test comments
5. Use `AssertMemoryWithinLimits()` for memory checks
6. Log progress every 10 iterations for long tests

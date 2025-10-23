# VoiceLite Production Telemetry Guide

## Overview

VoiceLite includes a **privacy-first, opt-in telemetry system** (`SimpleTelemetry.cs`) that tracks performance, reliability, and usage metrics to help understand real-world app behavior.

**Key Principles**:
- ✅ **Opt-in only** - Respects existing `settings.EnableAnalytics` flag
- ✅ **Anonymous** - Uses SHA256 `AnonymousUserId` (no PII)
- ✅ **Local-first** - Metrics stored locally in `%LOCALAPPDATA%/VoiceLite/telemetry/{date}.json`
- ✅ **Non-blocking** - Never impacts UI responsiveness (<5ms overhead per metric)
- ✅ **Fail-safe** - Silent failures, never crashes the app

---

## Metric Types

### 1. Performance Metrics ⚡

#### `app_start_time_ms`
**What it measures**: Time from process start to full app initialization
**Formula**: Time between app start and `TrackAppStart()` call
**Target**: <3000ms (3 seconds)
**Impact**: High - First impression, cold start experience

**Metadata**:
- `version`: App version (e.g., "1.0.62")

**Example**:
```json
{
  "metricType": "app_start_time_ms",
  "value": 2850,
  "metadata": { "version": "1.0.62" }
}
```

---

#### `hotkey_response_time_ms`
**What it measures**: Time from hotkey press to recording start
**Formula**: Time between `TrackHotkeyResponseStart()` and `TrackHotkeyResponseEnd()`
**Target**: <200ms
**Impact**: Critical - Core UX, determines app "snappiness"

**Metadata**:
- `threshold_ms`: 200 (target threshold for good UX)

**Example**:
```json
{
  "metricType": "hotkey_response_time_ms",
  "value": 145,
  "metadata": { "threshold_ms": 200 }
}
```

---

#### `transcription_duration_ms`
**What it measures**: Total time from recording start to text injection
**Formula**: Recording + audio processing + Whisper transcription + text injection
**Target**: Varies by audio length (typically <2000ms for 3-second recording)
**Impact**: High - Primary feature latency

**Metadata**:
- `modelUsed`: Whisper model (e.g., "ggml-small.bin")
- `wordCount`: Number of words transcribed
- `success`: Boolean - whether transcription succeeded
- `avgDurationMs`: Session average transcription time

**Example**:
```json
{
  "metricType": "transcription_duration_ms",
  "value": 1820,
  "metadata": {
    "modelUsed": "ggml-small.bin",
    "wordCount": 12,
    "success": true,
    "avgDurationMs": 1950
  }
}
```

---

#### `memory_usage_mb`
**What it measures**: Current app memory usage
**Formula**: `Process.WorkingSet64 / (1024 * 1024)` (RAM in MB)
**Target**: <300MB
**Impact**: Medium - Resource efficiency, long-running stability

**Metadata**:
- `sessionDurationMinutes`: Time since app start
- `transcriptionCount`: Total transcriptions this session

**Example**:
```json
{
  "metricType": "memory_usage_mb",
  "value": 185.4,
  "metadata": {
    "sessionDurationMinutes": 45,
    "transcriptionCount": 38
  }
}
```

---

### 2. Reliability Metrics 🛡️

#### `crash`
**What it measures**: Unhandled exceptions that crash the app
**Formula**: Count of app terminations due to unhandled exceptions
**Target**: 0
**Impact**: Critical - Complete app failure, data loss

**Metadata**:
- `errorType`: Exception type (e.g., "NullReferenceException")
- `component`: Component where crash occurred (e.g., "MainWindow")

**Example**:
```json
{
  "metricType": "crash",
  "value": 1,
  "metadata": {
    "errorType": "NullReferenceException",
    "component": "PersistentWhisperService"
  }
}
```

---

#### `error`
**What it measures**: Recoverable errors (caught exceptions)
**Formula**: Count of handled exceptions logged
**Target**: <10 per 24 hours
**Impact**: Medium - Degraded UX, potential data loss

**Metadata**:
- `errorType`: Error category (e.g., "TranscriptionTimeout", "FileAccessDenied")
- `component`: Component where error occurred
- `totalErrorsThisSession`: Running count of errors this session

**Example**:
```json
{
  "metricType": "error",
  "value": 1,
  "metadata": {
    "errorType": "TranscriptionTimeout",
    "component": "PersistentWhisperService",
    "totalErrorsThisSession": 3
  }
}
```

---

#### `feature_attempt`
**What it measures**: Success/failure rate of core features
**Formula**: `value = 1` for success, `value = 0` for failure
**Target**: >95% success rate
**Impact**: High - Feature reliability

**Metadata**:
- `featureName`: Feature name (e.g., "recording", "transcription", "text_injection")
- `success`: Boolean
- `failureReason`: If failed, reason why (e.g., "MicrophoneAccessDenied")

**Example (success)**:
```json
{
  "metricType": "feature_attempt",
  "value": 1,
  "metadata": {
    "featureName": "transcription",
    "success": true
  }
}
```

**Example (failure)**:
```json
{
  "metricType": "feature_attempt",
  "value": 0,
  "metadata": {
    "featureName": "recording",
    "success": false,
    "failureReason": "MicrophoneAccessDenied"
  }
}
```

---

#### `recovery_attempt`
**What it measures**: Success/failure of automatic recovery mechanisms
**Formula**: `value = 1` for successful recovery, `value = 0` for failed recovery
**Target**: >90% success rate
**Impact**: Medium - App resilience, self-healing capability

**Metadata**:
- `recoveryType`: Recovery mechanism (e.g., "RestartWhisperProcess", "RetryTranscription")
- `success`: Boolean

**Example**:
```json
{
  "metricType": "recovery_attempt",
  "value": 1,
  "metadata": {
    "recoveryType": "RestartWhisperProcess",
    "success": true,
    "totalRecoveriesThisSession": 2
  }
}
```

---

### 3. Usage Metrics 📊

#### `daily_active_user`
**What it measures**: Unique users per day
**Formula**: Count of unique `AnonymousUserId` with at least one app launch
**Target**: N/A (growth metric)
**Impact**: High - Growth, retention, market fit

**Metadata**:
- `tier`: "FREE" or "PRO"
- `version`: App version
- `osVersion`: Windows version (e.g., "Windows 11")

**Example**:
```json
{
  "metricType": "daily_active_user",
  "value": 1,
  "metadata": {
    "tier": "FREE",
    "version": "1.0.62",
    "osVersion": "Windows 11"
  }
}
```

---

#### `transcriptions_per_session`
**What it measures**: Total transcriptions per session
**Formula**: Count of successful transcriptions from app start to app close
**Target**: N/A (engagement metric)
**Impact**: High - Engagement, feature usage intensity

**Metadata**:
- `sessionDurationMinutes`: Session length
- `avgTranscriptionsPerHour`: Transcriptions / hours

**Example**:
```json
{
  "metricType": "transcriptions_per_session",
  "value": 38,
  "metadata": {
    "sessionDurationMinutes": 45,
    "avgTranscriptionsPerHour": 50.6
  }
}
```

---

####  `feature_usage`
**What it measures**: Feature adoption and usage frequency
**Formula**: Count of feature activations
**Target**: N/A (adoption metric)
**Impact**: Medium - Feature discoverability, UX effectiveness

**Metadata**:
- `featureName`: Feature name (e.g., "custom_dictionary", "voice_shortcuts", "history_panel")

**Example**:
```json
{
  "metricType": "feature_usage",
  "value": 1,
  "metadata": {
    "featureName": "voice_shortcuts"
  }
}
```

---

#### `session_length_minutes`
**What it measures**: Total session duration (app open time)
**Formula**: Time from app start to app close
**Target**: N/A (engagement metric)
**Impact**: High - Engagement, daily usage patterns

**Metadata**:
- `transcriptionCount`: Total transcriptions this session
- `errorCount`: Total errors this session
- `recoveryAttempts`: Total recovery attempts this session

**Example**:
```json
{
  "metricType": "session_length_minutes",
  "value": 125.3,
  "metadata": {
    "transcriptionCount": 92,
    "errorCount": 1,
    "recoveryAttempts": 0
  }
}
```

---

## Dashboard Access

**URL**: `https://voicelite.app/metrics_dashboard.html` (after deploying to Vercel)

**Features**:
- Real-time metrics aggregation
- Time range filters (1h, 24h, 7d, 30d)
- Performance, reliability, and usage breakdowns
- Top errors and popular features
- Auto-refresh every 30 seconds (optional)

---

## Privacy Guarantees

### What is Tracked:
✅ Performance timings (app start, hotkey response, transcription duration)
✅ Memory usage
✅ Error counts and types (no stack traces, no file paths)
✅ Feature usage counts
✅ Session duration and transcription counts

### What is NOT Tracked:
❌ Recording content or transcription text
❌ File paths or directory names
❌ User names, emails, or PII
❌ IP addresses (set to `null` in backend)
❌ Device fingerprints (only SHA256 anonymous ID)

---

## Local Storage

**Directory**: `%LOCALAPPDATA%/VoiceLite/telemetry/`
**Format**: Daily JSON files (`2025-10-08.json`)
**Retention**: Unlimited (user-managed)
**Structure**: One JSON object per line (newline-delimited JSON)

**Example local telemetry file**:
```json
{"timestamp":"2025-10-08T14:23:15Z","anonymousUserId":"a3f2...","metricType":"app_start_time_ms","value":2850,"metadata":{"version":"1.0.62"}}
{"timestamp":"2025-10-08T14:25:10Z","anonymousUserId":"a3f2...","metricType":"transcription_duration_ms","value":1820,"metadata":{"modelUsed":"ggml-small.bin","wordCount":12,"success":true}}
```

---

## Upload Process

**Frequency**: Every 10 minutes
**Batch Size**: Up to 100 metrics per upload
**Endpoint**: `POST /api/metrics/upload`
**Rate Limit**: 50 batches/hour per user (5000 metrics/hour max)
**Failure Handling**: Silent - metrics stored locally until next upload

---

## Troubleshooting

### Metrics not appearing in dashboard?

1. **Check analytics opt-in**: Settings → Privacy → "Enable Analytics" must be `true`
2. **Check local telemetry files**: `%LOCALAPPDATA%/VoiceLite/telemetry/` should have daily JSON files
3. **Check network**: Metrics upload requires internet connection
4. **Check backend**: Visit `https://voicelite.app/api/metrics/dashboard?timeRange=24h` to verify API is working

### High memory usage reported?

- **Expected**: 150-200MB during active use
- **Warning**: 200-300MB (may indicate memory leak)
- **Critical**: >300MB (investigate MemoryMonitor.cs and zombie processes)

### High error counts?

- Check `/api/metrics/dashboard` → "Top Errors" section
- Cross-reference with `%LOCALAPPDATA%/VoiceLite/logs/voicelite.log`
- File bug reports with error types and frequencies

---

## Implementation Notes

**SimpleTelemetry.cs**:
- Located in `VoiceLite/VoiceLite/Services/`
- Initialized in `MainWindow.xaml.cs`
- Disposed on app close (calls `TrackSessionEnd()`)

**Integration Points**:
- `MainWindow_Loaded` → `TrackAppStart()`
- Hotkey press → `TrackHotkeyResponseStart()`
- Recording start → `TrackHotkeyResponseEnd()`
- Transcription complete → `TrackTranscriptionDuration()`
- Error logging → `TrackError()`
- App close → `TrackSessionEnd()`

---

## Changelog

**v1.0.63** (2025-10-08):
- Initial production telemetry system
- 12 metric types (performance, reliability, usage)
- Privacy-first design (opt-in, anonymous, local-first)
- Dashboard at `/metrics_dashboard.html`
- Batch upload every 10 minutes

---

## Support

For questions or issues:
- GitHub Issues: https://github.com/mikha08-rgb/VoiceLite/issues
- Metrics Dashboard: https://voicelite.app/metrics_dashboard.html
- Privacy Policy: https://voicelite.app/privacy

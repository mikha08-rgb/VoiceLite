using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VoiceLite.Models;
using VoiceLite.Services.Auth;

namespace VoiceLite.Services
{
    /// <summary>
    /// Lightweight production telemetry service for tracking performance, reliability, and usage metrics.
    /// Privacy-first: Respects existing analytics opt-in, never blocks UI, stores locally first.
    /// </summary>
    public class SimpleTelemetry : IDisposable
    {
        private readonly Settings settings;
        private readonly string telemetryDirectory;
        private readonly string todayTelemetryFile;
        private readonly Timer uploadTimer;
        private readonly object telemetryLock = new object();
        private readonly List<TelemetryMetric> metricQueue = new List<TelemetryMetric>();
        private readonly Stopwatch appStartStopwatch;
        private bool disposed = false;

        // Performance metrics state
        private DateTime sessionStartTime;
        private int sessionTranscriptionCount = 0;
        private long totalTranscriptionTimeMs = 0;
        private DateTime? lastHotkeyPressTime;

        // Reliability metrics state
        private int errorCountThisSession = 0;
        private Dictionary<string, int> errorCountsByType = new Dictionary<string, int>();
        private int recoveryAttempts = 0;

        public SimpleTelemetry(Settings settings)
        {
            this.settings = settings;
            this.appStartStopwatch = Stopwatch.StartNew();
            this.sessionStartTime = DateTime.UtcNow;

            // Setup telemetry directory: %LOCALAPPDATA%/VoiceLite/telemetry/
            telemetryDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoiceLite",
                "telemetry");

            Directory.CreateDirectory(telemetryDirectory);

            // Daily telemetry file: telemetry/{date}.json
            todayTelemetryFile = Path.Combine(telemetryDirectory, $"{DateTime.UtcNow:yyyy-MM-dd}.json");

            // Upload timer: Every 10 minutes
            uploadTimer = new Timer(UploadMetricsCallback, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10));
        }

        #region Performance Metrics

        /// <summary>
        /// Track app start time (call once app is fully initialized)
        /// </summary>
        public void TrackAppStart()
        {
            var startTimeMs = appStartStopwatch.ElapsedMilliseconds;
            TrackMetric(new TelemetryMetric
            {
                MetricType = "app_start_time_ms",
                Value = startTimeMs,
                Metadata = new { version = GetAppVersion() }
            });
        }

        /// <summary>
        /// Track hotkey response time (from press to recording start)
        /// </summary>
        public void TrackHotkeyResponseStart()
        {
            lastHotkeyPressTime = DateTime.UtcNow;
        }

        public void TrackHotkeyResponseEnd()
        {
            if (lastHotkeyPressTime.HasValue)
            {
                var responseTimeMs = (DateTime.UtcNow - lastHotkeyPressTime.Value).TotalMilliseconds;
                TrackMetric(new TelemetryMetric
                {
                    MetricType = "hotkey_response_time_ms",
                    Value = responseTimeMs,
                    Metadata = new { threshold_ms = 200 } // Target: <200ms
                });
                lastHotkeyPressTime = null;
            }
        }

        /// <summary>
        /// Track transcription duration (from recording start to text injection)
        /// </summary>
        public void TrackTranscriptionDuration(long durationMs, string modelUsed, int wordCount, bool success)
        {
            sessionTranscriptionCount++;
            if (success)
            {
                totalTranscriptionTimeMs += durationMs;
            }

            TrackMetric(new TelemetryMetric
            {
                MetricType = "transcription_duration_ms",
                Value = durationMs,
                Metadata = new
                {
                    modelUsed,
                    wordCount,
                    success,
                    avgDurationMs = success ? totalTranscriptionTimeMs / sessionTranscriptionCount : 0
                }
            });
        }

        /// <summary>
        /// Track current memory usage
        /// </summary>
        public void TrackMemoryUsage(long memoryBytes)
        {
            TrackMetric(new TelemetryMetric
            {
                MetricType = "memory_usage_mb",
                Value = memoryBytes / (1024.0 * 1024.0),
                Metadata = new
                {
                    sessionDurationMinutes = (DateTime.UtcNow - sessionStartTime).TotalMinutes,
                    transcriptionCount = sessionTranscriptionCount
                }
            });
        }

        #endregion

        #region Reliability Metrics

        /// <summary>
        /// Track error occurrence (crash or recoverable error)
        /// </summary>
        public void TrackError(string errorType, string? component = null, bool isCrash = false)
        {
            errorCountThisSession++;

            if (!errorCountsByType.ContainsKey(errorType))
                errorCountsByType[errorType] = 0;

            errorCountsByType[errorType]++;

            TrackMetric(new TelemetryMetric
            {
                MetricType = isCrash ? "crash" : "error",
                Value = 1,
                Metadata = new
                {
                    errorType,
                    component,
                    totalErrorsThisSession = errorCountThisSession,
                    errorTypeCount = errorCountsByType[errorType]
                }
            });
        }

        /// <summary>
        /// Track feature success/fail rate (e.g., recording, transcription, text injection)
        /// </summary>
        public void TrackFeatureAttempt(string featureName, bool success, string? failureReason = null)
        {
            TrackMetric(new TelemetryMetric
            {
                MetricType = "feature_attempt",
                Value = success ? 1 : 0,
                Metadata = new
                {
                    featureName,
                    success,
                    failureReason,
                    sessionTranscriptionCount
                }
            });
        }

        /// <summary>
        /// Track recovery attempt (e.g., restarting Whisper process, retrying transcription)
        /// </summary>
        public void TrackRecoveryAttempt(string recoveryType, bool success)
        {
            recoveryAttempts++;

            TrackMetric(new TelemetryMetric
            {
                MetricType = "recovery_attempt",
                Value = success ? 1 : 0,
                Metadata = new
                {
                    recoveryType,
                    success,
                    totalRecoveriesThisSession = recoveryAttempts
                }
            });
        }

        #endregion

        #region Usage Metrics

        /// <summary>
        /// Track daily active user (called once per app launch)
        /// </summary>
        public void TrackDailyActiveUser()
        {
            TrackMetric(new TelemetryMetric
            {
                MetricType = "daily_active_user",
                Value = 1,
                Metadata = new
                {
                    tier = GetCurrentTier(),
                    version = GetAppVersion(),
                    osVersion = GetOsVersion()
                }
            });
        }

        /// <summary>
        /// Track transcription completion (aggregated per session)
        /// </summary>
        public void TrackTranscriptionCount()
        {
            TrackMetric(new TelemetryMetric
            {
                MetricType = "transcriptions_per_session",
                Value = sessionTranscriptionCount,
                Metadata = new
                {
                    sessionDurationMinutes = (DateTime.UtcNow - sessionStartTime).TotalMinutes,
                    avgTranscriptionsPerHour = sessionTranscriptionCount / Math.Max(0.1, (DateTime.UtcNow - sessionStartTime).TotalHours)
                }
            });
        }

        /// <summary>
        /// Track feature usage (e.g., custom dictionary, voice shortcuts, history panel)
        /// </summary>
        public void TrackFeatureUsage(string featureName)
        {
            TrackMetric(new TelemetryMetric
            {
                MetricType = "feature_usage",
                Value = 1,
                Metadata = new { featureName }
            });
        }

        /// <summary>
        /// Track session length (called on app close)
        /// </summary>
        public void TrackSessionEnd()
        {
            var sessionDurationMinutes = (DateTime.UtcNow - sessionStartTime).TotalMinutes;

            TrackMetric(new TelemetryMetric
            {
                MetricType = "session_length_minutes",
                Value = sessionDurationMinutes,
                Metadata = new
                {
                    transcriptionCount = sessionTranscriptionCount,
                    errorCount = errorCountThisSession,
                    recoveryAttempts
                }
            });

            // Upload remaining metrics before shutdown
            UploadMetricsNow();
        }

        #endregion

        #region Core Tracking & Upload

        /// <summary>
        /// Track a telemetry metric (stores locally and queues for upload)
        /// </summary>
        private void TrackMetric(TelemetryMetric metric)
        {
            // Respect analytics opt-in (reuse existing setting)
            if (settings.EnableAnalytics != true)
                return;

            try
            {
                metric.Timestamp = DateTime.UtcNow;
                metric.AnonymousUserId = settings.AnonymousUserId ?? "unknown";

                lock (telemetryLock)
                {
                    // Write to local file immediately (append-only)
                    AppendMetricToLocalFile(metric);

                    // Queue for batch upload
                    metricQueue.Add(metric);
                }
            }
            catch (Exception ex)
            {
                // Fail silently - telemetry should never crash the app
                ErrorLogger.LogMessage($"Telemetry tracking failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Append metric to local JSON file (for offline storage and backup)
        /// </summary>
        private void AppendMetricToLocalFile(TelemetryMetric metric)
        {
            try
            {
                var json = JsonSerializer.Serialize(metric, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                File.AppendAllText(todayTelemetryFile, json + Environment.NewLine);
            }
            catch
            {
                // Silent fail - don't block app if file I/O fails
            }
        }

        /// <summary>
        /// Timer callback: Upload metrics every 10 minutes
        /// </summary>
        private void UploadMetricsCallback(object? state)
        {
            UploadMetricsNow();
        }

        /// <summary>
        /// Upload queued metrics to backend API (batch upload)
        /// </summary>
        private void UploadMetricsNow()
        {
            if (settings.EnableAnalytics != true)
                return;

            List<TelemetryMetric> metricsToUpload;
            lock (telemetryLock)
            {
                if (metricQueue.Count == 0)
                    return;

                metricsToUpload = new List<TelemetryMetric>(metricQueue);
                metricQueue.Clear();
            }

            // Upload in background (don't block)
            Task.Run(async () =>
            {
                try
                {
                    var json = JsonSerializer.Serialize(new
                    {
                        metrics = metricsToUpload
                    }, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    using var content = new StringContent(json, Encoding.UTF8, "application/json");
                    using var response = await ApiClient.Client.PostAsync("/api/metrics/upload", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        ErrorLogger.LogMessage($"Telemetry upload failed: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    // Fail silently - offline mode should work fine
                    ErrorLogger.LogMessage($"Telemetry upload exception: {ex.Message}");
                }
            });
        }

        #endregion

        #region Helper Methods

        private string GetCurrentTier()
        {
            var proModels = new[] { "ggml-base.bin", "ggml-medium.bin", "ggml-large-v3.bin" };
            var hasProModel = proModels.Any(m => m.Equals(settings.WhisperModel, StringComparison.OrdinalIgnoreCase));
            return hasProModel ? "PRO" : "FREE";
        }

        private string GetAppVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly()
                .GetName().Version?.ToString() ?? "unknown";
        }

        private string GetOsVersion()
        {
            return $"Windows {Environment.OSVersion.Version.Major}.{Environment.OSVersion.Version.Minor}";
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            // Stop upload timer
            uploadTimer?.Dispose();

            // Upload remaining metrics
            UploadMetricsNow();

            GC.SuppressFinalize(this);
        }

        #endregion
    }

    /// <summary>
    /// Telemetry metric structure
    /// </summary>
    public class TelemetryMetric
    {
        public DateTime Timestamp { get; set; }
        public string AnonymousUserId { get; set; } = string.Empty;
        public string MetricType { get; set; } = string.Empty;
        public double Value { get; set; }
        public object? Metadata { get; set; }
    }
}

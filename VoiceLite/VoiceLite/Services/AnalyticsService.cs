using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VoiceLite.Models;
using VoiceLite.Services.Auth;

namespace VoiceLite.Services
{
    /// <summary>
    /// Privacy-first anonymous analytics service.
    /// Tracks usage metrics with user consent. All data is anonymous and opt-in.
    /// </summary>
    public class AnalyticsService
    {
        private readonly Settings settings;
        private string? anonymousUserId;
        private DateTime lastTranscriptionLogTime = DateTime.MinValue;
        private int dailyTranscriptionCount = 0;

        public AnalyticsService(Settings settings)
        {
            this.settings = settings;
            InitializeAnonymousUserId();
        }

        /// <summary>
        /// Initialize or load the anonymous user ID (SHA256 hash)
        /// </summary>
        private void InitializeAnonymousUserId()
        {
            if (settings.AnonymousUserId != null)
            {
                anonymousUserId = settings.AnonymousUserId;
                return;
            }

            // Generate new anonymous ID from machine ID + timestamp
            var machineId = Environment.MachineName;
            var timestamp = DateTime.UtcNow.Ticks.ToString();
            var combined = $"{machineId}:{timestamp}";

            // SHA256 hash for anonymization (irreversible)
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
                anonymousUserId = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }

            // Save to settings
            settings.AnonymousUserId = anonymousUserId;
        }

        /// <summary>
        /// Track app launch event
        /// </summary>
        public async Task TrackAppLaunchAsync()
        {
            if (!IsAnalyticsEnabled())
                return;

            await TrackEventAsync(new AnalyticsEventPayload
            {
                AnonymousUserId = anonymousUserId!,
                EventType = "APP_LAUNCHED",
                Tier = GetCurrentTier(),
                AppVersion = GetAppVersion(),
                OsVersion = GetOsVersion()
            });
        }

        /// <summary>
        /// Track transcription completion (aggregated daily to reduce noise)
        /// </summary>
        public async Task TrackTranscriptionAsync(string modelUsed, int wordCount)
        {
            if (!IsAnalyticsEnabled())
                return;

            // Aggregate transcriptions daily
            var today = DateTime.UtcNow.Date;
            if (lastTranscriptionLogTime.Date != today)
            {
                dailyTranscriptionCount = 0;
                lastTranscriptionLogTime = today;
            }

            dailyTranscriptionCount++;

            // Send aggregated count once per day
            if (dailyTranscriptionCount == 1)
            {
                await TrackEventAsync(new AnalyticsEventPayload
                {
                    AnonymousUserId = anonymousUserId!,
                    EventType = "TRANSCRIPTION_COMPLETED",
                    Tier = GetCurrentTier(),
                    AppVersion = GetAppVersion(),
                    OsVersion = GetOsVersion(),
                    ModelUsed = modelUsed,
                    Metadata = new { wordCount }
                });
            }
        }

        /// <summary>
        /// Track model change
        /// </summary>
        public async Task TrackModelChangeAsync(string oldModel, string newModel)
        {
            if (!IsAnalyticsEnabled())
                return;

            await TrackEventAsync(new AnalyticsEventPayload
            {
                AnonymousUserId = anonymousUserId!,
                EventType = "MODEL_CHANGED",
                Tier = GetCurrentTier(),
                AppVersion = GetAppVersion(),
                OsVersion = GetOsVersion(),
                ModelUsed = newModel,
                Metadata = new { oldModel, newModel }
            });
        }

        /// <summary>
        /// Track settings change
        /// </summary>
        public async Task TrackSettingsChangeAsync(string settingName)
        {
            if (!IsAnalyticsEnabled())
                return;

            await TrackEventAsync(new AnalyticsEventPayload
            {
                AnonymousUserId = anonymousUserId!,
                EventType = "SETTINGS_CHANGED",
                Tier = GetCurrentTier(),
                AppVersion = GetAppVersion(),
                OsVersion = GetOsVersion(),
                Metadata = new { settingName }
            });
        }

        /// <summary>
        /// Track error occurrence (opt-in)
        /// </summary>
        public async Task TrackErrorAsync(string errorType, string? component = null)
        {
            if (!IsAnalyticsEnabled())
                return;

            await TrackEventAsync(new AnalyticsEventPayload
            {
                AnonymousUserId = anonymousUserId!,
                EventType = "ERROR_OCCURRED",
                Tier = GetCurrentTier(),
                AppVersion = GetAppVersion(),
                OsVersion = GetOsVersion(),
                Metadata = new { errorType, component }
            });
        }

        /// <summary>
        /// Track Pro upgrade
        /// </summary>
        public async Task TrackProUpgradeAsync()
        {
            if (!IsAnalyticsEnabled())
                return;

            await TrackEventAsync(new AnalyticsEventPayload
            {
                AnonymousUserId = anonymousUserId!,
                EventType = "PRO_UPGRADE",
                Tier = "PRO",
                AppVersion = GetAppVersion(),
                OsVersion = GetOsVersion(),
                Metadata = new { fromFreeTier = true }
            });
        }

        /// <summary>
        /// Send analytics event to backend
        /// </summary>
        private async Task TrackEventAsync(AnalyticsEventPayload payload)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await ApiClient.Client.PostAsync("/api/analytics/event", content);

                // Fail silently - analytics should never break the app
                if (!response.IsSuccessStatusCode)
                {
                    ErrorLogger.LogMessage($"Analytics event failed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                // Fail silently - offline mode should work fine
                ErrorLogger.LogMessage($"Analytics event exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if analytics is enabled
        /// </summary>
        private bool IsAnalyticsEnabled()
        {
            // null = not asked yet, false = opted out, true = opted in
            return settings.EnableAnalytics == true;
        }

        /// <summary>
        /// Get current tier (FREE or PRO)
        /// </summary>
        private string GetCurrentTier()
        {
            // Check if Pro model is unlocked (Swift, Elite, Ultra)
            // Users with Pro licenses can use models beyond the free tier (Lite/Pro)
            var proModels = new[] { "ggml-base.bin", "ggml-medium.bin", "ggml-large-v3.bin" };
            var hasProModel = proModels.Any(m => m.Equals(settings.WhisperModel, StringComparison.OrdinalIgnoreCase));

            return hasProModel ? "PRO" : "FREE";
        }

        /// <summary>
        /// Get app version
        /// </summary>
        private string GetAppVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly()
                .GetName().Version?.ToString() ?? "unknown";
        }

        /// <summary>
        /// Get OS version
        /// </summary>
        private string GetOsVersion()
        {
            return $"Windows {Environment.OSVersion.Version.Major}.{Environment.OSVersion.Version.Minor}";
        }
    }

    /// <summary>
    /// Analytics event payload structure
    /// </summary>
    internal class AnalyticsEventPayload
    {
        public string AnonymousUserId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string Tier { get; set; } = "FREE";
        public string? AppVersion { get; set; }
        public string? OsVersion { get; set; }
        public string? ModelUsed { get; set; }
        public object? Metadata { get; set; }
    }
}

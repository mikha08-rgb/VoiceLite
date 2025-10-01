using System;
using System.Text.Json.Serialization;

namespace VoiceLite.Models
{
    /// <summary>
    /// Ed25519-signed license payload matching backend structure.
    /// This is the JSON payload embedded in signed license strings.
    /// </summary>
    public sealed class LicensePayload
    {
        [JsonPropertyName("license_id")]
        public string LicenseId { get; set; } = string.Empty;

        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("product_id")]
        public string ProductId { get; set; } = string.Empty;

        [JsonPropertyName("plan")]
        public string Plan { get; set; } = string.Empty; // "pro" | "lifetime"

        [JsonPropertyName("device_fingerprint")]
        public string DeviceFingerprint { get; set; } = string.Empty;

        [JsonPropertyName("seat_limit")]
        public int SeatLimit { get; set; }

        [JsonPropertyName("issued_at")]
        public string IssuedAt { get; set; } = string.Empty; // ISO8601

        [JsonPropertyName("expires_at")]
        public string ExpiresAt { get; set; } = string.Empty; // ISO8601

        [JsonPropertyName("grace_days")]
        public int GraceDays { get; set; }

        [JsonPropertyName("key_version")]
        public int KeyVersion { get; set; }

        [JsonPropertyName("version")]
        public int Version { get; set; }

        /// <summary>
        /// Check if the license is currently expired (including grace period).
        /// </summary>
        public bool IsExpired()
        {
            if (!DateTime.TryParse(ExpiresAt, out var expiresAt))
            {
                return true; // Invalid date = treat as expired
            }

            var gracePeriod = TimeSpan.FromDays(GraceDays);
            var effectiveExpiry = expiresAt.Add(gracePeriod);

            return DateTime.UtcNow > effectiveExpiry;
        }

        /// <summary>
        /// Check if the license is within grace period (expired but still usable).
        /// </summary>
        public bool IsInGracePeriod()
        {
            if (!DateTime.TryParse(ExpiresAt, out var expiresAt))
            {
                return false;
            }

            var now = DateTime.UtcNow;
            var gracePeriod = TimeSpan.FromDays(GraceDays);
            var effectiveExpiry = expiresAt.Add(gracePeriod);

            return now > expiresAt && now <= effectiveExpiry;
        }
    }
}

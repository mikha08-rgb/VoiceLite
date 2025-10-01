using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VoiceLite.Models
{
    /// <summary>
    /// Certificate Revocation List payload matching backend structure.
    /// This is the JSON payload embedded in signed CRL strings.
    /// </summary>
    public sealed class CRLPayload
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("updated_at")]
        public string UpdatedAt { get; set; } = string.Empty; // ISO8601

        [JsonPropertyName("revoked_license_ids")]
        public List<string> RevokedLicenseIds { get; set; } = new();

        [JsonPropertyName("key_version")]
        public int KeyVersion { get; set; }
    }
}

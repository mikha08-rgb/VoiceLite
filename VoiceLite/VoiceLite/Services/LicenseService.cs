using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    /// <summary>
    /// Service for validating VoiceLite licenses with the web API.
    /// </summary>
    public class LicenseService
    {
        private static readonly HttpClient httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private const string API_BASE_URL = "https://voicelite.app";

        /// <summary>
        /// Validate a license key with the API.
        /// </summary>
        public static async Task<License> ValidateLicenseAsync(string licenseKey)
        {
            try
            {
                var requestBody = new
                {
                    licenseKey = licenseKey.Trim()
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{API_BASE_URL}/api/licenses/validate", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // License not found or error
                    return License.Free();
                }

                var licenseResponse = JsonSerializer.Deserialize<LicenseValidationResponse>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (licenseResponse == null || !licenseResponse.Valid)
                {
                    return License.Free();
                }

                return new License
                {
                    Valid = true,
                    Status = licenseResponse.Status ?? "ACTIVE",
                    Type = licenseResponse.Type,
                    Features = licenseResponse.Features ?? new List<string>(),
                    ExpiresAt = licenseResponse.ExpiresAt
                };
            }
            catch (Exception ex)
            {
                // Network error or API down - return cached license or free tier
                System.Diagnostics.Debug.WriteLine($"License validation failed: {ex.Message}");
                return License.Free();
            }
        }

        /// <summary>
        /// Check if cached license is still valid (validated within last 24 hours).
        /// </summary>
        public static bool IsCachedLicenseValid(DateTime? lastValidated)
        {
            if (!lastValidated.HasValue)
                return false;

            return (DateTime.UtcNow - lastValidated.Value).TotalHours < 24;
        }

        private class LicenseValidationResponse
        {
            public bool Valid { get; set; }
            public string? Status { get; set; }
            public string? Type { get; set; }
            public List<string>? Features { get; set; }
            public DateTime? ExpiresAt { get; set; }
            public string? Error { get; set; }
        }
    }
}

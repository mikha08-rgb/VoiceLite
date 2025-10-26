using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VoiceLite.Services
{
    public class LicenseService : IDisposable
    {
        // WEEK 1 FIX: Static HttpClient prevents socket exhaustion
        // Single instance shared across all LicenseService instances
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
            DefaultRequestHeaders =
            {
                { "User-Agent", "VoiceLite-Desktop/1.0" }
            }
        };

        private readonly string _apiBaseUrl;
        private bool _disposed = false;

        static LicenseService()
        {
            // Configure HttpClient once at startup
            _httpClient.BaseAddress = new Uri("https://voicelite.app/");
        }

        public LicenseService()
        {
            _apiBaseUrl = "https://voicelite.app";
        }

        /// <summary>
        /// Validates a license key with the VoiceLite API
        /// </summary>
        /// <param name="licenseKey">The license key to validate</param>
        /// <returns>True if the license is valid and active, false otherwise</returns>
        public async Task<LicenseValidationResult> ValidateLicenseAsync(string licenseKey)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(licenseKey))
                {
                    return new LicenseValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "License key cannot be empty"
                    };
                }

                var request = new
                {
                    licenseKey = licenseKey.Trim()
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/api/licenses/validate", content);

                if (!response.IsSuccessStatusCode)
                {
                    return new LicenseValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = $"Server error: {response.StatusCode}"
                    };
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ValidationResponse>(responseBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result == null)
                {
                    return new LicenseValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Invalid response from server"
                    };
                }

                return new LicenseValidationResult
                {
                    IsValid = result.Valid,
                    Tier = result.Tier ?? "free",
                    ErrorMessage = result.Valid ? null : "Invalid or expired license key"
                };
            }
            catch (HttpRequestException ex)
            {
                ErrorLogger.LogError("License validation HTTP error", ex);
                return new LicenseValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Connection error. Please check your internet connection."
                };
            }
            catch (TaskCanceledException)
            {
                return new LicenseValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Request timed out. Please try again."
                };
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("License validation failed", ex);
                return new LicenseValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Validation error: {ex.Message}"
                };
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // WEEK 1 FIX: Don't dispose static HttpClient
                // Static instance lives for application lifetime
                // This prevents socket exhaustion from creating/disposing multiple instances
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        // Response model from API
        private class ValidationResponse
        {
            public bool Valid { get; set; }
            public string? Tier { get; set; }
        }
    }

    public class LicenseValidationResult
    {
        public bool IsValid { get; set; }
        public string Tier { get; set; } = "free";
        public string? ErrorMessage { get; set; }
    }
}

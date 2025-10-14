using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VoiceLite.Services
{
    /// <summary>
    /// Validates VoiceLite Pro license keys via API call to voicelite.app
    /// </summary>
    public class LicenseValidator
    {
        private const string API_BASE_URL = "https://voicelite.app";
        private const string VALIDATE_ENDPOINT = "/api/licenses/validate";

        // ARCH-002 FIX: Instance HttpClient for testability
        private readonly HttpClient _httpClient;

        // Singleton instance for backward compatibility with static callers
        private static readonly Lazy<LicenseValidator> _instance = new Lazy<LicenseValidator>(() =>
            new LicenseValidator(CreateDefaultHttpClient()));

        public class ValidationResponse
        {
            public bool valid { get; set; }
            public string? status { get; set; }
            public string? type { get; set; }
            public string? expiresAt { get; set; }
            public string? email { get; set; }
            public string? error { get; set; }
        }

        /// <summary>
        /// Constructor for dependency injection (testable)
        /// </summary>
        public LicenseValidator(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
        /// Creates default HttpClient with 10-second timeout
        /// </summary>
        private static HttpClient CreateDefaultHttpClient()
        {
            return new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        /// <summary>
        /// Validates a license key with the server (instance method)
        /// </summary>
        /// <param name="licenseKey">License key in format VL-XXXXXX-XXXXXX-XXXXXX</param>
        /// <returns>Validation response with status and details</returns>
        public async Task<ValidationResponse> ValidateAsync(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                return new ValidationResponse
                {
                    valid = false,
                    error = "License key is empty"
                };
            }

            try
            {
                var requestBody = new
                {
                    licenseKey = licenseKey.Trim()
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{API_BASE_URL}{VALIDATE_ENDPOINT}", content);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new ValidationResponse
                    {
                        valid = false,
                        error = "License key not found"
                    };
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var validationResponse = JsonSerializer.Deserialize<ValidationResponse>(responseBody);

                return validationResponse ?? new ValidationResponse
                {
                    valid = false,
                    error = "Invalid response from server"
                };
            }
            catch (TaskCanceledException)
            {
                return new ValidationResponse
                {
                    valid = false,
                    error = "Request timed out - check your internet connection"
                };
            }
            catch (HttpRequestException ex)
            {
                return new ValidationResponse
                {
                    valid = false,
                    error = $"Network error: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new ValidationResponse
                {
                    valid = false,
                    error = $"Validation failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Static method for backward compatibility (uses singleton instance)
        /// </summary>
        public static Task<ValidationResponse> ValidateAsync_Static(string licenseKey)
        {
            return _instance.Value.ValidateAsync(licenseKey);
        }

        /// <summary>
        /// Checks if a license key matches the expected format (basic client-side validation)
        /// </summary>
        public static bool IsValidFormat(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
                return false;

            // Expected format: VL-XXXXXX-XXXXXX-XXXXXX (6 chars per segment)
            var parts = licenseKey.Trim().Split('-');
            return parts.Length == 4 &&
                   parts[0] == "VL" &&
                   parts[1].Length == 6 &&
                   parts[2].Length == 6 &&
                   parts[3].Length == 6;
        }
    }
}

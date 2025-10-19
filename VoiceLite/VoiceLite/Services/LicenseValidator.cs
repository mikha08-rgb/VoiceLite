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
    // AUDIT FIX (RESOURCE-CRIT-2): Implement IDisposable for HttpClient cleanup
    public class LicenseValidator : IDisposable
    {
        private const string API_BASE_URL = "https://voicelite.app";
        private const string VALIDATE_ENDPOINT = "/api/licenses/validate";

        // ARCH-002 FIX: Instance HttpClient for testability
        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;
        private bool _disposed = false;

        // Static shared HttpClient (Microsoft best practice for singleton pattern)
        // This prevents socket exhaustion and is properly managed by the runtime
        private static readonly HttpClient _sharedHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        // Singleton instance for backward compatibility with static callers
        private static readonly Lazy<LicenseValidator> _instance = new Lazy<LicenseValidator>(() =>
            new LicenseValidator());

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
        /// Constructor for dependency injection (caller owns HttpClient)
        /// </summary>
        public LicenseValidator(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _ownsHttpClient = false;  // Caller will dispose
        }

        /// <summary>
        /// Private constructor for singleton (uses shared HttpClient)
        /// </summary>
        private LicenseValidator()
        {
            _httpClient = _sharedHttpClient;
            _ownsHttpClient = false;  // Shared instance, don't dispose
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

                // AUDIT FIX: Check for null response content before reading
                if (response.Content == null)
                {
                    return new ValidationResponse
                    {
                        valid = false,
                        error = "Empty response from server"
                    };
                }

                var responseBody = await response.Content.ReadAsStringAsync();

                // AUDIT FIX: Validate response body before deserialization
                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    return new ValidationResponse
                    {
                        valid = false,
                        error = "Invalid response from server"
                    };
                }

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

            // SECURITY FIX: Check length before accessing array elements
            if (parts.Length != 4)
                return false;

            return parts[0] == "VL" &&
                   parts[1].Length == 6 &&
                   parts[2].Length == 6 &&
                   parts[3].Length == 6;
        }

        /// <summary>
        /// AUDIT FIX (RESOURCE-CRIT-2): Dispose of HttpClient if owned by this instance
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            if (_ownsHttpClient)
            {
                try { _httpClient?.Dispose(); } catch { }
            }

            _disposed = true;
        }
    }
}

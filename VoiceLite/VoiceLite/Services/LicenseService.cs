using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VoiceLite.Core.Interfaces.Features;
using VoiceLite.Infrastructure.Resilience;

namespace VoiceLite.Services
{
    public class LicenseService : ILicenseService, IDisposable
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
        private bool _isLicenseValid = false;
        private string? _storedLicenseKey = null;
        private string? _licenseEmail = null;
        private int _activationCount = 0;
        private int _maxActivations = 3;

        // PHASE 3 - DAY 1: Cached validation result (lifetime - no expiry)
        // Licenses are $20 one-time payment with lifetime validity
        // Once validated successfully, cache forever for offline use
        private LicenseValidationResult? _cachedValidationResult = null;

        public event EventHandler<bool>? LicenseStatusChanged;

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
        /// PHASE 3 - DAY 1: Now with automatic retry and lifetime caching
        ///
        /// Caching Strategy:
        /// - Lifetime licenses cached forever after successful validation
        /// - Only re-validates if user changes license key
        /// - Retry logic (3 attempts) ensures resilient first-time activation
        /// </summary>
        /// <param name="licenseKey">The license key to validate</param>
        /// <returns>Validation result</returns>
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

                // Check if we have a cached result for this key (cache forever - lifetime licenses)
                if (_cachedValidationResult != null && _storedLicenseKey == licenseKey.Trim())
                {
                    ErrorLogger.LogMessage("Using cached license validation result (lifetime cache)");
                    return _cachedValidationResult;
                }

                var request = new
                {
                    licenseKey = licenseKey.Trim()
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // PHASE 3 - DAY 1: Wrap API call with Polly retry policy
                // This will automatically retry up to 3 times on transient failures (5xx errors, network issues)
                HttpResponseMessage response;
                try
                {
                    response = await RetryPolicies.HttpRetryPolicy.ExecuteAsync(async () =>
                        await _httpClient.PostAsync($"{_apiBaseUrl}/api/licenses/validate", content));
                }
                catch (Exception retryEx)
                {
                    // All retries exhausted - check if we have a cached result to fall back to
                    // (This handles case where user tries to re-validate an already-cached license while offline)
                    if (_cachedValidationResult != null && _storedLicenseKey == licenseKey.Trim())
                    {
                        ErrorLogger.LogWarning(
                            $"License API unreachable after 3 retries. Using cached result (lifetime license). " +
                            $"Exception: {retryEx.Message}"
                        );
                        return _cachedValidationResult;
                    }

                    // No cache available - return error (first-time activation requires internet)
                    throw;
                }

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

                var validationResult = new LicenseValidationResult
                {
                    IsValid = result.Valid,
                    Tier = result.Tier ?? "free",
                    ErrorMessage = result.Valid ? null : "Invalid or expired license key"
                };

                // PHASE 3 - DAY 1: Cache successful validation result (forever - lifetime licenses)
                if (result.Valid)
                {
                    _cachedValidationResult = validationResult;
                    ErrorLogger.LogMessage($"License validation succeeded - cached permanently (lifetime license)");
                }

                return validationResult;
            }
            catch (HttpRequestException ex)
            {
                ErrorLogger.LogError("License validation HTTP error", ex);

                // Try to use cached result even on network failure
                if (_cachedValidationResult != null && _storedLicenseKey == licenseKey.Trim())
                {
                    ErrorLogger.LogWarning("Network error - using cached license validation result");
                    return _cachedValidationResult;
                }

                return new LicenseValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Connection error. Please check your internet connection."
                };
            }
            catch (TaskCanceledException)
            {
                // Try to use cached result on timeout
                if (_cachedValidationResult != null && _storedLicenseKey == licenseKey.Trim())
                {
                    ErrorLogger.LogWarning("Request timeout - using cached license validation result");
                    return _cachedValidationResult;
                }

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

        #region ILicenseService Implementation

        public bool IsLicenseValid => _isLicenseValid;


        public string GetStoredLicenseKey()
        {
            return _storedLicenseKey ?? string.Empty;
        }

        public void SaveLicenseKey(string licenseKey)
        {
            _storedLicenseKey = licenseKey;
            // TODO: Save to secure storage
        }

        public void RemoveLicenseKey()
        {
            _storedLicenseKey = null;
            _isLicenseValid = false;
            _licenseEmail = null;
            _activationCount = 0;
            LicenseStatusChanged?.Invoke(this, false);
        }

        public string GetLicenseEmail()
        {
            return _licenseEmail ?? string.Empty;
        }

        public int GetActivationCount()
        {
            return _activationCount;
        }

        public int GetMaxActivations()
        {
            return _maxActivations;
        }

        #endregion

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

}

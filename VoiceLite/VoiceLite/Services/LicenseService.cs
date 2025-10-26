using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VoiceLite.Core.Interfaces.Features;

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

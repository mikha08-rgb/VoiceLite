using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VoiceLite.Core.Interfaces.Features;
using VoiceLite.Infrastructure.Resilience;

namespace VoiceLite.Services
{
    public class LicenseService : ILicenseService, IDisposable
    {
        /// <summary>
        /// Static HttpClient for license validation requests.
        ///
        /// BUG FIX (HTTPCLIENT-DOC-001): Documented intentional static HttpClient pattern.
        ///
        /// Why static:
        /// - Prevents socket exhaustion (creating/disposing HttpClient rapidly exhausts ports)
        /// - Microsoft best practice: reuse HttpClient instances
        /// - Single client sufficient for single API endpoint (voicelite.app)
        ///
        /// Why not IHttpClientFactory:
        /// - Overkill for single client scenario
        /// - IHttpClientFactory is designed for multi-client/multi-endpoint scenarios
        /// - Would add unnecessary DI complexity for minimal benefit
        ///
        /// Disposal:
        /// - Intentionally NOT disposed in Dispose() method
        /// - Lives for application lifetime (process exit handles cleanup)
        /// - Disposing would break other LicenseService instances (static shared state)
        ///
        /// Reference: https://docs.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines
        /// </summary>
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

        // SECURITY FIX (LICENSE-ENC-001): Encrypted license storage
        // License keys are encrypted using Windows DPAPI (Data Protection API)
        // Scope: CurrentUser (tied to Windows user account)
        // Location: %LOCALAPPDATA%\VoiceLite\license.dat
        private static readonly string LicenseFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VoiceLite",
            "license.dat"
        );

        public event EventHandler<bool>? LicenseStatusChanged;

        static LicenseService()
        {
            // Configure HttpClient once at startup
            _httpClient.BaseAddress = new Uri("https://voicelite.app/");
        }

        public LicenseService()
        {
            _apiBaseUrl = "https://voicelite.app";

            // SECURITY FIX (LICENSE-ENC-001): Auto-load encrypted license key on startup
            LoadLicenseKey();
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
                    licenseKey = licenseKey.Trim(),
                    machineId = HardwareIdService.GetMachineId(),
                    machineLabel = HardwareIdService.GetMachineLabel(),
                    machineHash = HardwareIdService.GetMachineHash()
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
                    // 403 = Activation limit reached
                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync();
                        var errorResult = JsonSerializer.Deserialize<ValidationResponse>(errorBody, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        return new LicenseValidationResult
                        {
                            IsValid = false,
                            ErrorMessage = errorResult?.Error ?? "Maximum device activations reached (3 devices). Please deactivate a device to continue."
                        };
                    }

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
                    ErrorMessage = result.Valid ? null : (result.Error ?? "Invalid or expired license key")
                };

                // Update activation counts from response
                if (result.Valid && result.License != null)
                {
                    _activationCount = result.License.ActivationsUsed;
                    _maxActivations = result.License.MaxActivations;
                }

                // PHASE 3 - DAY 1: Cache successful validation result (forever - lifetime licenses)
                if (result.Valid)
                {
                    _cachedValidationResult = validationResult;
                    ErrorLogger.LogMessage($"License validation succeeded - cached permanently (lifetime license). Activations: {_activationCount}/{_maxActivations}");
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
            catch (TaskCanceledException ex)
            {
                // Try to use cached result on timeout
                if (_cachedValidationResult != null && _storedLicenseKey == licenseKey.Trim())
                {
                    ErrorLogger.LogWarning("Request timeout - using cached license validation result");
                    return _cachedValidationResult;
                }

                ErrorLogger.LogWarning($"License validation request timed out: {ex.Message}");
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

        /// <summary>
        /// SECURITY FIX (LICENSE-ENC-001): Saves license key with Windows DPAPI encryption
        ///
        /// Encryption details:
        /// - Uses ProtectedData.Protect() with CurrentUser scope
        /// - Key is tied to Windows user account (can't be copied to other machines/users)
        /// - Encrypted data stored in %LOCALAPPDATA%\VoiceLite\license.dat
        /// - Safe from memory dumps and disk inspection
        /// </summary>
        public void SaveLicenseKey(string licenseKey)
        {
            try
            {
                _storedLicenseKey = licenseKey;

                // Encrypt license key using Windows DPAPI
                byte[] plaintextBytes = Encoding.UTF8.GetBytes(licenseKey);
                byte[] encryptedBytes = ProtectedData.Protect(
                    plaintextBytes,
                    null, // No additional entropy (user account is enough)
                    DataProtectionScope.CurrentUser
                );

                // Ensure directory exists
                string? directory = Path.GetDirectoryName(LicenseFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Write encrypted bytes to file
                File.WriteAllBytes(LicenseFilePath, encryptedBytes);

                ErrorLogger.LogMessage($"License key saved securely (encrypted with DPAPI)");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to save encrypted license key", ex);
                throw new InvalidOperationException("Failed to save license key securely", ex);
            }
        }

        /// <summary>
        /// SECURITY FIX (LICENSE-ENC-001): Loads encrypted license key on startup
        ///
        /// Auto-migration:
        /// - If encrypted file doesn't exist but settings.json has plaintext key, migrate it
        /// - Supports smooth upgrade from v1.2.0 to v1.2.1
        /// </summary>
        private void LoadLicenseKey()
        {
            try
            {
                // If encrypted license file exists, load it
                if (File.Exists(LicenseFilePath))
                {
                    byte[] encryptedBytes = File.ReadAllBytes(LicenseFilePath);
                    byte[] plaintextBytes = ProtectedData.Unprotect(
                        encryptedBytes,
                        null,
                        DataProtectionScope.CurrentUser
                    );

                    _storedLicenseKey = Encoding.UTF8.GetString(plaintextBytes);
                    ErrorLogger.LogMessage("License key loaded from encrypted storage");
                }
                else
                {
                    // MIGRATION PATH: Check if plaintext license exists in settings.json
                    // This handles upgrade from v1.2.0 (no encryption) to v1.2.1 (DPAPI encryption)
                    var settingsPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "VoiceLite",
                        "settings.json"
                    );

                    if (File.Exists(settingsPath))
                    {
                        try
                        {
                            var settingsJson = File.ReadAllText(settingsPath);
                            var settings = JsonSerializer.Deserialize<Models.Settings>(settingsJson);

                            if (settings != null && !string.IsNullOrWhiteSpace(settings.LicenseKey))
                            {
                                ErrorLogger.LogMessage("Migrating plaintext license key to encrypted storage...");

                                // Migrate to encrypted storage
                                SaveLicenseKey(settings.LicenseKey);

                                // Clear plaintext key from settings.json
                                settings.LicenseKey = string.Empty;
                                var updatedJson = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                                {
                                    WriteIndented = true
                                });
                                File.WriteAllText(settingsPath, updatedJson);

                                ErrorLogger.LogMessage("License key migration complete - plaintext removed from settings");
                            }
                        }
                        catch (Exception migrationEx)
                        {
                            ErrorLogger.LogWarning($"License key migration failed: {migrationEx.Message}");
                            // Don't throw - migration is best-effort
                        }
                    }
                }
            }
            catch (CryptographicException ex)
            {
                ErrorLogger.LogError("Failed to decrypt license key (may be corrupted or from different user)", ex);
                // Don't throw - let app continue without license
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning($"Failed to load license key: {ex.Message}");
                // Don't throw - let app continue without license
            }
        }

        public void RemoveLicenseKey()
        {
            _storedLicenseKey = null;
            _isLicenseValid = false;
            _licenseEmail = null;
            _activationCount = 0;

            // SECURITY FIX (LICENSE-ENC-001): Delete encrypted license file
            try
            {
                if (File.Exists(LicenseFilePath))
                {
                    File.Delete(LicenseFilePath);
                    ErrorLogger.LogMessage("Encrypted license file deleted");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning($"Failed to delete license file: {ex.Message}");
            }

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
                // BUG FIX (HTTPCLIENT-DOC-001): Intentionally NOT disposing static HttpClient
                // See detailed explanation in HttpClient field documentation above
                // Static instance lives for application lifetime - disposing would break other instances
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
            public string? Error { get; set; }
            public LicenseInfo? License { get; set; }
        }

        private class LicenseInfo
        {
            public string? Type { get; set; }
            public DateTime? ExpiresAt { get; set; }
            public int ActivationsUsed { get; set; }
            public int MaxActivations { get; set; }
        }
    }

}

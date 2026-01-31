using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
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
            Timeout = TimeSpan.FromSeconds(30), // Increased for Vercel cold start + DB query
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

        // HIGH-9 FIX: Changed from lifetime cache to 30-day cache
        // MED-5 FIX: Reduced from 30 days to 14 days for faster revocation detection
        // Revoked/expired licenses should stop working after cache expires
        // 14 days provides good offline UX while allowing faster revocation enforcement
        private LicenseValidationResult? _cachedValidationResult = null;
        private DateTime? _cacheTimestamp = null;
        private static readonly TimeSpan CacheExpiration = TimeSpan.FromDays(14);

        // THREAD-SAFETY FIX: Lock for cache access to prevent race conditions
        // Multiple concurrent ValidateLicenseAsync calls could corrupt cache without synchronization
        private readonly object _cacheLock = new object();

        private LicenseValidationResult? TryGetCachedResult(string licenseKey)
        {
            lock (_cacheLock)
            {
                // HIGH-9 FIX: Check cache expiration (14 days)
                if (_cachedValidationResult == null || _storedLicenseKey != licenseKey.Trim())
                {
                    return null;
                }

                // Check if cache has expired
                if (_cacheTimestamp.HasValue && DateTime.UtcNow - _cacheTimestamp.Value > CacheExpiration)
                {
                    ErrorLogger.LogMessage($"License cache expired (age: {DateTime.UtcNow - _cacheTimestamp.Value}), will re-validate");
                    _cachedValidationResult = null;
                    _cacheTimestamp = null;
                    return null;
                }

                return _cachedValidationResult;
            }
        }

        // SECURITY FIX (LICENSE-ENC-001): Encrypted license storage
        // License keys are encrypted using Windows DPAPI (Data Protection API)
        // Scope: CurrentUser (tied to Windows user account)
        // Location: %LOCALAPPDATA%\VoiceLite\license.dat

        // HIGH-5 FIX: Add entropy for stronger DPAPI encryption
        private static readonly byte[] DpapiEntropy = Encoding.UTF8.GetBytes("VoiceLite-License-v1");

        private static readonly string LicenseFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VoiceLite",
            "license.dat"
        );

        public event EventHandler<bool>? LicenseStatusChanged;

        /// <summary>
        /// HIGH-3 FIX: Static method to verify a license key matches DPAPI-encrypted storage.
        /// Used for tamper detection when settings.json's LicenseKey might have been manually edited.
        /// Returns true if the key matches the encrypted storage, or false if no match/no storage.
        ///
        /// CRITICAL-3 FIX: Now also verifies email when available. Storage format is "key|email".
        /// Attacker cannot steal key alone and bypass detection - email must also match.
        /// </summary>
        public static bool VerifyLicenseKeyMatchesStorage(string licenseKey, string? email = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(licenseKey))
                    return false;

                if (!File.Exists(LicenseFilePath))
                    return false;

                byte[] encryptedBytes = File.ReadAllBytes(LicenseFilePath);
                byte[] plaintextBytes;

                // Try new entropy format first
                try
                {
                    plaintextBytes = ProtectedData.Unprotect(
                        encryptedBytes,
                        DpapiEntropy,
                        DataProtectionScope.CurrentUser
                    );
                }
                catch (CryptographicException)
                {
                    // Try old format (no entropy)
                    plaintextBytes = ProtectedData.Unprotect(
                        encryptedBytes,
                        null,
                        DataProtectionScope.CurrentUser
                    );
                }

                var storedData = Encoding.UTF8.GetString(plaintextBytes);

                // CRITICAL-3 FIX: Parse key|email format (backward compatible with key-only format)
                string storedKey;
                string? storedEmail = null;
                var parts = storedData.Split('|');
                if (parts.Length >= 2)
                {
                    storedKey = parts[0];
                    storedEmail = parts[1];
                }
                else
                {
                    storedKey = storedData;
                }

                // Verify key matches
                if (!string.Equals(storedKey.Trim(), licenseKey.Trim(), StringComparison.OrdinalIgnoreCase))
                    return false;

                // CRITICAL-3 FIX: If email provided and stored, verify it matches
                // This prevents key theft without also knowing the email
                if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(storedEmail))
                {
                    if (!string.Equals(storedEmail.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        ErrorLogger.LogWarning("CRITICAL-3 FIX: License key matches but email mismatch detected - possible tampering");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning($"CRITICAL-3 FIX: Failed to verify license key against DPAPI storage: {ex.Message}");
                return false;
            }
        }

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

                // THREAD-SAFETY FIX: Check cache under lock to prevent race conditions
                var cached = TryGetCachedResult(licenseKey);
                if (cached != null)
                {
                    // MINOR-10 FIX: Updated comment - cache is 14-day expiration, not lifetime
                ErrorLogger.LogMessage("Using cached license validation result (14-day cache)");
                    return cached;
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
                    var cachedRetry = TryGetCachedResult(licenseKey);
                    if (cachedRetry != null)
                    {
                        ErrorLogger.LogWarning(
                            $"License API unreachable after 3 retries. Using cached result (lifetime license). " +
                            $"Exception: {retryEx.Message}"
                        );
                        return cachedRetry;
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

                    // 429 = Rate limited - parse actual error message from response
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync();
                        var errorResult = JsonSerializer.Deserialize<ValidationResponse>(errorBody, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        return new LicenseValidationResult
                        {
                            IsValid = false,
                            ErrorMessage = errorResult?.Error ?? "Too many validation attempts. Please wait a few minutes and try again."
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

                // HIGH-9 FIX: Cache successful validation result with 14-day expiration
                // THREAD-SAFETY FIX: Write cache and ALL state fields under lock
                // MED-2 FIX: Extended lock coverage to include _activationCount, _maxActivations, _licenseEmail
                // Previously these were updated outside the lock, causing race conditions
                if (result.Valid)
                {
                    lock (_cacheLock)
                    {
                        // Update activation counts and email from response (now inside lock)
                        // HIGH-1 FIX: Use default values when JSON doesn't provide them
                        // HIGH-7 FIX: Extract email from response
                        if (result.License != null)
                        {
                            _activationCount = result.License.ActivationsUsed; // 0 is acceptable default
                            _maxActivations = result.License.MaxActivations > 0
                                ? result.License.MaxActivations
                                : 3; // Default to 3 activations if not provided
                            _licenseEmail = result.License.Email; // HIGH-7 FIX: Store license email
                        }

                        _cachedValidationResult = validationResult;
                        _cacheTimestamp = DateTime.UtcNow; // HIGH-9 FIX: Track cache timestamp for expiration
                        // CRITICAL-2 FIX: Set _isLicenseValid to true on successful validation
                        // Previously this was never set, causing IsLicenseValid to always return false
                        _isLicenseValid = true;
                        _storedLicenseKey = licenseKey.Trim();
                    }

                    // CRITICAL-2 FIX: Raise LicenseStatusChanged event on successful validation
                    LicenseStatusChanged?.Invoke(this, true);

                    ErrorLogger.LogMessage($"License validation succeeded - cached for 14 days. Activations: {_activationCount}/{_maxActivations}");
                }

                return validationResult;
            }
            catch (HttpRequestException ex)
            {
                ErrorLogger.LogError("License validation HTTP error", ex);

                // Try to use cached result even on network failure
                var cachedHttp = TryGetCachedResult(licenseKey);
                if (cachedHttp != null)
                {
                    ErrorLogger.LogWarning("Network error - using cached license validation result");
                    return cachedHttp;
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
                var cachedTimeout = TryGetCachedResult(licenseKey);
                if (cachedTimeout != null)
                {
                    ErrorLogger.LogWarning("Request timeout - using cached license validation result");
                    return cachedTimeout;
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

        // THREAD-SAFETY FIX (P1): Synchronize property read to prevent torn reads
        // UI thread may otherwise see inconsistent values during concurrent ValidateLicenseAsync
        public bool IsLicenseValid
        {
            get { lock (_cacheLock) { return _isLicenseValid; } }
        }


        // THREAD-SAFETY FIX (P1): Synchronize property read to prevent torn reads
        // Consistent with other getters (GetLicenseEmail, GetActivationCount, etc.)
        public string GetStoredLicenseKey()
        {
            lock (_cacheLock) { return _storedLicenseKey ?? string.Empty; }
        }

        /// <summary>
        /// SECURITY FIX (LICENSE-ENC-001): Saves license key with Windows DPAPI encryption
        ///
        /// Encryption details:
        /// - Uses ProtectedData.Protect() with CurrentUser scope
        /// - Key is tied to Windows user account (can't be copied to other machines/users)
        /// - Encrypted data stored in %LOCALAPPDATA%\VoiceLite\license.dat
        /// - Safe from memory dumps and disk inspection
        ///
        /// CRITICAL-3 FIX: Now stores email alongside key in format "key|email"
        /// This enables email verification in tamper detection.
        /// </summary>
        public void SaveLicenseKey(string licenseKey)
        {
            // THREAD-SAFETY FIX (P1): Read _licenseEmail under lock to prevent race with ValidateLicenseAsync
            string? currentEmail;
            lock (_cacheLock) { currentEmail = _licenseEmail; }
            SaveLicenseKey(licenseKey, currentEmail);
        }

        /// <summary>
        /// CRITICAL-3 FIX: Overload that saves license key with email for tamper detection.
        /// Format: "key|email" - email is optional for backward compatibility.
        /// THREAD-SAFETY FIX (P1): Field updates synchronized to prevent race with ValidateLicenseAsync.
        /// </summary>
        public void SaveLicenseKey(string licenseKey, string? email)
        {
            try
            {
                // THREAD-SAFETY FIX (P1): Synchronize field updates to prevent race with
                // ValidateLicenseAsync, RemoveLicenseKey, and GetStoredLicenseKey
                lock (_cacheLock)
                {
                    _storedLicenseKey = licenseKey;
                    if (!string.IsNullOrEmpty(email))
                    {
                        _licenseEmail = email;
                    }
                }

                // File I/O OUTSIDE lock (non-blocking, as done in RemoveLicenseKey)
                // CRITICAL-3 FIX: Store key|email for tamper detection
                string dataToStore = string.IsNullOrEmpty(email)
                    ? licenseKey
                    : $"{licenseKey}|{email}";

                // HIGH-5 FIX: Encrypt license key using Windows DPAPI with entropy
                byte[] plaintextBytes = Encoding.UTF8.GetBytes(dataToStore);
                byte[] encryptedBytes = ProtectedData.Protect(
                    plaintextBytes,
                    DpapiEntropy,  // Additional entropy for stronger encryption
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

                ErrorLogger.LogMessage($"License key saved securely (encrypted with DPAPI, email={!string.IsNullOrEmpty(email)})");
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
        ///
        /// CRITICAL-3 FIX: Now parses key|email format for tamper detection.
        /// </summary>
        private void LoadLicenseKey()
        {
            try
            {
                // If encrypted license file exists, load it
                if (File.Exists(LicenseFilePath))
                {
                    byte[] encryptedBytes = File.ReadAllBytes(LicenseFilePath);
                    byte[] plaintextBytes;

                    // HIGH-5 FIX: Try new entropy first, fall back to old (null) for migration
                    try
                    {
                        plaintextBytes = ProtectedData.Unprotect(
                            encryptedBytes,
                            DpapiEntropy,
                            DataProtectionScope.CurrentUser
                        );
                    }
                    catch (CryptographicException)
                    {
                        // Old license file without entropy - migrate it
                        ErrorLogger.LogMessage("Migrating license from old encryption format...");
                        plaintextBytes = ProtectedData.Unprotect(
                            encryptedBytes,
                            null,  // Old format had no entropy
                            DataProtectionScope.CurrentUser
                        );
                        // Re-save with new entropy
                        _storedLicenseKey = Encoding.UTF8.GetString(plaintextBytes);
                        SaveLicenseKey(_storedLicenseKey);
                        ErrorLogger.LogMessage("License migrated to new encryption format");
                        return;
                    }

                    // CRITICAL-3 FIX: Parse key|email format (backward compatible with key-only format)
                    var storedData = Encoding.UTF8.GetString(plaintextBytes);
                    var parts = storedData.Split('|');
                    if (parts.Length >= 2)
                    {
                        _storedLicenseKey = parts[0];
                        _licenseEmail = parts[1];
                        ErrorLogger.LogMessage("License key and email loaded from encrypted storage");
                    }
                    else
                    {
                        _storedLicenseKey = storedData;
                        ErrorLogger.LogMessage("License key loaded from encrypted storage (no email)");
                    }
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
            // THREAD-SAFETY FIX (P1): Wrap field updates in lock to prevent race with ValidateLicenseAsync
            // Race scenario: ValidateLicenseAsync succeeds, RemoveLicenseKey clears fields, ValidateLicenseAsync
            // then sets _isLicenseValid = true inside lock - resulting in corrupted state
            lock (_cacheLock)
            {
                _storedLicenseKey = null;
                _isLicenseValid = false;
                _licenseEmail = null;
                _activationCount = 0;
                _cachedValidationResult = null;
                _cacheTimestamp = null;
            }

            // File I/O OUTSIDE lock (non-blocking, prevents holding lock during disk access)
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

        // THREAD-SAFETY FIX (P1): Synchronize property reads to prevent torn reads
        // UI thread may otherwise see inconsistent values during concurrent ValidateLicenseAsync
        public string GetLicenseEmail()
        {
            lock (_cacheLock) { return _licenseEmail ?? string.Empty; }
        }

        public int GetActivationCount()
        {
            lock (_cacheLock) { return _activationCount; }
        }

        public int GetMaxActivations()
        {
            lock (_cacheLock) { return _maxActivations; }
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
            public string? Email { get; set; }  // HIGH-7 FIX: Added Email field
            public DateTime? ExpiresAt { get; set; }
            public int ActivationsUsed { get; set; }
            public int MaxActivations { get; set; }
        }
    }

}

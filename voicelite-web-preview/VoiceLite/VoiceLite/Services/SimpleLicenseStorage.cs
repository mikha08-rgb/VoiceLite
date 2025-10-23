using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VoiceLite.Services
{
    /// <summary>
    /// Simple local license storage for one-time purchase model.
    /// Stores validated license locally after first online validation.
    /// Uses Windows DPAPI encryption to protect license data from tampering.
    /// No network calls needed after initial activation.
    /// </summary>
    public static class SimpleLicenseStorage
    {
#if DEBUG
        // TEST MODE: Enable mock license validation for unit tests
        // This flag is ONLY available in DEBUG builds and has ZERO impact on RELEASE builds
        internal static bool _testMode = false;
        internal static bool _mockHasValidLicense = false;
        internal static StoredLicense? _mockLicense = null;
#endif

        // BUG FIX: File lock for thread-safe operations
        private static readonly object _fileLock = new object();

        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VoiceLite"
        );

        private static readonly string LicensePath = Path.Combine(AppDataPath, "license.dat");

        public class StoredLicense
        {
            public string LicenseKey { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public DateTime ValidatedAt { get; set; }
            public string Type { get; set; } = "LIFETIME";
        }

        /// <summary>
        /// Decrypt data using Windows DPAPI (Data Protection API)
        /// Falls back to plaintext if decryption fails (backward compatibility)
        /// </summary>
        private static string DecryptData(byte[] encryptedData)
        {
            try
            {
                // Try to decrypt using DPAPI
                var decryptedBytes = ProtectedData.Unprotect(
                    encryptedData,
                    null, // No additional entropy
                    DataProtectionScope.CurrentUser // User-specific encryption
                );
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch
            {
                // Decryption failed - assume plaintext (backward compatibility)
                // This handles migration from old unencrypted licenses
                return Encoding.UTF8.GetString(encryptedData);
            }
        }

        /// <summary>
        /// Encrypt data using Windows DPAPI (Data Protection API)
        /// </summary>
        private static byte[] EncryptData(string plaintext)
        {
            try
            {
                var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
                return ProtectedData.Protect(
                    plaintextBytes,
                    null, // No additional entropy
                    DataProtectionScope.CurrentUser // User-specific encryption
                );
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("DPAPI encryption failed - falling back to plaintext", ex);
                // Fallback to plaintext if encryption fails (fail-open)
                return Encoding.UTF8.GetBytes(plaintext);
            }
        }

        /// <summary>
        /// Check if we have a valid local license stored
        /// </summary>
        public static bool HasValidLicense(out StoredLicense? license)
        {
#if DEBUG
            // TEST MODE: Return mock license for unit tests
            if (_testMode)
            {
                license = _mockLicense;
                return _mockHasValidLicense;
            }
#endif

            lock (_fileLock) // BUG FIX: Thread-safe file access
            {
                license = null;

                if (!File.Exists(LicensePath))
                {
                    return false;
                }

                try
                {
                    // Read encrypted license file
                    var encryptedData = File.ReadAllBytes(LicensePath);

                    // Decrypt using DPAPI (falls back to plaintext for old licenses)
                    var json = DecryptData(encryptedData);

                    license = JsonSerializer.Deserialize<StoredLicense>(json);

                    if (license == null || string.IsNullOrWhiteSpace(license.LicenseKey))
                    {
                        license = null; // BUG FIX: Set to null before returning to prevent null reference exceptions
                        return false;
                    }

                    // License is valid if it exists and has required fields
                    return true;
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Failed to read stored license", ex);
                    return false;
                }
            }
        }

        /// <summary>
        /// Save validated license locally (called after successful online validation)
        /// Uses Windows DPAPI encryption to protect license data from tampering
        /// </summary>
        public static void SaveLicense(string licenseKey, string email = "", string type = "LIFETIME")
        {
            // BUG FIX #3: Input validation - fail fast on invalid data
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                throw new ArgumentException("License key cannot be null or empty", nameof(licenseKey));
            }

            // Email is optional - it may not be returned by the API for privacy reasons
            // If not provided, we'll store an empty string
            email = email ?? "";

            lock (_fileLock) // BUG FIX #2: Thread-safe file access
            {
                try
                {
                    var license = new StoredLicense
                    {
                        LicenseKey = licenseKey,
                        Email = email,
                        ValidatedAt = DateTime.UtcNow,
                        Type = type
                    };

                    // Ensure directory exists
                    Directory.CreateDirectory(AppDataPath);

                    // Serialize to JSON
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };
                    var json = JsonSerializer.Serialize(license, options);

                    // SECURITY FIX: Encrypt using Windows DPAPI before saving
                    var encryptedData = EncryptData(json);
                    File.WriteAllBytes(LicensePath, encryptedData);

                    ErrorLogger.LogMessage($"License saved locally (encrypted) for {email}");
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Failed to save license", ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Delete stored license (used for deactivation or when license becomes invalid)
        /// </summary>
        public static void DeleteLicense()
        {
            lock (_fileLock) // BUG FIX: Thread-safe file access
            {
                try
                {
                    if (File.Exists(LicensePath))
                    {
                        File.Delete(LicensePath);
                        ErrorLogger.LogMessage("Local license deleted");
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Failed to delete license", ex);
                }
            }
        }

        /// <summary>
        /// Get the stored license without validation (for display purposes)
        /// </summary>
        public static StoredLicense? GetStoredLicense()
        {
            if (HasValidLicense(out var license))
            {
                return license;
            }
            return null;
        }

        /// <summary>
        /// Check if license file exists (doesn't validate contents)
        /// </summary>
        public static bool LicenseFileExists()
        {
            return File.Exists(LicensePath);
        }

        /// <summary>
        /// Check if user is using Pro version (has valid license)
        /// </summary>
        public static bool IsProVersion()
        {
#if DEBUG
            if (_testMode)
            {
                return _mockHasValidLicense;
            }
#endif
            return HasValidLicense(out _);
        }

        /// <summary>
        /// Check if user is using Free version (no license)
        /// </summary>
        public static bool IsFreeVersion()
        {
#if DEBUG
            if (_testMode)
            {
                return !_mockHasValidLicense;
            }
#endif
            return !HasValidLicense(out _);
        }
    }
}

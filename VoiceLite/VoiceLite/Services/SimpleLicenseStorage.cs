using System;
using System.IO;
using System.Text.Json;

namespace VoiceLite.Services
{
    /// <summary>
    /// Simple local license storage for one-time purchase model.
    /// Stores validated license locally after first online validation.
    /// No network calls needed after initial activation.
    /// </summary>
    public static class SimpleLicenseStorage
    {
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
        /// Check if we have a valid local license stored
        /// </summary>
        public static bool HasValidLicense(out StoredLicense? license)
        {
            license = null;

            if (!File.Exists(LicensePath))
            {
                return false;
            }

            try
            {
                var json = File.ReadAllText(LicensePath);
                license = JsonSerializer.Deserialize<StoredLicense>(json);

                if (license == null || string.IsNullOrWhiteSpace(license.LicenseKey))
                {
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

        /// <summary>
        /// Save validated license locally (called after successful online validation)
        /// </summary>
        public static void SaveLicense(string licenseKey, string email, string type = "LIFETIME")
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

                // Serialize and save
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var json = JsonSerializer.Serialize(license, options);
                File.WriteAllText(LicensePath, json);

                ErrorLogger.LogMessage($"License saved locally for {email}");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to save license", ex);
                throw;
            }
        }

        /// <summary>
        /// Delete stored license (used for deactivation or when license becomes invalid)
        /// </summary>
        public static void DeleteLicense()
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
            return HasValidLicense(out _);
        }

        /// <summary>
        /// Check if user is using Free version (no license)
        /// </summary>
        public static bool IsFreeVersion()
        {
            return !HasValidLicense(out _);
        }
    }
}

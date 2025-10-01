using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VoiceLite.Models;

namespace VoiceLite.Services.Licensing
{
    /// <summary>
    /// Handles secure storage and retrieval of license files using Windows DPAPI encryption.
    /// License files are stored in %APPDATA%\VoiceLite\license.dat
    /// </summary>
    public sealed class LicenseStorage
    {
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoiceLite"
        );

        private static readonly string LicenseFilePath = Path.Combine(AppDataPath, "license.dat");
        private static readonly string CRLFilePath = Path.Combine(AppDataPath, "crl.dat");

        /// <summary>
        /// Save a signed license to disk with DPAPI encryption.
        /// </summary>
        public void SaveLicense(string signedLicense)
        {
            if (string.IsNullOrWhiteSpace(signedLicense))
            {
                throw new ArgumentException("Signed license cannot be null or whitespace", nameof(signedLicense));
            }

            Directory.CreateDirectory(AppDataPath);

            var plainBytes = Encoding.UTF8.GetBytes(signedLicense);
            var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

            File.WriteAllBytes(LicenseFilePath, encryptedBytes);
        }

        /// <summary>
        /// Load and decrypt the signed license from disk.
        /// Returns null if no license exists or decryption fails.
        /// </summary>
        public string? LoadLicense()
        {
            try
            {
                if (!File.Exists(LicenseFilePath))
                {
                    return null;
                }

                var encryptedBytes = File.ReadAllBytes(LicenseFilePath);
                var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch
            {
                // Decryption failure, corrupted file, or other error
                return null;
            }
        }

        /// <summary>
        /// Delete the license file from disk.
        /// </summary>
        public void DeleteLicense()
        {
            try
            {
                if (File.Exists(LicenseFilePath))
                {
                    File.Delete(LicenseFilePath);
                }
            }
            catch
            {
                // Ignore errors during deletion
            }
        }

        /// <summary>
        /// Check if a license file exists on disk.
        /// </summary>
        public bool HasLicense()
        {
            return File.Exists(LicenseFilePath);
        }

        /// <summary>
        /// Save CRL (Certificate Revocation List) to disk with DPAPI encryption.
        /// </summary>
        public void SaveCRL(string signedCRL)
        {
            if (string.IsNullOrWhiteSpace(signedCRL))
            {
                throw new ArgumentException("Signed CRL cannot be null or whitespace", nameof(signedCRL));
            }

            Directory.CreateDirectory(AppDataPath);

            var plainBytes = Encoding.UTF8.GetBytes(signedCRL);
            var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

            File.WriteAllBytes(CRLFilePath, encryptedBytes);
        }

        /// <summary>
        /// Load and decrypt the CRL from disk.
        /// Returns null if no CRL exists or decryption fails.
        /// </summary>
        public string? LoadCRL()
        {
            try
            {
                if (!File.Exists(CRLFilePath))
                {
                    return null;
                }

                var encryptedBytes = File.ReadAllBytes(CRLFilePath);
                var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the timestamp of the last CRL update.
        /// Returns null if CRL file doesn't exist.
        /// </summary>
        public DateTime? GetCRLLastUpdated()
        {
            try
            {
                if (!File.Exists(CRLFilePath))
                {
                    return null;
                }

                return File.GetLastWriteTimeUtc(CRLFilePath);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if CRL needs to be refreshed (older than 24 hours).
        /// </summary>
        public bool ShouldRefreshCRL()
        {
            var lastUpdate = GetCRLLastUpdated();
            if (lastUpdate == null)
            {
                return true; // No CRL exists, should fetch
            }

            var age = DateTime.UtcNow - lastUpdate.Value;
            return age.TotalHours >= 24;
        }
    }
}

using System;
using System.Threading.Tasks;

namespace VoiceLite.Core.Interfaces.Features
{
    /// <summary>
    /// Interface for license validation and management
    /// </summary>
    public interface ILicenseService
    {
        /// <summary>
        /// Validates a license key
        /// </summary>
        /// <param name="licenseKey">The license key to validate</param>
        /// <returns>Validation result with details</returns>
        Task<LicenseValidationResult> ValidateLicenseAsync(string licenseKey);

        /// <summary>
        /// Gets the current license status
        /// </summary>
        bool IsLicenseValid { get; }

        /// <summary>
        /// Gets the license key if one is stored
        /// </summary>
        string GetStoredLicenseKey();

        /// <summary>
        /// Saves a license key
        /// </summary>
        /// <param name="licenseKey">The license key to save</param>
        void SaveLicenseKey(string licenseKey);

        /// <summary>
        /// Removes the stored license key
        /// </summary>
        void RemoveLicenseKey();

        /// <summary>
        /// Gets the email associated with the license
        /// </summary>
        string GetLicenseEmail();

        /// <summary>
        /// Gets the number of activations used
        /// </summary>
        int GetActivationCount();

        /// <summary>
        /// Gets the maximum number of activations allowed
        /// </summary>
        int GetMaxActivations();

        /// <summary>
        /// Raised when license status changes
        /// </summary>
        event EventHandler<bool> LicenseStatusChanged;
    }

    /// <summary>
    /// Result of license validation
    /// </summary>
    public class LicenseValidationResult
    {
        public bool IsValid { get; set; }
        public string Tier { get; set; } = "free";
        public string? ErrorMessage { get; set; }
        public string? Email { get; set; }
        public int? ActivationCount { get; set; }
    }
}
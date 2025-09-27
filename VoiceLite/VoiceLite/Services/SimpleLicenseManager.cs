using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace VoiceLite.Services
{
    public enum SimpleLicenseType
    {
        Trial,
        Personal,
        Professional,
        Business
    }

    public class SimpleLicenseManager
    {
        private readonly string licensePath;
        private readonly string registryKey = @"SOFTWARE\VoiceLite";
        private LicenseData? currentLicense;

        public class LicenseData
        {
            public string Key { get; set; } = "";
            public SimpleLicenseType Type { get; set; } = SimpleLicenseType.Trial;
            public DateTime StartDate { get; set; } = DateTime.Now;
            public DateTime? ActivationDate { get; set; }
            public bool IsActivated { get; set; }
        }

        public SimpleLicenseManager()
        {
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VoiceLite");
            Directory.CreateDirectory(appData);
            licensePath = Path.Combine(appData, "license.json");
            LoadLicense();
        }

        private void LoadLicense()
        {
            try
            {
                // Try to load from file
                if (File.Exists(licensePath))
                {
                    var json = File.ReadAllText(licensePath);
                    currentLicense = JsonSerializer.Deserialize<LicenseData>(json);
                }

                // If no file, check registry for trial start
                if (currentLicense == null)
                {
                    currentLicense = new LicenseData();

                    using (var key = Registry.CurrentUser.OpenSubKey(registryKey))
                    {
                        if (key != null)
                        {
                            var startDateStr = key.GetValue("TrialStart") as string;
                            if (DateTime.TryParse(startDateStr, out var startDate))
                            {
                                currentLicense.StartDate = startDate;
                            }
                        }
                    }

                    SaveLicense();
                }
            }
            catch
            {
                // If anything fails, just use defaults
                currentLicense = new LicenseData();
            }
        }

        private void SaveLicense()
        {
            try
            {
                var json = JsonSerializer.Serialize(currentLicense, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(licensePath, json);

                // Also save to registry for trial tracking
                using (var key = Registry.CurrentUser.CreateSubKey(registryKey))
                {
                    key?.SetValue("TrialStart", currentLicense!.StartDate.ToString("O"));
                    if (currentLicense.IsActivated)
                    {
                        key?.SetValue("Activated", "true");
                    }
                }
            }
            catch
            {
                // Ignore save failures
            }
        }

        public bool IsValid()
        {
            if (currentLicense == null) return false;

            // If activated with a valid key, always valid
            if (currentLicense.IsActivated && !string.IsNullOrEmpty(currentLicense.Key))
                return true;

            // Otherwise check trial period (14 days)
            var daysUsed = (DateTime.Now - currentLicense.StartDate).TotalDays;
            return daysUsed <= 14;
        }

        public int GetTrialDaysRemaining()
        {
            if (currentLicense?.IsActivated == true) return -1; // Not in trial

            var daysUsed = (DateTime.Now - (currentLicense?.StartDate ?? DateTime.Now)).TotalDays;
            var remaining = 14 - (int)daysUsed;
            return Math.Max(0, remaining);
        }

        public bool ActivateLicense(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
                return false;

            // Simple offline validation
            licenseKey = licenseKey.Trim().ToUpper().Replace(" ", "");

            // Expected format: XXX-XXXX-XXXX-XXXX-XXXX
            if (licenseKey.Length < 23)
                return false;

            // Determine license type from prefix
            var prefix = licenseKey.Substring(0, 3);
            var type = prefix switch
            {
                "PER" => SimpleLicenseType.Personal,
                "PRO" => SimpleLicenseType.Professional,
                "BUS" => SimpleLicenseType.Business,
                _ => SimpleLicenseType.Trial
            };

            // If no valid prefix, reject
            if (type == SimpleLicenseType.Trial)
                return false;

            // Validate format and checksum
            var keyPart = licenseKey.Replace("-", "");

            // Must be exactly prefix + 16 hex characters
            if (!System.Text.RegularExpressions.Regex.IsMatch(keyPart, @"^(PER|PRO|BUS)[A-F0-9]{16}$"))
            {
                return false;
            }

            // Simple checksum validation - last 4 chars should match a calculated value
            var mainPart = keyPart.Substring(0, keyPart.Length - 4);
            var checksum = keyPart.Substring(keyPart.Length - 4);

            // Calculate expected checksum
            int sum = 0;
            foreach (char c in mainPart)
            {
                sum += c;
            }
            var expectedChecksum = (sum * 7919).ToString("X4"); // Use prime number for better distribution

            // Validate checksum
            if (!checksum.Equals(expectedChecksum.Substring(expectedChecksum.Length - 4)))
            {
                return false;
            }

            // Activate
            currentLicense = new LicenseData
            {
                Key = licenseKey,
                Type = type,
                StartDate = currentLicense?.StartDate ?? DateTime.Now,
                ActivationDate = DateTime.Now,
                IsActivated = true
            };

            SaveLicense();
            return true;
        }

        public string GetLicenseStatus()
        {
            if (currentLicense?.IsActivated == true)
            {
                return $"{currentLicense.Type} License - Activated";
            }

            var days = GetTrialDaysRemaining();
            if (days > 0)
            {
                return $"Trial - {days} days remaining";
            }

            return "Trial Expired";
        }

        public SimpleLicenseType GetLicenseType()
        {
            return currentLicense?.Type ?? SimpleLicenseType.Trial;
        }

        // Removed ResetTrial() - security vulnerability
    }
}
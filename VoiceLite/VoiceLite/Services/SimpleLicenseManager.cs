using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace VoiceLite.Services
{
    public enum SimpleLicenseType
    {
        Free,
        Pro
    }

    public class SimpleLicenseManager
    {
        private readonly string licensePath;
        private readonly string registryKey = @"SOFTWARE\VoiceLite";
        private LicenseData? currentLicense;
        private readonly HttpClient httpClient;
        private const string LICENSE_SERVER_URL = "https://voicelite-license.up.railway.app"; // Update when deployed

        public class LicenseData
        {
            public string Key { get; set; } = "";
            public SimpleLicenseType Type { get; set; } = SimpleLicenseType.Free;
            public DateTime StartDate { get; set; } = DateTime.Now;
            public DateTime? ActivationDate { get; set; }
            public bool IsActivated { get; set; }
        }

        public SimpleLicenseManager()
        {
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VoiceLite");
            Directory.CreateDirectory(appData);
            licensePath = Path.Combine(appData, "license.json");
            httpClient = new HttpClient();
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
            // Always valid - usage limits are handled by UsageTracker
            return true;
        }

        public int GetTrialDaysRemaining()
        {
            // No longer using trial system
            return -1;
        }

        public bool ActivateLicense(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
                return false;

            licenseKey = licenseKey.Trim().ToUpper().Replace(" ", "");

            // Basic format validation
            if (licenseKey.Length < 20)
                return false;

            try
            {
                // Validate with server (this will be async in production)
                var result = ValidateLicenseWithServer(licenseKey);
                if (!result)
                    return false;

                // For now, do basic offline validation as fallback
                var prefix = licenseKey.Substring(0, 3);
                var type = prefix switch
                {
                    "PRO" => SimpleLicenseType.Pro,
                    "SUB" => SimpleLicenseType.Pro, // Subscription prefix
                    _ => SimpleLicenseType.Free
                };

                if (type == SimpleLicenseType.Free)
                    return false;

                // Store activated license
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
            catch
            {
                // If server validation fails, reject activation
                return false;
            }
        }

        private bool ValidateLicenseWithServer(string licenseKey)
        {
            // TODO Phase 1.3: Implement JWT-based license tokens for enhanced security
            // - Server returns signed JWT containing: {type, expiry, machineId, signature}
            // - Client validates signature using public key (embedded in app)
            // - Private key stays on server only
            // - Requires: System.IdentityModel.Tokens.Jwt NuGet package

            try
            {
                // Validate with actual license server
                var request = new
                {
                    key = licenseKey,
                    machine_id = GetMachineId()
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Use synchronous call in constructor context
                var task = httpClient.PostAsync($"{LICENSE_SERVER_URL}/api/validate", content);
                task.Wait(TimeSpan.FromSeconds(10)); // 10 second timeout

                if (!task.IsCompleted || task.Result == null)
                {
                    ErrorLogger.LogMessage("License validation timed out");
                    return false; // Fail closed on timeout
                }

                var response = task.Result;
                if (!response.IsSuccessStatusCode)
                {
                    ErrorLogger.LogMessage($"License validation failed: {response.StatusCode}");
                    return false; // Fail closed on server error
                }

                var responseTask = response.Content.ReadAsStringAsync();
                responseTask.Wait(TimeSpan.FromSeconds(5));

                if (!responseTask.IsCompleted)
                {
                    return false;
                }

                var result = JsonSerializer.Deserialize<JsonElement>(responseTask.Result);

                if (result.TryGetProperty("valid", out var valid))
                {
                    return valid.GetBoolean();
                }

                return false;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("License validation failed", ex);
                return false; // Fail closed on any error
            }
        }

        private string GetMachineId()
        {
            try
            {
                // Use same machine ID generation as SecurityService for consistency
                using var searcher = new System.Management.ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
                var cpuId = searcher.Get().Cast<System.Management.ManagementObject>().FirstOrDefault()?["ProcessorId"]?.ToString() ?? "UNKNOWN";
                var machineGuid = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography")?.GetValue("MachineGuid")?.ToString();

                var combined = $"{cpuId}|{machineGuid}";
                using (var sha256 = System.Security.Cryptography.SHA256.Create())
                {
                    var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
                    return Convert.ToBase64String(hash).Substring(0, 16);
                }
            }
            catch
            {
                // Fallback to username + machine name
                var fallback = Environment.UserName + Environment.MachineName;
                using (var sha256 = System.Security.Cryptography.SHA256.Create())
                {
                    var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(fallback));
                    return Convert.ToBase64String(hash).Substring(0, 16);
                }
            }
        }

        public string GetLicenseStatus()
        {
            if (currentLicense?.IsActivated == true)
            {
                return $"Pro Subscription - Active";
            }

            return "Free Account";
        }

        public SimpleLicenseType GetLicenseType()
        {
            return currentLicense?.Type ?? SimpleLicenseType.Free;
        }

        // Removed ResetTrial() - security vulnerability
    }
}
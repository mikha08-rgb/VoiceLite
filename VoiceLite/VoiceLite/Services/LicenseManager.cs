using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    public class LicenseManager
    {
        private readonly string licensePath;
        private readonly string trialPath;
        private LicenseInfo? currentLicense;
        private readonly string machineId;
        private readonly HttpClient httpClient;
        private const string LICENSE_SERVER_URL = "https://api.voicelite.app"; // Production API
        private const string API_KEY = "CE7038B50A2FC2F91C52D042EAADAA77"; // Production API key

        public LicenseManager()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VoiceLite"
            );

            Directory.CreateDirectory(appDataPath);

            licensePath = Path.Combine(appDataPath, "license.dat");
            trialPath = Path.Combine(appDataPath, ".trial");
            machineId = GetMachineId();
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("x-api-key", API_KEY);
        }

        public LicenseInfo GetCurrentLicense()
        {
            if (currentLicense != null)
                return currentLicense;

            currentLicense = LoadLicense() ?? InitializeTrial();
            return currentLicense;
        }

        private LicenseInfo? LoadLicense()
        {
            try
            {
                // First check for a paid license
                if (File.Exists(licensePath))
                {
                    var encryptedData = File.ReadAllText(licensePath);
                    var decryptedJson = DecryptData(encryptedData);
                    var license = JsonSerializer.Deserialize<LicenseInfo>(decryptedJson);

                    if (license != null)
                    {
                        // Verify machine ID matches
                        if (license.MachineId != machineId)
                        {
                            license.Status = LicenseStatus.Invalid;
                            ErrorLogger.LogMessage($"License machine ID mismatch. Expected: {machineId}, Got: {license.MachineId}");
                        }
                        else if (license.IsValid())
                        {
                            license.Status = LicenseStatus.Valid;
                        }
                        else
                        {
                            license.Status = license.Type == LicenseType.Trial
                                ? LicenseStatus.TrialExpired
                                : LicenseStatus.Expired;
                        }

                        return license;
                    }
                }

                // Check for trial - FIRST check registry (harder to tamper)
                var (trialDate, trialMachineId) = SecurityService.GetTrialFromRegistry();
                if (trialDate.HasValue && trialMachineId == machineId)
                {
                    var trial = new LicenseInfo
                    {
                        Type = LicenseType.Trial,
                        TrialStartDate = trialDate.Value,
                        ActivationDate = trialDate.Value,
                        ExpirationDate = trialDate.Value.AddDays(14),
                        MachineId = machineId,
                        RegisteredTo = "Trial User"
                    };

                    if (trial.IsValid())
                    {
                        trial.Status = LicenseStatus.Valid;
                    }
                    else
                    {
                        trial.Status = LicenseStatus.TrialExpired;
                    }
                    return trial;
                }

                // Fallback to file-based trial (if registry was cleared)
                if (File.Exists(trialPath))
                {
                    try
                    {
                        var trialData = File.ReadAllText(trialPath);
                        var decryptedTrial = SecurityService.DecryptString(trialData);
                        var trial = JsonSerializer.Deserialize<LicenseInfo>(decryptedTrial);

                        if (trial != null && trial.MachineId == machineId)
                        {
                            // Restore to registry if missing
                            if (!trialDate.HasValue && trial.TrialStartDate.HasValue)
                            {
                                SecurityService.StoreTrial(trial.TrialStartDate.Value, machineId);
                            }

                            if (trial.IsValid())
                            {
                                trial.Status = LicenseStatus.Valid;
                            }
                            else
                            {
                                trial.Status = LicenseStatus.TrialExpired;
                            }
                            return trial;
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to load license", ex);
            }

            return null;
        }

        private LicenseInfo InitializeTrial()
        {
            var trial = new LicenseInfo
            {
                Type = LicenseType.Trial,
                Status = LicenseStatus.Valid,
                TrialStartDate = DateTime.Now,
                ActivationDate = DateTime.Now,
                ExpirationDate = DateTime.Now.AddDays(14),
                MachineId = machineId,
                RegisteredTo = "Trial User"
            };

            SaveTrial(trial);
            return trial;
        }

        private void SaveTrial(LicenseInfo trial)
        {
            try
            {
                // Save to registry (harder to reset than file)
                if (trial.TrialStartDate.HasValue)
                {
                    SecurityService.StoreTrial(trial.TrialStartDate.Value, machineId);
                }

                // Also save to file as backup
                var json = JsonSerializer.Serialize(trial);
                var encrypted = SecurityService.EncryptString(json);
                File.WriteAllText(trialPath, encrypted);

                // Set file as hidden and system
                File.SetAttributes(trialPath, FileAttributes.Hidden | FileAttributes.System);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to save trial info", ex);
            }
        }

        public bool ActivateLicense(string licenseKey, string email)
        {
            try
            {
                // Try server activation first
                var serverResult = ActivateWithServer(licenseKey, email).Result;
                if (serverResult != null)
                {
                    SaveLicense(serverResult);
                    currentLicense = serverResult;
                    return true;
                }

                // Fallback to local validation for testing
                var license = ValidateLicenseKey(licenseKey);
                if (license == null)
                    return false;

                license.LicenseKey = licenseKey;
                license.Email = email;
                license.MachineId = machineId;
                license.ActivationDate = DateTime.Now;
                license.Status = LicenseStatus.Valid;

                SaveLicense(license);
                currentLicense = license;

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("License activation failed", ex);
                return false;
            }
        }

        private LicenseInfo? ValidateLicenseKey(string key)
        {
            // Simplified validation for testing
            // Format: XXXX-XXXX-XXXX-XXXX
            if (string.IsNullOrWhiteSpace(key) || key.Length != 19)
                return null;

            // Parse license type from key prefix (for testing)
            // Real implementation would verify with server
            var prefix = key.Substring(0, 4).ToUpper();

            var license = new LicenseInfo();

            if (prefix.StartsWith("PERS"))
            {
                license.Type = LicenseType.Personal;
                license.ExpirationDate = null; // Lifetime license
            }
            else if (prefix.StartsWith("PRO"))
            {
                license.Type = LicenseType.Pro;
                license.ExpirationDate = null;
            }
            else if (prefix.StartsWith("BUS"))
            {
                license.Type = LicenseType.Business;
                license.ExpirationDate = null;
                license.DeviceLimit = 5;
            }
            else
            {
                return null;
            }

            return license;
        }

        private void SaveLicense(LicenseInfo license)
        {
            try
            {
                var json = JsonSerializer.Serialize(license);
                var encrypted = EncryptData(json);
                File.WriteAllText(licensePath, encrypted);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to save license", ex);
            }
        }

        public void RevokeLicense()
        {
            try
            {
                if (File.Exists(licensePath))
                    File.Delete(licensePath);

                currentLicense = null;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to revoke license", ex);
            }
        }

        private string GetMachineId()
        {
            try
            {
                var components = new StringBuilder();

                // CPU ID
                using (var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                {
                    foreach (var item in searcher.Get())
                    {
                        components.Append(item["ProcessorId"]?.ToString() ?? "");
                        break;
                    }
                }

                // Motherboard Serial
                using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard"))
                {
                    foreach (var item in searcher.Get())
                    {
                        components.Append(item["SerialNumber"]?.ToString() ?? "");
                        break;
                    }
                }

                // Create hash of components
                using (var sha256 = SHA256.Create())
                {
                    var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(components.ToString()));
                    return Convert.ToBase64String(hash).Substring(0, 16);
                }
            }
            catch
            {
                // Fallback to username + machine name
                var fallback = Environment.UserName + Environment.MachineName;
                using (var sha256 = SHA256.Create())
                {
                    var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(fallback));
                    return Convert.ToBase64String(hash).Substring(0, 16);
                }
            }
        }

        private string EncryptData(string plainText)
        {
            // Simple XOR encryption for local storage
            // In production, use proper encryption with Cryptlex or similar
            var key = Encoding.UTF8.GetBytes(machineId.PadRight(32).Substring(0, 32));
            var data = Encoding.UTF8.GetBytes(plainText);

            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= key[i % key.Length];
            }

            return Convert.ToBase64String(data);
        }

        private string DecryptData(string cipherText)
        {
            var key = Encoding.UTF8.GetBytes(machineId.PadRight(32).Substring(0, 32));
            var data = Convert.FromBase64String(cipherText);

            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= key[i % key.Length];
            }

            return Encoding.UTF8.GetString(data);
        }

        public bool CheckModelAccess(string modelName)
        {
            var license = GetCurrentLicense();
            if (!license.IsValid())
                return false;

            return license.AllowedModels.Contains(modelName);
        }

        public int GetDailyUsageLimit()
        {
            var license = GetCurrentLicense();
            return license.Type switch
            {
                LicenseType.Trial => 600, // 10 minutes daily
                _ => int.MaxValue // Unlimited for paid licenses
            };
        }

        private async Task<LicenseInfo?> ActivateWithServer(string licenseKey, string email)
        {
            try
            {
                var request = new
                {
                    license_key = licenseKey,
                    email = email,
                    machine_id = machineId
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{LICENSE_SERVER_URL}/api/activate", content);
                if (!response.IsSuccessStatusCode)
                {
                    ErrorLogger.LogMessage($"Server activation failed: {response.StatusCode}");
                    return null;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(responseJson);

                if (result.TryGetProperty("success", out var success) && success.GetBoolean())
                {
                    var licenseType = result.GetProperty("license_type").GetString();
                    return new LicenseInfo
                    {
                        LicenseKey = licenseKey,
                        Email = email,
                        MachineId = machineId,
                        Type = Enum.Parse<LicenseType>(licenseType ?? "Personal"),
                        Status = LicenseStatus.Valid,
                        ActivationDate = DateTime.Now,
                        RegisteredTo = email
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Server activation failed", ex);
                return null;
            }
        }

        public async Task<bool> ValidateWithServer()
        {
            try
            {
                var license = GetCurrentLicense();
                if (license == null || string.IsNullOrEmpty(license.LicenseKey))
                    return false;

                var request = new
                {
                    license_key = license.LicenseKey,
                    machine_id = machineId
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{LICENSE_SERVER_URL}/api/validate", content);
                if (!response.IsSuccessStatusCode)
                    return false;

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(responseJson);

                return result.TryGetProperty("valid", out var valid) && valid.GetBoolean();
            }
            catch
            {
                // Server validation failed, use local validation
                return GetCurrentLicense()?.IsValid() ?? false;
            }
        }
    }
}
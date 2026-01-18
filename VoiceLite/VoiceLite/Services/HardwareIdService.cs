using System;
using System.IO;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace VoiceLite.Services
{
    /// <summary>
    /// Generates unique hardware-based identifiers for device activation tracking.
    /// Uses CPU ID and motherboard serial number for stable identification.
    /// </summary>
    public static class HardwareIdService
    {
        // HIGH-6 FIX: Path to store fallback machine ID (persistent across restarts)
        private static readonly string FallbackIdFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VoiceLite",
            "machine_id.dat"
        );

        // HIGH-4 FIX: Entropy for DPAPI encryption of machine_id.dat
        // Prevents users from copying machine_id.dat to other machines to bypass device limit
        private static readonly byte[] DpapiEntropy = Encoding.UTF8.GetBytes("VoiceLite-MachineId-v1");
        /// <summary>
        /// Gets a unique machine identifier based on hardware.
        /// Format: Base64-encoded SHA256 hash of CPU ID + Motherboard Serial (truncated to 32 chars)
        /// </summary>
        public static string GetMachineId()
        {
            try
            {
                var cpuId = GetCpuId();
                var mbSerial = GetMotherboardSerial();
                var combined = $"{cpuId}:{mbSerial}";

                using (var sha256 = SHA256.Create())
                {
                    var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
                    var base64 = Convert.ToBase64String(hash);
                    // Return first 32 characters for reasonable length
                    return base64.Substring(0, Math.Min(32, base64.Length));
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to generate machine ID", ex);
                // Fallback to computer name hash if hardware IDs fail
                return GetFallbackMachineId();
            }
        }

        /// <summary>
        /// Gets a human-readable machine label (computer name)
        /// </summary>
        public static string GetMachineLabel()
        {
            try
            {
                return Environment.MachineName;
            }
            catch
            {
                return "Unknown Machine";
            }
        }

        /// <summary>
        /// Gets a secondary hardware hash for additional verification
        /// Includes CPU ID, Motherboard Serial, and BIOS Serial
        /// </summary>
        public static string GetMachineHash()
        {
            try
            {
                var cpuId = GetCpuId();
                var mbSerial = GetMotherboardSerial();
                var biosSerial = GetBiosSerial();
                var combined = $"{cpuId}:{mbSerial}:{biosSerial}";

                using (var sha256 = SHA256.Create())
                {
                    var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
                    return Convert.ToBase64String(hash);
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to generate machine hash", ex);
                return string.Empty;
            }
        }

        private static string GetCpuId()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                using (var collection = searcher.Get())
                {
                    foreach (ManagementObject obj in collection)
                    {
                        using (obj)  // WMI resource leak fix: ManagementObject must be disposed
                        {
                            var processorId = obj["ProcessorId"]?.ToString();
                            if (!string.IsNullOrEmpty(processorId))
                            {
                                return processorId;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning($"Failed to get CPU ID: {ex.Message}");
            }

            return "UNKNOWN_CPU";
        }

        private static string GetMotherboardSerial()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard"))
                using (var collection = searcher.Get())
                {
                    foreach (ManagementObject obj in collection)
                    {
                        using (obj)  // WMI resource leak fix: ManagementObject must be disposed
                        {
                            var serial = obj["SerialNumber"]?.ToString();
                            if (!string.IsNullOrEmpty(serial) && serial != "None")
                            {
                                return serial;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning($"Failed to get motherboard serial: {ex.Message}");
            }

            return "UNKNOWN_MB";
        }

        private static string GetBiosSerial()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS"))
                using (var collection = searcher.Get())
                {
                    foreach (ManagementObject obj in collection)
                    {
                        using (obj)  // WMI resource leak fix: ManagementObject must be disposed
                        {
                            var serial = obj["SerialNumber"]?.ToString();
                            if (!string.IsNullOrEmpty(serial))
                            {
                                return serial;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning($"Failed to get BIOS serial: {ex.Message}");
            }

            return "UNKNOWN_BIOS";
        }

        private static string GetFallbackMachineId()
        {
            try
            {
                // Use computer name + user domain as fallback
                var identifier = $"{Environment.MachineName}:{Environment.UserDomainName}";
                using (var sha256 = SHA256.Create())
                {
                    var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(identifier));
                    var base64 = Convert.ToBase64String(hash);
                    return base64.Substring(0, Math.Min(32, base64.Length));
                }
            }
            catch
            {
                // HIGH-6 FIX: Last resort - use PERSISTENT random GUID (saved to disk)
                // Previously generated a new GUID every call, causing license bypass on VMs
                return GetOrCreatePersistentFallbackId();
            }
        }

        /// <summary>
        /// HIGH-6 FIX: Gets or creates a persistent fallback machine ID
        /// HIGH-4 FIX: Now encrypted with DPAPI to prevent copying to other machines
        /// Stores a random GUID in %LOCALAPPDATA%\VoiceLite\machine_id.dat (encrypted)
        /// This prevents users on VMs/headless systems from bypassing the 3-device limit
        /// </summary>
        private static string GetOrCreatePersistentFallbackId()
        {
            try
            {
                // Check if we have a saved fallback ID
                if (File.Exists(FallbackIdFilePath))
                {
                    var fileBytes = File.ReadAllBytes(FallbackIdFilePath);

                    // HIGH-4 FIX: Try to decrypt with DPAPI first (new format)
                    try
                    {
                        var decryptedBytes = ProtectedData.Unprotect(
                            fileBytes,
                            DpapiEntropy,
                            DataProtectionScope.CurrentUser
                        );
                        var savedId = Encoding.UTF8.GetString(decryptedBytes).Trim();
                        if (!string.IsNullOrEmpty(savedId) && savedId.Length == 32)
                        {
                            return savedId;
                        }
                    }
                    catch (CryptographicException)
                    {
                        // Migration: Try reading as plaintext (old format)
                        var savedId = Encoding.UTF8.GetString(fileBytes).Trim();
                        if (!string.IsNullOrEmpty(savedId) && savedId.Length == 32 &&
                            savedId.All(c => char.IsLetterOrDigit(c)))
                        {
                            // Migrate to encrypted format
                            ErrorLogger.LogMessage("Migrating machine ID to encrypted format...");
                            SaveEncryptedMachineId(savedId);
                            return savedId;
                        }
                    }
                }

                // Generate and save a new persistent ID (encrypted)
                var newId = Guid.NewGuid().ToString("N").Substring(0, 32);
                SaveEncryptedMachineId(newId);
                ErrorLogger.LogWarning($"Created persistent fallback machine ID (hardware detection failed)");

                return newId;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning($"Failed to persist fallback machine ID: {ex.Message}");
                // Absolute last resort - still generate random but log warning
                return Guid.NewGuid().ToString("N").Substring(0, 32);
            }
        }

        /// <summary>
        /// HIGH-4 FIX: Saves machine ID with DPAPI encryption
        /// </summary>
        private static void SaveEncryptedMachineId(string machineId)
        {
            var plaintextBytes = Encoding.UTF8.GetBytes(machineId);
            var encryptedBytes = ProtectedData.Protect(
                plaintextBytes,
                DpapiEntropy,
                DataProtectionScope.CurrentUser
            );

            // Ensure directory exists
            var directory = Path.GetDirectoryName(FallbackIdFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(FallbackIdFilePath, encryptedBytes);
        }
    }
}

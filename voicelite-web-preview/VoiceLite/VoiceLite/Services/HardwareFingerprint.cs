using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace VoiceLite.Services
{
    /// <summary>
    /// Generates a hardware fingerprint based on CPU and Motherboard information.
    /// Used for hardware-bound license activation.
    /// </summary>
    public static class HardwareFingerprint
    {
        /// <summary>
        /// Generate a unique hardware fingerprint for this machine
        /// </summary>
        public static string Generate()
        {
            try
            {
                var cpuId = GetCpuId();
                var motherboardId = GetMotherboardId();

                // Combine CPU and Motherboard IDs
                var combined = $"{cpuId}-{motherboardId}";

                // Hash to create fixed-length fingerprint
                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
                var hash = Convert.ToBase64String(hashBytes);

                // Return first 32 characters for readability
                return hash.Substring(0, 32).Replace("/", "").Replace("+", "");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to generate hardware fingerprint", ex);
                // Fallback to machine name if WMI fails
                return $"FALLBACK-{Environment.MachineName}-{Environment.UserName}";
            }
        }

        private static string GetCpuId()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
                // AUDIT FIX (RESOURCE-CRIT-1): Dispose ManagementObjectCollection and individual objects
                using var collection = searcher.Get();
                foreach (ManagementObject obj in collection)
                {
                    // AUDIT FIX: Dispose each ManagementObject to prevent WMI handle leak
                    using (obj)
                    {
                        var id = obj["ProcessorId"]?.ToString();
                        if (!string.IsNullOrEmpty(id))
                            return id;
                    }
                }
            }
            catch
            {
                // Fallback
            }

            return "CPU-UNKNOWN";
        }

        private static string GetMotherboardId()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
                // AUDIT FIX (RESOURCE-CRIT-1): Dispose ManagementObjectCollection and individual objects
                using var collection = searcher.Get();
                foreach (ManagementObject obj in collection)
                {
                    // AUDIT FIX: Dispose each ManagementObject to prevent WMI handle leak
                    using (obj)
                    {
                        var serial = obj["SerialNumber"]?.ToString();
                        if (!string.IsNullOrEmpty(serial))
                            return serial;
                    }
                }
            }
            catch
            {
                // Fallback
            }

            return "MB-UNKNOWN";
        }
    }
}

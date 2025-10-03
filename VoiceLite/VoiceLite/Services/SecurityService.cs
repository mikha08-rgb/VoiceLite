using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace VoiceLite.Services
{
    public static class SecurityService
    {
        private static readonly byte[] Salt = Encoding.UTF8.GetBytes("VL2024@Secure#");
        private static readonly string RegistryPath = @"SOFTWARE\VoiceLite\Security";
        private static bool isProtectionActive = false;
        private static bool _integrityWarningLogged = false;
        // Anti-debugging thread is intentionally disabled for open-source build
        // This field exists for compatibility but is never used
        private static Thread? antiDebugThread = null;

        [DllImport("kernel32.dll")]
        private static extern bool IsDebuggerPresent();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isDebuggerPresent);

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass,
            ref int processInformation, int processInformationLength, ref int returnLength);

        static SecurityService()
        {
            StartProtection();
        }

        public static void StartProtection()
        {
            if (isProtectionActive) return;
            isProtectionActive = true;

            // DISABLED: Anti-debugging protection removed for open-source project
            // This is an open-source application - anti-debugging is counterproductive and hostile to:
            // - Contributors who need to debug the application
            // - Developers running Fiddler/Wireshark for network debugging
            // - Users with Process Explorer/Process Monitor for system monitoring
            //
            // The original implementation would:
            // - Kill the app if ANY debugger was attached (preventing legitimate debugging)
            // - Kill the app if Fiddler, Wireshark, Process Hacker, etc. were running ANYWHERE on the system
            // - Consume CPU every 500ms checking for "suspicious" processes
            // - Use Environment.FailFast() which terminates without cleanup (no resource disposal, no save settings)
            //
            // For open-source software, transparency and debuggability are features, not bugs.

            ErrorLogger.LogMessage("SecurityService: Anti-debugging protection is DISABLED (open-source build)");

            // Check integrity on startup (warn only - no forced termination)
            // Note: Environment.FailFast() was removed because it terminates without cleanup:
            // - No resource disposal (audio files, settings, etc.)
            // - No graceful shutdown
            // - Generates crash dumps unnecessarily
            // Instead, we log warnings and let the application continue
            // Only log once per session to reduce log noise
            if (!VerifyIntegrity() && !_integrityWarningLogged)
            {
                ErrorLogger.LogMessage("WARNING: Application integrity check failed - binary may have been modified");
                ErrorLogger.LogMessage("This is a warning only - application will continue to run");
                _integrityWarningLogged = true;
            }
        }

        private static void AntiDebugLoop()
        {
            // DISABLED: This method is no longer called
            // Keeping the method signature for compatibility, but it does nothing
            // Original implementation would forcibly terminate the application if it detected:
            // - Debuggers (IsDebuggerPresent, CheckRemoteDebuggerPresent)
            // - Development tools (dnSpy, ILSpy, dotPeek, etc.)
            // - Network analysis tools (Fiddler, Wireshark, Charles)
            // - Process monitors (Process Hacker, Process Monitor, Process Explorer)
            //
            // This was inappropriate for an open-source project and has been removed.
            ErrorLogger.LogMessage("SecurityService: AntiDebugLoop called but disabled (no-op)");
        }

        public static bool VerifyIntegrity()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var expectedHash = GetStoredAssemblyHash();

                if (string.IsNullOrEmpty(expectedHash))
                {
                    // First run, store the hash
                    StoreAssemblyHash(assembly);
                    return true;
                }

                var currentHash = ComputeAssemblyHash(assembly);
                return expectedHash == currentHash;
            }
            catch
            {
                return true; // Don't block if we can't verify
            }
        }

        private static string ComputeAssemblyHash(Assembly assembly)
        {
            using (var sha256 = SHA256.Create())
            {
                var location = assembly.Location;
                if (string.IsNullOrEmpty(location))
                    return string.Empty;

                using (var stream = File.OpenRead(location))
                {
                    var hash = sha256.ComputeHash(stream);
                    return Convert.ToBase64String(hash);
                }
            }
        }

        private static void StoreAssemblyHash(Assembly assembly)
        {
            try
            {
                var hash = ComputeAssemblyHash(assembly);
                using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    key?.SetValue("AH", EncryptString(hash));
                }
            }
            catch { }
        }

        private static string GetStoredAssemblyHash()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    var encrypted = key?.GetValue("AH") as string;
                    return encrypted != null ? DecryptString(encrypted) : string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        // Registry-based trial tracking (harder to reset than file)
        public static bool StoreTrial(DateTime startDate, string machineId)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key == null) return false;

                    // Store in multiple locations to prevent easy reset
                    key.SetValue("TD", EncryptString(startDate.ToBinary().ToString()));
                    key.SetValue("MI", EncryptString(machineId));

                    // Also store in a secondary hidden location
                    using (var backupKey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VL"))
                    {
                        backupKey?.SetValue("Data", EncryptString($"{startDate.ToBinary()}|{machineId}"));
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static (DateTime? startDate, string? machineId) GetTrialFromRegistry()
        {
            try
            {
                // Try primary location
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key != null)
                    {
                        var encryptedDate = key.GetValue("TD") as string;
                        var encryptedMachine = key.GetValue("MI") as string;

                        if (!string.IsNullOrEmpty(encryptedDate) && !string.IsNullOrEmpty(encryptedMachine))
                        {
                            var dateStr = DecryptString(encryptedDate);
                            var machineStr = DecryptString(encryptedMachine);

                            if (long.TryParse(dateStr, out long binaryDate))
                            {
                                return (DateTime.FromBinary(binaryDate), machineStr);
                            }
                        }
                    }
                }

                // Try backup location
                using (var backupKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VL"))
                {
                    var data = backupKey?.GetValue("Data") as string;
                    if (!string.IsNullOrEmpty(data))
                    {
                        var decrypted = DecryptString(data);
                        var parts = decrypted.Split('|');
                        if (parts.Length == 2 && long.TryParse(parts[0], out long binaryDate))
                        {
                            return (DateTime.FromBinary(binaryDate), parts[1]);
                        }
                    }
                }
            }
            catch { }

            return (null, null);
        }

        // String encryption for sensitive data
        public static string EncryptString(string plainText)
        {
            try
            {
                using (var aes = Aes.Create())
                {
                    // Derive key from machine-specific data
                    var key = GetMachineKey();
                    aes.Key = key;
                    aes.GenerateIV();

                    using (var encryptor = aes.CreateEncryptor())
                    using (var ms = new MemoryStream())
                    {
                        ms.Write(aes.IV, 0, aes.IV.Length);

                        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                        using (var sw = new StreamWriter(cs))
                        {
                            sw.Write(plainText);
                        }

                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
            catch
            {
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
            }
        }

        public static string DecryptString(string cipherText)
        {
            try
            {
                var buffer = Convert.FromBase64String(cipherText);

                using (var aes = Aes.Create())
                {
                    var key = GetMachineKey();
                    aes.Key = key;

                    var iv = new byte[16];
                    Array.Copy(buffer, 0, iv, 0, 16);
                    aes.IV = iv;

                    using (var ms = new MemoryStream(buffer, 16, buffer.Length - 16))
                    using (var decryptor = aes.CreateDecryptor())
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var sr = new StreamReader(cs))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
            catch
            {
                // Fallback for non-encrypted data
                try
                {
                    return Encoding.UTF8.GetString(Convert.FromBase64String(cipherText));
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        private static byte[] GetMachineKey()
        {
            try
            {
                var cpuId = GetCpuId();
                var machineGuid = GetMachineGuid();
                var combined = $"{cpuId}|{machineGuid}|VoiceLite";

                using (var pbkdf2 = new Rfc2898DeriveBytes(combined, Salt, 10000, HashAlgorithmName.SHA256))
                {
                    return pbkdf2.GetBytes(32); // 256-bit key
                }
            }
            catch
            {
                // Fallback key
                using (var pbkdf2 = new Rfc2898DeriveBytes("VoiceLite-Default", Salt, 10000, HashAlgorithmName.SHA256))
                {
                    return pbkdf2.GetBytes(32);
                }
            }
        }

        private static string GetCpuId()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                {
                    foreach (var item in searcher.Get())
                    {
                        return item["ProcessorId"]?.ToString() ?? "DEFAULT";
                    }
                }
            }
            catch { }
            return "DEFAULT";
        }

        private static string GetMachineGuid()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                {
                    return key?.GetValue("MachineGuid")?.ToString() ?? "DEFAULT";
                }
            }
            catch
            {
                return "DEFAULT";
            }
        }

        public static void StopProtection()
        {
            isProtectionActive = false;

            // Try to stop the thread gracefully
            if (antiDebugThread != null && antiDebugThread.IsAlive)
            {
                // Give it up to 1 second to stop
                if (!antiDebugThread.Join(1000))
                {
                    // Force interrupt if it doesn't stop
                    try
                    {
                        antiDebugThread.Interrupt();
                    }
                    catch { }
                }
            }
        }
    }
}
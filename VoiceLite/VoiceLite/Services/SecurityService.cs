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
        private static Thread? antiDebugThread;

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

            // Start anti-debugging thread
            antiDebugThread = new Thread(AntiDebugLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.Lowest
            };
            antiDebugThread.Start();

            // Check integrity on startup
            // DISABLED FOR DEVELOPMENT - Re-enable for production builds
            // if (!VerifyIntegrity())
            // {
            //     Environment.FailFast("Application integrity check failed");
            // }
        }

        private static void AntiDebugLoop()
        {
            while (isProtectionActive)
            {
                try
                {
                    // Check for debuggers
                    if (IsDebuggerPresent())
                    {
                        Environment.FailFast("Debugger detected");
                    }

                    // Check for remote debugger
                    bool isRemoteDebuggerPresent = false;
                    CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref isRemoteDebuggerPresent);
                    if (isRemoteDebuggerPresent)
                    {
                        Environment.FailFast("Remote debugger detected");
                    }

                    // Check for common debugging/RE tools
                    var blacklistedProcesses = new[]
                    {
                        "dnspy", "ilspy", "dotpeek", "justdecompile", "reflector",
                        "ollydbg", "x64dbg", "x32dbg", "windbg", "ida", "ida64",
                        "cheatengine", "processhacker", "procmon", "procexp",
                        "fiddler", "wireshark", "charles"
                    };

                    var runningProcesses = Process.GetProcesses()
                        .Select(p => p.ProcessName.ToLower())
                        .ToList();

                    foreach (var blacklisted in blacklistedProcesses)
                    {
                        if (runningProcesses.Any(p => p.Contains(blacklisted)))
                        {
                            Environment.FailFast($"Suspicious process detected: {blacklisted}");
                        }
                    }

                    Thread.Sleep(500); // Check every 0.5 seconds for faster shutdown
                }
                catch
                {
                    // Silently continue if we can't check
                }
            }
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

                using (var pbkdf2 = new Rfc2898DeriveBytes(combined, Salt, 10000))
                {
                    return pbkdf2.GetBytes(32); // 256-bit key
                }
            }
            catch
            {
                // Fallback key
                using (var pbkdf2 = new Rfc2898DeriveBytes("VoiceLite-Default", Salt, 10000))
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
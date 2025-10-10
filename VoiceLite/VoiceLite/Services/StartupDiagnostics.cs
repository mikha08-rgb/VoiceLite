using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace VoiceLite.Services
{
    public static class StartupDiagnostics
    {
        public static async Task<DiagnosticResult> RunCompleteDiagnosticsAsync()
        {
            var result = new DiagnosticResult();

            try
            {
                // 1. Check Windows Defender / Antivirus exclusions
                result.AntivirusIssues = await CheckAntivirusIssuesAsync();

                // 2. Check file permissions
                result.PermissionIssues = CheckFilePermissions();

                // 3. Check if running from protected folder
                result.ProtectedFolderIssue = CheckProtectedFolder();

                // 4. Check available disk space
                result.DiskSpaceIssue = CheckDiskSpace();

                // 5. Check if files are blocked (downloaded from internet)
                result.BlockedFilesIssue = CheckBlockedFiles();

                // 6. Check for conflicting software
                result.ConflictingSoftware = CheckConflictingSoftware();

                // 7. Verify all critical files exist
                result.MissingFiles = CheckCriticalFiles();

                // 8. Check Windows version compatibility
                result.WindowsVersionIssue = CheckWindowsVersion();

                // 9. Check if temp folder is accessible
                result.TempFolderIssue = CheckTempFolderAccess();

                // 10. Check model file integrity
                result.CorruptModelFile = await CheckModelIntegrityAsync();

            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Startup diagnostics failed", ex);
            }

            return result;
        }

        private static async Task<bool> CheckAntivirusIssuesAsync()
        {
            try
            {
                // Check if Windows Defender might be blocking
                var whisperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper", "whisper.exe");
                if (!File.Exists(whisperPath))
                    return false;

                // Try to read the file - if blocked by AV, this might fail
                try
                {
                    using (var fs = File.OpenRead(whisperPath))
                    {
                        // Can read file
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    return true; // Likely blocked by antivirus
                }

                // Check if InputSimulator operations might be blocked
                // This is a common issue with antivirus software
                var processName = Process.GetCurrentProcess().ProcessName;

                // Check for common AV processes that might interfere
                var avProcesses = new[]
                {
                    "MsMpEng", // Windows Defender
                    "avp", // Kaspersky
                    "avgnt", // Avira
                    "mcshield", // McAfee
                    "avguard", // Avira
                    "bdagent", // Bitdefender
                    "AvastSvc", // Avast
                    "WRSA" // Webroot
                };

                var runningAV = Process.GetProcesses()
                    .Where(p => avProcesses.Contains(p.ProcessName, StringComparer.OrdinalIgnoreCase))
                    .Select(p => p.ProcessName)
                    .ToList();

                if (runningAV.Any())
                {
                    ErrorLogger.LogMessage($"Detected antivirus: {string.Join(", ", runningAV)}");

                    // Test if we can actually run whisper
                    var testResult = await TestWhisperWithTimeoutAsync(2000);
                    if (!testResult)
                    {
                        return true; // AV is likely blocking
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<bool> TestWhisperWithTimeoutAsync(int timeoutMs)
        {
            try
            {
                var whisperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper", "whisper.exe");
                if (!File.Exists(whisperPath))
                    return false;

                var tcs = new TaskCompletionSource<bool>();

                // CRITICAL FIX: Use 'using' to ensure Process is disposed and prevent resource leak
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = whisperPath,
                        Arguments = "--help",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };

                process.Exited += (s, e) => tcs.TrySetResult(process.ExitCode == 0 || process.ExitCode == 1);

                process.Start();

                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));

                if (completedTask != tcs.Task)
                {
                    // Timeout - kill the process
                    try { process.Kill(); } catch { }
                    return false;
                }

                return await tcs.Task;
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckFilePermissions()
        {
            try
            {
                // NOTE: We don't test BaseDirectory (Program Files) because it's expected to be read-only
                // VoiceLite writes all data to AppData and system temp, not Program Files

                // Check if we can write to temp folder
                var tempPath = Path.Combine(Path.GetTempPath(), $"voicelite_test_{Guid.NewGuid()}.tmp");
                File.WriteAllText(tempPath, "test");
                File.Delete(tempPath);

                // Check if we can write to AppData
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VoiceLite"
                );

                if (!Directory.Exists(appDataPath))
                {
                    Directory.CreateDirectory(appDataPath);
                }

                var appDataTest = Path.Combine(appDataPath, "test.tmp");
                File.WriteAllText(appDataTest, "test");
                File.Delete(appDataTest);

                return false; // No permission issues
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Permission check failed", ex);
                return true; // Permission issues detected
            }
        }

        private static bool CheckProtectedFolder()
        {
            // DISABLED: This check is no longer needed since we fixed the actual permission issues
            // The app now uses Windows temp folder for all temporary files
            // Program Files is the CORRECT location for Windows apps
            return false;
        }

        private static bool CheckDiskSpace()
        {
            try
            {
                var root = Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory);
                if (string.IsNullOrEmpty(root))
                {
                    return false;
                }

                var drive = new DriveInfo(root);
                var availableSpaceGB = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);

                // Need at least 500MB free for temp files and operation
                return availableSpaceGB < 0.5;
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckBlockedFiles()
        {
            try
            {
                var filesToCheck = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VoiceLite.exe"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper", "whisper.exe"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "H.InputSimulator.dll")
                };

                foreach (var file in filesToCheck.Where(File.Exists))
                {
                    // Check for Zone.Identifier stream (indicates downloaded file)
                    var zoneIdPath = file + ":Zone.Identifier";

                    try
                    {
                        if (File.Exists(zoneIdPath))
                        {
                            // File is marked as downloaded from internet
                            // Try to unblock it
                            UnblockFile(file);
                        }
                    }
                    catch
                    {
                        // Might not have permission to check/unblock
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static void UnblockFile(string fileName)
        {
            try
            {
                // Remove Zone.Identifier alternate data stream
                var zoneIdPath = fileName + ":Zone.Identifier";
                if (File.Exists(zoneIdPath))
                {
                    File.Delete(zoneIdPath);
                }
            }
            catch
            {
                // Might not have permission
            }
        }

        private static bool CheckConflictingSoftware()
        {
            try
            {
                // Check for software that might conflict with hotkeys or text injection
                var conflictingProcesses = new[]
                {
                    "AutoHotkey", // Might intercept hotkeys
                    "MacroRecorder",
                    "TeamViewer", // Sometimes interferes with input
                    "AnyDesk",
                    "LogMeIn",
                    "Synergy" // Keyboard/mouse sharing software
                };

                var running = Process.GetProcesses()
                    .Where(p => conflictingProcesses.Any(cp =>
                        p.ProcessName.IndexOf(cp, StringComparison.OrdinalIgnoreCase) >= 0))
                    .Select(p => p.ProcessName)
                    .ToList();

                if (running.Any())
                {
                    ErrorLogger.LogMessage($"Potentially conflicting software: {string.Join(", ", running)}");
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static List<string> CheckCriticalFiles()
        {
            var missing = new List<string>();
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            var criticalFiles = new[]
            {
                Path.Combine(baseDir, "NAudio.dll"),
                Path.Combine(baseDir, "NAudio.Core.dll"),
                Path.Combine(baseDir, "NAudio.WinMM.dll"),
                Path.Combine(baseDir, "NAudio.Wasapi.dll"),
                Path.Combine(baseDir, "H.InputSimulator.dll"),
                Path.Combine(baseDir, "Hardcodet.NotifyIcon.Wpf.dll"),
                Path.Combine(baseDir, "System.Text.Json.dll"),
                Path.Combine(baseDir, "whisper", "whisper.exe"),
                Path.Combine(baseDir, "whisper", "whisper.dll")
            };

            foreach (var file in criticalFiles)
            {
                if (!File.Exists(file))
                {
                    missing.Add(Path.GetFileName(file));
                    ErrorLogger.LogMessage($"Missing critical file: {file}");
                }
            }

            // Check for at least one model file
            var modelFiles = new[]
            {
                "ggml-tiny.bin",
                "ggml-base.bin",
                "ggml-small.bin",
                "ggml-medium.bin",
                "ggml-large-v3.bin"
            };

            var whisperDir = Path.Combine(baseDir, "whisper");
            var hasModel = modelFiles.Any(m =>
                File.Exists(Path.Combine(whisperDir, m)) ||
                File.Exists(Path.Combine(baseDir, m)));

            if (!hasModel)
            {
                missing.Add("AI Model (*.bin)");
            }

            return missing;
        }

        private static bool CheckWindowsVersion()
        {
            try
            {
                var os = Environment.OSVersion;

                // Windows 10 is version 10.0.xxxxx
                // Windows 11 is version 10.0.22000+ (build number distinguishes Win11)
                if (os.Platform == PlatformID.Win32NT)
                {
                    // Check for Windows 10 or later (version 10.0+)
                    if (os.Version.Major < 10)
                    {
                        ErrorLogger.LogMessage($"Unsupported Windows version: {os.Version}");
                        return true; // Issue detected
                    }

                    // Windows 11 detection: build 22000+
                    var isWindows11 = os.Version.Major == 10 && os.Version.Build >= 22000;
                    var windowsName = isWindows11 ? "Windows 11" : "Windows 10";
                    ErrorLogger.LogMessage($"Running on {windowsName} (Build {os.Version.Build})");
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckTempFolderAccess()
        {
            try
            {
                var tempPath = Path.GetTempPath();
                var testFile = Path.Combine(tempPath, $"voicelite_test_{Guid.NewGuid()}.tmp");

                File.WriteAllText(testFile, "test");
                File.Delete(testFile);

                return false; // No issues
            }
            catch
            {
                return true; // Can't write to temp folder
            }
        }

        private static async Task<bool> CheckModelIntegrityAsync()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var whisperDir = Path.Combine(baseDir, "whisper");

                // Expected model sizes (approximate)
                var expectedSizes = new Dictionary<string, long>
                {
                    { "ggml-tiny.bin", 75L * 1024 * 1024 },     // ~75MB
                    { "ggml-base.bin", 140L * 1024 * 1024 },    // ~140MB
                    { "ggml-small.bin", 460L * 1024 * 1024 },   // ~460MB
                    { "ggml-medium.bin", 1450L * 1024 * 1024 }, // ~1.45GB
                    { "ggml-large-v3.bin", 3000L * 1024 * 1024 } // ~3GB
                };

                foreach (var model in expectedSizes)
                {
                    var modelPath = Path.Combine(whisperDir, model.Key);
                    if (!File.Exists(modelPath))
                    {
                        modelPath = Path.Combine(baseDir, model.Key);
                        if (!File.Exists(modelPath))
                            continue;
                    }

                    var fileInfo = new FileInfo(modelPath);

                    // Check if file size is reasonable (within 20% of expected)
                    var minSize = model.Value * 0.8;
                    var maxSize = model.Value * 1.2;

                    if (fileInfo.Length < minSize || fileInfo.Length > maxSize)
                    {
                        ErrorLogger.LogMessage($"Model file {model.Key} has unexpected size: {fileInfo.Length} bytes");
                        return true; // Corrupt model detected
                    }

                    // Quick check: try to read first few bytes
                    try
                    {
                        using (var fs = File.OpenRead(modelPath))
                        {
                            var buffer = new byte[1024];
                            await fs.ReadAsync(buffer, 0, buffer.Length);

                            // Check for GGML magic number (simplified check)
                            if (buffer[0] == 0 && buffer[1] == 0 && buffer[2] == 0 && buffer[3] == 0)
                            {
                                ErrorLogger.LogMessage($"Model file {model.Key} appears to be corrupt (null bytes)");
                                return true;
                            }
                        }
                    }
                    catch
                    {
                        return true; // Can't read model file
                    }
                }

                return false; // No corruption detected
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> TryAutoFixIssuesAsync(DiagnosticResult result)
        {
            bool anyFixed = false;

            // Auto-unblock files
            if (result.BlockedFilesIssue)
            {
                ErrorLogger.LogMessage("Attempting to unblock files...");
                UnblockAllFiles();
                anyFixed = true;
            }

            // Create required directories
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoiceLite"
            );

            if (!Directory.Exists(appDataPath))
            {
                try
                {
                    Directory.CreateDirectory(appDataPath);
                    anyFixed = true;
                }
                catch { }
            }

            // Add Windows Defender exclusion (requires admin)
            if (result.AntivirusIssues && IsAdministrator())
            {
                try
                {
                    await AddWindowsDefenderExclusionAsync();
                    anyFixed = true;
                }
                catch { }
            }

            return anyFixed;
        }

        private static void UnblockAllFiles()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;

                // Unblock all DLLs and EXEs
                var files = Directory.GetFiles(baseDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                               f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

                foreach (var file in files)
                {
                    UnblockFile(file);
                }
            }
            catch { }
        }

        private static async Task AddWindowsDefenderExclusionAsync()
        {
            try
            {
                var appPath = AppDomain.CurrentDomain.BaseDirectory;

                // Add folder exclusion using PowerShell
                var script = $"Add-MpPreference -ExclusionPath '{appPath}'";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"{script}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                };

                // MEMORY FIX: Use using statement to ensure process disposal
                using var process = Process.Start(psi);
                if (process == null)
                {
                    ErrorLogger.LogMessage("Failed to start PowerShell for Defender exclusion");
                    return;
                }

                await Task.Run(() => process.WaitForExit());

                ErrorLogger.LogMessage("Added Windows Defender exclusion");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to add Defender exclusion", ex);
            }
        }

        private static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public class DiagnosticResult
    {
        public bool AntivirusIssues { get; set; }
        public bool PermissionIssues { get; set; }
        public bool ProtectedFolderIssue { get; set; }
        public bool DiskSpaceIssue { get; set; }
        public bool BlockedFilesIssue { get; set; }
        public bool ConflictingSoftware { get; set; }
        public List<string> MissingFiles { get; set; } = new List<string>();
        public bool WindowsVersionIssue { get; set; }
        public bool TempFolderIssue { get; set; }
        public bool CorruptModelFile { get; set; }

        public bool HasAnyIssues =>
            AntivirusIssues ||
            PermissionIssues ||
            ProtectedFolderIssue ||
            DiskSpaceIssue ||
            BlockedFilesIssue ||
            ConflictingSoftware ||
            MissingFiles.Any() ||
            WindowsVersionIssue ||
            TempFolderIssue ||
            CorruptModelFile;

        public string GetSummary()
        {
            var issues = new List<string>();

            if (AntivirusIssues)
                issues.Add("Antivirus may be blocking VoiceLite");

            if (PermissionIssues)
                issues.Add("File permission issues detected");

            if (ProtectedFolderIssue)
                issues.Add("Running from protected system folder");

            if (DiskSpaceIssue)
                issues.Add("Low disk space (need 500MB free)");

            if (BlockedFilesIssue)
                issues.Add("Files blocked by Windows security");

            if (ConflictingSoftware)
                issues.Add("Conflicting software detected");

            if (MissingFiles.Any())
                issues.Add($"Missing files: {string.Join(", ", MissingFiles)}");

            if (WindowsVersionIssue)
                issues.Add("Windows version not supported (need Windows 10 or Windows 11)");

            if (TempFolderIssue)
                issues.Add("Cannot access temp folder");

            if (CorruptModelFile)
                issues.Add("AI model file appears corrupted");

            return issues.Any() ? string.Join("\n", issues) : "No issues detected";
        }
    }
}
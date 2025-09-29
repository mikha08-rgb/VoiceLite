using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace VoiceLite.Services
{
    public static class DependencyChecker
    {
        private const string VCRedistUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe";
        private const string VCRedistRegistryKey = @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64";
        private const string VCRedist2022Key = @"SOFTWARE\Microsoft\DevDiv\VC\Servicing\14.0\RuntimeMinimum";

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool FreeLibrary(IntPtr hModule);

        public static async Task<DependencyCheckResult> CheckAndInstallDependenciesAsync()
        {
            var result = new DependencyCheckResult();

            // Check 1: Whisper.exe and model files
            result.WhisperExeFound = CheckWhisperExecutable();
            result.ModelFound = CheckWhisperModel();

            // Check 2: Visual C++ Runtime (required for whisper.exe)
            result.VCRuntimeInstalled = CheckVCRuntime();

            // Check 3: Test whisper.exe can actually run
            if (result.WhisperExeFound && result.ModelFound)
            {
                result.WhisperCanRun = await TestWhisperExecutableAsync();
            }

            // If VC Runtime is missing and whisper can't run, offer to install
            if (!result.VCRuntimeInstalled && !result.WhisperCanRun && result.WhisperExeFound)
            {
                var message = "VoiceLite requires Microsoft Visual C++ Runtime to work.\n\n" +
                             "Would you like to download and install it now?\n" +
                             "(This is a one-time installation from Microsoft)";

                var response = MessageBox.Show(message, "Required Component Missing",
                    MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (response == MessageBoxResult.Yes)
                {
                    result.VCRuntimeInstalled = await InstallVCRuntimeAsync();
                    if (result.VCRuntimeInstalled)
                    {
                        result.WhisperCanRun = await TestWhisperExecutableAsync();
                    }
                }
            }

            return result;
        }

        private static bool CheckWhisperExecutable()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var whisperPath = Path.Combine(baseDir, "whisper", "whisper.exe");

            if (File.Exists(whisperPath))
            {
                ErrorLogger.LogMessage($"Whisper.exe found at: {whisperPath}");
                return true;
            }

            // Try alternative location
            whisperPath = Path.Combine(baseDir, "whisper.exe");
            if (File.Exists(whisperPath))
            {
                ErrorLogger.LogMessage($"Whisper.exe found at: {whisperPath}");
                return true;
            }

            ErrorLogger.LogError("Whisper.exe not found in expected locations", null);
            return false;
        }

        private static bool CheckWhisperModel()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var whisperDir = Path.Combine(baseDir, "whisper");

            // Check for any model file
            var modelFiles = new[]
            {
                "ggml-tiny.bin",
                "ggml-base.bin",
                "ggml-small.bin",
                "ggml-medium.bin",
                "ggml-large-v3.bin"
            };

            foreach (var model in modelFiles)
            {
                var modelPath = Path.Combine(whisperDir, model);
                if (File.Exists(modelPath))
                {
                    ErrorLogger.LogMessage($"Model found: {modelPath}");
                    return true;
                }

                // Check root directory too
                modelPath = Path.Combine(baseDir, model);
                if (File.Exists(modelPath))
                {
                    ErrorLogger.LogMessage($"Model found: {modelPath}");
                    return true;
                }
            }

            ErrorLogger.LogError("No Whisper models found", null);
            return false;
        }

        private static bool CheckVCRuntime()
        {
            try
            {
                // Method 1: Try to load a VC Runtime DLL directly
                // Note: VCRUNTIME140_1.dll is optional and not always present
                var requiredDlls = new[] { "VCRUNTIME140.dll", "MSVCP140.dll" };

                foreach (var dll in requiredDlls)
                {
                    IntPtr handle = LoadLibrary(dll);
                    if (handle == IntPtr.Zero)
                    {
                        ErrorLogger.LogMessage($"VC Runtime DLL not found: {dll}");
                        return false;
                    }
                    FreeLibrary(handle);
                }

                // Method 2: Check registry for installed runtimes
                using (var key = Registry.LocalMachine.OpenSubKey(VCRedistRegistryKey))
                {
                    if (key != null)
                    {
                        var installed = key.GetValue("Installed");
                        if (installed != null && (int)installed == 1)
                        {
                            ErrorLogger.LogMessage("VC Runtime found via registry");
                            return true;
                        }
                    }
                }

                // Method 3: Alternative registry check for newer versions
                using (var key = Registry.LocalMachine.OpenSubKey(VCRedist2022Key))
                {
                    if (key != null)
                    {
                        ErrorLogger.LogMessage("VC Runtime 2022 found via registry");
                        return true;
                    }
                }

                ErrorLogger.LogMessage("All VC Runtime DLLs loaded successfully");
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to check VC Runtime", ex);
                return false;
            }
        }

        private static async Task<bool> TestWhisperExecutableAsync()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var whisperPath = Path.Combine(baseDir, "whisper", "whisper.exe");

                if (!File.Exists(whisperPath))
                {
                    whisperPath = Path.Combine(baseDir, "whisper.exe");
                    if (!File.Exists(whisperPath))
                        return false;
                }

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = whisperPath,
                        Arguments = "--help",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();

                // Wait up to 5 seconds for help to display
                var completed = await Task.Run(() => process.WaitForExit(5000));

                if (!completed)
                {
                    try { process.Kill(); } catch { }
                    ErrorLogger.LogError("Whisper.exe timed out during test", null);
                    return false;
                }

                if (process.ExitCode == 0)
                {
                    ErrorLogger.LogMessage("Whisper.exe test successful");
                    return true;
                }

                var error = await process.StandardError.ReadToEndAsync();

                // Check for specific VC Runtime error messages
                if (error.Contains("VCRUNTIME", StringComparison.OrdinalIgnoreCase) ||
                    error.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                    error.Contains("missing", StringComparison.OrdinalIgnoreCase))
                {
                    ErrorLogger.LogError($"Whisper.exe missing dependencies: {error}", null);
                    return false;
                }

                // Exit code non-zero but might still work
                ErrorLogger.LogMessage($"Whisper.exe test completed with exit code: {process.ExitCode}");
                return process.ExitCode <= 1; // Some help commands return 1
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to test whisper.exe", ex);

                // Check if it's a specific Windows error about missing DLLs
                if (ex.Message.Contains("specified module could not be found") ||
                    ex.Message.Contains("application has failed to start"))
                {
                    ErrorLogger.LogError("Whisper.exe cannot start - missing runtime dependencies", null);
                }

                return false;
            }
        }

        private static async Task<bool> InstallVCRuntimeAsync()
        {
            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), "vc_redist.x64.exe");

                // Show download progress
                var progressWindow = new Window
                {
                    Title = "Installing Dependencies",
                    Width = 400,
                    Height = 150,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Content = new System.Windows.Controls.StackPanel
                    {
                        Margin = new Thickness(20),
                        Children =
                        {
                            new System.Windows.Controls.TextBlock
                            {
                                Text = "Downloading Microsoft Visual C++ Runtime...",
                                FontSize = 14,
                                Margin = new Thickness(0, 10, 0, 10)
                            },
                            new System.Windows.Controls.ProgressBar
                            {
                                Height = 20,
                                IsIndeterminate = true
                            }
                        }
                    }
                };

                progressWindow.Show();

                try
                {
                    // Download the installer
                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromMinutes(5);
                        var response = await client.GetAsync(VCRedistUrl);
                        response.EnsureSuccessStatusCode();

                        var bytes = await response.Content.ReadAsByteArrayAsync();
                        await File.WriteAllBytesAsync(tempPath, bytes);
                    }

                    progressWindow.Close();

                    // Run the installer
                    var installProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = tempPath,
                            Arguments = "/quiet /norestart",
                            UseShellExecute = true,
                            Verb = "runas" // Request admin rights
                        }
                    };

                    installProcess.Start();
                    await Task.Run(() => installProcess.WaitForExit());

                    // Clean up
                    try { File.Delete(tempPath); } catch { }

                    if (installProcess.ExitCode == 0 || installProcess.ExitCode == 3010) // 3010 = restart required
                    {
                        if (installProcess.ExitCode == 3010)
                        {
                            MessageBox.Show(
                                "Visual C++ Runtime installed successfully.\n" +
                                "A system restart may be required for changes to take effect.",
                                "Installation Complete",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }

                        ErrorLogger.LogMessage("VC Runtime installed successfully");
                        return true;
                    }

                    ErrorLogger.LogError($"VC Runtime installation failed with exit code: {installProcess.ExitCode}", null);
                    return false;
                }
                finally
                {
                    progressWindow.Close();
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to install VC Runtime", ex);

                MessageBox.Show(
                    "Failed to install Visual C++ Runtime automatically.\n\n" +
                    "Please download and install it manually from:\n" +
                    "https://aka.ms/vs/17/release/vc_redist.x64.exe",
                    "Installation Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }
        }
    }

    public class DependencyCheckResult
    {
        public bool WhisperExeFound { get; set; }
        public bool ModelFound { get; set; }
        public bool VCRuntimeInstalled { get; set; }
        public bool WhisperCanRun { get; set; }

        public bool AllDependenciesMet => WhisperExeFound && ModelFound && VCRuntimeInstalled && WhisperCanRun;

        public string GetErrorMessage()
        {
            if (!WhisperExeFound)
                return "Whisper.exe not found. Please ensure the 'whisper' folder is included with the application.";

            if (!ModelFound)
                return "No Whisper AI models found. Please ensure at least one model file (e.g., ggml-small.bin) is in the 'whisper' folder.";

            if (!VCRuntimeInstalled)
                return "Microsoft Visual C++ Runtime is not installed. This is required for speech recognition to work.";

            if (!WhisperCanRun)
                return "Speech recognition engine cannot start. This usually means Visual C++ Runtime needs to be installed.";

            return "All dependencies are installed correctly.";
        }
    }
}
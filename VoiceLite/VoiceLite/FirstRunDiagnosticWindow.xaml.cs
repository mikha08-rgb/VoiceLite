using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using VoiceLite.Services;

namespace VoiceLite
{
    public partial class FirstRunDiagnosticWindow : Window
    {
        private readonly List<DiagnosticCheckItem> _checkItems = new();
        private bool _allChecksPassed = false;

        public FirstRunDiagnosticWindow()
        {
            InitializeComponent();
            Loaded += async (s, e) => await RunDiagnosticsAsync();
        }

        private async Task RunDiagnosticsAsync()
        {
            try
            {
                DiagnosticProgress.IsIndeterminate = true;
                StatusMessage.Text = "Running system diagnostics...";
                ChecklistPanel.Children.Clear();
                _checkItems.Clear();

                // Run all diagnostic checks
                await Task.Delay(500); // Brief delay for visual feedback

                await CheckVCRuntimeAsync();
                await CheckWhisperExecutableAsync();
                await CheckModelFilesAsync();
                await CheckAntivirusAsync();
                await CheckPermissionsAsync();
                await CheckDiskSpaceAsync();

                // Update final status
                DiagnosticProgress.IsIndeterminate = false;
                DiagnosticProgress.Value = 100;

                var criticalIssues = _checkItems.Count(c => !c.IsPassed && c.IsCritical);
                var warnings = _checkItems.Count(c => !c.IsPassed && !c.IsCritical);

                if (criticalIssues == 0)
                {
                    _allChecksPassed = true;
                    StatusMessage.Text = warnings > 0
                        ? $"✅ Ready to use! ({warnings} optional improvement{(warnings > 1 ? "s" : "")} available)"
                        : "✅ All checks passed! VoiceLite is ready to use.";
                    StatusMessage.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                    ((Border)StatusMessage.Parent).Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5E9"));
                    ((Border)StatusMessage.Parent).BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                    ContinueButton.IsEnabled = true;
                }
                else
                {
                    _allChecksPassed = false;
                    StatusMessage.Text = $"⚠️ {criticalIssues} critical issue{(criticalIssues > 1 ? "s" : "")} found. Please fix before using VoiceLite.";
                    StatusMessage.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D32F2F"));
                    ((Border)StatusMessage.Parent).Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEBEE"));
                    ((Border)StatusMessage.Parent).BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
                    ContinueButton.IsEnabled = false;
                }

                RerunButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("FirstRunDiagnosticWindow.RunDiagnosticsAsync", ex);
                StatusMessage.Text = "❌ Diagnostic check failed. See logs for details.";
            }
        }

        private async Task CheckVCRuntimeAsync()
        {
            await Task.Run(() =>
            {
                var item = new DiagnosticCheckItem
                {
                    Title = "Visual C++ Runtime",
                    IsCritical = true
                };

                try
                {
                    // Check registry for VC++ Runtime 2015-2022
                    var installed = false;
                    using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"))
                    {
                        if (key != null)
                        {
                            var value = key.GetValue("Installed");
                            installed = value != null && Convert.ToInt32(value) == 1;
                        }
                    }

                    if (!installed)
                    {
                        // Try alternative location
                        using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"))
                        {
                            if (key != null)
                            {
                                var value = key.GetValue("Installed");
                                installed = value != null && Convert.ToInt32(value) == 1;
                            }
                        }
                    }

                    if (installed)
                    {
                        item.IsPassed = true;
                        item.Description = "Microsoft Visual C++ Runtime 2015-2022 is installed";
                    }
                    else
                    {
                        item.IsPassed = false;
                        item.Description = "Microsoft Visual C++ Runtime 2015-2022 is NOT installed. VoiceLite cannot run without it.";
                        item.FixAction = "Download VC++ Runtime";
                        item.OnFix = () =>
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "https://aka.ms/vs/17/release/vc_redist.x64.exe",
                                UseShellExecute = true
                            });
                            MessageBox.Show(
                                "After installing VC++ Runtime, please RESTART your computer.\n\nThen re-run VoiceLite.",
                                "Restart Required",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        };
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("CheckVCRuntimeAsync", ex);
                    item.IsPassed = false;
                    item.Description = "Failed to verify VC++ Runtime installation";
                }

                // CRIT-001 FIX: Replace blocking Dispatcher.Invoke with async InvokeAsync to prevent deadlock
                Dispatcher.InvokeAsync(() => AddCheckItem(item));
            });
        }

        private async Task CheckWhisperExecutableAsync()
        {
            await Task.Run(() =>
            {
                var item = new DiagnosticCheckItem
                {
                    Title = "Whisper AI Engine",
                    IsCritical = true
                };

                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var whisperPath = System.IO.Path.Combine(baseDir, "whisper", "whisper.exe");

                    if (File.Exists(whisperPath))
                    {
                        // Try to run --help to verify it works
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = whisperPath,
                            Arguments = "--help",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };

                        using (var process = Process.Start(startInfo))
                        {
                            if (process != null && process.WaitForExit(5000))
                            {
                                if (process.ExitCode == 0 || process.ExitCode == 1) // Both are valid for --help
                                {
                                    item.IsPassed = true;
                                    var fileSize = new FileInfo(whisperPath).Length;
                                    item.Description = $"Whisper.exe verified ({fileSize / 1024} KB)";
                                }
                                else
                                {
                                    item.IsPassed = false;
                                    item.Description = $"Whisper.exe exists but failed to run (exit code: {process.ExitCode}). Missing DLLs?";
                                }
                            }
                            else
                            {
                                item.IsPassed = false;
                                item.Description = "Whisper.exe exists but timed out. May be blocked by antivirus.";
                            }
                        }
                    }
                    else
                    {
                        item.IsPassed = false;
                        item.Description = "Whisper.exe not found. Reinstall VoiceLite from official source.";
                        item.FixAction = "Download VoiceLite";
                        item.OnFix = () =>
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "https://github.com/mikha08-rgb/VoiceLite/releases/latest",
                                UseShellExecute = true
                            });
                        };
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("CheckWhisperExecutableAsync", ex);
                    item.IsPassed = false;
                    item.Description = $"Failed to verify whisper.exe: {ex.Message}";
                }

                // CRIT-001 FIX: Replace blocking Dispatcher.Invoke with async InvokeAsync to prevent deadlock
                Dispatcher.InvokeAsync(() => AddCheckItem(item));
            });
        }

        private async Task CheckModelFilesAsync()
        {
            await Task.Run(() =>
            {
                var item = new DiagnosticCheckItem
                {
                    Title = "AI Models",
                    IsCritical = true
                };

                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var whisperDir = System.IO.Path.Combine(baseDir, "whisper");

                    var tinyModel = System.IO.Path.Combine(whisperDir, "ggml-tiny.bin");
                    var smallModel = System.IO.Path.Combine(whisperDir, "ggml-small.bin");

                    var modelsFound = new List<string>();

                    if (File.Exists(tinyModel))
                    {
                        var size = new FileInfo(tinyModel).Length / (1024 * 1024);
                        if (size >= 60 && size <= 90) // 60-90 MB range
                            modelsFound.Add($"Lite ({size} MB)");
                    }

                    if (File.Exists(smallModel))
                    {
                        var size = new FileInfo(smallModel).Length / (1024 * 1024);
                        if (size >= 400 && size <= 550) // 400-550 MB range
                            modelsFound.Add($"Pro ({size} MB)");
                    }

                    if (modelsFound.Count > 0)
                    {
                        item.IsPassed = true;
                        item.Description = $"Found {modelsFound.Count} model{(modelsFound.Count > 1 ? "s" : "")}: {string.Join(", ", modelsFound)}";
                    }
                    else
                    {
                        item.IsPassed = false;
                        item.Description = "No valid AI models found. Reinstall VoiceLite or download models from Settings.";
                        item.FixAction = "Help";
                        item.OnFix = () =>
                        {
                            MessageBox.Show(
                                "To fix missing models:\n\n" +
                                "1. Uninstall VoiceLite\n" +
                                "2. Download installer from GitHub releases\n" +
                                "3. Verify SHA256 hash (see release notes)\n" +
                                "4. Reinstall\n\n" +
                                "If issue persists, download may be corrupted.",
                                "Missing AI Models",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        };
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("CheckModelFilesAsync", ex);
                    item.IsPassed = false;
                    item.Description = "Failed to verify model files";
                }

                // CRIT-001 FIX: Replace blocking Dispatcher.Invoke with async InvokeAsync to prevent deadlock
                Dispatcher.InvokeAsync(() => AddCheckItem(item));
            });
        }

        private async Task CheckAntivirusAsync()
        {
            await Task.Run(() =>
            {
                var item = new DiagnosticCheckItem
                {
                    Title = "Antivirus Status",
                    IsCritical = false // Not critical, but recommended
                };

                try
                {
                    // Check if Windows Defender is running
                    var defenderRunning = Process.GetProcesses().Any(p => p.ProcessName.Equals("MsMpEng", StringComparison.OrdinalIgnoreCase));

                    if (defenderRunning)
                    {
                        // Check if VoiceLite is excluded (best-effort)
                        var installPath = AppDomain.CurrentDomain.BaseDirectory;
                        var scriptPath = System.IO.Path.Combine(installPath, "Add-VoiceLite-Exclusion.ps1");

                        if (File.Exists(scriptPath))
                        {
                            item.IsPassed = false; // We can't reliably check exclusions, so mark as warning
                            item.Description = "Windows Defender detected. Run the exclusion script to prevent blocking.";
                            item.FixAction = "Add Exclusions";
                            item.OnFix = () =>
                            {
                                try
                                {
                                    var startInfo = new ProcessStartInfo
                                    {
                                        FileName = "powershell.exe",
                                        Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\"",
                                        UseShellExecute = true,
                                        Verb = "runas" // Request admin
                                    };
                                    Process.Start(startInfo);
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show(
                                        $"Failed to run exclusion script:\n{ex.Message}\n\nPlease run the desktop shortcut 'Fix Antivirus Issues' manually.",
                                        "Error",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Error);
                                }
                            };
                        }
                        else
                        {
                            item.IsPassed = true;
                            item.Description = "Windows Defender detected (exclusion script not found)";
                        }
                    }
                    else
                    {
                        item.IsPassed = true;
                        item.Description = "No antivirus interference detected";
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("CheckAntivirusAsync", ex);
                    item.IsPassed = true; // Don't block on this check
                    item.Description = "Could not check antivirus status";
                }

                // CRIT-001 FIX: Replace blocking Dispatcher.Invoke with async InvokeAsync to prevent deadlock
                Dispatcher.InvokeAsync(() => AddCheckItem(item));
            });
        }

        private async Task CheckPermissionsAsync()
        {
            await Task.Run(() =>
            {
                var item = new DiagnosticCheckItem
                {
                    Title = "File Permissions",
                    IsCritical = false
                };

                try
                {
                    var appDataDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VoiceLite");
                    var logsDir = System.IO.Path.Combine(appDataDir, "logs");

                    // Try to create AppData directory
                    if (!Directory.Exists(appDataDir))
                    {
                        Directory.CreateDirectory(appDataDir);
                    }

                    // Try to write a test file
                    var testFile = System.IO.Path.Combine(appDataDir, "test.tmp");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);

                    item.IsPassed = true;
                    item.Description = $"Write permissions verified for {appDataDir}";
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("CheckPermissionsAsync", ex);
                    item.IsPassed = false;
                    item.Description = "Cannot write to AppData folder. Check user permissions.";
                    item.FixAction = "Help";
                    item.OnFix = () =>
                    {
                        MessageBox.Show(
                            "VoiceLite cannot write to your AppData folder.\n\n" +
                            "This may be caused by:\n" +
                            "• Restricted user account permissions\n" +
                            "• Antivirus blocking file access\n" +
                            "• Disk errors\n\n" +
                            "Try running VoiceLite as administrator, or contact your IT administrator.",
                            "Permission Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    };
                }

                // CRIT-001 FIX: Replace blocking Dispatcher.Invoke with async InvokeAsync to prevent deadlock
                Dispatcher.InvokeAsync(() => AddCheckItem(item));
            });
        }

        private async Task CheckDiskSpaceAsync()
        {
            await Task.Run(() =>
            {
                var item = new DiagnosticCheckItem
                {
                    Title = "Disk Space",
                    IsCritical = false
                };

                try
                {
                    var installDrive = System.IO.Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory);
                    if (installDrive != null)
                    {
                        var driveInfo = new DriveInfo(installDrive);
                        var freeSpaceGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);

                        if (freeSpaceGB >= 1.0) // At least 1 GB free
                        {
                            item.IsPassed = true;
                            item.Description = $"{freeSpaceGB:F1} GB free on {installDrive}";
                        }
                        else
                        {
                            item.IsPassed = false;
                            item.Description = $"Low disk space: Only {freeSpaceGB:F1} GB free on {installDrive}. At least 1 GB recommended.";
                            item.FixAction = "Help";
                            item.OnFix = () =>
                            {
                                Process.Start("cleanmgr.exe");
                            };
                        }
                    }
                    else
                    {
                        item.IsPassed = true;
                        item.Description = "Could not check disk space";
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("CheckDiskSpaceAsync", ex);
                    item.IsPassed = true; // Don't block on this check
                    item.Description = "Could not check disk space";
                }

                // CRIT-001 FIX: Replace blocking Dispatcher.Invoke with async InvokeAsync to prevent deadlock
                Dispatcher.InvokeAsync(() => AddCheckItem(item));
            });
        }

        private void AddCheckItem(DiagnosticCheckItem item)
        {
            _checkItems.Add(item);

            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Status icon
            var iconCanvas = new Canvas { Width = 24, Height = 24 };
            var iconPath = new System.Windows.Shapes.Path
            {
                Fill = item.IsPassed
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"))
                    : (item.IsCritical
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800"))),
                Data = Geometry.Parse(item.IsPassed
                    ? "M12,2C6.48,2 2,6.48 2,12C2,17.52 6.48,22 12,22C17.52,22 22,17.52 22,12C22,6.48 17.52,2 12,2M10,17L5,12L6.41,10.59L10,14.17L17.59,6.58L19,8L10,17Z" // Checkmark
                    : "M13,13H11V7H13M13,17H11V15H13M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z") // Warning
            };
            iconCanvas.Children.Add(iconPath);

            var iconViewbox = new Viewbox
            {
                Width = 24,
                Height = 24,
                Margin = new Thickness(0, 0, 10, 0),
                Child = iconCanvas
            };
            Grid.SetColumn(iconViewbox, 0);
            grid.Children.Add(iconViewbox);

            // Content
            var contentPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var titleBlock = new TextBlock
            {
                Text = item.Title,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#212121"))
            };
            var descBlock = new TextBlock
            {
                Text = item.Description,
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#757575")),
                Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            contentPanel.Children.Add(titleBlock);
            contentPanel.Children.Add(descBlock);
            Grid.SetColumn(contentPanel, 1);
            grid.Children.Add(contentPanel);

            // Fix button
            if (!item.IsPassed && item.FixAction != null && item.OnFix != null)
            {
                var fixButton = new Button
                {
                    Content = item.FixAction,
                    Style = (Style)FindResource("WarningButtonStyle"),
                    VerticalAlignment = VerticalAlignment.Center,
                    MinWidth = 90
                };
                fixButton.Click += (s, e) => item.OnFix?.Invoke();
                Grid.SetColumn(fixButton, 2);
                grid.Children.Add(fixButton);
            }

            border.Child = grid;
            ChecklistPanel.Children.Add(border);
        }

        private async void RerunButton_Click(object sender, RoutedEventArgs e)
        {
            // CRITICAL FIX: Wrap entire async void method in try-catch to prevent crashes
            try
            {
                RerunButton.IsEnabled = false;
                ContinueButton.IsEnabled = false;
                await RunDiagnosticsAsync();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Diagnostic rerun failed", ex);
                RerunButton.IsEnabled = true;
                MessageBox.Show($"Diagnostic check failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            if (_allChecksPassed || MessageBox.Show(
                "Some checks did not pass. Continue anyway?",
                "Continue?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                DialogResult = true;
                Close();
            }
        }

        private class DiagnosticCheckItem
        {
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
            public bool IsPassed { get; set; }
            public bool IsCritical { get; set; }
            public string? FixAction { get; set; }
            public Action? OnFix { get; set; }
        }
    }
}

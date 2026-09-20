using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// v2.5.2 review #16: RunGateWithoutLastWindowShutdown switches the app to
    /// OnExplicitShutdown and had no exception handling. A throw from the first-launch dialog
    /// left OnStartup, was swallowed by OnDispatcherUnhandledException ("The application will
    /// continue"), and the process lived on with no window, no tray icon and an explicit-only
    /// shutdown mode - holding the single-instance mutex, so every later launch exited silently
    /// until the user killed it in Task Manager.
    ///
    /// App.TryRunStartupGate reports the exception to OnStartup, which shuts down with a message.
    /// Same child "pwsh -STA" approach as StartupFirstLaunchGateTests (one WPF Application per
    /// process); only plain off-screen Windows are created, VoiceLite itself is never started,
    /// and the helper under test does not log (the log path is the real user log).
    /// </summary>
    public class StartupGateThrowsTests
    {
        private const string Prelude = @"
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework
$asm = [Reflection.Assembly]::LoadFrom('{ASSEMBLY}')
$appType = $asm.GetType('VoiceLite.App', $true)
$flags = [Reflection.BindingFlags]'NonPublic,Static'
$logLevel = $asm.GetType('VoiceLite.Services.ErrorLogger', $true).GetProperty('MinimumLogLevel')
$logLevel.SetValue($null, [Enum]::Parse($logLevel.PropertyType, 'Error'))
$app = [Windows.Application]::new()
$try = $appType.GetMethod('TryRunStartupGate', $flags)
if ($null -eq $try) { throw 'App.TryRunStartupGate not found' }
";

        [Fact]
        public void GateThrows_ReturnsFalse_AndHandsTheExceptionToTheCaller()
        {
            var output = RunInChildPwsh(Prelude + @"
$gate = [Func[bool]]{ throw [InvalidOperationException]::new('model dialog blew up') }
$a = @($app, $gate, $null)
$ok = $try.Invoke($null, $a)
""GATE=$ok""
""HASERROR=$($null -ne $a[2])""
""MESSAGE=$($a[2].Message) $($a[2].InnerException.Message)""
");
            Assert.Contains("GATE=False", output);
            Assert.Contains("HASERROR=True", output);
            Assert.Contains("model dialog blew up", output);
        }

        [Fact]
        public void GateDeclined_ReturnsFalse_WithoutAnError()
        {
            var output = RunInChildPwsh(Prelude + @"
$gate = [Func[bool]]{ $false }
$a = @($app, $gate, $null)
$ok = $try.Invoke($null, $a)
""GATE=$ok""
""HASERROR=$($null -ne $a[2])""
");
            Assert.Contains("GATE=False", output);
            Assert.Contains("HASERROR=False", output);
        }

        [Fact]
        public void GatePassed_ReturnsTrue_AndRestoresLastWindowClose()
        {
            var output = RunInChildPwsh(Prelude + @"
$gate = [Func[bool]]{ $true }
$a = @($app, $gate, $null)
$ok = $try.Invoke($null, $a)
""GATE=$ok""
""HASERROR=$($null -ne $a[2])""
""MODE=$($app.ShutdownMode)""
");
            Assert.Contains("GATE=True", output);
            Assert.Contains("HASERROR=False", output);
            Assert.Contains("MODE=OnLastWindowClose", output);
        }

        [Fact]
        public void Wiring_OnStartupShutsDown_WhenTheGateThrew()
        {
            var code = SettingsWindowHotkeyTests.ReadAppSource("App.xaml.cs");
            if (code == null) return; // test binaries copied away from the source tree

            Assert.True(code.Contains("TryRunStartupGate(this, ShowFirstLaunchModelDownload, out var gateError)"),
                "OnStartup must run the first-launch gate through TryRunStartupGate");
            // Restoring the shutdown mode is not enough: with zero windows OnLastWindowClose never fires.
            Assert.True(Regex.IsMatch(code, @"if \(gateError != null\)\s*\{[^}]*Shutdown\(1\);\s*return;"),
                "a gate that threw must end the process (releasing the single-instance mutex), not leave a windowless app running");
        }

        private static string RunInChildPwsh(string script)
        {
            script = script.Replace("{ASSEMBLY}", typeof(App).Assembly.Location.Replace("'", "''"));
            var psi = new ProcessStartInfo("pwsh")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-STA", "-EncodedCommand",
                         Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) })
                psi.ArgumentList.Add(arg);

            Process process;
            try
            {
                process = Process.Start(psi)!;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new InvalidOperationException(
                    "This test needs PowerShell 7 (pwsh) on PATH to run the WPF scenario in a separate process.", ex);
            }

            using (process)
            {
                var stderr = process.StandardError.ReadToEndAsync();
                var stdout = process.StandardOutput.ReadToEndAsync();
                if (!process.WaitForExit(60_000))
                {
                    try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
                    Assert.Fail("Child pwsh did not finish within 60s");
                }
                var output = stdout.Result;
                Assert.True(process.ExitCode == 0,
                    $"Child pwsh exited with {process.ExitCode}.\nstdout:\n{output}\nstderr:\n{stderr.Result}");
                return output;
            }
        }
    }
}

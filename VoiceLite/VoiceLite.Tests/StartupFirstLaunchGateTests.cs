using System;
using System.Diagnostics;
using System.Text;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// Fresh-install bug: the model-download dialog is the only WPF Window, so closing it
    /// under the default ShutdownMode (OnLastWindowClose) starts application shutdown and
    /// the following MainWindow.Show() is a silent no-op - the app exits right after the
    /// 640MB download.
    ///
    /// Reproducing that in-process would set WPF's static Application.IsShuttingDown for the
    /// whole test run (one Application per process), so the scenario runs in a child
    /// "pwsh -STA" that loads VoiceLite.dll and calls the real App helpers by reflection.
    /// Only plain off-screen Windows are created; VoiceLite itself is never started.
    /// </summary>
    public class StartupFirstLaunchGateTests
    {
        private const string Prelude = @"
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework
$asm = [Reflection.Assembly]::LoadFrom('{ASSEMBLY}')
$appType = $asm.GetType('VoiceLite.App', $true)
$flags = [Reflection.BindingFlags]'NonPublic,Static'
function New-TestWindow {
    $w = [Windows.Window]::new()
    $w.WindowStyle = 'None'; $w.ShowInTaskbar = $false; $w.ShowActivated = $false
    $w.Width = 1; $w.Height = 1; $w.Left = -32000; $w.Top = -32000
    $w
}
";
        private const string WithApplication = Prelude + "$app = [Windows.Application]::new()\n";

        [Fact]
        public void GatePassed_MainWindowStillShows_AndShutdownModeIsRestored()
        {
            var output = RunInChildPwsh(WithApplication + @"
$run = $appType.GetMethod('RunGateWithoutLastWindowShutdown', $flags)
$gate = [Func[bool]]{ $d = New-TestWindow; $d.Show(); $d.Close(); $true }
$ok = $run.Invoke($null, @($app, $gate))
$main = New-TestWindow
$main.Show()
""GATE=$ok""
""MODE=$($app.ShutdownMode)""
""VISIBLE=$($main.IsVisible)""
");
            Assert.Contains("GATE=True", output);
            Assert.Contains("VISIBLE=True", output);
            Assert.Contains("MODE=OnLastWindowClose", output);
        }

        [Fact]
        public void GateDeclined_ReturnsFalse_AndLeavesExplicitShutdown()
        {
            // Cancel path: OnStartup shows a MessageBox and calls Shutdown(0) itself; the
            // mode is restored on the success path only.
            var output = RunInChildPwsh(WithApplication + @"
$run = $appType.GetMethod('RunGateWithoutLastWindowShutdown', $flags)
$gate = [Func[bool]]{ $d = New-TestWindow; $d.Show(); $d.Close(); $false }
$ok = $run.Invoke($null, @($app, $gate))
""GATE=$ok""
""MODE=$($app.ShutdownMode)""
");
            Assert.Contains("GATE=False", output);
            Assert.Contains("MODE=OnExplicitShutdown", output);
        }

        [Fact]
        public void SurfaceWindow_OnClosedWindow_DoesNotThrow()
        {
            // Defensive: Window.Show() on a closed window throws InvalidOperationException,
            // which the dispatcher handler would turn into an "An error occurred" dialog.
            // No Application here: closing its only window would start shutdown and make
            // the final Show() a no-op for an unrelated reason.
            var output = RunInChildPwsh(Prelude + @"
$surface = $appType.GetMethod('SurfaceWindow', $flags)
$closed = New-TestWindow; $closed.Show(); $closed.Close()
$surface.Invoke($null, @($closed))
$surface.Invoke($null, @($null))
$open = New-TestWindow
$surface.Invoke($null, @($open))
""SURFACED=$($open.IsVisible)""
");
            Assert.Contains("SURFACED=True", output);
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

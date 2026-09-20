using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
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
# This child loads its own copy of VoiceLite.dll, so the test assembly's log redirect
# (TestLogRedirect) does not reach it: point ErrorLogger away from the real
# %LOCALAPPDATA%\VoiceLite\logs\voicelite.log BEFORE the assembly is loaded...
[AppContext]::SetData('{LOGKEY}', '{LOGDIR}')
$asm = [Reflection.Assembly]::LoadFrom('{ASSEMBLY}')
$appType = $asm.GetType('VoiceLite.App', $true)
$flags = [Reflection.BindingFlags]'NonPublic,Static'
# ...and, second layer, raise the level so the Warning paths exercised here return before
# any file access.
$logLevel = $asm.GetType('VoiceLite.Services.ErrorLogger', $true).GetProperty('MinimumLogLevel')
$logLevel.SetValue($null, [Enum]::Parse($logLevel.PropertyType, 'Error'))
$logPath = $asm.GetType('VoiceLite.Services.ErrorLogger', $true).GetField('LogPath', $flags).GetValue($null)
if (-not $logPath.StartsWith('{LOGDIR}', [StringComparison]::OrdinalIgnoreCase)) { throw ""ErrorLogger is not redirected ($logPath); refusing to risk the real voicelite.log"" }
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
if ($logLevel.GetValue($null) -ne 'Error') { throw 'ErrorLogger is not muted; refusing to write to the real voicelite.log' }
""LOGLEVEL=$($logLevel.GetValue($null))""
$closed = New-TestWindow; $closed.Show(); $closed.Close()
$surface.Invoke($null, @($closed))
$surface.Invoke($null, @($null))
$open = New-TestWindow
$surface.Invoke($null, @($open))
""SURFACED=$($open.IsVisible)""
");
            // The closed-window path logs a Warning; it must not land in the real user log.
            Assert.Contains("LOGLEVEL=Error", output);
            Assert.Contains("SURFACED=True", output);
        }

        // ---- call-site tripwire --------------------------------------------------------
        // The tests above call the helper by reflection with their own gate, so they stay
        // green if App.OnStartup stops USING it (e.g. a merge turns the call site back into
        // "if (!ShowFirstLaunchModelDownload())") - and a fresh install exits silently right
        // after the 640MB download again. Startup can't be exercised without launching the
        // app, so the call site is pinned at source level.

        [Fact]
        public void OnStartup_RoutesTheFirstLaunchDialog_ThroughRunGateWithoutLastWindowShutdown() =>
            AssertFirstLaunchGateIsRoutedThroughHelper(AppSourceFile());

        [Fact]
        public void GateCallSiteTripwire_FailsLoudly_WhenSourceIsMissing() =>
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
                AssertFirstLaunchGateIsRoutedThroughHelper(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "App.xaml.cs")));

        [Theory]
        // The original bug: the dialog is shown directly, the helper is left unused.
        [InlineData("if (!ShowFirstLaunchModelDownload())")]
        // Helper still called, but the dialog is also shown directly somewhere else.
        [InlineData("ShowFirstLaunchModelDownload(); {WIRED}")]
        // Helper called with some other gate.
        [InlineData("if (!{HELPER}(this, () => true{REST}))")]
        public void GateCallSiteTripwire_FailsLoudly_WhenTheCallSiteBypassesTheHelper(string revertedCallSite)
        {
            // Derived from whichever accepted shape App.OnStartup really uses, so these stay
            // mutations of the REAL call site when it moves between the two.
            var real = File.ReadAllText(AppSourceFile());
            var wired = Regex.Match(real,
                @"if \(!(?<helper>RunGateWithoutLastWindowShutdown|TryRunStartupGate)\(this, ShowFirstLaunchModelDownload(?<rest>[^)]*)\)\)");
            Assert.True(wired.Success,
                "the first-launch gate call site in App.xaml.cs has a shape this theory cannot mutate; update it");

            AssertMutationTripsTheWire(real.Replace(wired.Value, revertedCallSite
                .Replace("{WIRED}", wired.Value)
                .Replace("{HELPER}", wired.Groups["helper"].Value)
                .Replace("{REST}", wired.Groups["rest"].Value)));
        }

        [Fact]
        public void GateCallSiteTripwire_FailsLoudly_WhenTheWrapperStopsCallingTheHelper()
        {
            // Second hop: OnStartup goes through TryRunStartupGate, which must itself run the
            // gate inside RunGateWithoutLastWindowShutdown. Calling gate() directly there is the
            // original bug again, one level down.
            const string hop = "return RunGateWithoutLastWindowShutdown(app, gate);";
            var real = File.ReadAllText(AppSourceFile());
            Assert.Contains("if (!TryRunStartupGate(this, ShowFirstLaunchModelDownload,", real);
            Assert.Contains(hop, real);

            AssertMutationTripsTheWire(real.Replace(hop, "return gate();"));
        }

        private static void AssertMutationTripsTheWire(string mutatedSource)
        {
            var dir = Path.Combine(Path.GetTempPath(), "VoiceLite-GateTripwire-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var mutated = Path.Combine(dir, "App.xaml.cs");
                File.WriteAllText(mutated, mutatedSource);

                Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertFirstLaunchGateIsRoutedThroughHelper(mutated));
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
            }
        }

        private static void AssertFirstLaunchGateIsRoutedThroughHelper(string source)
        {
            // Resolved from the compile-time path of this file; a copied test binary or a
            // PathMap/deterministic build must fail here rather than pass unchecked.
            Assert.True(File.Exists(source),
                $"First-launch gate tripwire could not find App.xaml.cs at '{source}'; the gate call site is unverified.");

            var text = File.ReadAllText(source);
            // Two accepted shapes: the helper directly, or the TryRunStartupGate wrapper (which
            // also catches a throwing gate) - and then the wrapper's own hop is pinned as well.
            const string direct = "RunGateWithoutLastWindowShutdown(this, ShowFirstLaunchModelDownload)";
            const string wrapped = "TryRunStartupGate(this, ShowFirstLaunchModelDownload,";
            const string wrapperHop = "RunGateWithoutLastWindowShutdown(app, gate)";
            int gate = text.IndexOf(direct, StringComparison.Ordinal);
            if (gate < 0)
            {
                gate = text.IndexOf(wrapped, StringComparison.Ordinal);
                Assert.True(gate >= 0,
                    $"App.xaml.cs contains neither '{direct}' nor '{wrapped}'. Showing the model-download dialog any other way " +
                    "closes the app's only window under OnLastWindowClose: a fresh install exits right after the download.");

                // Body = from the wrapper's declaration to the next member (doc comment or modifier).
                var wrapper = Regex.Match(text,
                    @"\bstatic\s+bool\s+TryRunStartupGate\s*\((?<body>.*?)(?=\n\s*(?:///|internal\s|private\s|public\s|protected\s)|\z)",
                    RegexOptions.Singleline);
                Assert.True(wrapper.Success,
                    "TryRunStartupGate is called but its declaration was not found in App.xaml.cs; update this tripwire");
                Assert.True(wrapper.Groups["body"].Value.Contains(wrapperHop, StringComparison.Ordinal),
                    $"TryRunStartupGate no longer calls '{wrapperHop}': the first-launch dialog runs under OnLastWindowClose " +
                    "again and a fresh install exits right after the download.");
            }

            // The pinned call passes the method GROUP (no parenthesis), so the declaration is the
            // only "ShowFirstLaunchModelDownload(" allowed; a second one is a direct call.
            int invocations = Regex.Matches(text, @"\bShowFirstLaunchModelDownload\s*\(").Count;
            Assert.True(invocations == 1,
                $"Expected only the declaration of ShowFirstLaunchModelDownload( in App.xaml.cs, found {invocations} occurrences: " +
                "the dialog is being shown without RunGateWithoutLastWindowShutdown.");

            // The helper restores OnLastWindowClose; that has to happen before MainWindow exists.
            int mainWindow = text.IndexOf("new MainWindow()", StringComparison.Ordinal);
            Assert.True(mainWindow >= 0, "'new MainWindow()' not found in App.xaml.cs; update this tripwire");
            Assert.True(gate < mainWindow, "the first-launch gate must run before MainWindow is created");
        }

        private static string AppSourceFile([CallerFilePath] string thisFile = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "VoiceLite", "App.xaml.cs"));

        private static string RunInChildPwsh(string script)
        {
            script = script
                .Replace("{ASSEMBLY}", typeof(App).Assembly.Location.Replace("'", "''"))
                .Replace("{LOGKEY}", VoiceLite.Services.ErrorLogger.LogDirectoryOverrideKey)
                .Replace("{LOGDIR}", TestUtilities.TestLogRedirect.LogDirectory.Replace("'", "''"));
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

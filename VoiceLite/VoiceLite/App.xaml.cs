using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VoiceLite.Controls;
using VoiceLite.Services;

namespace VoiceLite
{
    public partial class App : Application
    {
        // Single-instance guard. Held for the process lifetime; the installer's
        // CheckForMutexes (VoiceLiteSetup.iss) checks this EXACT name to block
        // upgrades over a running app. Session-local (no Global\ prefix) to match
        // the per-user install; Inno's CheckForMutexes probes both namespaces.
        private static Mutex? singleInstanceMutex;

        // A second launch sets this; the running instance listens and brings its window
        // forward (the window is hidden while minimized to the tray, so a plain Activate
        // from another process cannot reach it).
        private const string ShowWindowSignalName = "VoiceLite_ShowWindow";
        private static EventWaitHandle? showWindowSignal;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Must be acquired BEFORE any startup gate: it also prevents a second
            // instance from double-injecting text into the foreground app.
            singleInstanceMutex = new Mutex(initiallyOwned: true, "VoiceLite_SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                // We don't own the mutex — just drop the handle, never ReleaseMutex it.
                singleInstanceMutex.Dispose();
                singleInstanceMutex = null;
                if (!SignalRunningInstanceToShow())
                {
                    // Running instance predates the show-window signal.
                    MessageBox.Show(
                        "VoiceLite is already running — check the system tray.",
                        "VoiceLite", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                Shutdown(0);
                return;
            }

            // Builds before v2.2.0 never created the mutex, so an old VoiceLite left running
            // in the tray (typical right after an upgrade) slips past the check above; the new
            // instance then fails to register the hotkey ("in use by another application")
            // while the old one keeps swallowing the keypresses. Catch it by process name.
            //
            // The listener must exist BEFORE that check: a fast second launch of this build
            // already shows up in the process list, and without the event it would pop
            // "already running" while we report it as an "older copy".
            ListenForShowWindowSignal();

            if (!CloseOtherInstances())
            {
                Shutdown(0);
                return;
            }

            // Catch unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            // VC++ runtime probe: Sherpa-ONNX native DLLs link against vcruntime140 /
            // msvcp140. The installer bundles vc_redist.x64.exe and auto-runs it, but
            // antivirus, portable installs, or a manually-killed bootstrapper can leave
            // the user without it. Probe BEFORE the model download UI so we don't make
            // them sit through a 640MB download only to fail on first transcription.
            if (!IsSherpaNativeLoadable())
            {
                ShowVCRuntimeMissingDialog();
                Shutdown(1);
                return;
            }

            // First-launch gate: Parakeet model isn't bundled in the installer (~640MB
            // would balloon the .exe). Block MainWindow until the user downloads it.
            // ModelResolverService throws FileNotFoundException without this gate.
            if (!IsParakeetModelInstalled())
            {
                if (!TryRunStartupGate(this, ShowFirstLaunchModelDownload, out var gateError))
                {
                    if (gateError != null)
                    {
                        // Not "continue": no window and no tray icon exist yet, and the gate left the
                        // app in OnExplicitShutdown - nothing else would ever end this process.
                        ErrorLogger.LogError("First-launch model download dialog failed", gateError);
                        MessageBox.Show(
                            "VoiceLite could not show the speech model download.\n\n" +
                            gateError.Message + "\n\n" +
                            "Please start VoiceLite again. If this keeps happening, reinstall VoiceLite " +
                            "(details are in voicelite.log).",
                            "VoiceLite", MessageBoxButton.OK, MessageBoxImage.Error);
                        Shutdown(1);
                        return;
                    }
                    MessageBox.Show(
                        "VoiceLite needs the Parakeet v3 speech model to run.\n\n" +
                        "Restart VoiceLite when you're ready to download (~640MB).",
                        "VoiceLite", MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown(0);
                    return;
                }
            }

            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        private static bool SignalRunningInstanceToShow()
        {
            try
            {
                if (!EventWaitHandle.TryOpenExisting(ShowWindowSignalName, out var signal)) return false;
                using (signal) signal.Set();
                ErrorLogger.LogWarning("Second launch: asked the running VoiceLite to show its window");
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning($"Second launch: could not signal the running instance: {ex.Message}");
                return false;
            }
        }

        private void ListenForShowWindowSignal()
        {
            try
            {
                showWindowSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowSignalName);
                var listener = new Thread(() =>
                {
                    while (true)
                    {
                        var signal = showWindowSignal;
                        if (signal == null) return;
                        try { signal.WaitOne(); }
                        catch (ObjectDisposedException) { return; }
                        Dispatcher.BeginInvoke(() => SurfaceWindow(MainWindow));
                    }
                })
                { IsBackground = true, Name = "VoiceLite.ShowWindowListener" };
                listener.Start();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning($"Show-window listener not started: {ex.Message}");
            }
        }

        internal static void SurfaceWindow(Window? window)
        {
            if (window == null) return;
            try
            {
                window.Show();
                window.WindowState = WindowState.Normal;
                window.Activate();
                // Windows refuses to steal focus for a background process; the
                // Topmost flip is the standard way to actually surface the window.
                window.Topmost = true;
                window.Topmost = false;
            }
            catch (InvalidOperationException ex)
            {
                // Show() on an already-closed window throws; a second launch must never
                // surface that as an error dialog in the running instance.
                ErrorLogger.LogWarning($"Show-window signal ignored: {ex.Message}");
            }
        }

        /// <summary>
        /// Runs a startup gate whose dialog is the app's only Window. Under the default
        /// OnLastWindowClose, closing that dialog starts application shutdown and the
        /// MainWindow created afterwards never shows (Show() is a no-op while shutting down).
        /// The mode is restored on the success path only, before MainWindow exists; on
        /// failure the caller shuts down explicitly.
        /// </summary>
        internal static bool RunGateWithoutLastWindowShutdown(Application app, Func<bool> gate)
        {
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            if (!gate())
                return false;
            app.ShutdownMode = ShutdownMode.OnLastWindowClose;
            return true;
        }

        /// <summary>
        /// RunGateWithoutLastWindowShutdown, but a gate that THROWS is handed to the caller instead
        /// of escaping OnStartup. There it would reach OnDispatcherUnhandledException ("The
        /// application will continue", Handled = true) and leave a process with no window, no tray
        /// icon and OnExplicitShutdown - a zombie that holds the single-instance mutex, so every
        /// later launch signals it and exits silently. The caller must Shutdown(): restoring the
        /// mode would not help, OnLastWindowClose never fires with zero windows either.
        /// Does not log (tests run it in a child process; the log path is the real user log).
        /// </summary>
        internal static bool TryRunStartupGate(Application app, Func<bool> gate, out Exception? gateError)
        {
            gateError = null;
            try
            {
                return RunGateWithoutLastWindowShutdown(app, gate);
            }
            catch (Exception ex)
            {
                gateError = ex;
                return false;
            }
        }

        internal readonly record struct InstanceInfo(int Pid, int SessionId, DateTime? StartTime, bool HasExited);

        /// <summary>
        /// True if <paramref name="other"/> is a VoiceLite process that blocks this one: same
        /// Windows session (the mutex, the show-window event and RegisterHotKey are all
        /// per-session, so another user's copy is none of our business), still running, and
        /// not started after us (a later launch of this build hits our mutex and exits on
        /// its own). An unreadable start time (elevated copy) counts as stale.
        /// </summary>
        internal static bool IsStaleInstanceCandidate(InstanceInfo other, int selfPid, int selfSessionId, DateTime selfStart)
        {
            if (other.Pid == selfPid || other.SessionId != selfSessionId || other.HasExited)
                return false;
            return other.StartTime == null || other.StartTime <= selfStart;
        }

        /// <summary>
        /// Returns true once the process is gone. A process that exited on its own (before
        /// or during the kill) is a success, not a "couldn't close" failure.
        /// </summary>
        internal static bool TryCloseInstance(Func<bool> hasExited, Action kill, Func<int, bool> waitForExit)
        {
            try { if (hasExited()) return true; }
            catch (Exception) { /* no handle (elevated copy) - let Kill decide */ }

            try
            {
                kill();
            }
            catch (InvalidOperationException)
            {
                return true; // exited before we got to it
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Access denied - also what TerminateProcess reports for a process that is
                // already on its way out.
                try { return hasExited(); }
                catch (Exception) { return false; }
            }
            return waitForExit(5000);
        }

        private static InstanceInfo DescribeInstance(Process process)
        {
            DateTime? startTime = null;
            bool hasExited = false;
            try { startTime = process.StartTime; } catch (Exception) { /* elevated or already gone */ }
            try { hasExited = process.HasExited; } catch (Exception) { /* no handle - assume running */ }
            return new InstanceInfo(process.Id, process.SessionId, startTime, hasExited);
        }

        /// <summary>
        /// Returns false if startup must abort because another VoiceLite process is running
        /// and the user declined to close it (or it couldn't be closed).
        /// </summary>
        private static bool CloseOtherInstances()
        {
            using var self = Process.GetCurrentProcess();
            var all = Process.GetProcessesByName(self.ProcessName);
            try
            {
                int selfPid = self.Id;
                int selfSessionId = self.SessionId;
                DateTime selfStart = self.StartTime;
                bool IsStale(Process p) => IsStaleInstanceCandidate(DescribeInstance(p), selfPid, selfSessionId, selfStart);

                var others = all.Where(IsStale).ToList();

                // A second launch of this build that lost the mutex race exits by itself within
                // moments. One shared grace period, so it isn't reported as an "older copy".
                var grace = Stopwatch.StartNew();
                while (others.Count > 0 && grace.ElapsedMilliseconds < 2000)
                {
                    Thread.Sleep(100);
                    others = others.Where(IsStale).ToList();
                }
                if (others.Count == 0)
                    return true;

                var result = MessageBox.Show(
                    "An older copy of VoiceLite is still running in the background (probably in the system tray).\n\n" +
                    "Close it and continue starting this one?",
                    "VoiceLite", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                    return false;

                foreach (var other in others)
                {
                    bool closed;
                    try
                    {
                        closed = TryCloseInstance(() => other.HasExited, () => other.Kill(), ms => other.WaitForExit(ms));
                        if (!closed)
                            ErrorLogger.LogWarning($"Stale VoiceLite instance (pid {other.Id}) could not be closed");
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogError($"Failed to close stale VoiceLite instance (pid {other.Id})", ex);
                        closed = false;
                    }

                    if (!closed)
                    {
                        MessageBox.Show(
                            "Couldn't close the other VoiceLite. Exit it from the system tray and start VoiceLite again.",
                            "VoiceLite", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                    ErrorLogger.LogWarning($"Closed stale VoiceLite instance (pid {other.Id})");
                }
                return true;
            }
            finally
            {
                foreach (var process in all)
                    process.Dispose();
            }
        }

        private static bool IsSherpaNativeLoadable()
        {
            try
            {
                var handle = NativeLibrary.Load("sherpa-onnx-c-api", typeof(App).Assembly, null);
                NativeLibrary.Free(handle);
                // Log success so post-pilot telemetry can correlate probe-pass with
                // first-transcription failures (i.e. detect false negatives).
                ErrorLogger.LogMessage("Sherpa-ONNX native library loaded — VC++ runtime OK");
                return true;
            }
            catch (DllNotFoundException ex)
            {
                ErrorLogger.LogError("Sherpa-ONNX native DLL load failed — VC++ runtime likely missing", ex);
                return false;
            }
            catch (BadImageFormatException ex)
            {
                ErrorLogger.LogError("Sherpa-ONNX native DLL load failed — bad image (32/64-bit mismatch or VC++ ABI mismatch)", ex);
                return false;
            }
            catch (Exception ex)
            {
                // Unexpected — log but don't block startup. Could be AV interference.
                ErrorLogger.LogError("Sherpa-ONNX native DLL probe threw unexpected exception (continuing startup)", ex);
                return true;
            }
        }

        private static void ShowVCRuntimeMissingDialog()
        {
            const string vcRedistUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe";

            var result = MessageBox.Show(
                "VoiceLite couldn't load its speech engine.\n\n" +
                "This usually means the Microsoft Visual C++ Redistributable (x64) " +
                "isn't installed, or was blocked by antivirus during VoiceLite setup.\n\n" +
                "Click OK to open the Microsoft download page. Install the redistributable, " +
                "then relaunch VoiceLite.",
                "VoiceLite — Missing Visual C++ Runtime",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Error);

            if (result != MessageBoxResult.OK) return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = vcRedistUrl,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to open VC++ redistributable download page", ex);
                MessageBox.Show(
                    $"Couldn't open the browser automatically. Please visit:\n\n{vcRedistUrl}",
                    "VoiceLite",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private static bool IsParakeetModelInstalled()
        {
            try
            {
                var resolver = new ModelResolverService(AppDomain.CurrentDomain.BaseDirectory);
                // Exists-only checks let a 0-byte/truncated model (interrupted download,
                // full disk) pass this gate forever and wedge the app on first transcription.
                // Require every model file to be non-empty.
                return resolver.GetAvailableModelPaths().Any(dir =>
                    new[] { "encoder.int8.onnx", "decoder.int8.onnx", "joiner.int8.onnx", "tokens.txt" }
                        .All(f =>
                        {
                            var info = new FileInfo(Path.Combine(dir, f));
                            return info.Exists && info.Length > 0;
                        }));
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("IsParakeetModelInstalled probe failed", ex);
                return false;
            }
        }

        private static bool ShowFirstLaunchModelDownload()
        {
            var control = new ModelDownloadControl();
            control.Initialize(null, null);

            var window = new Window
            {
                Title = "VoiceLite — First-Launch Setup",
                Width = 720,
                Height = 480,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Content = new ScrollViewer
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Padding = new Thickness(20),
                    Content = control,
                },
            };

            // Auto-close once the download+extract completes so the user doesn't have
            // to click through another step.
            control.InstallCompleted += (_, _) =>
            {
                window.DialogResult = true;
                window.Close();
            };

            window.ShowDialog();
            return IsParakeetModelInstalled();
        }

        // Minimal OnExit, re-added solely to release the single-instance mutex on
        // normal shutdown (the previous OnExit/OnProcessExit overrides were deleted
        // as dead code). On abnormal termination the OS abandons the mutex, which
        // the installer's CheckForMutexes treats the same as released.
        protected override void OnExit(ExitEventArgs e)
        {
            var signal = Interlocked.Exchange(ref showWindowSignal, null);
            try { signal?.Dispose(); } catch (Exception ex) { ErrorLogger.LogDebug($"Show-window signal dispose failed: {ex.Message}"); }
            ReleaseSingleInstanceMutex();
            base.OnExit(e);
        }

        private static void ReleaseSingleInstanceMutex()
        {
            var mutex = Interlocked.Exchange(ref singleInstanceMutex, null);
            if (mutex == null)
                return;

            try { mutex.ReleaseMutex(); }
            catch (Exception ex) { ErrorLogger.LogDebug($"Single-instance mutex release failed: {ex.Message}"); }
            try { mutex.Dispose(); }
            catch (Exception ex) { ErrorLogger.LogDebug($"Single-instance mutex dispose failed: {ex.Message}"); }
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            ErrorLogger.LogError("UnobservedTaskException", e.Exception);
            e.SetObserved(); // Prevent process termination
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            if (ex != null)
            {
                ErrorLogger.LogError("UnhandledException", ex);
                MessageBox.Show($"Fatal error: {ex.Message}\n\nCheck the log at %LOCALAPPDATA%\\VoiceLite\\logs\\voicelite.log for details.",
                    "VoiceLite Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ErrorLogger.LogError("DispatcherUnhandledException", e.Exception);

            // Show user-friendly error only for non-critical exceptions
            if (!IsCriticalException(e.Exception))
            {
                MessageBox.Show($"An error occurred: {e.Exception.Message}\n\nThe application will continue.",
                    "VoiceLite Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                e.Handled = true; // Continue running
            }
            else
            {
                MessageBox.Show($"Critical error: {e.Exception.Message}\n\nThe application must close.",
                    "VoiceLite Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = false; // Let the application crash
            }
        }

        private bool IsCriticalException(Exception ex)
        {
            // Determine if an exception is critical enough to crash the app
            return ex is OutOfMemoryException ||
                   ex is StackOverflowException ||
                   ex is AccessViolationException ||
                   ex is TypeInitializationException;
        }
    }
}

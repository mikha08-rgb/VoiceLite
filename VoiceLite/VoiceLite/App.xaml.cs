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
                MessageBox.Show(
                    "VoiceLite is already running — check the system tray.",
                    "VoiceLite", MessageBoxButton.OK, MessageBoxImage.Information);
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
                if (!ShowFirstLaunchModelDownload())
                {
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

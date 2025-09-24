using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using VoiceLite.Services;

namespace VoiceLite
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Catch unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            // CRITICAL: Also handle process exit for forced closures
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            // Clean up any orphaned whisper processes from previous crashes
            CleanupOrphanedWhisperProcesses();
        }

        private void OnProcessExit(object? sender, EventArgs e)
        {
            // Clean up on any type of exit
            CleanupOrphanedWhisperProcesses();
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
                MessageBox.Show($"Fatal error: {ex.Message}\n\nCheck voicelite_error.log for details.",
                    "VoiceLite Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // CRITICAL: Clean up whisper processes before crash
            CleanupOrphanedWhisperProcesses();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ErrorLogger.LogError("DispatcherUnhandledException", e.Exception);
            MessageBox.Show($"Error: {e.Exception.Message}\n\nCheck voicelite_error.log for details.",
                "VoiceLite Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; // Prevent app from closing
        }

        private void CleanupOrphanedWhisperProcesses()
        {
            try
            {
                var whisperProcesses = Process.GetProcessesByName("whisper");
                if (whisperProcesses.Length > 0)
                {
                    ErrorLogger.LogMessage($"Found {whisperProcesses.Length} whisper processes to clean up");
                }

                foreach (var process in whisperProcesses)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            ErrorLogger.LogMessage($"Killing whisper process: PID {process.Id}");

                            // Kill entire process tree to ensure child processes are terminated
                            process.Kill(true);
                            process.WaitForExit(1000);
                        }
                    }
                    catch { /* Ignore individual process errors */ }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("CleanupOrphanedWhisperProcesses", ex);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Final cleanup on exit
            CleanupOrphanedWhisperProcesses();
            base.OnExit(e);
        }
    }
}
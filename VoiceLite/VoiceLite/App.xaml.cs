using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VoiceLite.Infrastructure.DependencyInjection;
using VoiceLite.Services;

namespace VoiceLite
{
    public partial class App : Application
    {
        private IHost? _host;

        protected override async void OnStartup(StartupEventArgs e)
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

            // Configure and build the DI host
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Add all application services
                    services.AddVoiceLiteServices();
                    services.AddInfrastructureServices();
                    services.ConfigureOptions();
                })
                .Build();

            // Initialize the service provider wrapper for global access
            ServiceProviderWrapper.Initialize(_host.Services);

            // Start the host
            await _host.StartAsync();

            // Create and show the main window
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            // Clean up on exit
            CleanupOrphanedWhisperProcesses();

            // Stop and dispose the host
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }

            base.OnExit(e);
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

            // Clean up before crash
            CleanupOrphanedWhisperProcesses();
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

        private void CleanupOrphanedWhisperProcesses()
        {
            try
            {
                var orphanedProcesses = Process.GetProcessesByName("whisper")
                    .Where(p =>
                    {
                        try
                        {
                            // CRITICAL FIX #2: Only kill processes started by this user
                            // to avoid permission errors and killing system processes
                            var processPath = p.MainModule?.FileName ?? "";
                            var ourWhisperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper", "whisper.exe");

                            // Check if it's OUR whisper process
                            return processPath.Equals(ourWhisperPath, StringComparison.OrdinalIgnoreCase);
                        }
                        catch
                        {
                            // Can't access process info (probably system process) - skip it
                            return false;
                        }
                    });

                foreach (var process in orphanedProcesses)
                {
                    try
                    {
                        ErrorLogger.LogMessage($"Killing orphaned whisper process: PID {process.Id}");
                        process.Kill();
                        process.WaitForExit(1000); // Wait up to 1 second
                        process.Dispose();
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogMessage($"Failed to kill whisper process {process.Id}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to cleanup orphaned processes", ex);
            }
        }
    }
}
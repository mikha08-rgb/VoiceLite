using System;
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

            // Create and show the main window
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
        }

        private void OnProcessExit(object? sender, EventArgs e)
        {
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

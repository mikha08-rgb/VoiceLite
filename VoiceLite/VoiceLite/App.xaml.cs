using System;
using System.Linq;
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
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Catch unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            // CRITICAL: Also handle process exit for forced closures
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

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

        private static bool IsParakeetModelInstalled()
        {
            try
            {
                var resolver = new ModelResolverService(AppDomain.CurrentDomain.BaseDirectory);
                return resolver.GetAvailableModelPaths().Any();
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

using System;
using System.Threading.Tasks;
using System.Windows;
using VoiceLite.Services;

namespace VoiceLite.Helpers
{
    /// <summary>
    /// WEEK 1 FIX: Helper class for safely handling async operations in event handlers
    /// Prevents unhandled exceptions in async void methods from crashing the application
    /// </summary>
    public static class AsyncHelper
    {
        /// <summary>
        /// Safely executes async code in event handlers with proper error handling
        /// Use this for fire-and-forget async operations that don't return a value
        /// </summary>
        /// <param name="task">The async task to execute</param>
        /// <param name="operationName">Name of the operation for logging</param>
        /// <param name="showUserMessage">Whether to show error message to user</param>
        public static void SafeFireAndForget(
            Task task,
            string operationName = "Operation",
            bool showUserMessage = true)
        {
            // Start the async operation and handle any exceptions
            _ = SafeFireAndForgetInternal(task, operationName, showUserMessage);
        }

        /// <summary>
        /// Safely executes async code in event handlers with proper error handling
        /// Use this when you need to start an async operation from a sync context
        /// </summary>
        /// <param name="asyncAction">The async action to execute</param>
        /// <param name="operationName">Name of the operation for logging</param>
        /// <param name="showUserMessage">Whether to show error message to user</param>
        public static void SafeFireAndForget(
            Func<Task> asyncAction,
            string operationName = "Operation",
            bool showUserMessage = true)
        {
            _ = SafeFireAndForgetInternal(asyncAction(), operationName, showUserMessage);
        }

        /// <summary>
        /// Internal implementation that actually handles the async execution
        /// </summary>
        private static async Task SafeFireAndForgetInternal(
            Task task,
            string operationName,
            bool showUserMessage)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // Normal during shutdown or user cancellation - don't log
                ErrorLogger.LogDebug($"{operationName} was cancelled");
            }
            catch (ObjectDisposedException ex)
            {
                // Common during shutdown - log as warning, not error
                ErrorLogger.LogWarning($"{operationName} failed: Object already disposed - {ex.Message}");
            }
            catch (Exception ex)
            {
                // Log the error
                ErrorLogger.LogError($"{operationName} failed", ex);

                // Show user message if requested and UI is available
                if (showUserMessage && Application.Current != null)
                {
                    try
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(
                                $"{operationName} failed:\n{ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        });
                    }
                    catch
                    {
                        // If we can't show the message (app shutting down), just log it
                        ErrorLogger.LogWarning($"Could not show error message to user for: {operationName}");
                    }
                }
            }
        }

        /// <summary>
        /// Safely executes async code with a return value and proper error handling
        /// Returns a default value if an exception occurs
        /// </summary>
        /// <typeparam name="T">The return type</typeparam>
        /// <param name="taskFactory">Function that creates the async task</param>
        /// <param name="operationName">Name of the operation for logging</param>
        /// <param name="defaultValue">Value to return if exception occurs</param>
        /// <returns>The result or default value</returns>
        public static async Task<T?> SafeExecuteAsync<T>(
            Func<Task<T>> taskFactory,
            string operationName = "Operation",
            T? defaultValue = default)
        {
            try
            {
                return await taskFactory().ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                ErrorLogger.LogDebug($"{operationName} was cancelled");
                return defaultValue;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError($"{operationName} failed", ex);
                return defaultValue;
            }
        }

    }
}
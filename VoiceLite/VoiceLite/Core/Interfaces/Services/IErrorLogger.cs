using System;

namespace VoiceLite.Core.Interfaces.Services
{
    /// <summary>
    /// Interface for centralized error logging
    /// </summary>
    public interface IErrorLogger
    {
        /// <summary>
        /// Logs an error with optional user notification
        /// </summary>
        /// <param name="exception">The exception to log</param>
        /// <param name="context">Context information about where the error occurred</param>
        /// <param name="showUserMessage">Whether to show a message box to the user</param>
        void LogError(Exception exception, string context, bool showUserMessage = false);

        /// <summary>
        /// Logs an informational message
        /// </summary>
        /// <param name="message">The message to log</param>
        void LogInfo(string message);

        /// <summary>
        /// Logs a warning message
        /// </summary>
        /// <param name="message">The warning message</param>
        void LogWarning(string message);

        /// <summary>
        /// Logs debug information (only in debug builds)
        /// </summary>
        /// <param name="message">The debug message</param>
        void LogDebug(string message);

        /// <summary>
        /// Gets the path to the log file
        /// </summary>
        string GetLogFilePath();

        /// <summary>
        /// Clears old log files based on retention policy
        /// </summary>
        void CleanupOldLogs(int daysToKeep = 7);

        /// <summary>
        /// Flushes any buffered log entries to disk
        /// </summary>
        void Flush();
    }
}
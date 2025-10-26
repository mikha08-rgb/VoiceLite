using System;
using System.IO;
using VoiceLite.Core.Interfaces.Services;

namespace VoiceLite.Services
{
    /// <summary>
    /// Service wrapper for the static ErrorLogger class
    /// Implements IErrorLogger interface for dependency injection
    /// </summary>
    public class ErrorLoggerService : IErrorLogger
    {
        public void LogError(Exception exception, string context, bool showUserMessage = false)
        {
            ErrorLogger.LogError(context, exception);
            // Note: showUserMessage parameter ignored - ErrorLogger doesn't support it
        }

        public void LogInfo(string message)
        {
            ErrorLogger.LogMessage(message);
        }

        public void LogWarning(string message)
        {
            ErrorLogger.LogMessage($"WARNING: {message}");
        }

        public void LogDebug(string message)
        {
#if DEBUG
            ErrorLogger.LogMessage($"DEBUG: {message}");
#endif
        }

        public string GetLogFilePath()
        {
            // Return the standard log file path
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoiceLite",
                "logs",
                "voicelite.log");
        }

        public void CleanupOldLogs(int daysToKeep = 7)
        {
            // Call the static cleanup method if it exists
            // For now, this is a no-op as the static class doesn't have this method
            // You could implement file cleanup logic here
        }

        public void Flush()
        {
            // Force flush any buffered logs
            // The static ErrorLogger writes immediately, so this is a no-op
        }
    }
}
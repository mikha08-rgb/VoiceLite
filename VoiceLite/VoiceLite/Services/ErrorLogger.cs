using System;
using System.IO;
using System.Linq;

namespace VoiceLite.Services
{
    public static class ErrorLogger
    {
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoiceLite",
            "logs");

        private static readonly string LogPath = Path.Combine(LogDirectory, "voicelite.log");

        private const long MaxLogSizeBytes = 10 * 1024 * 1024; // 10MB max
        private static readonly object LogLock = new object();

        static ErrorLogger()
        {
            // Ensure log directory exists
            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }
            }
            catch
            {
                // If we can't create the log directory, logging will fail silently
                // This is better than crashing the application
            }
        }

        public static void LogError(string context, Exception ex)
        {
            try
            {
                var message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}: {ex.Message}\n" +
                             $"Stack: {ex.StackTrace}\n" +
                             $"Inner: {ex.InnerException?.Message}\n" +
                             "---\n";

                lock (LogLock)
                {
                    RotateLogIfNeeded();
                    File.AppendAllText(LogPath, message);
                }
            }
            catch
            {
                // Can't log the error about logging errors
            }
        }

        public static void LogMessage(string message)
        {
            try
            {
                var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n";
                lock (LogLock)
                {
                    RotateLogIfNeeded();
                    File.AppendAllText(LogPath, logMessage);
                }
            }
            catch
            {
                // Silent fail
            }
        }

        private static void RotateLogIfNeeded()
        {
            try
            {
                if (File.Exists(LogPath))
                {
                    var fileInfo = new FileInfo(LogPath);
                    if (fileInfo.Length > MaxLogSizeBytes)
                    {
                        // Archive old log
                        var archivePath = LogPath + ".old";
                        if (File.Exists(archivePath))
                        {
                            File.Delete(archivePath);
                        }
                        File.Move(LogPath, archivePath);
                    }
                }
            }
            catch
            {
                // If rotation fails, try to truncate
                try
                {
                    File.WriteAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Log rotated due to size\n");
                }
                catch { }
            }
        }
    }
}
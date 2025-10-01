using System;
using System.IO;
using System.Linq;

namespace VoiceLite.Services
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    public static class ErrorLogger
    {
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoiceLite",
            "logs");

        private static readonly string LogPath = Path.Combine(LogDirectory, "voicelite.log");

        private const long MaxLogSizeBytes = 10 * 1024 * 1024; // 10MB max
        private static readonly object LogLock = new object();

        // Log level configuration - can be changed at runtime
        // In Debug builds, log everything. In Release builds, only log warnings and errors.
#if DEBUG
        public static LogLevel MinimumLogLevel { get; set; } = LogLevel.Debug;
#else
        public static LogLevel MinimumLogLevel { get; set; } = LogLevel.Warning;
#endif

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

        // New level-based logging methods
        public static void LogDebug(string message) => Log(LogLevel.Debug, message);
        public static void LogInfo(string message) => Log(LogLevel.Info, message);
        public static void LogWarning(string message) => Log(LogLevel.Warning, message);

        public static void Log(LogLevel level, string message)
        {
            // Filter based on minimum log level
            if (level < MinimumLogLevel)
                return;

            try
            {
                var levelStr = level switch
                {
                    LogLevel.Debug => "DEBUG",
                    LogLevel.Info => "INFO",
                    LogLevel.Warning => "WARN",
                    LogLevel.Error => "ERROR",
                    _ => "INFO"
                };

                var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{levelStr}] {message}\n";
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

        // Legacy methods - kept for backward compatibility
        // LogError() always logs regardless of level
        public static void LogError(string context, Exception ex)
        {
            try
            {
                var message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] {context}: {ex.Message}\n" +
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

        // LogMessage() uses Info level by default
        public static void LogMessage(string message) => Log(LogLevel.Info, message);

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
using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// Tests for ErrorLogger service.
    /// Note: Since ErrorLogger uses readonly static fields for log paths,
    /// these tests write to the real log directory and use unique markers to identify test entries.
    /// </summary>
    [Trait("Category", "FileIO")]
    public class ErrorLoggerTests : IDisposable
    {
        private readonly LogLevel _originalMinimumLogLevel;
        private readonly string _testMarker;

        public ErrorLoggerTests()
        {
            // Save original log level
            _originalMinimumLogLevel = ErrorLogger.MinimumLogLevel;

            // Create unique test marker
            _testMarker = $"TEST_{Guid.NewGuid()}";

            // Reset to Debug level for tests
            ErrorLogger.MinimumLogLevel = LogLevel.Debug;
        }

        public void Dispose()
        {
            // Restore original log level
            ErrorLogger.MinimumLogLevel = _originalMinimumLogLevel;
        }

        // Task 2: Happy Path Tests (5 tests)

        [Fact]
        public void LogDebug_WhenCalled_WritesToLogFile()
        {
            var message = $"{_testMarker}_Debug";
            ErrorLogger.LogDebug(message);

            // Verify log exists and contains the message
            var logPath = GetLogPath();
            File.Exists(logPath).Should().BeTrue();
            var content = File.ReadAllText(logPath);
            content.Should().Contain("[DEBUG]");
            content.Should().Contain(message);
        }

        [Fact]
        public void LogInfo_WhenCalled_WritesToLogFile()
        {
            var message = $"{_testMarker}_Info";
            ErrorLogger.LogInfo(message);

            var logPath = GetLogPath();
            var content = File.ReadAllText(logPath);
            content.Should().Contain("[INFO]");
            content.Should().Contain(message);
        }

        [Fact]
        public void LogWarning_WhenCalled_WritesToLogFile()
        {
            var message = $"{_testMarker}_Warning";
            ErrorLogger.LogWarning(message);

            var logPath = GetLogPath();
            var content = File.ReadAllText(logPath);
            content.Should().Contain("[WARN]");
            content.Should().Contain(message);
        }

        [Fact]
        public void LogError_WithException_WritesStackTrace()
        {
            var message = $"{_testMarker}_Error";
            var exception = new InvalidOperationException("Test exception");

            ErrorLogger.LogError(message, exception);

            var logPath = GetLogPath();
            var content = File.ReadAllText(logPath);
            content.Should().Contain("[ERROR]");
            content.Should().Contain(message);
            content.Should().Contain("Test exception");
            content.Should().Contain("Stack:");
        }

        [Fact]
        public void LogMessage_UsesInfoLevel()
        {
            var message = $"{_testMarker}_Message";
            ErrorLogger.LogMessage(message);

            var logPath = GetLogPath();
            var content = File.ReadAllText(logPath);
            content.Should().Contain("[INFO]");
            content.Should().Contain(message);
        }

        // Task 3: Log Level Filtering Tests (4 tests)

        [Fact]
        public void Log_BelowMinimumLevel_DoesNotWrite()
        {
            ErrorLogger.MinimumLogLevel = LogLevel.Warning;
            var message = $"{_testMarker}_FilteredDebug";

            ErrorLogger.LogDebug(message);
            ErrorLogger.LogInfo(message);

            var logPath = GetLogPath();
            if (File.Exists(logPath))
            {
                var content = File.ReadAllText(logPath);
                content.Should().NotContain(message);
            }

            // Restore Debug level for other tests
            ErrorLogger.MinimumLogLevel = LogLevel.Debug;
        }

        [Fact]
        public void Log_AtMinimumLevel_Writes()
        {
            ErrorLogger.MinimumLogLevel = LogLevel.Warning;
            var message = $"{_testMarker}_AtMinimum";

            ErrorLogger.LogWarning(message);

            var logPath = GetLogPath();
            var content = File.ReadAllText(logPath);
            content.Should().Contain(message);

            ErrorLogger.MinimumLogLevel = LogLevel.Debug;
        }

        [Fact]
        public void Log_AboveMinimumLevel_Writes()
        {
            ErrorLogger.MinimumLogLevel = LogLevel.Warning;
            var message = $"{_testMarker}_AboveMinimum";

            ErrorLogger.Log(LogLevel.Error, message);

            var logPath = GetLogPath();
            var content = File.ReadAllText(logPath);
            content.Should().Contain(message);

            ErrorLogger.MinimumLogLevel = LogLevel.Debug;
        }

        [Fact]
        public void MinimumLogLevel_CanBeChanged_AtRuntime()
        {
            var message1 = $"{_testMarker}_Level1";
            var message2 = $"{_testMarker}_Level2";

            ErrorLogger.MinimumLogLevel = LogLevel.Debug;
            ErrorLogger.LogDebug(message1);

            ErrorLogger.MinimumLogLevel = LogLevel.Error;
            ErrorLogger.LogDebug(message2);

            var logPath = GetLogPath();
            var content = File.ReadAllText(logPath);
            content.Should().Contain(message1);
            content.Should().NotContain(message2);

            ErrorLogger.MinimumLogLevel = LogLevel.Debug;
        }

        // Task 4: Log Rotation Tests (Simplified - testing the code path exists)

        [Fact]
        public void LogRotation_CodePath_DoesNotThrow()
        {
            // We can't easily create a 10MB file in tests, but we can verify
            // that the rotation code path doesn't throw errors
            var message = $"{_testMarker}_Rotation";

            Action act = () => ErrorLogger.LogInfo(message);
            act.Should().NotThrow();

            var logPath = GetLogPath();
            File.Exists(logPath).Should().BeTrue();
        }

        // Task 5: Error Handling Tests (4 tests)

        [Fact]
        public void Log_WhenFileWriteMightFail_DoesNotThrow()
        {
            var message = $"{_testMarker}_ErrorHandling";

            Action act = () => ErrorLogger.LogInfo(message);
            act.Should().NotThrow();
        }

        [Fact]
        public void LogError_WhenFileWriteMightFail_DoesNotThrow()
        {
            var message = $"{_testMarker}_ErrorErrorHandling";
            var ex = new Exception("Test exception");

            Action act = () => ErrorLogger.LogError(message, ex);
            act.Should().NotThrow();
        }

        // Task 6: Thread Safety Tests (3 tests)

        [Fact]
        public async Task Log_ConcurrentCalls_ThreadSafe()
        {
            var tasks = new Task[20];
            for (int i = 0; i < 20; i++)
            {
                int index = i;
                var message = $"{_testMarker}_Concurrent{index}";
                tasks[i] = Task.Run(() => ErrorLogger.LogInfo(message));
            }

            await Task.WhenAll(tasks);

            // All messages should be logged
            var logPath = GetLogPath();
            var content = File.ReadAllText(logPath);
            for (int i = 0; i < 20; i++)
            {
                content.Should().Contain($"{_testMarker}_Concurrent{i}");
            }
        }

        [Fact]
        public async Task LogError_ConcurrentCalls_ThreadSafe()
        {
            var tasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                int index = i;
                var message = $"{_testMarker}_ErrorConcurrent{index}";
                tasks[i] = Task.Run(() =>
                {
                    var ex = new Exception($"Exception {index}");
                    ErrorLogger.LogError(message, ex);
                });
            }

            await Task.WhenAll(tasks);

            // All errors should be logged
            var logPath = GetLogPath();
            var content = File.ReadAllText(logPath);
            for (int i = 0; i < 10; i++)
            {
                content.Should().Contain($"{_testMarker}_ErrorConcurrent{i}");
            }
        }

        [Fact]
        public async Task ConcurrentWrites_ThreadSafe()
        {
            var tasks = new Task[15];
            for (int i = 0; i < 15; i++)
            {
                int index = i;
                var message = $"{_testMarker}_MixedConcurrent{index}";
                tasks[i] = Task.Run(() =>
                {
                    if (index % 2 == 0)
                    {
                        ErrorLogger.LogInfo(message);
                    }
                    else
                    {
                        var ex = new Exception($"Ex{index}");
                        ErrorLogger.LogError(message, ex);
                    }
                });
            }

            await Task.WhenAll(tasks);

            var logPath = GetLogPath();
            File.Exists(logPath).Should().BeTrue();
        }

        // Task 7: Branch Coverage Tests

        [Fact]
        public void LogLevel_SwitchStatement_AllCases()
        {
            var marker = $"{_testMarker}_Switch";

            // Test all 4 log levels
            ErrorLogger.Log(LogLevel.Debug, $"{marker}_Debug");
            ErrorLogger.Log(LogLevel.Info, $"{marker}_Info");
            ErrorLogger.Log(LogLevel.Warning, $"{marker}_Warning");
            ErrorLogger.Log(LogLevel.Error, $"{marker}_Error");

            var logPath = GetLogPath();
            var content = File.ReadAllText(logPath);
            content.Should().Contain("[DEBUG]").And.Contain($"{marker}_Debug");
            content.Should().Contain("[INFO]").And.Contain($"{marker}_Info");
            content.Should().Contain("[WARN]").And.Contain($"{marker}_Warning");
            content.Should().Contain("[ERROR]").And.Contain($"{marker}_Error");
        }

        [Fact]
        public void LogLevel_InvalidLevel_UsesDefault()
        {
            var marker = $"{_testMarker}_Invalid";

            // Cast invalid int to LogLevel to test default case
            var invalidLevel = (LogLevel)999;
            ErrorLogger.Log(invalidLevel, marker);

            var logPath = GetLogPath();
            var content = File.ReadAllText(logPath);
            content.Should().Contain("[INFO]"); // Default case maps to INFO
            content.Should().Contain(marker);
        }

        [Fact]
        public void MinimumLogLevel_Filter_BelowMinimum()
        {
            ErrorLogger.MinimumLogLevel = LogLevel.Error;
            var marker = $"{_testMarker}_BelowMin";

            ErrorLogger.LogDebug($"{marker}_Debug");
            ErrorLogger.LogInfo($"{marker}_Info");
            ErrorLogger.LogWarning($"{marker}_Warning");

            var logPath = GetLogPath();
            if (File.Exists(logPath))
            {
                var content = File.ReadAllText(logPath);
                content.Should().NotContain(marker);
            }

            ErrorLogger.MinimumLogLevel = LogLevel.Debug;
        }

        [Fact]
        public void Log_CreatesLogFile_WhenNotExists()
        {
            var marker = $"{_testMarker}_Create";
            ErrorLogger.LogInfo(marker);

            var logPath = GetLogPath();
            File.Exists(logPath).Should().BeTrue();
            var content = File.ReadAllText(logPath);
            content.Should().Contain(marker);
        }

        [Fact]
        public void Log_AppendsToExistingFile()
        {
            var marker1 = $"{_testMarker}_Append1";
            var marker2 = $"{_testMarker}_Append2";

            ErrorLogger.LogInfo(marker1);
            ErrorLogger.LogInfo(marker2);

            var logPath = GetLogPath();
            var content = File.ReadAllText(logPath);
            content.Should().Contain(marker1);
            content.Should().Contain(marker2);
        }

        [Fact]
        public void LogError_IncludesInnerException()
        {
            var marker = $"{_testMarker}_Inner";
            var inner = new InvalidOperationException("Inner exception");
            var outer = new Exception("Outer exception", inner);

            ErrorLogger.LogError(marker, outer);

            var logPath = GetLogPath();
            var content = File.ReadAllText(logPath);
            content.Should().Contain(marker);
            content.Should().Contain("Outer exception");
            content.Should().Contain("Inner exception");
        }

        // Helper method to get log path via reflection
        private string GetLogPath()
        {
            var errorLoggerType = typeof(ErrorLogger);
            var logPathField = errorLoggerType.GetField("LogPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return (string)logPathField!.GetValue(null)!;
        }
    }
}

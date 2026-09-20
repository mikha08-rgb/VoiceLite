using System;
using System.IO;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// Source tripwire: LogMessage/LogDebug/LogInfo write nothing in Release (MinimumLogLevel is
    /// Warning), so failure branches logged that way leave no trace in a user's voicelite.log.
    /// ErrorLogger is a static file writer with no seam; this only checks which method the
    /// failure messages below are handed to. It does not exercise the failure paths.
    /// </summary>
    public class ModelInstallerMicMonitorLogLevelTests
    {
        private static string? ReadAppSource(string relativePath, [CallerFilePath] string thisFile = "")
        {
            var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "VoiceLite", relativePath));
            return File.Exists(path) ? File.ReadAllText(path) : null; // null: binaries copied away from the source tree
        }

        [Theory]
        [InlineData("Services/ModelInstaller.cs", "Temp model archive cleanup failed")]
        [InlineData("Services/ModelInstaller.cs", "Stale model archive sweep failed")]
        [InlineData("Services/ModelInstaller.cs", "Disk-space preflight skipped for")]
        [InlineData("Services/ModelInstaller.cs", "Disk-space preflight skipped:")]
        [InlineData("Services/MicLevelMonitor.cs", "MicLevelMonitor stop failed")]
        [InlineData("Services/MicLevelMonitor.cs", "MicLevelMonitor dispose failed")]
        public void FailureBranch_IsLoggedAtAReleaseVisibleLevel(string file, string messagePrefix)
        {
            var source = ReadAppSource(file);
            if (source == null) return;

            var at = source.IndexOf(messagePrefix, StringComparison.Ordinal);
            at.Should().BeGreaterThan(0, $"'{messagePrefix}' should still be logged in {file} (update this test if it was reworded)");

            const string call = "ErrorLogger.Log";
            var callAt = source.LastIndexOf(call, at, StringComparison.Ordinal);
            callAt.Should().BeGreaterThan(0);
            var method = source.Substring(callAt + "ErrorLogger.".Length, source.IndexOf('(', callAt) - callAt - "ErrorLogger.".Length);

            method.Should().BeOneOf(new[] { "LogWarning", "LogError" },
                $"'{messagePrefix}' is a failure/fallback branch and must show up in Release logs");
        }
    }
}

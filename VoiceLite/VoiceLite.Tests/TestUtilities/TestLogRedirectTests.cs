using System;
using System.IO;
using AwesomeAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.TestUtilities
{
    /// <summary>
    /// Tripwire for TestLogRedirect: a test run must never append to (or rotate) the real
    /// %LOCALAPPDATA%\VoiceLite\logs\voicelite.log - that log is what update problems are
    /// diagnosed from, and the owner's licensed VoiceLite may be writing to it right now.
    /// </summary>
    public class TestLogRedirectTests
    {
        private static readonly string RealVoiceLiteData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VoiceLite");

        [Fact]
        public void ErrorLogger_InTheTestHost_DoesNotPointAtTheRealUserLog()
        {
            Path.GetFullPath(ErrorLogger.LogPath).Should().NotStartWith(RealVoiceLiteData,
                "tests drive product code that logs fake 'installer exited' / 'shutting down for update' lines");
            Path.GetFullPath(ErrorLogger.LogPath).Should().Be(
                Path.GetFullPath(Path.Combine(TestLogRedirect.LogDirectory, "voicelite.log")));
        }

        [Fact]
        public void ReleaseVisibleLogCalls_LandInTheRedirectedFile()
        {
            var marker = "TestLogRedirectTests-" + Guid.NewGuid().ToString("N");

            ErrorLogger.LogWarning(marker + "-warning");
            ErrorLogger.LogError(marker + "-error", new InvalidOperationException("boom"));

            // FileShare.ReadWrite: tests in other classes are appending to this file right now.
            using var stream = new FileStream(Path.Combine(TestLogRedirect.LogDirectory, "voicelite.log"),
                FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var written = new StreamReader(stream).ReadToEnd();
            written.Should().Contain(marker + "-warning");
            written.Should().Contain(marker + "-error: boom");
        }

        [Fact]
        public void WithoutTheOverride_TheProductionPathIsUnchanged()
        {
            // The seam must not move the shipped log: support instructions and the Settings
            // "open logs" affordances point at %LOCALAPPDATA%\VoiceLite\logs\voicelite.log.
            // ErrorLogger resolves its path once per process, so the default is pinned at
            // source level (SourceTripwirePolicy: a missing source fails).
            var source = RepoSourceFile.ReadApp("Services", "ErrorLogger.cs");

            source.Should().Contain("AppContext.GetData(LogDirectoryOverrideKey) as string");
            source.Should().Contain("Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),");
            source.Should().Contain("\"VoiceLite\",");
            source.Should().Contain("\"logs\");");
            source.Should().Contain("Path.Combine(LogDirectory, \"voicelite.log\")");
        }
    }
}

using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// Source tripwire (S6, protected files): LogMessage/LogDebug/LogInfo write nothing in
    /// Release (MinimumLogLevel is Warning), so a paying user who hits one of these failure
    /// branches in the capture core, the native recognizer lifecycle or the clipboard privacy
    /// auto-clear used to leave no trace in voicelite.log. These sites are not unit-reachable
    /// (WaveInEvent, native OfflineRecognizer, STA clipboard), so this only checks which
    /// ErrorLogger method each failure message is handed to - EVERY occurrence of it. It does
    /// not exercise the failure paths.
    /// </summary>
    public class ProtectedFailureBranchLogLevelTests
    {
        // A missing source file FAILS the tripwire (SourceTripwirePolicyTests), it is not skipped.
        private static string ReadAppSource(string relativePath) => RepoSourceFile.ReadApp(relativePath.Split('/'));

        [Theory]
        // Capture core: self-declared invariant violation (device lost mid-recording lands here).
        [InlineData("Services/AudioRecorder.cs", "OnRecordingStopped: Defensive waveIn cleanup - this should not happen", 1)]
        // Native recognizer dispose failure = leaked ~600MB model or a native-side fault.
        [InlineData("Services/TranscriptionService.cs", "OfflineRecognizer disposal failed during switch", 1)]
        [InlineData("Services/TranscriptionService.cs", "OfflineRecognizer dispose failed", 1)]
        // Privacy guarantee: dictated text must not linger on the clipboard (both paste paths).
        [InlineData("Services/TextInjector.cs", "Clipboard auto-clear failed", 2)] // ordinal: not the "Scheduled clipboard..." lines
        [InlineData("Services/TextInjector.cs", "Scheduled clipboard auto-clear failed", 2)]
        public void FailureBranch_IsLoggedAtAReleaseVisibleLevel(string file, string messagePrefix, int expectedOccurrences)
        {
            var source = ReadAppSource(file);

            var methods = new List<string>();
            for (var at = source.IndexOf(messagePrefix, StringComparison.Ordinal);
                 at >= 0;
                 at = source.IndexOf(messagePrefix, at + messagePrefix.Length, StringComparison.Ordinal))
            {
                const string call = "ErrorLogger.";
                var callAt = source.LastIndexOf(call + "Log", at, StringComparison.Ordinal);
                callAt.Should().BeGreaterThan(0);
                methods.Add(source.Substring(callAt + call.Length, source.IndexOf('(', callAt) - callAt - call.Length));
            }

            methods.Should().HaveCount(expectedOccurrences,
                $"'{messagePrefix}' should still be logged in {file} (update this test if it was reworded)");
            methods.Should().OnlyContain(m => m == "LogWarning" || m == "LogError",
                $"'{messagePrefix}' is a failure/fallback branch and must show up in Release logs");
        }
    }
}

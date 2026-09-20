using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

            var methods = LogMethodsFor(source, messagePrefix);

            methods.Should().HaveCount(expectedOccurrences,
                $"'{messagePrefix}' should still be logged in {file} (update this test if it was reworded)");
            methods.Should().OnlyContain(m => m == "LogWarning" || m == "LogError",
                $"'{messagePrefix}' is a failure/fallback branch and must show up in Release logs");
        }

        /// <summary>
        /// The ErrorLogger method each occurrence of <paramref name="messagePrefix"/> is handed to.
        /// </summary>
        internal static List<string> LogMethodsFor(string source, string messagePrefix)
        {
            var methods = new List<string>();
            for (var at = source.IndexOf(messagePrefix, StringComparison.Ordinal);
                 at >= 0;
                 at = source.IndexOf(messagePrefix, at + messagePrefix.Length, StringComparison.Ordinal))
            {
                const string call = "ErrorLogger.";
                var callAt = source.LastIndexOf(call + "Log", at, StringComparison.Ordinal);
                callAt.Should().BeGreaterThan(0);

                // Call and message must be ONE statement: otherwise the nearest preceding
                // ErrorLogger call is an unrelated one (message moved into a local/helper).
                source.Substring(callAt, at - callAt).IndexOfAny(new[] { ';', '\n' }).Should().Be(-1,
                    $"'{messagePrefix}' must be passed directly to an ErrorLogger call");

                // ...and that statement must be compiled in Release: the nearest preceding
                // conditional directive must not open a DEBUG-only block.
                var directive = Regex.Matches(source.Substring(0, callAt), @"^[ \t]*#(if|elif|else|endif)\b[^\r\n]*", RegexOptions.Multiline)
                    .LastOrDefault()?.Value.Trim();
                (directive ?? "").Should().NotStartWith("#if DEBUG",
                    $"'{messagePrefix}' is logged inside #if DEBUG, so Release logs stay silent");

                methods.Add(source.Substring(callAt + call.Length, source.IndexOf('(', callAt) - callAt - call.Length));
            }
            return methods;
        }

        // ---- the tripwire's own blind spots, on synthetic sources ----

        [Fact]
        public void Tripwire_Accepts_TheShapeOfThePinnedSites() =>
            LogMethodsFor("{\n    try { x(); }\n    catch (Exception ex) { ErrorLogger.LogWarning($\"Boom failed: {ex.Message}\"); }\n}\n", "Boom failed")
                .Should().Equal("LogWarning");

        [Fact]
        public void Tripwire_Fails_WhenTheMessageMovedOutOfTheLogCall()
        {
            // The nearest PRECEDING ErrorLogger call is an unrelated one: it must not be credited.
            var source = "{\n    ErrorLogger.LogWarning(\"unrelated\");\n    var msg = \"Boom failed\";\n    Helper.Log(msg);\n}\n";

            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => LogMethodsFor(source, "Boom failed"));
        }

        [Fact]
        public void Tripwire_Fails_WhenTheCallIsCompiledOnlyInDebug()
        {
            // TextInjector already wraps its routine lines this way; Release would be silent again.
            var source = "{\n#if DEBUG\n    ErrorLogger.LogWarning(\"Boom failed\");\n#endif\n}\n";

            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => LogMethodsFor(source, "Boom failed"));
        }

        [Fact]
        public void Tripwire_Accepts_ACallAfterAClosedDebugBlock()
        {
            var source = "{\n#if DEBUG\n    ErrorLogger.LogMessage(\"noise\");\n#endif\n    ErrorLogger.LogWarning(\"Boom failed\");\n}\n";

            LogMethodsFor(source, "Boom failed").Should().Equal("LogWarning");
        }
    }
}

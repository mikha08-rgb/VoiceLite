using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Threading;
using AwesomeAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// MainWindow's "back to Ready" status reset: one pending reset at a time, the no-speech
    /// status resets like the other transient ones, and the auto-stop text follows the real limit.
    /// MainWindow itself is never constructed here (that would need the whole app).
    /// </summary>
    public class MainWindowStatusResetTests
    {
        /// <summary>Runs <paramref name="body"/> on an STA thread and pumps its dispatcher for <paramref name="pumpMs"/>.</summary>
        private static void RunOnStaAndPump(Action body, int pumpMs)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    body();
                    var frame = new DispatcherFrame();
                    var end = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(pumpMs) };
                    end.Tick += (s, e) => { end.Stop(); frame.Continue = false; };
                    end.Start();
                    Dispatcher.PushFrame(frame);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    // This thread's own dispatcher only - never the Application.
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            thread.Join(TimeSpan.FromSeconds(20)).Should().BeTrue("the dispatcher pump must finish");
            if (failure != null) throw new Xunit.Sdk.XunitException("STA body threw: " + failure);
        }

        [Fact]
        public void RestartOneShot_ANewResetCancelsThePendingOne()
        {
            int staleFired = 0, currentFired = 0;
            DispatcherTimer? slot = null;

            RunOnStaAndPump(() =>
            {
                MainWindow.RestartOneShot(ref slot, 50, () => staleFired++);   // dictation A's reset
                MainWindow.RestartOneShot(ref slot, 200, () => currentFired++); // dictation B's reset
            }, pumpMs: 700);

            staleFired.Should().Be(0, "a stale reset must not cut a newer status message short");
            currentFired.Should().Be(1, "the latest reset fires exactly once");
        }

        [Fact]
        public void RestartOneShot_StoppingTheSlotCancelsTheReset()
        {
            int fired = 0;
            DispatcherTimer? slot = null;

            RunOnStaAndPump(() =>
            {
                MainWindow.RestartOneShot(ref slot, 50, () => fired++);
                slot!.Stop(); // what RecordingStarted / window close do
            }, pumpMs: 300);

            fired.Should().Be(0);
        }

        [Theory]
        [InlineData(5, "Stopped after 5 minutes (hot-mic safety). Transcribing what was captured.")]
        [InlineData(7, "Stopped after 7 minutes (hot-mic safety). Transcribing what was captured.")]
        [InlineData(1, "Stopped after 1 minute (hot-mic safety). Transcribing what was captured.")]
        public void DescribeAutoStop_UsesTheRealLimit(int minutes, string expected)
        {
            MainWindow.DescribeAutoStop(TimeSpan.FromMinutes(minutes)).Should().Be(expected);
        }

        [Fact]
        public void DescribeAutoStop_DefaultLimit_KeepsTheShippedWording()
        {
            MainWindow.DescribeAutoStop(DictationController.DefaultAutoStopAfter)
                .Should().Be("Stopped after 5 minutes (hot-mic safety). Transcribing what was captured.");
        }

        [Fact]
        public void DescribeAutoStop_WhenTheAudioWasLost_SaysSoInsteadOfTranscribing()
        {
            MainWindow.DescribeAutoStop(TimeSpan.FromMinutes(5), "Recording failed - audio was not saved")
                .Should().Be("Stopped after 5 minutes (hot-mic safety). Recording failed - audio was not saved.");
        }

        // ---- source tripwires: ShowDictationNotice is WPF code-behind with no seam, so these only
        // check that the wiring is still written down. They do not prove the UI behaviour.

        // A missing source file FAILS the tripwire (SourceTripwirePolicyTests), it is not skipped.
        private static string MainWindowSource() => RepoSourceFile.ReadApp("MainWindow.xaml.cs");

        private static string CaseBlock(string source, string kind)
        {
            var start = source.IndexOf($"case DictationEventKind.{kind}:", StringComparison.Ordinal);
            start.Should().BeGreaterThan(0, $"ShowDictationNotice must handle {kind}");
            var end = source.IndexOf("case DictationEventKind.", start + 10, StringComparison.Ordinal);
            return source.Substring(start, end - start);
        }

        [Fact]
        public void Tripwire_NoSpeechStatus_IsResetToReady()
        {
            var source = MainWindowSource();
            CaseBlock(source, "NoSpeech").Should().Contain("ResetToReadyLater(",
                "the no-speech status is transient like \"Error\" and must not stick until the next dictation");
        }

        [Fact]
        public void Tripwire_RecordingStarted_CancelsThePendingReset()
        {
            var source = MainWindowSource();
            CaseBlock(source, "RecordingStarted").Should().Contain("CancelReadyReset(",
                "a reset armed by the previous dictation must not fire into this one's messages");
        }

        [Fact]
        public void Tripwire_AutoStopBalloon_HasNoHardcodedMinutes()
        {
            var source = MainWindowSource();
            var block = CaseBlock(source, "AutoStopped");
            block.Should().Contain("DescribeAutoStop(");
            block.Should().Contain("ev.Message", "a lost recording must not be announced as \"Transcribing what was captured\"");
            block.Should().NotContain("5 minutes");
        }
    }
}

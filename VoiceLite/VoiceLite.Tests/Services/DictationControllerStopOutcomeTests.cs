using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using VoiceLite.Models;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// A non-cancel stop must end in exactly one outcome: audio, or an error. When the recorder
    /// produces neither (it had already died), the UI used to sit on "Processing..." forever.
    /// </summary>
    public partial class DictationControllerTests
    {
        [Fact]
        public void PushToTalk_RecorderDiedSilently_ReleaseReportsFailureNotProcessing()
        {
            var c = Create();

            c.HotkeyPressed();
            recorder.SimulateSilentCaptureFailure();
            c.HotkeyReleased();

            Kinds.Should().Equal(
                DictationEventKind.RecordingStarted,
                DictationEventKind.RecordingStopped,
                DictationEventKind.RecordingFailed);
            Find(DictationEventKind.RecordingFailed)!.Message.Should().Contain("not saved");
            c.State.Should().Be(DictationState.Idle);

            Thread.Sleep(300); // past the hotkey debounce
            c.HotkeyPressed();
            recorder.StartCalls.Should().Be(2, "the next press records normally");
            c.State.Should().Be(DictationState.Recording);
        }

        [Fact]
        public void PushToTalk_StopProducesNeitherAudioNorError_ReportsFailure()
        {
            // Device lost mid-recording: the recorder still says IsRecording, but its
            // StopRecording raises nothing because the writer is already gone.
            recorder.StopProducesNothing = true;
            var c = Create();

            c.HotkeyPressed();
            c.HotkeyReleased();

            Kinds.Should().Equal(
                DictationEventKind.RecordingStarted,
                DictationEventKind.RecordingStopped,
                DictationEventKind.RecordingFailed);
            c.State.Should().Be(DictationState.Idle);
        }

        [Fact]
        public void Toggle_RecorderDiedSilently_SecondPressReportsFailureAndThirdPressRecords()
        {
            settings.Mode = RecordMode.Toggle;
            var c = Create();

            c.HotkeyPressed();
            recorder.SimulateSilentCaptureFailure();
            Thread.Sleep(300);
            c.HotkeyPressed();

            Kinds.Should().EndWith(DictationEventKind.RecordingFailed);
            c.State.Should().Be(DictationState.Idle);

            Thread.Sleep(300);
            c.HotkeyPressed();
            recorder.StartCalls.Should().Be(2);
            recorder.IsRecording.Should().BeTrue();
        }

        [Fact]
        public async Task HotMicLimit_OnADeadRecorder_BalloonSaysTheAudioWasLost()
        {
            settings.Mode = RecordMode.Toggle;
            var c = Create(autoStop: TimeSpan.FromMilliseconds(50));

            c.HotkeyPressed();
            recorder.SimulateSilentCaptureFailure();
            await WaitForKind(DictationEventKind.AutoStopped);

            // The status line is invisible when the window sits in the tray, so the balloon still
            // goes out - but it must carry the failure instead of "Transcribing what was captured".
            Kinds.Should().ContainInOrder(DictationEventKind.RecordingFailed, DictationEventKind.AutoStopped);
            Find(DictationEventKind.AutoStopped)!.Message.Should().Contain("not saved");
        }

        [Fact]
        public async Task HotMicLimit_OnAHealthyRecorder_BalloonCarriesNoFailure()
        {
            settings.Mode = RecordMode.Toggle;
            var c = Create(autoStop: TimeSpan.FromMilliseconds(50));

            c.HotkeyPressed();
            await WaitForKind(DictationEventKind.AutoStopped);

            Find(DictationEventKind.AutoStopped)!.Message.Should().BeNull();
        }

        [Fact]
        public void Cancel_OnADeadRecorder_StaysCancelled()
        {
            var c = Create();

            c.HotkeyPressed();
            recorder.SimulateSilentCaptureFailure();
            c.Cancel();

            Kinds.Should().Equal(DictationEventKind.RecordingStarted, DictationEventKind.RecordingCancelled);
        }

        [Fact]
        public async Task NormalStop_ProducesExactlyOneOutcome()
        {
            var c = Create();

            c.HotkeyPressed();
            c.HotkeyReleased();
            await WaitForKind(DictationEventKind.TranscriptionCompleted);

            Kinds.Should().NotContain(DictationEventKind.RecordingFailed);
        }

        [Fact]
        public void AutoStopAfter_ExposesTheLimitInUse()
        {
            Create(autoStop: TimeSpan.FromMinutes(7)).AutoStopAfter.Should().Be(TimeSpan.FromMinutes(7));
            controller!.Dispose();
            Create().AutoStopAfter.Should().Be(DictationController.DefaultAutoStopAfter);
        }

        [Fact]
        public void ErrorOnStop_IsReportedOnce()
        {
            recorder.EmitAudioOnStop = false;
            recorder.ErrorOnStop = new System.IO.IOException("disk full");
            var c = Create();

            c.HotkeyPressed();
            c.HotkeyReleased();

            Kinds.Should().Equal(
                DictationEventKind.RecordingStarted,
                DictationEventKind.RecordingStopped,
                DictationEventKind.RecordingFailed);
        }
    }
}

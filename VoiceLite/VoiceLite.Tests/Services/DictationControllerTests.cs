using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using VoiceLite.Models;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// The dictation state machine, driven through fakes: every transition the hotkey,
    /// the record button, Esc, the hot-mic limit and the transcription watchdog can cause.
    /// </summary>
    public class DictationControllerTests : IDisposable
    {
        private sealed class FakeRecorder : IDictationRecorder
        {
            public bool IsRecording { get; private set; }
            public string? ActiveDeviceName { get; set; } = "Fake Mic";
            public double LastPeakPercent { get; set; } = 50;
            public bool FailToStart { get; set; }
            public bool EmitAudioOnStop { get; set; } = true;
            public Exception? ErrorOnStop { get; set; }
            public int StartCalls;
            public int StopCalls;
            public readonly List<string> EmittedFiles = new();

            public event EventHandler<string>? AudioFileReady;
            public event EventHandler<Exception>? RecordingError;

            public void StartRecording()
            {
                StartCalls++;
                if (FailToStart) throw new InvalidOperationException("no device");
                IsRecording = true;
            }

            public void StopRecording()
            {
                StopCalls++;
                IsRecording = false;
                if (ErrorOnStop != null)
                {
                    RecordingError?.Invoke(this, ErrorOnStop);
                    return;
                }
                if (EmitAudioOnStop)
                {
                    var path = Path.Combine(Path.GetTempPath(), $"dictation-test-{Guid.NewGuid():N}.wav");
                    File.WriteAllBytes(path, new byte[4]);
                    EmittedFiles.Add(path);
                    AudioFileReady?.Invoke(this, path); // synchronous, exactly like AudioRecorder
                }
            }
        }

        private sealed class FakeTranscriber : IDictationTranscriber
        {
            private long counter;
            public bool EffectiveTranslateToEnglish { get; set; }
            public Func<string, Task<string>> Transcribe { get; set; } = _ => Task.FromResult("hello world");
            public long BeginDictationSession() => Interlocked.Increment(ref counter);
            public void ExpireDictationSessions() => Interlocked.Increment(ref counter);
            public bool IsDictationSessionCurrent(long sessionId) => Interlocked.Read(ref counter) == sessionId;
            public Task<string> TranscribeAsync(string audioFilePath) => Transcribe(audioFilePath);
        }

        private readonly FakeRecorder recorder = new();
        private readonly FakeTranscriber transcriber = new();
        private readonly Settings settings = new() { Mode = RecordMode.PushToTalk };
        private readonly List<DictationEvent> events = new();
        private DictationController? controller;

        private DictationController Create(TimeSpan? autoStop = null, TimeSpan? stuck = null)
        {
            controller = new DictationController(recorder, transcriber, () => settings,
                autoStopAfter: autoStop, stuckAfter: stuck);
            controller.Notice += (_, e) => { lock (events) events.Add(e); };
            return controller;
        }

        private List<DictationEventKind> Kinds { get { lock (events) return events.Select(e => e.Kind).ToList(); } }
        private DictationEvent? Find(DictationEventKind kind) { lock (events) return events.FirstOrDefault(e => e.Kind == kind); }

        private static async Task WaitFor(Func<bool> condition, int timeoutMs = 3000)
        {
            var sw = Stopwatch.StartNew();
            while (!condition() && sw.ElapsedMilliseconds < timeoutMs) await Task.Delay(10);
        }

        private Task WaitForKind(DictationEventKind kind) => WaitFor(() => Kinds.Contains(kind));

        public void Dispose()
        {
            controller?.Dispose();
            foreach (var f in recorder.EmittedFiles) { try { File.Delete(f); } catch { } }
        }

        [Fact]
        public async Task PushToTalk_PressStartsReleaseStopsAndTranscribes()
        {
            var c = Create();

            c.HotkeyPressed();
            recorder.IsRecording.Should().BeTrue();
            c.State.Should().Be(DictationState.Recording);

            c.HotkeyReleased();
            await WaitForKind(DictationEventKind.TranscriptionCompleted);

            Kinds.Should().Equal(
                DictationEventKind.RecordingStarted,
                DictationEventKind.RecordingStopped,
                DictationEventKind.TranscriptionCompleted);
            Find(DictationEventKind.TranscriptionCompleted)!.Text.Should().Be("hello world");
            await WaitFor(() => c.State == DictationState.Idle);
            c.State.Should().Be(DictationState.Idle);
            File.Exists(recorder.EmittedFiles.Single()).Should().BeFalse("temp audio is deleted after transcription");
        }

        [Fact]
        public void Toggle_SecondPressStops_ReleaseIsIgnored()
        {
            settings.Mode = RecordMode.Toggle;
            var c = Create();

            c.HotkeyPressed();
            c.HotkeyReleased();
            recorder.IsRecording.Should().BeTrue("toggle mode ignores key release");

            Thread.Sleep(300); // past the hotkey debounce
            c.HotkeyPressed();
            recorder.IsRecording.Should().BeFalse();
            recorder.StopCalls.Should().Be(1);
            Kinds.Should().Contain(DictationEventKind.RecordingStopped);
        }

        [Fact]
        public void PushToTalk_ReleaseDoesNotStopARecordingTheHotkeyDidNotStart()
        {
            var c = Create();

            c.Toggle(); // record button
            c.HotkeyReleased();

            recorder.IsRecording.Should().BeTrue();
            recorder.StopCalls.Should().Be(0);
        }

        [Fact]
        public void Debounce_IgnoresASecondPressWithin250ms()
        {
            settings.Mode = RecordMode.Toggle;
            var c = Create();

            c.HotkeyPressed();
            c.HotkeyPressed();

            recorder.IsRecording.Should().BeTrue();
            recorder.StopCalls.Should().Be(0);
        }

        [Fact]
        public async Task PressWhileTranscribing_IsRefusedWithBusy()
        {
            var tcs = new TaskCompletionSource<string>();
            transcriber.Transcribe = _ => tcs.Task;
            var c = Create();

            c.HotkeyPressed();
            c.HotkeyReleased();
            c.State.Should().Be(DictationState.Transcribing);

            Thread.Sleep(300);
            c.HotkeyPressed();
            Kinds.Should().Contain(DictationEventKind.Busy);
            recorder.StartCalls.Should().Be(1);

            tcs.SetResult("done");
            await WaitFor(() => c.State == DictationState.Idle);
            c.State.Should().Be(DictationState.Idle);
        }

        [Fact]
        public void Cancel_DiscardsTheAudioWithoutTranscribing()
        {
            var c = Create();

            c.HotkeyPressed();
            c.Cancel();

            Kinds.Should().Equal(DictationEventKind.RecordingStarted, DictationEventKind.RecordingCancelled);
            File.Exists(recorder.EmittedFiles.Single()).Should().BeFalse("cancelled audio is deleted, never transcribed");
            c.State.Should().Be(DictationState.Idle);
        }

        [Fact]
        public async Task EmptyTranscription_ReportsNoSpeechWithMicDiagnosis()
        {
            transcriber.Transcribe = _ => Task.FromResult("   ");
            recorder.LastPeakPercent = 0.2;
            var c = Create();

            c.HotkeyPressed();
            c.HotkeyReleased();
            await WaitForKind(DictationEventKind.NoSpeech);

            var ev = Find(DictationEventKind.NoSpeech)!;
            ev.MicName.Should().Be("Fake Mic");
            ev.MicSilent.Should().BeTrue();
            Kinds.Should().NotContain(DictationEventKind.TranscriptionCompleted);
        }

        [Fact]
        public async Task HotMicLimit_StopsAToggleRecordingByItself()
        {
            settings.Mode = RecordMode.Toggle;
            var c = Create(autoStop: TimeSpan.FromMilliseconds(50));

            c.HotkeyPressed();
            await WaitForKind(DictationEventKind.AutoStopped);

            recorder.IsRecording.Should().BeFalse();
            Kinds.Should().ContainInOrder(DictationEventKind.RecordingStopped, DictationEventKind.AutoStopped);
            await WaitForKind(DictationEventKind.TranscriptionCompleted);
            Kinds.Should().Contain(DictationEventKind.TranscriptionCompleted, "the captured audio is still transcribed");
        }

        [Fact]
        public async Task HotMicLimit_DoesNotApplyToPushToTalk()
        {
            var c = Create(autoStop: TimeSpan.FromMilliseconds(50));

            c.HotkeyPressed();
            await Task.Delay(200);

            recorder.IsRecording.Should().BeTrue();
            Kinds.Should().NotContain(DictationEventKind.AutoStopped);
        }

        [Fact]
        public async Task Watchdog_ResetsStateAndDiscardsTheLateResult()
        {
            var tcs = new TaskCompletionSource<string>();
            transcriber.Transcribe = _ => tcs.Task;
            var c = Create(stuck: TimeSpan.FromMilliseconds(50));

            c.HotkeyPressed();
            c.HotkeyReleased();
            await WaitForKind(DictationEventKind.StuckRecovered);
            c.IsTranscribing.Should().BeFalse();
            c.State.Should().Be(DictationState.Idle);

            tcs.SetResult("late text that must never be typed");
            await Task.Delay(100);
            Kinds.Should().NotContain(DictationEventKind.TranscriptionCompleted);

            c.Toggle();
            recorder.StartCalls.Should().Be(2, "a new dictation can start after recovery");
        }

        [Fact]
        public void TooShortRecording_IsReportedAsTooShortNotAsFailure()
        {
            recorder.EmitAudioOnStop = false;
            recorder.ErrorOnStop = new InvalidOperationException("Recording too short (120ms)");
            var c = Create();

            c.HotkeyPressed();
            c.HotkeyReleased();

            Kinds.Should().Contain(DictationEventKind.RecordingTooShort);
            Kinds.Should().NotContain(DictationEventKind.RecordingFailed);
            c.State.Should().Be(DictationState.Idle);
        }

        [Fact]
        public void TooShortAfterCancel_IsSuppressed()
        {
            recorder.EmitAudioOnStop = false;
            recorder.ErrorOnStop = new InvalidOperationException("Recording too short (120ms)");
            var c = Create();

            c.HotkeyPressed();
            c.Cancel();

            Kinds.Should().Equal(DictationEventKind.RecordingStarted, DictationEventKind.RecordingCancelled);
        }

        [Fact]
        public void SaveFailure_IsReportedAsRecordingFailed()
        {
            recorder.EmitAudioOnStop = false;
            recorder.ErrorOnStop = new IOException("disk full");
            var c = Create();

            c.HotkeyPressed();
            c.HotkeyReleased();

            Find(DictationEventKind.RecordingFailed)!.Message.Should().Contain("not saved");
        }

        [Theory]
        [InlineData(false, "Transcription error")]
        [InlineData(true, "Translation error")]
        public async Task TranscriberException_IsReportedAsGenericError(bool translating, string expected)
        {
            transcriber.EffectiveTranslateToEnglish = translating;
            transcriber.Transcribe = _ => Task.FromException<string>(new InvalidOperationException("boom"));
            var c = Create();

            c.HotkeyPressed();
            c.HotkeyReleased();
            await WaitForKind(DictationEventKind.TranscriptionFailed);

            Find(DictationEventKind.TranscriptionFailed)!.Message.Should().Be(expected);
            await WaitFor(() => c.State == DictationState.Idle);
            c.State.Should().Be(DictationState.Idle);
        }

        [Fact]
        public async Task MissingModel_IsReportedVerbatim()
        {
            transcriber.Transcribe = _ => Task.FromException<string>(new FileNotFoundException("Model not found - download it from Settings"));
            var c = Create();

            c.HotkeyPressed();
            c.HotkeyReleased();
            await WaitForKind(DictationEventKind.TranscriptionFailed);

            Find(DictationEventKind.TranscriptionFailed)!.Message.Should().Be("Model not found - download it from Settings");
        }

        [Fact]
        public void RecorderFailsToStart_RollsBackAndCanRetry()
        {
            recorder.FailToStart = true;
            var c = Create();

            c.HotkeyPressed();
            Kinds.Should().Equal(DictationEventKind.RecordingStarted, DictationEventKind.RecordingFailed);
            c.State.Should().Be(DictationState.Idle);

            recorder.FailToStart = false;
            c.Toggle();
            recorder.IsRecording.Should().BeTrue();
        }

        [Fact]
        public void Dispose_DetachesFromTheRecorder()
        {
            var c = Create();
            c.HotkeyPressed();
            c.Dispose();

            recorder.StopRecording(); // raises AudioFileReady into a disposed controller
            Kinds.Should().NotContain(DictationEventKind.RecordingStopped);
            Kinds.Should().NotContain(DictationEventKind.TranscriptionCompleted);
        }
    }
}

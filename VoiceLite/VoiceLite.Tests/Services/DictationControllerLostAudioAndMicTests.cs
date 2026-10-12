using System;
using System.Collections.Generic;
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
    /// v2.5.2 review #15: the hot-mic auto-stop balloon is the only surface while the window sits
    /// in the tray. It said "Transcribing what was captured" whenever the stop produced ANY
    /// outcome - including RecordingError (save failed) and a throwing StopRecording - so five
    /// minutes of dictation vanished behind a message promising a transcription.
    ///
    /// v2.5.2 review #13: a microphone change saved while a recording is running cannot be
    /// applied (AudioRecorder.SetDevice throws while recording) and nothing re-applied it, so
    /// every later recording used the OLD device while the UI showed the new one. The controller
    /// now lets its owner prepare the recorder from the current settings before every recording.
    /// </summary>
    public partial class DictationControllerTests
    {
        // ------------------------------------------------------------------ #15

        [Fact]
        public async Task HotMicLimit_SaveFailedOnStop_BalloonSaysTheAudioWasLost()
        {
            settings.Mode = RecordMode.Toggle;
            recorder.EmitAudioOnStop = false;
            recorder.ErrorOnStop = new System.IO.IOException("disk full");
            var c = Create(autoStop: TimeSpan.FromMilliseconds(50));

            c.HotkeyPressed();
            await WaitForKind(DictationEventKind.AutoStopped);

            Find(DictationEventKind.AutoStopped)!.Message.Should().Contain("not saved",
                "RecordingError means nothing will be transcribed; the balloon must not promise it");
            Kinds.Count(k => k == DictationEventKind.RecordingFailed).Should().Be(1,
                "the recorder's own error is the one failure notice - no duplicate from the no-outcome check");
        }

        [Fact]
        public async Task HotMicLimit_StopRecordingThrows_BalloonSaysTheAudioWasLost()
        {
            settings.Mode = RecordMode.Toggle;
            var throwing = new ThrowingStopRecorder();
            var notices = new List<DictationEvent>();
            using var c = new DictationController(throwing, transcriber, () => settings,
                autoStopAfter: TimeSpan.FromMilliseconds(50));
            c.Notice += (_, e) => { lock (notices) notices.Add(e); };

            c.HotkeyPressed();
            await WaitFor(() => { lock (notices) return notices.Any(e => e.Kind == DictationEventKind.AutoStopped); });

            DictationEvent? autoStopped;
            int failures;
            lock (notices)
            {
                autoStopped = notices.FirstOrDefault(e => e.Kind == DictationEventKind.AutoStopped);
                failures = notices.Count(e => e.Kind == DictationEventKind.RecordingFailed);
            }
            autoStopped.Should().NotBeNull();
            autoStopped!.Message.Should().NotBeNull("a stop that threw kept no audio");
            failures.Should().Be(1, "'Failed to stop recording' is reported once");
        }

        [Fact]
        public async Task HotMicLimit_TooShortOnStop_BalloonDoesNotPromiseATranscription()
        {
            settings.Mode = RecordMode.Toggle;
            recorder.EmitAudioOnStop = false;
            recorder.ErrorOnStop = new InvalidOperationException("Recording too short (0.1s)");
            var c = Create(autoStop: TimeSpan.FromMilliseconds(50));

            c.HotkeyPressed();
            await WaitForKind(DictationEventKind.AutoStopped);

            Find(DictationEventKind.AutoStopped)!.Message.Should().NotBeNull();
        }

        private sealed class ThrowingStopRecorder : IDictationRecorder
        {
            public bool IsRecording { get; private set; }
            public string? ActiveDeviceName => "Fake Mic";
            public double LastPeakPercent => 50;
#pragma warning disable CS0067 // never raised: this recorder's stop throws instead
            public event EventHandler<string>? AudioFileReady;
            public event EventHandler<Exception>? RecordingError;
#pragma warning restore CS0067
            public void StartRecording() => IsRecording = true;
            public void StopRecording()
            {
                IsRecording = false;
                throw new InvalidOperationException("waveIn dispose failed");
            }
        }

        // ------------------------------------------------------------------ #13

        [Fact]
        public void EveryRecording_PreparesTheRecorderFromTheCurrentSettings_BeforeItStarts()
        {
            settings.SelectedMicrophoneIndex = 1;
            settings.SelectedMicrophoneName = "Old Mic";
            var prepared = new List<(string? Name, int StartCallsSoFar)>();
            controller = new DictationController(recorder, transcriber, () => settings,
                prepareRecorder: s => prepared.Add((s.SelectedMicrophoneName, recorder.StartCalls)));

            controller.HotkeyPressed();

            // Settings saved a new microphone WHILE recording: it could not be applied then.
            settings.SelectedMicrophoneIndex = 2;
            settings.SelectedMicrophoneName = "New Mic";
            controller.HotkeyReleased();

            Thread.Sleep(300); // past the hotkey debounce
            controller.HotkeyPressed();

            prepared.Should().Equal(("Old Mic", 0), ("New Mic", 1));
            recorder.StartCalls.Should().Be(2);
        }

        [Fact]
        public void PrepareRecorderThrowing_DoesNotBlockTheRecording()
        {
            controller = new DictationController(recorder, transcriber, () => settings,
                prepareRecorder: _ => throw new InvalidOperationException("Cannot change device while recording"));
            controller.Notice += (_, e) => { lock (events) events.Add(e); };

            controller.HotkeyPressed();

            recorder.IsRecording.Should().BeTrue("a failed device push falls back to the device already set");
            Kinds.Should().Equal(DictationEventKind.RecordingStarted);
        }
    }
}

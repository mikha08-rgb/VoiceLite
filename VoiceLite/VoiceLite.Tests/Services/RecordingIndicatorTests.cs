using System;
using System.Collections.Generic;
using AwesomeAssertions;
using VoiceLite.Services;
using VoiceLite.Utilities;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// RecordingIndicator sits between dictation and the pill window. Its one hard promise:
    /// whatever the window does (throws on create, throws on show), dictation never notices.
    /// </summary>
    public class RecordingIndicatorTests
    {
        private sealed class FakeView : IRecordingIndicatorView
        {
            public readonly List<string> Calls = new();
            public bool Throw;

            public void ShowState(RecordingIndicatorState state)
            {
                Calls.Add("show:" + state);
                if (Throw) throw new InvalidOperationException("view broke");
            }

            public void HideIndicator()
            {
                Calls.Add("hide");
                if (Throw) throw new InvalidOperationException("view broke");
            }

            public void CloseView()
            {
                Calls.Add("close");
                if (Throw) throw new InvalidOperationException("view broke");
            }
        }

        private static RecordingIndicator Make(FakeView view, Func<bool>? enabled = null, Func<bool>? active = null) =>
            new(enabled ?? (() => true), active, _ => view);

        [Fact]
        public void WalksTheViewThroughADictation()
        {
            var view = new FakeView();
            var indicator = Make(view);

            indicator.OnDictationEvent(DictationEventKind.RecordingStarted);
            indicator.OnDictationEvent(DictationEventKind.RecordingStopped);
            indicator.OnDictationEvent(DictationEventKind.TranscriptionCompleted);

            view.Calls.Should().Equal("show:Recording", "show:Processing", "hide");
        }

        [Fact]
        public void ViewIsCreatedLazily_AndOnlyOnce()
        {
            int created = 0;
            var view = new FakeView();
            var indicator = new RecordingIndicator(() => true, null, _ => { created++; return view; });

            indicator.OnDictationEvent(DictationEventKind.TranscriptionCompleted); // nothing to hide
            indicator.OnDictationEvent(DictationEventKind.Busy);
            created.Should().Be(0, "no window until the first recording");

            indicator.OnDictationEvent(DictationEventKind.RecordingStarted);
            indicator.OnDictationEvent(DictationEventKind.RecordingCancelled);
            indicator.OnDictationEvent(DictationEventKind.RecordingStarted);
            created.Should().Be(1);
        }

        [Fact]
        public void RepeatedNotices_DoNotRepeatViewCalls()
        {
            var view = new FakeView();
            var indicator = Make(view);

            indicator.OnDictationEvent(DictationEventKind.RecordingStarted);
            indicator.OnDictationEvent(DictationEventKind.Busy);
            indicator.OnDictationEvent(DictationEventKind.RecordingStopped);
            indicator.OnDictationEvent(DictationEventKind.AutoStopped);
            indicator.OnDictationEvent(DictationEventKind.NoSpeech);
            indicator.OnDictationEvent(DictationEventKind.Fault);

            view.Calls.Should().Equal("show:Recording", "show:Processing", "hide");
        }

        [Fact]
        public void SettingOff_NeverCreatesOrShowsTheView()
        {
            int created = 0;
            var indicator = new RecordingIndicator(() => false, null, _ => { created++; return new FakeView(); });

            indicator.OnDictationEvent(DictationEventKind.RecordingStarted);
            indicator.OnDictationEvent(DictationEventKind.RecordingStopped);
            indicator.OnDictationEvent(DictationEventKind.TranscriptionCompleted);

            created.Should().Be(0);
        }

        [Fact]
        public void SettingTurnedOffWhileRecording_HidesOnRefresh_AndBackOnShowsTheCurrentState()
        {
            bool enabled = true;
            var view = new FakeView();
            var indicator = Make(view, () => enabled);

            indicator.OnDictationEvent(DictationEventKind.RecordingStarted);
            enabled = false;
            indicator.Refresh();
            view.Calls.Should().Equal("show:Recording", "hide");

            // The dictation carries on while hidden; turning the setting back on shows where it is now.
            indicator.OnDictationEvent(DictationEventKind.RecordingStopped);
            enabled = true;
            indicator.Refresh();
            view.Calls.Should().Equal("show:Recording", "hide", "show:Processing");
        }

        [Fact]
        public void AThrowingView_NeverReachesTheCaller_AndIsLoggedOnce()
        {
            var view = new FakeView { Throw = true };
            var warnings = new List<string>();
            var indicator = Make(view);
            indicator.LogWarning = warnings.Add;

            var act = () =>
            {
                indicator.OnDictationEvent(DictationEventKind.RecordingStarted);
                indicator.OnDictationEvent(DictationEventKind.RecordingStopped);
                indicator.OnDictationEvent(DictationEventKind.TranscriptionFailed);
                indicator.Refresh();
                indicator.SafetyTick();
                indicator.Close();
            };

            act.Should().NotThrow();
            warnings.Should().HaveCount(1, "one line in voicelite.log, not one per dictation");
        }

        [Fact]
        public void AThrowingFactory_NeverReachesTheCaller_AndStopsBeingRetried()
        {
            int attempts = 0;
            var indicator = new RecordingIndicator(() => true, null, _ => { attempts++; throw new InvalidOperationException("XAML broke"); });
            indicator.LogWarning = _ => { };

            var act = () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    indicator.OnDictationEvent(DictationEventKind.RecordingStarted);
                    indicator.OnDictationEvent(DictationEventKind.RecordingCancelled);
                }
            };

            act.Should().NotThrow();
            attempts.Should().BeLessThanOrEqualTo(RecordingIndicator.MaxFailures,
                "a window that cannot be built must not cost a XamlParseException on every hotkey press");
        }

        [Fact]
        public void AThrowingSettingsOrActivityProbe_NeverReachesTheCaller()
        {
            var view = new FakeView();
            var indicator = new RecordingIndicator(() => throw new InvalidOperationException("settings"),
                () => throw new InvalidOperationException("probe"), _ => view);
            indicator.LogWarning = _ => { };

            var act = () =>
            {
                indicator.OnDictationEvent(DictationEventKind.RecordingStarted);
                indicator.SafetyTick();
                indicator.SafetyTick();
            };
            act.Should().NotThrow();
        }

        // ------------------------------------------------------------------ safety auto-hide

        [Fact]
        public void SafetyTick_HidesAPillThatOutlivedItsDictation()
        {
            bool active = true;
            var view = new FakeView();
            var warnings = new List<string>();
            var indicator = Make(view, active: () => active);
            indicator.LogWarning = warnings.Add;

            indicator.OnDictationEvent(DictationEventKind.RecordingStarted);
            indicator.OnDictationEvent(DictationEventKind.RecordingStopped);

            indicator.SafetyTick();
            view.Calls.Should().NotContain("hide", "the controller is still transcribing");

            active = false; // ... and the terminal notice never arrived
            indicator.SafetyTick();
            view.Calls.Should().NotContain("hide", "one idle reading is not enough");
            indicator.SafetyTick();
            view.Calls.Should().EndWith("hide");
            warnings.Should().ContainSingle().Which.Should().Contain("safety");

            // The next dictation works as usual.
            active = true;
            indicator.OnDictationEvent(DictationEventKind.RecordingStarted);
            view.Calls.Should().EndWith("show:Recording");
        }

        [Fact]
        public void SafetyTick_LeavesALongRecordingAlone()
        {
            var view = new FakeView();
            var indicator = Make(view, active: () => true);

            indicator.OnDictationEvent(DictationEventKind.RecordingStarted);
            for (int i = 0; i < 1000; i++) // push-to-talk has no time limit
                indicator.SafetyTick();

            view.Calls.Should().Equal("show:Recording");
        }

        // ------------------------------------------------------------------ level

        [Fact]
        public void Level_IsOnlyTakenWhileRecording_AndGoesStale()
        {
            long now = 1000;
            var indicator = Make(new FakeView());
            indicator.Clock = () => now;

            indicator.OnLevel(this, 0.7f);
            indicator.CurrentLevel().Should().Be(0f, "hidden: updates are dropped");

            indicator.OnDictationEvent(DictationEventKind.RecordingStarted);
            indicator.OnLevel(this, 0.7f);
            indicator.CurrentLevel().Should().Be(0.7f);

            now += RecordingIndicator.LevelStaleAfterMs + 1;
            indicator.CurrentLevel().Should().Be(0f, "the recorder stopped delivering buffers: the bars must settle, not freeze");

            indicator.OnLevel(this, 0.4f);
            indicator.OnDictationEvent(DictationEventKind.RecordingStopped);
            indicator.CurrentLevel().Should().Be(0f, "processing shows the shimmer, not the last level");
            indicator.OnLevel(this, 0.9f);
            indicator.CurrentLevel().Should().Be(0f);
        }

        [Fact]
        public void Level_FromTheViewFactory_IsTheSameSource()
        {
            Func<float>? source = null;
            var indicator = new RecordingIndicator(() => true, null, level => { source = level; return new FakeView(); });
            indicator.Clock = () => 5;

            indicator.OnDictationEvent(DictationEventKind.RecordingStarted);
            indicator.OnLevel(this, 0.25f);

            source.Should().NotBeNull();
            source!().Should().Be(0.25f);
        }

        [Fact]
        public void Close_ClosesTheView_AndLaterNoticesDoNothing()
        {
            int created = 0;
            var view = new FakeView();
            var indicator = new RecordingIndicator(() => true, null, _ => { created++; return view; });

            indicator.OnDictationEvent(DictationEventKind.RecordingStarted);
            indicator.Close();
            indicator.OnDictationEvent(DictationEventKind.RecordingStarted);
            indicator.Refresh();
            indicator.Close();

            view.Calls.Should().Equal("show:Recording", "close");
            created.Should().Be(1, "a closed indicator must not build a new window during shutdown");
        }
    }
}

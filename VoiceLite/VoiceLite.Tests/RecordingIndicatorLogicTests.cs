using System;
using System.Linq;
using AwesomeAssertions;
using VoiceLite.Services;
using VoiceLite.Utilities;
using Xunit;
using Logic = VoiceLite.Utilities.RecordingIndicatorLogic;
using State = VoiceLite.Utilities.RecordingIndicatorState;

namespace VoiceLite.Tests
{
    /// <summary>
    /// The pure half of the recording pill: which dictation notice puts it in which state,
    /// how a mic level becomes bar heights, the smoothing, and where the window goes.
    /// </summary>
    public class RecordingIndicatorLogicTests
    {
        // ------------------------------------------------------------------ state machine

        [Theory]
        [InlineData(DictationEventKind.RecordingStarted, State.Recording)]
        [InlineData(DictationEventKind.RecordingStopped, State.Processing)]
        [InlineData(DictationEventKind.RecordingCancelled, State.Hidden)]
        [InlineData(DictationEventKind.RecordingFailed, State.Hidden)]
        [InlineData(DictationEventKind.RecordingTooShort, State.Hidden)]
        [InlineData(DictationEventKind.TranscriptionCompleted, State.Hidden)]
        [InlineData(DictationEventKind.NoSpeech, State.Hidden)]
        [InlineData(DictationEventKind.TranscriptionFailed, State.Hidden)]
        [InlineData(DictationEventKind.StuckRecovered, State.Hidden)]
        [InlineData(DictationEventKind.Fault, State.Hidden)]
        public void NextState_DoesNotDependOnTheCurrentState(DictationEventKind kind, State expected)
        {
            foreach (var current in Enum.GetValues<State>())
                Logic.NextState(current, kind).Should().Be(expected, $"{kind} from {current}");
        }

        [Theory]
        // Busy: a start was refused because a transcription is still running - the pill keeps
        // showing that transcription. AutoStopped always FOLLOWS RecordingStopped (-> Processing)
        // or RecordingStopped + RecordingFailed (-> Hidden); it must not undo either.
        [InlineData(DictationEventKind.Busy)]
        [InlineData(DictationEventKind.AutoStopped)]
        public void NextState_InformationalNotices_KeepTheCurrentState(DictationEventKind kind)
        {
            foreach (var current in Enum.GetValues<State>())
                Logic.NextState(current, kind).Should().Be(current);
        }

        [Fact]
        public void NextState_CoversEveryDictationEventKind()
        {
            // A kind added to the controller later must be placed deliberately: anything this
            // test does not know about fails here instead of silently leaving the pill up.
            var known = new[]
            {
                DictationEventKind.RecordingStarted, DictationEventKind.RecordingStopped,
                DictationEventKind.RecordingCancelled, DictationEventKind.RecordingFailed,
                DictationEventKind.RecordingTooShort, DictationEventKind.Busy,
                DictationEventKind.TranscriptionCompleted, DictationEventKind.NoSpeech,
                DictationEventKind.TranscriptionFailed, DictationEventKind.AutoStopped,
                DictationEventKind.StuckRecovered, DictationEventKind.Fault,
            };
            Enum.GetValues<DictationEventKind>().Should().BeEquivalentTo(known);
        }

        [Fact]
        public void NextState_UnknownKind_Hides()
        {
            // Hidden is the safe direction: a pill that is wrongly gone costs nothing, a pill
            // that is wrongly stuck on screen is a bug report.
            Logic.NextState(State.Recording, (DictationEventKind)999).Should().Be(State.Hidden);
        }

        [Fact]
        public void EveryTerminalPathOfADictation_EndsHidden()
        {
            DictationEventKind[][] paths =
            {
                new[] { DictationEventKind.RecordingStarted, DictationEventKind.RecordingStopped, DictationEventKind.TranscriptionCompleted },
                new[] { DictationEventKind.RecordingStarted, DictationEventKind.RecordingStopped, DictationEventKind.NoSpeech },
                new[] { DictationEventKind.RecordingStarted, DictationEventKind.RecordingStopped, DictationEventKind.TranscriptionFailed },
                new[] { DictationEventKind.RecordingStarted, DictationEventKind.RecordingStopped, DictationEventKind.RecordingTooShort },
                // stop-outcome timeout / recorder died: Stopped, then Failed
                new[] { DictationEventKind.RecordingStarted, DictationEventKind.RecordingStopped, DictationEventKind.RecordingFailed },
                new[] { DictationEventKind.RecordingStarted, DictationEventKind.RecordingStopped, DictationEventKind.RecordingFailed, DictationEventKind.AutoStopped },
                new[] { DictationEventKind.RecordingStarted, DictationEventKind.RecordingStopped, DictationEventKind.AutoStopped, DictationEventKind.TranscriptionCompleted },
                new[] { DictationEventKind.RecordingStarted, DictationEventKind.RecordingStopped, DictationEventKind.Busy, DictationEventKind.StuckRecovered },
                new[] { DictationEventKind.RecordingStarted, DictationEventKind.RecordingCancelled },
                new[] { DictationEventKind.RecordingStarted, DictationEventKind.RecordingFailed }, // device would not open
                new[] { DictationEventKind.RecordingStarted, DictationEventKind.RecordingCancelled, DictationEventKind.Fault },
            };

            foreach (var path in paths)
            {
                var state = State.Hidden;
                foreach (var kind in path)
                    state = Logic.NextState(state, kind);
                state.Should().Be(State.Hidden, string.Join(" -> ", path));
            }
        }

        // ------------------------------------------------------------------ safety watchdog

        [Fact]
        public void IdleStrikes_CountOnlyWhileThePillIsUpAndDictationIsIdle()
        {
            Logic.NextIdleStrikes(State.Recording, dictationActive: false, strikes: 0).Should().Be(1);
            Logic.NextIdleStrikes(State.Processing, dictationActive: false, strikes: 1).Should().Be(2);
            Logic.NextIdleStrikes(State.Processing, dictationActive: true, strikes: 1).Should().Be(0, "activity resets the count");
            Logic.NextIdleStrikes(State.Hidden, dictationActive: false, strikes: 1).Should().Be(0);
            Logic.IdleStrikesToHide.Should().BeGreaterThan(1,
                "one idle reading can be the gap between the controller going idle and its notice reaching the UI");
        }

        // ------------------------------------------------------------------ level shaping

        [Fact]
        public void ShapeLevel_MapsSilenceToZero_FullScaleToOne_AndIsMonotonic()
        {
            Logic.ShapeLevel(0f).Should().Be(0);
            Logic.ShapeLevel(-1f).Should().Be(0);
            Logic.ShapeLevel(float.NaN).Should().Be(0);
            Logic.ShapeLevel(0.002f).Should().Be(0, "an idle microphone's noise floor must not move the bars");
            Logic.ShapeLevel(1f).Should().Be(1);
            Logic.ShapeLevel(5f).Should().Be(1);

            double previous = 0;
            for (float peak = 0.001f; peak <= 1f; peak *= 1.3f)
            {
                var shaped = Logic.ShapeLevel(peak);
                shaped.Should().BeInRange(0, 1);
                shaped.Should().BeGreaterThanOrEqualTo(previous);
                previous = shaped;
            }
        }

        [Fact]
        public void ShapeLevel_OrdinarySpeech_IsClearlyVisible()
        {
            // Speech peaks sit around 5-30% of full scale; a linear meter would barely move.
            Logic.ShapeLevel(0.1f).Should().BeGreaterThan(0.45);
        }

        // ------------------------------------------------------------------ smoothing

        [Fact]
        public void Smooth_AttacksFasterThanItDecays()
        {
            const double dt = 1.0 / 60;
            var rise = Logic.Smooth(0, 1, dt);
            var fall = 1 - Logic.Smooth(1, 0, dt);
            rise.Should().BeGreaterThan(fall * 2);
        }

        [Fact]
        public void Smooth_NeverOvershoots_AndConverges()
        {
            double value = 0;
            for (int i = 0; i < 120; i++)
            {
                value = Logic.Smooth(value, 0.8, 1.0 / 60);
                value.Should().BeInRange(0, 0.8);
            }
            value.Should().BeApproximately(0.8, 0.001);

            Logic.Smooth(0.2, 0.9, 0).Should().Be(0.2, "no time passed");
            Logic.Smooth(0.2, 0.9, -1).Should().Be(0.2);
            Logic.Smooth(0.2, 0.9, 60).Should().BeApproximately(0.9, 1e-9, "a long stall (sleep, debugger) lands on the target");
            Logic.Smooth(double.NaN, 0.5, 0.016).Should().Be(0.5, "a poisoned value must not stay poisoned");
        }

        // ------------------------------------------------------------------ bars

        [Fact]
        public void BarHeight_Silence_RestsAtTheMinimumForEveryBarAtAnyTime()
        {
            for (int i = 0; i < Logic.BarCount; i++)
                foreach (var t in new[] { 0.0, 0.37, 1.9, 123.4 })
                    Logic.BarHeight(0, i, Logic.BarCount, t).Should().Be(Logic.MinBarHeight);
        }

        [Fact]
        public void BarHeight_StaysInsideThePill()
        {
            for (int i = 0; i < Logic.BarCount; i++)
                for (double t = 0; t < 5; t += 0.013)
                    foreach (var level in new[] { 0.0, 0.3, 1.0, 7.0, double.NaN })
                        Logic.BarHeight(level, i, Logic.BarCount, t).Should().BeInRange(Logic.MinBarHeight, Logic.MaxBarHeight);

            Logic.MaxBarHeight.Should().BeLessThan(Logic.PillHeight - 8);
        }

        [Fact]
        public void BarHeight_IsCentreWeighted()
        {
            double Average(int bar) => Enumerable.Range(0, 600).Average(k => Logic.BarHeight(0.8, bar, Logic.BarCount, k / 60.0));

            int centre = Logic.BarCount / 2;
            Average(centre).Should().BeGreaterThan(Average(0) * 1.4);
            Average(centre).Should().BeGreaterThan(Average(Logic.BarCount - 1) * 1.4);
            Average(1).Should().BeGreaterThan(Average(0));
        }

        [Fact]
        public void BarHeight_GrowsWithTheLevel()
        {
            for (int i = 0; i < Logic.BarCount; i++)
                Logic.BarHeight(0.9, i, Logic.BarCount, 0.5).Should().BeGreaterThan(Logic.BarHeight(0.2, i, Logic.BarCount, 0.5));
        }

        [Fact]
        public void BarHeight_BarsMoveIndependently()
        {
            // Same level, same envelope position (mirror bars 0 and 6): only the per-bar phase differs.
            var differences = Enumerable.Range(0, 120)
                .Select(k => Math.Abs(Logic.BarHeight(1, 0, Logic.BarCount, k / 60.0) - Logic.BarHeight(1, Logic.BarCount - 1, Logic.BarCount, k / 60.0)))
                .ToList();
            differences.Max().Should().BeGreaterThan(0.5, "bars that move in lockstep look like a progress bar, not a voice");
        }

        [Fact]
        public void ProcessingShimmer_IsCalmAndTravels()
        {
            for (int i = 0; i < Logic.BarCount; i++)
                for (double t = 0; t < 4; t += 0.05)
                {
                    Logic.ProcessingBarHeight(i, t).Should().BeInRange(Logic.MinBarHeight, Logic.MaxBarHeight / 2);
                    Logic.ProcessingBarOpacity(i, t).Should().BeInRange(0.2, 1.0);
                }

            // The wave moves left to right: bar i+1 shows a moment later what bar i shows now.
            double lag = Logic.ShimmerBarLagSeconds;
            lag.Should().BeGreaterThan(0);
            Logic.ProcessingBarOpacity(3, 1.0 + lag).Should().BeApproximately(Logic.ProcessingBarOpacity(2, 1.0), 1e-9);
            Logic.ProcessingBarHeight(3, 1.0 + lag).Should().BeApproximately(Logic.ProcessingBarHeight(2, 1.0), 1e-9);
        }

        [Fact]
        public void GlowOpacity_BreathesSlowlyBetweenItsBounds()
        {
            var samples = Enumerable.Range(0, 600).Select(k => Logic.GlowOpacity(k / 60.0)).ToList();
            samples.Min().Should().BeGreaterThanOrEqualTo(0.2);
            samples.Max().Should().BeLessThanOrEqualTo(1.0);
            (samples.Max() - samples.Min()).Should().BeGreaterThan(0.3);
            Logic.GlowPeriodSeconds.Should().BeGreaterThanOrEqualTo(2.0, "breathing, not blinking");
        }

        // ------------------------------------------------------------------ placement

        [Fact]
        public void Placement_At100Percent_CentresThePillAboveTheTaskbar()
        {
            var work = new PixelRect(0, 0, 1920, 1040); // 1080p with a 40 px taskbar
            var window = Logic.Placement(work, 1.0);

            window.Width.Should().Be((int)(Logic.PillWidth + 2 * Logic.ShadowPadding));
            window.Height.Should().Be((int)(Logic.PillHeight + 2 * Logic.ShadowPadding));
            (window.Left + window.Width / 2.0).Should().BeApproximately(960, 0.5);

            // The PILL (not the transparent shadow margin around it) keeps the gap.
            double pillBottom = window.Top + Logic.ShadowPadding + Logic.PillHeight;
            pillBottom.Should().Be(1040 - Logic.BottomGap);
        }

        [Fact]
        public void Placement_ScalesWithDpi_OnAMonitorWithAnOffsetOrigin()
        {
            // Second monitor left of and above the primary, 150%: negative virtual-screen coordinates.
            var work = new PixelRect(-2560, -300, 2560, 1380);
            var window = Logic.Placement(work, 1.5);

            window.Width.Should().Be((int)Math.Round((Logic.PillWidth + 2 * Logic.ShadowPadding) * 1.5));
            window.Height.Should().Be((int)Math.Round((Logic.PillHeight + 2 * Logic.ShadowPadding) * 1.5));
            (window.Left + window.Width / 2.0).Should().BeApproximately(-2560 + 1280, 0.5);

            double pillBottom = window.Top + (Logic.ShadowPadding + Logic.PillHeight) * 1.5;
            pillBottom.Should().BeApproximately(-300 + 1380 - Logic.BottomGap * 1.5, 1.0);
            window.Top.Should().BeGreaterThan(work.Top);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-2.0)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void Placement_NonsenseDpi_FallsBackTo100Percent(double scale)
        {
            var work = new PixelRect(0, 0, 1920, 1040);
            Logic.Placement(work, scale).Should().Be(Logic.Placement(work, 1.0));
        }
    }
}

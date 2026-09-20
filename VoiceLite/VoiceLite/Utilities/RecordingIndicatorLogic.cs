using System;
using VoiceLite.Services;

namespace VoiceLite.Utilities
{
    public enum RecordingIndicatorState { Hidden, Recording, Processing }

    /// <summary>A rectangle in device pixels (virtual-screen coordinates, may be negative).</summary>
    internal readonly record struct PixelRect(int Left, int Top, int Width, int Height)
    {
        public int Right => Left + Width;
        public int Bottom => Top + Height;
    }

    /// <summary>
    /// Everything about the recording pill that is not WPF: the state machine, the level
    /// meter maths and the placement. RecordingIndicatorWindow only draws what this says.
    /// Sizes are DIPs unless they say pixels.
    /// </summary>
    internal static class RecordingIndicatorLogic
    {
        public const int BarCount = 9;
        public const double PillWidth = 120;
        public const double PillHeight = 34;
        /// <summary>Gap between the pill and the bottom of the work area (= top of the taskbar).</summary>
        public const double BottomGap = 28;
        /// <summary>Transparent margin around the pill inside its window: room for the drop shadow and the slide-in.</summary>
        public const double ShadowPadding = 16;
        public const double MinBarHeight = 3;
        public const double MaxBarHeight = 18;

        // ------------------------------------------------------------------ state machine

        /// <summary>
        /// Every DictationEventKind is placed here on purpose (a test pins the enum). Anything
        /// that ends a dictation hides; anything unknown hides too - a pill that is wrongly
        /// gone costs nothing, one that is stuck on screen does.
        /// </summary>
        public static RecordingIndicatorState NextState(RecordingIndicatorState current, DictationEventKind kind) => kind switch
        {
            DictationEventKind.RecordingStarted => RecordingIndicatorState.Recording,
            DictationEventKind.RecordingStopped => RecordingIndicatorState.Processing,

            // Informational: Busy = a start was refused while a transcription runs (keep showing
            // it); AutoStopped always follows RecordingStopped (and RecordingFailed when the
            // audio was lost), which already decided.
            DictationEventKind.Busy => current,
            DictationEventKind.AutoStopped => current,

            _ => RecordingIndicatorState.Hidden,
        };

        public const int IdleStrikesToHide = 2;

        /// <summary>
        /// Safety auto-hide: consecutive watchdog readings that found the pill up while the
        /// dictation controller was idle. One such reading can be the moment between the
        /// controller going idle and its notice reaching the UI thread; two in a row cannot.
        /// </summary>
        public static int NextIdleStrikes(RecordingIndicatorState state, bool dictationActive, int strikes) =>
            state == RecordingIndicatorState.Hidden || dictationActive ? 0 : strikes + 1;

        // ------------------------------------------------------------------ level meter

        private const double FloorDb = -46; // below this an idle microphone's hiss: bars stay at rest
        private const double CeilingDb = -6;

        /// <summary>Linear buffer peak (0..1) to a perceptual 0..1: speech sits at 5-30% of full scale.</summary>
        public static double ShapeLevel(float peak)
        {
            if (float.IsNaN(peak) || peak <= 0f) return 0;
            double db = 20 * Math.Log10(peak);
            return Clamp01((db - FloorDb) / (CeilingDb - FloorDb));
        }

        private const double AttackSeconds = 0.035;
        private const double DecaySeconds = 0.20;

        /// <summary>One smoothing step: fast attack, slower decay, frame-rate independent, never overshoots.</summary>
        public static double Smooth(double current, double target, double dtSeconds)
        {
            if (double.IsNaN(current)) return target;
            if (!(dtSeconds > 0)) return current;
            double tau = target > current ? AttackSeconds : DecaySeconds;
            return current + (target - current) * (1 - Math.Exp(-dtSeconds / tau));
        }

        /// <summary>
        /// Height of one bar while recording. Centre-weighted envelope (the middle bars reach
        /// higher) times a per-bar wobble with its own speed and phase, so one level value
        /// reads as a voice waveform instead of nine bars moving in lockstep.
        /// </summary>
        public static double BarHeight(double level, int index, int count, double timeSeconds)
        {
            if (double.IsNaN(level) || level <= 0) return MinBarHeight;

            double half = Math.Max(1, (count - 1) / 2.0);
            double x = (index - (count - 1) / 2.0) / half;            // -1 .. 1
            double envelope = 0.34 + 0.66 * (1 - x * x);              // edges 0.34, centre 1
            double wobble = 0.72 + 0.28 * Math.Sin(timeSeconds * (7.0 + 1.3 * index) + index * 1.9);

            return MinBarHeight + (MaxBarHeight - MinBarHeight) * Clamp01(Clamp01(level) * envelope * wobble);
        }

        // ------------------------------------------------------------------ processing shimmer

        private const double ShimmerSpeed = 3.2;   // rad/s
        private const double ShimmerBarPhase = 0.75; // rad between neighbouring bars
        public const double ShimmerBarLagSeconds = ShimmerBarPhase / ShimmerSpeed;

        private static double ShimmerWave(int index, double timeSeconds) =>
            0.5 + 0.5 * Math.Sin(timeSeconds * ShimmerSpeed - index * ShimmerBarPhase);

        public static double ProcessingBarHeight(int index, double timeSeconds) => 4 + 3 * ShimmerWave(index, timeSeconds);

        public static double ProcessingBarOpacity(int index, double timeSeconds) => 0.32 + 0.58 * ShimmerWave(index, timeSeconds);

        // ------------------------------------------------------------------ dot glow

        public const double GlowPeriodSeconds = 2.4;

        public static double GlowOpacity(double timeSeconds) =>
            0.35 + 0.55 * (0.5 + 0.5 * Math.Sin(timeSeconds * 2 * Math.PI / GlowPeriodSeconds));

        // ------------------------------------------------------------------ placement

        /// <summary>
        /// Window rectangle in pixels for a monitor's work area (pixels) and DPI scale
        /// (1.0 = 96 DPI): horizontally centred, the pill itself BottomGap above the taskbar.
        /// </summary>
        public static PixelRect Placement(PixelRect workArea, double dpiScale)
        {
            if (double.IsNaN(dpiScale) || double.IsInfinity(dpiScale) || dpiScale <= 0) dpiScale = 1.0;

            int width = (int)Math.Round((PillWidth + 2 * ShadowPadding) * dpiScale);
            int height = (int)Math.Round((PillHeight + 2 * ShadowPadding) * dpiScale);
            int left = workArea.Left + (workArea.Width - width) / 2;
            int top = workArea.Bottom - (int)Math.Round((BottomGap + PillHeight + ShadowPadding) * dpiScale);
            return new PixelRect(left, top, width, height);
        }

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}

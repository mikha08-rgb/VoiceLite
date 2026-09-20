using System;
using System.Threading;
using System.Windows.Threading;
using VoiceLite.Utilities;

namespace VoiceLite.Services
{
    /// <summary>What RecordingIndicator needs from the pill window. UI thread only.</summary>
    public interface IRecordingIndicatorView
    {
        void ShowState(RecordingIndicatorState state);
        void HideIndicator();
        void CloseView();
    }

    /// <summary>
    /// Drives the recording pill (RecordingIndicatorWindow) from dictation notices.
    ///
    /// The one hard rule: nothing in here may break or delay dictation. Every call into the
    /// window - and the window's creation - is caught, logged once, and given up on after
    /// <see cref="MaxFailures"/> failures. MainWindow calls OnDictationEvent / Refresh / Close on
    /// the UI thread; OnLevel arrives on the audio capture thread and only writes two fields
    /// (the window polls <see cref="CurrentLevel"/> from its render tick - nothing is ever
    /// posted to, or waited for on, the UI thread from the capture callback).
    /// </summary>
    internal sealed class RecordingIndicator
    {
        public const int MaxFailures = 3;
        public const long LevelStaleAfterMs = 200;
        private static readonly TimeSpan SafetyInterval = TimeSpan.FromSeconds(2);

        private readonly Func<bool> isEnabled;
        private readonly Func<bool>? isDictationActive;
        private readonly Func<Func<float>, IRecordingIndicatorView> viewFactory;
        private readonly Func<Action, IDisposable>? startSafetyTimer;

        private IRecordingIndicatorView? view;
        private IDisposable? safetyTimer;
        private RecordingIndicatorState dictationState = RecordingIndicatorState.Hidden; // what the notices say
        private volatile RecordingIndicatorState shownState = RecordingIndicatorState.Hidden; // what the view was told
        private int idleStrikes;
        private int failures;
        private bool warned;
        private bool closed;

        private float level;
        private long levelAtMs;

        internal Action<string> LogWarning = ErrorLogger.LogWarning;
        internal Func<long> Clock = () => Environment.TickCount64;

        public RecordingIndicator(
            Func<bool> isEnabled,
            Func<bool>? isDictationActive,
            Func<Func<float>, IRecordingIndicatorView> viewFactory,
            Func<Action, IDisposable>? startSafetyTimer = null)
        {
            this.isEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));
            this.isDictationActive = isDictationActive;
            this.viewFactory = viewFactory ?? throw new ArgumentNullException(nameof(viewFactory));
            this.startSafetyTimer = startSafetyTimer;
        }

        /// <summary>The real thing: the pill window plus a dispatcher timer for the safety auto-hide.</summary>
        public static RecordingIndicator ForWindow(Func<bool> isEnabled, Func<bool> isDictationActive) =>
            new(isEnabled, isDictationActive,
                levelSource => new RecordingIndicatorWindow(levelSource),
                tick => new SafetyTimer(tick));

        // ------------------------------------------------------------------ UI thread

        public void OnDictationEvent(DictationEventKind kind)
        {
            dictationState = RecordingIndicatorLogic.NextState(dictationState, kind);
            Apply();
        }

        /// <summary>Re-reads the setting (Settings was just saved): hides a visible pill, or shows the dictation in flight.</summary>
        public void Refresh() => Apply();

        /// <summary>
        /// Safety auto-hide. The notices cover every terminal path, but a pill stuck on top of
        /// every app is bad enough to check against the controller itself: up while the
        /// controller has been idle for two readings means a notice was lost.
        /// </summary>
        internal void SafetyTick()
        {
            if (isDictationActive == null) return;
            Guard("safety check", () =>
            {
                idleStrikes = RecordingIndicatorLogic.NextIdleStrikes(shownState, isDictationActive(), idleStrikes);
                if (idleStrikes < RecordingIndicatorLogic.IdleStrikesToHide) return;

                LogWarning($"Recording indicator: safety auto-hide - still showing {shownState} while dictation is idle");
                dictationState = RecordingIndicatorState.Hidden;
                Apply();
            });
        }

        public void Close()
        {
            if (closed) return;
            closed = true;
            shownState = RecordingIndicatorState.Hidden;
            StopSafetyTimer();
            var v = view;
            view = null;
            if (v != null) Guard("close", v.CloseView);
        }

        private void Apply()
        {
            if (closed || failures >= MaxFailures) return;
            Guard("update", () =>
            {
                var target = isEnabled() ? dictationState : RecordingIndicatorState.Hidden;
                if (target == shownState) return;

                if (target == RecordingIndicatorState.Hidden)
                {
                    shownState = target;
                    idleStrikes = 0;
                    StopSafetyTimer();
                    view?.HideIndicator();
                    return;
                }

                view ??= viewFactory(CurrentLevel);
                shownState = target;
                idleStrikes = 0;
                view.ShowState(target);
                safetyTimer ??= startSafetyTimer?.Invoke(SafetyTick);
            });
        }

        private void StopSafetyTimer()
        {
            var t = safetyTimer;
            safetyTimer = null;
            if (t != null) Guard("timer", t.Dispose);
        }

        private void Guard(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failures++;
                if (failures == MaxFailures) GiveUp();
                if (warned) return;
                warned = true;
                try { LogWarning($"Recording indicator failed ({what}) - dictation is unaffected: {ex.GetType().Name}: {ex.Message}"); }
                catch { /* logging must not throw into dictation either */ }
            }
        }

        /// <summary>Too many failures: the pill is off until restart. Whatever is on screen goes away first.</summary>
        private void GiveUp()
        {
            shownState = RecordingIndicatorState.Hidden;
            var v = view;
            view = null;
            try { safetyTimer?.Dispose(); } catch { /* giving up anyway */ }
            safetyTimer = null;
            try { v?.CloseView(); } catch { /* giving up anyway */ }
        }

        // ------------------------------------------------------------------ level (any thread)

        /// <summary>AudioRecorder.LevelAvailable handler. Capture thread: two field writes, nothing else.</summary>
        public void OnLevel(object? sender, float peak)
        {
            if (shownState != RecordingIndicatorState.Recording) return; // hidden or processing: dropped
            Volatile.Write(ref level, peak);
            Volatile.Write(ref levelAtMs, Clock());
        }

        /// <summary>Latest buffer peak (0..1); 0 when not recording or when the recorder stopped delivering buffers.</summary>
        internal float CurrentLevel()
        {
            if (shownState != RecordingIndicatorState.Recording) return 0f;
            return Clock() - Volatile.Read(ref levelAtMs) > LevelStaleAfterMs ? 0f : Volatile.Read(ref level);
        }

        private sealed class SafetyTimer : IDisposable
        {
            private readonly DispatcherTimer timer;
            private readonly EventHandler handler;

            public SafetyTimer(Action tick)
            {
                handler = (_, _) => tick();
                timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = SafetyInterval };
                timer.Tick += handler;
                timer.Start();
            }

            public void Dispose()
            {
                timer.Stop();
                timer.Tick -= handler;
            }
        }
    }
}

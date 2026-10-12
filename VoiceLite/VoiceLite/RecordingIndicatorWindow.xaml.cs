using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using VoiceLite.Services;
using VoiceLite.Utilities;
using Logic = VoiceLite.Utilities.RecordingIndicatorLogic;

namespace VoiceLite
{
    /// <summary>
    /// The recording pill: bottom-centre of the monitor the user is working on, above the taskbar.
    ///
    /// NON-NEGOTIABLE: it never takes focus or activation and it is click-through. Dictated text
    /// is pasted into the foreground window - a pill that became the foreground window, even for
    /// a moment, would swallow the paste. Hence ShowActivated/Focusable=false in XAML,
    /// WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW below, SWP_NOACTIVATE on every
    /// move, and no call in this file that activates or focuses anything (a test scans for it).
    ///
    /// Created once by RecordingIndicator, then shown/hidden. It has no owner (an owned window
    /// disappears with a MainWindow that is minimized to the tray) and is closed from
    /// MainWindow.OnClosed - ShutdownMode is OnLastWindowClose and a hidden window still counts.
    /// All calls are UI-thread calls; the mic level is polled from the render tick.
    /// </summary>
    public partial class RecordingIndicatorWindow : Window, IRecordingIndicatorView
    {
        private static readonly TimeSpan AppearDuration = TimeSpan.FromMilliseconds(140);
        private static readonly TimeSpan DisappearDuration = TimeSpan.FromMilliseconds(120);
        private static readonly TimeSpan HideFallbackDelay = TimeSpan.FromMilliseconds(300);
        private const double SlideDistance = 6;
        private const double StateBlendSeconds = 0.18;

        private readonly Func<float> levelSource;
        private readonly Rectangle[] bars;
        private readonly Stopwatch clock = Stopwatch.StartNew();

        private RecordingIndicatorState state = RecordingIndicatorState.Hidden;
        private bool ticking;
        private bool tickFailed;
        private double lastTickSeconds;
        private double smoothedLevel;
        private double processingBlend; // 0 = recording look, 1 = processing look
        private DispatcherTimer? hideFallback; // one-shot: see HideIndicator

        /// <summary>Tests show the window off-screen themselves; production always places it.</summary>
        internal bool PlaceOnShow { get; set; } = true;

        /// <summary>Starts the fade-out. A seam: tests make it throw, or never complete.</summary>
        internal Action<DoubleAnimation> StartFadeOut;

        public RecordingIndicatorWindow(Func<float> levelSource)
        {
            this.levelSource = levelSource ?? throw new ArgumentNullException(nameof(levelSource));
            InitializeComponent();
            StartFadeOut = fade => Root.BeginAnimation(OpacityProperty, fade);

            // One source of truth for the geometry: Placement() assumes exactly these.
            Width = Logic.PillWidth + 2 * Logic.ShadowPadding;
            Height = Logic.PillHeight + 2 * Logic.ShadowPadding;
            Root.Margin = new Thickness(Logic.ShadowPadding);

            bars = Bars.Children.OfType<Rectangle>().ToArray();
            SourceInitialized += OnSourceInitialized;
        }

        // ------------------------------------------------------------------ IRecordingIndicatorView

        public void ShowState(RecordingIndicatorState newState)
        {
            if (newState == RecordingIndicatorState.Hidden)
            {
                HideIndicator();
                return;
            }

            bool wasHidden = state == RecordingIndicatorState.Hidden;
            state = newState;
            hideFallback?.Stop();

            if (wasHidden)
            {
                // Start in the right look instead of morphing from the previous dictation's.
                processingBlend = newState == RecordingIndicatorState.Processing ? 1 : 0;
                smoothedLevel = 0;
            }

            if (!IsVisible)
            {
                if (PlaceOnShow) PlaceOnActiveMonitor();
                Show(); // ShowActivated=false: SW_SHOWNOACTIVATE
            }

            StartTicking();
            Root.BeginAnimation(OpacityProperty, new DoubleAnimation(1, AppearDuration));
            Slide.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(0, AppearDuration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }

        public void HideIndicator()
        {
            state = RecordingIndicatorState.Hidden;
            if (!IsVisible) return;

            // The fade is cosmetic; the hide is not. Completed is the normal route, the one-shot
            // timer covers a Completed that never comes (no rendering: locked session, RDP
            // disconnect), and a fade that cannot even start hides at once.
            var fade = new DoubleAnimation(0, DisappearDuration);
            fade.Completed += (_, _) => FinishHide();
            try
            {
                hideFallback ??= CreateHideFallback();
                hideFallback.Stop();
                hideFallback.Start();
                StartFadeOut(fade);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning($"Recording indicator: fade-out failed, hiding at once: {ex.GetType().Name}: {ex.Message}");
                FinishHide();
            }
        }

        private DispatcherTimer CreateHideFallback()
        {
            var timer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = HideFallbackDelay };
            timer.Tick += (_, _) => FinishHide();
            return timer;
        }

        /// <summary>Idempotent; a no-op when the pill was shown again in the meantime. Never throws.</summary>
        private void FinishHide()
        {
            try
            {
                hideFallback?.Stop();
                if (state != RecordingIndicatorState.Hidden) return; // shown again while fading
                StopTicking();
                if (!IsVisible) return;
                Hide();
                Slide.BeginAnimation(TranslateTransform.YProperty, null);
                Slide.Y = SlideDistance; // the next appearance slides up again
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning($"Recording indicator: hide failed: {ex.Message}");
            }
        }

        public void CloseView()
        {
            state = RecordingIndicatorState.Hidden;
            hideFallback?.Stop();
            StopTicking();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            StopTicking(); // CompositionTarget.Rendering is static: a leftover handler would keep this window alive
            hideFallback?.Stop(); // so would a running DispatcherTimer
            base.OnClosed(e);
        }

        // ------------------------------------------------------------------ animation

        private void StartTicking()
        {
            if (ticking || tickFailed) return;
            ticking = true;
            lastTickSeconds = clock.Elapsed.TotalSeconds;
            CompositionTarget.Rendering += OnRendering;
        }

        private void StopTicking()
        {
            if (!ticking) return;
            ticking = false;
            CompositionTarget.Rendering -= OnRendering;
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            try
            {
                double now = clock.Elapsed.TotalSeconds;
                double dt = Math.Min(0.1, Math.Max(0, now - lastTickSeconds));
                lastTickSeconds = now;

                bool processing = state == RecordingIndicatorState.Processing;
                double step = dt / StateBlendSeconds;
                processingBlend = processing ? Math.Min(1, processingBlend + step) : Math.Max(0, processingBlend - step);

                double target = state == RecordingIndicatorState.Recording ? Logic.ShapeLevel(levelSource()) : 0;
                smoothedLevel = Logic.Smooth(smoothedLevel, target, dt);

                for (int i = 0; i < bars.Length; i++)
                {
                    double recording = Logic.BarHeight(smoothedLevel, i, bars.Length, now);
                    double shimmer = Logic.ProcessingBarHeight(i, now);
                    bars[i].Height = recording + (shimmer - recording) * processingBlend;
                    bars[i].Opacity = 1 + (Logic.ProcessingBarOpacity(i, now) - 1) * processingBlend;
                }

                RecordingDot.Opacity = 1 - processingBlend;
                ProcessingDot.Opacity = processingBlend;
                RecordingGlow.Opacity = Logic.GlowOpacity(now) * (1 - processingBlend);
                ProcessingGlow.Opacity = 0.6 * processingBlend;
            }
            catch (Exception ex)
            {
                // An exception escaping a Rendering handler is an unhandled dispatcher exception:
                // it would take the app down for the sake of a level meter. Freeze instead.
                tickFailed = true;
                StopTicking();
                ErrorLogger.LogWarning($"Recording indicator: animation stopped: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------ placement

        /// <summary>
        /// Moves the (still hidden) window to the bottom-centre of the work area of the monitor
        /// that has the foreground window (primary when there is none). Pure Win32 pixels on
        /// purpose: WPF's Left/Top are DIPs of the window's CURRENT monitor, which is the wrong
        /// unit for a jump to a monitor with another scale factor.
        /// </summary>
        private void PlaceOnActiveMonitor()
        {
            var hwnd = new WindowInteropHelper(this).EnsureHandle();

            IntPtr monitor = MonitorFromWindow(GetForegroundWindow(), MONITOR_DEFAULTTOPRIMARY);
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
            {
                ErrorLogger.LogWarning("Recording indicator: no monitor information - not shown at a new position");
                return;
            }

            var work = new PixelRect(info.rcWork.Left, info.rcWork.Top,
                info.rcWork.Right - info.rcWork.Left, info.rcWork.Bottom - info.rcWork.Top);
            var target = Logic.Placement(work, MonitorScale(monitor));

            // Twice when the target monitor has another DPI: the first move makes WPF re-scale
            // the window for that monitor (per-monitor-aware processes), the second one lands
            // the final size and position. A no-op otherwise.
            for (int pass = 0; pass < 2; pass++)
            {
                SetWindowPos(hwnd, HWND_TOPMOST, target.Left, target.Top, target.Width, target.Height,
                    SWP_NOACTIVATE | SWP_NOOWNERZORDER);
            }
        }

        /// <summary>
        /// Scale of a monitor as THIS process sees it: the monitor's own DPI when the process is
        /// per-monitor aware, the system DPI when it is system aware - which is also the unit
        /// GetMonitorInfo and SetWindowPos speak in that case, so the maths holds either way.
        /// </summary>
        private double MonitorScale(IntPtr monitor)
        {
            try
            {
                if (GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX > 0)
                    return dpiX / 96.0;
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                // shcore.dll: Windows 8.1+. Fall through.
            }
            return VisualTreeHelper.GetDpi(this).DpiScaleX;
        }

        // ------------------------------------------------------------------ window styles

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(exStyle | RequiredExtendedStyles));
            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        }

        /// <summary>No activation, click-through (with WS_EX_LAYERED from AllowsTransparency), not in Alt+Tab.</summary>
        internal const long RequiredExtendedStyles = WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW;

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Click-through means this should never arrive; if it does, still refuse activation.
            if (msg == WM_MOUSEACTIVATE)
            {
                handled = true;
                return new IntPtr(MA_NOACTIVATE);
            }
            return IntPtr.Zero;
        }

        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TRANSPARENT = 0x00000020;
        private const long WS_EX_TOOLWINDOW = 0x00000080;
        private const long WS_EX_NOACTIVATE = 0x08000000;
        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE = 3;
        private const uint MONITOR_DEFAULTTOPRIMARY = 1;
        private const int MDT_EFFECTIVE_DPI = 0;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOOWNERZORDER = 0x0200;
        private static readonly IntPtr HWND_TOPMOST = new(-1);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private static IntPtr GetWindowLongPtr(IntPtr hwnd, int index) =>
            IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, index) : new IntPtr(GetWindowLong32(hwnd, index));

        private static IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value) =>
            IntPtr.Size == 8 ? SetWindowLongPtr64(hwnd, index, value) : new IntPtr(SetWindowLong32(hwnd, index, value.ToInt32()));

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong32(IntPtr hwnd, int index);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong32(IntPtr hwnd, int index, int value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);
    }
}

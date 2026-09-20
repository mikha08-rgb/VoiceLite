using System;
using System.Diagnostics;
using System.Windows.Threading;
using NAudio.Wave;

namespace VoiceLite.Services
{
    public sealed class MicLevelEventArgs : EventArgs
    {
        /// <summary>Peak amplitude of the last buffer, 0.0 (silence) .. 1.0 (full scale).</summary>
        public float Peak { get; }

        /// <summary>True once the peak has stayed below <see cref="MicLevelMonitor.NoSignalThreshold"/> for <see cref="MicLevelMonitor.NoSignalDelay"/>.</summary>
        public bool NoSignal { get; }

        public MicLevelEventArgs(float peak, bool noSignal)
        {
            Peak = peak;
            NoSignal = noSignal;
        }
    }

    /// <summary>
    /// Live input-level meter for the Settings window. Opens its OWN WaveInEvent on the
    /// chosen device (MME allows a second capture on the same device, so this works while
    /// the app is idle or even mid-recording) and reports the peak of every 50 ms buffer
    /// on the dispatcher that created the monitor. Deliberately independent of
    /// AudioRecorder: it must never touch the recording pipeline.
    /// </summary>
    public sealed class MicLevelMonitor : IDisposable
    {
        public const float NoSignalThreshold = 0.01f;
        public static readonly TimeSpan NoSignalDelay = TimeSpan.FromSeconds(2);

        public event EventHandler<MicLevelEventArgs>? LevelChanged;

        private readonly int deviceIndex;
        private readonly Dispatcher dispatcher;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private WaveInEvent? waveIn;
        private long lastSignalMs;
        private volatile bool disposed;

        /// <param name="deviceIndex">
        /// Settings.SelectedMicrophoneIndex semantics: -1 = default device. Mapped to
        /// WaveInEvent device 0 exactly like AudioRecorder.StartRecording does, so the
        /// meter shows the device the app will actually record from.
        /// </param>
        public MicLevelMonitor(int deviceIndex)
        {
            this.deviceIndex = deviceIndex >= 0 ? deviceIndex : 0;
            dispatcher = Dispatcher.CurrentDispatcher;
        }

        /// <summary>Opens the device. Throws (NAudio MmException etc.) if it cannot be opened.</summary>
        public void Start()
        {
            if (disposed) throw new ObjectDisposedException(nameof(MicLevelMonitor));
            if (waveIn != null) return;

            lastSignalMs = clock.ElapsedMilliseconds;
            var capture = new WaveInEvent
            {
                DeviceNumber = deviceIndex,
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 50,
            };
            capture.DataAvailable += OnDataAvailable;
            try
            {
                capture.StartRecording();
            }
            catch
            {
                capture.DataAvailable -= OnDataAvailable;
                capture.Dispose();
                throw;
            }
            waveIn = capture;
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (disposed) return;
            var args = Process(e.Buffer, e.BytesRecorded, clock.ElapsedMilliseconds);
            dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (!disposed)
                    LevelChanged?.Invoke(this, args);
            }));
        }

        /// <summary>
        /// Peak + no-signal bookkeeping for one buffer. Pure apart from the
        /// last-signal timestamp; internal so tests can drive it with a synthetic clock.
        /// </summary>
        internal MicLevelEventArgs Process(byte[] buffer, int bytesRecorded, long nowMs)
        {
            float peak = ComputePeak(buffer, bytesRecorded);
            if (peak >= NoSignalThreshold)
                lastSignalMs = nowMs;
            bool noSignal = nowMs - lastSignalMs >= (long)NoSignalDelay.TotalMilliseconds;
            return new MicLevelEventArgs(peak, noSignal);
        }

        /// <summary>Peak |sample| of a 16-bit little-endian mono PCM buffer, scaled to 0..1.</summary>
        public static float ComputePeak(byte[] buffer, int bytesRecorded)
        {
            if (buffer == null || bytesRecorded < 2)
                return 0f;

            int max = 0;
            int end = Math.Min(bytesRecorded, buffer.Length) - 1;
            for (int i = 0; i < end; i += 2)
            {
                // Widen before Abs: Math.Abs(short.MinValue) throws OverflowException.
                int sample = Math.Abs((int)(short)(buffer[i] | (buffer[i + 1] << 8)));
                if (sample > max) max = sample;
            }
            return Math.Min(1f, max / 32768f);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            var capture = waveIn;
            waveIn = null;
            if (capture == null) return;

            capture.DataAvailable -= OnDataAvailable;
            try { capture.StopRecording(); }
            catch (Exception ex) { ErrorLogger.LogWarning($"MicLevelMonitor stop failed: {ex.Message}"); }
            try { capture.Dispose(); }
            catch (Exception ex) { ErrorLogger.LogWarning($"MicLevelMonitor dispose failed: {ex.Message}"); }
        }
    }
}

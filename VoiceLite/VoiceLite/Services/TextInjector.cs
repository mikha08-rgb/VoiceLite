using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    public class TextInjector : IDisposable
    {
        // Disposal tracking - volatile so worker threads see the latest value.
        private volatile bool _disposed = false;

        // Timing constants (in milliseconds)
        private const int CLIPBOARD_READY_DELAY_MS = 20;       // Wait for clipboard to be ready across applications
        private const int CLIPBOARD_AUTO_CLEAR_DELAY_MS = 2000; // Delay before auto-clearing transcription from clipboard
        private const int KEY_MODIFIER_DELAY_MS = 5;            // Delay for modifier keys (Ctrl)
        private const int KEY_RELEASE_DELAY_MS = 2;             // Delay after releasing keys

        private readonly Settings settings;

        // Semaphore to prevent multiple concurrent clipboard operations.
        // If a timeout occurs, subsequent calls wait rather than spawning unlimited threads.
        private readonly SemaphoreSlim _clipboardSemaphore = new SemaphoreSlim(1, 1);

        public TextInjector(Settings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void InjectText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
#if DEBUG
                ErrorLogger.LogMessage("InjectText called with empty text");
#endif
                return;
            }

            // Capture the foreground window where the user expects text to land.
            // For long transcriptions the user may switch focus during processing — we restore it before pasting.
            IntPtr capturedHandle = GetForegroundWindow();
            ErrorLogger.LogWarning($"InjectText: {text.Length} chars, window={capturedHandle}");

            try
            {
                InjectViaClipboard(text, capturedHandle);
#if DEBUG
                ErrorLogger.LogMessage($"Text injected via clipboard ({text.Length} chars)");
#endif
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("TextInjector.InjectText", ex);
                throw new InvalidOperationException($"Failed to inject text: {ex.Message}", ex);
            }
        }

        private void InjectViaClipboard(string text, IntPtr targetHandle)
        {
            PasteViaClipboard(text, targetHandle);

            // Auto-clear clipboard after a delay so transcribed content doesn't linger
            // on shared machines (esp. clinical workstations). Match-before-clear preserves
            // the user's clipboard if they copy something else in the interim.
            ScheduleClipboardClear(text);
        }

        private void ScheduleClipboardClear(string textToClear)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(CLIPBOARD_AUTO_CLEAR_DELAY_MS);
                    if (_disposed) return;

                    var thread = new Thread(() =>
                    {
                        try
                        {
                            string? current = Clipboard.ContainsText() ? Clipboard.GetText() : null;
                            if (ShouldClearClipboard(current, textToClear))
                            {
                                Clipboard.Clear();
                            }
                        }
                        catch (Exception ex)
                        {
                            ErrorLogger.LogDebug($"Clipboard auto-clear failed: {ex.Message}");
                        }
                    });
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                    thread.Join(TimeSpan.FromSeconds(2));
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogDebug($"Scheduled clipboard auto-clear failed: {ex.Message}");
                }
            });
        }

        // Match-before-clear: only clear the clipboard if the transcription is still on it.
        // If the user copied something else in the 2s window, their content is preserved.
        // This is the privacy guarantee documented in PILOT.md.
        internal static bool ShouldClearClipboard(string? currentClipboard, string expectedText)
        {
            return currentClipboard != null && currentClipboard == expectedText;
        }

        private void PasteViaClipboard(string text, IntPtr targetHandle)
        {
            // Acquire semaphore to serialize clipboard operations and prevent thread accumulation
            // if a previous operation timed out but is still running.
            if (!_clipboardSemaphore.Wait(TimeSpan.FromSeconds(5)))
            {
                ErrorLogger.LogWarning("Clipboard semaphore acquisition timed out - previous operation may be stuck");
                throw new InvalidOperationException("Clipboard operation busy. Please try again.");
            }

            Exception? workerException = null;
            // Use threadStarted instead of operationCompleted to avoid race conditions —
            // threadStarted is only written by the main thread, so no synchronization needed.
            bool threadStarted = false;

            try
            {
                // Clipboard requires an STA thread. The handle is captured by closure so
                // the worker uses the handle from the call site, not a shared instance field.
                Thread thread = new Thread(() =>
                {
                    try
                    {
                        SetClipboardText(text);
                        Thread.Sleep(CLIPBOARD_READY_DELAY_MS);
                        SimulateCtrlV(targetHandle);
                    }
                    catch (Exception ex)
                    {
                        workerException = ex;
                    }
                    finally
                    {
                        // Release semaphore when thread completes (even on timeout path)
                        // so the next operation can proceed.
                        if (!_disposed)
                        {
                            try { _clipboardSemaphore.Release(); }
                            catch (SemaphoreFullException ex) { ErrorLogger.LogError("TextInjector: SemaphoreFullException - double release detected (sync bug)", ex); }
                            catch (ObjectDisposedException) { }
                        }
                    }
                });

                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                threadStarted = true;

                if (!thread.Join(TimeSpan.FromSeconds(5)))
                {
                    ErrorLogger.LogWarning("Clipboard operation thread timed out - thread will release semaphore when done");
                    throw new InvalidOperationException("Clipboard operation timed out.");
                }

                if (workerException != null)
                {
                    throw new InvalidOperationException("Clipboard operation failed.", workerException);
                }
            }
            catch (InvalidOperationException)
            {
                // Re-throw timeout/failure exceptions - semaphore released by thread
                throw;
            }
            catch (Exception ex)
            {
                // Only release semaphore if the thread never started.
                // Once thread.Start() succeeds, its finally block handles release.
                if (!threadStarted && !_disposed)
                {
                    try { _clipboardSemaphore.Release(); }
                    catch (SemaphoreFullException semEx) { ErrorLogger.LogError("TextInjector: SemaphoreFullException in catch - double release detected (sync bug)", semEx); }
                    catch (ObjectDisposedException) { }
                }
                throw new InvalidOperationException("Clipboard operation failed unexpectedly.", ex);
            }
        }

        private void SetClipboardText(string text)
        {
            const int maxAttempts = 5;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    Clipboard.SetText(text, TextDataFormat.UnicodeText);
#if DEBUG
                    ErrorLogger.LogMessage("Text copied to clipboard successfully");
#endif
                    return;
                }
                catch (ExternalException)
                {
                    // 5ms, 10ms, 15ms, 20ms backoff between attempts.
                    if (attempt < maxAttempts - 1)
                    {
                        Thread.Sleep(5 * (attempt + 1));
                    }
                }
            }

            throw new InvalidOperationException("Unable to access clipboard.");
        }

        private void SimulateCtrlV(IntPtr targetHandle)
        {
            try
            {
#if DEBUG
                ErrorLogger.LogMessage($"Simulating Ctrl+V to window handle: {targetHandle}");
#endif

                // For long transcriptions the user may have clicked the VoiceLite window to check progress.
                // Restoring focus ensures Ctrl+V goes to the original target app.
                if (targetHandle != IntPtr.Zero && IsWindow(targetHandle))
                {
                    if (!SetForegroundWindow(targetHandle))
                    {
                        ErrorLogger.LogWarning($"SetForegroundWindow failed for handle {targetHandle} in SimulateCtrlV");
                    }
                    Thread.Sleep(50); // Allow window activation to complete
                }
                else if (targetHandle != IntPtr.Zero)
                {
                    ErrorLogger.LogWarning($"Target window {targetHandle} no longer valid in SimulateCtrlV, using current foreground window");
                }

                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYDOWN, 0);
                Thread.Sleep(KEY_MODIFIER_DELAY_MS);
                keybd_event(VK_V, 0, KEYEVENTF_KEYDOWN, 0);
                Thread.Sleep(KEY_RELEASE_DELAY_MS);
                keybd_event(VK_V, 0, KEYEVENTF_KEYUP, 0);
                Thread.Sleep(KEY_MODIFIER_DELAY_MS);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);

#if DEBUG
                ErrorLogger.LogMessage("Ctrl+V simulation completed");
#endif
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("SimulateCtrlV failed", ex);
                throw;
            }
        }

        public bool CanInject()
        {
            try
            {
                return GetForegroundWindow() != IntPtr.Zero;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogDebug($"CanInject check failed: {ex.Message}");
                return false;
            }
        }

        public string GetFocusedApplicationName()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                    return "Unknown";

                GetWindowThreadProcessId(hwnd, out uint processId);

                try
                {
                    var process = Process.GetProcessById((int)processId);
                    return process?.ProcessName ?? "Unknown";
                }
                catch (ArgumentException)
                {
                    // Process exited between GetWindowThreadProcessId and GetProcessById.
                    ErrorLogger.LogDebug($"Process {processId} exited before name could be retrieved");
                    return "Unknown";
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogDebug($"GetFocusedApplicationName failed: {ex.Message}");
                return "Unknown";
            }
        }

        // Virtual key codes
        private const byte VK_CONTROL = 0x11;
        private const byte VK_V = 0x56;

        // Key event flags
        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        // Validate window handles before use — the window may have been closed between capture and injection.
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            if (disposing)
            {
                try
                {
                    _clipboardSemaphore?.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, ignore
                }
            }
        }

        ~TextInjector()
        {
            Dispose(false);
        }
    }
}

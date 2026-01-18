using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsInput;
using WindowsInput.Native;
using VoiceLite.Models;
using VoiceLite.Core.Interfaces.Services;

namespace VoiceLite.Services
{
    public class TextInjector : ITextInjector, IDisposable
    {
        // Thread-safe AutoPaste property (Issue 3)
        private volatile bool _autoPaste = true;
        public bool AutoPaste
        {
            get => _autoPaste;
            set => _autoPaste = value;
        }

        // Target window handle for paste operations
        // Captured when InjectText is called to ensure paste goes to correct window
        // Made volatile for thread-safe access between main thread and worker threads (Issue 2)
        private volatile IntPtr _targetWindowHandle = IntPtr.Zero;

        // Disposal tracking (Issue 1)
        // CRITICAL-2 FIX: Made volatile to ensure visibility across threads
        // Without volatile, worker thread may not see disposal and could call Release() on disposed semaphore
        private volatile bool _disposed = false;

        // Text length thresholds for injection mode selection
        private const int SHORT_TEXT_THRESHOLD = 50; // Use typing for text shorter than this
        private const int LONG_TEXT_THRESHOLD = 100; // Text longer than this is considered "long"

        // Timing constants (in milliseconds)
        private const int TYPING_DELAY_MS = 2; // Delay between keystrokes when typing
        private const int CLIPBOARD_RETRY_DELAY_MS = 10; // Delay before retrying clipboard operation
        private const int CLIPBOARD_SETTLE_DELAY_MS = 50; // Wait for clipboard to settle after set
        private const int CLIPBOARD_READY_DELAY_MS = 20; // Ensure clipboard is ready across applications
        private const int KEY_MODIFIER_DELAY_MS = 5; // Delay for modifier keys (Ctrl, Alt, etc.)
        private const int KEY_RELEASE_DELAY_MS = 2; // Delay after releasing keys

        private readonly InputSimulator inputSimulator;
        private readonly Settings settings;

        // THREAD-LEAK FIX: Semaphore to prevent multiple concurrent clipboard operations
        // If timeout occurs, subsequent calls wait rather than spawning unlimited threads
        private readonly SemaphoreSlim _clipboardSemaphore = new SemaphoreSlim(1, 1);

        public TextInjector(Settings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            inputSimulator = new InputSimulator();
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

            // CRITICAL FIX: Capture foreground window IMMEDIATELY when InjectText is called
            // This is the window where the user expects text to be pasted
            // For long transcriptions, user may switch focus during processing - we restore it later
            _targetWindowHandle = GetForegroundWindow();

            // Release-visible log for troubleshooting injection issues
            ErrorLogger.LogWarning($"InjectText: {text.Length} chars, mode={settings.TextInjectionMode}, window={_targetWindowHandle}");

            try
            {
                // Respect explicit user preferences - skip UI Automation for these modes
                if (settings.TextInjectionMode == TextInjectionMode.AlwaysType)
                {
                    InjectViaTyping(text);
#if DEBUG
                    ErrorLogger.LogMessage($"Text injected via typing (AlwaysType mode, {text.Length} chars)");
#endif
                    return;
                }

                if (settings.TextInjectionMode == TextInjectionMode.AlwaysPaste)
                {
                    InjectViaClipboard(text);
#if DEBUG
                    ErrorLogger.LogMessage($"Text injected via clipboard (AlwaysPaste mode, {text.Length} chars)");
#endif
                    return;
                }

                // Use smart logic for SmartAuto/PreferType/PreferPaste modes
                if (ShouldUseTyping(text))
                {
                    InjectViaTyping(text);
#if DEBUG
                    ErrorLogger.LogMessage($"Text injected via typing ({text.Length} chars)");
#endif
                }
                else
                {
                    InjectViaClipboard(text);
#if DEBUG
                    ErrorLogger.LogMessage($"Text injected via clipboard ({text.Length} chars)");
#endif
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("TextInjector.InjectText", ex);

                // Issue 7: Don't fall back to paste if user explicitly wants typing only
                if (settings.TextInjectionMode == TextInjectionMode.AlwaysType)
                {
                    ErrorLogger.LogWarning("InjectViaTyping failed and AlwaysType mode set - not falling back to clipboard");
                    throw new InvalidOperationException($"Text injection via typing failed: {ex.Message}", ex);
                }

                // Fallback to clipboard method if typing fails (for other modes)
                // HIGH-5 FIX: Re-capture foreground window before fallback paste
                // User may have switched windows during the failed typing attempt
                _targetWindowHandle = GetForegroundWindow();
                try
                {
#if DEBUG
                    ErrorLogger.LogMessage("Falling back to clipboard injection");
#endif
                    PasteViaClipboard(text);
                }
                catch
                {
                    throw new InvalidOperationException($"Failed to inject text: {ex.Message}", ex);
                }
            }
        }

        private bool ShouldUseTyping(string text)
        {
            // Decide based on user's preference setting
            switch (settings.TextInjectionMode)
            {
                case TextInjectionMode.AlwaysType:
                    return true;

                case TextInjectionMode.AlwaysPaste:
                    return false;

                case TextInjectionMode.PreferType:
                    // Use typing unless text is really long
                    return text.Length < LONG_TEXT_THRESHOLD;

                case TextInjectionMode.PreferPaste:
                    // Use paste unless in secure field or very short text
                    return IsInSecureField() || text.Length < 10;

                case TextInjectionMode.SmartAuto:
                default:
                    // Smart decision based on text length and context
                    if (IsInSecureField())
                        return true;
                    if (text.Length < SHORT_TEXT_THRESHOLD)
                        return true;
                    if (ContainsSensitiveContent(text))
                        return true;
                    return false;
            }
        }

        private bool ContainsSensitiveContent(string text)
        {
            // Check for patterns that might indicate passwords or sensitive data
            // This is a simple check - can be enhanced
            return text.Length < 30 && !text.Contains(" ") && ContainsSpecialChars(text);
        }

        private bool ContainsSpecialChars(string text)
        {
            foreach (char c in text)
            {
                if (!char.IsLetterOrDigit(c) && c != ' ')
                    return true;
            }
            return false;
        }

        private bool IsInSecureField()
        {
            // Check if we're in a password field by examining the focused window
            try
            {
                IntPtr focusedHandle = GetFocus();
                if (focusedHandle == IntPtr.Zero) return false;

                // Get window class name
                var className = new System.Text.StringBuilder(256);
                GetClassName(focusedHandle, className, className.Capacity);
                string classNameStr = className.ToString().ToLower();

                // Common password field indicators
                if (classNameStr.Contains("password") || classNameStr.Contains("secure"))
                    return true;

                // Check window text/title for password indicators
                var windowText = new System.Text.StringBuilder(256);
                SendMessage(focusedHandle, WM_GETTEXT, windowText.Capacity, windowText);
                string windowTextStr = windowText.ToString().ToLower();

                if (windowTextStr.Contains("password") ||
                    windowTextStr.Contains("passcode") ||
                    windowTextStr.Contains("pin") ||
                    windowTextStr.Contains("secret"))
                    return true;

                // Check if it's a password input style
                long style = GetWindowLong(focusedHandle, GWL_STYLE);
                if ((style & ES_PASSWORD) == ES_PASSWORD)
                    return true;
            }
            catch (Exception ex)
            {
                // If detection fails, err on the side of caution
                ErrorLogger.LogDebug($"Secure field detection failed: {ex.Message}");
                return false;
            }

            return false;
        }

        // Win32 API declarations for secure field detection
        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, System.Text.StringBuilder lParam);

        [DllImport("user32.dll")]
        private static extern long GetWindowLong(IntPtr hWnd, int nIndex);

        private const uint WM_GETTEXT = 0x000D;
        private const int GWL_STYLE = -16;
        private const long ES_PASSWORD = 0x0020;

        /// <summary>
        /// Gets adaptive typing delay based on focused application
        /// Returns optimal delay for compatibility vs speed balance
        /// </summary>
        private int GetTypingDelayForApplication()
        {
            string appName = GetFocusedApplicationName().ToLower();

            // Modern applications - minimal delay (1ms is 50% faster than original 2ms)
            // NOTE: 0ms was too aggressive and caused dropped keystrokes
            if (appName.Contains("notepad") ||
                appName.Contains("code") ||       // VS Code
                appName.Contains("chrome") ||
                appName.Contains("firefox") ||
                appName.Contains("edge") ||
                appName.Contains("slack") ||
                appName.Contains("discord") ||
                appName.Contains("teams"))
            {
                return 1; // 1ms - fast but reliable (was 0ms, caused drops)
            }

            // Office applications - standard delay for reliability
            if (appName.Contains("word") ||
                appName.Contains("excel") ||
                appName.Contains("powerpoint") ||
                appName.Contains("outlook"))
            {
                return 2; // 2ms - matches original reliability
            }

            // Terminal/console applications - need more delay
            if (appName.Contains("cmd") ||
                appName.Contains("powershell") ||
                appName.Contains("windowsterminal") ||
                appName.Contains("terminal"))
            {
                return 5; // 5ms - slower but reliable for terminals
            }

            // Default for unknown applications - match original behavior
            return 2; // 2ms - original TYPING_DELAY_MS value (proven reliable)
        }

        /// <summary>
        /// Wrapper method for getting current typing delay
        /// Enables future configuration/override if needed
        /// </summary>
        private int GetTypingDelay()
        {
            return GetTypingDelayForApplication();
        }

        private void InjectViaTyping(string text)
        {
            // Issue 6: Restore focus to target window before typing
            // For long transcriptions, user may have clicked away during processing
            // HIGH-4 FIX: Validate window handle is still valid before use
            if (_targetWindowHandle != IntPtr.Zero && IsWindow(_targetWindowHandle))
            {
                if (!SetForegroundWindow(_targetWindowHandle))
                {
                    ErrorLogger.LogWarning($"SetForegroundWindow failed for handle {_targetWindowHandle} in InjectViaTyping");
                }
                Thread.Sleep(50); // Allow window activation to complete
            }
            else if (_targetWindowHandle != IntPtr.Zero)
            {
                // Window was closed - log warning but continue (will type to current foreground window)
                ErrorLogger.LogWarning($"Target window {_targetWindowHandle} no longer valid in InjectViaTyping, using current foreground window");
            }

            // Get adaptive delay based on current application
            int delay = GetTypingDelay();

            // Type each character using InputSimulator
            foreach (char c in text)
            {
                if (c == '\n')
                {
                    inputSimulator.Keyboard.KeyPress(VirtualKeyCode.RETURN);
                }
                else if (c == '\t')
                {
                    inputSimulator.Keyboard.KeyPress(VirtualKeyCode.TAB);
                }
                else
                {
                    inputSimulator.Keyboard.TextEntry(c.ToString());
                }

                // Small delay to ensure keystrokes register properly (adaptive per-app)
                if (delay > 0)
                {
                    Thread.Sleep(delay);
                }
            }
        }

        private void InjectViaClipboard(string text)
        {
            // CRIT-5 FIX: Removed clipboard preservation logic - was causing race condition data loss
            // Now we just paste and clear after delay (simpler, safer)

            // CRIT-5 FIX: Removed clipboard clearing entirely
            // Data loss risk > cleanup benefit. Transcription text left in clipboard
            // is actually useful (user might paste again). Previous async clear could
            // delete user's unrelated clipboard content during the delay window.
            PasteViaClipboard(text);
        }

        private void PasteViaClipboard(string text)
        {
            // THREAD-LEAK FIX: Acquire semaphore to prevent unbounded thread accumulation
            // If previous operation timed out but thread is still running, we wait here
            if (!_clipboardSemaphore.Wait(TimeSpan.FromSeconds(5)))
            {
                ErrorLogger.LogWarning("Clipboard semaphore acquisition timed out - previous operation may be stuck");
                throw new InvalidOperationException("Clipboard operation busy. Please try again.");
            }

            Exception? workerException = null;
            // Issue 5: Use threadStarted instead of operationCompleted to avoid race condition
            // threadStarted is only written by the main thread, so no synchronization needed
            bool threadStarted = false;

            try
            {
                Thread thread = new Thread(() =>
                {
                    try
                    {
                        SetClipboardText(text);

                        if (AutoPaste)
                        {
                            // RELIABILITY FIX: Increased delay from 5ms to 20ms for better compatibility
                            // Some applications need more time to process clipboard changes
                            Thread.Sleep(CLIPBOARD_READY_DELAY_MS);
                            SimulateCtrlV();
                        }
                    }
                    catch (Exception ex)
                    {
                        workerException = ex;
                    }
                    finally
                    {
                        // THREAD-LEAK FIX: Release semaphore when thread completes (even on timeout path)
                        // This ensures next operation can proceed
                        // CRITICAL-2 FIX: Check _disposed before releasing to avoid ObjectDisposedException
                        // The volatile keyword ensures we see the latest value across threads
                        if (!_disposed)
                        {
                            try { _clipboardSemaphore.Release(); } catch (SemaphoreFullException) { } catch (ObjectDisposedException) { }
                        }
                    }
                });

                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                threadStarted = true; // Issue 5: Set AFTER Start() succeeds - thread will handle semaphore release

                // Issue 4: Align timeout with semaphore timeout (5s) to prevent false timeout errors
                if (!thread.Join(TimeSpan.FromSeconds(5)))
                {
                    // Thread timed out but is still running - semaphore will be released when it completes
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
                // Issue 5: Only release semaphore if thread never started
                // Once thread.Start() succeeds, thread's finally block handles semaphore release
                // CRITICAL-2 FIX: Also check _disposed to avoid ObjectDisposedException
                if (!threadStarted && !_disposed)
                {
                    try { _clipboardSemaphore.Release(); } catch (SemaphoreFullException) { } catch (ObjectDisposedException) { }
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
                    // CRITICAL PERFORMANCE FIX: Reduce exponential backoff delays
                    // Old: 50ms, 100ms, 150ms, 200ms, 250ms (total: 750ms worst case)
                    // New: 5ms, 10ms, 15ms, 20ms, 25ms (total: 75ms worst case)
                    // 90% reduction in retry delay time!
                    if (attempt < maxAttempts - 1) // Don't sleep on the last attempt
                    {
                        Thread.Sleep(5 * (attempt + 1));
                    }
                }
            }

            throw new InvalidOperationException("Unable to access clipboard.");
        }

        private void SimulateCtrlV()
        {
            try
            {
#if DEBUG
                ErrorLogger.LogMessage($"Simulating Ctrl+V to window handle: {_targetWindowHandle}");
#endif

                // CRITICAL FIX: Restore focus to target window before sending keystrokes
                // For long transcriptions (>5s), user may have clicked VoiceLite window to check progress
                // This ensures Ctrl+V goes to the ORIGINAL target app, not VoiceLite
                // HIGH-4 FIX: Validate window handle is still valid before use
                if (_targetWindowHandle != IntPtr.Zero && IsWindow(_targetWindowHandle))
                {
                    if (!SetForegroundWindow(_targetWindowHandle))
                    {
                        ErrorLogger.LogWarning($"SetForegroundWindow failed for handle {_targetWindowHandle} in SimulateCtrlV");
                    }
                    Thread.Sleep(50); // Allow window activation to complete
                }
                else if (_targetWindowHandle != IntPtr.Zero)
                {
                    // Window was closed - log warning but continue (will paste to current foreground window)
                    ErrorLogger.LogWarning($"Target window {_targetWindowHandle} no longer valid in SimulateCtrlV, using current foreground window");
                }

                // RELIABILITY FIX: Increased timing for better cross-application compatibility
                // Previous 1ms delays were too aggressive, causing paste failures in some apps
                // Balance between speed (still fast at 12ms total) and reliability

                // Press Ctrl
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYDOWN, 0);

                // Press V (5ms delay to ensure Ctrl is registered across all applications)
                Thread.Sleep(KEY_MODIFIER_DELAY_MS);
                keybd_event(VK_V, 0, KEYEVENTF_KEYDOWN, 0);

                // Small delay before releasing (helps with slower applications)
                Thread.Sleep(KEY_RELEASE_DELAY_MS);

                // Release V
                keybd_event(VK_V, 0, KEYEVENTF_KEYUP, 0);

                // Release Ctrl (5ms delay to ensure proper key sequence completion)
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

        // Interface implementation methods
        public async Task InjectTextAsync(string text, ITextInjector.InjectionMode mode)
        {
            await Task.Run(() =>
            {
                switch (mode)
                {
                    case ITextInjector.InjectionMode.Type:
                        InjectViaTyping(text);
                        break;
                    case ITextInjector.InjectionMode.Paste:
                        InjectViaClipboard(text);
                        break;
                    case ITextInjector.InjectionMode.SmartAuto:
                        InjectText(text); // Uses existing smart logic
                        break;
                }
            });
        }

        public bool CanInject()
        {
            try
            {
                // Check if we can get the foreground window
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

                uint processId;
                GetWindowThreadProcessId(hwnd, out processId);

                try
                {
                    var process = Process.GetProcessById((int)processId);
                    return process?.ProcessName ?? "Unknown";
                }
                catch (ArgumentException)
                {
                    // Issue 8: Process exited between GetWindowThreadProcessId and GetProcessById
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

        /// <summary>
        /// Sets the typing delay between keystrokes.
        /// Issue 9: This is intentionally a no-op because adaptive per-application
        /// delays are used instead (see GetTypingDelayForApplication). The delay
        /// is automatically optimized based on the target application for best
        /// balance between speed and reliability.
        /// </summary>
        /// <param name="millisecondsDelay">Ignored - adaptive delays are used instead</param>
        public void SetTypingDelay(int millisecondsDelay)
        {
            // Intentional no-op: Adaptive per-application delays are used instead
            // See GetTypingDelayForApplication() for the actual delay logic
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

        // HIGH-4 FIX: Add IsWindow() to validate window handles before use
        // Window may have been closed between capture and injection
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        /// <summary>
        /// Disposes the TextInjector and releases the clipboard semaphore.
        /// Issue 1: Prevents SemaphoreSlim leak on app exit.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Implements the standard Dispose pattern.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            if (disposing)
            {
                // Dispose managed resources
                try
                {
                    _clipboardSemaphore?.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, ignore
                }
            }
            // No unmanaged resources to release
        }

        /// <summary>
        /// Finalizer to catch cases where Dispose() was never called.
        /// </summary>
        ~TextInjector()
        {
            Dispose(false);
        }
    }
}

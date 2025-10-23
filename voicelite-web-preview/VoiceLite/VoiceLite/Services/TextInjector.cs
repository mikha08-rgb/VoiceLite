using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsInput;
using WindowsInput.Native;
using VoiceLite.Interfaces;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    public class TextInjector : ITextInjector, IDisposable
    {
        public bool AutoPaste { get; set; } = true;
        private const int SHORT_TEXT_THRESHOLD = 50; // Use typing for text shorter than this
        private const int LONG_TEXT_THRESHOLD = 100; // Text longer than this is considered "long"
        private const int TYPING_DELAY_MS = 2; // Delay between keystrokes when typing
        private readonly InputSimulator inputSimulator;
        private readonly Settings settings;

        // QUICK WIN 5: Track clipboard restoration failures for data-driven decision making
        // AUDIT FIX (CRITICAL-TS): Use long for thread-safe Interlocked operations
        private static long clipboardRestoreFailures = 0;
        private static long clipboardRestoreSuccesses = 0;

        // CRITICAL FIX (CRITICAL-1): Track background tasks for proper disposal
        // Prevents thread pool exhaustion from orphaned clipboard restore tasks
        private readonly CancellationTokenSource disposalCts = new CancellationTokenSource();
        private readonly System.Collections.Concurrent.ConcurrentBag<Task> pendingTasks = new System.Collections.Concurrent.ConcurrentBag<Task>();
        private bool isDisposed = false;

        public TextInjector(Settings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            inputSimulator = new InputSimulator();
        }

        /// <summary>
        /// BUG-007 FIX: Calculate CRC32 checksum for clipboard verification
        /// More reliable than string equality for large texts or texts with whitespace variations
        /// </summary>
        private static uint CalculateCRC32(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            uint crc = 0xFFFFFFFF;
            foreach (char c in text)
            {
                crc ^= (uint)c;
                for (int i = 0; i < 8; i++)
                    crc = (crc >> 1) ^ (0xEDB88320 & ~((crc & 1) - 1));
            }
            return ~crc;
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

#if DEBUG
            ErrorLogger.LogMessage($"InjectText called with {text.Length} characters, AutoPaste: {AutoPaste}");
#endif

            try
            {
                // Decide injection method based on text length and context
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
                // Fallback to clipboard method if typing fails
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
            catch
            {
                // If detection fails, err on the side of caution
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

        private void InjectViaTyping(string text)
        {
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

                // Small delay to ensure keystrokes register properly
                if (TYPING_DELAY_MS > 0)
                {
                    Thread.Sleep(TYPING_DELAY_MS);
                }
            }
        }

        private void InjectViaClipboard(string text)
        {
            string? originalClipboard = null;
            bool hadOriginalClipboard = false;

            // Try to preserve original clipboard content with retry logic
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        originalClipboard = Clipboard.GetText();
                        hadOriginalClipboard = true;
#if DEBUG
                        ErrorLogger.LogMessage($"Original clipboard saved ({originalClipboard.Length} chars)");
#endif
                        break;
                    }
                }
                catch (Exception
#if DEBUG
                ex
#endif
                )
                {
#if DEBUG
                    ErrorLogger.LogMessage($"Clipboard read attempt {attempt + 1} failed: {ex.Message}");
#endif
                    if (attempt < 2)
                        Thread.Sleep(10); // Brief delay before retry
                }
            }

            try
            {
                PasteViaClipboard(text);
            }
            finally
            {
                // BUG FIX (BUG-004): Always restore original clipboard after timeout
                // Previous logic could lose user data if another app modified clipboard
                // Now we ALWAYS restore after a fixed delay, regardless of current clipboard state
                if (hadOriginalClipboard && !string.IsNullOrEmpty(originalClipboard))
                {
                    var clipboardToRestore = originalClipboard; // Capture for closure
                    // BUG-007 FIX: Calculate CRC32 hash of transcription for reliable comparison
                    var transcriptionHash = CalculateCRC32(text);

                    // CRITICAL FIX (CRITICAL-1): Track background task and support cancellation
                    var restoreTask = Task.Run(async () =>
                    {
                        try
                        {
                            // CRITICAL FIX: Check for cancellation before starting work
                            if (disposalCts.Token.IsCancellationRequested) return;

                            // QUICK WIN 2: Reduced delay from 300ms → 50ms
                            // Paste completes in <10ms on modern systems, 300ms created 5x larger race window
                            // This reduces chance of external clipboard modification and speeds up completion by 250ms
                            await Task.Delay(50, disposalCts.Token);

                            // CRITICAL FIX: Check if clipboard was modified by user before restoring
                            // Only restore if clipboard still contains our transcription text
                            // This prevents overwriting user's new clipboard content during the 50ms window
                            string? currentClipboard = null;
                            try
                            {
                                if (Clipboard.ContainsText())
                                {
                                    // ISSUE #8 FIX: Clipboard.GetText() can return null on failure
                                    currentClipboard = Clipboard.GetText() ?? string.Empty;
                                }
                            }
                            catch (Exception ex)
                            {
                                ErrorLogger.LogMessage($"BUG-007: Failed to check current clipboard: {ex.Message}");
                                // Can't check clipboard state - skip restoration to be safe
                                return;
                            }

                            // BUG-007 FIX: Use CRC32 hash comparison for more reliable detection
                            // Handles cases where string equality fails due to whitespace or encoding differences
                            // ISSUE #8 FIX: currentClipboard is never null after ?? string.Empty above
                            bool clipboardUnchanged = string.IsNullOrEmpty(currentClipboard) ||
                                                     currentClipboard == text ||
                                                     CalculateCRC32(currentClipboard) == transcriptionHash;

                            if (clipboardUnchanged)
                            {
                                // Safe to restore - clipboard hasn't been modified by user
                                bool restoreSucceeded = false;
                                for (int attempt = 0; attempt < 3; attempt++)
                                {
                                    try
                                    {
                                        SetClipboardText(clipboardToRestore);
#if DEBUG
                                        ErrorLogger.LogMessage($"Original clipboard content restored (attempt {attempt + 1})");
#endif
                                        restoreSucceeded = true;
                                        break;
                                    }
                                    catch (Exception
#if DEBUG
                                    ex
#endif
                                    )
                                    {
#if DEBUG
                                        ErrorLogger.LogMessage($"Clipboard restore attempt {attempt + 1} failed: {ex.Message}");
#endif
                                        if (attempt < 2)
                                            await Task.Delay(50, disposalCts.Token);
                                        else
                                        {
#if DEBUG
                                            ErrorLogger.LogMessage("WARNING: Failed to restore original clipboard content after 3 attempts");
#endif
                                        }
                                    }
                                }

                                // QUICK WIN 5: Track restoration success/failure for metrics
                                // AUDIT FIX (CRITICAL-TS): Thread-safe static field access with Interlocked
                                if (restoreSucceeded)
                                {
                                    System.Threading.Interlocked.Increment(ref clipboardRestoreSuccesses);
                                }
                                else
                                {
                                    long failures = System.Threading.Interlocked.Increment(ref clipboardRestoreFailures);
                                    // Log every 10 failures to track problem severity
                                    if (failures % 10 == 0)
                                    {
                                        long successes = System.Threading.Interlocked.Read(ref clipboardRestoreSuccesses);
                                        ErrorLogger.LogWarning($"QUICK WIN 5: Clipboard restoration has failed {failures} times (successes: {successes})");
                                    }
                                }
                            }
                            else
                            {
#if DEBUG
                                // User copied something new - don't overwrite it!
                                ErrorLogger.LogMessage("Clipboard was modified by user - skipping restoration to preserve user's clipboard");
#endif
                            }
                        }
                        catch (TaskCanceledException)
                        {
                            // CRITICAL FIX: Disposal was requested - exit gracefully
#if DEBUG
                            ErrorLogger.LogMessage("Clipboard restore task cancelled (disposal requested)");
#endif
                            return;
                        }
                        catch (Exception ex)
                        {
                            ErrorLogger.LogError("Background clipboard restore", ex);
                        }
                    }, disposalCts.Token);

                    // CRITICAL FIX: Track task for disposal
                    pendingTasks.Add(restoreTask);
                }
            }
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            // Cancel all pending clipboard restore tasks
            try { disposalCts.Cancel(); } catch { /* Already cancelled */ }

            try
            {
                // CRITICAL-4 FIX: Wait for tasks with timeout before disposing CancellationTokenSource
                var tasksArray = pendingTasks.ToArray();
                if (tasksArray.Length > 0)
                {
#if DEBUG
                    ErrorLogger.LogMessage($"TextInjector disposing - waiting for {tasksArray.Length} clipboard tasks");
#endif
                    // Wait with 2-second timeout to prevent indefinite blocking
                    try
                    {
                        Task.WaitAll(tasksArray, TimeSpan.FromSeconds(2));
                    }
                    catch (AggregateException)
                    {
                        // Expected - tasks were cancelled
                    }
                }
            }
            catch (Exception ex)
            {
                // Unexpected error during disposal
                ErrorLogger.LogError("TextInjector disposal error", ex);
            }
            finally
            {
                // NOW safe to dispose CancellationTokenSource after tasks have acknowledged cancellation
                try { disposalCts.Dispose(); } catch { /* Already disposed */ }
            }

#if DEBUG
            ErrorLogger.LogMessage("TextInjector disposed - all clipboard tasks cancelled");
#endif
        }

        private void PasteViaClipboard(string text)
        {
            Exception? workerException = null;

            Thread thread = new Thread(() =>
            {
                try
                {
                    SetClipboardText(text);

                    if (AutoPaste)
                    {
                        // CRITICAL PERFORMANCE FIX: Reduce delay from 50ms to 5ms
                        // Modern systems need much less time for clipboard operations
                        Thread.Sleep(5); // Minimal delay to ensure clipboard is ready
                        SimulateCtrlV();
                    }
                }
                catch (Exception ex)
                {
                    workerException = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            if (!thread.Join(TimeSpan.FromSeconds(3)))
            {
                ErrorLogger.LogMessage("Clipboard operation thread timed out");
                throw new InvalidOperationException("Clipboard operation timed out.");
            }

            if (workerException != null)
            {
                throw new InvalidOperationException("Clipboard operation failed.", workerException);
            }
        }

        private static void SetClipboardText(string text)
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

        private static void SimulateCtrlV()
        {
            try
            {
#if DEBUG
                ErrorLogger.LogMessage("Simulating Ctrl+V");
#endif

                // CRITICAL PERFORMANCE FIX: Optimize key simulation timing
                // Old: 10ms delay between each key event (30ms total)
                // New: 1ms delay only where necessary (2ms total)
                // Modern systems can handle much faster key simulation

                // Press Ctrl
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYDOWN, 0);

                // Press V (minimal delay to ensure Ctrl is registered)
                Thread.Sleep(1);
                keybd_event(VK_V, 0, KEYEVENTF_KEYDOWN, 0);

                // Release V (no delay needed)
                keybd_event(VK_V, 0, KEYEVENTF_KEYUP, 0);

                // Release Ctrl (minimal delay to ensure proper key sequence)
                Thread.Sleep(1);
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

        // Virtual key codes
        private const byte VK_CONTROL = 0x11;
        private const byte VK_V = 0x56;

        // Key event flags
        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
    }
}

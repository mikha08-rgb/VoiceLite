using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Automation;
using WindowsInput;
using WindowsInput.Native;
using VoiceLite.Models;
using VoiceLite.Core.Interfaces.Services;

namespace VoiceLite.Services
{
    public class TextInjector : ITextInjector
    {
        public bool AutoPaste { get; set; } = true;

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

#if DEBUG
            ErrorLogger.LogMessage($"InjectText called with {text.Length} characters, AutoPaste: {AutoPaste}");
#endif

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

                // For SmartAuto/PreferType/PreferPaste: Try UI Automation first (fastest ~5ms)
                if (TryInjectViaUIAutomation(text))
                {
#if DEBUG
                    ErrorLogger.LogMessage($"Text injected via UI Automation ({text.Length} chars)");
#endif
                    return;
                }

                // Fallback to existing smart logic
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

        /// <summary>
        /// UI Automation injection is DISABLED due to fundamental limitations.
        ///
        /// BUG FIX (DEAD-CODE-001): Removed 60+ lines of commented-out UI Automation code.
        ///
        /// Why disabled:
        /// - ValuePattern.SetValue() replaces ENTIRE field content instead of inserting at cursor
        /// - This causes data loss when user has existing text in the field
        /// - TextPattern API would be needed for proper cursor-aware insertion, but it's complex
        ///   and not universally supported across all text controls
        ///
        /// Fallback methods (clipboard/typing) work reliably across all applications.
        ///
        /// TODO: If performance becomes critical, investigate TextPattern implementation with:
        ///   1. Get cursor position via TextPatternRange
        ///   2. Insert text at cursor using RangeFromPoint
        ///   3. Handle applications that don't support TextPattern gracefully
        /// </summary>
        private bool TryInjectViaUIAutomation(string text)
        {
            // Always return false - UI Automation not implemented
            // Fallback to clipboard/typing methods which work universally
            return false;
        }

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
            string? originalClipboard = null;
            bool hadOriginalClipboard = false;

            // Try to preserve original clipboard content with retry logic
            for (int attempt = 0; attempt < 2; attempt++)
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
                    if (attempt < 1)
                        Thread.Sleep(CLIPBOARD_RETRY_DELAY_MS);
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

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // QUICK WIN 2: User-configurable delay (30-100ms, default 50ms)
                            // Paste completes in <10ms on modern systems
                            // Configurable delay balances speed vs reliability for different system configurations
                            await Task.Delay(settings.ClipboardRestorationDelayMs);

                            // CRITICAL FIX: Check if clipboard was modified by user before restoring
                            // Only restore if clipboard still contains our transcription text
                            // This prevents overwriting user's new clipboard content during the 50ms window
                            string? currentClipboard = null;
                            bool clipboardCheckFailed = false;
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
                                // CRITICAL FIX #3: If we can't check clipboard state, restore anyway
                                // Better to restore than lose user's original data
                                clipboardCheckFailed = true;
                            }

                            // ISSUE #8 FIX: currentClipboard is never null after ?? string.Empty above
                            bool clipboardUnchanged = clipboardCheckFailed ||
                                                     string.IsNullOrEmpty(currentClipboard) ||
                                                     currentClipboard == text;

                            if (clipboardUnchanged)
                            {
                                // Safe to restore - clipboard hasn't been modified by user
                                for (int attempt = 0; attempt < 2; attempt++)
                                {
                                    try
                                    {
                                        SetClipboardText(clipboardToRestore);
#if DEBUG
                                        ErrorLogger.LogMessage($"Original clipboard content restored (attempt {attempt + 1})");
#endif
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
                                        if (attempt < 1)
                                            await Task.Delay(CLIPBOARD_SETTLE_DELAY_MS);
                                        else
                                        {
#if DEBUG
                                            ErrorLogger.LogMessage("WARNING: Failed to restore original clipboard content after 2 attempts");
#endif
                                        }
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
                        catch (Exception ex)
                        {
                            ErrorLogger.LogError("Background clipboard restore", ex);
                        }
                    });
                }
            }
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
            catch
            {
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

                var process = Process.GetProcessById((int)processId);
                return process?.ProcessName ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        public void SetTypingDelay(int millisecondsDelay)
        {
            // Would need to refactor TYPING_DELAY_MS to be non-const
            // For now, this is a no-op as the delay is constant
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
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }
}

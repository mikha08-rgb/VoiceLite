using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using WindowsInput;
using WindowsInput.Native;
using VoiceLite.Interfaces;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    public class TextInjector : ITextInjector
    {
        public bool AutoPaste { get; set; } = true;
        private const int SHORT_TEXT_THRESHOLD = 50; // Use typing for text shorter than this
        private const int LONG_TEXT_THRESHOLD = 100; // Text longer than this is considered "long"
        private const int TYPING_DELAY_MS = 2; // Delay between keystrokes when typing
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
                ErrorLogger.LogMessage("InjectText called with empty text");
                return;
            }

            ErrorLogger.LogMessage($"InjectText called with {text.Length} characters, AutoPaste: {AutoPaste}");

            try
            {
                // Decide injection method based on text length and context
                if (ShouldUseTyping(text))
                {
                    InjectViaTyping(text);
                    ErrorLogger.LogMessage($"Text injected via typing ({text.Length} chars)");
                }
                else
                {
                    InjectViaClipboard(text);
                    ErrorLogger.LogMessage($"Text injected via clipboard ({text.Length} chars)");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("TextInjector.InjectText", ex);
                // Fallback to clipboard method if typing fails
                try
                {
                    ErrorLogger.LogMessage("Falling back to clipboard injection");
                    PasteViaClipboard(text);
                }
                catch
                {
                    throw new Exception($"Failed to inject text: {ex.Message}", ex);
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
                        ErrorLogger.LogMessage($"Original clipboard saved ({originalClipboard.Length} chars)");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogMessage($"Clipboard read attempt {attempt + 1} failed: {ex.Message}");
                    if (attempt < 2)
                        Thread.Sleep(10); // Brief delay before retry
                }
            }

            bool clipboardRestored = false;

            try
            {
                PasteViaClipboard(text);
            }
            finally
            {
                // Restore original clipboard if we had saved it
                if (hadOriginalClipboard && !string.IsNullOrEmpty(originalClipboard))
                {
                    // Retry restoration up to 3 times
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        try
                        {
                            Thread.Sleep(100); // Wait for paste to complete
                            SetClipboardText(originalClipboard);
                            ErrorLogger.LogMessage($"Original clipboard content restored (attempt {attempt + 1})");
                            clipboardRestored = true;
                            break;
                        }
                        catch (Exception ex)
                        {
                            ErrorLogger.LogMessage($"Clipboard restore attempt {attempt + 1} failed: {ex.Message}");
                            if (attempt < 2)
                                Thread.Sleep(50);
                        }
                    }

                    // If restoration failed after all retries, log warning
                    if (!clipboardRestored)
                    {
                        ErrorLogger.LogMessage("WARNING: Failed to restore original clipboard content after 3 attempts");
                        // Note: We don't show a UI notification here as it would be disruptive
                        // Users can check logs if they notice clipboard issues
                    }
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
                    ErrorLogger.LogMessage("Text copied to clipboard successfully");
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
                ErrorLogger.LogMessage("Simulating Ctrl+V");

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

                ErrorLogger.LogMessage("Ctrl+V simulation completed");
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

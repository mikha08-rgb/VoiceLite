using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace VoiceLite.Services
{
    public class HotkeyManager : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int HOTKEY_ID = 9000;
        private const uint MOD_NONE = 0x0000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;

        private IntPtr windowHandle;
        private HwndSource? source;
        private bool isRegistered = false;
        private CancellationTokenSource? keyMonitorCts;

        private Key currentKey = Key.F1;
        private ModifierKeys currentModifiers = ModifierKeys.None;
        private uint currentVirtualKey = 0x70; // VK_F1
        private bool isKeyDown = false;

        private readonly object stateLock = new();
        private readonly Dispatcher dispatcher;

        public HotkeyManager()
        {
            dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        }

        public event EventHandler? HotkeyPressed;
        public event EventHandler? HotkeyReleased;

        public Key CurrentKey => currentKey;
        public ModifierKeys CurrentModifiers => currentModifiers;

        public void RegisterHotkey(IntPtr handle)
        {
            RegisterHotkey(handle, currentKey, currentModifiers);
        }

        public void RegisterHotkey(IntPtr handle, Key key, ModifierKeys modifiers)
        {
            if (isRegistered)
            {
                UnregisterCurrentHotkey();
            }

            windowHandle = handle;
            currentKey = key;
            currentModifiers = modifiers;
            currentVirtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);

            // CRITICAL FIX: Handle different key types appropriately
            bool isStandaloneModifier = IsModifierKey(key);
            bool isSpecialKey = IsSpecialKey(key);

            if (isStandaloneModifier)
            {
                // CRITICAL FIX: RegisterHotKey doesn't work reliably for standalone modifiers
                // Use polling method which actually works for Left Ctrl, Shift, etc.
                ErrorLogger.LogMessage($"Using polling method for standalone modifier key: {key}");
                StartPollingForModifierKey(key);
                isRegistered = true;
            }
            else if (isSpecialKey)
            {
                // Special keys like CapsLock - use polling since RegisterHotKey may not work
                ErrorLogger.LogMessage($"Using polling method for special key: {key}");
                StartPollingForModifierKey(key);
                isRegistered = true;
            }
            else
            {
                // For regular keys with or without modifiers, use RegisterHotKey
                uint win32Modifiers = ConvertModifiers(modifiers);

                source = HwndSource.FromHwnd(handle);
                if (source == null)
                {
                    throw new InvalidOperationException("Unable to acquire window source for hotkey registration.");
                }

                source.AddHook(HwndHook);

                if (!RegisterHotKey(windowHandle, HOTKEY_ID, win32Modifiers, currentVirtualKey))
                {
                    source.RemoveHook(HwndHook);
                    source = null;
                    throw new Exception($"Failed to register {modifiers} + {key} hotkey. It may be in use by another application.");
                }

                isRegistered = true;
            }
        }

        private bool IsModifierKey(Key key)
        {
            return key == Key.LeftCtrl || key == Key.RightCtrl ||
                   key == Key.LeftAlt || key == Key.RightAlt ||
                   key == Key.LeftShift || key == Key.RightShift ||
                   key == Key.LWin || key == Key.RWin;
            // REMOVED CapsLock - it's not a true modifier, should use regular RegisterHotKey
        }

        private bool IsSpecialKey(Key key)
        {
            // CapsLock and similar keys that need polling but aren't true modifiers
            return key == Key.CapsLock;
        }

        /// <summary>
        /// CRITICAL FIX: Convert standalone modifier keys to RegisterHotKey-compatible format
        /// Uses Windows tricks to make RegisterHotKey work with standalone modifiers
        /// </summary>
        private (ModifierKeys modifiers, Key key) GetSpecialRegistrationForModifier(Key modifierKey)
        {
            switch (modifierKey)
            {
                case Key.LeftCtrl:
                case Key.RightCtrl:
                    // Trick: Register Ctrl+Ctrl to capture standalone Ctrl presses
                    return (ModifierKeys.Control, Key.LeftCtrl);

                case Key.LeftAlt:
                case Key.RightAlt:
                    // Trick: Register Alt+Alt to capture standalone Alt presses
                    return (ModifierKeys.Alt, Key.LeftAlt);

                case Key.LeftShift:
                case Key.RightShift:
                    // Trick: Register Shift+Shift to capture standalone Shift presses
                    return (ModifierKeys.Shift, Key.LeftShift);

                case Key.LWin:
                case Key.RWin:
                    // Windows key is special - use a different approach
                    // Register Win+Win (this works on most systems)
                    return (ModifierKeys.Windows, Key.LWin);

                case Key.CapsLock:
                    // CapsLock is not a modifier in Windows API, treat as regular key
                    // No special RegisterHotKey tricks needed for CapsLock
                    return (ModifierKeys.None, Key.CapsLock);

                default:
                    // Fallback - should not reach here if IsModifierKey is correct
                    return (ModifierKeys.None, modifierKey);
            }
        }

        private void StartPollingForModifierKey(Key key)
        {
            StopKeyMonitor();

            int vKey = GetVirtualKeyForModifier(key);
            var cts = new CancellationTokenSource();

            lock (stateLock)
            {
                keyMonitorCts = cts;
            }

            Task.Run(async () =>
            {
                try
                {
                    while (!cts.IsCancellationRequested)
                    {
                        bool keyPressed = (GetAsyncKeyState(vKey) & 0x8000) != 0;
                        bool raisePressed = false;
                        bool raiseReleased = false;

                        lock (stateLock)
                        {
                            if (keyPressed && !isKeyDown)
                            {
                                isKeyDown = true;
                                raisePressed = true;
                            }
                            else if (!keyPressed && isKeyDown)
                            {
                                isKeyDown = false;
                                raiseReleased = true;
                            }
                        }

                        if (raisePressed)
                        {
                            RunOnDispatcher(() => HotkeyPressed?.Invoke(this, EventArgs.Empty));
                        }

                        if (raiseReleased)
                        {
                            RunOnDispatcher(() => HotkeyReleased?.Invoke(this, EventArgs.Empty));
                        }

                        await Task.Delay(15, cts.Token).ConfigureAwait(false);
                    }
                }
                catch (TaskCanceledException)
                {
                    // Expected during cancellation
                }
                finally
                {
                    lock (stateLock)
                    {
                        if (keyMonitorCts == cts)
                        {
                            keyMonitorCts = null;
                        }
                    }
                    cts.Dispose();
                }
            }, cts.Token);
        }

        private int GetVirtualKeyForModifier(Key key)
        {
            switch (key)
            {
                case Key.LeftCtrl: return 0xA2;   // VK_LCONTROL
                case Key.RightCtrl: return 0xA3;  // VK_RCONTROL
                case Key.LeftAlt: return 0xA4;    // VK_LMENU
                case Key.RightAlt: return 0xA5;   // VK_RMENU
                case Key.LeftShift: return 0xA0;  // VK_LSHIFT
                case Key.RightShift: return 0xA1; // VK_RSHIFT
                case Key.LWin: return 0x5B;       // VK_LWIN
                case Key.RWin: return 0x5C;       // VK_RWIN
                case Key.CapsLock: return 0x14;   // VK_CAPITAL (CapsLock)
                default: return (int)KeyInterop.VirtualKeyFromKey(key);
            }
        }

        private void StopKeyMonitor()
        {
            CancellationTokenSource? cts;
            lock (stateLock)
            {
                cts = keyMonitorCts;
                keyMonitorCts = null;
            }

            cts?.Cancel();
        }

        private void RunOnDispatcher(Action action)
        {
            if (dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.BeginInvoke(action);
            }
        }

        public void UnregisterCurrentHotkey()
        {
            if (isRegistered)
            {
                // Stop any polling for modifier keys
                StopKeyMonitor();

                // Unregister standard hotkey if it was registered
                if (windowHandle != IntPtr.Zero && !IsModifierKey(currentKey))
                {
                    UnregisterHotKey(windowHandle, HOTKEY_ID);
                    if (source != null)
                    {
                        source.RemoveHook(HwndHook);
                        source = null;
                    }
                }

                isRegistered = false;
            }
        }

        public void SimulateKeyRelease()
        {
            lock (stateLock)
            {
                if (!isKeyDown)
                {
                    return;
                }

                isKeyDown = false;
            }

            StopKeyMonitor();
            RunOnDispatcher(() => HotkeyReleased?.Invoke(this, EventArgs.Empty));
        }

        private uint ConvertModifiers(ModifierKeys modifiers)
        {
            uint result = MOD_NONE;

            if ((modifiers & ModifierKeys.Alt) != 0)
                result |= MOD_ALT;
            if ((modifiers & ModifierKeys.Control) != 0)
                result |= MOD_CONTROL;
            if ((modifiers & ModifierKeys.Shift) != 0)
                result |= MOD_SHIFT;
            if ((modifiers & ModifierKeys.Windows) != 0)
                result |= MOD_WIN;

            return result;
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                bool shouldStartMonitoring = false;

                lock (stateLock)
                {
                    if (!isKeyDown)
                    {
                        isKeyDown = true;
                        shouldStartMonitoring = true;
                    }
                }

                if (shouldStartMonitoring)
                {
                    HotkeyPressed?.Invoke(this, EventArgs.Empty);

                    // Start monitoring for key release (for push-to-talk mode)
                    StartReleaseMonitoring();
                }
                handled = true;
            }

            return IntPtr.Zero;
        }

        private void StartReleaseMonitoring()
        {
            StopKeyMonitor();

            var cts = new CancellationTokenSource();

            lock (stateLock)
            {
                keyMonitorCts = cts;
            }

            int vKey = (int)currentVirtualKey;

            Task.Run(async () =>
            {
                try
                {
                    while (!cts.IsCancellationRequested)
                    {
                        bool keyPressed = (GetAsyncKeyState(vKey) & 0x8000) != 0;
                        bool raiseRelease = false;

                        lock (stateLock)
                        {
                            if (!keyPressed && isKeyDown)
                            {
                                isKeyDown = false;
                                raiseRelease = true;
                            }
                        }

                        if (raiseRelease)
                        {
                            RunOnDispatcher(() => HotkeyReleased?.Invoke(this, EventArgs.Empty));
                            break;
                        }

                        await Task.Delay(10, cts.Token).ConfigureAwait(false);
                    }
                }
                catch (TaskCanceledException)
                {
                    // Expected when monitoring is canceled
                }
                finally
                {
                    lock (stateLock)
                    {
                        if (keyMonitorCts == cts)
                        {
                            keyMonitorCts = null;
                        }
                    }
                    cts.Dispose();
                }
            }, cts.Token);
        }

        public void Dispose()
        {
            StopKeyMonitor();

            if (isRegistered && windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(windowHandle, HOTKEY_ID);
                if (source != null)
                {
                    source.RemoveHook(HwndHook);
                    source = null;
                }
                isRegistered = false;
            }
        }
    }
}













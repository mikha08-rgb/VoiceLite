using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using VoiceLite.Core.Interfaces.Services;

namespace VoiceLite.Services
{
    public class HotkeyManager : IHotkeyManager
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
        private Task? keyMonitorTask; // DISPOSAL SAFETY: Track polling task to wait for completion
        private Task? cleanupWaitTask; // HIGH-7 FIX: Track background cleanup task for proper disposal
        private readonly ManualResetEventSlim unregisterComplete = new ManualResetEventSlim(false); // CRIT-003 FIX: Non-blocking coordination for unregister

        private Key currentKey = Key.LeftAlt;
        private ModifierKeys currentModifiers = ModifierKeys.None;
        private uint currentVirtualKey = 0xA4; // VK_LMENU (Left Alt)
        private bool isKeyDown = false;

        private readonly object stateLock = new();
        private readonly Dispatcher dispatcher;

        public HotkeyManager()
        {
            dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        }

        public event EventHandler<HotkeyEventArgs>? HotkeyPressed;
        public event EventHandler<HotkeyEventArgs>? HotkeyReleased;

        // BUG-006 FIX: Add event to notify UI when polling mode is activated
        // Allows UI to show notification that polling mode has slightly higher latency
        public event EventHandler<string>? PollingModeActivated;

        // Public properties for interface compliance
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

                // BUG-006 FIX: Notify UI that polling mode is active
                PollingModeActivated?.Invoke(this, $"Using polling mode for {key} - may have slightly higher latency than standard hotkeys");
            }
            else if (isSpecialKey)
            {
                // Special keys like CapsLock - use polling since RegisterHotKey may not work
                ErrorLogger.LogMessage($"Using polling method for special key: {key}");
                StartPollingForModifierKey(key);
                isRegistered = true;

                // BUG-006 FIX: Notify UI that polling mode is active
                PollingModeActivated?.Invoke(this, $"Using polling mode for {key} - may have slightly higher latency");
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
                    throw new InvalidOperationException($"Failed to register {modifiers} + {key} hotkey. It may be in use by another application.");
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

            var task = Task.Run(async () =>
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
                            RunOnDispatcher(() => HotkeyPressed?.Invoke(this, new HotkeyEventArgs
                            {
                                Key = key,
                                Modifiers = ModifierKeys.None
                            }));
                        }

                        if (raiseReleased)
                        {
                            RunOnDispatcher(() => HotkeyReleased?.Invoke(this, new HotkeyEventArgs { Key = currentKey, Modifiers = currentModifiers, HotkeyId = 0 }));
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

            lock (stateLock)
            {
                keyMonitorTask = task;
            }
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
            // BUG FIX (BLOCKING-001): Log warning if called on UI thread
            // This method can block for up to 2 seconds, so it should ideally not be called on UI thread
            // However, in tests and some disposal scenarios, it may be unavoidable
            if (dispatcher.CheckAccess())
            {
                ErrorLogger.LogWarning("StopKeyMonitor called on UI thread - may cause brief UI freeze (up to 2s)");
            }

            CancellationTokenSource? cts;
            Task? task;
            lock (stateLock)
            {
                cts = keyMonitorCts;
                task = keyMonitorTask;
                keyMonitorCts = null;
                keyMonitorTask = null;
            }

            // BUG FIX (BUG-006): Ensure CancellationTokenSource is always disposed
            if (cts != null)
            {
                try
                {
                    cts.Cancel();
                    cts.Dispose(); // Prevent resource leak
                }
                catch (ObjectDisposedException disposeEx)
                {
                    // Already disposed, this is acceptable during cleanup
                    ErrorLogger.LogWarning($"CancellationTokenSource already disposed during cleanup: {disposeEx.Message}");
                }
            }

            // CRIT-003 FIX: Use ManualResetEventSlim for non-blocking coordination instead of Task.Wait()
            // BUG FIX (BLOCKING-001): Reduced timeout from 5s to 2s for faster shutdown
            // ManualResetEventSlim allows efficient signaling without busy-waiting
            if (task != null && !task.IsCompleted)
            {
                try
                {
                    unregisterComplete.Reset(); // Reset signal before waiting

                    // Start async task to signal completion
                    _ = task.ContinueWith(_ => unregisterComplete.Set(), TaskScheduler.Default);

                    // CRIT-4 FIX: Avoid blocking UI thread
                    if (dispatcher.CheckAccess())
                    {
                        // HIGH-7 FIX: Track cleanup task so Dispose() can wait for it
                        // On UI thread - track task instead of fire-and-forget
                        cleanupWaitTask = Task.Run(() =>
                        {
                            try
                            {
                                if (!unregisterComplete.Wait(TimeSpan.FromSeconds(2)))
                                {
                                    ErrorLogger.LogWarning("HotkeyManager: Key monitor task did not complete within 2 seconds");
                                }
                            }
                            catch (ObjectDisposedException)
                            {
                                // Expected if disposed while waiting
                            }
                        });
                    }
                    else
                    {
                        // Background thread - safe to block directly
                        if (!unregisterComplete.Wait(TimeSpan.FromSeconds(2)))
                        {
                            ErrorLogger.LogWarning("HotkeyManager: Key monitor task did not complete within 2 seconds - continuing anyway");
                        }
                    }
                }
                catch (AggregateException ex) when (ex.InnerException is TaskCanceledException)
                {
                    // Expected during cancellation
                    unregisterComplete.Set(); // Signal completion
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("HotkeyManager: Error waiting for key monitor task", ex);
                    unregisterComplete.Set(); // Signal completion
                }
            }
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
            RunOnDispatcher(() => HotkeyReleased?.Invoke(this, new HotkeyEventArgs { Key = currentKey, Modifiers = currentModifiers, HotkeyId = 0 }));
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
                    HotkeyPressed?.Invoke(this, new HotkeyEventArgs
                    {
                        Key = currentKey,
                        Modifiers = currentModifiers,
                        HotkeyId = HOTKEY_ID
                    });

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

            var task = Task.Run(async () =>
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
                            RunOnDispatcher(() => HotkeyReleased?.Invoke(this, new HotkeyEventArgs { Key = currentKey, Modifiers = currentModifiers, HotkeyId = 0 }));
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

            lock (stateLock)
            {
                keyMonitorTask = task;
            }
        }

        #region IHotkeyManager Implementation

        public void RegisterHotkey(Window window)
        {
            var helper = new WindowInteropHelper(window);
            RegisterHotkey(helper.Handle);
        }

        public void UnregisterAllHotkeys()
        {
            UnregisterCurrentHotkey();
        }

        public bool UpdateHotkey(Key key, ModifierKeys modifiers)
        {
            try
            {
                UnregisterCurrentHotkey();
                currentKey = key;
                currentModifiers = modifiers;
                RegisterHotkey(windowHandle, key, modifiers);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string GetHotkeyString()
        {
            var result = "";
            if (currentModifiers.HasFlag(ModifierKeys.Control)) result += "Ctrl+";
            if (currentModifiers.HasFlag(ModifierKeys.Alt)) result += "Alt+";
            if (currentModifiers.HasFlag(ModifierKeys.Shift)) result += "Shift+";
            if (currentModifiers.HasFlag(ModifierKeys.Windows)) result += "Win+";
            result += currentKey.ToString();
            return result;
        }

        #endregion

        public void Dispose()
        {
            // DISPOSAL SAFETY: Clear event handlers FIRST to prevent callbacks after disposal
            HotkeyPressed = null;
            HotkeyReleased = null;

            StopKeyMonitor();

            // HIGH-7 FIX: Wait for background cleanup task to complete before disposing unregisterComplete
            if (cleanupWaitTask != null && !cleanupWaitTask.IsCompleted)
            {
                try
                {
                    // Wait briefly for cleanup task - don't block indefinitely
                    cleanupWaitTask.Wait(TimeSpan.FromSeconds(1));
                }
                catch (Exception ex) when (ex is AggregateException || ex is TaskCanceledException)
                {
                    // Expected during disposal
                }
            }

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

            // CRIT-003 FIX: Dispose ManualResetEventSlim to release resources
            unregisterComplete.SafeDispose();
        }
    }
}













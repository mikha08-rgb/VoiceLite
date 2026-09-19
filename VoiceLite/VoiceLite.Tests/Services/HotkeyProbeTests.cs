using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using AwesomeAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// HotkeyManager.CanRegisterHotkey: the Settings window's pre-save probe. Uses real
    /// RegisterHotKey calls, so the "free" combination is deliberately obscure
    /// (Ctrl+Alt+Shift+F12) to avoid colliding with whatever the dev box runs.
    /// </summary>
    public class HotkeyProbeTests
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004;
        private const ModifierKeys CtrlAltShift = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift;

        [Fact]
        public void FreeCombination_IsAvailable_AndProbeReleasesIt()
        {
            HotkeyManager.CanRegisterHotkey(Key.F12, CtrlAltShift).Should().BeTrue();

            // The probe must unregister after itself: a second probe of the same key
            // succeeds, and so does a real registration by someone else.
            HotkeyManager.CanRegisterHotkey(Key.F12, CtrlAltShift).Should().BeTrue();
            const int id = 0x7A12;
            RegisterHotKey(IntPtr.Zero, id, MOD_CONTROL | MOD_ALT | MOD_SHIFT, 0x7B /* VK_F12 */).Should().BeTrue(
                "the probe must not leave its temporary registration behind");
            UnregisterHotKey(IntPtr.Zero, id);
        }

        [Fact]
        public void CombinationTakenByAnotherRegistration_IsNotAvailable()
        {
            const int id = 0x7A11;
            RegisterHotKey(IntPtr.Zero, id, MOD_CONTROL | MOD_ALT | MOD_SHIFT, 0x7A /* VK_F11 */).Should().BeTrue(
                "Ctrl+Alt+Shift+F11 must be free on this machine for the test to be meaningful");
            try
            {
                HotkeyManager.CanRegisterHotkey(Key.F11, CtrlAltShift).Should().BeFalse();
            }
            finally
            {
                UnregisterHotKey(IntPtr.Zero, id);
            }
            HotkeyManager.CanRegisterHotkey(Key.F11, CtrlAltShift).Should().BeTrue("released again");
        }

        [Theory]
        [InlineData(Key.LeftCtrl)]
        [InlineData(Key.RightShift)]
        [InlineData(Key.LeftAlt)]
        [InlineData(Key.LWin)]
        [InlineData(Key.CapsLock)]
        public void PolledKeys_AreAlwaysAvailable(Key key)
        {
            // Standalone modifiers and CapsLock are handled by GetAsyncKeyState polling,
            // not RegisterHotKey, so there is nothing to probe.
            HotkeyManager.CanRegisterHotkey(key, ModifierKeys.None).Should().BeTrue();
        }

        [Fact]
        public void KeyNone_IsNotAvailable()
        {
            HotkeyManager.CanRegisterHotkey(Key.None, ModifierKeys.None).Should().BeFalse();
        }
    }
}

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Input;
using System.Windows.Interop;
using AwesomeAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// v2.5.2 hardening (F3): when the hotkey failed to register at startup (another app held
    /// the combination), closing Settings with the SAME hotkey skipped registration and logged
    /// nothing in Release, so freeing the combination and pressing Save could never re-arm it.
    /// </summary>
    public class HotkeyReregistrationAfterSettingsTests
    {
        [Fact]
        public void UnchangedHotkey_ThatIsNotRegistered_IsRetried()
        {
            MainWindow.ShouldRegisterHotkeyAfterSettings(hotkeyChanged: false, currentlyRegistered: false)
                .Should().BeTrue("Save is the user's only way to retry after closing the conflicting app");
        }

        [Fact]
        public void UnchangedHotkey_ThatIsRegistered_IsLeftAlone()
        {
            MainWindow.ShouldRegisterHotkeyAfterSettings(hotkeyChanged: false, currentlyRegistered: true)
                .Should().BeFalse("no unregister/register churn on every Settings save");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ChangedHotkey_IsAlwaysRegistered(bool currentlyRegistered)
        {
            MainWindow.ShouldRegisterHotkeyAfterSettings(hotkeyChanged: true, currentlyRegistered).Should().BeTrue();
        }

        [Fact]
        public void Wiring_SettingsHandlerUsesThePredicate_AndLogsReleaseVisibly()
        {
            var code = SettingsWindowHotkeyTests.ReadAppSource("MainWindow.xaml.cs");
            if (code == null) return; // test binaries copied away from the source tree

            // Assert.True/False on Contains: a Should().Contain failure would dump the whole file.
            Assert.True(code.Contains("if (ShouldRegisterHotkeyAfterSettings(hotkeyChanged, hotkeyManager?.IsRegistered == true))"),
                "SettingsButton_Click must decide on re-registration with ShouldRegisterHotkeyAfterSettings");
            Assert.False(code.Contains("ErrorLogger.LogMessage(\"Hotkey unchanged"),
                "LogMessage is Debug-only; the skip branch must be visible in Release logs");
            Assert.False(code.Contains("ErrorLogger.LogMessage($\"Failed to register new hotkey"),
                "LogMessage is Debug-only; a failed registration must be visible in Release logs");
            Assert.False(code.Contains("The old hotkey may still be active"),
                "false claim: the old HotkeyManager is disposed before the new registration is attempted");
        }

        // ---- HotkeyManager.IsRegistered: the state the predicate is fed ----

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004;
        private const ModifierKeys CtrlAltShift = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift;

        [Fact]
        public void IsRegistered_IsFalseAfterAFailedRegistration_AndTrueAfterASuccessfulRetry()
        {
            // Real RegisterHotKey calls on a dedicated STA thread with a real (invisible)
            // HwndSource. Ctrl+Alt+Shift+F10: obscure, and distinct from HotkeyProbeTests'
            // F11/F12 because xUnit runs test classes in parallel. Everything, including the
            // raw "other app" registration, stays on one thread: hWnd=NULL hotkeys are
            // thread-bound and only the registering thread can release them.
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                const int otherAppId = 0x7A10;
                HwndSource? source = null;
                try
                {
                    source = new HwndSource(new HwndSourceParameters("VoiceLiteHotkeyRetryTest") { Width = 0, Height = 0, WindowStyle = 0 });
                    // Precondition, not an assertion: if some other tool on this machine/runner
                    // already holds Ctrl+Alt+Shift+F10 the scenario cannot be staged, and that
                    // must not fail a release build (xUnit 2 has no runtime skip, so: return).
                    if (!RegisterHotKey(IntPtr.Zero, otherAppId, MOD_CONTROL | MOD_ALT | MOD_SHIFT, 0x79 /* VK_F10 */))
                        return;

                    using var manager = new HotkeyManager();
                    manager.IsRegistered.Should().BeFalse();

                    Action register = () => manager.RegisterHotkey(source.Handle, Key.F10, CtrlAltShift);
                    register.Should().Throw<InvalidOperationException>("another registration holds the combination");
                    manager.IsRegistered.Should().BeFalse("this is what tells Settings/MainWindow the hotkey is dead");

                    UnregisterHotKey(IntPtr.Zero, otherAppId); // the user closes the conflicting app
                    register.Should().NotThrow();
                    manager.IsRegistered.Should().BeTrue();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    UnregisterHotKey(IntPtr.Zero, otherAppId);
                    source?.Dispose();
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.True(failure == null, failure?.ToString());
        }
    }
}

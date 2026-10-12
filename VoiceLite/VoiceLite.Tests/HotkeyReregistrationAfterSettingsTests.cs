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

        // Ctrl+Alt+Shift+F10, then F9, F8: obscure, and distinct from HotkeyProbeTests' F11/F12
        // because xUnit runs test classes in parallel.
        private static readonly (uint Vk, Key Key)[] CandidateKeys =
        {
            (0x79 /* VK_F10 */, Key.F10),
            (0x78 /* VK_F9 */, Key.F9),
            (0x77 /* VK_F8 */, Key.F8),
        };

        /// <summary>
        /// Plays "the other app": takes the first candidate combination that is free. If some
        /// tool on this machine/runner holds one, the next is tried; if ALL are taken the
        /// scenario cannot be staged and the test FAILS with that reason - xUnit 2 has no
        /// runtime skip, and a bare return would report the F3 state as verified when nothing
        /// was asserted.
        /// </summary>
        private static (uint Vk, Key Key) StageConflictingRegistration(Func<uint, bool> tryRegister)
        {
            foreach (var candidate in CandidateKeys)
            {
                if (tryRegister(candidate.Vk))
                    return candidate;
            }

            Assert.Fail("Precondition not met: RegisterHotKey failed for Ctrl+Alt+Shift+F10, F9 and F8 (all held by other " +
                        "programs, or no interactive window station), so HotkeyManager.IsRegistered after a failed " +
                        "registration and after a retry is UNVERIFIED on this machine.");
            throw new InvalidOperationException("unreachable");
        }

        [Fact]
        public void StageConflictingRegistration_FallsBackToTheNextFreeCombination()
        {
            var tried = new System.Collections.Generic.List<uint>();
            var staged = StageConflictingRegistration(vk => { tried.Add(vk); return vk == 0x78; });

            staged.Key.Should().Be(Key.F9);
            tried.Should().Equal(0x79u, 0x78u);
        }

        [Fact]
        public void StageConflictingRegistration_FailsLoudly_WhenNoCombinationIsFree()
        {
            Action act = () => StageConflictingRegistration(_ => false);

            act.Should().Throw<Xunit.Sdk.XunitException>().WithMessage("*Precondition not met*UNVERIFIED*");
        }

        [Fact]
        public void IsRegistered_IsFalseAfterAFailedRegistration_AndTrueAfterASuccessfulRetry()
        {
            // Real RegisterHotKey calls on a dedicated STA thread with a real (invisible)
            // HwndSource. Everything, including the raw "other app" registration, stays on one
            // thread: hWnd=NULL hotkeys are thread-bound and only the registering thread can
            // release them.
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                const int otherAppId = 0x7A10;
                HwndSource? source = null;
                try
                {
                    source = new HwndSource(new HwndSourceParameters("VoiceLiteHotkeyRetryTest") { Width = 0, Height = 0, WindowStyle = 0 });
                    var staged = StageConflictingRegistration(
                        vk => RegisterHotKey(IntPtr.Zero, otherAppId, MOD_CONTROL | MOD_ALT | MOD_SHIFT, vk));

                    using var manager = new HotkeyManager();
                    manager.IsRegistered.Should().BeFalse();

                    Action register = () => manager.RegisterHotkey(source.Handle, staged.Key, CtrlAltShift);
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
            thread.Join(TimeSpan.FromSeconds(30)).Should().BeTrue("nothing on that thread pumps messages or waits");

            Assert.True(failure == null, failure?.ToString());
        }
    }
}

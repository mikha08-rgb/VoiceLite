using System;
using System.Threading;
using System.Windows.Input;
using AwesomeAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    public sealed class HotkeyManagerTests : IDisposable
    {
        private readonly HotkeyManager hotkeyManager = new();

        #region Constructor & Default Values Tests

        [Fact]
        public void Constructor_UsesPushToTalkDefaults()
        {
            hotkeyManager.CurrentKey.Should().Be(Key.LeftAlt);
            hotkeyManager.CurrentModifiers.Should().Be(ModifierKeys.None);
        }

        [Fact]
        public void Constructor_InitializesSuccessfully()
        {
            // Arrange & Act
            using var manager = new HotkeyManager();

            // Assert
            manager.Should().NotBeNull();
            manager.CurrentKey.Should().Be(Key.LeftAlt);
            manager.CurrentModifiers.Should().Be(ModifierKeys.None);
        }

        #endregion

        #region RegisterHotkey Tests - Modifier Keys

        [Fact]
        public void RegisterHotkey_WithModifierKey_DoesNotRequireWindowHandle()
        {
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftCtrl, ModifierKeys.None);

            hotkeyManager.CurrentKey.Should().Be(Key.LeftCtrl);
            hotkeyManager.CurrentModifiers.Should().Be(ModifierKeys.None);
        }

        [Fact]
        public void RegisterHotkey_WithLeftAlt_RegistersSuccessfully()
        {
            // Act
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftAlt, ModifierKeys.None);

            // Assert
            hotkeyManager.CurrentKey.Should().Be(Key.LeftAlt);
            hotkeyManager.CurrentModifiers.Should().Be(ModifierKeys.None);
        }

        [Fact]
        public void RegisterHotkey_WithRightAlt_RegistersSuccessfully()
        {
            // Act
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.RightAlt, ModifierKeys.None);

            // Assert
            hotkeyManager.CurrentKey.Should().Be(Key.RightAlt);
        }

        [Fact]
        public void RegisterHotkey_WithLeftCtrl_RegistersSuccessfully()
        {
            // Act
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftCtrl, ModifierKeys.None);

            // Assert
            hotkeyManager.CurrentKey.Should().Be(Key.LeftCtrl);
        }

        [Fact]
        public void RegisterHotkey_WithRightCtrl_RegistersSuccessfully()
        {
            // Act
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.RightCtrl, ModifierKeys.None);

            // Assert
            hotkeyManager.CurrentKey.Should().Be(Key.RightCtrl);
        }

        [Fact]
        public void RegisterHotkey_WithLeftShift_RegistersSuccessfully()
        {
            // Act
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftShift, ModifierKeys.None);

            // Assert
            hotkeyManager.CurrentKey.Should().Be(Key.LeftShift);
        }

        [Fact]
        public void RegisterHotkey_WithRightShift_RegistersSuccessfully()
        {
            // Act
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.RightShift, ModifierKeys.None);

            // Assert
            hotkeyManager.CurrentKey.Should().Be(Key.RightShift);
        }

        [Fact]
        public void RegisterHotkey_WithCapsLock_RegistersSuccessfully()
        {
            // Act
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.CapsLock, ModifierKeys.None);

            // Assert
            hotkeyManager.CurrentKey.Should().Be(Key.CapsLock);
        }

        [Fact]
        public void RegisterHotkey_ReplacesExistingRegistration()
        {
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftCtrl, ModifierKeys.None);
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.RightAlt, ModifierKeys.None);

            hotkeyManager.CurrentKey.Should().Be(Key.RightAlt);
        }

        [Fact]
        public void RegisterHotkey_MultipleTimes_ReplacesEachTime()
        {
            // Act
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftCtrl, ModifierKeys.None);
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftShift, ModifierKeys.None);
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftAlt, ModifierKeys.None);

            // Assert
            hotkeyManager.CurrentKey.Should().Be(Key.LeftAlt, "last registration should win");
        }

        #endregion

        #region RegisterHotkey Tests - Standard Keys

        [Fact]
        public void RegisterHotkey_WithStandardKeyRequiresWindowHandle()
        {
            Action act = () => hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.F2, ModifierKeys.Control);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void RegisterHotkey_WithStandardKey_ThrowsOnZeroHandle()
        {
            // Act
            Action act = () => hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.A, ModifierKeys.Control);

            // Assert
            act.Should().Throw<Exception>("standard keys require a window handle");
        }

        #endregion

        #region UnregisterHotkey Tests

        [Fact]
        public void UnregisterCurrentHotkey_IsIdempotent()
        {
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftCtrl, ModifierKeys.None);

            hotkeyManager.UnregisterCurrentHotkey();
            hotkeyManager.UnregisterCurrentHotkey();
        }

        [Fact]
        public void UnregisterCurrentHotkey_WithoutRegistration_DoesNotThrow()
        {
            // Act
            Action act = () => hotkeyManager.UnregisterCurrentHotkey();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void UnregisterCurrentHotkey_AfterRegistration_CompletesSuccessfully()
        {
            // Arrange
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftAlt, ModifierKeys.None);

            // Act
            Action act = () => hotkeyManager.UnregisterCurrentHotkey();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void UnregisterAllHotkeys_UnregistersCurrentHotkey()
        {
            // Arrange
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftCtrl, ModifierKeys.None);

            // Act
            hotkeyManager.UnregisterAllHotkeys();

            // Assert - Second call should not throw (already unregistered)
            Action act = () => hotkeyManager.UnregisterAllHotkeys();
            act.Should().NotThrow();
        }

        #endregion

        #region UpdateHotkey Tests

        [Fact]
        public void UpdateHotkey_WithModifierKey_ReturnsTrue()
        {
            // Arrange - First register a hotkey
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftAlt, ModifierKeys.None);

            // Act
            bool result = hotkeyManager.UpdateHotkey(Key.LeftCtrl, ModifierKeys.None);

            // Assert
            result.Should().BeTrue();
            hotkeyManager.CurrentKey.Should().Be(Key.LeftCtrl);
        }

        [Fact]
        public void UpdateHotkey_UpdatesCurrentKey()
        {
            // Arrange
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftAlt, ModifierKeys.None);

            // Act
            hotkeyManager.UpdateHotkey(Key.RightAlt, ModifierKeys.None);

            // Assert
            hotkeyManager.CurrentKey.Should().Be(Key.RightAlt);
            hotkeyManager.CurrentModifiers.Should().Be(ModifierKeys.None);
        }

        [Fact]
        public void UpdateHotkey_WithoutInitialRegistration_ReturnsFalse()
        {
            // Act - Try to update without registering first
            bool result = hotkeyManager.UpdateHotkey(Key.F2, ModifierKeys.Control);

            // Assert - Should return false because no window handle was set
            result.Should().BeFalse();
        }

        #endregion

        #region GetHotkeyString Tests

        [Fact]
        public void GetHotkeyString_WithNoModifiers_ReturnsKeyName()
        {
            // Arrange
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftAlt, ModifierKeys.None);

            // Act
            string result = hotkeyManager.GetHotkeyString();

            // Assert
            result.Should().Be("LeftAlt");
        }

        [Fact]
        public void GetHotkeyString_WithControl_ReturnsCtrlPlusKey()
        {
            // Arrange
            using var manager = new HotkeyManager();

            // Use reflection to set modifiers without actual registration (for testing)
            var field = typeof(HotkeyManager).GetField("currentModifiers",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(manager, ModifierKeys.Control);

            var keyField = typeof(HotkeyManager).GetField("currentKey",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            keyField?.SetValue(manager, Key.A);

            // Act
            string result = manager.GetHotkeyString();

            // Assert
            result.Should().Be("Ctrl+A");
        }

        [Fact]
        public void GetHotkeyString_WithAlt_ReturnsAltPlusKey()
        {
            // Arrange
            using var manager = new HotkeyManager();

            var field = typeof(HotkeyManager).GetField("currentModifiers",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(manager, ModifierKeys.Alt);

            var keyField = typeof(HotkeyManager).GetField("currentKey",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            keyField?.SetValue(manager, Key.F5);

            // Act
            string result = manager.GetHotkeyString();

            // Assert
            result.Should().Be("Alt+F5");
        }

        [Fact]
        public void GetHotkeyString_WithShift_ReturnsShiftPlusKey()
        {
            // Arrange
            using var manager = new HotkeyManager();

            var field = typeof(HotkeyManager).GetField("currentModifiers",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(manager, ModifierKeys.Shift);

            var keyField = typeof(HotkeyManager).GetField("currentKey",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            keyField?.SetValue(manager, Key.Tab);

            // Act
            string result = manager.GetHotkeyString();

            // Assert
            result.Should().Be("Shift+Tab");
        }

        [Fact]
        public void GetHotkeyString_WithMultipleModifiers_ReturnsCombination()
        {
            // Arrange
            using var manager = new HotkeyManager();

            var field = typeof(HotkeyManager).GetField("currentModifiers",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(manager, ModifierKeys.Control | ModifierKeys.Alt);

            var keyField = typeof(HotkeyManager).GetField("currentKey",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            keyField?.SetValue(manager, Key.Delete);

            // Act
            string result = manager.GetHotkeyString();

            // Assert
            result.Should().Be("Ctrl+Alt+Delete");
        }

        [Fact]
        public void GetHotkeyString_WithAllModifiers_ReturnsFullCombination()
        {
            // Arrange
            using var manager = new HotkeyManager();

            var field = typeof(HotkeyManager).GetField("currentModifiers",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(manager, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Windows);

            var keyField = typeof(HotkeyManager).GetField("currentKey",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            keyField?.SetValue(manager, Key.F1);

            // Act
            string result = manager.GetHotkeyString();

            // Assert
            result.Should().Be("Ctrl+Alt+Shift+Win+F1");
        }

        #endregion

        #region Event Handling Tests

        [Fact]
        public void HotkeyPressed_EventCanBeSubscribed()
        {
            // Arrange
            bool eventFired = false;
            hotkeyManager.HotkeyPressed += (sender, args) => eventFired = true;

            // Assert - Just verify subscription doesn't throw
            eventFired.Should().BeFalse("event hasn't been triggered yet");
        }

        [Fact]
        public void HotkeyReleased_EventCanBeSubscribed()
        {
            // Arrange
            bool eventFired = false;
            hotkeyManager.HotkeyReleased += (sender, args) => eventFired = true;

            // Assert - Just verify subscription doesn't throw
            eventFired.Should().BeFalse("event hasn't been triggered yet");
        }

        [Fact]
        public void PollingModeActivated_EventCanBeSubscribed()
        {
            // Arrange
            bool eventFired = false;
            hotkeyManager.PollingModeActivated += (sender, message) => eventFired = true;

            // Assert - Just verify subscription doesn't throw
            eventFired.Should().BeFalse("event hasn't been triggered yet");
        }

        [Fact]
        public void PollingModeActivated_FiresWhenRegisteringModifierKey()
        {
            // Arrange
            bool eventFired = false;
            string? eventMessage = null;
            hotkeyManager.PollingModeActivated += (sender, message) =>
            {
                eventFired = true;
                eventMessage = message;
            };

            // Act
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftCtrl, ModifierKeys.None);

            // Assert
            eventFired.Should().BeTrue("polling mode should be activated for modifier key");
            eventMessage.Should().NotBeNullOrEmpty();
            eventMessage.Should().Contain("LeftCtrl");
        }

        [Fact]
        public void PollingModeActivated_FiresWhenRegisteringSpecialKey()
        {
            // Arrange
            bool eventFired = false;
            hotkeyManager.PollingModeActivated += (sender, message) => eventFired = true;

            // Act
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.CapsLock, ModifierKeys.None);

            // Assert
            eventFired.Should().BeTrue("polling mode should be activated for CapsLock");
        }

        #endregion

        #region SimulateKeyRelease Tests

        [Fact]
        public void SimulateKeyRelease_WhenKeyNotDown_DoesNotThrow()
        {
            // Act
            Action act = () => hotkeyManager.SimulateKeyRelease();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void SimulateKeyRelease_CanBeCalledMultipleTimes()
        {
            // Act
            hotkeyManager.SimulateKeyRelease();
            Action act = () => hotkeyManager.SimulateKeyRelease();

            // Assert
            act.Should().NotThrow();
        }

        #endregion

        #region Disposal Tests

        [Fact]
        public void Dispose_CanBeInvokedMultipleTimes()
        {
            hotkeyManager.Dispose();
            hotkeyManager.Invoking(m => m.Dispose()).Should().NotThrow();
        }

        [Fact]
        public void Dispose_ClearsEventHandlers()
        {
            // Arrange
            hotkeyManager.HotkeyPressed += (s, e) => { };
            hotkeyManager.HotkeyReleased += (s, e) => { };

            // Act
            hotkeyManager.Dispose();

            // Assert - Should not throw when disposed
            Action act = () => hotkeyManager.Dispose();
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_AfterRegistration_CleansUpCorrectly()
        {
            // Arrange
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftAlt, ModifierKeys.None);

            // Act
            hotkeyManager.Dispose();

            // Assert - Second dispose should not throw
            Action act = () => hotkeyManager.Dispose();
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_WithActivePolling_StopsPolling()
        {
            // Arrange
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftCtrl, ModifierKeys.None);

            // Act - Give polling time to start
            Thread.Sleep(50);
            hotkeyManager.Dispose();

            // Assert - Should complete without hanging
            Action act = () => hotkeyManager.Dispose();
            act.Should().NotThrow();
        }

        #endregion

        #region Property Tests

        [Fact]
        public void CurrentKey_AfterRegistration_ReturnsRegisteredKey()
        {
            // Act
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.RightShift, ModifierKeys.None);

            // Assert
            hotkeyManager.CurrentKey.Should().Be(Key.RightShift);
        }

        [Fact]
        public void CurrentModifiers_AfterRegistration_ReturnsRegisteredModifiers()
        {
            // Act
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftAlt, ModifierKeys.None);

            // Assert
            hotkeyManager.CurrentModifiers.Should().Be(ModifierKeys.None);
        }

        [Fact]
        public void CurrentKey_DefaultValue_IsLeftAlt()
        {
            // Assert
            hotkeyManager.CurrentKey.Should().Be(Key.LeftAlt, "default push-to-talk key");
        }

        [Fact]
        public void CurrentModifiers_DefaultValue_IsNone()
        {
            // Assert
            hotkeyManager.CurrentModifiers.Should().Be(ModifierKeys.None, "default has no modifiers");
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void RegisterHotkey_SameKeyTwice_DoesNotThrow()
        {
            // Act
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftAlt, ModifierKeys.None);
            Action act = () => hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftAlt, ModifierKeys.None);

            // Assert
            act.Should().NotThrow("re-registering same key should work");
        }

        [Fact]
        public void RegisterHotkey_DifferentModifierKeys_EachWorksIndependently()
        {
            // Act & Assert - Each should register without throwing
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftCtrl, ModifierKeys.None);
            hotkeyManager.CurrentKey.Should().Be(Key.LeftCtrl);

            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.RightCtrl, ModifierKeys.None);
            hotkeyManager.CurrentKey.Should().Be(Key.RightCtrl);

            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftShift, ModifierKeys.None);
            hotkeyManager.CurrentKey.Should().Be(Key.LeftShift);
        }

        [Fact]
        public void UnregisterCurrentHotkey_BeforeAnyRegistration_DoesNotThrow()
        {
            // Arrange - Fresh manager, never registered
            using var manager = new HotkeyManager();

            // Act
            Action act = () => manager.UnregisterCurrentHotkey();

            // Assert
            act.Should().NotThrow();
        }

        #endregion

        public void Dispose()
        {
            hotkeyManager.Dispose();
        }
    }
}

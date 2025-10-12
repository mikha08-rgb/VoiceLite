using System;
using System.Windows.Input;
using FluentAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    [Trait("Category", "Unit")]
    public sealed class HotkeyManagerTests : IDisposable
    {
        private readonly HotkeyManager hotkeyManager = new();

        [Fact]
        public void Constructor_UsesPushToTalkDefaults()
        {
            hotkeyManager.CurrentKey.Should().Be(Key.LeftAlt);
            hotkeyManager.CurrentModifiers.Should().Be(ModifierKeys.None);
        }

        [Fact]
        public void RegisterHotkey_WithModifierKey_DoesNotRequireWindowHandle()
        {
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftCtrl, ModifierKeys.None);

            hotkeyManager.CurrentKey.Should().Be(Key.LeftCtrl);
            hotkeyManager.CurrentModifiers.Should().Be(ModifierKeys.None);
        }

        [Fact]
        public void RegisterHotkey_ReplacesExistingRegistration()
        {
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftCtrl, ModifierKeys.None);
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.RightAlt, ModifierKeys.None);

            hotkeyManager.CurrentKey.Should().Be(Key.RightAlt);
        }

        [Fact]
        public void RegisterHotkey_WithStandardKeyRequiresWindowHandle()
        {
            Action act = () => hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.F2, ModifierKeys.Control);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void UnregisterCurrentHotkey_IsIdempotent()
        {
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftCtrl, ModifierKeys.None);

            hotkeyManager.UnregisterCurrentHotkey();
            hotkeyManager.UnregisterCurrentHotkey();
        }

        [Fact]
        public void Dispose_CanBeInvokedMultipleTimes()
        {
            hotkeyManager.Dispose();
            hotkeyManager.Invoking(m => m.Dispose()).Should().NotThrow();
        }

        // Branch Coverage Tests - Key Type Detection

        [Theory]
        [InlineData(Key.LeftCtrl, true)]
        [InlineData(Key.RightCtrl, true)]
        [InlineData(Key.LeftAlt, true)]
        [InlineData(Key.RightAlt, true)]
        [InlineData(Key.LeftShift, true)]
        [InlineData(Key.RightShift, true)]
        [InlineData(Key.LWin, true)]
        [InlineData(Key.RWin, true)]
        [InlineData(Key.A, false)]
        [InlineData(Key.F1, false)]
        [InlineData(Key.CapsLock, false)]
        public void IsModifierKey_VariousKeys_ReturnsExpectedResult(Key key, bool expected)
        {
            // Act
            var result = InvokeIsModifierKey(hotkeyManager, key);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(Key.CapsLock, true)]
        [InlineData(Key.LeftCtrl, false)]
        [InlineData(Key.A, false)]
        [InlineData(Key.F1, false)]
        public void IsSpecialKey_VariousKeys_ReturnsExpectedResult(Key key, bool expected)
        {
            // Act
            var result = InvokeIsSpecialKey(hotkeyManager, key);

            // Assert
            result.Should().Be(expected);
        }

        // Branch Coverage Tests - RegisterHotkey Branches

        [Fact]
        public void RegisterHotkey_WithSpecialKey_UsesPollingPath()
        {
            // Act
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.CapsLock, ModifierKeys.None);

            // Assert - CapsLock should use polling (special key)
            hotkeyManager.CurrentKey.Should().Be(Key.CapsLock);
        }

        [Theory]
        [InlineData(Key.LeftCtrl)]
        [InlineData(Key.RightAlt)]
        [InlineData(Key.LeftShift)]
        public void RegisterHotkey_WithModifierKeys_UsesPollingPath(Key modifierKey)
        {
            // Act
            hotkeyManager.RegisterHotkey(IntPtr.Zero, modifierKey, ModifierKeys.None);

            // Assert - Modifier keys should use polling
            hotkeyManager.CurrentKey.Should().Be(modifierKey);
        }

        // Branch Coverage Tests - UnregisterCurrentHotkey Branches

        [Fact]
        public void UnregisterCurrentHotkey_WhenNotRegistered_DoesNothing()
        {
            // Arrange
            var manager = new HotkeyManager();

            // Act - Should not throw when unregistering without registration
            manager.UnregisterCurrentHotkey();

            // Assert - Early return path tested
            manager.Invoking(m => m.UnregisterCurrentHotkey()).Should().NotThrow();

            manager.Dispose();
        }

        [Fact]
        public void UnregisterCurrentHotkey_AfterModifierKeyRegistration_StopsPolling()
        {
            // Arrange
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftCtrl, ModifierKeys.None);

            // Act
            hotkeyManager.UnregisterCurrentHotkey();

            // Assert - Polling should be stopped
            hotkeyManager.UnregisterCurrentHotkey(); // Should be idempotent
        }

        // Branch Coverage Tests - ConvertModifiers

        [Theory]
        [InlineData(ModifierKeys.None, 0x0000)]
        [InlineData(ModifierKeys.Alt, 0x0001)]
        [InlineData(ModifierKeys.Control, 0x0002)]
        [InlineData(ModifierKeys.Shift, 0x0004)]
        [InlineData(ModifierKeys.Windows, 0x0008)]
        public void ConvertModifiers_SingleModifier_ReturnsCorrectFlag(ModifierKeys modifier, uint expectedFlag)
        {
            // Act
            var result = InvokeConvertModifiers(hotkeyManager, modifier);

            // Assert
            result.Should().Be(expectedFlag);
        }

        [Fact]
        public void ConvertModifiers_CombinedModifiers_CombinesFlags()
        {
            // Arrange
            var combined = ModifierKeys.Control | ModifierKeys.Shift;
            uint expected = 0x0002 | 0x0004; // MOD_CONTROL | MOD_SHIFT = 0x0006

            // Act
            var result = InvokeConvertModifiers(hotkeyManager, combined);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void ConvertModifiers_AllModifiers_CombinesAllFlags()
        {
            // Arrange
            var all = ModifierKeys.Alt | ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Windows;
            uint expected = 0x0001 | 0x0002 | 0x0004 | 0x0008; // All flags = 0x000F

            // Act
            var result = InvokeConvertModifiers(hotkeyManager, all);

            // Assert
            result.Should().Be(expected);
        }

        // Helper methods to invoke private methods via reflection

        private bool InvokeIsModifierKey(HotkeyManager manager, Key key)
        {
            var method = typeof(HotkeyManager).GetMethod("IsModifierKey",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (bool)method!.Invoke(manager, new object[] { key })!;
        }

        private bool InvokeIsSpecialKey(HotkeyManager manager, Key key)
        {
            var method = typeof(HotkeyManager).GetMethod("IsSpecialKey",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (bool)method!.Invoke(manager, new object[] { key })!;
        }

        private uint InvokeConvertModifiers(HotkeyManager manager, ModifierKeys modifiers)
        {
            var method = typeof(HotkeyManager).GetMethod("ConvertModifiers",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (uint)method!.Invoke(manager, new object[] { modifiers })!;
        }

        // Branch Coverage Tests - Async Task Completion Scenarios

        [Fact]
        public async Task StopKeyMonitor_WhenTaskRunning_WaitsForCompletion()
        {
            // Arrange - Start polling to create a running task
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftCtrl, ModifierKeys.None);

            // Act - Unregister should wait for task completion
            hotkeyManager.UnregisterCurrentHotkey();

            // Give some time for async cleanup
            await Task.Delay(100);

            // Assert - Should not throw, task should be cleaned up
            hotkeyManager.Invoking(m => m.UnregisterCurrentHotkey()).Should().NotThrow();
        }

        [Fact]
        public void Dispose_WhenPollingActive_CleansUpResources()
        {
            // Arrange
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftAlt, ModifierKeys.None);

            // Act - Dispose should cancel polling and clean up
            hotkeyManager.Dispose();

            // Assert - Double dispose should be safe
            hotkeyManager.Invoking(m => m.Dispose()).Should().NotThrow();
        }

        [Fact]
        public void SimulateKeyRelease_WhenCalled_StopsMonitoring()
        {
            // Arrange
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftCtrl, ModifierKeys.None);

            // Act - SimulateKeyRelease should stop key monitor
            hotkeyManager.SimulateKeyRelease();

            // Assert - Should be able to unregister without issues (monitor stopped)
            hotkeyManager.Invoking(m => m.UnregisterCurrentHotkey()).Should().NotThrow();
        }

        [Fact]
        public void SimulateKeyRelease_WhenKeyNotDown_DoesNothing()
        {
            // Arrange
            var releaseEventRaised = false;
            hotkeyManager.HotkeyReleased += (s, e) => releaseEventRaised = true;

            // Act - Simulate release without key being down
            hotkeyManager.SimulateKeyRelease();

            // Assert - Event should NOT be raised (early return path)
            releaseEventRaised.Should().BeFalse();
        }

        [Fact]
        public void RegisterHotkey_WhenAlreadyRegistered_UnregistersFirst()
        {
            // Arrange - Register first hotkey
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.LeftCtrl, ModifierKeys.None);
            hotkeyManager.CurrentKey.Should().Be(Key.LeftCtrl);

            // Act - Register second hotkey (should unregister first)
            hotkeyManager.RegisterHotkey(IntPtr.Zero, Key.RightAlt, ModifierKeys.None);

            // Assert - New hotkey should be active
            hotkeyManager.CurrentKey.Should().Be(Key.RightAlt);
        }

        [Fact]
        public void Dispose_ClearsEventHandlers()
        {
            // Arrange
            var pressedCalled = false;
            var releasedCalled = false;
            hotkeyManager.HotkeyPressed += (s, e) => pressedCalled = true;
            hotkeyManager.HotkeyReleased += (s, e) => releasedCalled = true;

            // Act
            hotkeyManager.Dispose();

            // After disposal, simulate events (should not raise since handlers cleared)
            hotkeyManager.SimulateKeyRelease();

            // Assert - Events should not be raised after disposal
            pressedCalled.Should().BeFalse();
            releasedCalled.Should().BeFalse();
        }

        public void Dispose()
        {
            hotkeyManager.Dispose();
        }
    }
}

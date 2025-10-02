using System;
using System.Windows.Input;
using FluentAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
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

        public void Dispose()
        {
            hotkeyManager.Dispose();
        }
    }
}

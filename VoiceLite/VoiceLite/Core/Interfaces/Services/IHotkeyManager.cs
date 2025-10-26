using System;
using System.Windows;
using System.Windows.Input;

namespace VoiceLite.Core.Interfaces.Services
{
    /// <summary>
    /// Interface for global hotkey management
    /// </summary>
    public interface IHotkeyManager : IDisposable
    {
        /// <summary>
        /// Raised when a registered hotkey is pressed
        /// </summary>
        event EventHandler<HotkeyEventArgs> HotkeyPressed;

        /// <summary>
        /// Raised when a registered hotkey is released
        /// </summary>
        event EventHandler<HotkeyEventArgs> HotkeyReleased;

        /// <summary>
        /// Gets the current hotkey
        /// </summary>
        Key CurrentKey { get; }

        /// <summary>
        /// Gets the current modifier keys
        /// </summary>
        ModifierKeys CurrentModifiers { get; }

        /// <summary>
        /// Registers a global hotkey
        /// </summary>
        /// <param name="window">The window to receive hotkey messages</param>
        void RegisterHotkey(Window window);

        /// <summary>
        /// Registers a global hotkey
        /// </summary>
        /// <param name="handle">The window handle to receive hotkey messages</param>
        void RegisterHotkey(IntPtr handle);

        /// <summary>
        /// Unregisters all hotkeys
        /// </summary>
        void UnregisterAllHotkeys();

        /// <summary>
        /// Updates the hotkey combination
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="modifiers">The modifier keys</param>
        bool UpdateHotkey(Key key, ModifierKeys modifiers);

        /// <summary>
        /// Gets the current hotkey combination as a string
        /// </summary>
        string GetHotkeyString();
    }

    /// <summary>
    /// Event arguments for hotkey events
    /// </summary>
    public class HotkeyEventArgs : EventArgs
    {
        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }
        public int HotkeyId { get; set; }
    }
}
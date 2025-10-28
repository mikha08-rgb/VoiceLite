using System;

namespace VoiceLite.Models
{
    /// <summary>
    /// Represents a custom text expansion shortcut.
    /// When the trigger phrase is spoken, it gets replaced with the replacement text.
    /// </summary>
    public class CustomShortcut
    {
        /// <summary>
        /// Unique identifier for this shortcut
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The trigger phrase to listen for (e.g., "my email")
        /// </summary>
        public string Trigger { get; set; } = string.Empty;

        /// <summary>
        /// The replacement text to inject (e.g., "john@example.com")
        /// </summary>
        public string Replacement { get; set; } = string.Empty;

        /// <summary>
        /// Whether this shortcut is currently active
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// When this shortcut was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Display-friendly preview of the shortcut for UI binding
        /// </summary>
        public string DisplayPreview => $"{Trigger} → {Replacement}";
    }
}

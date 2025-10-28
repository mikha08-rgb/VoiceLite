namespace VoiceLite.Core.Interfaces.Services
{
    /// <summary>
    /// Service for processing custom text expansion shortcuts.
    /// Replaces trigger phrases with their defined replacement text.
    /// </summary>
    public interface ICustomShortcutService
    {
        /// <summary>
        /// Process the input text and replace any matching shortcuts.
        /// </summary>
        /// <param name="text">The text to process (after Whisper transcription and post-processing)</param>
        /// <returns>Text with shortcuts replaced</returns>
        string ProcessShortcuts(string text);
    }
}

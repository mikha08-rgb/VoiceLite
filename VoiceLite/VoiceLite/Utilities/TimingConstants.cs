namespace VoiceLite.Utilities
{
    /// <summary>
    /// Centralized timing constants for debouncing, delays, and timeouts
    /// </summary>
    public static class TimingConstants
    {
        /// <summary>
        /// Debounce delay for button clicks to prevent rapid clicking (300ms)
        /// </summary>
        public const int ClickDebounceMs = 300;

        /// <summary>
        /// Debounce delay for hotkey presses to prevent rapid key events (250ms)
        /// </summary>
        public const int HotkeyDebounceMs = 250;

        /// <summary>
        /// Debounce delay for settings saves to batch writes (500ms)
        /// </summary>
        public const int SettingsSaveDebounceMs = 500;

        /// <summary>
        /// Delay before reverting temporary status messages (1500ms / 1.5 seconds)
        /// </summary>
        public const int StatusRevertDelayMs = 1500;

        /// <summary>
        /// Delay between file cleanup retry attempts (100ms)
        /// </summary>
        public const int FileCleanupRetryDelayMs = 100;

        /// <summary>
        /// Maximum number of retry attempts for file cleanup
        /// </summary>
        public const int FileCleanupMaxRetries = 3;

        /// <summary>
        /// Delay before resetting transcription text to ready state (3000ms / 3 seconds)
        /// </summary>
        public const int TranscriptionTextResetDelayMs = 3000;
    }
}

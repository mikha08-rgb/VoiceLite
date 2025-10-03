using System.Windows.Media;

namespace VoiceLite.Utilities
{
    /// <summary>
    /// Centralized status colors for consistent UI theming
    /// </summary>
    public static class StatusColors
    {
        /// <summary>
        /// Red color for recording state (#E74C3C)
        /// </summary>
        public static readonly Color Recording = Color.FromRgb(231, 76, 60);

        /// <summary>
        /// Orange color for processing/transcribing state (#F39C12)
        /// </summary>
        public static readonly Color Processing = Color.FromRgb(243, 156, 18);

        /// <summary>
        /// Green color for ready/success state (#27AE60)
        /// </summary>
        public static readonly Color Ready = Color.FromRgb(39, 174, 96);

        /// <summary>
        /// Gray color for inactive/cancelled state
        /// </summary>
        public static readonly Color Inactive = Colors.Gray;

        /// <summary>
        /// Red color for error state
        /// </summary>
        public static readonly Color Error = Colors.Red;

        /// <summary>
        /// Blue color for informational state
        /// </summary>
        public static readonly Color Info = Colors.Blue;
    }
}

using System.Threading.Tasks;

namespace VoiceLite.Core.Interfaces.Services
{
    /// <summary>
    /// Interface for text injection into applications
    /// </summary>
    public interface ITextInjector
    {
        /// <summary>
        /// Text injection modes
        /// </summary>
        public enum InjectionMode
        {
            /// <summary>
            /// Type text character by character
            /// </summary>
            Type,

            /// <summary>
            /// Paste text using clipboard
            /// </summary>
            Paste,

            /// <summary>
            /// Automatically choose between Type and Paste based on text length
            /// </summary>
            SmartAuto
        }

        /// <summary>
        /// Injects text into the active application
        /// </summary>
        /// <param name="text">The text to inject</param>
        /// <param name="mode">The injection mode to use</param>
        Task InjectTextAsync(string text, InjectionMode mode);

        /// <summary>
        /// Determines if text injection is currently possible
        /// </summary>
        bool CanInject();

        /// <summary>
        /// Gets the name of the currently focused application
        /// </summary>
        string GetFocusedApplicationName();

        /// <summary>
        /// Sets the delay between keystrokes for Type mode
        /// </summary>
        void SetTypingDelay(int millisecondsDelay);
    }
}
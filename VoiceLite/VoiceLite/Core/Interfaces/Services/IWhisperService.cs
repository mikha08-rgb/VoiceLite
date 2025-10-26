using System;
using System.Threading.Tasks;

namespace VoiceLite.Core.Interfaces.Services
{
    /// <summary>
    /// Interface for Whisper AI transcription service
    /// </summary>
    public interface IWhisperService : IDisposable
    {
        /// <summary>
        /// Gets whether transcription is currently in progress
        /// </summary>
        bool IsProcessing { get; }

        /// <summary>
        /// Raised when transcription is complete
        /// </summary>
        event EventHandler<string> TranscriptionComplete;

        /// <summary>
        /// Raised when a transcription error occurs
        /// </summary>
        event EventHandler<Exception> TranscriptionError;

        /// <summary>
        /// Raised to report transcription progress
        /// </summary>
        event EventHandler<int> ProgressChanged;

        /// <summary>
        /// Transcribes an audio file using the specified model
        /// </summary>
        /// <param name="audioFilePath">Path to the audio file</param>
        /// <param name="modelPath">Path to the Whisper model</param>
        /// <returns>The transcribed text</returns>
        Task<string> TranscribeAsync(string audioFilePath, string modelPath);

        /// <summary>
        /// Cancels the current transcription operation
        /// </summary>
        void CancelTranscription();

        /// <summary>
        /// Validates that the Whisper executable is available
        /// </summary>
        bool ValidateWhisperExecutable();

        /// <summary>
        /// Gets the current Whisper version
        /// </summary>
        string GetWhisperVersion();
    }
}
using System;
using System.Threading.Tasks;

namespace VoiceLite.Core.Interfaces.Services
{
    /// <summary>
    /// Interface for audio recording functionality
    /// </summary>
    public interface IAudioRecorder : IDisposable
    {
        /// <summary>
        /// Gets whether recording is currently in progress
        /// </summary>
        bool IsRecording { get; }

        /// <summary>
        /// Raised when an audio file is ready for processing
        /// </summary>
        event EventHandler<string> AudioFileReady;

        /// <summary>
        /// Raised when a recording error occurs
        /// </summary>
        event EventHandler<Exception> RecordingError;

        /// <summary>
        /// Starts recording audio
        /// </summary>
        void StartRecording();

        /// <summary>
        /// Stops the current recording
        /// </summary>
        void StopRecording();

        /// <summary>
        /// Gets the path to the last recorded audio file
        /// </summary>
        Task<string> GetLastAudioFileAsync();

        /// <summary>
        /// Validates that the audio system is properly configured
        /// </summary>
        bool ValidateAudioSystem();
    }
}
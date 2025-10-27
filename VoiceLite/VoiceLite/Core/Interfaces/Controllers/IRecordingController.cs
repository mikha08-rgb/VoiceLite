using System;
using System.Threading.Tasks;
using VoiceLite.Core.Interfaces.Services;

namespace VoiceLite.Core.Interfaces.Controllers
{
    /// <summary>
    /// Interface for orchestrating the recording and transcription workflow
    /// </summary>
    public interface IRecordingController
    {
        /// <summary>
        /// Gets whether recording is currently in progress
        /// </summary>
        bool IsRecording { get; }

        /// <summary>
        /// Gets whether transcription is currently in progress
        /// </summary>
        bool IsTranscribing { get; }

        /// <summary>
        /// Starts the recording and transcription workflow
        /// </summary>
        /// <param name="modelPath">Path to the Whisper model to use</param>
        /// <param name="injectionMode">Text injection mode</param>
        /// <returns>The result of the transcription</returns>
        Task<TranscriptionResult> RecordAndTranscribeAsync(
            string modelPath,
            ITextInjector.InjectionMode injectionMode);

        /// <summary>
        /// Starts recording only (without automatic transcription)
        /// </summary>
        Task StartRecordingAsync();

        /// <summary>
        /// Stops recording and optionally transcribes
        /// </summary>
        /// <param name="transcribe">Whether to transcribe after stopping</param>
        /// <returns>The transcription result if transcribe is true</returns>
        Task<TranscriptionResult> StopRecordingAsync(bool transcribe = true);

        /// <summary>
        /// Cancels the current operation
        /// </summary>
        void Cancel();

        /// <summary>
        /// Transcribes an existing audio file
        /// </summary>
        /// <param name="audioFilePath">Path to the audio file</param>
        /// <param name="modelPath">Path to the Whisper model</param>
        /// <returns>The transcription result</returns>
        Task<TranscriptionResult> TranscribeFileAsync(string audioFilePath, string modelPath);

        /// <summary>
        /// Raised when recording starts
        /// </summary>
        event EventHandler RecordingStarted;

        /// <summary>
        /// Raised when recording stops
        /// </summary>
        event EventHandler RecordingStopped;

        /// <summary>
        /// Raised when transcription starts
        /// </summary>
        event EventHandler TranscriptionStarted;

        /// <summary>
        /// Raised when transcription completes
        /// </summary>
        event EventHandler<TranscriptionResult> TranscriptionCompleted;

        /// <summary>
        /// Raised to report progress
        /// </summary>
        event EventHandler<RecordingProgress> ProgressChanged;
    }

    /// <summary>
    /// Result of a transcription operation
    /// </summary>
    public class TranscriptionResult
    {
        public bool Success { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public TimeSpan ProcessingTime { get; set; }
        public string ModelUsed { get; set; } = string.Empty;
        public string AudioFilePath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Progress information for recording/transcription
    /// </summary>
    public class RecordingProgress
    {
        public string Status { get; set; } = string.Empty;
        public int PercentComplete { get; set; }
        public TimeSpan Elapsed { get; set; }
        public bool IsIndeterminate { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VoiceLite.Core.Interfaces.Controllers
{
    /// <summary>
    /// Interface for managing transcription workflows and batch operations
    /// </summary>
    public interface ITranscriptionController
    {
        /// <summary>
        /// Processes a queue of audio files for transcription
        /// </summary>
        /// <param name="audioFiles">List of audio file paths</param>
        /// <param name="modelPath">Path to the Whisper model</param>
        /// <returns>List of transcription results</returns>
        Task<IEnumerable<TranscriptionResult>> BatchTranscribeAsync(
            IEnumerable<string> audioFiles,
            string modelPath);

        /// <summary>
        /// Retries a failed transcription
        /// </summary>
        /// <param name="audioFilePath">Path to the audio file</param>
        /// <param name="modelPath">Path to the Whisper model</param>
        /// <param name="maxRetries">Maximum number of retry attempts</param>
        /// <returns>The transcription result</returns>
        Task<TranscriptionResult> RetryTranscriptionAsync(
            string audioFilePath,
            string modelPath,
            int maxRetries = 3);

        /// <summary>
        /// Gets the optimal model for a given audio file
        /// </summary>
        /// <param name="audioFilePath">Path to the audio file</param>
        /// <returns>Recommended model name</returns>
        Task<string> GetRecommendedModelAsync(string audioFilePath);

        /// <summary>
        /// Validates that transcription prerequisites are met
        /// </summary>
        /// <returns>Validation result with any issues found</returns>
        Task<ValidationResult> ValidateTranscriptionSetupAsync();

        /// <summary>
        /// Cleans up temporary transcription files
        /// </summary>
        /// <param name="olderThan">Delete files older than this timespan</param>
        /// <returns>Number of files cleaned up</returns>
        Task<int> CleanupTemporaryFilesAsync(TimeSpan olderThan);

        /// <summary>
        /// Gets statistics about transcription performance
        /// </summary>
        TranscriptionStatistics GetStatistics();

        /// <summary>
        /// Raised when a batch transcription item completes
        /// </summary>
        event EventHandler<BatchProgressEventArgs> BatchItemCompleted;
    }

    /// <summary>
    /// Validation result for transcription setup
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Issues { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// Statistics about transcription performance
    /// </summary>
    public class TranscriptionStatistics
    {
        public int TotalTranscriptions { get; set; }
        public int SuccessfulTranscriptions { get; set; }
        public int FailedTranscriptions { get; set; }
        public TimeSpan AverageProcessingTime { get; set; }
        public TimeSpan TotalProcessingTime { get; set; }
        public Dictionary<string, int> ModelUsageCount { get; set; }
        public DateTime LastTranscription { get; set; }
    }

    /// <summary>
    /// Event arguments for batch progress
    /// </summary>
    public class BatchProgressEventArgs : EventArgs
    {
        public int CurrentItem { get; set; }
        public int TotalItems { get; set; }
        public string CurrentFile { get; set; }
        public TranscriptionResult Result { get; set; }
    }
}
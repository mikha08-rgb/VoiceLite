using System.Collections.Generic;

namespace VoiceLite.Core.Interfaces.Services
{
    /// <summary>
    /// Service for resolving Whisper model and executable paths.
    /// Handles model discovery across multiple installation locations.
    /// </summary>
    public interface IModelResolverService
    {
        /// <summary>
        /// Resolves the full path to whisper.exe executable.
        /// </summary>
        /// <returns>Full path to whisper.exe</returns>
        /// <exception cref="System.IO.FileNotFoundException">Thrown when whisper.exe is not found</exception>
        string ResolveWhisperExePath();

        /// <summary>
        /// Resolves the full path to the specified Whisper model file.
        /// Searches in bundled and downloaded model locations.
        /// </summary>
        /// <param name="modelName">Model name (e.g., "tiny", "base", "ggml-small.bin")</param>
        /// <returns>Full path to the model file</returns>
        /// <exception cref="System.IO.FileNotFoundException">Thrown when model file is not found</exception>
        string ResolveModelPath(string modelName);

        /// <summary>
        /// Gets all available model file paths currently installed.
        /// Searches both bundled and downloaded model locations.
        /// </summary>
        /// <returns>List of full paths to available model files</returns>
        IEnumerable<string> GetAvailableModelPaths();

        /// <summary>
        /// Validates the integrity of the whisper.exe executable.
        /// Performs SHA256 hash verification against known good binary.
        /// </summary>
        /// <param name="whisperExePath">Path to whisper.exe to validate</param>
        /// <returns>True if validation passes or fails open (allows execution with warning)</returns>
        bool ValidateWhisperExecutable(string whisperExePath);

        /// <summary>
        /// Normalizes a model name to its full filename.
        /// Supports short names (tiny, base) and full filenames (ggml-tiny.bin).
        /// </summary>
        /// <param name="modelName">Model name to normalize</param>
        /// <returns>Full model filename (e.g., "ggml-base.bin")</returns>
        string NormalizeModelName(string modelName);
    }
}

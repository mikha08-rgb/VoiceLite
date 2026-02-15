using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VoiceLite.Core.Interfaces.Features;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    /// <summary>
    /// Service for resolving Whisper model paths.
    /// SECURITY FIX (MODEL-GATE-001): Added Pro license validation to prevent freemium bypass
    /// </summary>
    public class ModelResolverService
    {
        private readonly string _baseDir;
        private readonly IProFeatureService? _proFeatureService;

        public ModelResolverService(string baseDirectory, IProFeatureService? proFeatureService = null)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentNullException(nameof(baseDirectory));

            _baseDir = baseDirectory;
            _proFeatureService = proFeatureService;
        }

        /// <summary>
        /// Resolves the full path to the specified Whisper model file.
        /// Searches in: baseDir/whisper/, baseDir/, %LocalAppData%/VoiceLite/whisper/
        /// SECURITY FIX (MODEL-GATE-001): Validates Pro license before returning Pro models
        /// </summary>
        public string ResolveModelPath(string modelName)
        {
            var modelFile = NormalizeModelName(modelName);

            // SECURITY FIX (MODEL-GATE-001): Validate Pro license before resolving Pro models
            if (_proFeatureService != null && !_proFeatureService.CanUseModel(modelFile))
            {
                throw new UnauthorizedAccessException(
                    $"Model '{GetModelDisplayName(modelFile)}' requires Pro license.\n\n" +
                    _proFeatureService.GetUpgradeMessage("Advanced AI Models") + "\n\n" +
                    "Free tier includes Swift model (ggml-base.bin) which provides excellent accuracy for most users.\n" +
                    "Upgrade to Pro to unlock:\n" +
                    "- Pro model (90-93% accuracy)\n" +
                    "- Elite model (95-97% accuracy)\n" +
                    "- Turbo model (97-99% accuracy, 3-4x faster than Ultra)\n" +
                    "- Ultra model (97-99% Dragon-level quality)");
            }

            // Check bundled models in Program Files (read-only)
            var modelPath = Path.Combine(_baseDir, "whisper", modelFile);
            if (File.Exists(modelPath))
                return modelPath;

            modelPath = Path.Combine(_baseDir, modelFile);
            if (File.Exists(modelPath))
                return modelPath;

            // Check downloaded models in LocalApplicationData (user-writable)
            var localDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoiceLite",
                "whisper",
                modelFile
            );
            if (File.Exists(localDataPath))
                return localDataPath;

            // Model not found - provide helpful error message
            throw new FileNotFoundException(
                $"Whisper model '{modelFile}' not found.\n\n" +
                $"Please download it from Settings -> AI Models tab, or reinstall VoiceLite.\n\n" +
                $"Expected locations:\n" +
                $"- Bundled: {Path.Combine(_baseDir, "whisper", modelFile)}\n" +
                $"- Downloaded: {localDataPath}");
        }

        private string GetModelDisplayName(string modelFile)
        {
            var name = WhisperModelInfo.GetDisplayName(modelFile);
            return name == "Unknown" ? modelFile : name;
        }

        /// <summary>
        /// Normalizes a model name to its full filename.
        /// Supports short names (tiny, base, small, medium, turbo, large) and full filenames.
        /// </summary>
        public string NormalizeModelName(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
                return "ggml-base.bin"; // Default fallback

            return modelName.ToLower() switch
            {
                "tiny" => "ggml-tiny.bin",
                "base" => "ggml-base.bin",
                "small" => "ggml-small.bin",
                "medium" => "ggml-medium.bin",
                "turbo" => "ggml-large-v3-turbo-q8_0.bin",
                "large" => "ggml-large-v3.bin",
                _ => modelName.EndsWith(".bin") ? modelName : "ggml-base.bin"
            };
        }

        /// <summary>
        /// Gets all available model file paths currently installed.
        /// Searches both bundled (Program Files) and downloaded (LocalAppData) locations.
        /// </summary>
        public IEnumerable<string> GetAvailableModelPaths()
        {
            var availablePaths = new List<string>();

            // Search bundled models in Program Files
            var bundledWhisperDir = Path.Combine(_baseDir, "whisper");
            if (Directory.Exists(bundledWhisperDir))
            {
                availablePaths.AddRange(Directory.GetFiles(bundledWhisperDir, "ggml-*.bin"));
            }

            // Search base directory
            if (Directory.Exists(_baseDir))
            {
                availablePaths.AddRange(Directory.GetFiles(_baseDir, "ggml-*.bin"));
            }

            // Search downloaded models in LocalAppData
            var localWhisperDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoiceLite",
                "whisper"
            );
            if (Directory.Exists(localWhisperDir))
            {
                availablePaths.AddRange(Directory.GetFiles(localWhisperDir, "ggml-*.bin"));
            }

            // Return distinct paths (in case same file exists in multiple locations)
            return availablePaths.Distinct().OrderBy(p => Path.GetFileName(p));
        }
    }
}

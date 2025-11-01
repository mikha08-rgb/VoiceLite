using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VoiceLite.Core.Interfaces.Features;
using VoiceLite.Core.Interfaces.Services;

namespace VoiceLite.Services
{
    /// <summary>
    /// Service for resolving Whisper model and executable paths.
    /// Extracted from PersistentWhisperService for better separation of concerns.
    /// SECURITY FIX (MODEL-GATE-001): Added Pro license validation to prevent freemium bypass
    /// </summary>
    public class ModelResolverService : IModelResolverService
    {
        private readonly string _baseDir;
        private readonly IProFeatureService? _proFeatureService;
        private static bool _integrityWarningLogged = false;

        // Expected SHA256 hash of the official whisper.exe binary (whisper.cpp v1.7.6)
        private const string EXPECTED_WHISPER_HASH = "B7C6DC2E999A80BC2D23CD4C76701211F392AE55D5CABDF0D45EB2CA4FAF09AF";

        public ModelResolverService(string baseDirectory, IProFeatureService? proFeatureService = null)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentNullException(nameof(baseDirectory));

            _baseDir = baseDirectory;
            _proFeatureService = proFeatureService;
        }

        /// <summary>
        /// Resolves the full path to whisper.exe executable.
        /// Searches in: baseDir/whisper/, baseDir/
        /// </summary>
        public string ResolveWhisperExePath()
        {
            // Check whisper/ subdirectory first (preferred location)
            var whisperExePath = Path.Combine(_baseDir, "whisper", "whisper.exe");
            if (File.Exists(whisperExePath))
            {
                ValidateWhisperExecutable(whisperExePath);
                return whisperExePath;
            }

            // Check base directory
            whisperExePath = Path.Combine(_baseDir, "whisper.exe");
            if (File.Exists(whisperExePath))
            {
                ValidateWhisperExecutable(whisperExePath);
                return whisperExePath;
            }

            throw new FileNotFoundException(
                "Whisper.exe not found.\n\n" +
                "Please reinstall VoiceLite to restore the whisper executable.\n\n" +
                $"Expected locations:\n" +
                $"- {Path.Combine(_baseDir, "whisper", "whisper.exe")}\n" +
                $"- {Path.Combine(_baseDir, "whisper.exe")}");
        }

        /// <summary>
        /// Validates whisper.exe integrity using SHA256 hash verification.
        /// Fails open (warns but allows execution) to avoid breaking legitimate updates.
        /// </summary>
        public bool ValidateWhisperExecutable(string whisperExePath)
        {
            try
            {
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                using var stream = File.OpenRead(whisperExePath);
                var hash = sha256.ComputeHash(stream);
                var hashString = BitConverter.ToString(hash).Replace("-", "");

                if (!hashString.Equals(EXPECTED_WHISPER_HASH, StringComparison.OrdinalIgnoreCase))
                {
                    // Integrity check failed - log warning only once per session
                    if (!_integrityWarningLogged)
                    {
                        ErrorLogger.LogMessage("WARNING: Whisper.exe integrity check failed. Using anyway (fail-open mode).");
                        _integrityWarningLogged = true;
                    }
                    return true; // Warn but allow execution
                }

                // Only log success on first check
                if (!_integrityWarningLogged)
                {
                    ErrorLogger.LogMessage("Whisper.exe integrity check passed");
                }
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to validate whisper.exe integrity", ex);
                return true; // Fail open - allow execution on validation error
            }
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
            // This prevents freemium bypass where users manually download Pro models
            if (_proFeatureService != null && !_proFeatureService.CanUseModel(modelFile))
            {
                throw new UnauthorizedAccessException(
                    $"Model '{GetModelDisplayName(modelFile)}' requires Pro license.\n\n" +
                    _proFeatureService.GetUpgradeMessage("Advanced AI Models") + "\n\n" +
                    "Free tier includes Swift model (ggml-base.bin) which provides excellent accuracy for most users.\n" +
                    "Upgrade to Pro to unlock:\n" +
                    "- Pro model (90-93% accuracy)\n" +
                    "- Elite model (95-97% accuracy)\n" +
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
                $"Please download it from Settings → AI Models tab, or reinstall VoiceLite.\n\n" +
                $"Expected locations:\n" +
                $"- Bundled: {Path.Combine(_baseDir, "whisper", modelFile)}\n" +
                $"- Downloaded: {localDataPath}");
        }

        /// <summary>
        /// Gets user-friendly display name for model file
        /// </summary>
        private string GetModelDisplayName(string modelFile)
        {
            return modelFile.ToLower() switch
            {
                "ggml-tiny.bin" => "Lite",
                "ggml-base.bin" => "Swift",
                "ggml-small.bin" => "Pro",
                "ggml-medium.bin" => "Elite",
                "ggml-large-v3.bin" => "Ultra",
                _ => modelFile
            };
        }

        /// <summary>
        /// Normalizes a model name to its full filename.
        /// Supports short names (tiny, base, small, medium, large) and full filenames.
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

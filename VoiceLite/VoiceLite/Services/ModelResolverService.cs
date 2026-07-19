using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    /// <summary>
    /// Resolves the directory containing Parakeet TDT ONNX model files.
    /// Pro-tier model gating was removed when the model lineup collapsed to one.
    /// </summary>
    public class ModelResolverService
    {
        private readonly string _baseDir;
        private readonly ProFeatureService? _proFeatureService;

        // Canonical model id used by Settings.TranscriptionModel after migration.
        public const string ParakeetModelId = "parakeet-tdt-0.6b-v3-int8";
        public const string ParakeetDirName = "parakeet-v3";

        public ModelResolverService(string baseDirectory, ProFeatureService? proFeatureService = null)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentNullException(nameof(baseDirectory));

            _baseDir = baseDirectory;
            _proFeatureService = proFeatureService;
        }

        /// <summary>
        /// Returns the directory containing encoder.int8.onnx, decoder.int8.onnx,
        /// joiner.int8.onnx, and tokens.txt. The single-model lineup means the
        /// resolver always probes the same locations.
        /// </summary>
        public string ResolveModelPath()
        {
            foreach (var dir in CandidateDirectories())
            {
                if (Directory.Exists(dir) && HasRequiredFiles(dir))
                    return dir;
            }

            throw new FileNotFoundException(
                "Parakeet model not found.\n\n" +
                "Expected one of:\n  " +
                string.Join("\n  ", CandidateDirectories()) + "\n\n" +
                "Please download the model from Settings → AI Models, or reinstall VoiceLite.");
        }

        /// <summary>
        /// Returns paths to all installed model directories (single-entry list when present).
        /// </summary>
        public IEnumerable<string> GetAvailableModelPaths() =>
            CandidateDirectories().Where(d => Directory.Exists(d) && HasRequiredFiles(d));

        private IEnumerable<string> CandidateDirectories()
        {
            yield return Path.Combine(_baseDir, "models", ParakeetDirName);
            yield return Path.Combine(_baseDir, "whisper", ParakeetDirName);
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoiceLite",
                "models",
                ParakeetDirName);
        }

        // Length > 0 matters: a truncated/interrupted install can leave 0-byte files
        // that pass a bare File.Exists check, wedging the recognizer on every launch
        // with no recovery path (the first-launch download gate never re-opens).
        private static bool HasRequiredFiles(string dir) =>
            IsNonEmptyFile(Path.Combine(dir, "encoder.int8.onnx"))
            && IsNonEmptyFile(Path.Combine(dir, "decoder.int8.onnx"))
            && IsNonEmptyFile(Path.Combine(dir, "joiner.int8.onnx"))
            && IsNonEmptyFile(Path.Combine(dir, "tokens.txt"));

        private static bool IsNonEmptyFile(string path)
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length > 0;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VoiceLite.Core.Interfaces.Features;
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
        private readonly IProFeatureService? _proFeatureService;

        // Canonical model id used by Settings.WhisperModel after migration.
        public const string ParakeetModelId = "parakeet-tdt-0.6b-v3-int8";
        public const string ParakeetDirName = "parakeet-v3";

        public ModelResolverService(string baseDirectory, IProFeatureService? proFeatureService = null)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentNullException(nameof(baseDirectory));

            _baseDir = baseDirectory;
            _proFeatureService = proFeatureService;
        }

        /// <summary>
        /// Returns the directory containing encoder.int8.onnx, decoder.int8.onnx,
        /// joiner.int8.onnx, and tokens.txt. The <paramref name="modelName"/> argument
        /// is preserved for call-site compatibility but is no longer load-bearing —
        /// the single-model lineup means the resolver always probes the same locations.
        /// </summary>
        public string ResolveModelPath(string modelName)
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
        /// Maps any input (including legacy GGML filenames) to the canonical Parakeet id.
        /// </summary>
        public string NormalizeModelName(string modelName) => ParakeetModelId;

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

        private static bool HasRequiredFiles(string dir) =>
            File.Exists(Path.Combine(dir, "encoder.int8.onnx"))
            && File.Exists(Path.Combine(dir, "decoder.int8.onnx"))
            && File.Exists(Path.Combine(dir, "joiner.int8.onnx"))
            && File.Exists(Path.Combine(dir, "tokens.txt"));
    }
}

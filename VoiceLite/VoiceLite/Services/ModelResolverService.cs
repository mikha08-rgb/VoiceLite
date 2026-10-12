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
        private readonly string _localAppDataDir;

        // Canonical model id used by Settings.TranscriptionModel after migration.
        public const string ParakeetModelId = "parakeet-tdt-0.6b-v3-int8";
        public const string ParakeetDirName = "parakeet-v3";

        /// <param name="localAppDataDirectory">Tests only: stands in for %LOCALAPPDATA% (same seam as TranslationModelResolverService).</param>
        public ModelResolverService(string baseDirectory, ProFeatureService? proFeatureService = null, string? localAppDataDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentNullException(nameof(baseDirectory));

            _baseDir = baseDirectory;
            _proFeatureService = proFeatureService;
            _localAppDataDir = localAppDataDirectory ??
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
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
                // Settings cannot re-download Parakeet; only the startup gate in App.xaml.cs can, and
                // closing the window just hides it to the tray, so a real exit is needed first.
                "Exit VoiceLite from the tray icon (right-click > Exit) and start it again to download the model, or reinstall VoiceLite.");
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
                _localAppDataDir,
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VoiceLite.Services
{
    /// <summary>
    /// Resolves the optional local Canary speech-translation model. Unlike Parakeet,
    /// this model is not required at startup and is downloaded only when a user opts in.
    /// </summary>
    public class TranslationModelResolverService
    {
        public const string ModelId = "canary-180m-flash-en-es-de-fr-int8";
        public const string ModelFolderName = "canary-translation";

        public static readonly string[] RequiredModelFiles =
        {
            "encoder.int8.onnx",
            "decoder.int8.onnx",
            "tokens.txt"
        };

        private readonly IReadOnlyList<string> modelPaths;

        public TranslationModelResolverService(
            string? baseDirectory = null,
            string? localAppDataDirectory = null)
        {
            var baseDir = baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
            var localAppData = localAppDataDirectory ??
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            modelPaths = new[]
            {
                Path.Combine(baseDir, "models", ModelFolderName),
                Path.Combine(baseDir, "translation", ModelFolderName),
                Path.Combine(localAppData, "VoiceLite", "models", ModelFolderName)
            };
        }

        public static string DefaultModelDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VoiceLite", "models", ModelFolderName);

        public IEnumerable<string> GetAvailableModelPaths() => modelPaths;

        public string ResolveModelPath()
        {
            var modelPath = modelPaths.FirstOrDefault(HasRequiredFiles);
            if (modelPath != null)
                return modelPath;

            throw new FileNotFoundException(
                "The English translation model is not installed. " +
                "Open Settings > General > Translate to English and click Install translation model.");
        }

        public bool IsModelInstalled() => modelPaths.Any(HasRequiredFiles);

        public static bool HasRequiredFiles(string directory)
        {
            if (!Directory.Exists(directory))
                return false;

            return RequiredModelFiles.All(fileName =>
            {
                var file = new FileInfo(Path.Combine(directory, fileName));
                return file.Exists && file.Length > 0;
            });
        }
    }
}

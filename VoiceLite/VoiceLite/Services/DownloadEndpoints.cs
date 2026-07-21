namespace VoiceLite.Services
{
    /// <summary>
    /// Centralized download URLs for VoiceLite's Sherpa-ONNX model bundles.
    /// Hosting changes or integrity audits only need to touch this file.
    /// </summary>
    public static class DownloadEndpoints
    {
        private static readonly string[] ParakeetRequiredFiles =
        {
            "encoder.int8.onnx",
            "decoder.int8.onnx",
            "joiner.int8.onnx",
            "tokens.txt"
        };

        /// <summary>
        /// Upstream URL hosted by k2-fsa on GitHub Releases. Roughly 640MB tarball
        /// containing encoder.int8.onnx, decoder.int8.onnx, joiner.int8.onnx, tokens.txt.
        /// </summary>
        /// <remarks>
        /// TODO(launch-blocker): mirror this tarball on VoiceLite-controlled hosting
        /// (e.g. our own GitHub Releases) and point this constant at the mirror.
        /// RISK: every new customer's first-launch onboarding downloads from this
        /// k2-fsa GitHub Releases asset — a third party we don't control. If they
        /// rename/delete the asset, retag the release, or GitHub throttles the asset
        /// under our launch-day traffic, onboarding breaks for 100% of new installs
        /// (the app ships without the model and cannot transcribe until this URL works).
        /// Mirroring also insulates k2-fsa from our bandwidth.
        /// </remarks>
        public const string ParakeetV3Int8 =
            "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8.tar.bz2";

        /// <summary>
        /// Optional local speech-translation model. The int8 bundle is about 154MB
        /// and translates Spanish, French, or German speech directly to English.
        /// </summary>
        /// <remarks>
        /// TODO: mirror this bundle on VoiceLite-controlled hosting for the same
        /// availability reason documented on <see cref="ParakeetV3Int8"/>.
        /// </remarks>
        public const string CanaryTranslationInt8 =
            "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemo-canary-180m-flash-en-es-de-fr-int8.tar.bz2";

        // Exact GitHub release-asset metadata. If upstream intentionally replaces an
        // archive, review the new contents first, then update size and hash together.
        internal const long ParakeetV3Int8ArchiveSize = 487_170_055;
        internal const string ParakeetV3Int8Sha256 =
            "5793d0fd397c5778d2cf2126994d58e9d56b1be7c04d13c7a15bb1b4eafb16bf";
        internal const long CanaryTranslationInt8ArchiveSize = 153_692_328;
        internal const string CanaryTranslationInt8Sha256 =
            "7a38ed8b13f014ad632b09ff8d22e0c6f1359dd046af9235d281dfae841b9ab9";

        /// <summary>
        /// Resolves a canonical model id to its download URL, or null if unsupported.
        /// </summary>
        public static string? GetUrlForFileName(string fileName) => fileName switch
        {
            ModelResolverService.ParakeetModelId => ParakeetV3Int8,
            TranslationModelResolverService.ModelId => CanaryTranslationInt8,
            _ => null,
        };

        /// <summary>
        /// Selects the complete integrity/install manifest for a supported model.
        /// Disk allowances intentionally retain the existing conservative preflight
        /// margins rather than using the smaller exact archive sizes.
        /// </summary>
        internal static ModelManifest GetManifestForFileName(
            string fileName,
            string modelDirectory) => fileName switch
        {
            ModelResolverService.ParakeetModelId => new ModelManifest(
                ModelResolverService.ParakeetModelId,
                new System.Uri(ParakeetV3Int8),
                modelDirectory,
                ParakeetRequiredFiles,
                "parakeet",
                ParakeetV3Int8ArchiveSize,
                ParakeetV3Int8Sha256,
                ArchiveDiskSpaceBytes: 700_000_000,
                ExtractedDiskSpaceBytes: 800_000_000),

            TranslationModelResolverService.ModelId => new ModelManifest(
                TranslationModelResolverService.ModelId,
                new System.Uri(CanaryTranslationInt8),
                modelDirectory,
                TranslationModelResolverService.RequiredModelFiles,
                "canary-translation",
                CanaryTranslationInt8ArchiveSize,
                CanaryTranslationInt8Sha256,
                ArchiveDiskSpaceBytes: 200_000_000,
                ExtractedDiskSpaceBytes: 250_000_000),

            _ => throw new System.ArgumentOutOfRangeException(
                nameof(fileName), fileName, "Unsupported model id")
        };
    }
}

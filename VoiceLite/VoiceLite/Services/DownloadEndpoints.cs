namespace VoiceLite.Services
{
    /// <summary>
    /// Centralized download URLs for VoiceLite's Sherpa-ONNX model bundles.
    /// Hosting changes or integrity audits only need to touch this file.
    /// </summary>
    public static class DownloadEndpoints
    {
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

        /// <summary>
        /// Resolves a canonical model id to its download URL, or null if unsupported.
        /// </summary>
        public static string? GetUrlForFileName(string fileName) => fileName switch
        {
            "parakeet-tdt-0.6b-v3-int8" => ParakeetV3Int8,
            TranslationModelResolverService.ModelId => CanaryTranslationInt8,
            _ => null,
        };
    }
}

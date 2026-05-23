namespace VoiceLite.Services
{
    /// <summary>
    /// Centralized download URL for the Parakeet TDT v3 model bundle (Sherpa-ONNX format).
    /// Hosting changes or integrity audits only need to touch this file.
    /// </summary>
    public static class DownloadEndpoints
    {
        /// <summary>
        /// Upstream URL hosted by k2-fsa on GitHub Releases. Roughly 640MB tarball
        /// containing encoder.int8.onnx, decoder.int8.onnx, joiner.int8.onnx, tokens.txt.
        /// </summary>
        /// <remarks>
        /// Mirror this on the VoiceLite GitHub Releases before a public launch to avoid
        /// k2-fsa being hit with our launch-day traffic.
        /// </remarks>
        public const string ParakeetV3Int8 =
            "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8.tar.bz2";

        /// <summary>
        /// Resolves a canonical model id to its download URL, or null if unsupported.
        /// </summary>
        public static string? GetUrlForFileName(string fileName) => fileName switch
        {
            "parakeet-tdt-0.6b-v3-int8" => ParakeetV3Int8,
            _ => null,
        };
    }
}

namespace VoiceLite.Services
{
    /// <summary>
    /// Centralized download URLs for Whisper models.
    /// All external endpoints used by the app for model downloads live here so
    /// hosting changes or integrity audits only need to touch one file.
    /// </summary>
    public static class DownloadEndpoints
    {
        private const string HuggingFaceBase = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main";

        // The full ggml-medium.bin (1.5GB) is hosted on our GitHub release because
        // HuggingFace serves the F16 medium-only as a different filename.
        private const string GitHubMediumBase = "https://github.com/mikha08-rgb/VoiceLite/releases/download/v1.0.0";

        public const string TinyQ8 = $"{HuggingFaceBase}/ggml-tiny-q8_0.bin";
        public const string SmallQ8 = $"{HuggingFaceBase}/ggml-small-q8_0.bin";
        public const string MediumQ8 = $"{HuggingFaceBase}/ggml-medium-q8_0.bin";
        public const string LargeV3 = $"{HuggingFaceBase}/ggml-large-v3.bin";
        public const string MediumFull = $"{GitHubMediumBase}/ggml-medium.bin";

        /// <summary>
        /// Resolves a model file name to its download URL, or null if unsupported.
        /// </summary>
        public static string? GetUrlForFileName(string fileName) => fileName switch
        {
            "ggml-tiny-q8_0.bin" => TinyQ8,
            "ggml-small-q8_0.bin" => SmallQ8,
            "ggml-medium-q8_0.bin" => MediumQ8,
            "ggml-medium.bin" => MediumFull,
            "ggml-large-v3.bin" => LargeV3,
            _ => null
        };
    }
}

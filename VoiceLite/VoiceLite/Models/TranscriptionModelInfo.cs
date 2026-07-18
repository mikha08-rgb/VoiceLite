using System.Collections.Generic;

namespace VoiceLite.Models
{
    // Exists only to hold the legacy GGML filenames that pre-v2.0 versions persisted in
    // settings.json; SettingsMigration uses them to detect upgrade paths. The old
    // multi-model registry (ratings, install probes, recommendations) was deleted 2026-07-17.
    public static class TranscriptionModelInfo
    {
        public static readonly IReadOnlyList<string> LegacyGgmlFileNames = new[]
        {
            "ggml-tiny.bin",
            "ggml-base.bin",
            "ggml-small.bin",
            "ggml-medium.bin",
            "ggml-large-v3-turbo-q8_0.bin",
            "ggml-large-v3.bin",
        };
    }
}

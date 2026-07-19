using VoiceLite.Services;

namespace VoiceLite.Tests.TestUtilities
{
    /// <summary>
    /// Detects audio capabilities of the machine running the tests.
    /// GitHub Actions Windows runners have no audio capture devices, so any test that
    /// calls AudioRecorder.StartRecording() must early-return when no microphone exists
    /// (StartRecording throws InvalidOperationException("No microphone detected...")).
    /// xUnit SkippableFact is not available in this repo — use the established pattern:
    ///     if (!AudioTestEnvironment.HasMicrophone) return; // no audio device (CI runner)
    /// </summary>
    public static class AudioTestEnvironment
    {
        /// <summary>
        /// True when at least one audio capture device is present. Uses the exact same
        /// predicate AudioRecorder.StartRecording() checks before throwing.
        /// </summary>
        public static bool HasMicrophone => AudioRecorder.HasAnyMicrophone();
    }
}

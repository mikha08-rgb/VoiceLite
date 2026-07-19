using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// Deterministic tests for Parakeet model-path resolution. The old
    /// ModelResolverServiceTests was deleted in the 2026-07-17 cleanup as a Phase-E
    /// zombie (it asserted against the 5-model GGML world). These tests use real temp
    /// directories under the test's control, so they run identically on dev machines
    /// and bare CI — except the LOCALAPPDATA-fallback test, which needs the installed
    /// model and gates on its presence.
    ///
    /// NOTE: resolution checks file EXISTENCE only (zero-byte model files still
    /// resolve) — content validation happens later, at recognizer load.
    /// </summary>
    public class ModelResolverServiceTests : IDisposable
    {
        private static readonly string[] RequiredFiles =
            { "encoder.int8.onnx", "decoder.int8.onnx", "joiner.int8.onnx", "tokens.txt" };

        private readonly string _tempBase;

        public ModelResolverServiceTests()
        {
            _tempBase = Path.Combine(Path.GetTempPath(), $"vl-resolver-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempBase);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempBase, true); } catch { }
        }

        private string CreateModelDir(string parent, params string[] files)
        {
            var dir = Path.Combine(_tempBase, parent, ModelResolverService.ParakeetDirName);
            Directory.CreateDirectory(dir);
            foreach (var f in files)
            {
                // Non-empty: the resolver treats zero-byte files as a corrupt install.
                File.WriteAllBytes(Path.Combine(dir, f), new byte[] { 0x01 });
            }
            return dir;
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_NullOrWhitespaceBaseDir_Throws(string? baseDir)
        {
            var act = () => new ModelResolverService(baseDir!);

            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void ResolveModelPath_ReturnsModelsDir_WhenAllFourFilesPresent()
        {
            var modelsDir = CreateModelDir("models", RequiredFiles);
            var resolver = new ModelResolverService(_tempBase);

            resolver.ResolveModelPath().Should().Be(modelsDir);
        }

        [Fact]
        public void ResolveModelPath_FallsBackToLegacyWhisperDir_WhenModelsDirAbsent()
        {
            // Installs upgraded from the Whisper era keep models under "whisper\".
            var whisperDir = CreateModelDir("whisper", RequiredFiles);
            var resolver = new ModelResolverService(_tempBase);

            resolver.ResolveModelPath().Should().Be(whisperDir);
        }

        [Fact]
        public void ResolveModelPath_PrefersModelsDir_OverLegacyWhisperDir()
        {
            var modelsDir = CreateModelDir("models", RequiredFiles);
            CreateModelDir("whisper", RequiredFiles);
            var resolver = new ModelResolverService(_tempBase);

            resolver.ResolveModelPath().Should().Be(modelsDir);
        }

        [Fact]
        public void ResolveModelPath_SkipsIncompleteDir_AndResolvesTheNextCandidate()
        {
            // models\parakeet-v3 exists but is missing tokens.txt → must be skipped,
            // not returned (an incomplete install must never win over a complete one).
            CreateModelDir("models", "encoder.int8.onnx", "decoder.int8.onnx", "joiner.int8.onnx");
            var whisperDir = CreateModelDir("whisper", RequiredFiles);
            var resolver = new ModelResolverService(_tempBase);

            resolver.ResolveModelPath().Should().Be(whisperDir);
        }

        [Fact]
        public void GetAvailableModelPaths_ExcludesDirsMissingRequiredFiles()
        {
            var incompleteDir = CreateModelDir("models", "encoder.int8.onnx", "tokens.txt");
            var resolver = new ModelResolverService(_tempBase);

            resolver.GetAvailableModelPaths().Should().NotContain(incompleteDir);
        }

        [Fact]
        public void ResolveModelPath_FallsBackToLocalAppData_WhenBaseDirHasNoModel()
        {
            if (!TranscriptionServiceFixture.ModelPresent) return; // needs the installed model (e.g. absent on bare CI)

            // _tempBase is empty → the production install fallback
            // (%LOCALAPPDATA%\VoiceLite\models\parakeet-v3) must resolve.
            var resolver = new ModelResolverService(_tempBase);

            resolver.ResolveModelPath().Should().Be(TranscriptionServiceFixture.ModelDir);
        }
    }
}

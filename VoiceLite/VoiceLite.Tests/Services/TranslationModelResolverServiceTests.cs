using System;
using System.IO;
using AwesomeAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    public sealed class TranslationModelResolverServiceTests : IDisposable
    {
        private readonly string testRoot = Path.Combine(
            Path.GetTempPath(),
            $"VoiceLite-TranslationResolver-{Guid.NewGuid():N}");

        [Fact]
        public void ResolveModelPath_WhenOptionalModelIsMissing_ThrowsActionableError()
        {
            var resolver = new TranslationModelResolverService(
                Path.Combine(testRoot, "app"),
                Path.Combine(testRoot, "local"));

            var act = () => resolver.ResolveModelPath();

            act.Should().Throw<FileNotFoundException>()
                .WithMessage("*Install translation model*");
        }

        [Fact]
        public void ResolveModelPath_WhenAllFilesArePresent_ReturnsModelDirectory()
        {
            var localAppData = Path.Combine(testRoot, "local");
            var modelDir = Path.Combine(
                localAppData,
                "VoiceLite",
                "models",
                TranslationModelResolverService.ModelFolderName);
            WriteRequiredFiles(modelDir);

            var resolver = new TranslationModelResolverService(
                Path.Combine(testRoot, "app"),
                localAppData);

            resolver.ResolveModelPath().Should().Be(modelDir);
            resolver.IsModelInstalled().Should().BeTrue();
        }

        [Fact]
        public void HasRequiredFiles_RejectsZeroByteModelFile()
        {
            var modelDir = Path.Combine(testRoot, "model");
            WriteRequiredFiles(modelDir);
            File.WriteAllBytes(Path.Combine(modelDir, "decoder.int8.onnx"), Array.Empty<byte>());

            TranslationModelResolverService.HasRequiredFiles(modelDir).Should().BeFalse();
        }

        private static void WriteRequiredFiles(string modelDir)
        {
            Directory.CreateDirectory(modelDir);
            foreach (var fileName in TranslationModelResolverService.RequiredModelFiles)
            {
                File.WriteAllText(Path.Combine(modelDir, fileName), "test");
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }
    }
}

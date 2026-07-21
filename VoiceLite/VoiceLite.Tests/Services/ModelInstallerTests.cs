using System;
using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    public sealed class ModelInstallerTests : IDisposable
    {
        private readonly string tempRoot = Path.Combine(
            Path.GetTempPath(),
            $"VoiceLite-ModelInstaller-{Guid.NewGuid():N}");

        public ModelInstallerTests()
        {
            Directory.CreateDirectory(tempRoot);
        }

        [Fact]
        public async Task VerifyArchiveAsync_RejectsSizeMismatch()
        {
            var archivePath = Path.Combine(tempRoot, "wrong-size.tar.bz2");
            await File.WriteAllBytesAsync(archivePath, new byte[] { 1, 2, 3 });
            var manifest = CreateManifest(
                archiveSizeBytes: 4,
                archiveSha256: new string('0', 64));

            Func<Task> act = () => ModelInstaller.VerifyArchiveAsync(
                archivePath,
                manifest);

            await act.Should().ThrowAsync<InvalidDataException>()
                .WithMessage("*size mismatch*");
        }

        [Fact]
        public async Task VerifyArchiveAsync_RejectsChecksumMismatch()
        {
            var archivePath = Path.Combine(tempRoot, "wrong-checksum.tar.bz2");
            await File.WriteAllBytesAsync(archivePath, new byte[] { 1, 2, 3, 4 });
            var manifest = CreateManifest(
                archiveSizeBytes: 4,
                archiveSha256: new string('0', 64));

            Func<Task> act = () => ModelInstaller.VerifyArchiveAsync(
                archivePath,
                manifest);

            await act.Should().ThrowAsync<InvalidDataException>()
                .WithMessage("*checksum mismatch*");
        }

        [Fact]
        public void RecoverInterruptedInstall_CrashBetweenRenames_RestoresWorkingBackup()
        {
            var manifest = CreateManifest();
            var backupDirectory = manifest.ModelDirectory + ".backup";
            var tempDirectory = manifest.ModelDirectory + ".tmp-interrupted";

            WriteRequiredModel(backupDirectory, "known-working-model");
            WriteRequiredModel(tempDirectory, "new-model-awaiting-second-rename");

            var result = ModelInstaller.RecoverInterruptedInstall(manifest);

            result.Should().Be(ModelRecoveryResult.RestoredBackup);
            ModelInstaller.HasRequiredModelFiles(manifest).Should().BeTrue();
            File.ReadAllText(Path.Combine(manifest.ModelDirectory, "model.bin"))
                .Should().Be("known-working-model");
            Directory.Exists(backupDirectory).Should().BeFalse();
            Directory.Exists(tempDirectory).Should().BeFalse();
        }

        [Fact]
        public void DownloadManifests_PinExactArchiveIntegrityMetadata()
        {
            var parakeet = DownloadEndpoints.GetManifestForFileName(
                ModelResolverService.ParakeetModelId,
                Path.Combine(tempRoot, "parakeet"));
            var canary = DownloadEndpoints.GetManifestForFileName(
                TranslationModelResolverService.ModelId,
                Path.Combine(tempRoot, "canary"));

            parakeet.ArchiveSizeBytes.Should().Be(487_170_055);
            parakeet.ArchiveSha256.Should().Be(
                "5793d0fd397c5778d2cf2126994d58e9d56b1be7c04d13c7a15bb1b4eafb16bf");
            canary.ArchiveSizeBytes.Should().Be(153_692_328);
            canary.ArchiveSha256.Should().Be(
                "7a38ed8b13f014ad632b09ff8d22e0c6f1359dd046af9235d281dfae841b9ab9");
        }

        private ModelManifest CreateManifest(
            long archiveSizeBytes = 4,
            string? archiveSha256 = null) => new ModelManifest(
                "test-model",
                new Uri("https://example.test/model.tar.bz2"),
                Path.Combine(tempRoot, "model"),
                new[] { "model.bin" },
                "test-model",
                archiveSizeBytes,
                archiveSha256 ?? new string('0', 64),
                ArchiveDiskSpaceBytes: archiveSizeBytes,
                ExtractedDiskSpaceBytes: 1);

        private static void WriteRequiredModel(string directory, string contents)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "model.bin"), contents);
        }

        public void Dispose()
        {
            try { Directory.Delete(tempRoot, recursive: true); }
            catch { }
        }
    }
}

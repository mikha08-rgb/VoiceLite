using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AwesomeAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// SHA-256 verification and the refuse-without-hash gate. No network: DownloadAsync is
    /// only invoked on the path that must throw BEFORE any HTTP call.
    /// </summary>
    public sealed class UpdateInstallerTests : IDisposable
    {
        private readonly string tempRoot = Path.Combine(
            Path.GetTempPath(),
            $"VoiceLite-UpdateInstaller-{Guid.NewGuid():N}");

        public UpdateInstallerTests()
        {
            Directory.CreateDirectory(tempRoot);
        }

        private static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

        private static UpdateInfo Info(string? sha256, string? assetName = "VoiceLite-Setup-9.9.9.exe") => new()
        {
            Version = "9.9.9",
            DownloadUrl = "https://127.0.0.1:1/never-contacted.exe",
            ReleasePageUrl = "https://127.0.0.1:1/release",
            AssetName = assetName,
            Sha256 = sha256,
            Sha256Source = sha256 == null ? null : "digest",
        };

        [Fact]
        public async Task VerifyOrDeleteAsync_AcceptsMatchingHash_AndKeepsFile()
        {
            var bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            var path = Path.Combine(tempRoot, "good.exe");
            await File.WriteAllBytesAsync(path, bytes);

            await UpdateInstaller.VerifyOrDeleteAsync(path, Sha256Hex(bytes));

            File.Exists(path).Should().BeTrue();
        }

        [Fact]
        public async Task VerifyOrDeleteAsync_IsCaseInsensitive()
        {
            var bytes = new byte[] { 9, 8, 7 };
            var path = Path.Combine(tempRoot, "lower.exe");
            await File.WriteAllBytesAsync(path, bytes);

            await UpdateInstaller.VerifyOrDeleteAsync(path, Sha256Hex(bytes).ToLowerInvariant());

            File.Exists(path).Should().BeTrue();
        }

        [Fact]
        public async Task VerifyOrDeleteAsync_RejectsMismatch_AndDeletesFile()
        {
            var path = Path.Combine(tempRoot, "bad.exe");
            await File.WriteAllBytesAsync(path, new byte[] { 1, 2, 3, 4 });

            Func<Task> act = () => UpdateInstaller.VerifyOrDeleteAsync(path, new string('0', 64));

            await act.Should().ThrowAsync<UpdateVerificationException>()
                .WithMessage("*SHA-256*");
            File.Exists(path).Should().BeFalse("a file that fails verification must never be left on disk");
        }

        [Fact]
        public async Task ComputeSha256Async_MatchesFrameworkHash()
        {
            var bytes = new byte[200_000];
            new Random(42).NextBytes(bytes);
            var path = Path.Combine(tempRoot, "big.bin");
            await File.WriteAllBytesAsync(path, bytes);

            var actual = await UpdateInstaller.ComputeSha256Async(path);

            actual.Should().Be(Sha256Hex(bytes));
        }

        [Fact]
        public async Task DownloadAsync_RefusesWhenNoHash_WithoutTouchingNetworkOrDisk()
        {
            var dir = Path.Combine(tempRoot, "updates-nohash");

            Func<Task> act = () => UpdateInstaller.DownloadAsync(Info(sha256: null), progress: null, default, dir);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*SHA-256*");
            Directory.Exists(dir).Should().BeFalse("refusal must happen before any directory is created");
        }

        [Fact]
        public async Task DownloadAsync_RefusesWhenNoInstallerAsset()
        {
            var dir = Path.Combine(tempRoot, "updates-noasset");

            Func<Task> act = () => UpdateInstaller.DownloadAsync(Info(new string('A', 64), assetName: null), null, default, dir);

            await act.Should().ThrowAsync<InvalidOperationException>();
            Directory.Exists(dir).Should().BeFalse();
        }

        [Fact]
        public async Task DownloadAsync_ReusesAlreadyVerifiedInstaller_AndSweepsStaleFiles()
        {
            // A verified installer for this version already sits in the updates dir
            // (e.g. the user closed the dialog right before install). Must be reused
            // without any network call, and older versions' files must be deleted.
            var dir = Path.Combine(tempRoot, "updates-reuse");
            Directory.CreateDirectory(dir);
            var bytes = new byte[] { 42, 42, 42 };
            var target = UpdateInstaller.GetInstallerPath("9.9.9", dir);
            await File.WriteAllBytesAsync(target, bytes);
            var stale = Path.Combine(dir, "VoiceLite-Setup-9.9.8.exe");
            await File.WriteAllBytesAsync(stale, new byte[] { 1 });
            var partial = target + ".partial";
            await File.WriteAllBytesAsync(partial, new byte[] { 2 });

            double lastProgress = -1;
            var progress = new SynchronousProgress(p => lastProgress = p);

            var result = await UpdateInstaller.DownloadAsync(Info(Sha256Hex(bytes)), progress, default, dir);

            result.Should().Be(target);
            File.Exists(target).Should().BeTrue();
            File.Exists(stale).Should().BeFalse("installers for other versions are swept");
            File.Exists(partial).Should().BeFalse("leftover partial downloads are swept");
            lastProgress.Should().Be(1.0);
        }

        [Fact]
        public void GetInstallerPath_UsesVersionedNameUnderUpdatesDir()
        {
            var path = UpdateInstaller.GetInstallerPath("2.5.0", @"C:\x\updates");
            path.Should().Be(@"C:\x\updates\VoiceLite-Setup-2.5.0.exe");

            UpdateInstaller.GetUpdatesDirectory().Should().EndWith(Path.Combine("VoiceLite", "updates"));
        }

        [Fact]
        public void InstallerArguments_AreSilentAndRequestRelaunch()
        {
            // The .iss [Run] entry keys off /RELAUNCH=1 and the postinstall launch is
            // skipifsilent — both halves must stay in sync or a silent upgrade leaves
            // the user with no running VoiceLite.
            UpdateInstaller.InstallerArguments.Should().Contain("/VERYSILENT");
            UpdateInstaller.InstallerArguments.Should().Contain("/NORESTART");
            UpdateInstaller.InstallerArguments.Should().Contain("/SUPPRESSMSGBOXES");
            UpdateInstaller.InstallerArguments.Should().Contain("/RELAUNCH=1");
        }

        public void Dispose()
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { /* best effort */ }
        }

        // Progress<T> posts to a SynchronizationContext; tests have none, so use a
        // synchronous IProgress to observe the final report deterministically.
        private sealed class SynchronousProgress : IProgress<double>
        {
            private readonly Action<double> onReport;
            public SynchronousProgress(Action<double> onReport) => this.onReport = onReport;
            public void Report(double value) => onReport(value);
        }
    }
}

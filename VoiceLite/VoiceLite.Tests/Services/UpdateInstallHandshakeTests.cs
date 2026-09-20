using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AwesomeAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// F4: VoiceLite must not shut down just because Process.Start returned. It waits for the
    /// installer's /READYFILE (written once elevation succeeded and [Code] is running) and
    /// stays alive - reporting the exit code - when the installer dies first.
    /// No real process is ever started: the launcher is a fake that returns an exit-code probe.
    /// </summary>
    public sealed class UpdateInstallHandshakeTests : IDisposable
    {
        private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(10);
        private static readonly TimeSpan Guard = TimeSpan.FromSeconds(10);

        private readonly string dir = Path.Combine(
            Path.GetTempPath(),
            $"VoiceLite-UpdateHandshake-{Guid.NewGuid():N}");
        private readonly string installer;
        private readonly string logPath;

        public UpdateInstallHandshakeTests()
        {
            Directory.CreateDirectory(dir);
            installer = Path.Combine(dir, "VoiceLite-Setup-9.9.9.exe");
            File.WriteAllBytes(installer, new byte[] { 1 });
            logPath = Path.Combine(dir, "logs", "update-setup.log");
        }

        public void Dispose()
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }

        private static string ReadyFileFrom(ProcessStartInfo psi)
        {
            var m = Regex.Match(psi.Arguments, "/READYFILE=\"([^\"]+)\"");
            m.Success.Should().BeTrue($"the installer contract is /READYFILE=\"<absolute path>\" (got: {psi.Arguments})");
            return m.Groups[1].Value;
        }

        private static async Task<Exception?> RunGuarded(Task install)
        {
            var finished = await Task.WhenAny(install, Task.Delay(Guard));
            finished.Should().BeSameAs(install, "the handshake must finish once the fake installer exits or signals ready");
            try { await install; return null; }
            catch (Exception ex) { return ex; }
        }

        [Fact]
        public async Task InstallerExitsBeforeReady_DoesNotShutDown_AndReportsExitCode()
        {
            var shutdowns = 0;

            var error = await RunGuarded(UpdateInstaller.InstallAsync(
                installer, _ => () => 5, () => shutdowns++, Poll, logPath));

            shutdowns.Should().Be(0, "a declined UAC prompt / dead installer must leave VoiceLite running");
            error.Should().BeOfType<UpdateInstallFailedException>()
                .Which.ExitCode.Should().Be(5);
        }

        [Fact]
        public async Task ReadyFileAppears_WhileInstallerAlive_ShutsDownExactlyOnce()
        {
            var shutdowns = 0;
            var polls = 0;
            string? readyFile = null;

            var error = await RunGuarded(UpdateInstaller.InstallAsync(
                installer,
                psi =>
                {
                    readyFile = ReadyFileFrom(psi);
                    return () =>
                    {
                        // Alive throughout (exit code stays null). "UAC accepted" after a few polls.
                        if (++polls == 5) File.WriteAllText(readyFile, "ready");
                        return null;
                    };
                },
                () => shutdowns++, Poll, logPath));

            error.Should().BeNull();
            shutdowns.Should().Be(1);
            polls.Should().BeGreaterThanOrEqualTo(5, "there is no timeout while the installer is alive (UAC can sit for minutes)");
        }

        [Fact]
        public async Task StaleReadyFile_IsDeletedBeforeLaunch_AndNotTreatedAsReady()
        {
            var stale = Path.Combine(dir, "install-ready-0123456789abcdef.flag");
            File.WriteAllText(stale, "left over from a previous attempt");
            var shutdowns = 0;
            var staleExistedAtLaunch = true;

            var error = await RunGuarded(UpdateInstaller.InstallAsync(
                installer,
                psi =>
                {
                    staleExistedAtLaunch = File.Exists(stale);
                    ReadyFileFrom(psi).Should().NotBe(stale, "every attempt uses a fresh ready-file name");
                    return () => 1;
                },
                () => shutdowns++, Poll, logPath));

            staleExistedAtLaunch.Should().BeFalse();
            shutdowns.Should().Be(0);
            error.Should().BeOfType<UpdateInstallFailedException>();
        }

        [Fact]
        public async Task Arguments_KeepSilentFlags_AndAddReadyFileAndLog()
        {
            ProcessStartInfo? first = null, second = null;

            await RunGuarded(UpdateInstaller.InstallAsync(installer, psi => { first = psi; return () => 1; }, () => { }, Poll, logPath));
            await RunGuarded(UpdateInstaller.InstallAsync(installer, psi => { second = psi; return () => 1; }, () => { }, Poll, logPath));

            first.Should().NotBeNull();
            first!.FileName.Should().Be(installer);
            first.Arguments.Should().StartWith(UpdateInstaller.InstallerArguments,
                "an installer that doesn't know the new flags must still get the shipped silent/relaunch flags");
            first.Arguments.Should().Contain($"/LOG=\"{logPath}\"");
            first.Arguments.Should().NotContain("%", "ProcessStartInfo.Arguments does no environment-variable expansion");
            Directory.Exists(Path.GetDirectoryName(logPath)).Should().BeTrue("Inno aborts if it cannot create the /LOG file");

            var ready = ReadyFileFrom(first);
            Path.IsPathFullyQualified(ready).Should().BeTrue();
            Path.GetDirectoryName(ready).Should().Be(dir, "the ready file lives in the updates directory next to the installer");
            ReadyFileFrom(second!).Should().NotBe(ready, "unique name per attempt");
        }

        [Fact]
        public void SetupLogPath_IsUnderLocalAppDataVoiceLiteLogs()
        {
            var expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoiceLite", "logs", "update-setup.log");

            UpdateInstaller.GetSetupLogPath().Should().Be(expected);
        }

        [Fact]
        public async Task MissingInstaller_ThrowsBeforeLaunching()
        {
            var launched = false;

            var error = await RunGuarded(UpdateInstaller.InstallAsync(
                Path.Combine(dir, "nope.exe"), _ => { launched = true; return () => 0; }, () => { }, Poll, logPath));

            error.Should().BeOfType<FileNotFoundException>();
            launched.Should().BeFalse();
        }

        [Fact]
        public async Task LeftoverReadyFiles_FromThisAttempt_AreNotLeftBehindOnFailure()
        {
            await RunGuarded(UpdateInstaller.InstallAsync(installer, _ => () => 1, () => { }, Poll, logPath));

            Directory.GetFiles(dir, "install-ready-*").Should().BeEmpty();
            Directory.GetFiles(dir).Select(Path.GetFileName).Should().Contain("VoiceLite-Setup-9.9.9.exe",
                "the verified installer is kept for the retry");
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace VoiceLite.Services
{
    /// <summary>
    /// Thrown when a downloaded installer's SHA-256 does not match the hash published with
    /// the release. The file has already been deleted when this is thrown.
    /// </summary>
    public sealed class UpdateVerificationException : Exception
    {
        public UpdateVerificationException(string message) : base(message) { }
    }

    /// <summary>
    /// Thrown when the installer process ended before it signalled that it was ready to
    /// install (UAC declined, no admin rights, antivirus kill, ...). VoiceLite has NOT been
    /// shut down when this is thrown. Exit code 0 means the installer finished without the
    /// handshake - it may have installed, so callers must not claim "nothing changed".
    /// </summary>
    public sealed class UpdateInstallFailedException : Exception
    {
        public int ExitCode { get; }

        public UpdateInstallFailedException(int exitCode)
            : base(exitCode == 0
                ? "The installer finished (exit code 0) without asking VoiceLite to close."
                : $"The installer stopped before it could update VoiceLite (exit code {exitCode}).")
        {
            ExitCode = exitCode;
        }
    }

    /// <summary>
    /// Downloads a release installer to %LOCALAPPDATA%\VoiceLite\updates, verifies it
    /// against the published SHA-256, and hands off to Inno Setup in silent mode.
    /// No DI, no state — MainWindow/UpdateWindow drive it directly.
    /// </summary>
    public static class UpdateInstaller
    {
        /// <summary>
        /// /VERYSILENT: no wizard. /NORESTART: never reboot. /SUPPRESSMSGBOXES: no prompts.
        /// /RELAUNCH=1: custom flag read by VoiceLiteSetup.iss ([Run] Check: RelaunchRequested)
        /// so the silent install starts VoiceLite again when it finishes.
        /// </summary>
        public const string InstallerArguments = "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES /RELAUNCH=1";

        private const int BufferSize = 64 * 1024;

        /// <summary>
        /// Longest the download may go without receiving a single byte before it is failed as
        /// stalled (same value as ModelInstaller). Per read, NOT a cap on the whole download.
        /// </summary>
        internal static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60);

        // Same lazy-init pattern as UpdateCheckService: never construct HttpClient during startup.
        private static HttpClient? _httpClient;
        private static readonly object _httpClientLock = new();
        private static HttpClient GetHttpClient()
        {
            if (_httpClient != null) return _httpClient;
            lock (_httpClientLock)
            {
                if (_httpClient != null) return _httpClient;
                // With ResponseHeadersRead this timeout only bounds the wait for response headers
                // (generous on purpose: scanning proxies can hold them while they fetch the whole
                // file). The body is bounded per read by the stall watchdog in DownloadAsync, and
                // the caller's CancellationToken covers "Later" / window close.
                var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                client.DefaultRequestHeaders.Add("User-Agent", "VoiceLite-Desktop/UpdateInstaller");
                _httpClient = client;
                return _httpClient;
            }
        }

        public static string GetUpdatesDirectory() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VoiceLite",
            "updates");

        public static string GetInstallerPath(string version, string? directory = null) =>
            Path.Combine(directory ?? GetUpdatesDirectory(), $"VoiceLite-Setup-{version}.exe");

        /// <summary>
        /// Downloads the installer for <paramref name="info"/>, streaming progress (0..1),
        /// then verifies its SHA-256. Returns the verified installer path.
        /// Throws <see cref="InvalidOperationException"/> before touching the network when the
        /// release carries no hash, and <see cref="UpdateVerificationException"/> (after
        /// deleting the file) on a hash mismatch.
        /// </summary>
        public static Task<string> DownloadAsync(
            UpdateInfo info,
            IProgress<double>? progress,
            CancellationToken ct,
            string? directory = null) =>
            DownloadAsync(info, progress, ct, directory, GetHttpClient(), StallTimeout);

        /// <summary>Test seam: the public overload with the HTTP client and stall window injected.</summary>
        internal static async Task<string> DownloadAsync(
            UpdateInfo info,
            IProgress<double>? progress,
            CancellationToken ct,
            string? directory,
            HttpClient httpClient,
            TimeSpan stallTimeout)
        {
            if (!info.CanAutoInstall)
            {
                ErrorLogger.LogWarning(
                    $"UpdateInstaller: refusing to auto-install {info.Version} - " +
                    (info.AssetName == null ? "release has no installer asset" : "no SHA-256 published for the asset"));
                throw new InvalidOperationException(
                    $"VoiceLite {info.Version} cannot be installed automatically because the release does not publish a SHA-256 hash for the installer.");
            }

            var dir = directory ?? GetUpdatesDirectory();
            Directory.CreateDirectory(dir);
            var target = GetInstallerPath(info.Version, dir);
            DeleteStaleFiles(dir, keep: target);

            // A previous run may have already downloaded and verified this exact installer
            // (e.g. the user closed the window right before install). Re-verify and reuse.
            if (File.Exists(target))
            {
                var existing = await ComputeSha256Async(target, ct).ConfigureAwait(false);
                if (string.Equals(existing, info.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    ErrorLogger.LogWarning($"UpdateInstaller: reusing already-verified {target}");
                    progress?.Report(1.0);
                    return target;
                }
                ErrorLogger.LogWarning($"UpdateInstaller: stale {target} failed verification, re-downloading");
                File.Delete(target);
            }

            var partial = target + ".partial";
            ErrorLogger.LogWarning($"UpdateInstaller: downloading {info.DownloadUrl} -> {partial}");
            try
            {
                long readBytes = 0;
                // Stall watchdog (same approach as ModelInstaller.DownloadArchiveAsync): re-armed
                // before every body read, paused during disk writes so a slow disk / AV scan is
                // never mistaken for a dead connection. Header phase stays on HttpClient.Timeout.
                using var stallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                try
                {
                    using var response = await httpClient
                        .GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                        .ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength;

                    await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
                    await using (var dest = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
                    {
                        var buffer = new byte[BufferSize];
                        while (true)
                        {
                            stallCts.CancelAfter(stallTimeout);
                            var n = await source.ReadAsync(buffer, stallCts.Token).ConfigureAwait(false);
                            if (n <= 0) break;

                            stallCts.CancelAfter(Timeout.InfiniteTimeSpan);
                            await dest.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                            readBytes += n;
                            if (totalBytes > 0)
                                progress?.Report(Math.Min(0.999, (double)readBytes / totalBytes.Value));
                        }
                        stallCts.CancelAfter(Timeout.InfiniteTimeSpan);
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // Not the caller's token: the stall watchdog or HttpClient.Timeout fired.
                    // Callers treat OperationCanceledException as "the user cancelled", so this
                    // must surface as a real failure (error + retry) instead.
                    ErrorLogger.LogWarning($"UpdateInstaller: download stalled or timed out after {readBytes:N0} bytes");
                    throw new IOException("The download stalled or timed out.");
                }

                ErrorLogger.LogWarning($"UpdateInstaller: downloaded {readBytes:N0} bytes, verifying SHA-256 ({info.Sha256Source})");
                await VerifyOrDeleteAsync(partial, info.Sha256!, ct).ConfigureAwait(false);

                File.Move(partial, target, overwrite: true);
                ErrorLogger.LogWarning($"UpdateInstaller: verified installer at {target}");
                progress?.Report(1.0);
                return target;
            }
            catch
            {
                TryDelete(partial);
                throw;
            }
        }

        /// <summary>
        /// Computes the file's SHA-256 and compares it to <paramref name="expectedHex"/>.
        /// On mismatch the file is deleted and <see cref="UpdateVerificationException"/> is thrown.
        /// </summary>
        internal static async Task VerifyOrDeleteAsync(string path, string expectedHex, CancellationToken ct = default)
        {
            var actual = await ComputeSha256Async(path, ct).ConfigureAwait(false);
            if (string.Equals(actual, expectedHex, StringComparison.OrdinalIgnoreCase))
                return;

            ErrorLogger.LogError(
                "UpdateInstaller: SHA-256 mismatch, deleting download",
                new UpdateVerificationException($"expected {expectedHex}, got {actual}"));
            TryDelete(path);
            throw new UpdateVerificationException(
                "The downloaded installer failed its SHA-256 check and was deleted. " +
                "The download may be corrupt or tampered with. Try again, or download from the release page.");
        }

        internal static async Task<string> ComputeSha256Async(string path, CancellationToken ct = default)
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
            var digest = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
            return Convert.ToHexString(digest);
        }

        private const string ReadyFilePrefix = "install-ready-";
        private static readonly TimeSpan ReadyPollInterval = TimeSpan.FromMilliseconds(250);

        /// <summary>Inno Setup log of the last in-app update (/LOG), next to voicelite.log.</summary>
        internal static string GetSetupLogPath() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VoiceLite",
            "logs",
            "update-setup.log");

        /// <summary>
        /// Launches the verified installer silently, then waits for it to prove it is really
        /// going to install before shutting VoiceLite down. Handshake (VoiceLiteSetup.iss,
        /// 2.5.2+): we pass /READYFILE="&lt;path&gt;" and the installer creates that file at the
        /// start of PrepareToInstall - i.e. after any UAC prompt was accepted and [Code] runs.
        ///   ready file appears        -> shut down (MainWindow.OnClosed flushes settings; the
        ///                                installer's PrepareToInstall kills a lingering
        ///                                VoiceLite.exe as a backstop)
        ///   installer exits first     -> <see cref="UpdateInstallFailedException"/>, VoiceLite keeps running
        /// There is deliberately no timeout while the installer is alive: a UAC prompt can sit
        /// for minutes. An installer that doesn't know /READYFILE ignores it and falls back to
        /// its own wait-then-taskkill of VoiceLite.exe, which is what 2.5.1 relied on anyway.
        /// </summary>
        public static Task InstallAsync(string installerPath) =>
            InstallAsync(installerPath, StartInstallerProcess, ShutdownApplication, ReadyPollInterval, GetSetupLogPath());

        /// <summary>
        /// Test seam. <paramref name="startInstaller"/> launches the process and returns a probe
        /// that yields null while it is running and its exit code once it has exited.
        /// </summary>
        internal static async Task InstallAsync(
            string installerPath,
            Func<ProcessStartInfo, Func<int?>> startInstaller,
            Action shutdown,
            TimeSpan pollInterval,
            string? logPath)
        {
            if (!File.Exists(installerPath))
                throw new FileNotFoundException("Installer not found", installerPath);

            var dir = Path.GetDirectoryName(Path.GetFullPath(installerPath)) ?? string.Empty;

            // Unique name per attempt + sweep of older ones: a leftover file from a previous
            // attempt (possibly admin-owned and undeletable) can never be read as "ready".
            foreach (var stale in Directory.EnumerateFiles(dir, ReadyFilePrefix + "*"))
                TryDelete(stale);
            var readyFile = Path.Combine(dir, $"{ReadyFilePrefix}{Guid.NewGuid():N}.flag");

            // ProcessStartInfo.Arguments does no %VAR% expansion, so both paths are absolute.
            var arguments = $"{InstallerArguments} /READYFILE=\"{readyFile}\"";
            if (logPath != null)
            {
                try
                {
                    // Inno aborts when it cannot create the /LOG file, so only pass it when the
                    // directory is known to exist.
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                    arguments += $" /LOG=\"{logPath}\"";
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogWarning($"UpdateInstaller: no setup log, could not create its directory: {ex.Message}");
                }
            }

            ErrorLogger.LogWarning($"UpdateInstaller: launching \"{installerPath}\" {arguments}");
            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = arguments,
                UseShellExecute = true,
                WorkingDirectory = dir,
            };
            var exitCodeIfExited = startInstaller(psi);

            while (true)
            {
                // Exit is checked first: if the installer is gone there is nothing left that
                // would update or relaunch us, whatever it managed to write.
                var exitCode = exitCodeIfExited();
                if (exitCode != null)
                {
                    TryDelete(readyFile);
                    ErrorLogger.LogError(
                        "UpdateInstaller: installer exited before signalling ready - staying running",
                        new UpdateInstallFailedException(exitCode.Value));
                    throw new UpdateInstallFailedException(exitCode.Value);
                }

                if (File.Exists(readyFile))
                {
                    ErrorLogger.LogWarning("UpdateInstaller: installer signalled ready, shutting down VoiceLite for update");
                    shutdown();
                    return;
                }

                await Task.Delay(pollInterval).ConfigureAwait(false);
            }
        }

        private static Func<int?> StartInstallerProcess(ProcessStartInfo psi)
        {
            // Not disposed here: the handle is needed for HasExited/ExitCode while we wait.
            var proc = Process.Start(psi);
            if (proc == null)
                throw new InvalidOperationException("Process.Start returned null for the installer");

            ErrorLogger.LogWarning($"UpdateInstaller: installer pid {proc.Id} started, waiting for it to signal ready");
            return () =>
            {
                if (!proc.HasExited) return null;
                var code = proc.ExitCode;
                proc.Dispose();
                return code;
            };
        }

        private static void ShutdownApplication()
        {
            var app = Application.Current;
            if (app != null)
                app.Dispatcher.BeginInvoke(new Action(() => app.Shutdown()));
        }

        private static void DeleteStaleFiles(string dir, string keep)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    if (string.Equals(file, keep, StringComparison.OrdinalIgnoreCase)) continue;
                    TryDelete(file);
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning($"UpdateInstaller: could not sweep {dir}: {ex.Message}");
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning($"UpdateInstaller: could not delete {path}: {ex.Message}");
            }
        }
    }
}

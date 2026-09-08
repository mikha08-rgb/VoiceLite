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

        // Same lazy-init pattern as UpdateCheckService: never construct HttpClient during startup.
        private static HttpClient? _httpClient;
        private static readonly object _httpClientLock = new();
        private static HttpClient GetHttpClient()
        {
            if (_httpClient != null) return _httpClient;
            lock (_httpClientLock)
            {
                if (_httpClient != null) return _httpClient;
                // Installer is ~100 MB; 10 minutes covers a slow link, and the caller's
                // CancellationToken covers "Later" / window close.
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
        public static async Task<string> DownloadAsync(
            UpdateInfo info,
            IProgress<double>? progress,
            CancellationToken ct,
            string? directory = null)
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
                using var response = await GetHttpClient()
                    .GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength;
                long readBytes = 0;

                await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
                await using (var dest = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
                {
                    var buffer = new byte[BufferSize];
                    int n;
                    while ((n = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                    {
                        await dest.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                        readBytes += n;
                        if (totalBytes > 0)
                            progress?.Report(Math.Min(0.999, (double)readBytes / totalBytes.Value));
                    }
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

        /// <summary>
        /// Launches the verified installer silently and shuts VoiceLite down so the installer
        /// can replace files. MainWindow.OnClosed flushes settings on the way out; the
        /// installer's PrepareToInstall also kills any lingering VoiceLite.exe as a backstop.
        /// </summary>
        public static void Install(string installerPath)
        {
            if (!File.Exists(installerPath))
                throw new FileNotFoundException("Installer not found", installerPath);

            ErrorLogger.LogWarning($"UpdateInstaller: launching \"{installerPath}\" {InstallerArguments}");
            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = InstallerArguments,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(installerPath) ?? string.Empty,
            };
            using var proc = Process.Start(psi);
            if (proc == null)
                throw new InvalidOperationException("Process.Start returned null for the installer");

            ErrorLogger.LogWarning($"UpdateInstaller: installer pid {proc.Id} started, shutting down VoiceLite for update");
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

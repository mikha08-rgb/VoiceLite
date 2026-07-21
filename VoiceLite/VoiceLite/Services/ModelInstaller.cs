using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace VoiceLite.Services
{
    /// <summary>
    /// Immutable description of a downloadable speech model. Integrity metadata is
    /// pinned to the exact upstream release asset so a truncated or replaced archive
    /// is rejected before any live model files are changed.
    /// </summary>
    internal sealed record ModelManifest(
        string ModelId,
        Uri DownloadUri,
        string ModelDirectory,
        IReadOnlyCollection<string> RequiredFiles,
        string TempFilePrefix,
        long ArchiveSizeBytes,
        string ArchiveSha256,
        long ArchiveDiskSpaceBytes,
        long ExtractedDiskSpaceBytes);

    internal enum ModelInstallStage
    {
        Downloading,
        Verifying,
        Extracting,
        Installing,
        Complete
    }

    internal readonly record struct ModelInstallProgress(
        ModelInstallStage Stage,
        int Percentage);

    internal enum ModelRecoveryResult
    {
        NoChange,
        RestoredBackup,
        PromotedTemporaryDirectory,
        NoWorkingModelFound
    }

    /// <summary>
    /// Headless downloader and crash-safe installer for VoiceLite model bundles.
    /// UI concerns (selection, dialogs, binding) stay in ModelDownloadControl.
    /// </summary>
    internal sealed class ModelInstaller
    {
        private const int StallTimeoutSeconds = 60;

        // Shared HttpClient prevents socket exhaustion. The timeout is infinite because
        // these archives can take more than 30 minutes on slow links. Per-read stall
        // detection below still fails a dead connection after 60 seconds without data.
        private static readonly HttpClient SharedHttpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan,
            DefaultRequestHeaders = { { "User-Agent", "VoiceLite-Desktop/1.0" } }
        };

        private readonly HttpClient httpClient;

        internal ModelInstaller(HttpClient? httpClient = null)
        {
            this.httpClient = httpClient ?? SharedHttpClient;
        }

        internal async Task InstallAsync(
            ModelManifest manifest,
            IProgress<ModelInstallProgress>? progress,
            CancellationToken cancellationToken)
        {
            ValidateManifest(manifest);

            // Resolve a killed previous install before starting another one. This also
            // guarantees the stable backup path is free for the next atomic swap.
            RecoverInterruptedInstall(manifest);

            var parentDirectory = Path.GetDirectoryName(manifest.ModelDirectory)
                ?? throw new InvalidOperationException(
                    $"Model directory has no parent: {manifest.ModelDirectory}");
            Directory.CreateDirectory(parentDirectory);

            string? tempArchivePath = Path.Combine(
                Path.GetTempPath(),
                $"{manifest.TempFilePrefix}-{Guid.NewGuid():N}.tar.bz2");
            string? tempExtractDirectory = null;

            try
            {
                EnsureSufficientDiskSpace(
                    tempArchivePath,
                    parentDirectory,
                    manifest.ArchiveDiskSpaceBytes,
                    manifest.ExtractedDiskSpaceBytes);

                await DownloadArchiveAsync(
                    manifest,
                    tempArchivePath,
                    progress,
                    cancellationToken);

                progress?.Report(new ModelInstallProgress(ModelInstallStage.Verifying, 92));
                await VerifyArchiveAsync(tempArchivePath, manifest, cancellationToken);

                tempExtractDirectory = Path.Combine(
                    parentDirectory,
                    $"{Path.GetFileName(manifest.ModelDirectory)}.tmp-{Guid.NewGuid():N}");

                progress?.Report(new ModelInstallProgress(ModelInstallStage.Extracting, 96));
                var archivePath = tempArchivePath;
                var extractDirectory = tempExtractDirectory;
                await Task.Run(
                    () => ExtractRequiredFiles(
                        archivePath,
                        extractDirectory,
                        manifest.RequiredFiles,
                        cancellationToken),
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (!HasRequiredModelFiles(manifest, tempExtractDirectory))
                {
                    throw new InvalidDataException(
                        $"Extraction completed but required model files are missing or empty in " +
                        tempExtractDirectory);
                }

                progress?.Report(new ModelInstallProgress(ModelInstallStage.Installing, 99));
                SwapIntoPlace(manifest, tempExtractDirectory);
                tempExtractDirectory = null; // now owned by the live model path

                progress?.Report(new ModelInstallProgress(ModelInstallStage.Complete, 100));
            }
            finally
            {
                if (tempArchivePath != null && File.Exists(tempArchivePath))
                {
                    try { File.Delete(tempArchivePath); }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogDebug($"Temp model archive cleanup failed: {ex.Message}");
                    }
                }

                if (tempExtractDirectory != null && Directory.Exists(tempExtractDirectory))
                {
                    TryDeleteDirectory(tempExtractDirectory, "Temp model extraction cleanup");
                }
            }
        }

        internal static async Task VerifyArchiveAsync(
            string archivePath,
            ModelManifest manifest,
            CancellationToken cancellationToken = default)
        {
            var archiveInfo = new FileInfo(archivePath);
            if (!archiveInfo.Exists || archiveInfo.Length != manifest.ArchiveSizeBytes)
            {
                var actualBytes = archiveInfo.Exists ? archiveInfo.Length : 0;
                throw new InvalidDataException(
                    $"Model archive size mismatch for {manifest.ModelId}: expected " +
                    $"{manifest.ArchiveSizeBytes:N0} bytes, received {actualBytes:N0} bytes.");
            }

            await using var archiveStream = new FileStream(
                archivePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                1024 * 1024,
                useAsync: true);
            var digest = await SHA256.HashDataAsync(archiveStream, cancellationToken);
            var actualSha256 = Convert.ToHexString(digest);

            if (!actualSha256.Equals(manifest.ArchiveSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Model archive checksum mismatch for {manifest.ModelId}. " +
                    "The download may be corrupt or the upstream file may have changed.");
            }
        }

        internal static bool HasRequiredModelFiles(
            ModelManifest manifest,
            string? directory = null)
        {
            var modelDirectory = directory ?? manifest.ModelDirectory;
            if (!Directory.Exists(modelDirectory))
                return false;

            try
            {
                return manifest.RequiredFiles.All(fileName =>
                {
                    var info = new FileInfo(Path.Combine(modelDirectory, fileName));
                    return info.Exists && info.Length > 0;
                });
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Recovers the stable states left by a process death during an install:
        /// live+backup means the new model won and the backup can go; no live+backup
        /// means the first rename completed and the known-working backup is restored;
        /// no live+complete temp promotes the verified extraction from a first install.
        /// </summary>
        internal static ModelRecoveryResult RecoverInterruptedInstall(ModelManifest manifest)
        {
            ValidateManifest(manifest);

            var liveDirectory = manifest.ModelDirectory;
            var backupDirectory = GetBackupDirectory(manifest);
            var parentDirectory = Path.GetDirectoryName(liveDirectory)!;
            var tempDirectories = Directory.Exists(parentDirectory)
                ? Directory.EnumerateDirectories(
                        parentDirectory,
                        Path.GetFileName(liveDirectory) + ".tmp-*")
                    .OrderByDescending(Directory.GetLastWriteTimeUtc)
                    .ToArray()
                : Array.Empty<string>();

            if (HasRequiredModelFiles(manifest, liveDirectory))
            {
                if (Directory.Exists(backupDirectory))
                    TryDeleteDirectory(backupDirectory, "Completed model backup cleanup");
                DeleteTemporaryDirectories(tempDirectories, except: null);
                return ModelRecoveryResult.NoChange;
            }

            if (HasRequiredModelFiles(manifest, backupDirectory))
            {
                if (Directory.Exists(liveDirectory))
                    Directory.Delete(liveDirectory, recursive: true);

                Directory.Move(backupDirectory, liveDirectory);
                DeleteTemporaryDirectories(tempDirectories, except: null);
                return ModelRecoveryResult.RestoredBackup;
            }

            var completeTempDirectory = tempDirectories.FirstOrDefault(
                directory => HasRequiredModelFiles(manifest, directory));
            if (completeTempDirectory != null)
            {
                if (Directory.Exists(liveDirectory))
                    Directory.Delete(liveDirectory, recursive: true);

                Directory.Move(completeTempDirectory, liveDirectory);
                if (Directory.Exists(backupDirectory))
                    TryDeleteDirectory(backupDirectory, "Invalid model backup cleanup");
                DeleteTemporaryDirectories(tempDirectories, completeTempDirectory);
                return ModelRecoveryResult.PromotedTemporaryDirectory;
            }

            // Nothing here can become a working model. Remove only installer-owned
            // stale artifacts so they cannot block a clean retry. Keep an incomplete
            // live directory; the next successful swap will preserve it as the backup
            // until the replacement has moved into place.
            if (Directory.Exists(backupDirectory))
                TryDeleteDirectory(backupDirectory, "Invalid model backup cleanup");
            DeleteTemporaryDirectories(tempDirectories, except: null);
            return ModelRecoveryResult.NoWorkingModelFound;
        }

        internal static void SweepStaleArchiveDownloads(IEnumerable<ModelManifest> manifests)
        {
            try
            {
                foreach (var prefix in manifests
                             .Select(manifest => manifest.TempFilePrefix)
                             .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    foreach (var archivePath in Directory.EnumerateFiles(
                                 Path.GetTempPath(),
                                 $"{prefix}-*.tar.bz2"))
                    {
                        try { File.Delete(archivePath); }
                        catch { /* locked by an active download; leave it alone */ }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogDebug($"Stale model archive sweep failed: {ex.Message}");
            }
        }

        private async Task DownloadArchiveAsync(
            ModelManifest manifest,
            string destinationPath,
            IProgress<ModelInstallProgress>? progress,
            CancellationToken cancellationToken)
        {
            using var stallCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            stallCts.CancelAfter(TimeSpan.FromSeconds(StallTimeoutSeconds));

            using var response = await WrapStall(
                httpClient.GetAsync(
                    manifest.DownloadUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    stallCts.Token),
                cancellationToken);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is long contentLength &&
                contentLength != manifest.ArchiveSizeBytes)
            {
                throw new InvalidDataException(
                    $"Model archive size mismatch for {manifest.ModelId}: expected " +
                    $"{manifest.ArchiveSizeBytes:N0} bytes, server reported {contentLength:N0} bytes.");
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                65536,
                useAsync: true);

            var buffer = new byte[65536];
            long totalRead = 0;
            while (true)
            {
                stallCts.CancelAfter(TimeSpan.FromSeconds(StallTimeoutSeconds));
                var bytesRead = await WrapStall(
                    contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), stallCts.Token).AsTask(),
                    cancellationToken);
                if (bytesRead == 0)
                    break;

                stallCts.CancelAfter(Timeout.InfiniteTimeSpan); // pause timer during disk I/O
                await fileStream.WriteAsync(
                    buffer.AsMemory(0, bytesRead),
                    cancellationToken);
                totalRead += bytesRead;

                if (totalRead > manifest.ArchiveSizeBytes)
                {
                    throw new InvalidDataException(
                        $"Model archive size mismatch for {manifest.ModelId}: expected " +
                        $"{manifest.ArchiveSizeBytes:N0} bytes, received more data than expected.");
                }

                var percentage = (int)Math.Min(
                    90,
                    totalRead * 90L / manifest.ArchiveSizeBytes);
                progress?.Report(new ModelInstallProgress(
                    ModelInstallStage.Downloading,
                    percentage));
            }
        }

        private static async Task<T> WrapStall<T>(
            Task<T> task,
            CancellationToken cancellationToken)
        {
            try
            {
                return await task;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new IOException(
                    $"Connection stalled: no data received for {StallTimeoutSeconds} seconds. " +
                    "Check your internet connection, then click Download to retry.");
            }
        }

        private static void SwapIntoPlace(
            ModelManifest manifest,
            string tempExtractDirectory)
        {
            var liveDirectory = manifest.ModelDirectory;
            var backupDirectory = GetBackupDirectory(manifest);
            var movedLiveToBackup = false;

            try
            {
                if (Directory.Exists(backupDirectory))
                {
                    throw new IOException(
                        $"Cannot install {manifest.ModelId}: a previous model backup still exists.");
                }

                if (Directory.Exists(liveDirectory))
                {
                    Directory.Move(liveDirectory, backupDirectory);
                    movedLiveToBackup = true;
                }

                Directory.Move(tempExtractDirectory, liveDirectory);

                if (!HasRequiredModelFiles(manifest, liveDirectory))
                {
                    throw new InvalidDataException(
                        $"Model install verification failed after swap in {liveDirectory}");
                }
            }
            catch
            {
                // A normal exception is not a crash; restore the old working model
                // immediately. Process death between these lines is handled at startup
                // by RecoverInterruptedInstall.
                if (movedLiveToBackup && Directory.Exists(backupDirectory))
                {
                    if (Directory.Exists(liveDirectory))
                        Directory.Delete(liveDirectory, recursive: true);
                    Directory.Move(backupDirectory, liveDirectory);
                }

                throw;
            }

            if (Directory.Exists(backupDirectory))
                TryDeleteDirectory(backupDirectory, "Installed model backup cleanup");
        }

        private static void ExtractRequiredFiles(
            string tarBz2Path,
            string targetDirectory,
            IReadOnlyCollection<string> requiredFiles,
            CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(targetDirectory);
            var wanted = new HashSet<string>(requiredFiles, StringComparer.OrdinalIgnoreCase);

            using var archive = ArchiveFactory.OpenArchive(
                tarBz2Path,
                new SharpCompress.Readers.ReaderOptions());
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.IsDirectory)
                    continue;

                var entryName = Path.GetFileName(entry.Key ?? string.Empty);
                if (string.IsNullOrEmpty(entryName) || !wanted.Contains(entryName))
                    continue;

                var destinationPath = Path.Combine(targetDirectory, entryName);
                entry.WriteToFile(
                    destinationPath,
                    new ExtractionOptions { Overwrite = true });
            }
        }

        private static void EnsureSufficientDiskSpace(
            string tempFilePath,
            string targetDirectory,
            long archiveBytesRequired,
            long extractedBytesRequired)
        {
            try
            {
                var tempRoot = Path.GetPathRoot(Path.GetFullPath(tempFilePath));
                var targetRoot = Path.GetPathRoot(Path.GetFullPath(targetDirectory));
                if (string.IsNullOrEmpty(tempRoot) || string.IsNullOrEmpty(targetRoot))
                    return;

                var requirements = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    [tempRoot] = archiveBytesRequired
                };
                if (requirements.ContainsKey(targetRoot))
                    requirements[targetRoot] += extractedBytesRequired;
                else
                    requirements[targetRoot] = extractedBytesRequired;

                foreach (var (root, requiredBytes) in requirements)
                {
                    long availableBytes;
                    try
                    {
                        availableBytes = new DriveInfo(root).AvailableFreeSpace;
                    }
                    catch (Exception ex)
                    {
                        // Best-effort for unusual/UNC drives: never reject a download
                        // solely because the preflight itself could not run.
                        ErrorLogger.LogDebug(
                            $"Disk-space preflight skipped for {root}: {ex.Message}");
                        continue;
                    }

                    if (availableBytes < requiredBytes)
                    {
                        throw new IOException(
                            $"Not enough disk space on drive {root} — " +
                            $"{requiredBytes / 1_000_000_000.0:F1} GB required, " +
                            $"{availableBytes / 1_000_000_000.0:F1} GB available.\n" +
                            "Free up space, then click Download to retry.");
                    }
                }
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogDebug($"Disk-space preflight skipped: {ex.Message}");
            }
        }

        private static string GetBackupDirectory(ModelManifest manifest) =>
            manifest.ModelDirectory + ".backup";

        private static void DeleteTemporaryDirectories(
            IEnumerable<string> directories,
            string? except)
        {
            foreach (var directory in directories)
            {
                if (except != null &&
                    directory.Equals(except, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Directory.Exists(directory))
                    TryDeleteDirectory(directory, "Stale model extraction cleanup");
            }
        }

        private static void TryDeleteDirectory(string directory, string operation)
        {
            try { Directory.Delete(directory, recursive: true); }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning($"{operation} failed for {directory}: {ex.Message}");
            }
        }

        private static void ValidateManifest(ModelManifest manifest)
        {
            ArgumentNullException.ThrowIfNull(manifest);

            if (string.IsNullOrWhiteSpace(manifest.ModelId) ||
                string.IsNullOrWhiteSpace(manifest.ModelDirectory) ||
                string.IsNullOrWhiteSpace(manifest.TempFilePrefix) ||
                manifest.RequiredFiles.Count == 0 ||
                manifest.ArchiveSizeBytes <= 0 ||
                manifest.ArchiveDiskSpaceBytes < manifest.ArchiveSizeBytes ||
                manifest.ExtractedDiskSpaceBytes <= 0 ||
                manifest.ArchiveSha256.Length != 64 ||
                manifest.ArchiveSha256.Any(character => !Uri.IsHexDigit(character)))
            {
                throw new ArgumentException("Model manifest is incomplete or invalid.", nameof(manifest));
            }
        }
    }
}

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// F5: a stalled update download must end (per-read stall watchdog, same idea as
    /// ModelInstaller.DownloadArchiveAsync) and neither a stall nor an HttpClient timeout may
    /// look like a user cancel. Fake HttpMessageHandler only - no network.
    /// </summary>
    public sealed class UpdateDownloadStallTests : IDisposable
    {
        private static readonly TimeSpan Guard = TimeSpan.FromSeconds(10);

        private readonly string dir = Path.Combine(
            Path.GetTempPath(),
            $"VoiceLite-UpdateStall-{Guid.NewGuid():N}");

        public void Dispose()
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }

        private static UpdateInfo Info(byte[] payload) => new()
        {
            Version = "9.9.9",
            DownloadUrl = "https://github.com/x/y/releases/download/v9.9.9/VoiceLite-Setup-9.9.9.exe",
            ReleasePageUrl = "https://github.com/x/y/releases/tag/v9.9.9",
            AssetName = "VoiceLite-Setup-9.9.9.exe",
            Sha256 = Convert.ToHexString(SHA256.HashData(payload)),
            Sha256Source = "digest",
        };

        private static async Task<Exception?> RunGuarded(Task download)
        {
            var finished = await Task.WhenAny(download, Task.Delay(Guard));
            finished.Should().BeSameAs(download, "the download must end on its own instead of hanging forever");
            try { await download; return null; }
            catch (Exception ex) { return ex; }
        }

        [Fact]
        public async Task Download_BodyStalls_ThrowsIOException_NotCancellation_AndDeletesPartial()
        {
            var payload = new byte[4096];
            using var client = new HttpClient(new FakeHandler(_ => new ScriptedStream(payload, chunk: 1024, stallAfterBytes: 1024)));

            var error = await RunGuarded(UpdateInstaller.DownloadAsync(
                Info(payload), null, CancellationToken.None, dir, client, TimeSpan.FromMilliseconds(200)));

            error.Should().BeOfType<IOException>("a stall is a network failure the UI must show with a retry");
            Directory.GetFiles(dir, "*.partial").Should().BeEmpty();
            File.Exists(UpdateInstaller.GetInstallerPath("9.9.9", dir)).Should().BeFalse();
        }

        [Fact]
        public async Task Download_HttpClientTimeout_IsNotReportedAsCancellation()
        {
            // What HttpClient.Timeout produces in .NET 8 while waiting for headers: a
            // TaskCanceledException (inner TimeoutException) with the caller's token NOT cancelled.
            using var client = new HttpClient(new FakeHandler(
                _ => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout", new TimeoutException())));

            var error = await RunGuarded(UpdateInstaller.DownloadAsync(
                Info(new byte[8]), null, CancellationToken.None, dir, client, TimeSpan.FromSeconds(60)));

            error.Should().NotBeNull();
            error.Should().NotBeAssignableTo<OperationCanceledException>(
                "UpdateWindow treats OperationCanceledException as 'the user closed the window' and shows nothing");
            error.Should().BeOfType<IOException>();
        }

        [Fact]
        public async Task Download_UserCancel_StillThrowsOperationCanceled_AndDeletesPartial()
        {
            var payload = new byte[4096];
            using var client = new HttpClient(new FakeHandler(_ => new ScriptedStream(payload, chunk: 1024, stallAfterBytes: 1024)));
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(150));

            var error = await RunGuarded(UpdateInstaller.DownloadAsync(
                Info(payload), null, cts.Token, dir, client, TimeSpan.FromSeconds(60)));

            error.Should().BeAssignableTo<OperationCanceledException>();
            Directory.GetFiles(dir, "*.partial").Should().BeEmpty();
        }

        [Fact]
        public async Task Download_SlowButSteady_DoesNotTripStall()
        {
            // 8 chunks x 60 ms = ~480 ms total, far longer than the 300 ms stall window: the
            // watchdog is per read, not a cap on the whole download (slow links must still finish).
            var payload = new byte[8 * 512];
            new Random(7).NextBytes(payload);
            using var client = new HttpClient(new FakeHandler(
                _ => new ScriptedStream(payload, chunk: 512, delayPerRead: TimeSpan.FromMilliseconds(60))));

            var download = UpdateInstaller.DownloadAsync(
                Info(payload), null, CancellationToken.None, dir, client, TimeSpan.FromMilliseconds(300));
            var error = await RunGuarded(download);

            error.Should().BeNull();
            var path = await download;
            path.Should().Be(UpdateInstaller.GetInstallerPath("9.9.9", dir));
            (await File.ReadAllBytesAsync(path)).Should().Equal(payload);
        }

        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Stream> body;
            public FakeHandler(Func<HttpRequestMessage, Stream> body) => this.body = body;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var stream = body(request);
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) };
                response.Content.Headers.ContentLength = stream.Length;
                return Task.FromResult(response);
            }
        }

        /// <summary>
        /// Serves <c>payload</c> in chunks. After <c>stallAfterBytes</c> the next read never
        /// completes until its token is cancelled - like a real socket read on a dead connection.
        /// </summary>
        private sealed class ScriptedStream : Stream
        {
            private readonly byte[] payload;
            private readonly int chunk;
            private readonly int stallAfterBytes;
            private readonly TimeSpan delayPerRead;
            private int position;

            public ScriptedStream(byte[] payload, int chunk, int stallAfterBytes = int.MaxValue, TimeSpan delayPerRead = default)
            {
                this.payload = payload;
                this.chunk = chunk;
                this.stallAfterBytes = stallAfterBytes;
                this.delayPerRead = delayPerRead;
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (position >= stallAfterBytes)
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                if (delayPerRead > TimeSpan.Zero)
                    await Task.Delay(delayPerRead, cancellationToken);

                var n = Math.Min(Math.Min(chunk, buffer.Length), payload.Length - position);
                payload.AsMemory(position, n).CopyTo(buffer);
                position += n;
                return n;
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
                ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

            public override int Read(byte[] buffer, int offset, int count) =>
                ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => payload.Length;
            public override long Position { get => position; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}

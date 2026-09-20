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
            var finished = await Task.WhenAny(download, Task.Delay(Guard)).ConfigureAwait(false);
            finished.Should().BeSameAs(download, "the download must end on its own instead of hanging forever");
            try { await download.ConfigureAwait(false); return null; }
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

        // 12 reads x 200 ms = 2.4 s in total against a 2 s per-read window: the whole download
        // still takes LONGER than the window (the point of the test), but each read has 1.8 s
        // of slack instead of the former 240 ms (60 ms reads, 300 ms window), which a loaded
        // 4-core CI runner could eat - and release.yml's build job "needs: test".
        private static readonly TimeSpan SlowButSteadyStallWindow = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan SlowButSteadyDelayPerRead = TimeSpan.FromMilliseconds(200);
        private const int SlowButSteadyChunks = 12;

        private async Task<Exception?> RunSlowButSteady(TimeSpan stallWindow, TimeSpan delayPerRead, int chunks)
        {
            (delayPerRead * chunks).Should().BeGreaterThan(stallWindow,
                "the point of the test: the watchdog is per read, not a cap on the whole download (slow links must still finish)");

            var payload = new byte[chunks * 512];
            new Random(7).NextBytes(payload);
            using var client = new HttpClient(new FakeHandler(
                _ => new ScriptedStream(payload, chunk: 512, delayPerRead: delayPerRead)));

            // Task.Run: FakeHandler completes synchronously, so without it DownloadAsync would run
            // up to its first read on the caller's (xunit's bounded) SynchronizationContext.
            var download = Task.Run(() => UpdateInstaller.DownloadAsync(
                Info(payload), null, CancellationToken.None, dir, client, stallWindow));
            var error = await RunGuarded(download).ConfigureAwait(false);
            if (error != null) return error;

            var path = await download.ConfigureAwait(false);
            path.Should().Be(UpdateInstaller.GetInstallerPath("9.9.9", dir));
            (await File.ReadAllBytesAsync(path).ConfigureAwait(false)).Should().Equal(payload);
            return null;
        }

        [Fact]
        public async Task Download_SlowButSteady_DoesNotTripStall()
        {
            (await RunSlowButSteady(SlowButSteadyStallWindow, SlowButSteadyDelayPerRead, SlowButSteadyChunks)).Should().BeNull();
        }

        [Fact]
        public async Task Download_SlowButSteady_DoesNotDependOnTheTestSynchronizationContext()
        {
            // xunit runs async tests on a bounded worker context (one worker per core). Other
            // test classes block those workers for seconds (child pwsh, STA thread joins), so a
            // scripted read whose continuation is posted there resumes late, the stall window
            // expires and the slow-but-steady test fails although nothing is wrong. Simulated
            // here with a context that delivers every post later than the stall window: if any
            // read (or the download itself) captured it, the window below expires.
            var window = TimeSpan.FromSeconds(1);
            var previous = SynchronizationContext.Current;
            Task<Exception?> run;
            SynchronizationContext.SetSynchronizationContext(new StarvedSynchronizationContext(window * 1.5));
            try
            {
                run = RunSlowButSteady(window, delayPerRead: TimeSpan.FromMilliseconds(100), chunks: 12);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }

            (await run).Should().BeNull("blocked test workers must not look like a stalled download");
        }

        /// <summary>Every post arrives <c>delay</c> late, like a worker queue whose threads are all blocked.</summary>
        private sealed class StarvedSynchronizationContext : SynchronizationContext
        {
            private readonly TimeSpan delay;
            public StarvedSynchronizationContext(TimeSpan delay) => this.delay = delay;

            public override void Post(SendOrPostCallback d, object? state) =>
                Task.Delay(delay).ContinueWith(_ =>
                {
                    // Like xunit's worker threads: the callback runs WITH this context current,
                    // so every further await inside it queues here again.
                    SetSynchronizationContext(this);
                    try { d(state); }
                    finally { SetSynchronizationContext(null); }
                }, TaskScheduler.Default);
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
                // ConfigureAwait(false): a read must complete on the thread pool like a real socket
                // read, never queue behind xunit's bounded (and often blocked) test workers.
                if (position >= stallAfterBytes)
                    await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                if (delayPerRead > TimeSpan.Zero)
                    await Task.Delay(delayPerRead, cancellationToken).ConfigureAwait(false);

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

using System;
using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using VoiceLite.Models;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// Regression tests for the v2.4.1 transcription lifecycle fixes.
    /// OfflineRecognizer.Decode is blocking native code that cannot be cancelled once
    /// started, so (a) a dictation the UI already abandoned (stuck-state timeout,
    /// shutdown, or a newer dictation) must be identifiable as stale via its dictation
    /// session id — MainWindow discards stale results instead of injecting them — and
    /// (b) Dispose must never free the native recognizer while a decode still holds the
    /// transcription slot; it defers disposal to the slot holder instead.
    /// </summary>
    public class TranscriptionLifecycleTests
    {
        // ---- Dictation session identity (the stale-result discard contract) ----

        [Fact]
        public void BeginDictationSession_MakesThatSessionCurrent()
        {
            using var service = new TranscriptionService(new Settings());

            var session = service.BeginDictationSession();

            service.IsDictationSessionCurrent(session).Should().BeTrue();
        }

        [Fact]
        public void TimedOutDecode_CompletingLate_IsStale()
        {
            using var service = new TranscriptionService(new Settings());

            // A dictation starts; its native decode hangs past the stuck-state timeout.
            var session = service.BeginDictationSession();

            // MainWindow's stuck-state recovery expires the session at timeout...
            service.ExpireDictationSessions();

            // ...so when the non-cancellable decode finally returns, the id is stale and
            // MainWindow discards the text instead of injecting it into the (by now
            // unrelated) foreground window.
            service.IsDictationSessionCurrent(session).Should().BeFalse(
                "a dictation the UI timed out must never inject its late result");
        }

        [Fact]
        public void NewDictationAfterTimeout_GetsFreshSession_AndOnlyItsOwnResultIsCurrent()
        {
            using var service = new TranscriptionService(new Settings());

            var timedOut = service.BeginDictationSession();
            service.ExpireDictationSessions();           // stuck-state recovery fired

            var fresh = service.BeginDictationSession(); // user dictates again

            service.IsDictationSessionCurrent(fresh).Should().BeTrue(
                "the new dictation must be able to consume its own result");
            service.IsDictationSessionCurrent(timedOut).Should().BeFalse(
                "the abandoned dictation must stay stale even after a new one starts");
        }

        [Fact]
        public void BeginningANewDictation_ExpiresThePreviousOne()
        {
            using var service = new TranscriptionService(new Settings());

            var older = service.BeginDictationSession();
            var newer = service.BeginDictationSession();

            service.IsDictationSessionCurrent(older).Should().BeFalse(
                "only the newest dictation's result may be injected");
            service.IsDictationSessionCurrent(newer).Should().BeTrue();
        }

        [Fact]
        public async Task Dispose_ExpiresOutstandingSessions()
        {
            var service = new TranscriptionService(new Settings());
            await service.WarmupTask;
            var session = service.BeginDictationSession();

            service.Dispose();

            service.IsDictationSessionCurrent(session).Should().BeFalse(
                "a decode completing during/after shutdown must be discarded");
        }

        // ---- Deferred native disposal (shutdown during an active decode) ----

        [Fact]
        public async Task Dispose_WhileDecodeSlotHeld_DefersNativeDisposal_ThenHolderCompletesIt()
        {
            var service = new TranscriptionService(new Settings())
            {
                DisposeWaitTimeout = TimeSpan.FromMilliseconds(100),
            };
            await service.WarmupTask;

            // Simulate an in-flight decode: hold the transcription slot exactly the way
            // TranscribeFromStreamAsync holds it across the native Decode call.
            service.AcquireTranscriptionSlotForTest();

            // Shutdown arrives mid-decode: Dispose must give up waiting, defer, not crash.
            service.Dispose();

            service.IsRecognizerDisposalDeferred.Should().BeTrue(
                "Dispose must never free the recognizer while a decode may still be running on it");

            // The "decode" finishes → the holder's release path completes the disposal.
            service.ReleaseTranscriptionSlotForTest();

            service.IsRecognizerDisposalDeferred.Should().BeFalse(
                "the slot holder must perform the deferred disposal when its decode completes");

            // Native resources are actually gone: the transcription slot itself is disposed.
            var act = () => service.AcquireTranscriptionSlotForTest();
            act.Should().Throw<ObjectDisposedException>();
        }

        [Fact]
        public async Task Dispose_WithNoDecodeInFlight_DisposesImmediately()
        {
            var service = new TranscriptionService(new Settings());
            await service.WarmupTask;

            service.Dispose();

            service.IsRecognizerDisposalDeferred.Should().BeFalse(
                "with no decode in flight there is nothing to defer");

            var act = () => service.AcquireTranscriptionSlotForTest();
            act.Should().Throw<ObjectDisposedException>(
                "immediate disposal must release the native resources right away");
        }

        [Fact]
        public async Task ShutdownDuringActiveDecode_DefersDisposal_AndDoesNotCrash()
        {
            if (!TranscriptionServiceFixture.ModelPresent) return; // no model on this machine (e.g. bare CI)

            var service = new TranscriptionService(new Settings())
            {
                DisposeWaitTimeout = TimeSpan.FromMilliseconds(1),
            };
            await service.WarmupTask;

            // Long non-speech audio so the native decode is still running when Dispose hits.
            var noisePath = CreateNoiseWav(durationSeconds: 30);
            try
            {
                var transcribeTask = service.TranscribeAsync(noisePath, TranscriptionServiceFixture.ModelDir);

                // Wait until the transcription holds the decode slot, then give Decode a
                // moment to actually enter native code.
                var waitStart = DateTime.UtcNow;
                while (!service.IsProcessing && DateTime.UtcNow - waitStart < TimeSpan.FromSeconds(10))
                {
                    await Task.Delay(10);
                }
                service.IsProcessing.Should().BeTrue("the transcription must be in flight before Dispose");
                await Task.Delay(300);

                // Shutdown mid-decode. Before this fix the recognizer was disposed under a
                // live native Decode — a use-after-free that crashes the process with no
                // managed exception. Now disposal defers to the in-flight call.
                service.Dispose();

                // The decode must run to completion without a native crash. Its result is
                // irrelevant (MainWindow discards it — Dispose expired the session). An
                // OperationCanceledException is also safe: Dispose's cancel won the race
                // before the decode task started.
                try
                {
                    await transcribeTask;
                }
                catch (OperationCanceledException) { }

                // Whichever side performed the disposal, the deferred handoff must be
                // complete and the native resources freed by now.
                service.IsRecognizerDisposalDeferred.Should().BeFalse(
                    "the in-flight call must complete a deferred disposal when its decode finishes");
                var act = () => service.AcquireTranscriptionSlotForTest();
                act.Should().Throw<ObjectDisposedException>();
            }
            finally
            {
                try { File.Delete(noisePath); } catch { }
            }
        }

        // 16kHz/16-bit/mono low-amplitude white noise — same format AudioRecorder produces.
        private static string CreateNoiseWav(int durationSeconds)
        {
            var rng = new Random(42); // deterministic: same "noise" every run
            var path = Path.Combine(Path.GetTempPath(), $"lifecycle-noise-{Guid.NewGuid():N}.wav");
            int sampleCount = 16000 * durationSeconds;
            int dataSize = sampleCount * 2;

            using var fs = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(fs);
            w.Write("RIFF".ToCharArray());
            w.Write(36 + dataSize);
            w.Write("WAVE".ToCharArray());
            w.Write("fmt ".ToCharArray());
            w.Write(16);
            w.Write((short)1);      // PCM
            w.Write((short)1);      // mono
            w.Write(16000);         // sample rate
            w.Write(32000);         // byte rate
            w.Write((short)2);      // block align
            w.Write((short)16);     // bits per sample
            w.Write("data".ToCharArray());
            w.Write(dataSize);
            for (int i = 0; i < sampleCount; i++)
            {
                w.Write((short)rng.Next(-3000, 3001));
            }
            return path;
        }
    }
}

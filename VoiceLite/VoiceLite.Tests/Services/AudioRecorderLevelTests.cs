using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using AwesomeAssertions;
using NAudio.Wave;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// AudioRecorder.LevelAvailable feeds the recording pill's level meter. It rides on the
    /// capture callback, so the contract is: purely additive, raised outside lockObject, and a
    /// subscriber that throws can never reach the capture path.
    /// </summary>
    public class AudioRecorderLevelTests
    {
        private static byte[] Pcm(params short[] samples)
        {
            var bytes = new byte[samples.Length * 2];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        [Fact]
        public void ComputeBufferPeak_IsThePeakAbsoluteSample_ScaledToOne()
        {
            AudioRecorder.ComputeBufferPeak(Pcm(0, 0, 0), 6).Should().Be(0f);
            AudioRecorder.ComputeBufferPeak(Pcm(100, -16384, 300), 6).Should().BeApproximately(0.5f, 1e-4f);
            AudioRecorder.ComputeBufferPeak(Pcm(short.MaxValue), 2).Should().BeApproximately(1f, 1e-4f);
            AudioRecorder.ComputeBufferPeak(Pcm(short.MinValue), 2).Should().Be(1f, "Math.Abs(short.MinValue) overflows unless widened first");
        }

        [Fact]
        public void ComputeBufferPeak_OnlyReadsTheRecordedBytes_AndNeverThrows()
        {
            // NAudio reuses one buffer: bytes past BytesRecorded are stale data from an earlier callback.
            AudioRecorder.ComputeBufferPeak(Pcm(1000, 30000), 2).Should().BeApproximately(1000 / 32768f, 1e-5f);

            AudioRecorder.ComputeBufferPeak(null!, 10).Should().Be(0f);
            AudioRecorder.ComputeBufferPeak(Array.Empty<byte>(), 0).Should().Be(0f);
            AudioRecorder.ComputeBufferPeak(new byte[1], 1).Should().Be(0f);
            AudioRecorder.ComputeBufferPeak(Pcm(500), 3).Should().BeApproximately(500 / 32768f, 1e-5f, "an odd trailing byte is ignored");
            AudioRecorder.ComputeBufferPeak(Pcm(500), 4000).Should().BeApproximately(500 / 32768f, 1e-5f, "a count past the buffer is clamped");
            AudioRecorder.ComputeBufferPeak(Pcm(500), -4).Should().Be(0f);
        }

        [Fact]
        public void RaiseLevelAvailable_DeliversThePeak()
        {
            using var recorder = new AudioRecorder();
            var levels = new List<float>();
            recorder.LevelAvailable += (_, level) => levels.Add(level);

            recorder.RaiseLevelAvailable(Pcm(0, 8192, -4096), 6);

            levels.Should().ContainSingle().Which.Should().BeApproximately(0.25f, 1e-4f);
        }

        [Fact]
        public void RaiseLevelAvailable_SwallowsAThrowingSubscriber_AndWorksWithNone()
        {
            using var recorder = new AudioRecorder();
            var silent = () => recorder.RaiseLevelAvailable(Pcm(1, 2, 3), 6);
            silent.Should().NotThrow("no subscriber");

            recorder.LevelAvailable += (_, _) => throw new InvalidOperationException("pill broke");
            var act = () => recorder.RaiseLevelAvailable(Pcm(1, 2, 3), 6);
            act.Should().NotThrow("an exception here would unwind into NAudio's capture callback");
        }

        // ---- the real capture callback, driven without a device ----
        // OnDataAvailable only needs isRecording + a WaveFileWriter; both are set by reflection
        // (over a MemoryStream - nothing is opened, no file is written) and reset before Dispose.

        private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

        private static void WithFakeRecordingSession(Action<AudioRecorder, Action<byte[], int>, MemoryStream> body)
        {
            var isRecording = typeof(AudioRecorder).GetField("isRecording", Priv);
            var waveFile = typeof(AudioRecorder).GetField("waveFile", Priv);
            var onData = typeof(AudioRecorder).GetMethod("OnDataAvailable", Priv);
            Assert.True(isRecording != null && waveFile != null && onData != null,
                "AudioRecorder.isRecording / waveFile / OnDataAvailable were renamed - update this test");

            using var recorder = new AudioRecorder();
            var stream = new MemoryStream();
            var writer = new WaveFileWriter(stream, new WaveFormat(16000, 16, 1));
            try
            {
                waveFile!.SetValue(recorder, writer);
                isRecording!.SetValue(recorder, true);
                body(recorder, (buffer, count) => onData!.Invoke(recorder, new object?[] { null, new WaveInEventArgs(buffer, count) }), stream);
            }
            finally
            {
                isRecording!.SetValue(recorder, false);
                waveFile!.SetValue(recorder, null);
                writer.Dispose();
            }
        }

        [Fact]
        public void OnDataAvailable_RaisesOneLevelPerWrittenBuffer_OfTheRecordedBytes_OutsideTheLock()
        {
            WithFakeRecordingSession((recorder, deliver, stream) =>
            {
                var lockObject = typeof(AudioRecorder).GetField("lockObject", Priv)?.GetValue(recorder);
                Assert.True(lockObject != null, "AudioRecorder.lockObject was renamed - update this test");

                var levels = new List<float>();
                bool raisedUnderLock = false;
                recorder.LevelAvailable += (_, level) =>
                {
                    levels.Add(level);
                    raisedUnderLock |= Monitor.IsEntered(lockObject!);
                };

                // NAudio reuses one buffer: the 30000 past BytesRecorded is stale and must not count.
                long before = stream.Length;
                deliver(Pcm(100, -8192, 30000), 4);

                levels.Should().ContainSingle().Which.Should().BeApproximately(0.25f, 1e-4f);
                raisedUnderLock.Should().BeFalse("a subscriber must never run under the capture lock (StopRecording takes it on the UI thread)");
                (stream.Length - before).Should().Be(4, "the level is additive: the buffer is still written, exactly once");
            });
        }

        [Fact]
        public void OnDataAvailable_RaisesNoLevel_ForBuffersItDoesNotWrite()
        {
            WithFakeRecordingSession((recorder, deliver, _) =>
            {
                var levels = new List<float>();
                recorder.LevelAvailable += (_, level) => levels.Add(level);

                deliver(Pcm(20000), 0); // empty callback
                typeof(AudioRecorder).GetField("isRecording", Priv)!.SetValue(recorder, false);
                deliver(Pcm(20000), 2); // late callback after stop

                levels.Should().BeEmpty("the bars must not move for audio that is not part of the recording");
            });
        }

        [Fact]
        public void OnDataAvailable_AThrowingSubscriber_DoesNotStopTheRecording()
        {
            WithFakeRecordingSession((recorder, deliver, stream) =>
            {
                recorder.LevelAvailable += (_, _) => throw new InvalidOperationException("pill broke");

                long before = stream.Length;
                var act = () => { deliver(Pcm(1, 2), 4); deliver(Pcm(3, 4), 4); };

                act.Should().NotThrow();
                recorder.IsRecording.Should().BeTrue("a broken level subscriber must not trip the write-failure path");
                (stream.Length - before).Should().Be(8);
            });
        }

        [Fact]
        public void OnDataAvailable_RaisesTheLevelOnlyAfterTheLockIsReleased()
        {
            // Source tripwire (fails when the source is missing - SourceTripwirePolicyTests): the
            // capture lock is also taken by StopRecording on the UI thread; a subscriber running
            // under it could stall the stop, or deadlock if it ever waited on the UI thread.
            var code = RepoSourceFile.ReadApp("Services", "AudioRecorder.cs").Replace("\r\n", "\n");
            var method = Regex.Match(code, @"private void OnDataAvailable\(object\? sender, WaveInEventArgs e\)\n        \{(?<body>[\s\S]*?)\n        \}\n");
            method.Success.Should().BeTrue("OnDataAvailable was renamed or reshaped - update this tripwire");
            var body = method.Groups["body"].Value;

            var lockBlock = Regex.Match(body, @"\n            lock \(lockObject\)\n            \{[\s\S]*?\n            \}\n");
            lockBlock.Success.Should().BeTrue();
            lockBlock.Value.Should().NotContain("LevelAvailable", "nothing about the level event may run under the capture lock");
            lockBlock.Value.Should().NotContain("ComputeBufferPeak");

            var afterLock = body.Substring(lockBlock.Index + lockBlock.Length);
            afterLock.Should().Contain("RaiseLevelAvailable(e.Buffer, e.BytesRecorded)");
        }
    }
}

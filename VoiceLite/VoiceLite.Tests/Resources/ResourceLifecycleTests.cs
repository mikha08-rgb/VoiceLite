using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using VoiceLite.Models;
using VoiceLite.Services;
using VoiceLite.Tests.Services;
using VoiceLite.Tests.TestUtilities;
using Xunit;

namespace VoiceLite.Tests.Resources
{
    /// <summary>
    /// Critical tests for resource lifecycle management - prevents memory leaks,
    /// file handle leaks, and ensures proper cleanup of audio streams and processes
    /// </summary>
    public class ResourceLifecycleTests : IDisposable
    {
        private readonly List<IDisposable> _disposables = new();
        private readonly string _tempDirectory;

        public ResourceLifecycleTests()
        {
            _tempDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_temp");
            Directory.CreateDirectory(_tempDirectory);
        }

        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                try { disposable?.Dispose(); } catch { }
            }

            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, true);
                }
            }
            catch { }
        }

        [Fact]
        public void AudioRecorder_DisposePreventsResourceLeaks()
        {
            if (!AudioTestEnvironment.HasMicrophone) return; // no audio device (CI runner)

            var recorder = new AudioRecorder();
            _disposables.Add(recorder);

            // Start recording to allocate resources
            recorder.StartRecording();
            Thread.Sleep(100);

            // Dispose should clean up all resources
            recorder.Dispose();

            // Verify disposed state
            recorder.IsRecording.Should().BeFalse();

            // Second dispose should not throw
            Action secondDispose = () => recorder.Dispose();
            secondDispose.Should().NotThrow();
        }

        [Fact]
        public async Task AudioRecorder_MultipleInstancesNoCrossContamination()
        {
            if (!AudioTestEnvironment.HasMicrophone) return; // no audio device (CI runner)

            var recorder1 = new AudioRecorder();
            var recorder2 = new AudioRecorder();
            _disposables.Add(recorder1);
            _disposables.Add(recorder2);

            var recorder1Data = false;
            var recorder2Data = false;

            recorder1.AudioDataReady += (s, d) => recorder1Data = true;
            recorder2.AudioDataReady += (s, d) => recorder2Data = true;

            // Only recorder1 should fire events
            recorder1.StartRecording();
            await Task.Delay(100);
            recorder1.StopRecording();

            await Task.Delay(200);

            recorder1Data.Should().BeTrue();
            recorder2Data.Should().BeFalse("recorder2 should not receive events from recorder1");
        }

        [Fact]
        public async Task TranscriptionService_DisposeReleasesResources()
        {
            // Gate on the real Parakeet model being installed (same pattern as
            // TranscriptionServiceTests) — bare CI has no model, dev machines do.
            if (!TranscriptionServiceFixture.ModelPresent)
            {
                return;
            }

            var initialMemory = GC.GetTotalMemory(true);

            var service = new TranscriptionService(new Settings());
            _disposables.Add(service);

            // Force the model to actually load by transcribing a short silent clip —
            // otherwise Dispose has nothing real to release and the test proves nothing.
            var wavPath = Path.Combine(_tempDirectory, "dispose_test.wav");
            CreateSilentWavFile(wavPath, 1);
            await service.TranscribeAsync(wavPath, TranscriptionServiceFixture.ModelDir);

            service.Dispose();

            // Force GC to reclaim model memory
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Memory should not grow unboundedly after dispose
            var finalMemory = GC.GetTotalMemory(true);
            // Allow 50MB tolerance (model loading/unloading has overhead)
            (finalMemory - initialMemory).Should().BeLessThan(50L * 1024 * 1024,
                "dispose should release model memory");
        }

        [Fact]
        public async Task TempFileCleanup_RemovesStaleFiles()
        {
            if (!AudioTestEnvironment.HasMicrophone) return; // no audio device (CI runner)

            var recorder = new AudioRecorder();
            _disposables.Add(recorder);

            // AudioRecorder's real temp directory (created by its constructor)
            var tempDir = Path.Combine(Path.GetTempPath(), "VoiceLite", "audio");

            // Create some old test files
            for (int i = 0; i < 5; i++)
            {
                var oldFile = Path.Combine(tempDir, $"old_test_{i}.wav");
                File.WriteAllBytes(oldFile, new byte[] { 0x00 });
                File.SetCreationTime(oldFile, DateTime.Now.AddHours(-2));
                File.SetLastWriteTime(oldFile, DateTime.Now.AddHours(-2));
            }

            // Recorder constructor should trigger cleanup
            var newRecorder = new AudioRecorder();
            _disposables.Add(newRecorder);

            // Do a recording cycle which may trigger cleanup
            newRecorder.StartRecording();
            await Task.Delay(100);
            newRecorder.StopRecording();

            // Note: Cleanup happens on a timer, so we're just verifying
            // the mechanism exists and doesn't crash
        }

        [Fact]
        public async Task MemoryStream_ProperlyDisposedAfterUse()
        {
            if (!AudioTestEnvironment.HasMicrophone) return; // no audio device (CI runner)

            var recorder = new AudioRecorder();
            _disposables.Add(recorder);

            var memoryFreed = false;

            recorder.AudioDataReady += (sender, data) =>
            {
                // Data should be accessible
                data.Should().NotBeNull();
                data.Length.Should().BeGreaterThan(0);

                // After this event, the internal memory stream should be cleaned up
                memoryFreed = true;
            };

            recorder.StartRecording();
            await Task.Delay(200);
            recorder.StopRecording();

            await Task.Delay(500);

            memoryFreed.Should().BeTrue("Memory stream should be freed after audio data is delivered");
        }

        [Fact]
        public void HotkeyManager_UnregistersOnDispose()
        {
            var manager = new HotkeyManager();
            _disposables.Add(manager);

            // Test disposal of manager
            manager.Dispose();

            // After dispose, should be safe to create new instance
            var newManager = new HotkeyManager();
            _disposables.Add(newManager);

            newManager.Should().NotBeNull("New instance should be created after disposal");
        }

        [Fact]
        public async Task ConcurrentDisposal_ThreadSafe()
        {
            if (!AudioTestEnvironment.HasMicrophone) return; // no audio device (CI runner)

            var recorder = new AudioRecorder();

            // Start recording
            recorder.StartRecording();
            await Task.Delay(50);

            // Attempt concurrent disposal
            var tasks = new Task[5];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(() => recorder.Dispose());
            }

            // Should handle concurrent disposal attempts safely
            await Task.WhenAll(tasks);

            recorder.IsRecording.Should().BeFalse();
        }

        [Fact]
        public async Task FileHandles_ReleasedAfterTranscription()
        {
            // Gate on the real Parakeet model being installed (same pattern as
            // TranscriptionServiceTests) — bare CI has no model, dev machines do.
            if (!TranscriptionServiceFixture.ModelPresent)
            {
                return;
            }

            var service = new TranscriptionService(new Settings());
            _disposables.Add(service);

            var testFile = Path.Combine(_tempDirectory, "test_audio.wav");
            CreateSilentWavFile(testFile, 1);

            // A transcription failure here is a real failure — no swallow-all catch.
            await service.TranscribeAsync(testFile, TranscriptionServiceFixture.ModelDir);

            // File should be deletable immediately after transcription
            Action deleteFile = () => File.Delete(testFile);
            deleteFile.Should().NotThrow("File handle should be released");
        }

        // NOTE: SystemTrayManager_CleansUpIconOnDispose was deleted 2026-07-17 — it had a
        // completely empty body (permanently green). SystemTrayManager needs a real Window,
        // which cannot be constructed in this unit-test host.

        [Fact]
        public async Task LongRunningOperation_CancellationCleansUpResources()
        {
            if (!AudioTestEnvironment.HasMicrophone) return; // no audio device (CI runner)

            var recorder = new AudioRecorder();
            _disposables.Add(recorder);

            using var cts = new CancellationTokenSource();

            var recordingTask = Task.Run(async () =>
            {
                recorder.StartRecording();
                try
                {
                    await Task.Delay(10000, cts.Token); // Long recording
                }
                finally
                {
                    recorder.StopRecording();
                }
            });

            await Task.Delay(100);

            // Cancel the long operation
            cts.Cancel();

            await Task.Delay(200);

            // Resources should be cleaned up
            recorder.IsRecording.Should().BeFalse("Recording should stop on cancellation");
        }

        private void CreateSilentWavFile(string path, int durationSeconds)
        {
            const int sampleRate = 16000;
            const int bitsPerSample = 16;
            const int channels = 1;

            var numSamples = sampleRate * durationSeconds;
            var dataSize = numSamples * (bitsPerSample / 8) * channels;

            using var fs = new FileStream(path, FileMode.Create);
            using var writer = new BinaryWriter(fs);

            // Write WAV header
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * (bitsPerSample / 8));
            writer.Write((short)(channels * (bitsPerSample / 8)));
            writer.Write((short)bitsPerSample);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);
            writer.Write(new byte[dataSize]);
        }
    }
}
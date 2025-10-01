using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NAudio.Wave;
using VoiceLite.Interfaces;

namespace VoiceLite.Services
{
    public class AudioDevice
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public override string ToString() => Name;
    }

    public class AudioRecorder : IRecorder, IDisposable
    {
        private WaveInEvent? waveIn;
        private WaveFileWriter? waveFile;
        private MemoryStream? audioMemoryStream; // Memory stream for buffering audio
        private readonly string tempDirectory;
        private string? currentAudioFilePath;
        private volatile bool isRecording; // volatile for thread-safe reads
        private int selectedDeviceIndex = -1; // -1 means default device
        private const float InputVolumeScale = 0.8f;
        private bool eventHandlersAttached = false;
        private readonly object lockObject = new object();
        private System.Timers.Timer? cleanupTimer;
        private const int CleanupIntervalMinutes = 30;
        private bool useMemoryBuffer = true; // Enable memory buffering by default

        // CRITICAL FIX: Instance tracking to prevent race conditions
        private int waveInInstanceId = 0;
        private int currentRecordingInstanceId = 0;


        public bool IsRecording => isRecording;
        public event EventHandler<string>? AudioFileReady;
        public event EventHandler<byte[]>? AudioDataReady; // New event for memory buffer mode


        public AudioRecorder()
        {
            // Use Windows temp folder instead of Program Files (fixes permissions issue)
            tempDirectory = Path.Combine(Path.GetTempPath(), "VoiceLite", "audio");
            if (!Directory.Exists(tempDirectory))
            {
                Directory.CreateDirectory(tempDirectory);
            }

            CleanupStaleAudioFiles();

            // Start periodic cleanup timer
            cleanupTimer = new System.Timers.Timer(CleanupIntervalMinutes * 60 * 1000);
            cleanupTimer.Elapsed += (s, e) => CleanupStaleAudioFiles();
            cleanupTimer.AutoReset = true;
            cleanupTimer.Start();
        }

        private void CleanupStaleAudioFiles()
        {
            try
            {
                if (!Directory.Exists(tempDirectory))
                    return;

                // Delete audio files older than 30 minutes to prevent accumulation
                var cutoffTime = DateTime.Now.AddMinutes(-30);
                var audioFiles = Directory.GetFiles(tempDirectory, "*.wav")
                    .Where(f => f != currentAudioFilePath); // Don't delete current file

                int deletedCount = 0;
                foreach (var file in audioFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.CreationTime < cutoffTime || fileInfo.LastWriteTime < cutoffTime)
                        {
                            File.Delete(file);
                            deletedCount++;
                        }
                    }
                    catch
                    {
                        // Ignore individual file deletion failures
                    }
                }

                if (deletedCount > 0)
                {
                    ErrorLogger.LogMessage($"Cleaned up {deletedCount} old audio files from temp directory");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("CleanupStaleAudioFiles", ex);
            }
        }

        /// <summary>
        /// CRITICAL METHOD: Completely disposes WaveInEvent and clears all internal buffers
        /// This is essential for preventing audio buffer leaks between recording sessions
        /// </summary>
        private void DisposeWaveInCompletely()
        {
            if (waveIn != null)
            {
                ErrorLogger.LogMessage("DisposeWaveInCompletely: CRITICAL - Disposing existing waveIn device to clear ALL buffers");

                // Detach handlers first to prevent late callbacks
                if (eventHandlersAttached)
                {
                    waveIn.DataAvailable -= OnDataAvailable;
                    waveIn.RecordingStopped -= OnRecordingStopped;
                    eventHandlersAttached = false;
                    ErrorLogger.LogMessage("DisposeWaveInCompletely: Event handlers detached");
                }

                // Stop recording if still active
                try
                {
                    if (waveIn.WaveFormat != null) // Check if device is initialized
                    {
                        waveIn.StopRecording();
                        Thread.Sleep(10); // Minimal delay for NAudio buffer flush
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogMessage($"DisposeWaveInCompletely: Error stopping recording - {ex.Message}");
                }

                // CRITICAL: Dispose the device completely to clear all internal NAudio buffers
                try
                {
                    waveIn.Dispose();
                    ErrorLogger.LogMessage("DisposeWaveInCompletely: WaveIn device disposed");
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogMessage($"DisposeWaveInCompletely: Error disposing waveIn - {ex.Message}");
                }

                waveIn = null;
                ErrorLogger.LogMessage("DisposeWaveInCompletely: WaveIn set to null - ALL buffers cleared");
            }

            // Also dispose any lingering wave file
            if (waveFile != null)
            {
                ErrorLogger.LogMessage("DisposeWaveInCompletely: Disposing lingering wave file");
                try
                {
                    waveFile.Dispose();
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogMessage($"DisposeWaveInCompletely: Error disposing wave file - {ex.Message}");
                }
                waveFile = null;
            }
        }

        /// <summary>
        /// Cleans up all recording state after errors to prevent partial state retention
        /// </summary>
        private void CleanupRecordingState()
        {
            try
            {
                waveFile?.Dispose();
                waveFile = null;

                if (eventHandlersAttached && waveIn != null)
                {
                    waveIn.DataAvailable -= OnDataAvailable;
                    waveIn.RecordingStopped -= OnRecordingStopped;
                    eventHandlersAttached = false;
                }

                waveIn?.Dispose();
                waveIn = null;

                ErrorLogger.LogMessage("CleanupRecordingState: All recording state cleaned up");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogMessage($"CleanupRecordingState: Error during cleanup - {ex.Message}");
            }
        }

        public static List<AudioDevice> GetAvailableMicrophones()
        {
            var devices = new List<AudioDevice>();
            int deviceCount = WaveInEvent.DeviceCount;

            if (deviceCount == 0)
                return devices;

            for (int i = 0; i < deviceCount; i++)
            {
                var capabilities = WaveInEvent.GetCapabilities(i);
                devices.Add(new AudioDevice
                {
                    Index = i,
                    Name = capabilities.ProductName
                });
            }

            return devices;
        }

        public static bool HasAnyMicrophone()
        {
            return WaveInEvent.DeviceCount > 0;
        }

        public void SetDevice(int deviceIndex)
        {
            if (isRecording)
                throw new InvalidOperationException("Cannot change device while recording");

            selectedDeviceIndex = deviceIndex;
        }

        public void StartRecording()
        {
            lock (lockObject)
            {
                if (isRecording) return;

                // Check if any microphone is available
                if (!HasAnyMicrophone())
                {
                    throw new InvalidOperationException("No microphone detected. Please connect a microphone and try again.");
                }

                // Validate selected device
                int deviceToUse = selectedDeviceIndex;
                if (deviceToUse >= WaveInEvent.DeviceCount)
                {
                    // Selected device no longer available, fall back to default
                    deviceToUse = 0;
                }

                try
                {
                    // CRITICAL FIX: ALWAYS dispose and recreate waveIn for EACH recording session
                    // This is the ONLY way to guarantee no buffered audio from previous sessions can leak
                    DisposeWaveInCompletely();

                    // CRITICAL: Verify complete state reset before creating new session
                    if (waveIn != null || waveFile != null || eventHandlersAttached)
                    {
                        ErrorLogger.LogWarning("StartRecording: Previous session not fully cleaned up!");
                        throw new InvalidOperationException("Previous recording session not fully cleaned up. Cannot start new session.");
                    }

                    // CRITICAL FIX: Increment instance ID to track this new recording session
                    waveInInstanceId++;
                    currentRecordingInstanceId = waveInInstanceId;
                    ErrorLogger.LogDebug($"StartRecording: New recording instance ID: {currentRecordingInstanceId}");

                    waveIn = new WaveInEvent
                    {
                        WaveFormat = new WaveFormat(16000, 16, 1),
                        BufferMilliseconds = 30,   // Optimized for low latency
                        NumberOfBuffers = 3,       // Balance between latency and stability
                        DeviceNumber = deviceToUse >= 0 ? deviceToUse : 0
                    };

                    // Attach event handlers to the FRESH device
                    waveIn.DataAvailable += OnDataAvailable;
                    waveIn.RecordingStopped += OnRecordingStopped;
                    eventHandlersAttached = true;

                    if (useMemoryBuffer)
                    {
                        // Use memory stream for zero file I/O
                        audioMemoryStream = new MemoryStream();
                        waveFile = new WaveFileWriter(audioMemoryStream, waveIn.WaveFormat);
                        ErrorLogger.LogDebug("StartRecording: Using memory buffer mode");
                    }
                    else
                    {
                        // Fallback to file-based recording
                        string guidPart = Guid.NewGuid().ToString("N")[..8];
                        currentAudioFilePath = Path.Combine(tempDirectory, $"recording_{DateTime.Now:yyyyMMddHHmmssfff}_{guidPart}.wav");
                        waveFile = new WaveFileWriter(currentAudioFilePath, waveIn.WaveFormat);
                        ErrorLogger.LogDebug($"StartRecording: File mode - {currentAudioFilePath}");
                    }

                    // Start recording - this creates a completely fresh audio capture session
                    waveIn.StartRecording();
                    isRecording = true;
                    ErrorLogger.LogInfo("Recording session started");
                }
                catch (Exception ex)
                {
                    // Clean up on error - ensure no partial state remains
                    CleanupRecordingState();
                    isRecording = false;

                    if (ex.Message.Contains("device") || ex.Message.Contains("audio"))
                    {
                        throw new InvalidOperationException("Failed to access the microphone. Please check if another application is using it.", ex);
                    }
                    throw;
                }
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            // CRITICAL FIX: Capture the instance ID at entry to detect stale callbacks
            int callbackInstanceId;

            // Quick pre-lock check for obvious late callbacks
            if (!isRecording)
            {
                // Silently discard late audio data - this is normal when stopping
                return;
            }

            // Additional safety check in lock to prevent race conditions
            lock (lockObject)
            {
                // Re-check after acquiring lock - state might have changed
                if (!isRecording)
                    return;

                // CRITICAL FIX: Capture current recording instance ID under lock
                callbackInstanceId = currentRecordingInstanceId;

                // ENHANCED SAFETY: Check if sender is from an old waveIn instance using instance ID
                if (sender is WaveInEvent senderWaveIn)
                {
                    // If sender is not the current waveIn instance, reject it
                    if (senderWaveIn != waveIn)
                    {
                        ErrorLogger.LogDebug($"OnDataAvailable: Ignoring {e.BytesRecorded} bytes from old waveIn instance");
                        return;
                    }
                }
                else
                {
                    // Sender is not a WaveInEvent? This should never happen
                    ErrorLogger.LogWarning($"OnDataAvailable: Unexpected sender type: {sender?.GetType().Name ?? "null"}");
                    return;
                }

                try
                {
                    // Triple-check all conditions with defensive programming
                    if (waveFile != null && isRecording && e.BytesRecorded > 0)
                    {
                        // Apply volume scaling (InputVolumeScale is const 0.8f)
                        // CRITICAL FIX: Use ArrayPool to prevent memory allocation on every callback
                        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(e.BytesRecorded);
                        try
                        {
                            Array.Copy(e.Buffer, buffer, e.BytesRecorded);

                            for (int i = 0; i < e.BytesRecorded; i += 2)
                            {
                                short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
                                int scaled = (int)Math.Round(sample * InputVolumeScale);
                                scaled = Math.Clamp(scaled, short.MinValue, short.MaxValue);
                                short clamped = (short)scaled;
                                buffer[i] = (byte)(clamped & 0xFF);
                                buffer[i + 1] = (byte)((clamped >> 8) & 0xFF);
                            }
                            waveFile.Write(buffer, 0, e.BytesRecorded);
                        }
                        finally
                        {
                            // CRITICAL: Return buffer to pool to prevent memory leak
                            System.Buffers.ArrayPool<byte>.Shared.Return(buffer, clearArray: false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("OnDataAvailable: Audio write failed", ex);
                    // Mic disconnected or error - stop this session gracefully
                    StopRecording();
                }
            }
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            // NOTE: This event may not fire if we disposed the device immediately in StopRecording()
            // This is intentional - we want immediate cleanup, not event-based cleanup
            ErrorLogger.LogMessage($"OnRecordingStopped: Event fired at {DateTime.Now:HH:mm:ss.fff} - cleanup already handled in StopRecording()");

            try
            {
                // Cleanup is already handled in StopRecording() for immediate effect
                // This event handler is just for logging/debugging purposes now
                lock (lockObject)
                {
                    ErrorLogger.LogMessage($"OnRecordingStopped: Current state - isRecording={isRecording}, waveIn={(waveIn == null ? "null" : "exists")}, waveFile={(waveFile == null ? "null" : "exists")}");

                    // Defensive cleanup in case StopRecording() didn't handle everything
                    if (waveFile != null)
                    {
                        try
                        {
                            waveFile.Dispose();
                        }
                        catch { }
                        waveFile = null;
                        ErrorLogger.LogMessage("OnRecordingStopped: Defensive wave file cleanup");
                    }

                    if (waveIn != null)
                    {
                        ErrorLogger.LogMessage("OnRecordingStopped: Defensive waveIn cleanup - this should not happen");
                        DisposeWaveInCompletely();
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("OnRecordingStopped", ex);
            }
        }


        public void StopRecording()
        {
            lock (lockObject)
            {
                if (!isRecording) return;

                ErrorLogger.LogMessage($"StopRecording: CRITICAL - Stopping session at {DateTime.Now:HH:mm:ss.fff}");

                // CRITICAL FIX: Set isRecording to false IMMEDIATELY to reject any incoming data
                isRecording = false;
                ErrorLogger.LogMessage("StopRecording: isRecording=false set - will IMMEDIATELY reject all incoming data");

                string? audioFileToNotify = currentAudioFilePath;

                // Immediately close the wave file to stop accepting any more data for this session
                if (waveFile != null)
                {
                    ErrorLogger.LogMessage("StopRecording: Flushing and closing wave file IMMEDIATELY for this session");
                    try
                    {
                        waveFile.Flush();

                        // If using memory buffer, extract the audio data before disposing
                        if (useMemoryBuffer && audioMemoryStream != null)
                        {
                            waveFile.Dispose(); // Must dispose to finalize WAV headers
                            waveFile = null;

                            // Get the complete WAV data from memory
                            var audioData = audioMemoryStream.ToArray();

                            // CRITICAL: Dispose memory stream immediately after getting data
                            try
                            {
                                audioMemoryStream.Dispose();
                            }
                            catch { }
                            finally
                            {
                                audioMemoryStream = null;
                            }

                            if (audioData.Length > 100) // Only process if there's actual audio
                            {
                                ErrorLogger.LogMessage($"StopRecording: Memory buffer contains {audioData.Length} bytes");

                                // Notify listeners with the audio data
                                AudioDataReady?.Invoke(this, audioData);

                                // Optionally save to temp file for compatibility with existing code
                                if (AudioFileReady != null)
                                {
                                    SaveMemoryBufferToTempFile(audioData);
                                }
                            }
                            else
                            {
                                ErrorLogger.LogMessage("StopRecording: Skipping empty memory buffer");
                            }
                        }
                        else
                        {
                            // File-based recording
                            waveFile.Dispose(); // CRITICAL: Dispose immediately, don't wait for event
                            waveFile = null;
                            ErrorLogger.LogMessage("StopRecording: Wave file disposed immediately");
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogMessage($"StopRecording: Error disposing wave file - {ex.Message}");

                        // CRITICAL: Ensure cleanup even on error
                        waveFile = null;
                        if (audioMemoryStream != null)
                        {
                            try { audioMemoryStream.Dispose(); } catch { }
                            audioMemoryStream = null;
                        }
                    }
                }

                // CRITICAL: Immediately dispose the audio device to stop ALL capture
                // Don't wait for OnRecordingStopped event - do it NOW
                if (waveIn != null)
                {
                    ErrorLogger.LogMessage("StopRecording: IMMEDIATELY disposing waveIn device to stop ALL audio capture");
                    try
                    {
                        waveIn.StopRecording();
                        Thread.Sleep(10); // Brief pause to let stop complete

                        // Detach handlers before disposal
                        if (eventHandlersAttached)
                        {
                            waveIn.DataAvailable -= OnDataAvailable;
                            waveIn.RecordingStopped -= OnRecordingStopped;
                            eventHandlersAttached = false;
                            ErrorLogger.LogMessage("StopRecording: Event handlers detached immediately");
                        }

                        waveIn.Dispose();
                        waveIn = null;
                        ErrorLogger.LogMessage("StopRecording: WaveIn device disposed IMMEDIATELY - NO MORE AUDIO CAPTURE");
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogMessage($"StopRecording: Error disposing waveIn - {ex.Message}");
                        // Force cleanup even if there's an error
                        waveIn = null;
                        eventHandlersAttached = false;
                    }
                }

                // Clear the current path immediately
                currentAudioFilePath = null;

                // Notify about the completed audio file if it's valid
                if (!string.IsNullOrEmpty(audioFileToNotify) && File.Exists(audioFileToNotify))
                {
                    var fileInfo = new FileInfo(audioFileToNotify);
                    if (fileInfo.Length > 100) // Only process files with actual audio content
                    {
                        ErrorLogger.LogMessage($"StopRecording: Immediately notifying about completed file - {audioFileToNotify}");
                        AudioFileReady?.Invoke(this, audioFileToNotify);
                    }
                    else
                    {
                        ErrorLogger.LogMessage($"StopRecording: Skipping empty recording - {audioFileToNotify} (size: {fileInfo.Length} bytes)");
                        try { File.Delete(audioFileToNotify); } catch { }
                    }
                }

                ErrorLogger.LogMessage("StopRecording: COMPLETE - All audio capture stopped immediately, no waiting for events");
            }
        }

        private void SaveMemoryBufferToTempFile(byte[] audioData)
        {
            try
            {
                string guidPart = Guid.NewGuid().ToString("N")[..8];
                currentAudioFilePath = Path.Combine(tempDirectory, $"recording_{DateTime.Now:yyyyMMddHHmmssfff}_{guidPart}.wav");

                // Write the complete WAV data to file
                File.WriteAllBytes(currentAudioFilePath, audioData);
                ErrorLogger.LogMessage($"SaveMemoryBufferToTempFile: Saved {audioData.Length} bytes to {currentAudioFilePath}");

                // Notify about the file
                AudioFileReady?.Invoke(this, currentAudioFilePath);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("SaveMemoryBufferToTempFile failed", ex);
            }
        }

        public void SetMemoryBufferMode(bool useMemory)
        {
            if (!isRecording)
            {
                useMemoryBuffer = useMemory;
                ErrorLogger.LogMessage($"Memory buffer mode set to: {useMemory}");
            }
        }

        public void Dispose()
        {
            lock (lockObject)
            {
                // Stop cleanup timer
                cleanupTimer?.Stop();
                cleanupTimer?.Dispose();
                cleanupTimer = null;

                if (isRecording)
                {
                    isRecording = false;
                    waveIn?.StopRecording();
                }

                // Minimal delay for cleanup
                Thread.Sleep(10);

                waveFile?.Dispose();
                waveFile = null;

                if (eventHandlersAttached && waveIn != null)
                {
                    waveIn.DataAvailable -= OnDataAvailable;
                    waveIn.RecordingStopped -= OnRecordingStopped;
                    eventHandlersAttached = false;
                }

                waveIn?.Dispose();
                waveIn = null;

                // Final cleanup
                CleanupStaleAudioFiles();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using VoiceLite.Core.Interfaces.Services;
using VoiceLite.Services.Audio;

namespace VoiceLite.Services
{
    public class AudioDevice
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public override string ToString() => Name;
    }

    public class AudioRecorder : IAudioRecorder
    {
        private WaveInEvent? waveIn;
        private WaveFileWriter? waveFile;
        private MemoryStream? audioMemoryStream; // Memory stream for buffering audio
        private readonly string tempDirectory;
        private string? currentAudioFilePath;
        private volatile bool isRecording; // volatile for thread-safe reads
        private int selectedDeviceIndex = -1; // -1 means default device
        private bool eventHandlersAttached = false;
        private readonly object lockObject = new object();
        private System.Timers.Timer? cleanupTimer;
        private const int CleanupIntervalMinutes = 30;

        // BUG FIX (DEAD-CODE-002): Removed useMemoryBuffer constant - memory mode is now permanent
        // All file-mode code paths have been removed - memory buffering is the only implementation
        // This eliminates dead code and makes the codebase clearer
        private volatile bool isDisposed = false; // DISPOSAL SAFETY: Prevents cleanup timer from running after disposal
        // MED-3 FIX: Session ID for rejecting stale callbacks
        // Note: int wraps around at int.MaxValue (2^31), which is acceptable since we only need uniqueness
        // vs previous session, not monotonicity. 2 billion recordings before wrap is practically infinite.
        // volatile long not allowed in C# (64-bit not atomic on 32-bit systems)
        private volatile int currentSessionId = 0;

        // Audio preprocessing settings (enabled by default for better transcription quality)
        private AudioPreprocessingSettings preprocessingSettings = new AudioPreprocessingSettings();

        // Timing constants (in milliseconds)
        private const int NAUDIO_BUFFER_FLUSH_DELAY_MS = 10; // Minimal delay for NAudio buffer flush
        private const int STOP_COMPLETION_DELAY_MS = 10; // Brief pause to let stop complete

        public bool IsRecording => isRecording;
        public event EventHandler<string>? AudioFileReady;
        public event EventHandler<byte[]>? AudioDataReady; // New event for memory buffer mode
        public event EventHandler<Exception>? RecordingError; // Required by IAudioRecorder

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
            // DISPOSAL SAFETY: Early exit if disposed
            if (isDisposed)
                return;

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
                    catch (Exception deleteEx)
                    {
                        // Log but don't fail cleanup for individual file errors
                        ErrorLogger.LogWarning($"Failed to delete old audio file {file}: {deleteEx.Message}");
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
                        // HIGH-3 FIX: Use Thread.Sleep instead of Task.Delay().Wait() for sync code
                        // Task.Delay creates unnecessary Task overhead when blocking synchronously
                        Thread.Sleep(NAUDIO_BUFFER_FLUSH_DELAY_MS);
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("DisposeWaveInCompletely: Error stopping recording", ex);
                }

                // CRITICAL: Dispose the device completely to clear all internal NAudio buffers
                try
                {
                    waveIn.Dispose();
                    ErrorLogger.LogMessage("DisposeWaveInCompletely: WaveIn device disposed");
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("DisposeWaveInCompletely: Error disposing waveIn", ex);
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
                    ErrorLogger.LogError("DisposeWaveInCompletely: Error disposing wave file", ex);
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
                ErrorLogger.LogError("CleanupRecordingState: Error during cleanup", ex);
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

        /// <summary>
        /// Configures audio preprocessing settings. Must be called before StartRecording().
        /// </summary>
        /// <param name="settings">Preprocessing configuration (null to use defaults)</param>
        public void SetPreprocessingSettings(AudioPreprocessingSettings? settings)
        {
            if (isRecording)
                throw new InvalidOperationException("Cannot change preprocessing settings while recording");

            preprocessingSettings = settings ?? new AudioPreprocessingSettings();
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

                    // CRITICAL FIX #1: Increment session ID to invalidate any stale callbacks
                    currentSessionId++;
                    int sessionId = currentSessionId;

                    // Release-visible log for troubleshooting recording issues
                    ErrorLogger.LogWarning($"StartRecording: session #{sessionId}, device={deviceToUse}");

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

                    // QUICK WIN 1: Memory buffer mode is now enforced (useMemoryBuffer = const true)
                    // File mode code paths removed to eliminate dead code warnings
                    audioMemoryStream = new MemoryStream();
                    waveFile = new WaveFileWriter(audioMemoryStream, waveIn.WaveFormat);
                    ErrorLogger.LogDebug($"StartRecording: Using memory buffer mode (session #{sessionId})");

                    // Start recording - this creates a completely fresh audio capture session
                    waveIn.StartRecording();
                    isRecording = true;
                    ErrorLogger.LogInfo($"Recording session #{sessionId} started");
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
            // CRIT-2 FIX: Capture session ID BEFORE acquiring lock
            // If another thread starts a new session while we wait for the lock,
            // we'll detect the mismatch and reject this stale callback
            int callbackSessionId = currentSessionId;

            // BUG FIX (RACE-001): Hold lock for entire method to prevent TOCTOU vulnerabilities
            // Previous code had pre-lock check that created race condition window
            // Now all validation happens atomically inside single lock block
            lock (lockObject)
            {
                // CRIT-2 FIX: Compare captured session ID against current
                // If session changed while waiting for lock, this callback is stale
                if (callbackSessionId != currentSessionId)
                {
                    ErrorLogger.LogDebug($"OnDataAvailable: Rejected stale callback from session #{callbackSessionId} (current: #{currentSessionId})");
                    return;
                }

                // Check recording state - must be inside lock to prevent race
                if (!isRecording)
                {
                    // Silently discard late audio data - this is normal when stopping
                    return;
                }

                // Validate sender is current waveIn instance (prevents stale callbacks)
                if (sender is WaveInEvent senderWaveIn && senderWaveIn != waveIn)
                {
                    // Ignore callbacks from disposed instances
                    return;
                }

                // Validate audio data size
                if (e.BytesRecorded <= 0)
                {
                    return; // No data to write
                }

                try
                {
                    // BUG FIX (BUG-012): Capture waveFile reference atomically under lock
                    WaveFileWriter? localWaveFile = waveFile;
                    if (localWaveFile == null)
                    {
                        return; // Exit early if disposed
                    }

                    // Write raw audio data directly (no volume scaling - AGC will handle normalization)
                    // This provides cleaner input for audio preprocessing pipeline
                    localWaveFile.Write(e.Buffer, 0, e.BytesRecorded);
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("OnDataAvailable: Audio write failed", ex);
                    // Mic disconnected or error - stop this session gracefully
                    // NOTE: StopRecording will try to acquire lock, but we already hold it
                    // This could cause deadlock - need to handle differently
                    try
                    {
                        // Set flag to stop without calling StopRecording (avoid deadlock)
                        isRecording = false;
                        ErrorLogger.LogMessage("OnDataAvailable: Recording stopped due to write error");
                    }
                    catch (Exception stopEx)
                    {
                        ErrorLogger.LogError("OnDataAvailable: Failed to stop recording after error", stopEx);
                    }
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
                        catch (Exception disposeEx)
                        {
                            ErrorLogger.LogWarning($"OnRecordingStopped: Failed to dispose wave file: {disposeEx.Message}");
                        }
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

                int stoppingSessionId = currentSessionId;
                ErrorLogger.LogMessage($"StopRecording: CRITICAL - Stopping session #{stoppingSessionId} at {DateTime.Now:HH:mm:ss.fff}");

                // CRITICAL FIX: Set isRecording to false IMMEDIATELY to reject any incoming data
                isRecording = false;
                ErrorLogger.LogMessage($"StopRecording: isRecording=false set for session #{stoppingSessionId} - will IMMEDIATELY reject all incoming data");

                string? audioFileToNotify = currentAudioFilePath;

                // Immediately close the wave file to stop accepting any more data for this session
                if (waveFile != null)
                {
                    ErrorLogger.LogMessage("StopRecording: Flushing and closing wave file IMMEDIATELY for this session");
                    try
                    {
                        waveFile.Flush();

                        // QUICK WIN 1: Memory buffer is always used (const true)
                        if (audioMemoryStream != null)
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
                            catch (Exception memEx)
                            {
                                ErrorLogger.LogWarning($"Failed to dispose audio memory stream: {memEx.Message}");
                            }
                            finally
                            {
                                audioMemoryStream = null;
                            }

                            // CRITICAL: Log at WARNING level so it shows in Release builds
                            ErrorLogger.LogWarning($"StopRecording: Memory buffer contains {audioData.Length} bytes");

                            if (audioData.Length > 100) // Only process if there's actual audio
                            {
                                ErrorLogger.LogWarning($"StopRecording: CALLING SaveMemoryBufferToTempFile with {audioData.Length} bytes");

                                // Notify listeners with the audio data
                                AudioDataReady?.Invoke(this, audioData);

                                // CRITICAL FIX: Always save to temp file for transcription
                                // Don't depend on event subscribers - the file is needed regardless
                                SaveMemoryBufferToTempFile(audioData);

                                ErrorLogger.LogWarning("StopRecording: SaveMemoryBufferToTempFile COMPLETED");
                            }
                            else
                            {
                                ErrorLogger.LogWarning($"StopRecording: Skipping TINY/EMPTY buffer - only {audioData.Length} bytes!");
                            }
                        }
                        // QUICK WIN 1: File-based recording code removed (memory mode is enforced)
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogError("StopRecording: Error disposing wave file", ex);

                        // CRITICAL: Ensure cleanup even on error
                        waveFile = null;
                        if (audioMemoryStream != null)
                        {
                            try { audioMemoryStream.Dispose(); } catch (Exception streamEx) { ErrorLogger.LogError("Failed to dispose memory stream in error handler", streamEx); }
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
                        // Brief pause for NAudio to complete stop operation
                        Task.Delay(STOP_COMPLETION_DELAY_MS).Wait();

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
                        ErrorLogger.LogError("StopRecording: Error disposing waveIn", ex);
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
                        try { File.Delete(audioFileToNotify); } catch (Exception delEx) { ErrorLogger.LogError("Failed to delete empty audio file", delEx); }
                    }
                }

                ErrorLogger.LogMessage("StopRecording: COMPLETE - All audio capture stopped immediately, no waiting for events");
            }
        }

        private void SaveMemoryBufferToTempFile(byte[] audioData)
        {
            ErrorLogger.LogWarning($"SaveMemoryBufferToTempFile: ENTERED with {audioData.Length} bytes");
            try
            {
                // Ensure temp directory exists (Windows temp cleanup might delete it while app is running)
                if (!Directory.Exists(tempDirectory))
                {
                    ErrorLogger.LogWarning($"SaveMemoryBufferToTempFile: Temp directory was deleted, recreating: {tempDirectory}");
                    Directory.CreateDirectory(tempDirectory);
                }

                string guidPart = Guid.NewGuid().ToString("N")[..8];
                currentAudioFilePath = Path.Combine(tempDirectory, $"recording_{DateTime.Now:yyyyMMddHHmmssfff}_{guidPart}.wav");

                ErrorLogger.LogWarning($"SaveMemoryBufferToTempFile: About to write {audioData.Length} bytes to {currentAudioFilePath}");

                // Apply audio preprocessing if any features are enabled
                byte[] processedAudioData = audioData;
                if (preprocessingSettings.EnableHighPassFilter ||
                    preprocessingSettings.EnableAGC ||
                    preprocessingSettings.EnableNoiseGate)
                {
                    try
                    {
                        ErrorLogger.LogDebug("SaveMemoryBufferToTempFile: Applying audio preprocessing...");
                        processedAudioData = AudioPreprocessor.ProcessAudioData(audioData, preprocessingSettings);
                        ErrorLogger.LogDebug($"SaveMemoryBufferToTempFile: Preprocessing complete - {processedAudioData.Length} bytes");
                    }
                    catch (Exception preprocessEx)
                    {
                        // Fallback to unprocessed audio if preprocessing fails
                        ErrorLogger.LogWarning($"SaveMemoryBufferToTempFile: Preprocessing failed, using raw audio: {preprocessEx.Message}");
                        processedAudioData = audioData;
                    }
                }

                // Write the complete WAV data to file
                File.WriteAllBytes(currentAudioFilePath, processedAudioData);

                ErrorLogger.LogWarning($"SaveMemoryBufferToTempFile: SUCCESS - Saved {processedAudioData.Length} bytes to {currentAudioFilePath}");

                // Notify about the file
                AudioFileReady?.Invoke(this, currentAudioFilePath);

                ErrorLogger.LogWarning($"SaveMemoryBufferToTempFile: AudioFileReady event invoked");
            }
            catch (Exception ex)
            {
                // BUG FIX (BUG-005): Log warning for memory buffer save failure
                // This is best-effort operation - audio data will be lost but app continues
                ErrorLogger.LogError("SaveMemoryBufferToTempFile failed", ex);
                ErrorLogger.LogMessage($"WARNING: Audio data ({audioData.Length} bytes) will be lost due to disk I/O failure. This is expected behavior for best-effort memory buffer mode.");

                // MED-8 FIX: Clear stale path reference on error
                // currentAudioFilePath was set before File.WriteAllBytes - clear it since file may not exist
                currentAudioFilePath = null;
            }
        }

        // QUICK WIN 1: SetMemoryBufferMode removed - memory mode is now enforced (const = true)
        // This eliminates 50-100ms file I/O latency per recording session

        // Interface implementation methods
        public async Task<string> GetLastAudioFileAsync()
        {
            return await Task.Run(() =>
            {
                if (!string.IsNullOrEmpty(currentAudioFilePath) && File.Exists(currentAudioFilePath))
                {
                    return currentAudioFilePath;
                }

                // Find the most recent audio file in temp directory
                var directory = new DirectoryInfo(tempDirectory);
                var latestFile = directory.GetFiles("*.wav")
                    .OrderByDescending(f => f.CreationTime)
                    .FirstOrDefault();

                return latestFile?.FullName ?? string.Empty;
            });
        }

        public bool ValidateAudioSystem()
        {
            try
            {
                // Check if any recording devices are available
                int deviceCount = WaveInEvent.DeviceCount;
                if (deviceCount == 0)
                {
                    RecordingError?.Invoke(this, new InvalidOperationException("No audio recording devices found"));
                    return false;
                }

                // Test creating a WaveInEvent
                using (var testWaveIn = new WaveInEvent())
                {
                    testWaveIn.WaveFormat = new WaveFormat(16000, 16, 1);
                    return true;
                }
            }
            catch (Exception ex)
            {
                RecordingError?.Invoke(this, ex);
                return false;
            }
        }

        public void Dispose()
        {
            // MED-4 FIX: Stop timer BEFORE acquiring lock to prevent callback race condition
            // Timer callback could be waiting for lock when we try to dispose
            // Stopping first ensures no new callbacks are queued while we wait
            if (cleanupTimer != null)
            {
                try
                {
                    cleanupTimer.Stop(); // Prevents new callbacks from being queued
                }
                catch (ObjectDisposedException)
                {
                    // Timer already disposed, ignore
                }
            }

            // CRIT-007 FIX: Hold lock for entire disposal sequence to prevent TOCTOU race condition
            // Previously checked isDisposed, then performed disposal logic outside lock
            // This caused race conditions where multiple threads could enter disposal logic
            lock (lockObject)
            {
                // Check-and-set must be atomic (within same lock acquisition)
                if (isDisposed)
                    return;

                // Mark as disposed FIRST while holding lock
                isDisposed = true;

                // Dispose timer (already stopped above)
                if (cleanupTimer != null)
                {
                    try
                    {
                        cleanupTimer.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Timer already disposed, ignore
                    }
                    cleanupTimer = null;
                }

                // Stop recording if active
                if (isRecording)
                {
                    isRecording = false;
                    waveIn?.StopRecording();
                }

                // Minimal delay for cleanup - allow NAudio to finalize state
                Task.Delay(10).Wait();

                // Dispose wave file
                waveFile?.Dispose();
                waveFile = null;

                // Detach event handlers
                if (eventHandlersAttached && waveIn != null)
                {
                    waveIn.DataAvailable -= OnDataAvailable;
                    waveIn.RecordingStopped -= OnRecordingStopped;
                    eventHandlersAttached = false;
                }

                // Dispose wave input device
                waveIn?.Dispose();
                waveIn = null;

                // Final cleanup (safe - already marked disposed)
                CleanupStaleAudioFiles();
            }
        }
    }
}

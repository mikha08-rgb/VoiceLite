using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VoiceLite.Interfaces;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    /// <summary>
    /// Whisper transcription service using persistent server.exe for 5x faster performance.
    /// Falls back to PersistentWhisperService if server fails to start.
    /// </summary>
    public class WhisperServerService : ITranscriber, IDisposable
    {
        private readonly Settings settings;
        private readonly string baseDir;
        private Process? serverProcess;
        private HttpClient? httpClient;
        private readonly int serverPort;
        private readonly ITranscriber fallbackService;
        private bool isServerRunning = false;
        private bool isDisposed = false;
        private readonly object httpClientLock = new object();
        private readonly CancellationTokenSource disposeCts = new CancellationTokenSource();

        public WhisperServerService(Settings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // Find free port (8080-8090 range)
            this.serverPort = FindFreePort(8080, 8090);

            // Create fallback service for reliability
            this.fallbackService = new PersistentWhisperService(settings);
        }

        public async Task InitializeAsync()
        {
            if (!settings.UseWhisperServer)
            {
                ErrorLogger.LogMessage("WhisperServer disabled in settings - using fallback");
                return;
            }

            // PERFORMANCE FIX: Check if server already running and healthy (singleton pattern)
            // Avoids unnecessary restarts and improves reliability
            if (await IsServerHealthy())
            {
                ErrorLogger.LogMessage("WhisperServer already running and healthy - reusing existing instance");
                isServerRunning = true;
                return; // Reuse existing server
            }

            try
            {
                await StartServerAsync().ConfigureAwait(false);
                isServerRunning = true;
                ErrorLogger.LogMessage($"Whisper server started successfully on port {serverPort}");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to start Whisper server - using fallback", ex);
                isServerRunning = false;
            }
        }

        public async Task<string> TranscribeAsync(string audioFilePath)
        {
            string result;

            // Use server if running, otherwise fallback
            if (isServerRunning)
            {
                try
                {
                    result = await TranscribeViaServerAsync(audioFilePath).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Server transcription failed - falling back to process mode", ex);
                    isServerRunning = false;
                    return await fallbackService.TranscribeAsync(audioFilePath).ConfigureAwait(false);
                }
            }
            else
            {
                return await fallbackService.TranscribeAsync(audioFilePath).ConfigureAwait(false);
            }

            // CRITICAL FIX: Apply post-processing (VoiceShortcuts + Text Formatting)
            // This was missing, causing all post-processing features to be skipped in server mode!
            var customDict = settings.EnableCustomDictionary ? settings.CustomDictionaryEntries : null;
            result = TranscriptionPostProcessor.ProcessTranscription(result, settings.UseEnhancedDictionary, customDict, settings.PostProcessing);

            return result;
        }

        private async Task StartServerAsync()
        {
            // CRITICAL FIX: Kill any zombie server.exe processes before starting new one
            // This prevents multiple servers from fighting over resources and deadlocking
            KillExistingServers();

            var serverExePath = Path.Combine(baseDir, "whisper", "server.exe");
            var modelPath = ResolveModelPath();

            if (!File.Exists(serverExePath))
                throw new FileNotFoundException($"server.exe not found: {serverExePath}");

            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"Model file not found: {modelPath}");

            var arguments = $"-m \"{modelPath}\" " +
                          $"--host 127.0.0.1 " +
                          $"--port {serverPort} " +
                          $"--threads {Environment.ProcessorCount} " +
                          $"--processors 1 " +
                          $"-l {settings.Language} " +
                          $"--beam-size {settings.BeamSize} " +
                          $"--best-of {settings.BestOf} " +
                          $"--no-fallback"; // Disable temperature fallback for ~10-15% speed boost

            var processStartInfo = new ProcessStartInfo
            {
                FileName = serverExePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            serverProcess = new Process { StartInfo = processStartInfo };
            serverProcess.Start();

            // Set high priority for faster processing
            try
            {
                serverProcess.PriorityClass = ProcessPriorityClass.High;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogMessage($"Failed to set Whisper server process priority: {ex.Message}");
            }

            // BUG FIX (BUG-002): Ensure HttpClient is always disposed on failure
            // Use explicit disposal pattern to prevent resource leaks
            HttpClient? tempClient = null;
            bool clientOwnershipTransferred = false;

            try
            {
                tempClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{serverPort}") };
                // CRITICAL FIX: Use 120s timeout for transcription (was 3s, caused timeouts on longer audio)
                // Health checks use separate 1s timeout via CancellationToken (line 167)
                tempClient.Timeout = TimeSpan.FromSeconds(120);

                // Overall timeout to prevent infinite waiting
                using var overallTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                // Give server 3 seconds to start and bind port (6 retries × 500ms = 3s)
                for (int i = 0; i < 6; i++)
                {
                    // Check overall timeout first
                    if (overallTimeout.Token.IsCancellationRequested)
                        throw new TimeoutException("Server startup exceeded 5 second hard limit");

                    await Task.Delay(500, overallTimeout.Token).ConfigureAwait(false);

                    // Check if process crashed
                    if (serverProcess.HasExited)
                        throw new InvalidOperationException($"Server process exited with code {serverProcess.ExitCode}");

                    // Try a simple GET request to root endpoint
                    try
                    {
                        using var requestTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                        var response = await tempClient.GetAsync("/", requestTimeout.Token).ConfigureAwait(false);
                        // Any response (even 404) means server is listening
                        ErrorLogger.LogMessage("Whisper server is ready");

                        // Success - transfer ownership to class field
                        httpClient = tempClient;
                        clientOwnershipTransferred = true;
                        return;
                    }
                    catch (HttpRequestException)
                    {
                        // Server not ready yet, continue waiting
                    }
                    catch (OperationCanceledException)
                    {
                        // Request timeout, continue waiting
                    }
                }

                throw new TimeoutException("Server failed to respond within 3 seconds");
            }
            catch (OperationCanceledException)
            {
                // Overall timeout reached
                throw new TimeoutException("Server startup timed out after 5 seconds");
            }
            finally
            {
                // Clean up temporary client only if ownership was NOT transferred
                if (!clientOwnershipTransferred && tempClient != null)
                {
                    try
                    {
                        tempClient.Dispose();
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogError("Failed to dispose temporary HttpClient", ex);
                    }
                }
            }
        }

        private async Task<string> TranscribeViaServerAsync(string audioFilePath)
        {
            // CRIT-002 FIX: Capture httpClient reference under lock to prevent disposal race
            HttpClient? client;
            lock (httpClientLock)
            {
                if (isDisposed)
                    throw new ObjectDisposedException(nameof(WhisperServerService));

                client = httpClient;
                if (client == null)
                    throw new InvalidOperationException("Server not initialized");
            }

            using var audioStream = File.OpenRead(audioFilePath);
            using var content = new MultipartFormDataContent();

            var fileContent = new StreamContent(audioStream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
            content.Add(fileContent, "file", Path.GetFileName(audioFilePath));

            // Add transcription parameters
            content.Add(new StringContent("true"), "no_timestamps");
            content.Add(new StringContent(settings.Language), "language");
            content.Add(new StringContent("json"), "response_format");

            // CRIT-007 FIX: Create linked token with dispose cancellation
            using var localCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(localCts.Token, disposeCts.Token);

            try
            {
                var response = await client.PostAsync("/inference", content, linkedCts.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync(linkedCts.Token).ConfigureAwait(false);
                var result = JsonSerializer.Deserialize<WhisperServerResponse>(jsonResponse);

                return result?.text?.Trim() ?? string.Empty;
            }
            catch (OperationCanceledException) when (disposeCts.IsCancellationRequested)
            {
                throw new ObjectDisposedException(nameof(WhisperServerService), "Service disposed during transcription");
            }
            catch (OperationCanceledException) when (localCts.Token.IsCancellationRequested)
            {
                ErrorLogger.LogWarning("WhisperServerService: HTTP request timed out after 120 seconds");
                throw new TimeoutException("Server transcription timed out after 120 seconds. The server may be overloaded or unresponsive.");
            }
        }

        private string ResolveModelPath()
        {
            var modelPath = Path.Combine(baseDir, "whisper", settings.WhisperModel);
            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"Model file not found: {modelPath}");
            return modelPath;
        }

        private int FindFreePort(int startPort, int endPort)
        {
            for (int port = startPort; port <= endPort; port++)
            {
                System.Net.Sockets.TcpListener? listener = null;
                try
                {
                    listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
                    listener.Start();
                    return port;
                }
                catch
                {
                    // Port in use, try next
                }
                finally
                {
                    listener?.Stop();
                }
            }
            return startPort; // Fallback
        }


        public async Task<string> TranscribeFromMemoryAsync(byte[] audioData)
        {
            // Delegate to fallback service (server.exe doesn't support in-memory transcription)
            return await fallbackService.TranscribeFromMemoryAsync(audioData);
        }

        public void Dispose()
        {
            if (isDisposed) return;

            // CRIT-007 FIX: Cancel any pending requests immediately
            try
            {
                disposeCts.Cancel();
            }
            catch { /* Ignore cancellation errors */ }

            // CRIT-002 FIX: Lock to prevent disposal during active transcription
            lock (httpClientLock)
            {
                isDisposed = true;

                // Cancel pending HTTP requests before disposal
                try
                {
                    httpClient?.CancelPendingRequests();
                }
                catch { /* Best effort */ }

                httpClient?.Dispose();
            }

            // CRIT-001 FIX: Non-blocking process termination with hard timeout
            if (serverProcess != null && !serverProcess.HasExited)
            {
                try
                {
                    serverProcess.Kill(entireProcessTree: true);

                    // CRITICAL: Use Task.Run to prevent UI thread blocking
                    var waitTask = Task.Run(() =>
                    {
                        try
                        {
                            return serverProcess.WaitForExit(2000);
                        }
                        catch
                        {
                            return false;
                        }
                    });

                    // Hard timeout: 3 seconds max
                    if (waitTask.Wait(3000) && waitTask.Result)
                    {
                        ErrorLogger.LogMessage("Whisper server stopped gracefully");
                    }
                    else
                    {
                        ErrorLogger.LogWarning("Whisper server kill timed out - process may still be running");
                        // Fire-and-forget taskkill as last resort (don't wait)
                        _ = Task.Run(() =>
                        {
                            try
                            {
                                System.Diagnostics.Process.Start(new ProcessStartInfo
                                {
                                    FileName = "taskkill",
                                    Arguments = $"/F /T /PID {serverProcess.Id}",
                                    CreateNoWindow = true,
                                    UseShellExecute = false
                                });
                            }
                            catch { /* Best effort */ }
                        });
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Failed to stop Whisper server", ex);
                }
                finally
                {
                    try
                    {
                        serverProcess.Dispose();
                    }
                    catch { /* Ignore disposal errors */ }
                }
            }

            if (fallbackService is IDisposable disposable)
            {
                disposable.Dispose();
            }

            disposeCts.Dispose();
        }

        /// <summary>
        /// PERFORMANCE FIX: Check if server is already running and healthy
        /// Enables singleton pattern - reuse existing server instead of restarting
        /// </summary>
        private async Task<bool> IsServerHealthy()
        {
            if (httpClient == null)
                return false;

            try
            {
                using var cts = new CancellationTokenSource(1000); // 1 second timeout
                var response = await httpClient.GetAsync("/", cts.Token);

                // Any HTTP response (even 404) means server is alive
                return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound;
            }
            catch
            {
                // Timeout, connection refused, or other error = server not healthy
                return false;
            }
        }

        /// <summary>
        /// CRITICAL FIX: Kill zombie server.exe processes before starting new one
        /// Prevents multiple servers from deadlocking over shared resources
        /// </summary>
        private void KillExistingServers()
        {
            try
            {
                var existingServers = Process.GetProcessesByName("server");
                if (existingServers.Length == 0)
                {
                    return; // No servers running
                }

                ErrorLogger.LogMessage($"Found {existingServers.Length} existing server.exe process(es) - checking if zombies");

                foreach (var proc in existingServers)
                {
                    try
                    {
                        // Only kill if it's from whisper directory (not unrelated server.exe)
                        var mainModule = proc.MainModule?.FileName;
                        if (mainModule != null && mainModule.Contains("whisper", StringComparison.OrdinalIgnoreCase))
                        {
                            ErrorLogger.LogWarning($"Killing zombie server.exe PID {proc.Id} from {mainModule}");
                            proc.Kill(entireProcessTree: true);

                            // Wait briefly for clean shutdown
                            if (!proc.WaitForExit(2000))
                            {
                                ErrorLogger.LogWarning($"Zombie server {proc.Id} did not exit cleanly - forcing");
                            }
                        }
                        else if (mainModule != null)
                        {
                            ErrorLogger.LogMessage($"Ignoring unrelated server.exe at {mainModule}");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Process might already be dead or access denied - ignore
                        ErrorLogger.LogMessage($"Could not check/kill server PID {proc.Id}: {ex.Message}");
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                // Non-critical failure - log and continue
                ErrorLogger.LogMessage($"Failed to check for zombie servers: {ex.Message}");
            }
        }

        private class WhisperServerResponse
        {
            public string? text { get; set; }
        }
    }
}

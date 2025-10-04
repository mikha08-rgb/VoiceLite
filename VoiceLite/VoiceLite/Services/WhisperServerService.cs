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
            // Use server if running, otherwise fallback
            if (isServerRunning)
            {
                try
                {
                    return await TranscribeViaServerAsync(audioFilePath).ConfigureAwait(false);
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
        }

        private async Task StartServerAsync()
        {
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

            // Wait for server to be ready
            // Use temporary HttpClient to avoid leak if initialization fails
            HttpClient? tempClient = null;
            try
            {
                tempClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{serverPort}") };
                tempClient.Timeout = TimeSpan.FromSeconds(3); // Shorter timeout per request

                // HIGH PRIORITY FIX: Add overall timeout to prevent infinite waiting
                // If server never responds, fail fast instead of hanging forever
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
                        var response = await tempClient!.GetAsync("/", requestTimeout.Token).ConfigureAwait(false);
                        // Any response (even 404) means server is listening
                        ErrorLogger.LogMessage("Whisper server is ready");

                        // Success - transfer ownership to class field
                        httpClient = tempClient;
                        tempClient = null; // Prevent disposal in finally block
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
                // Clean up temporary client if initialization failed
                tempClient?.Dispose();
            }
        }

        private async Task<string> TranscribeViaServerAsync(string audioFilePath)
        {
            if (httpClient == null)
                throw new InvalidOperationException("Server not initialized");

            using var audioStream = File.OpenRead(audioFilePath);
            using var content = new MultipartFormDataContent();

            var fileContent = new StreamContent(audioStream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
            content.Add(fileContent, "file", Path.GetFileName(audioFilePath));

            // Add transcription parameters
            content.Add(new StringContent("true"), "no_timestamps");
            content.Add(new StringContent(settings.Language), "language");
            content.Add(new StringContent("json"), "response_format");

            // CRITICAL FIX: Add timeout to prevent infinite hang if server stops responding
            // 120 seconds max (same as RecordingCoordinator watchdog timeout)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            try
            {
                var response = await httpClient.PostAsync("/inference", content, cts.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                var result = JsonSerializer.Deserialize<WhisperServerResponse>(jsonResponse);

                return result?.text?.Trim() ?? string.Empty;
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
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
            isDisposed = true;

            httpClient?.Dispose();

            if (serverProcess != null && !serverProcess.HasExited)
            {
                try
                {
                    serverProcess.Kill(entireProcessTree: true);
                    serverProcess.WaitForExit(2000);
                    ErrorLogger.LogMessage("Whisper server stopped");
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Failed to stop Whisper server", ex);
                }
                serverProcess.Dispose();
            }

            if (fallbackService is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        private class WhisperServerResponse
        {
            public string? text { get; set; }
        }
    }
}

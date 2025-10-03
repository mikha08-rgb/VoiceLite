using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
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
                await StartServerAsync();
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
                    return await TranscribeViaServerAsync(audioFilePath);
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Server transcription failed - falling back to process mode", ex);
                    isServerRunning = false;
                    return await fallbackService.TranscribeAsync(audioFilePath);
                }
            }
            else
            {
                return await fallbackService.TranscribeAsync(audioFilePath);
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
                          $"--best-of {settings.BestOf}";

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
            catch { }

            // Wait for server to be ready
            httpClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{serverPort}") };
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Give server 3 seconds to start and bind port
            for (int i = 0; i < 6; i++)
            {
                await Task.Delay(500);

                // Check if process crashed
                if (serverProcess.HasExited)
                    throw new InvalidOperationException($"Server process exited with code {serverProcess.ExitCode}");

                // Try a simple GET request to root endpoint
                try
                {
                    var response = await httpClient.GetAsync("/");
                    // Any response (even 404) means server is listening
                    ErrorLogger.LogMessage("Whisper server is ready");
                    return;
                }
                catch (HttpRequestException)
                {
                    // Server not ready yet, continue waiting
                }
            }

            throw new TimeoutException("Server failed to respond within 3 seconds");
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

            var response = await httpClient.PostAsync("/inference", content);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<WhisperServerResponse>(jsonResponse);

            return result?.text?.Trim() ?? string.Empty;
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
                try
                {
                    var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
                    listener.Start();
                    listener.Stop();
                    return port;
                }
                catch
                {
                    // Port in use, try next
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

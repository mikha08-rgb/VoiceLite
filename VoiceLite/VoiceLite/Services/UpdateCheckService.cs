using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    public sealed class UpdateInfo
    {
        public required string Version { get; init; }
        public required string DownloadUrl { get; init; }
    }

    public static class UpdateCheckService
    {
        private const string GitHubLatestReleaseUrl =
            "https://api.github.com/repos/mikha08-rgb/VoiceLite/releases/latest";

        // Lazy-init: defer HttpClient construction until first CheckAsync call so
        // static init doesn't run during MainWindow_Loaded JIT (which caused a multi-minute
        // startup hang in v2.1.1 testing — symptom was app stalling right after Silero VAD init).
        private static HttpClient? _httpClient;
        private static readonly object _httpClientLock = new();
        private static HttpClient GetHttpClient()
        {
            if (_httpClient != null) return _httpClient;
            lock (_httpClientLock)
            {
                if (_httpClient != null) return _httpClient;
                var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                client.DefaultRequestHeaders.Add("User-Agent", "VoiceLite-Desktop/UpdateCheck");
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
                _httpClient = client;
                return _httpClient;
            }
        }

        public static async Task<UpdateInfo?> CheckAsync(Settings settings, CancellationToken ct = default)
        {
            if (!settings.CheckForUpdates) return null;

            try
            {
                using var response = await GetHttpClient().GetAsync(GitHubLatestReleaseUrl, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    ErrorLogger.LogWarning($"UpdateCheck: HTTP {(int)response.StatusCode} from GitHub Releases API");
                    return null;
                }

                using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
                var root = doc.RootElement;

                if (!root.TryGetProperty("tag_name", out var tagElem) || tagElem.ValueKind != JsonValueKind.String)
                    return null;
                var tag = tagElem.GetString();
                if (string.IsNullOrWhiteSpace(tag)) return null;

                var latestVersion = NormalizeTag(tag!);
                var currentVersion = GetCurrentVersion();

                if (latestVersion == null || currentVersion == null) return null;
                if (latestVersion <= currentVersion)
                {
                    ErrorLogger.LogMessage($"UpdateCheck: latest={latestVersion}, current={currentVersion}, no update");
                    return null;
                }

                if (string.Equals(settings.SkippedUpdateVersion, latestVersion.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    ErrorLogger.LogMessage($"UpdateCheck: latest={latestVersion} matches SkippedUpdateVersion, suppressed");
                    return null;
                }

                var downloadUrl = ResolveDownloadUrl(root, tag!);
                ErrorLogger.LogMessage($"UpdateCheck: update available {currentVersion} -> {latestVersion}");
                return new UpdateInfo { Version = latestVersion.ToString(3), DownloadUrl = downloadUrl };
            }
            catch (OperationCanceledException) { return null; }
            catch (HttpRequestException ex)
            {
                ErrorLogger.LogWarning($"UpdateCheck: network error - {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("UpdateCheck: unexpected failure", ex);
                return null;
            }
        }

        private static Version? NormalizeTag(string tag)
        {
            var trimmed = tag.Trim();
            if (trimmed.StartsWith('v') || trimmed.StartsWith('V')) trimmed = trimmed[1..];
            // Strip pre-release/build metadata so "2.1.1-beta" still parses
            var dash = trimmed.IndexOfAny(new[] { '-', '+' });
            if (dash >= 0) trimmed = trimmed[..dash];
            return Version.TryParse(trimmed, out var v) ? v : null;
        }

        private static Version? GetCurrentVersion()
        {
            // Assembly FileVersion is "2.1.1.0"; convert to Version for comparison
            var asm = Assembly.GetExecutingAssembly();
            var v = asm.GetName().Version;
            return v;
        }

        private static string ResolveDownloadUrl(JsonElement root, string tag)
        {
            // Prefer the .exe asset's browser_download_url; fall back to the release html_url.
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.TryGetProperty("name", out var nameElem) &&
                        nameElem.ValueKind == JsonValueKind.String &&
                        nameElem.GetString()!.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                        asset.TryGetProperty("browser_download_url", out var urlElem) &&
                        urlElem.ValueKind == JsonValueKind.String)
                    {
                        return urlElem.GetString()!;
                    }
                }
            }

            if (root.TryGetProperty("html_url", out var html) && html.ValueKind == JsonValueKind.String)
                return html.GetString()!;

            return $"https://github.com/mikha08-rgb/VoiceLite/releases/tag/{tag}";
        }
    }
}

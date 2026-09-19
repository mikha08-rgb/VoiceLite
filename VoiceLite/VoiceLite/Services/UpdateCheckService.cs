using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    public sealed class UpdateInfo
    {
        public required string Version { get; init; }

        /// <summary>
        /// Installer .exe asset URL when the release has one; otherwise the release page.
        /// </summary>
        public required string DownloadUrl { get; init; }

        /// <summary>
        /// Release page (GitHub html_url). Where the user is sent when auto-install is refused.
        /// </summary>
        public required string ReleasePageUrl { get; init; }

        /// <summary>
        /// Installer asset file name (e.g. VoiceLite-Setup-2.4.2.exe); null when the release
        /// has no .exe asset.
        /// </summary>
        public string? AssetName { get; init; }

        /// <summary>
        /// Expected SHA-256 of the installer asset as 64 uppercase hex chars, or null when
        /// neither the API asset digest nor the release notes provided one.
        /// </summary>
        public string? Sha256 { get; init; }

        /// <summary>
        /// Where <see cref="Sha256"/> came from: "digest" (GitHub asset digest field),
        /// "body" (### SHA256 Hashes section of the release notes), or null.
        /// </summary>
        public string? Sha256Source { get; init; }

        /// <summary>
        /// True only when we have an installer asset AND a hash to verify it against.
        /// Without a hash the updater must not run the download silently — it opens the
        /// release page instead.
        /// </summary>
        public bool CanAutoInstall => AssetName != null && Sha256 != null;
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
                    ErrorLogger.LogWarning($"UpdateCheck: latest={latestVersion}, current={currentVersion}, no update");
                    return null;
                }

                if (string.Equals(settings.SkippedUpdateVersion, latestVersion.ToString(3), StringComparison.OrdinalIgnoreCase))
                {
                    ErrorLogger.LogMessage($"UpdateCheck: latest={latestVersion} matches SkippedUpdateVersion, suppressed");
                    return null;
                }

                var info = BuildUpdateInfo(root, tag!, latestVersion.ToString(3));
                ErrorLogger.LogWarning(
                    $"UpdateCheck: update available {currentVersion} -> {latestVersion} " +
                    $"(asset={info.AssetName ?? "none"}, sha256Source={info.Sha256Source ?? "none"})");
                return info;
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

        internal static Version? GetCurrentVersion()
        {
            // Assembly FileVersion is "2.1.1.0"; convert to Version for comparison
            var asm = Assembly.GetExecutingAssembly();
            var v = asm.GetName().Version;
            return v;
        }

        /// <summary>
        /// Resolves the installer asset, its download URL and its expected SHA-256 from a
        /// GitHub release JSON object. Hash source priority:
        ///   1. the asset's <c>digest</c> field ("sha256:&lt;hex&gt;", present on assets uploaded since 2025)
        ///   2. the 64-hex hash listed under the asset's name in the release body
        ///      (release.yml writes a "### SHA256 Hashes" section)
        /// If neither yields a hash, <see cref="UpdateInfo.Sha256"/> is null and the caller
        /// must not auto-install.
        /// </summary>
        internal static UpdateInfo BuildUpdateInfo(JsonElement root, string tag, string version)
        {
            string releasePage = root.TryGetProperty("html_url", out var html) && html.ValueKind == JsonValueKind.String
                ? html.GetString()!
                : $"https://github.com/mikha08-rgb/VoiceLite/releases/tag/{tag}";

            string? assetName = null;
            string? assetUrl = null;
            string? digestHash = null;

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
                        assetName = nameElem.GetString()!;
                        assetUrl = urlElem.GetString()!;
                        if (asset.TryGetProperty("digest", out var digestElem) && digestElem.ValueKind == JsonValueKind.String)
                            digestHash = ParseSha256Digest(digestElem.GetString());
                        break;
                    }
                }
            }

            string? sha256 = null;
            string? sha256Source = null;
            if (assetName != null)
            {
                if (digestHash != null)
                {
                    sha256 = digestHash;
                    sha256Source = "digest";
                }
                else
                {
                    var body = root.TryGetProperty("body", out var bodyElem) && bodyElem.ValueKind == JsonValueKind.String
                        ? bodyElem.GetString()
                        : null;
                    var bodyHash = ExtractSha256FromBody(body, assetName);
                    if (bodyHash != null)
                    {
                        sha256 = bodyHash;
                        sha256Source = "body";
                    }
                    else
                    {
                        ErrorLogger.LogWarning(
                            $"UpdateCheck: no SHA-256 for {assetName} (no asset digest, none in release notes) - auto-install will be refused");
                    }
                }
            }

            return new UpdateInfo
            {
                Version = version,
                DownloadUrl = assetUrl ?? releasePage,
                ReleasePageUrl = releasePage,
                AssetName = assetName,
                Sha256 = sha256,
                Sha256Source = sha256Source,
            };
        }

        private static readonly Regex Hex64 = new(
            @"(?<![0-9A-Fa-f])([0-9A-Fa-f]{64})(?![0-9A-Fa-f])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Parses the GitHub asset <c>digest</c> field ("sha256:&lt;64 hex&gt;"). Returns the
        /// uppercase hex hash, or null for anything else (missing, other algorithm, malformed).
        /// </summary>
        internal static string? ParseSha256Digest(string? digest)
        {
            if (string.IsNullOrWhiteSpace(digest)) return null;
            var trimmed = digest.Trim();
            const string prefix = "sha256:";
            if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
            var hex = trimmed[prefix.Length..].Trim();
            if (hex.Length != 64) return null;
            foreach (var c in hex)
            {
                if (!Uri.IsHexDigit(c)) return null;
            }
            return hex.ToUpperInvariant();
        }

        /// <summary>
        /// Finds the 64-hex SHA-256 listed for <paramref name="assetName"/> in the release
        /// notes. release.yml emits "**name** (size)\n```text\nHASH\n```" blocks under a
        /// "### SHA256 Hashes" heading; we search that section first (the asset name may
        /// also appear earlier in install instructions) and fall back to the whole body.
        /// </summary>
        internal static string? ExtractSha256FromBody(string? body, string assetName)
        {
            if (string.IsNullOrEmpty(body) || string.IsNullOrEmpty(assetName)) return null;

            var sectionStart = body.IndexOf("SHA256 Hashes", StringComparison.OrdinalIgnoreCase);
            if (sectionStart >= 0)
            {
                var fromSection = FindHashAfterName(body[sectionStart..], assetName);
                if (fromSection != null) return fromSection;
            }

            return FindHashAfterName(body, assetName);
        }

        private static string? FindHashAfterName(string text, string assetName)
        {
            var idx = text.IndexOf(assetName, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var m = Hex64.Match(text, idx + assetName.Length);
            return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
        }
    }
}

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
                    ErrorLogger.LogWarning($"UpdateCheck: latest={latestVersion} matches SkippedUpdateVersion, suppressed");
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
            // html_url is ShellExecuted by UpdateWindow, so it gets the same https + github.com
            // check as the asset; anything else falls back to the constructed tag URL.
            string releasePage = root.TryGetProperty("html_url", out var html) && html.ValueKind == JsonValueKind.String
                                 && IsTrustedGitHubUrl(html.GetString(), requiredPathPart: null)
                ? html.GetString()!
                : $"https://github.com/mikha08-rgb/VoiceLite/releases/tag/{tag}";

            string? assetName = null;
            string? assetUrl = null;
            string? digestHash = null;

            // Only the installer release.yml publishes (VoiceLite-Setup-<version>.exe, served
            // from github.com over https) may be downloaded and run silently. Any other .exe
            // (a redistributable, a tool, an off-host URL) is ignored; with no pinned asset the
            // update falls back to the release page. The exact name for this version wins over
            // a merely pattern-matching one.
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                var exactName = $"VoiceLite-Setup-{version}.exe";
                foreach (var asset in assets.EnumerateArray())
                {
                    if (!asset.TryGetProperty("name", out var nameElem) || nameElem.ValueKind != JsonValueKind.String ||
                        !asset.TryGetProperty("browser_download_url", out var urlElem) || urlElem.ValueKind != JsonValueKind.String)
                        continue;

                    var name = nameElem.GetString()!;
                    var url = urlElem.GetString()!;
                    if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!InstallerAssetName.IsMatch(name) || !IsTrustedGitHubUrl(url, "/releases/download/"))
                    {
                        ErrorLogger.LogWarning($"UpdateCheck: ignoring unexpected .exe asset '{name}' ({url})");
                        continue;
                    }

                    var isExact = string.Equals(name, exactName, StringComparison.OrdinalIgnoreCase);
                    if (assetName != null && !isExact) continue;

                    assetName = name;
                    assetUrl = url;
                    digestHash = asset.TryGetProperty("digest", out var digestElem) && digestElem.ValueKind == JsonValueKind.String
                        ? ParseSha256Digest(digestElem.GetString())
                        : null;
                    if (isExact) break;
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

        // release.yml names the installer VoiceLite-Setup-$VERSION.exe where VERSION is [\d.]+.
        private static readonly Regex InstallerAssetName = new(
            @"^VoiceLite-Setup-\d+(\.\d+){1,3}\.exe$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        // "### SHA256 Hashes" on a line of its own. \s*$ (not a bare $) tolerates CRLF bodies.
        private static readonly Regex HashHeading = new(
            @"^[ \t]*#{1,6}[ \t]*SHA256 Hashes\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        /// <summary>
        /// https + github.com only. Only the URL the API hands us is checked: the download
        /// itself is a 302 to GitHub's asset CDN which HttpClient follows, and the SHA-256
        /// check (not the final host) is what authenticates the bytes. Owner/repo are not
        /// pinned so a repo rename doesn't silently disable auto-install.
        /// </summary>
        internal static bool IsTrustedGitHubUrl(string? url, string? requiredPathPart)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            if (uri.Scheme != Uri.UriSchemeHttps) return false;
            if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)) return false;
            return requiredPathPart == null ||
                   uri.AbsolutePath.Contains(requiredPathPart, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Finds the 64-hex SHA-256 listed for <paramref name="assetName"/> in the release
        /// notes. release.yml emits "**name** (size)\n```text\nHASH\n```" blocks under a
        /// "### SHA256 Hashes" heading. We anchor on that heading line (the template also says
        /// "SHA256 hashes" in prose and names the asset in the Download section, long before
        /// the real block) and fall back to the whole body only when there is no heading.
        /// Returns null - auto-install refused - rather than guess.
        /// </summary>
        internal static string? ExtractSha256FromBody(string? body, string assetName)
        {
            if (string.IsNullOrEmpty(body) || string.IsNullOrEmpty(assetName)) return null;

            var heading = HashHeading.Match(body);
            if (heading.Success)
            {
                var fromSection = FindHashAfterName(body[heading.Index..], assetName);
                if (fromSection != null) return fromSection;
            }

            return FindHashAfterName(body, assetName);
        }

        private static string? FindHashAfterName(string text, string assetName)
        {
            var idx = text.IndexOf(assetName, StringComparison.OrdinalIgnoreCase);
            while (idx >= 0)
            {
                var afterName = idx + assetName.Length;
                var m = Hex64.Match(text, afterName);
                if (!m.Success) return null;

                // Another file name between this mention and the hash means the hash belongs
                // to that file (or this mention is just prose) - try the next mention.
                var between = text.AsSpan(afterName, m.Index - afterName);
                if (between.IndexOf(".exe", StringComparison.OrdinalIgnoreCase) < 0)
                    return m.Groups[1].Value.ToUpperInvariant();

                idx = text.IndexOf(assetName, afterName, StringComparison.OrdinalIgnoreCase);
            }
            return null;
        }
    }
}

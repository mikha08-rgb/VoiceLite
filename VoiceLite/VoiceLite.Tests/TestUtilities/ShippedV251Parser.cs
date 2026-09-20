using System;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VoiceLite.Tests.TestUtilities
{
    /// <summary>
    /// FROZEN - this is what 2.5.0 / 2.5.1 clients in the field run; NEVER edit it.
    ///
    /// Verbatim copy of the update-resolution code shipped in v2.5.1
    /// (git show v2.5.1:VoiceLite/VoiceLite/Services/UpdateCheckService.cs - identical in
    /// v2.5.0): the UpdateInfo shape, BuildUpdateInfo (takes the FIRST asset whose name ends in
    /// ".exe", prefers its GitHub "digest", falls back to the release body), ParseSha256Digest,
    /// ExtractSha256FromBody (anchors on the first case-insensitive "SHA256 Hashes") and the
    /// single-shot FindHashAfterName (first 64-hex run after the first mention of the name).
    ///
    /// Those clients can only be updated BY the release they are about to download, so the
    /// release JSON + release body that release.yml produces is a contract with this code, not
    /// with whatever UpdateCheckService looks like today. ShippedUpdaterContractTests parses
    /// the rendered release with BOTH. If this copy seems wrong or outdated, it is not: the
    /// installed base does not change. FrozenRegionSha256 fails the suite on any edit.
    /// </summary>
    internal static class ShippedV251Parser
    {
        /// <summary>SHA-256 (uppercase hex, LF line endings, UTF-8) of the text between the FROZEN markers below.</summary>
        public const string FrozenRegionSha256 = "5B05FAF28CA0C4A9B1EA3D55DA2B0CA430CAA775B1CC311FC0057AA31137F9F1";

        public const string FrozenBeginMarker = "// <FROZEN v2.5.1>";
        public const string FrozenEndMarker = "// </FROZEN v2.5.1>";

        // The only non-verbatim part: the shipped code logs one warning through the app's
        // ErrorLogger. A no-op of the same name keeps the frozen text below untouched and free
        // of any dependency on today's product code.
        private static class ErrorLogger
        {
            public static void LogWarning(string message) { }
        }

        // <FROZEN v2.5.1>
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
        // </FROZEN v2.5.1>
    }
}

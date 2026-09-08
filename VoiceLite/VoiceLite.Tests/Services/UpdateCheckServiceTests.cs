using System.Text.Json;
using AwesomeAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// Hash-source resolution for the in-app updater. No network: every test feeds a
    /// hand-written GitHub Releases API payload into BuildUpdateInfo.
    /// </summary>
    public class UpdateCheckServiceTests
    {
        private const string HashA = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
        private const string HashB = "FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210";
        private const string AssetName = "VoiceLite-Setup-2.5.0.exe";
        private const string AssetUrl = "https://github.com/mikha08-rgb/VoiceLite/releases/download/v2.5.0/" + AssetName;
        private const string HtmlUrl = "https://github.com/mikha08-rgb/VoiceLite/releases/tag/v2.5.0";

        private static UpdateInfo Build(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return UpdateCheckService.BuildUpdateInfo(doc.RootElement, "v2.5.0", "2.5.0");
        }

        private static string ReleaseJson(string? digest, string? body)
        {
            var digestProp = digest == null ? "" : $", \"digest\": \"{digest}\"";
            var bodyProp = body == null ? "null" : JsonSerializer.Serialize(body);
            return $$"""
            {
              "tag_name": "v2.5.0",
              "html_url": "{{HtmlUrl}}",
              "body": {{bodyProp}},
              "assets": [
                { "name": "release-notes.txt", "browser_download_url": "https://example.invalid/notes.txt" },
                { "name": "{{AssetName}}", "browser_download_url": "{{AssetUrl}}"{{digestProp}} }
              ]
            }
            """;
        }

        // Mirrors the "### SHA256 Hashes" block release.yml writes into the release body.
        private static string BodyWithHashes(string hashForAsset) =>
            "## VoiceLite v2.5.0\n\n" +
            "### Installation\n" +
            $"1. Download **{AssetName}**\n" +
            "2. Run it\n\n" +
            "### SHA256 Hashes\n" +
            $"**{AssetName}** (98.12 MB)\n```text\n{hashForAsset}\n```\n\n" +
            "### Troubleshooting\nSee TROUBLESHOOTING.md\n";

        [Fact]
        public void DigestField_IsPreferredHashSource()
        {
            var info = Build(ReleaseJson($"sha256:{HashA.ToLowerInvariant()}", BodyWithHashes(HashB)));

            info.AssetName.Should().Be(AssetName);
            info.DownloadUrl.Should().Be(AssetUrl);
            info.ReleasePageUrl.Should().Be(HtmlUrl);
            info.Sha256.Should().Be(HashA, "the API digest wins over the release body");
            info.Sha256Source.Should().Be("digest");
            info.CanAutoInstall.Should().BeTrue();
        }

        [Fact]
        public void ReleaseBody_IsUsedWhenDigestMissing()
        {
            var info = Build(ReleaseJson(digest: null, BodyWithHashes(HashB)));

            info.Sha256.Should().Be(HashB);
            info.Sha256Source.Should().Be("body");
            info.CanAutoInstall.Should().BeTrue();
        }

        [Fact]
        public void ReleaseBody_IsUsedWhenDigestIsNotSha256()
        {
            var info = Build(ReleaseJson("md5:d41d8cd98f00b204e9800998ecf8427e", BodyWithHashes(HashB)));

            info.Sha256.Should().Be(HashB);
            info.Sha256Source.Should().Be("body");
        }

        [Fact]
        public void NoHashAnywhere_RefusesAutoInstall_ButKeepsDownloadUrl()
        {
            var info = Build(ReleaseJson(digest: null, body: "## VoiceLite v2.5.0\n\nBug fixes.\n"));

            info.AssetName.Should().Be(AssetName);
            info.DownloadUrl.Should().Be(AssetUrl);
            info.Sha256.Should().BeNull();
            info.Sha256Source.Should().BeNull();
            info.CanAutoInstall.Should().BeFalse("an unverifiable installer must never be run silently");
        }

        [Fact]
        public void NoExeAsset_FallsBackToReleasePage()
        {
            var json = $$"""
            {
              "tag_name": "v2.5.0",
              "html_url": "{{HtmlUrl}}",
              "body": "{{HashA}}",
              "assets": [ { "name": "source.zip", "browser_download_url": "https://example.invalid/source.zip" } ]
            }
            """;
            var info = Build(json);

            info.AssetName.Should().BeNull();
            info.DownloadUrl.Should().Be(HtmlUrl);
            info.Sha256.Should().BeNull("a hash without an asset is meaningless");
            info.CanAutoInstall.Should().BeFalse();
        }

        [Theory]
        [InlineData("sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", HashA)]
        [InlineData("SHA256:0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", HashA)]
        [InlineData("  sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef  ", HashA)]
        [InlineData("sha512:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", null)]
        [InlineData("sha256:0123456789abcdef", null)]
        [InlineData("sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdeZ", null)]
        [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void ParseSha256Digest_AcceptsOnlyWellFormedSha256(string? digest, string? expected)
        {
            UpdateCheckService.ParseSha256Digest(digest).Should().Be(expected);
        }

        [Fact]
        public void ExtractSha256FromBody_PicksHashForTheNamedAsset()
        {
            // Two installers listed; the asset we want is second. The first mention of the
            // asset name (install instructions) precedes the OTHER asset's hash — the parser
            // must anchor on the "SHA256 Hashes" section, not the first name occurrence.
            var body =
                "### Installation\n" +
                $"Download {AssetName} and run it.\n\n" +
                "### SHA256 Hashes\n" +
                $"**VoiceLite-Setup-2.5.0-arm64.exe** (90.00 MB)\n```text\n{HashA}\n```\n\n" +
                $"**{AssetName}** (98.12 MB)\n```text\n{HashB.ToLowerInvariant()}\n```\n";

            UpdateCheckService.ExtractSha256FromBody(body, AssetName).Should().Be(HashB);
        }

        [Fact]
        public void ExtractSha256FromBody_FallsBackToWholeBodyWithoutSection()
        {
            var body = $"Checksum for {AssetName}: {HashA}";

            UpdateCheckService.ExtractSha256FromBody(body, AssetName).Should().Be(HashA);
        }

        [Fact]
        public void ExtractSha256FromBody_IgnoresHashesBeforeTheAssetName()
        {
            var body = $"{HashA}\n\n### SHA256 Hashes\n**{AssetName}**\n```text\n{HashB}\n```";

            UpdateCheckService.ExtractSha256FromBody(body, AssetName).Should().Be(HashB);
        }

        [Fact]
        public void ExtractSha256FromBody_RejectsLongerHexRuns()
        {
            // 65 hex chars is not a SHA-256; must not be truncated into one.
            var body = $"### SHA256 Hashes\n**{AssetName}**\n```text\n{HashA}A\n```";

            UpdateCheckService.ExtractSha256FromBody(body, AssetName).Should().BeNull();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("no hashes here")]
        public void ExtractSha256FromBody_ReturnsNullForMissingInput(string? body)
        {
            UpdateCheckService.ExtractSha256FromBody(body, AssetName).Should().BeNull();
        }
    }
}

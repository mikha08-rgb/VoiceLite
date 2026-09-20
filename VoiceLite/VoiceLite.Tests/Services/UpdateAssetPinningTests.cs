using System.Text.Json;
using AwesomeAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// S5: the updater only auto-installs the asset release.yml publishes
    /// (VoiceLite-Setup-&lt;version&gt;.exe, https, github.com) and the release-notes hash
    /// parser anchors on the "### SHA256 Hashes" heading, not on the prose that mentions
    /// "SHA256 hashes" earlier in the real template. No network.
    /// </summary>
    public class UpdateAssetPinningTests
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

        private static string Asset(string name, string url, string hash) =>
            $"{{ \"name\": \"{name}\", \"browser_download_url\": \"{url}\", \"digest\": \"sha256:{hash.ToLowerInvariant()}\" }}";

        private static string Release(string assetsJson, string htmlUrl = HtmlUrl) =>
            $"{{ \"tag_name\": \"v2.5.0\", \"html_url\": \"{htmlUrl}\", \"body\": null, \"assets\": [ {assetsJson} ] }}";

        /// <summary>
        /// The body release.yml really writes (see .github/workflows/release.yml `body:`),
        /// including the "Compare with SHA256 hashes at bottom of this page" prose and the
        /// Download bullet that names the asset long before the hash section.
        /// </summary>
        private static string RealTemplateBody(string hashSection, string extraRequirementsLine = "") =>
            "## VoiceLite v2.5.0\n\n" +
            "### ⚠️ IMPORTANT: Verify Download First\n\n" +
            "**Before installing**, verify your download hasn't been corrupted:\n\n" +
            "```powershell\nGet-FileHash VoiceLite-Setup-*.exe -Algorithm SHA256\n```\n\n" +
            "Compare with SHA256 hashes at bottom of this page. **Mismatched hash = corrupted download**.\n\n" +
            "---\n\n" +
            "### Download\n" +
            $"- **{AssetName}** (~150 MB)\n" +
            "  - .NET 8 bundled (self-contained, no separate install needed)\n\n" +
            "### Installation Steps\n" +
            "1. **Verify SHA256 hash** (see above) ⚠️\n" +
            "2. Download the installer\n\n" +
            "### System Requirements\n" +
            "- Windows 10/11 (64-bit)\n" +
            extraRequirementsLine +
            "\n### SHA256 Hashes\n" +
            hashSection +
            "\n\n### Troubleshooting\nIf installation fails, see TROUBLESHOOTING.md\n";

        private static string HashBlock(string name, string hash) =>
            $"**{name}** (78.83 MB)\n```text\n{hash}\n```\n";

        // ---------------------------------------------------------------- asset pinning

        [Fact]
        public void BuildUpdateInfo_SkipsNonInstallerExe()
        {
            var info = Build(Release(
                Asset("vc_redist.x64.exe", "https://github.com/mikha08-rgb/VoiceLite/releases/download/v2.5.0/vc_redist.x64.exe", HashA) + ", " +
                Asset(AssetName, AssetUrl, HashB)));

            info.AssetName.Should().Be(AssetName, "only the installer release.yml publishes may be run silently");
            info.DownloadUrl.Should().Be(AssetUrl);
            info.Sha256.Should().Be(HashB);
        }

        [Fact]
        public void BuildUpdateInfo_PrefersTheExactVersionedInstaller()
        {
            var otherUrl = "https://github.com/mikha08-rgb/VoiceLite/releases/download/v2.5.0/VoiceLite-Setup-2.4.9.exe";
            var info = Build(Release(
                Asset("VoiceLite-Setup-2.4.9.exe", otherUrl, HashA) + ", " +
                Asset(AssetName, AssetUrl, HashB)));

            info.AssetName.Should().Be(AssetName);
            info.Sha256.Should().Be(HashB);
        }

        [Fact]
        public void BuildUpdateInfo_AcceptsFourPartInstallerName_WhenExactNameIsAbsent()
        {
            // latestVersion.ToString(3) drops a 4th component, so the pattern fallback must stay.
            var url = "https://github.com/mikha08-rgb/VoiceLite/releases/download/v2.5.0.1/VoiceLite-Setup-2.5.0.1.exe";
            var info = Build(Release(Asset("VoiceLite-Setup-2.5.0.1.exe", url, HashA)));

            info.AssetName.Should().Be("VoiceLite-Setup-2.5.0.1.exe");
            info.CanAutoInstall.Should().BeTrue();
        }

        [Theory]
        [InlineData("http://github.com/mikha08-rgb/VoiceLite/releases/download/v2.5.0/VoiceLite-Setup-2.5.0.exe")]
        [InlineData("https://evil.example/mikha08-rgb/VoiceLite/releases/download/v2.5.0/VoiceLite-Setup-2.5.0.exe")]
        [InlineData("https://github.com.evil.example/x/releases/download/v2.5.0/VoiceLite-Setup-2.5.0.exe")]
        [InlineData("https://github.com/mikha08-rgb/VoiceLite/archive/VoiceLite-Setup-2.5.0.exe")]
        [InlineData("file:///c:/x/releases/download/VoiceLite-Setup-2.5.0.exe")]
        [InlineData("not a url")]
        public void BuildUpdateInfo_RejectsOffHostOrNonHttpsAssetUrl(string url)
        {
            var info = Build(Release(Asset(AssetName, url, HashA)));

            info.AssetName.Should().BeNull();
            info.DownloadUrl.Should().Be(HtmlUrl, "an unpinned asset falls back to the release page");
            info.CanAutoInstall.Should().BeFalse();
        }

        [Fact]
        public void BuildUpdateInfo_AcceptsAssetUrlAfterRepoRename()
        {
            // Owner/repo are deliberately NOT pinned: after a rename GitHub redirects the API
            // call but browser_download_url carries the new owner/repo.
            var url = "https://github.com/new-owner/VoiceLite2/releases/download/v2.5.0/" + AssetName;
            var info = Build(Release(Asset(AssetName, url, HashA)));

            info.DownloadUrl.Should().Be(url);
            info.CanAutoInstall.Should().BeTrue();
        }

        [Theory]
        [InlineData("http://github.com/mikha08-rgb/VoiceLite/releases/tag/v2.5.0")]
        [InlineData("https://evil.example/releases/tag/v2.5.0")]
        [InlineData("file:///c:/windows/system32/calc.exe")]
        public void BuildUpdateInfo_IgnoresUntrustedHtmlUrl(string htmlUrl)
        {
            var info = Build(Release(Asset(AssetName, AssetUrl, HashA), htmlUrl));

            info.ReleasePageUrl.Should().Be(HtmlUrl, "the release page is ShellExecuted, so it falls back to the constructed tag URL");
        }

        [Fact]
        public void BuildUpdateInfo_LiveShapedPayload_UsesDigest()
        {
            // Every release since v2.2.0 carries an asset digest; the body is only a fallback.
            var json = $"{{ \"tag_name\": \"v2.5.0\", \"html_url\": \"{HtmlUrl}\", " +
                       $"\"body\": {JsonSerializer.Serialize(RealTemplateBody(HashBlock(AssetName, HashB)))}, " +
                       $"\"assets\": [ {Asset(AssetName, AssetUrl, HashA)} ] }}";

            var info = Build(json);

            info.Sha256Source.Should().Be("digest");
            info.Sha256.Should().Be(HashA);
        }

        // ---------------------------------------------------------------- body parser

        [Fact]
        public void ExtractSha256FromBody_ShippedFormat_StillParses()
        {
            var body = RealTemplateBody(HashBlock(AssetName, HashB));

            UpdateCheckService.ExtractSha256FromBody(body, AssetName).Should().Be(HashB);
        }

        [Fact]
        public void ExtractSha256FromBody_ShippedFormat_WithCrLf_StillParses()
        {
            var body = RealTemplateBody(HashBlock(AssetName, HashB)).Replace("\n", "\r\n");

            UpdateCheckService.ExtractSha256FromBody(body, AssetName).Should().Be(HashB);
        }

        [Fact]
        public void ExtractSha256FromBody_RealTemplate_TwoInstallers_PicksNamedAsset()
        {
            var body = RealTemplateBody(
                HashBlock("VoiceLite-Setup-2.5.0-arm64.exe", HashA) + "\n" + HashBlock(AssetName, HashB));

            UpdateCheckService.ExtractSha256FromBody(body, AssetName).Should().Be(HashB);
        }

        [Fact]
        public void ExtractSha256FromBody_IgnoresUnrelatedHexBetweenDownloadSectionAndHashSection()
        {
            var body = RealTemplateBody(
                HashBlock(AssetName, HashB),
                extraRequirementsLine: $"- Speech model checksum {HashA}\n");

            UpdateCheckService.ExtractSha256FromBody(body, AssetName).Should().Be(HashB);
        }

        [Fact]
        public void ExtractSha256FromBody_NeverBorrowsAnotherInstallersHash()
        {
            // The named asset is listed without a hash; the next hash belongs to another file.
            // Failing closed (null => CanAutoInstall false) beats verifying against the wrong hash.
            var body =
                "### SHA256 Hashes\n" +
                $"**{AssetName}** (78.83 MB)\n\n" +
                HashBlock("VoiceLite-Setup-2.5.0-arm64.exe", HashA);

            UpdateCheckService.ExtractSha256FromBody(body, AssetName).Should().BeNull();
        }

        [Fact]
        public void ExtractSha256FromBody_HeadingWithoutTheAsset_FailsClosed()
        {
            var body = RealTemplateBody(HashBlock("VoiceLite-Setup-2.5.0-arm64.exe", HashA));

            UpdateCheckService.ExtractSha256FromBody(body, AssetName).Should().BeNull();
        }
    }
}

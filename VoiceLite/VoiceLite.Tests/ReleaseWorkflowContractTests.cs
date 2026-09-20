using System;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// Text-contract tripwires for .github/workflows/release.yml: the release-notes body
    /// it generates is the SHA-256 fallback the shipped updater parses
    /// (UpdateCheckService.ExtractSha256FromBody), so its hash block is a frozen format.
    /// </summary>
    public class ReleaseWorkflowContractTests
    {
        private const string HashA = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        private const string HashB = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

        private static string? ReleaseYml() => RepoSourceFile.Read(".github", "workflows", "release.yml");

        private static string ReleaseBodyTemplate(string yml)
        {
            var lines = yml.Split('\n');
            var start = Array.FindIndex(lines, l => l.TrimEnd() == "        body: |");
            Assert.True(start >= 0, "release.yml: 'body: |' block not found");
            const string indent = "          ";
            var body = lines.Skip(start + 1)
                .TakeWhile(l => l.Length == 0 || l.StartsWith(indent, StringComparison.Ordinal))
                .Select(l => l.Length == 0 ? l : l.Substring(indent.Length));
            return string.Join("\n", body);
        }

        /// <summary>Exactly what the "Generate SHA256 hashes" step emits per installer.</summary>
        private static string HashBlock(string name, string hash) => $"**{name}** (78.83 MB)\n```text\n{hash}\n```\n";

        private static string Render(string template, string hashes) => template
            .Replace("${{ steps.version.outputs.VERSION }}", "9.9.9")
            .Replace("${{ github.repository }}", "owner/VoiceLite")
            .Replace("${{ steps.generate-hashes.outputs.HASHES }}", hashes);

        [Fact]
        public void ReleaseYml_HashGeneratorAndAssetName_AreByteCompatibleWithShippedParser()
        {
            var yml = ReleaseYml();
            if (yml == null) return;

            yml.Should().Contain("$hashes += \"**$($installer.Name)** ($sizeInMB MB)`n``````text`n$($hash.Hash)`n```````n\"");
            yml.Should().Contain("          ### SHA256 Hashes\n          ${{ steps.generate-hashes.outputs.HASHES }}\n");
            yml.Should().Contain("        files: |\n          VoiceLite-Setup-*.exe\n");
        }

        [Fact]
        public void ReleaseYml_RenderedBody_ParsesWithShippedExtractor()
        {
            var yml = ReleaseYml();
            if (yml == null) return;
            var body = Render(ReleaseBodyTemplate(yml), HashBlock("VoiceLite-Setup-9.9.9.exe", HashB));

            UpdateCheckService.ExtractSha256FromBody(body, "VoiceLite-Setup-9.9.9.exe").Should().Be(HashB);
        }

        [Fact]
        public void ReleaseYml_SectionAnchor_LandsOnTheHashHeading()
        {
            var yml = ReleaseYml();
            if (yml == null) return;
            var template = ReleaseBodyTemplate(yml);

            // The parser anchors on the FIRST case-insensitive "SHA256 Hashes". Prose using
            // that phrase above the heading drags the anchor up to the Download bullet.
            Regex.Matches(template, "SHA256 Hashes", RegexOptions.IgnoreCase).Count.Should().Be(1);

            // With the anchor on the heading, a second installer listed first cannot
            // donate its hash to the primary asset.
            var hashes = HashBlock("VoiceLite-Setup-9.9.9-arm64.exe", HashA) + "\n" + HashBlock("VoiceLite-Setup-9.9.9.exe", HashB);
            UpdateCheckService.ExtractSha256FromBody(Render(template, hashes), "VoiceLite-Setup-9.9.9.exe").Should().Be(HashB);
        }

        [Fact]
        public void ReleaseYml_BodyTemplate_HasNoStaleV2EraText_AndNoStrayHash()
        {
            var yml = ReleaseYml();
            if (yml == null) return;
            var template = ReleaseBodyTemplate(yml);

            template.Should().NotContain("What's New in v2.0");
            template.Should().NotContain("~150 MB");
            template.Should().NotContain("TROUBLESHOOTING.md", "no such file exists in the repo (404)");
            template.Should().NotContain("Model corruption", "the model is no longer bundled; the installer hash says nothing about it");
            Regex.IsMatch(template, "[0-9A-Fa-f]{64}").Should().BeFalse("a literal 64-hex run in the template could be parsed as the installer hash");
            // The 2.5.0 in-app updater never opened (fixed in 2.5.1) - don't send 2.5.0 users to it.
            template.Should().NotContain("2.5.0 or later");
        }

        [Fact]
        public void ReleaseYml_SetupLogHint_CoversElevationWithAnotherAccount()
        {
            var yml = ReleaseYml();
            if (yml == null) return;
            var hint = ReleaseBodyTemplate(yml).Split('\n').Single(l => l.Contains("Setup Log"));

            // Setup only logs in the process that installs. If the update was elevated with
            // a different administrator's credentials, that is the admin's %TEMP%, not the user's.
            hint.Should().Contain("%TEMP%");
            hint.Should().Contain("administrator");
        }

        // ---------- build-release.ps1 (S4, legacy README text) ----------

        [Fact]
        public void BuildReleaseReadme_DoesNotPointAtTheRemovedAiModelsTab()
        {
            var ps1 = RepoSourceFile.Read("build-release.ps1");
            if (ps1 == null) return;
            ps1.Should().NotContain("AI Models");
        }
    }
}

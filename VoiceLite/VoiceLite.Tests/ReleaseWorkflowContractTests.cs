using System;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using VoiceLite.Services;
using VoiceLite.Tests.TestUtilities;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// Text-contract tripwires for .github/workflows/release.yml: the release-notes body
    /// it generates is the SHA-256 fallback the updater parses, so its hash block is a frozen
    /// format. "The updater" is two parsers: the one 2.5.0/2.5.1 clients in the field run
    /// (ShippedV251Parser, a frozen copy - the in-tree method was rewritten for 2.5.2 and is NOT
    /// what they run) and today's UpdateCheckService.ExtractSha256FromBody. Every rendered
    /// body here must parse with both. ShippedUpdaterContractTests does the same with the
    /// real output of the hash-generator step and a full release JSON.
    /// </summary>
    public class ReleaseWorkflowContractTests
    {
        private const string HashA = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        private const string HashB = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

        internal static string ReleaseYml() => RepoSourceFile.Read(".github", "workflows", "release.yml");

        internal static string ReleaseBodyTemplate(string yml)
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

        /// <summary>What the "Generate SHA256 hashes" step emits per installer. Hand-written;
        /// ShippedUpdaterContractTests checks it against the step's REAL output.</summary>
        internal static string HashBlock(string name, string hash, string sizeInMB = "78.83") =>
            $"**{name}** ({sizeInMB} MB)\n```text\n{hash}\n```\n";

        internal static string Render(string template, string hashes) => template
            .Replace("${{ steps.version.outputs.VERSION }}", "9.9.9")
            .Replace("${{ github.repository }}", "owner/VoiceLite")
            .Replace("${{ steps.generate-hashes.outputs.HASHES }}", hashes);

        /// <summary>Both updaters, on the body as rendered (LF) and as a CRLF body.</summary>
        internal static void AssertBothUpdatersReadHash(string body, string assetName, string expectedHash)
        {
            foreach (var variant in new[] { body, body.Replace("\r\n", "\n").Replace("\n", "\r\n") })
            {
                var lineEndings = variant.Contains('\r') ? "CRLF" : "LF";
                ShippedV251Parser.ExtractSha256FromBody(variant, assetName).Should().Be(expectedHash,
                    $"2.5.0/2.5.1 clients in the field parse the release body with the FROZEN v2.5.1 code ({lineEndings} body)");
                UpdateCheckService.ExtractSha256FromBody(variant, assetName).Should().Be(expectedHash,
                    $"the current updater must read the same hash ({lineEndings} body)");
            }
        }

        [Fact]
        public void ReleaseYml_HashGeneratorAndAssetName_AreByteCompatibleWithShippedParser()
        {
            var yml = ReleaseYml();

            yml.Should().Contain("$hashes += \"**$($installer.Name)** ($sizeInMB MB)`n``````text`n$($hash.Hash)`n```````n\"");
            yml.Should().Contain("          ### SHA256 Hashes\n          ${{ steps.generate-hashes.outputs.HASHES }}\n");
            yml.Should().Contain("        files: |\n          VoiceLite-Setup-*.exe\n");
        }

        [Fact]
        public void ReleaseYml_RenderedBody_ParsesWithShippedExtractor()
        {
            var body = Render(ReleaseBodyTemplate(ReleaseYml()), HashBlock("VoiceLite-Setup-9.9.9.exe", HashB));

            AssertBothUpdatersReadHash(body, "VoiceLite-Setup-9.9.9.exe", HashB);
        }

        [Fact]
        public void ReleaseYml_SectionAnchor_LandsOnTheHashHeading()
        {
            var template = ReleaseBodyTemplate(ReleaseYml());

            // The shipped parser anchors on the FIRST case-insensitive "SHA256 Hashes". Prose using
            // that phrase above the heading drags the anchor up to the Download bullet.
            Regex.Matches(template, "SHA256 Hashes", RegexOptions.IgnoreCase).Count.Should().Be(1);

            // With the anchor on the heading, a second installer listed first cannot
            // donate its hash to the primary asset.
            var hashes = HashBlock("VoiceLite-Setup-9.9.9-arm64.exe", HashA) + "\n" + HashBlock("VoiceLite-Setup-9.9.9.exe", HashB);
            AssertBothUpdatersReadHash(Render(template, hashes), "VoiceLite-Setup-9.9.9.exe", HashB);
        }

        [Fact]
        public void ReleaseYml_BodyTemplate_HasNoStaleV2EraText_AndNoStrayHash()
        {
            var template = ReleaseBodyTemplate(ReleaseYml());

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
            var hint = ReleaseBodyTemplate(ReleaseYml()).Split('\n').Single(l => l.Contains("Setup Log"));

            // Setup only logs in the process that installs. If the update was elevated with
            // a different administrator's credentials, that is the admin's %TEMP%, not the user's.
            hint.Should().Contain("%TEMP%");
            hint.Should().Contain("administrator");
        }

        // ---------- build-release.ps1 (S4, legacy README text) ----------

        [Fact]
        public void BuildReleaseReadme_DoesNotPointAtTheRemovedAiModelsTab()
        {
            RepoSourceFile.Read("build-release.ps1").Should().NotContain("AI Models");
        }
    }
}

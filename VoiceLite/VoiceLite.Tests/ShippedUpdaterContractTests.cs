using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using VoiceLite.Services;
using VoiceLite.Tests.TestUtilities;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// The contract the NEXT release must not break: 2.5.0 / 2.5.1 clients in the field can only
    /// be updated by the update code they already have (ShippedV251Parser - a frozen, verbatim
    /// copy). So the release that release.yml publishes - its asset list and its notes body -
    /// is checked against that frozen code AND against today's UpdateCheckService.
    ///
    /// Nothing here is a pasted copy of release.yml: the body template, the "files:" list and
    /// the hash-generator script are read from the workflow file at test time, and the hash
    /// section is produced by RUNNING the workflow's own "Generate SHA256 hashes" step (child
    /// pwsh, throw-away directory, dummy files that are hashed and never executed).
    /// </summary>
    public class ShippedUpdaterContractTests
    {
        private const string Primary = "VoiceLite-Setup-9.9.9.exe";
        private const string Secondary = "VoiceLite-Setup-9.9.9-arm64.exe"; // sorts BEFORE the primary ('-' < '.')

        // ------------------------------------------------------------------ frozen copy

        [Fact]
        public void FrozenParser_HasNotBeenEdited()
        {
            var text = RepoSourceFile.Read("VoiceLite", "VoiceLite.Tests", "TestUtilities", "ShippedV251Parser.cs");
            var begin = "\n        " + ShippedV251Parser.FrozenBeginMarker + "\n";
            var end = "\n        " + ShippedV251Parser.FrozenEndMarker + "\n";
            var from = text.IndexOf(begin, StringComparison.Ordinal);
            var to = text.IndexOf(end, StringComparison.Ordinal);
            Assert.True(from >= 0 && to > from, "FROZEN markers not found in ShippedV251Parser.cs");

            var frozen = text.Substring(from + begin.Length, to - from - begin.Length);
            var actual = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(frozen)));

            Assert.True(actual == ShippedV251Parser.FrozenRegionSha256,
                "ShippedV251Parser's FROZEN region was edited. It is a verbatim copy of what 2.5.0/2.5.1 clients run " +
                "(git show v2.5.1:VoiceLite/VoiceLite/Services/UpdateCheckService.cs); the installed base does not change, " +
                "so neither may this. Revert the edit - if a contract test fails, the RELEASE (release.yml) is what broke. " +
                $"Expected {ShippedV251Parser.FrozenRegionSha256}, got {actual}.");
        }

        [Fact]
        public void FrozenParser_ReallyIsTheOldBehaviour_NotAnAliasOfTheCurrentOne()
        {
            // The case the 2.5.2 parser was hardened for and the shipped one gets WRONG: prose
            // saying "SHA256 hashes" above the heading drags the old anchor up, the first mention
            // of the name is then the Download bullet, and the first hash after it belongs to
            // another installer. If both parsers agreed here, asserting "both" would prove nothing.
            const string hashA = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
            const string hashB = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
            var body =
                "Compare the file with the SHA256 hashes at the bottom.\n\n### Download\n- **" + Primary + "**\n\n" +
                "### SHA256 Hashes\n" +
                ReleaseWorkflowContractTests.HashBlock(Secondary, hashA) + "\n" +
                ReleaseWorkflowContractTests.HashBlock(Primary, hashB);

            UpdateCheckService.ExtractSha256FromBody(body, Primary).Should().Be(hashB);
            ShippedV251Parser.ExtractSha256FromBody(body, Primary).Should().Be(hashA,
                "this is the shipped weakness the release body has to keep steering around (2.5.x then refuses the update: hash mismatch)");
        }

        // ------------------------------------------------------------------ release.yml -> assets

        [Fact]
        public void ReleaseYml_UploadsExactlyOneAssetPattern_TheInstaller()
        {
            // The shipped updater downloads the FIRST asset whose name ends in ".exe" - whatever
            // it is. A second upload pattern (a tool, a redistributable, a portable build) can
            // put another .exe first and 2.5.x would run THAT with /VERYSILENT.
            var lines = ReleaseWorkflowContractTests.ReleaseYml().Split('\n');
            var start = Array.FindIndex(lines, l => l.TrimEnd() == "        files: |");
            Assert.True(start >= 0, "release.yml: 'files: |' block not found");
            var entries = lines.Skip(start + 1)
                .TakeWhile(l => l.StartsWith("          ", StringComparison.Ordinal))
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            entries.Should().Equal("VoiceLite-Setup-*.exe");
        }

        [Fact]
        public void ShippedUpdater_TakesTheFirstExeAsset_WhateverItIs()
        {
            // Characterization of the frozen code - the reason for the test above.
            using var doc = JsonDocument.Parse(ReleaseJson(
                body: "",
                Asset("vc_redist.x64.exe", digest: null),
                Asset(Primary, digest: null)));

            ShippedV251Parser.BuildUpdateInfo(doc.RootElement, "v9.9.9", "9.9.9").AssetName.Should().Be("vc_redist.x64.exe");
            UpdateCheckService.BuildUpdateInfo(doc.RootElement, "v9.9.9", "9.9.9").AssetName.Should().Be(Primary);
        }

        // ------------------------------------------------------------------ release.yml -> body

        [Fact]
        public void HandWrittenHashBlock_MatchesWhatTheWorkflowStepReallyEmits()
        {
            // ReleaseWorkflowContractTests renders bodies from a hand-written block; this is
            // what keeps that block honest.
            var generated = GeneratedHashes.Value;

            generated.Hashes.Replace("\r\n", "\n").TrimEnd('\n').Should().Be(
                (ReleaseWorkflowContractTests.HashBlock(Secondary, generated.SecondaryHash, generated.SecondarySize) + "\n" +
                 ReleaseWorkflowContractTests.HashBlock(Primary, generated.PrimaryHash, generated.PrimarySize)).TrimEnd('\n'));
        }

        [Fact]
        public void RenderedReleaseBody_WithTheRealGeneratorOutput_ParsesWithShippedAndCurrentUpdater()
        {
            var generated = GeneratedHashes.Value;
            generated.PrimaryHash.Should().NotBe(generated.SecondaryHash);
            var template = ReleaseWorkflowContractTests.ReleaseBodyTemplate(ReleaseWorkflowContractTests.ReleaseYml());

            // As the runner hands it over (whatever line endings the step produced), then LF, then CRLF.
            var body = ReleaseWorkflowContractTests.Render(template, generated.Hashes);
            ShippedV251Parser.ExtractSha256FromBody(body, Primary).Should().Be(generated.PrimaryHash);
            UpdateCheckService.ExtractSha256FromBody(body, Primary).Should().Be(generated.PrimaryHash);
            ReleaseWorkflowContractTests.AssertBothUpdatersReadHash(body, Primary, generated.PrimaryHash);
            // ...and neither hands the primary's hash to the other installer.
            ReleaseWorkflowContractTests.AssertBothUpdatersReadHash(body, Secondary, generated.SecondaryHash);
        }

        // ------------------------------------------------------------------ GitHub release JSON

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GitHubReleaseJson_WithAssetDigest_ResolvesTheSameUpdate_InShippedAndCurrentUpdater(bool crlfBody)
        {
            var generated = GeneratedHashes.Value;
            // The digest, not the body, must win in both: give the body a DIFFERENT hash for the asset.
            var digest = new string('c', 64);
            using var doc = JsonDocument.Parse(ReleaseJson(RenderedBody(generated, crlfBody), Asset(Primary, "sha256:" + digest)));

            var shipped = ShippedV251Parser.BuildUpdateInfo(doc.RootElement, "v9.9.9", "9.9.9");
            var current = UpdateCheckService.BuildUpdateInfo(doc.RootElement, "v9.9.9", "9.9.9");

            AssertSameUpdate(shipped, current, expectedSha256: digest.ToUpperInvariant(), expectedSource: "digest");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GitHubReleaseJson_WithoutAssetDigest_FallsBackToTheBody_InShippedAndCurrentUpdater(bool crlfBody)
        {
            var generated = GeneratedHashes.Value;
            using var doc = JsonDocument.Parse(ReleaseJson(RenderedBody(generated, crlfBody), Asset(Primary, digest: null)));

            var shipped = ShippedV251Parser.BuildUpdateInfo(doc.RootElement, "v9.9.9", "9.9.9");
            var current = UpdateCheckService.BuildUpdateInfo(doc.RootElement, "v9.9.9", "9.9.9");

            AssertSameUpdate(shipped, current, expectedSha256: generated.PrimaryHash, expectedSource: "body");
        }

        private static void AssertSameUpdate(ShippedV251Parser.UpdateInfo shipped, UpdateInfo current, string expectedSha256, string expectedSource)
        {
            const string url = "https://github.com/mikha08-rgb/VoiceLite/releases/download/v9.9.9/" + Primary;

            shipped.AssetName.Should().Be(Primary);
            shipped.DownloadUrl.Should().Be(url);
            shipped.Sha256.Should().Be(expectedSha256);
            shipped.Sha256Source.Should().Be(expectedSource);
            shipped.CanAutoInstall.Should().BeTrue("a 2.5.x client that cannot auto-install is stuck on the broken-updater support path");

            current.AssetName.Should().Be(shipped.AssetName);
            current.DownloadUrl.Should().Be(shipped.DownloadUrl);
            current.ReleasePageUrl.Should().Be(shipped.ReleasePageUrl);
            current.Sha256.Should().Be(shipped.Sha256);
            current.Sha256Source.Should().Be(shipped.Sha256Source);
            current.CanAutoInstall.Should().BeTrue();
        }

        private static string RenderedBody(GeneratorRun generated, bool crlf)
        {
            var body = ReleaseWorkflowContractTests.Render(
                ReleaseWorkflowContractTests.ReleaseBodyTemplate(ReleaseWorkflowContractTests.ReleaseYml()), generated.Hashes);
            body = body.Replace("\r\n", "\n");
            return crlf ? body.Replace("\n", "\r\n") : body;
        }

        /// <summary>Shaped like GET /repos/{owner}/{repo}/releases/latest (fields the updaters ignore included).</summary>
        private static string ReleaseJson(string body, params Dictionary<string, object?>[] assets) =>
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["url"] = "https://api.github.com/repos/mikha08-rgb/VoiceLite/releases/999999",
                ["assets_url"] = "https://api.github.com/repos/mikha08-rgb/VoiceLite/releases/999999/assets",
                ["html_url"] = "https://github.com/mikha08-rgb/VoiceLite/releases/tag/v9.9.9",
                ["id"] = 999999,
                ["author"] = new Dictionary<string, object?> { ["login"] = "github-actions[bot]", ["type"] = "Bot" },
                ["tag_name"] = "v9.9.9",
                ["target_commitish"] = "master",
                ["name"] = "VoiceLite v9.9.9",
                ["draft"] = false,
                ["prerelease"] = false,
                ["created_at"] = "2026-09-19T10:00:00Z",
                ["published_at"] = "2026-09-19T10:20:00Z",
                ["assets"] = assets,
                ["tarball_url"] = "https://api.github.com/repos/mikha08-rgb/VoiceLite/tarball/v9.9.9",
                ["zipball_url"] = "https://api.github.com/repos/mikha08-rgb/VoiceLite/zipball/v9.9.9",
                ["body"] = body,
            });

        private static Dictionary<string, object?> Asset(string name, string? digest) => new()
        {
            ["url"] = "https://api.github.com/repos/mikha08-rgb/VoiceLite/releases/assets/111111",
            ["id"] = 111111,
            ["name"] = name,
            ["label"] = "",
            ["content_type"] = "application/x-msdownload",
            ["state"] = "uploaded",
            ["size"] = 82660000,
            ["digest"] = digest,
            ["download_count"] = 0,
            ["browser_download_url"] = "https://github.com/mikha08-rgb/VoiceLite/releases/download/v9.9.9/" + name,
        };

        // ------------------------------------------------------------------ running the workflow step

        private sealed record GeneratorRun(string Hashes, string PrimaryHash, string PrimarySize, string SecondaryHash, string SecondarySize);

        /// <summary>One pwsh run for the whole class (each test only reads the result).</summary>
        private static readonly Lazy<GeneratorRun> GeneratedHashes = new(RunHashGeneratorStep);

        /// <summary>The "run: |" script of the named step, exactly as it is in release.yml.</summary>
        private static string StepScript(string yml, string stepName)
        {
            var lines = yml.Split('\n');
            var step = Array.FindIndex(lines, l => l.TrimEnd() == "    - name: " + stepName);
            Assert.True(step >= 0, $"release.yml: step '{stepName}' not found");
            var run = Array.FindIndex(lines, step + 1, l => l.TrimEnd() == "      run: |");
            var nextStep = Array.FindIndex(lines, step + 1, l => l.StartsWith("    - ", StringComparison.Ordinal));
            Assert.True(run > step && (nextStep < 0 || run < nextStep), $"release.yml: step '{stepName}' has no 'run: |' block");

            const string indent = "        ";
            var script = lines.Skip(run + 1)
                .TakeWhile(l => l.Trim().Length == 0 || l.StartsWith(indent, StringComparison.Ordinal))
                .Select(l => l.Trim().Length == 0 ? "" : l.Substring(indent.Length));
            return string.Join("\n", script);
        }

        /// <summary>
        /// The step is EXECUTED on developer machines by this class (the owner's licensed
        /// VoiceLite may be running there), so it may only list, hash and append to
        /// $env:GITHUB_OUTPUT. Strict allowlist, not a denylist: anything this does not know -
        /// a new cmdlet, alias, native tool, .NET type or method, a redirection, a call
        /// operator - fails the test until a human has looked at it and extended the lists.
        /// Deliberately string-blind: "$( ... )" inside a string is code too.
        /// </summary>
        private static void AssertHashStepIsSafeToRun(string script)
        {
            const string review = "; review the 'Generate SHA256 hashes' step before letting a unit test run it, then extend the allowlist";
            var commands = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "echo", "Get-ChildItem", "Get-FileHash", "Out-File", "foreach" };
            var staticCalls = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "[math]::Round(", "[guid]::NewGuid(" };

            // Full-line comments are prose; everything else is scanned, strings included.
            var code = string.Join("\n", script.Split('\n').Where(l => !l.TrimStart().StartsWith('#')));

            // 1. Whatever stands where PowerShell reads a command name - start of a line, or
            //    after | ; { } ( = - is an allowlisted name or plainly an expression. '}' counts:
            //    a statement block needs no terminator, so "} Stop-Process" is a new command.
            foreach (Match m in Regex.Matches(code, @"(?:^|[|;{}(=])[ \t]*(?=(?<tok>\S+))", RegexOptions.Multiline))
            {
                var tok = m.Groups["tok"].Value;
                var name = Regex.Match(tok, @"^[A-Za-z_][\w-]*");
                if (name.Success)
                {
                    Assert.True(commands.Contains(name.Value), $"command '{name.Value}' is not on the allowlist" + review);
                    // Only the foreach STATEMENT: in a pipeline "foreach" is ForEach-Object, and
                    // "| foreach Delete" calls a method without parentheses.
                    if (name.Value.Equals("foreach", StringComparison.OrdinalIgnoreCase))
                        Assert.True(Regex.IsMatch(code.Substring(m.Index + m.Length), @"^foreach\s*\(", RegexOptions.IgnoreCase)
                            && !m.Value.TrimStart().StartsWith('|'), "'foreach' is allowed only as the statement 'foreach (...)'" + review);
                }
                else
                    Assert.True("$\"'[})@#|;".Contains(tok[0]) || char.IsDigit(tok[0]), $"'{tok}' in command position is not on the allowlist" + review);
            }

            // 2. .NET: only these static calls, and ToString() as the only instance method.
            foreach (Match m in Regex.Matches(code, @"\[[^\]\r\n]*\](?:\s*::\s*\w+\s*\(?)?"))
                Assert.True(staticCalls.Contains(Regex.Replace(m.Value, @"\s", "")), $"'{m.Value}' is not on the allowlist" + review);
            foreach (Match m in Regex.Matches(code, @"\.\s*(?<method>\w+)\s*\("))
                Assert.True(m.Groups["method"].Value == "ToString", $"method call '.{m.Groups["method"].Value}(' is not on the allowlist" + review);

            // 3. No call operator, no redirection; the only file written is $env:GITHUB_OUTPUT,
            //    and that variable is not reassigned.
            Assert.False(code.Contains('&') || code.Contains('>'), "'&' or '>' found" + review);
            foreach (Match m in Regex.Matches(code, @"\bOut-File\b[^\n|;]*", RegexOptions.IgnoreCase))
                Assert.True(m.Value.StartsWith("Out-File -FilePath $env:GITHUB_OUTPUT ", StringComparison.Ordinal), $"'{m.Value.Trim()}' writes somewhere else" + review);
            Assert.False(Regex.IsMatch(code, @"\$env:\w+\s*[-+*/]?="), "an environment variable is assigned" + review);
        }

        [Fact]
        public void HashStepGuard_AcceptsTheRealStep() =>
            AssertHashStepIsSafeToRun(StepScript(ReleaseWorkflowContractTests.ReleaseYml(), "Generate SHA256 hashes"));

        [Theory]
        [InlineData("Get-Process VoiceLite | Stop-Process")]
        [InlineData("gh release upload v9.9.9 x.exe")]
        [InlineData("rm -r -fo .")]
        [InlineData("$x = Get-Process")]
        [InlineData("$x=Stop-Process -Name VoiceLite")]
        [InlineData("echo \"$(Stop-Process -Name VoiceLite)\"")]
        [InlineData("echo 'done'; del x")]
        [InlineData("foreach ($i in $installers) { Move-Item $i.FullName C:\\ }")]
        [InlineData("& 'C:\\Windows\\System32\\taskkill.exe' /IM VoiceLite.exe")]
        [InlineData(". .\\other.ps1")]
        [InlineData("[Diagnostics.Process]::Start('notepad')")]
        [InlineData("[math]::Max(1, 2)")]
        [InlineData("$installers[0].Delete()")]
        [InlineData("'x' > C:\\somewhere.txt")]
        [InlineData("'x' | Out-File -FilePath C:\\somewhere.txt")]
        [InlineData("$env:GITHUB_OUTPUT = 'C:\\somewhere.txt'")]
        [InlineData("if ($true) { echo 'x' }")]
        [InlineData("Get-ChildItem | % { $_ }")]
        [InlineData("foreach ($i in $installers) { } Stop-Process -Name VoiceLite")]
        [InlineData("$installers | foreach Delete")]
        [InlineData("$installers | foreach ($_) { }")]
        public void HashStepGuard_RefusesAnythingOutsideTheAllowlist(string addedLine)
        {
            var real = StepScript(ReleaseWorkflowContractTests.ReleaseYml(), "Generate SHA256 hashes");

            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertHashStepIsSafeToRun(real + "\n" + addedLine + "\n"));
        }

        private static GeneratorRun RunHashGeneratorStep()
        {
            var script = StepScript(ReleaseWorkflowContractTests.ReleaseYml(), "Generate SHA256 hashes");
            AssertHashStepIsSafeToRun(script);

            var dir = Path.Combine(Path.GetTempPath(), "VoiceLite-ReleaseHashStep-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(dir, "work", "VoiceLite", "Installer"));
            try
            {
                // Dummy payloads with installer-like NAMES. They are hashed, never executed.
                var primaryBytes = Encoding.ASCII.GetBytes("not an installer - primary " + new string('p', 3_000_000));
                var secondaryBytes = Encoding.ASCII.GetBytes("not an installer - secondary " + new string('s', 1_500_000));
                // Get-ChildItem -Recurse lists a directory's own files before its subdirectories:
                // this puts the OTHER installer first in the hash section, the hostile order.
                File.WriteAllBytes(Path.Combine(dir, "work", Secondary), secondaryBytes);
                File.WriteAllBytes(Path.Combine(dir, "work", "VoiceLite", "Installer", Primary), primaryBytes);
                var outputFile = Path.Combine(dir, "github_output.txt");
                File.WriteAllText(outputFile, "");

                var psi = new ProcessStartInfo("pwsh")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Path.Combine(dir, "work"),
                };
                psi.Environment["GITHUB_OUTPUT"] = outputFile;
                foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand",
                             Convert.ToBase64String(Encoding.Unicode.GetBytes("$ErrorActionPreference = 'Stop'\n" + script)) })
                    psi.ArgumentList.Add(arg);

                Process process;
                try
                {
                    process = Process.Start(psi)!;
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    throw new InvalidOperationException(
                        "This test needs PowerShell 7 (pwsh) on PATH: it runs release.yml's own hash-generator step.", ex);
                }

                using (process)
                {
                    var stderr = process.StandardError.ReadToEndAsync();
                    var stdout = process.StandardOutput.ReadToEndAsync();
                    if (!process.WaitForExit(60_000))
                    {
                        try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
                        Assert.Fail("The hash-generator step did not finish within 60s");
                    }
                    Assert.True(process.ExitCode == 0,
                        $"The hash-generator step exited with {process.ExitCode}.\nstdout:\n{stdout.Result}\nstderr:\n{stderr.Result}");
                }

                return new GeneratorRun(
                    ReadMultilineOutput(File.ReadAllText(outputFile), "HASHES"),
                    Convert.ToHexString(SHA256.HashData(primaryBytes)), SizeInMB(primaryBytes.Length),
                    Convert.ToHexString(SHA256.HashData(secondaryBytes)), SizeInMB(secondaryBytes.Length));
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
            }
        }

        // [math]::Round(bytes / 1MB, 2), printed the way PowerShell prints a double.
        private static string SizeInMB(int bytes) =>
            Math.Round(bytes / 1048576.0, 2).ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The value of a "NAME&lt;&lt;DELIMITER ... DELIMITER" entry of a $GITHUB_OUTPUT file.</summary>
        private static string ReadMultilineOutput(string outputFile, string name)
        {
            var text = outputFile.TrimStart('﻿');
            var header = Regex.Match(text, "^" + Regex.Escape(name) + @"<<(\S+)\r?\n", RegexOptions.Multiline);
            Assert.True(header.Success, $"$GITHUB_OUTPUT has no multi-line '{name}' entry:\n{text}");
            var valueStart = header.Index + header.Length;
            var close = Regex.Match(text.Substring(valueStart), @"\r?\n" + Regex.Escape(header.Groups[1].Value) + @"\r?(\n|$)");
            Assert.True(close.Success, $"$GITHUB_OUTPUT: '{name}' entry is not terminated by its delimiter:\n{text}");
            return text.Substring(valueStart, close.Index);
        }
    }
}

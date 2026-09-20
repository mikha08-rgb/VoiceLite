using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// One policy for every source-text tripwire in this project: a tripwire that cannot find
    /// the file it pins goes RED. xunit 2.x has no runtime skip, and "return;" is a green test
    /// that checked nothing - if a build change (SourceLink / ContinuousIntegrationBuild /
    /// PathMap, or running copied binaries) moved the compile-time paths, about 25 wiring,
    /// installer and release-body contract checks would pass unread. Both CI workflows
    /// (pr-tests.yml, release.yml) build and test in the same checkout, so the
    /// [CallerFilePath] locations exist there.
    /// </summary>
    public class SourceTripwirePolicyTests
    {
        [Fact]
        public void RepoSourceFile_FailsLoudly_WhenTheCompileTimePathDoesNotExist()
        {
            var nowhere = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "SomeTests.cs");

            var ex = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => RepoSourceFile.FindRepoRoot(nowhere));
            Assert.Contains(nowhere, ex.Message);
        }

        [Fact]
        public void RepoSourceFile_FailsLoudly_WhenTheFileIsMissing()
        {
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => RepoSourceFile.Read("VoiceLite", "no-such-file.txt"));
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => RepoSourceFile.ReadApp("NoSuchWindow.xaml.cs"));
        }

        [Fact]
        public void RepoSourceFile_FindsTheCheckout()
        {
            Assert.True(File.Exists(Path.Combine(RepoSourceFile.RepoRoot, "VoiceLite", "VoiceLite.sln")));
            Assert.True(File.Exists(Path.Combine(RepoSourceFile.AppDir, "App.xaml.cs")));
            Assert.Contains("class App", RepoSourceFile.ReadApp("App.xaml.cs"));
        }

        [Fact]
        public void SharedReadAppSource_FailsLoudly_WhenTheFileIsMissing() =>
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => SettingsWindowHotkeyTests.ReadAppSource("NoSuchWindow.xaml.cs"));

        /// <summary>
        /// Keeps the policy from eroding: no test file may bring back a source lookup that
        /// answers "not found" with null or a bare return.
        /// </summary>
        [Fact]
        public void NoTestFile_LooksUpSourcesInAWayThatCanPassSilently()
        {
            // A [CallerFilePath] helper that may return null invites "if (x == null) return;".
            var nullableLookup = new Regex(@"\bstring\?\s+\w+\s*\([^)]*\[CallerFilePath\]");
            // An existence check on a file or directory whose "missing" branch is a bare return.
            var silentReturn = new Regex(@"if\s*\(\s*!\s*(File|Directory)\.Exists\([^;{]*\)\s*return\b");
            // An existence check used as a conditional whose "missing" arm is null.
            var nullWhenMissing = new Regex(@"(File|Directory)\.Exists\([^;]*\?[^;]*:\s*null\s*;");

            var testDir = Path.Combine(RepoSourceFile.RepoRoot, "VoiceLite", "VoiceLite.Tests");
            var files = Directory.GetFiles(testDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .ToList();
            Assert.True(files.Count > 20, $"only {files.Count} test sources found under {testDir}");

            var offenders = files
                .Where(f =>
                {
                    var text = File.ReadAllText(f);
                    return text.Contains("CallerFilePath", StringComparison.Ordinal)
                           && (nullableLookup.IsMatch(text) || silentReturn.IsMatch(text) || nullWhenMissing.IsMatch(text));
                })
                .Select(f => Path.GetRelativePath(testDir, f))
                .ToList();

            Assert.True(offenders.Count == 0,
                "These test files look up source files in a way that can pass without reading anything " +
                "(use RepoSourceFile, which fails when the source is missing): " + string.Join(", ", offenders));
        }
    }
}

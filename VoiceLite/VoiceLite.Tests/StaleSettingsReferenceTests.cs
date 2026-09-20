using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// Error text must not send people to Settings tabs (or buttons) that no longer exist.
    /// The Settings window was rebuilt in adc86ac; "AI Models" and "General" went away.
    /// </summary>
    public sealed class StaleSettingsReferenceTests : IDisposable
    {
        private readonly string testRoot = Path.Combine(Path.GetTempPath(), $"VoiceLite-StaleRefs-{Guid.NewGuid():N}");

        public void Dispose()
        {
            try { if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true); } catch { }
        }

        [Fact]
        public void MissingParakeetModel_MessageGivesAdviceThatWorks()
        {
            // Both directories are empty temp folders, so the real %LOCALAPPDATA% model is never probed.
            var resolver = new ModelResolverService(
                Path.Combine(testRoot, "app"),
                proFeatureService: null,
                localAppDataDirectory: Path.Combine(testRoot, "local"));

            var ex = Assert.Throws<FileNotFoundException>(() => resolver.ResolveModelPath());

            ex.Message.Should().NotContain("AI Models", "Settings has no such tab and cannot re-download Parakeet");
            ex.Message.Should().Contain("Exit VoiceLite from the tray icon",
                "closing the window only hides it; the download gate re-runs only on a real restart");
            ex.Message.Should().ContainEquivalentOf("model",
                "DictationController passes the message through verbatim only when it mentions the model");
            ex.Message.Should().Contain(Path.Combine(testRoot, "local"), "candidate paths stay in the message for the log");
        }

        [Fact]
        public void MissingTranslationModel_MessagePointsAtTheTranscriptionTab()
        {
            var resolver = new TranslationModelResolverService(
                Path.Combine(testRoot, "app"),
                Path.Combine(testRoot, "local"));

            var ex = Assert.Throws<FileNotFoundException>(() => resolver.ResolveModelPath());

            ex.Message.Should().Contain("Settings > Transcription");
            ex.Message.Should().NotContain("General");
            ex.Message.Should().Contain("Install translation model");
        }

        // ---- source tripwires (a missing source tree FAILS them: SourceTripwirePolicyTests)

        private static string AppSourceDir() => RepoSourceFile.AppDir;

        private static IEnumerable<string> AppCsFiles(string appDir) =>
            Directory.GetFiles(appDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

        private static List<string> TabHeaders(string xaml) =>
            // Header may be any attribute of the tag, on any line - SettingsWindow.xaml belongs to someone else.
            Regex.Matches(xaml, @"<TabItem(?![\w.:])[^>]*?\sHeader=""([^""]+)""")
                .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))
                .ToList();

        // Spaces around the arrow are required: skips generics like Deserialize<Settings>(json).
        // Whole-line comments are dropped: history notes may name tabs that are gone.
        private static List<Match> SettingsTabReferences(string source) =>
            Regex.Matches(Regex.Replace(source, @"(?m)^[ \t]*//[^\r\n]*", ""), @"Settings\s+(?:>|→)\s+([^""\r\n]{1,40})").ToList();

        [Fact]
        public void TabHeaders_FindsHeaderWhereverItSitsInTheTag()
        {
            var xaml = @"<TabControl>
  <TabItem Header=""Transcription"">
  <TabItem x:Name=""HotkeyTab"" Header=""Hotkey &amp; mode"" Style=""{StaticResource T}"">
  <TabItem
      x:Name=""AboutTab""
      Header=""About"" />
  <TabItemExtra Header=""NotATab"" />
</TabControl>";

            TabHeaders(xaml).Should().Equal("Transcription", "Hotkey & mode", "About");
        }

        [Fact]
        public void SettingsTabReferences_IgnoresCommentLines()
        {
            var source = @"// was: Settings > General before adc86ac
    /// see Settings > AI Models
var s = ""Open Settings > Transcription"";
";

            SettingsTabReferences(source).Select(m => m.Groups[1].Value).Should().Equal("Transcription");
        }

        [Fact]
        public void Tripwire_EverySettingsTabNamedInCode_ExistsInSettingsWindow()
        {
            var appDir = AppSourceDir();

            var xaml = File.ReadAllText(Path.Combine(appDir, "SettingsWindow.xaml"));
            var tabs = TabHeaders(xaml);
            tabs.Should().Contain("Transcription").And.Contain("Hotkey & mode");

            var stale = new List<string>();
            foreach (var file in AppCsFiles(appDir))
            {
                foreach (Match m in SettingsTabReferences(File.ReadAllText(file)))
                {
                    var rest = m.Groups[1].Value;
                    if (!tabs.Any(t => rest.StartsWith(t, StringComparison.Ordinal)))
                        stale.Add($"{Path.GetFileName(file)}: \"{m.Value.Trim()}\"");
                }
            }

            stale.Should().BeEmpty("these strings name a Settings tab that SettingsWindow.xaml does not have");
        }

        [Fact]
        public void Tripwire_NoMessageMentionsManualButtons()
        {
            var appDir = AppSourceDir();

            var offenders = AppCsFiles(appDir)
                .Where(f => File.ReadAllText(f).Contains("manual buttons", StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFileName)
                .ToList();

            offenders.Should().BeEmpty("the main window has no record button; the hotkey is the only way to dictate");
        }
    }
}

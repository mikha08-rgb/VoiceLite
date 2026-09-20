using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// Regression test for the v2.5.0 updater bug: UpdateWindow.xaml referenced
    /// {StaticResource SecondaryButtonStyle} after Resources/ModernStyles.xaml was deleted.
    /// StaticResource keys aren't checked at compile time, so the window threw
    /// XamlParseException on open and the update banner silently did nothing.
    /// </summary>
    public class XamlResourceKeyTests
    {
        private static readonly Regex KeyDefinition = new(@"x:Key=""([^""]+)""", RegexOptions.Compiled);
        // {StaticResource Key}, {StaticResource ResourceKey=Key} and <StaticResource ResourceKey="Key"/>.
        private static readonly Regex KeyReference = new(
            @"\{(?:StaticResource|DynamicResource)\s+(?:ResourceKey\s*=\s*)?([A-Za-z0-9_]+)\s*\}"
            + @"|<(?:StaticResource|DynamicResource)\s+ResourceKey=""([A-Za-z0-9_]+)""",
            RegexOptions.Compiled);
        private static readonly Regex MergedDictionary = new(@"Source=""([^""]+\.xaml)""", RegexOptions.Compiled);

        private static string AppSourceDir([CallerFilePath] string thisFile = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "VoiceLite"));

        [Fact]
        public void EveryResourceReference_ResolvesToADefinedKey()
        {
            var appDir = AppSourceDir();
            if (!Directory.Exists(appDir))
                return; // test binaries copied away from the source tree

            var xamlFiles = Directory.GetFiles(appDir, "*.xaml", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .ToList();
            Assert.NotEmpty(xamlFiles);

            // The scan is recursive, so every XAML file is covered - pin that, so a change to
            // the enumeration (or a moved file) cannot silently drop a window from the scan.
            var scanned = xamlFiles
                .Select(f => Path.GetRelativePath(appDir, f).Replace(Path.DirectorySeparatorChar, '/'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var expected in new[]
                     {
                         "App.xaml", "MainWindow.xaml", "SettingsWindow.xaml", "UpdateWindow.xaml",
                         "ShortcutEditDialog.xaml", "Controls/ModelDownloadControl.xaml",
                     })
            {
                Assert.True(scanned.Contains(expected), $"resource-key scan did not see {expected}");
            }

            var appKeys = KeysVisibleFrom(Path.Combine(appDir, "App.xaml"), appDir);
            var unresolved = new List<string>();

            foreach (var file in xamlFiles)
            {
                var visible = new HashSet<string>(appKeys);
                visible.UnionWith(KeysVisibleFrom(file, appDir));

                foreach (Match reference in KeyReference.Matches(File.ReadAllText(file)))
                {
                    var key = reference.Groups[1].Success ? reference.Groups[1].Value : reference.Groups[2].Value;
                    if (!visible.Contains(key))
                        unresolved.Add($"{Path.GetFileName(file)}: '{key}'");
                }
            }

            Assert.True(unresolved.Count == 0,
                "XAML references resource keys that are defined nowhere (window will throw on open): "
                + string.Join(", ", unresolved));
        }

        [Fact]
        public void UpdateWindow_CanBeConstructed()
        {
            // InitializeComponent is where a bad StaticResource throws; WPF needs an STA thread.
            Exception? failure = null;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    var info = new VoiceLite.Services.UpdateInfo
                    {
                        Version = "9.9.9",
                        DownloadUrl = "https://example.invalid/VoiceLite-Setup-9.9.9.exe",
                        ReleasePageUrl = "https://example.invalid/releases/v9.9.9",
                    };
                    new VoiceLite.UpdateWindow(info, () => false).Close();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.True(failure == null, $"UpdateWindow failed to construct: {failure}");
        }

        // Keys defined in the file itself plus any dictionaries it merges.
        private static HashSet<string> KeysVisibleFrom(string xamlPath, string appDir)
        {
            var keys = new HashSet<string>();
            if (!File.Exists(xamlPath))
                return keys;

            var text = File.ReadAllText(xamlPath);
            foreach (Match m in KeyDefinition.Matches(text))
                keys.Add(m.Groups[1].Value);

            foreach (Match m in MergedDictionary.Matches(text))
            {
                var relative = m.Groups[1].Value.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var merged = new[] { Path.Combine(Path.GetDirectoryName(xamlPath)!, relative), Path.Combine(appDir, relative) }
                    .FirstOrDefault(File.Exists);
                if (merged != null && !string.Equals(merged, xamlPath, StringComparison.OrdinalIgnoreCase))
                    keys.UnionWith(KeysVisibleFrom(merged, appDir));
            }
            return keys;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Xaml;
using System.Xml.Linq;
using VoiceLite.Controls;
using VoiceLite.Models;
using VoiceLite.Services;
using VoiceLite.Utilities;
using Xunit;
using XamlReader = System.Windows.Markup.XamlReader;

namespace VoiceLite.Tests
{
    /// <summary>
    /// "Construct every window." The v2.5.0 outage was a window that threw on open while the
    /// whole suite was green (two branches that each passed collided at merge: one deleted a
    /// XAML style the other used). XamlResourceKeyTests scans the keys; this class really
    /// opens the windows: constructor -> BAML parse -> StaticResource resolution -> Show()
    /// off-screen (templates applied, layout run, Loaded handlers fired) -> Close().
    ///
    /// Rules of this class:
    /// - NO System.Windows.Application is ever created (same as UpdateWindowFlowTests): App.xaml
    ///   has no resources, so nothing needs one, and without an Application there is no
    ///   ShutdownMode to trip - closing the last test window cannot put the process-wide
    ///   Application into shutdown and poison later tests. Each test owns an STA thread and
    ///   shuts down only that thread's Dispatcher.
    /// - Windows are shown at -32000,-32000 with ShowInTaskbar=false and ShowActivated=false,
    ///   so nothing flashes on a developer's screen or steals focus.
    /// - Nothing here may change real machine state. What each type's constructor/Loaded
    ///   handler touches is documented on its test. MainWindow is NOT constructed - see
    ///   <see cref="WindowXaml_LoadsAndShowsWithoutCodeBehind"/>.
    /// - Same collection as the tests that swap the real license.dat in and out
    ///   (LicenseFileGuard): SettingsWindow's constructor reads that file, and must never do
    ///   so while it is temporarily replaced by a test key.
    /// </summary>
    [Collection(RealLicenseFileCollection.Name)]
    public class WindowConstructionTests
    {
        // ------------------------------------------------------------------ inventory

        /// <summary>Every XAML file with a code-behind class, and how this class covers it.</summary>
        private static readonly Dictionary<string, string> KnownXamlClasses = new()
        {
            ["VoiceLite.App"] = "App.xaml: no resources, no UI; never instantiated in tests (one Application per process)",
            ["VoiceLite.MainWindow"] = "NOT constructed (writes settings, tray icon, hotkey, microphone, recognizer) - loose-XAML load only",
            ["VoiceLite.SettingsWindow"] = "constructed + shown, every tab realized",
            ["VoiceLite.UpdateWindow"] = "constructed + shown, all three constructors",
            ["VoiceLite.ShortcutEditDialog"] = "constructed + shown, both constructors",
            ["VoiceLite.Controls.ModelDownloadControl"] = "constructed + shown in both code-built host windows, isolated model directories",
            ["VoiceLite.RecordingIndicatorWindow"] = "constructed + shown, walked through recording -> processing -> hidden, no-activate styles asserted",
        };

        [Fact]
        public void EveryXamlClass_HasAConstructionTestInThisFile()
        {
            var appDir = RequireAppSourceDir();
            var classes = AppXamlFiles(appDir)
                .Select(f => Regex.Match(File.ReadAllText(f), @"x:Class=""([^""]+)""").Groups[1].Value)
                .Where(c => c.Length > 0)
                .OrderBy(c => c)
                .ToList();

            var uncovered = classes.Except(KnownXamlClasses.Keys).ToList();
            var stale = KnownXamlClasses.Keys.Except(classes).ToList();

            Assert.True(uncovered.Count == 0,
                "New XAML class without an open-the-window test (add one to WindowConstructionTests, "
                + "then list it in KnownXamlClasses): " + string.Join(", ", uncovered));
            Assert.True(stale.Count == 0,
                "KnownXamlClasses lists XAML classes that no longer exist: " + string.Join(", ", stale));
        }

        [Fact]
        public void EveryUiTypeInTheAppAssembly_IsAKnownXamlClass()
        {
            // Catches a Window/UserControl/custom control written without XAML (the XAML
            // inventory above would not see it).
            var uiTypes = typeof(VoiceLite.UpdateWindow).Assembly.GetTypes()
                .Where(t => typeof(FrameworkElement).IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t => t.FullName!)
                .ToList();

            var unknown = uiTypes.Except(KnownXamlClasses.Keys).ToList();
            Assert.True(unknown.Count == 0,
                "UI types with no open-the-window test in WindowConstructionTests: " + string.Join(", ", unknown));
        }

        [Fact]
        public void CodeBuiltWindows_AreOnlyTheTwoModelDownloadHosts()
        {
            // "new Window { ... }" has no XAML and no class of its own, so neither inventory
            // above sees it. Today there are exactly two, both hosting ModelDownloadControl
            // (first-launch setup in App.xaml.cs, translation add-on in SettingsWindow.xaml.cs);
            // ModelDownloadControl_* below builds the same hosts. A third one needs a test here.
            var appDir = RequireAppSourceDir();
            var sites = Directory.GetFiles(appDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !IsBuildOutput(f))
                .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"new\s+Window\b(?!\w)")
                    .Select(_ => Path.GetFileName(f)))
                .OrderBy(n => n)
                .ToList();

            Assert.Equal(new[] { "App.xaml.cs", "SettingsWindow.xaml.cs" }, sites);
        }

        // ------------------------------------------------------------------ UpdateWindow

        private static UpdateInfo Update(bool verifiable) => new()
        {
            Version = "9.9.9",
            DownloadUrl = "https://example.invalid/VoiceLite-Setup-9.9.9.exe",
            ReleasePageUrl = "https://example.invalid/releases/v9.9.9",
            AssetName = verifiable ? "VoiceLite-Setup-9.9.9.exe" : null,
            Sha256 = verifiable ? "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF" : null,
            Sha256Source = verifiable ? "digest" : null,
        };

        // Side effects read before writing these: the constructors only set text
        // (UpdateCheckService.GetCurrentVersion is reflection). Download/install start on a
        // button click, never on construction or Loaded; there is no Loaded handler. Closing
        // only cancels a download that was never started.

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void UpdateWindow_PublicTwoArgConstructor_OpensAndCloses(bool verifiable) => RunOnSta(() =>
        {
            var window = new VoiceLite.UpdateWindow(Update(verifiable), () => false);
            ShowOffScreenThenClose(window);
            Assert.Equal(UpdateWindowResult.Later, window.Result); // closed without a decision
        });

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void UpdateWindow_PublicFiveArgConstructor_OpensAndCloses(bool verifiable) => RunOnSta(() =>
        {
            // The overload MainWindow.UpdateBanner_Click uses.
            var hotkeyCalls = 0;
            var window = new VoiceLite.UpdateWindow(Update(verifiable), () => false, () => false,
                () => hotkeyCalls++, () => hotkeyCalls++);
            ShowOffScreenThenClose(window);
            Assert.Equal(0, hotkeyCalls); // opening and closing never blocks the hotkey
        });

        [Fact]
        public void UpdateWindow_InternalSeamConstructor_OpensAndCloses() => RunOnSta(() =>
        {
            var touched = false;
            var window = new VoiceLite.UpdateWindow(Update(verifiable: true), () => false, () => false,
                () => { }, () => { },
                download: (_, _, _) => { touched = true; return System.Threading.Tasks.Task.FromResult(""); },
                install: _ => { touched = true; return System.Threading.Tasks.Task.CompletedTask; });
            ShowOffScreenThenClose(window);
            Assert.False(touched, "opening the update dialog must not start a download or an install");
        });

        // ------------------------------------------------------------------ ShortcutEditDialog

        // Side effects: none (pure form; no Loaded handler; MessageBox only on Save click).

        [Fact]
        public void ShortcutEditDialog_AddConstructor_OpensAndCloses() => RunOnSta(() =>
        {
            ShowOffScreenThenClose(new VoiceLite.ShortcutEditDialog(new List<CustomShortcut>()));
        });

        [Fact]
        public void ShortcutEditDialog_EditConstructor_OpensAndCloses() => RunOnSta(() =>
        {
            var existing = new CustomShortcut { Trigger = "brb", Replacement = "be right back", IsEnabled = true };
            var dialog = new VoiceLite.ShortcutEditDialog(existing, new List<CustomShortcut> { existing });
            ShowOffScreenThenClose(dialog, whileOpen: () =>
            {
                Assert.Equal("brb", ((TextBox)dialog.FindName("TriggerTextBox")).Text);
                Assert.True(((Button)dialog.FindName("SaveButton")).IsEnabled, "an unchanged valid shortcut must be saveable");
            });
        });

        // ------------------------------------------------------------------ SettingsWindow

        // Side effects read before writing these:
        // - new LicenseService(): READS the real %LOCALAPPDATA%\VoiceLite\license.dat (allowed).
        //   It writes only when migrating a pre-entropy license.dat or a plaintext key left in
        //   settings.json - both already done by any VoiceLite that has started on this machine.
        //   The shared collection keeps this read away from LicenseFileGuard's swapped-out file.
        // - Settings are passed in (a fresh Settings object); settings.json is not read, and the
        //   save callback only runs on Save/Apply/activation clicks, which these tests never raise.
        // - Hotkey: HotkeyManager.CanRegisterHotkey (a real RegisterHotKey probe) runs at
        //   construction only when the app does NOT own the saved hotkey; the tests pass
        //   hotkeyRegistered: () => true, so no probe.
        // - Microphone: construction only ENUMERATES devices (WaveInEvent.GetCapabilities).
        //   The level meter - the only thing that opens a device - starts from
        //   LevelTrack.IsVisibleChanged, queued at DispatcherPriority.ApplicationIdle, and
        //   StartMicMonitor refuses while LevelTrack is not visible. The window opens on the
        //   Hotkey tab, so opening it never touches the microphone. The tab walk below does put
        //   the Microphone tab on screen, but Pump() drains the queue only down to Background
        //   priority and leaves the tab before ApplicationIdle is ever reached; the queued start
        //   then finds LevelTrack invisible and is a no-op. MicMonitorNeverStarted asserts it.

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SettingsWindow_OpensEveryTabAndCloses(bool pro) => RunOnSta(() =>
        {
            var settings = new Settings { IsProLicense = pro };
            settings.CustomShortcuts.Add(new CustomShortcut { Trigger = "brb", Replacement = "be right back" });
            settings.CustomDictionary.Add(new CustomDictionaryEntry { Spoken = "github", Written = "GitHub" });
            var saves = 0;

            var window = new VoiceLite.SettingsWindow(settings, () => saves++, () => true);
            ShowOffScreenThenClose(window, whileOpen: () =>
            {
                var tabs = (TabControl)window.FindName("SettingsTabs");
                Assert.True(tabs.Items.Count >= 7, "expected the seven settings tabs");

                // A TabControl only builds the selected tab's visual tree: walk them all so every
                // template in the window is applied once.
                foreach (var tab in tabs.Items.OfType<TabItem>().ToList())
                {
                    tabs.SelectedItem = tab;
                    Pump();
                    window.UpdateLayout();
                    AssertMicMonitorNeverStarted(window, $"after showing tab '{tab.Header}'");
                }
                tabs.SelectedIndex = 0;
                Pump();

                var dictionaryTab = (TabItem)window.FindName("CustomDictionaryTab");
                Assert.Equal(pro ? Visibility.Visible : Visibility.Collapsed, dictionaryTab.Visibility);
            });

            AssertMicMonitorNeverStarted(window, "after close");
            Assert.Equal(0, saves); // open + browse + close persists nothing
            Assert.False(window.ChangesCommitted);
        });

        private static void AssertMicMonitorNeverStarted(Window settingsWindow, string when)
        {
            var field = typeof(VoiceLite.SettingsWindow).GetField("micMonitor",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.True(field != null, "SettingsWindow.micMonitor was renamed - update this guard");
            Assert.True(field!.GetValue(settingsWindow) == null,
                $"this test opened the microphone ({when}); the level meter must only start from the idle-priority callback");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SettingsWindow_RecordingIndicatorCheckBox_LoadsFromSettings_AndWritesBackToTheDraft(bool initial) => RunOnSta(() =>
        {
            // Same side effects as above (constructed, never shown, nothing saved).
            var live = new Settings { ShowRecordingIndicator = initial };
            var window = new VoiceLite.SettingsWindow(live, () => { }, () => true);
            try
            {
                var box = (CheckBox)window.FindName("ShowRecordingIndicatorCheckBox");
                Assert.True(box != null, "ShowRecordingIndicatorCheckBox is gone from SettingsWindow.xaml");
                Assert.Equal("Show recording indicator while dictating", box!.Content);
                Assert.Equal(initial, box.IsChecked); // LoadSettings

                box.IsChecked = !initial;
                const System.Reflection.BindingFlags priv = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var readUi = typeof(VoiceLite.SettingsWindow).GetMethod("ReadUiIntoDraft", priv);
                var draftField = typeof(VoiceLite.SettingsWindow).GetField("settings", priv);
                Assert.True(readUi != null && draftField != null, "SettingsWindow.ReadUiIntoDraft / settings were renamed - update this test");
                readUi!.Invoke(window, null);

                Assert.Equal(!initial, ((Settings)draftField!.GetValue(window)!).ShowRecordingIndicator);
                Assert.Equal(initial, live.ShowRecordingIndicator); // not committed: live untouched
            }
            finally
            {
                window.Close();
            }
        });

        // ------------------------------------------------------------------ RecordingIndicatorWindow

        // Side effects: none beyond its own HWND. PlaceOnShow=false keeps it at the harness's
        // off-screen position (production moves it to the bottom-centre of the active monitor
        // with SetWindowPos - that would flash a pill on the developer's screen). No Application,
        // no owner, no timers; the render tick (CompositionTarget.Rendering) is unhooked on
        // hide and on close.

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [Fact]
        public void RecordingIndicatorWindow_NeverTakesActivation_AndWalksThroughItsStates() => RunOnSta(() =>
        {
            Assert.True(IntPtr.Size == 8, "this test reads the extended style with GetWindowLongPtrW (64-bit test host expected)");

            float level = 0.3f;
            var window = new VoiceLite.RecordingIndicatorWindow(() => level) { PlaceOnShow = false };

            // Asserted on the window AS BUILT - the harness below sets some of these itself.
            Assert.False(window.ShowActivated, "the pill must never be activated: dictated text is pasted into the foreground window");
            Assert.False(window.Focusable);
            Assert.False(window.ShowInTaskbar);
            Assert.True(window.Topmost);
            Assert.True(window.AllowsTransparency);
            Assert.Equal(WindowStyle.None, window.WindowStyle);
            Assert.False(window.IsHitTestVisible);
            Assert.Null(window.Owner); // an owned window would vanish with a MainWindow minimized to the tray
            Assert.Equal(RecordingIndicatorLogic.PillWidth + 2 * RecordingIndicatorLogic.ShadowPadding, window.Width);
            Assert.Equal(RecordingIndicatorLogic.PillHeight + 2 * RecordingIndicatorLogic.ShadowPadding, window.Height);

            var bars = ((Panel)window.FindName("Bars")).Children.OfType<System.Windows.Shapes.Rectangle>().ToList();
            Assert.Equal(RecordingIndicatorLogic.BarCount, bars.Count);
            var centre = bars[bars.Count / 2];
            var processingDot = (FrameworkElement)window.FindName("ProcessingDot");
            var root = (FrameworkElement)window.FindName("Root");

            ShowOffScreenThenClose(window, whileOpen: () =>
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                long exStyle = GetWindowLongPtr(hwnd, -20 /* GWL_EXSTYLE */).ToInt64();
                const long WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000, WS_EX_LAYERED = 0x80000;
                Assert.True((exStyle & WS_EX_NOACTIVATE) != 0, "WS_EX_NOACTIVATE missing");
                Assert.True((exStyle & WS_EX_TRANSPARENT) != 0, "WS_EX_TRANSPARENT (click-through) missing");
                Assert.True((exStyle & WS_EX_LAYERED) != 0, "WS_EX_LAYERED missing - WS_EX_TRANSPARENT is only click-through on a layered window");
                Assert.True((exStyle & WS_EX_TOOLWINDOW) != 0, "WS_EX_TOOLWINDOW (no Alt+Tab entry) missing");
                Assert.Equal(VoiceLite.RecordingIndicatorWindow.RequiredExtendedStyles, exStyle & VoiceLite.RecordingIndicatorWindow.RequiredExtendedStyles);

                window.ShowState(RecordingIndicatorState.Recording);
                PumpUntil(() => root.Opacity > 0.99 && centre.Height > RecordingIndicatorLogic.MinBarHeight + 2,
                    "recording: the pill never faded in / the bars never followed the level");
                // Smoke only: Windows never hands the foreground to a background test host, so these
                // two would pass without the styles too. The style bits and ShowActivated/Focusable
                // asserted above are the real no-activation coverage.
                Assert.False(window.IsActive);
                Assert.NotEqual(hwnd, GetForegroundWindow());

                level = 0f;
                PumpUntil(() => bars.All(b => b.Height < RecordingIndicatorLogic.MinBarHeight + 0.5),
                    "silence: the bars never came back to rest");

                window.ShowState(RecordingIndicatorState.Processing);
                PumpUntil(() => processingDot.Opacity > 0.99, "processing: the dot never changed");
                Assert.All(bars, b => Assert.InRange(b.Height, RecordingIndicatorLogic.MinBarHeight, RecordingIndicatorLogic.MaxBarHeight / 2));

                window.HideIndicator();
                PumpUntil(() => !window.IsVisible, "the pill never hid after its fade-out");

                // Shown again for the next dictation: same window, still not activated.
                level = 0.3f;
                window.ShowState(RecordingIndicatorState.Recording);
                PumpUntil(() => window.IsVisible && root.Opacity > 0.99 && processingDot.Opacity < 0.01,
                    "second dictation: the pill never came back in its recording look");
                Assert.False(window.IsActive);
                Assert.NotEqual(hwnd, GetForegroundWindow());

                // Hide, then show before the fade-out finished: must stay up.
                window.HideIndicator();
                window.ShowState(RecordingIndicatorState.Processing);
                for (int i = 0; i < 15; i++) { Thread.Sleep(20); Pump(); }
                Assert.True(window.IsVisible, "a show during the fade-out was undone by the fade-out's completion");
            });
        });

        [Theory]
        [InlineData(true)]  // the fade-out cannot even be started
        [InlineData(false)] // it starts, but its Completed never arrives (rendering suspended: locked session, RDP disconnect)
        public void RecordingIndicatorWindow_HidesEvenWhenItsFadeOutFails(bool fadeThrows) => RunOnSta(() =>
        {
            var window = new VoiceLite.RecordingIndicatorWindow(() => 0f) { PlaceOnShow = false };
            var ticking = typeof(VoiceLite.RecordingIndicatorWindow).GetField("ticking",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.True(ticking != null, "RecordingIndicatorWindow.ticking was renamed - update this test");

            ShowOffScreenThenClose(window, whileOpen: () =>
            {
                window.ShowState(RecordingIndicatorState.Recording);
                Assert.True(window.IsVisible);
                Assert.True((bool)ticking!.GetValue(window)!);

                window.StartFadeOut = fadeThrows
                    ? _ => throw new InvalidOperationException("animation system broke")
                    : _ => { };

                if (fadeThrows)
                {
                    window.HideIndicator(); // must not throw either
                    Assert.False(window.IsVisible, "a fade-out that cannot start must hide the pill at once");
                }
                else
                {
                    window.HideIndicator();
                    PumpUntil(() => !window.IsVisible, "a fade-out that never completes left the pill on screen");
                }
                Assert.False((bool)ticking.GetValue(window)!, "the 60 fps render tick stayed hooked on a hidden pill");

                // The fallback must not undo a later show.
                window.StartFadeOut = _ => { };
                window.ShowState(RecordingIndicatorState.Recording);
                window.HideIndicator();
                window.ShowState(RecordingIndicatorState.Processing);
                for (int i = 0; i < 30; i++) { Thread.Sleep(20); Pump(); }
                Assert.True(window.IsVisible, "the hide fallback fired after the pill was shown again");
            });
        });

        private static void PumpUntil(Func<bool> condition, string because)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!condition())
            {
                Assert.True(DateTime.UtcNow < deadline, because);
                Thread.Sleep(15);
                Pump();
            }
        }

        // ------------------------------------------------------------------ ModelDownloadControl

        // Side effects read before writing these: the PUBLIC constructor repairs interrupted
        // installs (directory deletes/renames) under the real %LOCALAPPDATA%\VoiceLite\models
        // and sweeps voicelite tarballs out of %TEMP% - it could delete the temp directory of a
        // download a running VoiceLite has in flight. So the tests use the internal seam
        // constructor with isolated directories and the sweep off; everything else (XAML, item
        // templates, Loaded handler, recovery -> InstallCompleted) is the production path.
        // Network starts on the Download button only.

        [Fact]
        public void ModelDownloadControl_FirstLaunchHost_NothingInstalled_OpensAndCloses() => RunOnSta(() =>
        {
            using var dirs = new IsolatedModelDirs();
            var control = dirs.NewControl();
            control.Initialize(null, null); // exactly what App.ShowFirstLaunchModelDownload does
            var completed = 0;
            control.InstallCompleted += (_, _) => completed++;

            ShowOffScreenThenClose(HostLikeTheApp(control, "VoiceLite — First-Launch Setup", 720, 480), whileOpen: () =>
            {
                Assert.False(control.IsModelInstalled);
                Assert.Single(((ItemsControl)control.FindName("ModelList")).Items);
            });
            Assert.Equal(0, completed);
        });

        [Fact]
        public void ModelDownloadControl_InstalledWithSettingsAndTranslationRow_OpensAndCloses() => RunOnSta(() =>
        {
            using var dirs = new IsolatedModelDirs();
            dirs.FakeInstall(ModelResolverService.ParakeetModelId, dirs.Parakeet);
            var control = dirs.NewControl();
            var saves = 0;
            control.Initialize(new Settings(), () => saves++, includeTranslationModel: true);

            ShowOffScreenThenClose(HostLikeTheApp(control, "host", 720, 480), whileOpen: () =>
            {
                Assert.True(control.IsModelInstalled);
                Assert.Equal(2, ((ItemsControl)control.FindName("ModelList")).Items.Count);
            });
        });

        [Fact]
        public void ModelDownloadControl_TranslationOnlyHost_OpensAndCloses() => RunOnSta(() =>
        {
            using var dirs = new IsolatedModelDirs();
            var control = dirs.NewControl();
            control.ShowTranslationModelOnly(); // exactly what SettingsWindow's install button does

            ShowOffScreenThenClose(HostLikeTheApp(control, "VoiceLite — Translation Model", 720, 430), whileOpen: () =>
                Assert.Single(((ItemsControl)control.FindName("ModelList")).Items));
        });

        [Fact]
        public void ModelDownloadControl_RecoveredInstall_LoadedHandlerReportsCompletion() => RunOnSta(() =>
        {
            // Process died between the swap renames: a complete extraction is waiting next to
            // the (missing) live directory. The constructor promotes it and the Loaded handler
            // tells the host, which is how first-launch continues without a second download.
            using var dirs = new IsolatedModelDirs();
            dirs.FakeInstall(ModelResolverService.ParakeetModelId, dirs.Parakeet + ".tmp-0123456789abcdef");
            var control = dirs.NewControl();
            control.Initialize(null, null);
            var completed = 0;
            control.InstallCompleted += (_, _) => completed++;

            ShowOffScreenThenClose(HostLikeTheApp(control, "host", 720, 480));

            Assert.Equal(1, completed);
            Assert.True(control.IsModelInstalled);
        });

        /// <summary>The two code-built hosts (App.ShowFirstLaunchModelDownload, SettingsWindow install button).</summary>
        private static Window HostLikeTheApp(ModelDownloadControl control, string title, double width, double height) => new()
        {
            Title = title,
            Width = width,
            Height = height,
            ResizeMode = ResizeMode.NoResize,
            Content = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(20),
                Content = control,
            },
        };

        private sealed class IsolatedModelDirs : IDisposable
        {
            private readonly string root = Path.Combine(Path.GetTempPath(), "voicelite-windowtests-" + Guid.NewGuid().ToString("N"));
            public string Parakeet => Path.Combine(root, "models", "parakeet");
            public string Translation => Path.Combine(root, "models", "translation");

            public IsolatedModelDirs() => Directory.CreateDirectory(Path.Combine(root, "models"));

            public ModelDownloadControl NewControl() => new(Parakeet, Translation, sweepStaleArchives: false);

            public void FakeInstall(string modelId, string directory)
            {
                Directory.CreateDirectory(directory);
                foreach (var file in DownloadEndpoints.GetManifestForFileName(modelId, directory).RequiredFiles)
                    File.WriteAllText(Path.Combine(directory, file), "x");
            }

            public void Dispose()
            {
                try { Directory.Delete(root, recursive: true); } catch { /* temp; best effort */ }
            }
        }

        // ------------------------------------------------------------------ MainWindow

        /// <summary>
        /// MainWindow is deliberately NOT constructed in the test process. Its constructor alone
        /// reads the real settings.json and can WRITE it (SettingsMigration / model validation /
        /// history cleanup all call SaveSettings, plus the Roaming -> Local migration deletes a
        /// folder), and its Loaded handler creates every service: the ~600 MB recognizer, the
        /// audio recorder, the tray icon, the global hotkey (which would fight a running
        /// VoiceLite for the same combination) and the update check (network).
        ///
        /// Application.LoadComponent / the BAML cannot be used either: the BAML root IS
        /// VoiceLite.MainWindow, so loading it runs that same constructor. What can be covered
        /// without the code-behind is the markup itself: the source XAML is loaded as loose XAML
        /// (x:Class and the event-handler attributes removed, which loose XAML forbids) into a
        /// plain Window and shown off-screen - real WPF parser, real StaticResource resolution,
        /// converters instantiated from the app assembly, templates applied, layout run.
        /// NOT covered for MainWindow: its constructor, Loaded handler and code-behind
        /// (FindName/x:Name fields are compile-checked; handler names are compile-checked).
        ///
        /// The same loose load runs for every other window too (cheap second opinion that does
        /// not depend on constructor arguments).
        /// </summary>
        [Theory]
        [InlineData("MainWindow.xaml")]
        [InlineData("RecordingIndicatorWindow.xaml")]
        [InlineData("SettingsWindow.xaml")]
        [InlineData("UpdateWindow.xaml")]
        [InlineData("ShortcutEditDialog.xaml")]
        [InlineData("Controls/ModelDownloadControl.xaml")]
        public void WindowXaml_LoadsAndShowsWithoutCodeBehind(string relativeXamlPath) => RunOnSta(() =>
        {
            var appDir = RequireAppSourceDir();
            var root = LoadLooseXaml(Path.Combine(appDir, relativeXamlPath.Replace('/', Path.DirectorySeparatorChar)), relativeXamlPath);

            var window = root as Window ?? new Window { Width = 720, Height = 480, Content = root };
            ShowOffScreenThenClose(window, whileOpen: () =>
            {
                // Tab contents are built lazily; realize them all (no code-behind here, so
                // nothing can start the mic meter).
                foreach (var tabs in Descendants(window).OfType<TabControl>().ToList())
                {
                    foreach (var tab in tabs.Items.OfType<TabItem>().ToList())
                    {
                        tabs.SelectedItem = tab;
                        Pump();
                        window.UpdateLayout();
                    }
                }
            });
        });

        [Fact]
        public void LooseXamlTheory_ListsEveryWindowXamlFile()
        {
            var appDir = RequireAppSourceDir();
            var onDisk = AppXamlFiles(appDir)
                .Select(f => Path.GetRelativePath(appDir, f).Replace(Path.DirectorySeparatorChar, '/'))
                .Where(f => f != "App.xaml")
                .OrderBy(f => f)
                .ToList();

            var inTheory = typeof(WindowConstructionTests)
                .GetMethod(nameof(WindowXaml_LoadsAndShowsWithoutCodeBehind))!
                .GetCustomAttributesData()
                .Where(a => a.AttributeType == typeof(InlineDataAttribute))
                .Select(a => (string)((IReadOnlyCollection<System.Reflection.CustomAttributeTypedArgument>)a.ConstructorArguments[0].Value!).First().Value!)
                .OrderBy(f => f)
                .ToList();

            Assert.Equal(onDisk, inTheory);
        }

        /// <summary>
        /// Source XAML -> loose XAML: drops x:Class, drops every attribute the WPF schema says
        /// is an event (loose XAML cannot wire handlers), and points clr-namespace prefixes at
        /// the app assembly. Anything it misses makes XamlReader throw, i.e. fails loudly.
        /// </summary>
        private static object LoadLooseXaml(string path, string relativeXamlPath)
        {
            const string appAssembly = "VoiceLite";
            var text = File.ReadAllText(path);
            text = Regex.Replace(text, @"(xmlns:\w+=""clr-namespace:[^"";]+)""", $"$1;assembly={appAssembly}\"");

            var doc = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            doc.Root!.Attribute(x + "Class")?.Remove();

            var schema = XamlReader.GetWpfSchemaContext();
            XamlType? TypeOf(string ns, string name) => schema.GetXamlType(new System.Xaml.Schema.XamlTypeName(ns, name));

            foreach (var element in doc.Descendants().ToList())
            {
                if (element.Name.LocalName == "EventSetter")
                {
                    element.Remove();
                    continue;
                }
                if (element.Name.LocalName.Contains('.'))
                    continue; // property element

                foreach (var attribute in element.Attributes().ToList())
                {
                    if (attribute.IsNamespaceDeclaration || attribute.Name.Namespace != XNamespace.None)
                        continue;

                    var name = attribute.Name.LocalName;
                    XamlMember? member;
                    var dot = name.IndexOf('.');
                    if (dot < 0)
                    {
                        member = TypeOf(element.Name.NamespaceName, element.Name.LocalName)?.GetMember(name);
                    }
                    else
                    {
                        var owner = TypeOf(element.Name.NamespaceName, name[..dot]);
                        member = owner?.GetMember(name[(dot + 1)..]) ?? owner?.GetAttachableMember(name[(dot + 1)..]);
                    }

                    if (member?.IsEvent == true)
                        attribute.Remove();
                }
            }

            // Relative URIs (Icon="VoiceLite.ico") resolve the way they do from the compiled BAML.
            _ = System.IO.Packaging.PackUriHelper.UriSchemePack; // registers pack:// when no Application exists
            var directory = Path.GetDirectoryName(relativeXamlPath)?.Replace('\\', '/');
            var baseUri = $"pack://application:,,,/{appAssembly};component/" + (string.IsNullOrEmpty(directory) ? "" : directory + "/");
            return XamlReader.Parse(doc.ToString(SaveOptions.DisableFormatting), new ParserContext { BaseUri = new Uri(baseUri) });
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            {
                yield return child;
                foreach (var grandChild in Descendants(child))
                    yield return grandChild;
            }
        }

        // ------------------------------------------------------------------ harness

        private static string RequireAppSourceDir([CallerFilePath] string thisFile = "")
        {
            var dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "VoiceLite"));
            Assert.True(Directory.Exists(dir),
                $"App source tree not found at {dir}: these tests read the XAML sources and must run from the repository.");
            return dir;
        }

        private static bool IsBuildOutput(string path) =>
            path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
            || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}");

        private static List<string> AppXamlFiles(string appDir)
        {
            var files = Directory.GetFiles(appDir, "*.xaml", SearchOption.AllDirectories).Where(f => !IsBuildOutput(f)).ToList();
            Assert.NotEmpty(files);
            return files;
        }

        /// <summary>
        /// Show off-screen -> pump until Loaded + first layout have run -> optional checks -> Close.
        /// An exception thrown by a Loaded handler or a template surfaces out of Pump() (there is
        /// no Application to swallow it) and fails the test.
        /// </summary>
        private static void ShowOffScreenThenClose(Window window, Action? whileOpen = null)
        {
            var loaded = false;
            var closed = false;
            window.Loaded += (_, _) => loaded = true;
            window.Closed += (_, _) => closed = true;

            window.WindowStartupLocation = WindowStartupLocation.Manual; // CenterOwner/CenterScreen would undo Left/Top
            window.Left = -32000;
            window.Top = -32000;
            window.ShowInTaskbar = false;
            window.ShowActivated = false;

            try
            {
                window.Show();
                Pump();
                window.UpdateLayout();
                Assert.True(loaded, $"{window.GetType().Name} never raised Loaded");
                Assert.True(window.ActualWidth > 0 && window.ActualHeight > 0, "window was never laid out");
                whileOpen?.Invoke();
            }
            finally
            {
                if (!closed)
                    window.Close();
                Pump();
            }
            Assert.True(closed, $"{window.GetType().Name} refused to close");
        }

        /// <summary>
        /// Runs the dispatcher down to (and including) Background priority: Loaded, DataBind,
        /// Render and Input work is done, ApplicationIdle work (SettingsWindow's deferred mic
        /// meter start) is deliberately NOT reached.
        /// </summary>
        private static void Pump()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        private static void RunOnSta(Action body)
        {
            ExceptionDispatchInfo? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                    body();
                }
                catch (Exception ex)
                {
                    failure = ExceptionDispatchInfo.Capture(ex);
                }
                finally
                {
                    // This thread's dispatcher only - never Application shutdown (there is no Application).
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(60)),
                "the STA window thread hung - most likely a modal MessageBox from an error path (check voicelite.log)");
            failure?.Throw();
            Assert.True(Application.Current == null, "a window test must never create the process-wide Application");
        }
    }

    /// <summary>
    /// Serializes every test class that touches the REAL %LOCALAPPDATA%\VoiceLite\license.dat:
    /// LicenseServiceTests deletes/rewrites it under LicenseFileGuard, and anything that
    /// constructs a parameterless LicenseService meanwhile would load a test key - or take the
    /// "no license.dat -> migrate plaintext key out of settings.json" path and rewrite the
    /// developer's real settings.json.
    /// </summary>
    [CollectionDefinition(Name)]
    public class RealLicenseFileCollection
    {
        public const string Name = "Real license.dat";
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AwesomeAssertions;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// v2.5.2 hardening (S7b): opening Settings opened the microphone and held it for the
    /// life of the dialog even though the level meter lives on the second tab - mic-in-use
    /// indicator lit (and Bluetooth headsets dropped to hands-free) just to change a hotkey.
    /// The meter now follows the visibility of its own track element.
    /// SettingsWindow itself cannot be constructed here (its ctor reads the real license
    /// store and the fix opens real audio hardware), so this is a tripwire on the wiring plus
    /// a check of the WPF behaviour the wiring relies on. The end-to-end check is manual.
    /// </summary>
    public class SettingsWindowMicMonitorTests
    {
        [Fact]
        public void Wiring_MeterFollowsLevelTrackVisibility_NotWindowLoaded()
        {
            var code = SettingsWindowHotkeyTests.ReadAppSource("SettingsWindow.xaml.cs");
            if (code == null) return; // test binaries copied away from the source tree

            // Assert.True/False on Contains: a Should().Contain failure would dump the whole file.
            Assert.False(code.Contains("Loaded += (_, _) => StartMicMonitor();"),
                "that opened the microphone on every Settings open, whatever tab was showing");
            Assert.True(code.Contains("LevelTrack.IsVisibleChanged +="),
                "the meter must start/stop with the visibility of its track element");
            Assert.True(code.Contains("if (!LevelTrack.IsVisible)"),
                "StartMicMonitor must refuse to open the device while the meter is not on screen");
        }

        [Fact]
        public void WpfContract_ContentOfAnUnselectedTab_IsNotVisible_UntilTheTabIsSelected()
        {
            // The assumption behind the wiring: a TabControl only hosts the selected tab's
            // content, so an element on the second tab reports IsVisible=false until the user
            // goes there, and false again when they leave.
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                Window? window = null;
                try
                {
                    var meter = new Border { Width = 10, Height = 10 };
                    var changes = new List<bool>();
                    meter.IsVisibleChanged += (_, e) => changes.Add((bool)e.NewValue);

                    var tabs = new TabControl();
                    tabs.Items.Add(new TabItem { Header = "Hotkey", Content = new TextBlock { Text = "first" } });
                    tabs.Items.Add(new TabItem { Header = "Microphone", Content = new StackPanel { Children = { meter } } });
                    window = new Window
                    {
                        Content = tabs, Width = 200, Height = 120, Left = -10000, Top = -10000,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        ShowInTaskbar = false, ShowActivated = false, WindowStyle = WindowStyle.ToolWindow,
                    };
                    window.Show();
                    Pump();
                    meter.IsVisible.Should().BeFalse("the window opens on the first tab");
                    changes.Should().BeEmpty();

                    tabs.SelectedIndex = 1;
                    Pump();
                    meter.IsVisible.Should().BeTrue();

                    tabs.SelectedIndex = 0;
                    Pump();
                    meter.IsVisible.Should().BeFalse();
                    changes.Should().Equal(true, false);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    window?.Close();
                    Dispatcher.CurrentDispatcher.InvokeShutdown(); // this thread's dispatcher only
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.True(failure == null, failure?.ToString());
        }

        private static void Pump()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }
}

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// App.xaml.cs single-instance gate. The stale-copy check used to span every Windows
    /// session (blocking a second user on a shared PC / RDP host, or killing the first
    /// user's copy when elevated) and misread a fast double-launch as an "older copy".
    /// These cover the pure pieces; nothing here enumerates or kills real processes.
    /// </summary>
    public class StartupSingleInstanceTests
    {
        private const int SelfPid = 1000;
        private const int SelfSession = 2;
        private static readonly DateTime SelfStart = new(2026, 9, 19, 12, 0, 0);

        private static bool IsStale(int pid, int session, DateTime? start, bool hasExited = false) =>
            App.IsStaleInstanceCandidate(new App.InstanceInfo(pid, session, start, hasExited), SelfPid, SelfSession, SelfStart);

        [Fact]
        public void Self_IsNeverStale() =>
            Assert.False(IsStale(SelfPid, SelfSession, SelfStart.AddMinutes(-5)));

        [Fact]
        public void SameSession_StartedEarlier_IsStale() =>
            Assert.True(IsStale(2000, SelfSession, SelfStart.AddMinutes(-5)));

        [Fact]
        public void OtherSession_StartedEarlier_IsNotStale() =>
            Assert.False(IsStale(2000, SelfSession + 1, SelfStart.AddMinutes(-5)));

        [Fact]
        public void OtherSession_UnknownStart_IsNotStale() =>
            Assert.False(IsStale(2000, SelfSession + 1, null));

        [Fact]
        public void SameSession_UnknownStart_IsStale() =>
            // StartTime is unreadable for an elevated copy; dropping it would bring back the
            // hotkey collision this gate exists for.
            Assert.True(IsStale(2000, SelfSession, null));

        [Fact]
        public void SameSession_StartedLater_IsNotStale() =>
            // A second launch of this build: it will hit our mutex and exit on its own.
            Assert.False(IsStale(2000, SelfSession, SelfStart.AddMilliseconds(200)));

        [Fact]
        public void SameSession_StartedEarlier_ButExited_IsNotStale() =>
            Assert.False(IsStale(2000, SelfSession, SelfStart.AddMinutes(-5), hasExited: true));

        [Fact]
        public void Close_AlreadyExited_SucceedsWithoutKilling()
        {
            bool killed = false;
            Assert.True(App.TryCloseInstance(() => true, () => killed = true, _ => false));
            Assert.False(killed);
        }

        [Fact]
        public void Close_KillThrowsInvalidOperation_CountsAsGone() =>
            Assert.True(App.TryCloseInstance(
                () => false,
                () => throw new InvalidOperationException("No process is associated with this object."),
                _ => false));

        [Fact]
        public void Close_KillDenied_ButProcessExitedMeanwhile_CountsAsGone()
        {
            int hasExitedCalls = 0;
            Assert.True(App.TryCloseInstance(
                () => hasExitedCalls++ > 0,
                () => throw new Win32Exception(5),
                _ => false));
        }

        [Fact]
        public void Close_KillDenied_AndStillRunning_Fails() =>
            Assert.False(App.TryCloseInstance(() => false, () => throw new Win32Exception(5), _ => false));

        [Fact]
        public void Close_StillAliveAfterWait_Fails() =>
            Assert.False(App.TryCloseInstance(() => false, () => { }, _ => false));

        [Fact]
        public void Close_KilledAndExited_Succeeds() =>
            Assert.True(App.TryCloseInstance(() => false, () => { }, _ => true));

        [Fact]
        public void Close_HasExitedUnreadable_StillTriesKill()
        {
            // HasExited needs a handle and throws access-denied for an elevated copy.
            bool killed = false;
            Assert.True(App.TryCloseInstance(() => throw new Win32Exception(5), () => killed = true, _ => true));
            Assert.True(killed);
        }

        [Fact]
        public void Close_RealProcessThatAlreadyExited_Succeeds()
        {
            // The double-launch case: the second copy exits on its own while the prompt is up,
            // then the user clicks Yes. Uses a throwaway ping.exe child, observed through a
            // separate Process object like the one GetProcessesByName hands out.
            using var child = StartThrowawayChild(seconds: 30);
            using var observed = System.Diagnostics.Process.GetProcessById(child.Id);
            child.Kill();
            Assert.True(child.WaitForExit(10_000));

            Assert.True(App.TryCloseInstance(() => observed.HasExited, () => observed.Kill(), ms => observed.WaitForExit(ms)));
        }

        [Fact]
        public void Close_RealRunningProcess_KillsIt()
        {
            using var child = StartThrowawayChild(seconds: 30);
            using var observed = System.Diagnostics.Process.GetProcessById(child.Id);

            Assert.True(App.TryCloseInstance(() => observed.HasExited, () => observed.Kill(), ms => observed.WaitForExit(ms)));
            Assert.True(child.WaitForExit(10_000));
        }

        private static System.Diagnostics.Process StartThrowawayChild(int seconds) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                Path.Combine(Environment.SystemDirectory, "ping.exe"), $"-n {seconds} 127.0.0.1")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            })!;

        /// <summary>
        /// Tripwire only (startup order can't be exercised without launching the app): the
        /// show-window listener must exist before the process check, or a fast second launch
        /// finds no event, shows "already running", and is itself reported as an "older copy".
        /// </summary>
        [Fact]
        public void OnStartup_StartsShowWindowListener_BeforeProcessCheck()
        {
            var source = AppSourceFile();
            if (!File.Exists(source))
                return; // test binaries copied away from the source tree

            var text = File.ReadAllText(source);
            int listen = text.IndexOf("ListenForShowWindowSignal();", StringComparison.Ordinal);
            int check = text.IndexOf("if (!CloseOtherInstances())", StringComparison.Ordinal);
            Assert.True(listen >= 0, "ListenForShowWindowSignal() call not found in App.xaml.cs");
            Assert.True(check >= 0, "CloseOtherInstances() call not found in App.xaml.cs");
            Assert.True(listen < check, "ListenForShowWindowSignal() must run before CloseOtherInstances()");
        }

        private static string AppSourceFile([CallerFilePath] string thisFile = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "VoiceLite", "App.xaml.cs"));
    }
}

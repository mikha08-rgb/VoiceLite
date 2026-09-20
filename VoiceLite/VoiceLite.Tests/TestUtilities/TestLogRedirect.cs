using System;
using System.IO;
using System.Runtime.CompilerServices;
using VoiceLite.Services;

namespace VoiceLite.Tests.TestUtilities
{
    /// <summary>
    /// Points ErrorLogger at a throw-away directory for the WHOLE test assembly. Without it
    /// every test that drives product code appends fabricated lines ("installer exited before
    /// signalling ready", "shutting down VoiceLite for update", version 9.9.9 ...) to the real
    /// %LOCALAPPDATA%\VoiceLite\logs\voicelite.log - the log update problems are diagnosed from,
    /// possibly while the developer's own VoiceLite is writing to it - and can rotate it.
    ///
    /// A module initializer runs before any other code in this assembly, i.e. before anything
    /// here can touch ErrorLogger, whose static initializer reads the value exactly once.
    /// Child processes that load VoiceLite.dll themselves (StartupFirstLaunchGateTests) have to
    /// set the same AppContext value before loading it; they get the directory from here.
    /// </summary>
    internal static class TestLogRedirect
    {
        public static string LogDirectory { get; } = Path.Combine(
            Path.GetTempPath(), $"VoiceLite-TestLogs-{Environment.ProcessId}-{Guid.NewGuid():N}");

#pragma warning disable CA2255 // ModuleInitializer in a test assembly is the point: no test can run before it
        [ModuleInitializer]
        internal static void Redirect()
        {
            Directory.CreateDirectory(LogDirectory);
            AppContext.SetData(ErrorLogger.LogDirectoryOverrideKey, LogDirectory);
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                try { Directory.Delete(LogDirectory, recursive: true); } catch { /* best effort */ }
            };
        }
#pragma warning restore CA2255
    }
}

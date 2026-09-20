using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// Text-contract tripwires for Installer/VoiceLiteSetup.iss. Pascal Script cannot run
    /// under xUnit, so these do NOT prove the installer behaves correctly - only that the
    /// pieces the shipped 2.5.0/2.5.1 updater depends on, and the v2.5.2 failure handling,
    /// are still there. The real gates are an ISCC compile and the manual install matrix.
    /// </summary>
    public class InstallerScriptContractTests
    {
        private static string Iss() => RepoSourceFile.Read("VoiceLite", "Installer", "VoiceLiteSetup.iss");

        /// <summary>Text of one Pascal routine: from its header to its closing column-0
        /// "end;" - the last one before the next top-level "function"/"procedure" (or end of
        /// file), so the comment block above the NEXT routine is not part of the slice.</summary>
        private static string Routine(string iss, string header)
        {
            var start = iss.IndexOf(header, StringComparison.Ordinal);
            Assert.True(start >= 0, $"'{header}' not found in VoiceLiteSetup.iss");
            var next = Regex.Match(iss.Substring(start + header.Length), @"^(function|procedure) ", RegexOptions.Multiline);
            var slice = next.Success ? iss.Substring(start, header.Length + next.Index) : iss.Substring(start);
            var close = Regex.Match(slice, @"^end;", RegexOptions.Multiline | RegexOptions.RightToLeft);
            Assert.True(close.Success, $"no closing 'end;' found for '{header}'");
            return slice.Substring(0, close.Index + close.Length);
        }

        [Fact]
        public void Routine_Slice_ExcludesTheCommentBlockAboveTheNextRoutine()
        {
            // Otherwise a comment added above the NEXT routine (or one mentioning "Exec(")
            // fails the tripwires below although the routine itself is unchanged.
            const string script =
                "// Use Exec( here? No.\n" +
                "procedure First();\nbegin\n  try\n    A;\n  except\n  end;\nend;\n" +
                "\n// Comment about Second, mentions Exec( too.\n\n// More.\n" +
                "function Second: Boolean;\nbegin\n  Result := True;\nend;\n";

            Routine(script, "procedure First(").Should().Be(
                "procedure First();\nbegin\n  try\n    A;\n  except\n  end;\nend;");
            Routine(script, "function Second").Should().Be(
                "function Second: Boolean;\nbegin\n  Result := True;\nend;");
        }

        // ---------- VoiceLiteSetup.iss: the frozen happy path ----------

        [Fact]
        public void Iss_HappyPathContractWithShippedUpdater_IsUnchanged()
        {
            var iss = Iss();

            // The shipped updater runs: Setup /VERYSILENT /NORESTART /SUPPRESSMSGBOXES /RELAUNCH=1
            iss.Should().Contain(
                "Filename: \"{app}\\VoiceLite.exe\"; Flags: nowait skipifnotsilent runasoriginaluser; Check: RelaunchRequested");
            iss.Should().Contain("Result := ExpandConstant('{param:RELAUNCH|0}') = '1';");
            // UpdateCheckService matches the release asset by this name.
            iss.Should().Contain("OutputBaseFilename=VoiceLite-Setup-{#MyAppVersion}");
            iss.Should().Contain("AppId={{A06BC0AA-DD0A-4341-9E41-68AC0D6E541E}");
            iss.Should().Contain("AppMutexName = 'VoiceLite_SingleInstance';");
        }

        // ---------- F4: a failed silent update must not just vanish ----------

        [Fact]
        public void Iss_EnablesSetupLogging()
        {
            var iss = Iss();
            Regex.IsMatch(iss, @"^SetupLogging=yes$", RegexOptions.Multiline).Should().BeTrue(
                "a failed silent update must leave a log in %TEMP%");
        }

        [Fact]
        public void Iss_TracksInstallOutcome()
        {
            var iss = Iss();
            var step = Routine(iss, "procedure CurStepChanged(");
            Regex.IsMatch(step, @"CurStep = ssInstall\b[^;]*\s+FilesTouched := True;").Should().BeTrue();
            Regex.IsMatch(step, @"CurStep = ssPostInstall\b[^;]*\s+begin\s+InstallSucceeded := True;").Should().BeTrue(
                "the success flag must be set before anything in ssPostInstall that could fail");
        }

        [Fact]
        public void Iss_DeinitializeSetup_ReportsFailedUpdaterInstall_AndCanNeverRaise()
        {
            var iss = Iss();
            var deinit = Routine(iss, "procedure DeinitializeSetup(");

            // Only the in-app updater path (/RELAUNCH=1 + silent) that did not succeed.
            deinit.Should().Contain("RelaunchRequested()");
            deinit.Should().Contain("WizardSilent");
            deinit.Should().Contain("InstallSucceeded");

            // Whole body guarded: first statement after "begin" is "try", and the routine
            // ends "except ... end; end;".
            Regex.IsMatch(deinit, @"\bbegin\s+try\b").Should().BeTrue();
            Regex.IsMatch(deinit, @"\bexcept\b[\s\S]*\bend;\s*end;\s*$").Should().BeTrue();

            // Never auto-start a half-copied install.
            var report = Routine(iss, "procedure ReportFailedUpdate(");
            report.Should().Contain("if not FilesTouched then");
            report.Should().Contain("ExecAsOriginalUser(");
            report.Should().NotContain(" Exec(", "an elevated Setup must not start VoiceLite elevated");

            // MsgBox is not silenced by /SUPPRESSMSGBOXES; SuppressibleMsgBox would be.
            report.Should().Contain("MsgBox(");
            report.Should().NotContain("SuppressibleMsgBox");
        }

        [Fact]
        public void Iss_SilentGraceBeforeTaskkill_Is10Seconds()
        {
            var iss = Iss();
            var prepare = Routine(iss, "function PrepareToInstall(");
            prepare.Should().Contain("if WaitForVoiceLiteExit(10000) then");
            prepare.Should().NotContain("WaitForVoiceLiteExit(3000)");
        }

        [Fact]
        public void Iss_WritesReadyFile_BeforeTheRunningCheck()
        {
            var iss = Iss();

            // Contract with UpdateInstaller: /READYFILE=<path> => Setup creates that file as
            // soon as it reaches PrepareToInstall (i.e. it got past UAC and initialisation).
            var write = Routine(iss, "procedure WriteReadyFile(");
            write.Should().Contain("ExpandConstant('{param:READYFILE|}')");
            write.Should().Contain("try");
            write.Should().Contain("except");
            write.Should().Contain("SaveStringToFile(");

            var prepare = Routine(iss, "function PrepareToInstall(");
            var writeAt = prepare.IndexOf("WriteReadyFile();", StringComparison.Ordinal);
            var checkAt = prepare.IndexOf("IsVoiceLiteRunning()", StringComparison.Ordinal);
            writeAt.Should().BeGreaterThan(0);
            checkAt.Should().BeGreaterThan(writeAt, "the ready file must be written before the is-running check/wait");
        }

        // ---------- F8: another session's VoiceLite must not abort a per-user install ----------

        [Fact]
        public void Iss_NonAdminInstall_IgnoresOtherSessionsWhenDecidingToAbort()
        {
            var iss = Iss();

            iss.Should().Contain("external 'ProcessIdToSessionId@kernel32.dll stdcall delayload';",
                "delayload keeps a failed import from stopping Setup at start-up");
            iss.Should().Contain("/FI \"SESSION eq ");

            var close = Routine(iss, "function CloseRunningVoiceLite(");
            // Kill semantics unchanged (machine-wide image-name kill)...
            close.Should().Contain("'/IM ' + AppExeName + ' /F'");
            close.Should().Contain("WaitForVoiceLiteExit(10000)");
            // ...only the abort decision is session-scoped, and only outside admin mode.
            close.Should().Contain("not IsAdminInstallMode");
            close.Should().Contain("IsVoiceLiteRunningInThisSession(");
        }
    }

    /// <summary>
    /// Reads a source file from the repo checkout the tests were built from. Shared by every
    /// source-text tripwire. There is NO "not in a checkout" escape hatch: a tripwire that
    /// cannot find its file fails (see SourceTripwirePolicyTests) instead of passing unread.
    /// </summary>
    internal static class RepoSourceFile
    {
        private static string CompileTimePathOfThisFile([CallerFilePath] string thisFile = "") => thisFile;

        /// <summary>Repo root = the directory that holds VoiceLite/VoiceLite.sln.</summary>
        public static string RepoRoot => FindRepoRoot(CompileTimePathOfThisFile());

        /// <summary>The app project directory (VoiceLite/VoiceLite).</summary>
        public static string AppDir
        {
            get
            {
                var dir = Path.Combine(RepoRoot, "VoiceLite", "VoiceLite");
                Assert.True(Directory.Exists(dir), "Missing app source directory " + dir);
                return dir;
            }
        }

        internal static string FindRepoRoot(string compileTimeTestFile)
        {
            Assert.True(File.Exists(compileTimeTestFile),
                $"Source tripwires locate the repository from the compile-time path of a test file, and '{compileTimeTestFile}' " +
                "does not exist. The test binaries are running away from the checkout they were built from (copied output, " +
                "PathMap / ContinuousIntegrationBuild / SourceLink paths): every source-text contract check is unverified.");
            var dir = new DirectoryInfo(Path.GetDirectoryName(compileTimeTestFile)!);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "VoiceLite", "VoiceLite.sln")))
                dir = dir.Parent;
            Assert.True(dir != null, "Could not find VoiceLite/VoiceLite.sln above " + compileTimeTestFile);
            return dir!.FullName;
        }

        /// <summary>Text of a file under the repo root, with LF line endings.</summary>
        public static string Read(params string[] parts) =>
            File.ReadAllText(Existing(RepoRoot, parts)).Replace("\r\n", "\n");

        /// <summary>Text of a file under VoiceLite/VoiceLite, exactly as it is on disk.</summary>
        public static string ReadApp(params string[] parts) => File.ReadAllText(Existing(AppDir, parts));

        private static string Existing(string root, string[] parts)
        {
            var path = Path.GetFullPath(Path.Combine(new[] { root }.Concat(parts).ToArray()));
            Assert.True(File.Exists(path), "Missing " + path);
            return path;
        }
    }
}

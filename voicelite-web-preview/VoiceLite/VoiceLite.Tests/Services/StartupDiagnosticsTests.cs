using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    [Trait("Category", "Unit")]
    public class StartupDiagnosticsTests
    {
        [Fact]
        public void DiagnosticResult_HasAnyIssues_WhenAntivirusIssuesTrue()
        {
            var result = new DiagnosticResult
            {
                AntivirusIssues = true
            };

            result.HasAnyIssues.Should().BeTrue();
        }

        [Fact]
        public void DiagnosticResult_HasAnyIssues_WhenPermissionIssuesTrue()
        {
            var result = new DiagnosticResult
            {
                PermissionIssues = true
            };

            result.HasAnyIssues.Should().BeTrue();
        }

        [Fact]
        public void DiagnosticResult_HasAnyIssues_WhenProtectedFolderIssueTrue()
        {
            var result = new DiagnosticResult
            {
                ProtectedFolderIssue = true
            };

            result.HasAnyIssues.Should().BeTrue();
        }

        [Fact]
        public void DiagnosticResult_HasAnyIssues_WhenDiskSpaceIssueTrue()
        {
            var result = new DiagnosticResult
            {
                DiskSpaceIssue = true
            };

            result.HasAnyIssues.Should().BeTrue();
        }

        [Fact]
        public void DiagnosticResult_HasAnyIssues_WhenBlockedFilesIssueTrue()
        {
            var result = new DiagnosticResult
            {
                BlockedFilesIssue = true
            };

            result.HasAnyIssues.Should().BeTrue();
        }

        [Fact]
        public void DiagnosticResult_HasAnyIssues_WhenConflictingSoftwareTrue()
        {
            var result = new DiagnosticResult
            {
                ConflictingSoftware = true
            };

            result.HasAnyIssues.Should().BeTrue();
        }

        [Fact]
        public void DiagnosticResult_HasAnyIssues_WhenMissingFilesPresent()
        {
            var result = new DiagnosticResult
            {
                MissingFiles = new List<string> { "NAudio.dll" }
            };

            result.HasAnyIssues.Should().BeTrue();
        }

        [Fact]
        public void DiagnosticResult_HasAnyIssues_WhenWindowsVersionIssueTrue()
        {
            var result = new DiagnosticResult
            {
                WindowsVersionIssue = true
            };

            result.HasAnyIssues.Should().BeTrue();
        }

        [Fact]
        public void DiagnosticResult_HasAnyIssues_WhenTempFolderIssueTrue()
        {
            var result = new DiagnosticResult
            {
                TempFolderIssue = true
            };

            result.HasAnyIssues.Should().BeTrue();
        }

        [Fact]
        public void DiagnosticResult_HasAnyIssues_WhenCorruptModelFileTrue()
        {
            var result = new DiagnosticResult
            {
                CorruptModelFile = true
            };

            result.HasAnyIssues.Should().BeTrue();
        }

        [Fact]
        public void DiagnosticResult_HasAnyIssues_WhenNoIssues()
        {
            var result = new DiagnosticResult();

            result.HasAnyIssues.Should().BeFalse();
        }

        [Fact]
        public void DiagnosticResult_GetSummary_NoIssues()
        {
            var result = new DiagnosticResult();

            var summary = result.GetSummary();

            summary.Should().Be("No issues detected");
        }

        [Fact]
        public void DiagnosticResult_GetSummary_AntivirusIssues()
        {
            var result = new DiagnosticResult
            {
                AntivirusIssues = true
            };

            var summary = result.GetSummary();

            summary.Should().Contain("Antivirus may be blocking VoiceLite");
        }

        [Fact]
        public void DiagnosticResult_GetSummary_PermissionIssues()
        {
            var result = new DiagnosticResult
            {
                PermissionIssues = true
            };

            var summary = result.GetSummary();

            summary.Should().Contain("File permission issues detected");
        }

        [Fact]
        public void DiagnosticResult_GetSummary_ProtectedFolderIssue()
        {
            var result = new DiagnosticResult
            {
                ProtectedFolderIssue = true
            };

            var summary = result.GetSummary();

            summary.Should().Contain("Running from protected system folder");
        }

        [Fact]
        public void DiagnosticResult_GetSummary_DiskSpaceIssue()
        {
            var result = new DiagnosticResult
            {
                DiskSpaceIssue = true
            };

            var summary = result.GetSummary();

            summary.Should().Contain("Low disk space");
        }

        [Fact]
        public void DiagnosticResult_GetSummary_BlockedFilesIssue()
        {
            var result = new DiagnosticResult
            {
                BlockedFilesIssue = true
            };

            var summary = result.GetSummary();

            summary.Should().Contain("Files blocked by Windows security");
        }

        [Fact]
        public void DiagnosticResult_GetSummary_ConflictingSoftware()
        {
            var result = new DiagnosticResult
            {
                ConflictingSoftware = true
            };

            var summary = result.GetSummary();

            summary.Should().Contain("Conflicting software detected");
        }

        [Fact]
        public void DiagnosticResult_GetSummary_MissingFiles()
        {
            var result = new DiagnosticResult
            {
                MissingFiles = new List<string> { "NAudio.dll", "whisper.exe" }
            };

            var summary = result.GetSummary();

            summary.Should().Contain("Missing files:");
            summary.Should().Contain("NAudio.dll");
            summary.Should().Contain("whisper.exe");
        }

        [Fact]
        public void DiagnosticResult_GetSummary_WindowsVersionIssue()
        {
            var result = new DiagnosticResult
            {
                WindowsVersionIssue = true
            };

            var summary = result.GetSummary();

            summary.Should().Contain("Windows version not supported");
        }

        [Fact]
        public void DiagnosticResult_GetSummary_TempFolderIssue()
        {
            var result = new DiagnosticResult
            {
                TempFolderIssue = true
            };

            var summary = result.GetSummary();

            summary.Should().Contain("Cannot access temp folder");
        }

        [Fact]
        public void DiagnosticResult_GetSummary_CorruptModelFile()
        {
            var result = new DiagnosticResult
            {
                CorruptModelFile = true
            };

            var summary = result.GetSummary();

            summary.Should().Contain("AI model file appears corrupted");
        }

        [Fact]
        public void DiagnosticResult_GetSummary_MultipleIssues()
        {
            var result = new DiagnosticResult
            {
                AntivirusIssues = true,
                PermissionIssues = true,
                MissingFiles = new List<string> { "NAudio.dll" }
            };

            var summary = result.GetSummary();

            summary.Should().Contain("Antivirus may be blocking VoiceLite");
            summary.Should().Contain("File permission issues detected");
            summary.Should().Contain("Missing files:");
        }

        [Fact]
        public void DiagnosticResult_MissingFiles_DefaultsToEmptyList()
        {
            var result = new DiagnosticResult();

            result.MissingFiles.Should().NotBeNull();
            result.MissingFiles.Should().BeEmpty();
        }

        [Fact]
        public void DiagnosticResult_AllPropertiesDefaultToFalse()
        {
            var result = new DiagnosticResult();

            result.AntivirusIssues.Should().BeFalse();
            result.PermissionIssues.Should().BeFalse();
            result.ProtectedFolderIssue.Should().BeFalse();
            result.DiskSpaceIssue.Should().BeFalse();
            result.BlockedFilesIssue.Should().BeFalse();
            result.ConflictingSoftware.Should().BeFalse();
            result.WindowsVersionIssue.Should().BeFalse();
            result.TempFolderIssue.Should().BeFalse();
            result.CorruptModelFile.Should().BeFalse();
        }

        [Fact]
        public void DiagnosticResult_GetSummary_IncludesNewlinesBetweenIssues()
        {
            var result = new DiagnosticResult
            {
                AntivirusIssues = true,
                PermissionIssues = true
            };

            var summary = result.GetSummary();

            summary.Should().Contain("\n");
        }
    }
}

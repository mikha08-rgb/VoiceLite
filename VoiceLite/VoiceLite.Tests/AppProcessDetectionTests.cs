using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using AwesomeAssertions;
using VoiceLite;
using Xunit;

namespace VoiceLite.Tests
{
    public class AppProcessDetectionTests
    {
        private static readonly MethodInfo DetectionMethod = typeof(App).GetMethod(
            "IsProcessOwnedByVoiceLite",
            BindingFlags.NonPublic | BindingFlags.Static) ??
            throw new InvalidOperationException("Helper not found");

        [Fact(Skip = "IsProcessOwnedByVoiceLite method no longer exists in App class")]
        public void IsProcessOwnedByVoiceLite_ReturnsTrueWhenExecutableResidesUnderRoot()
        {
            using var current = Process.GetCurrentProcess();
            var processPath = current.MainModule?.FileName ?? throw new InvalidOperationException("Main module missing");
            var installRoot = Path.GetDirectoryName(processPath) ?? throw new InvalidOperationException("Directory missing");

            var owned = (bool)DetectionMethod.Invoke(null, new object[] { current, installRoot })!;
            owned.Should().BeTrue();
        }

        [Fact(Skip = "IsProcessOwnedByVoiceLite method no longer exists in App class")]
        public void IsProcessOwnedByVoiceLite_ReturnsFalseForExternalProcesses()
        {
            using var current = Process.GetCurrentProcess();
            var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            var owned = (bool)DetectionMethod.Invoke(null, new object[] { current, tempRoot })!;
            owned.Should().BeFalse();
        }
    }
}

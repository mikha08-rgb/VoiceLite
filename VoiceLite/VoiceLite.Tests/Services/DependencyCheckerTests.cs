using System;
using System.IO;
using System.Reflection;
using FluentAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    public class DependencyCheckerTests
    {
        [Fact]
        public void VerifyMicrosoftSignature_WithInvalidBinary_Throws()
        {
            var method = typeof(DependencyChecker).GetMethod(
                "VerifyMicrosoftSignature",
                BindingFlags.NonPublic | BindingFlags.Static) ??
                throw new InvalidOperationException("Private helper not found");

            var tempPath = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(tempPath, new byte[] { 0x01, 0x02, 0x03 });

                Action act = () => method.Invoke(null, new object[] { tempPath });

                act.Should()
                    .Throw<TargetInvocationException>()
                    .WithInnerException<InvalidOperationException>();
            }
            finally
            {
                File.Delete(tempPath);
            }
        }
    }
}

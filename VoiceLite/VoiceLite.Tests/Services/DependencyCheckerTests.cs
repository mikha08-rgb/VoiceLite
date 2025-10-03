using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
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

        [Fact]
        public void DependencyCheckResult_AllDependenciesMet_WhenAllTrue()
        {
            var result = new DependencyCheckResult
            {
                WhisperExeFound = true,
                ModelFound = true,
                VCRuntimeInstalled = true,
                WhisperCanRun = true
            };

            result.AllDependenciesMet.Should().BeTrue();
        }

        [Fact]
        public void DependencyCheckResult_AllDependenciesNotMet_WhenWhisperExeMissing()
        {
            var result = new DependencyCheckResult
            {
                WhisperExeFound = false,
                ModelFound = true,
                VCRuntimeInstalled = true,
                WhisperCanRun = true
            };

            result.AllDependenciesMet.Should().BeFalse();
        }

        [Fact]
        public void DependencyCheckResult_AllDependenciesNotMet_WhenModelMissing()
        {
            var result = new DependencyCheckResult
            {
                WhisperExeFound = true,
                ModelFound = false,
                VCRuntimeInstalled = true,
                WhisperCanRun = true
            };

            result.AllDependenciesMet.Should().BeFalse();
        }

        [Fact]
        public void DependencyCheckResult_AllDependenciesNotMet_WhenVCRuntimeMissing()
        {
            var result = new DependencyCheckResult
            {
                WhisperExeFound = true,
                ModelFound = true,
                VCRuntimeInstalled = false,
                WhisperCanRun = true
            };

            result.AllDependenciesMet.Should().BeFalse();
        }

        [Fact]
        public void DependencyCheckResult_AllDependenciesNotMet_WhenWhisperCannotRun()
        {
            var result = new DependencyCheckResult
            {
                WhisperExeFound = true,
                ModelFound = true,
                VCRuntimeInstalled = true,
                WhisperCanRun = false
            };

            result.AllDependenciesMet.Should().BeFalse();
        }

        [Fact]
        public void DependencyCheckResult_GetErrorMessage_WhisperExeNotFound()
        {
            var result = new DependencyCheckResult
            {
                WhisperExeFound = false,
                ModelFound = true,
                VCRuntimeInstalled = true,
                WhisperCanRun = true
            };

            var message = result.GetErrorMessage();
            message.Should().Contain("Whisper.exe not found");
        }

        [Fact]
        public void DependencyCheckResult_GetErrorMessage_ModelNotFound()
        {
            var result = new DependencyCheckResult
            {
                WhisperExeFound = true,
                ModelFound = false,
                VCRuntimeInstalled = true,
                WhisperCanRun = true
            };

            var message = result.GetErrorMessage();
            message.Should().Contain("No Whisper AI models found");
        }

        [Fact]
        public void DependencyCheckResult_GetErrorMessage_VCRuntimeNotInstalled()
        {
            var result = new DependencyCheckResult
            {
                WhisperExeFound = true,
                ModelFound = true,
                VCRuntimeInstalled = false,
                WhisperCanRun = true
            };

            var message = result.GetErrorMessage();
            message.Should().Contain("Visual C++ Runtime");
        }

        [Fact]
        public void DependencyCheckResult_GetErrorMessage_WhisperCannotRun()
        {
            var result = new DependencyCheckResult
            {
                WhisperExeFound = true,
                ModelFound = true,
                VCRuntimeInstalled = true,
                WhisperCanRun = false
            };

            var message = result.GetErrorMessage();
            message.Should().Contain("Speech recognition engine cannot start");
        }

        [Fact]
        public void DependencyCheckResult_GetErrorMessage_AllDependenciesMet()
        {
            var result = new DependencyCheckResult
            {
                WhisperExeFound = true,
                ModelFound = true,
                VCRuntimeInstalled = true,
                WhisperCanRun = true
            };

            var message = result.GetErrorMessage();
            message.Should().Contain("All dependencies are installed correctly");
        }

        [Fact]
        public void DependencyCheckResult_GetErrorMessage_PrioritizesWhisperExeNotFound()
        {
            var result = new DependencyCheckResult
            {
                WhisperExeFound = false,
                ModelFound = false,
                VCRuntimeInstalled = false,
                WhisperCanRun = false
            };

            var message = result.GetErrorMessage();
            message.Should().Contain("Whisper.exe not found");
        }

        [Fact]
        public void DependencyCheckResult_GetErrorMessage_PrioritizesModelNotFoundOverVCRuntime()
        {
            var result = new DependencyCheckResult
            {
                WhisperExeFound = true,
                ModelFound = false,
                VCRuntimeInstalled = false,
                WhisperCanRun = false
            };

            var message = result.GetErrorMessage();
            message.Should().Contain("No Whisper AI models found");
        }

        [Fact]
        public void DependencyCheckResult_GetErrorMessage_PrioritizesVCRuntimeOverWhisperCanRun()
        {
            var result = new DependencyCheckResult
            {
                WhisperExeFound = true,
                ModelFound = true,
                VCRuntimeInstalled = false,
                WhisperCanRun = false
            };

            var message = result.GetErrorMessage();
            message.Should().Contain("Visual C++ Runtime");
        }
    }
}

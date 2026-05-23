using AwesomeAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    public class HardwareIdServiceTests
    {
        [Fact]
        public void GetMachineLabel_ReturnsNonEmptyString()
        {
            var label = HardwareIdService.GetMachineLabel();
            label.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void GetMachineId_ReturnsNonEmptyString()
        {
            var id = HardwareIdService.GetMachineId();
            id.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void GetMachineId_ReturnsAtMost32Characters()
        {
            // Output is truncated SHA-256 base64; length cap is 32 per implementation.
            var id = HardwareIdService.GetMachineId();
            id.Length.Should().BeLessThanOrEqualTo(32);
        }

        [Fact]
        public void GetMachineId_IsStableAcrossCalls()
        {
            // Same machine should produce the same ID every call (within a process).
            var first = HardwareIdService.GetMachineId();
            var second = HardwareIdService.GetMachineId();
            second.Should().Be(first);
        }

        [Fact]
        public void GetMachineHash_ReturnsNonEmptyString()
        {
            var hash = HardwareIdService.GetMachineHash();
            hash.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void GetMachineHash_IsStableAcrossCalls()
        {
            var first = HardwareIdService.GetMachineHash();
            var second = HardwareIdService.GetMachineHash();
            second.Should().Be(first);
        }

        [Fact]
        public void GetMachineId_DoesNotContainRawSystemIdentifiers()
        {
            // The ID is a hash — it must not leak the underlying CPU/MB serials as plain text.
            // We check that the output doesn't contain typical Windows serial-number patterns.
            var id = HardwareIdService.GetMachineId();
            id.Should().NotContain("BFEBFBFF"); // common Intel CPUID prefix
            id.Should().NotContain("To Be Filled"); // common motherboard default
        }
    }
}

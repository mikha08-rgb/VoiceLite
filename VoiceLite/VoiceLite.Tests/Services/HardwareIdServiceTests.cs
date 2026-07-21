using System;
using System.IO;
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

        [Theory]
        [InlineData("UNKNOWN_CPU", "UNKNOWN_MB")]
        [InlineData("UNKNOWN_CPU", "REAL_MOTHERBOARD_SERIAL")]
        [InlineData("REAL_CPU_ID", "UNKNOWN_MB")]
        [InlineData("To Be Filled By O.E.M.", "Default string")]
        [InlineData("0000000000000000", "FFFFFFFFFFFFFFFF")]
        public void GetMachineId_WhenWmiIdentifiersAreUnavailable_UsesStablePersistentFallback(
            string cpuId,
            string motherboardSerial)
        {
            var testDirectory = Path.Combine(
                Path.GetTempPath(),
                "VoiceLite-HardwareIdTests",
                Guid.NewGuid().ToString("N"));
            var fallbackPath = Path.Combine(testDirectory, "machine_id.dat");
            var provider = new StubHardwareInfoProvider(cpuId, motherboardSerial);

            try
            {
                var first = HardwareIdService.GetMachineId(provider, fallbackPath);
                var second = HardwareIdService.GetMachineId(provider, fallbackPath);

                first.Should().HaveLength(32);
                second.Should().Be(first);
                File.Exists(fallbackPath).Should().BeTrue();
            }
            finally
            {
                if (Directory.Exists(testDirectory))
                {
                    Directory.Delete(testDirectory, recursive: true);
                }
            }
        }

        private sealed class StubHardwareInfoProvider : IHardwareInfoProvider
        {
            private readonly string _cpuId;
            private readonly string _motherboardSerial;

            public StubHardwareInfoProvider(string cpuId, string motherboardSerial)
            {
                _cpuId = cpuId;
                _motherboardSerial = motherboardSerial;
            }

            public string GetCpuId() => _cpuId;

            public string GetMotherboardSerial() => _motherboardSerial;

            public string GetBiosSerial() => "TEST_BIOS_SERIAL";
        }
    }
}

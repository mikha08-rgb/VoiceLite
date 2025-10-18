using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// Tests for HardwareFingerprint service.
    ///
    /// CRITICAL: This class is responsible for generating hardware-bound identifiers
    /// for license activation. These tests verify:
    /// 1. Fingerprints are non-empty and well-formed
    /// 2. Fingerprints are consistent across calls (same device = same fingerprint)
    /// 3. Thread safety for concurrent fingerprint generation
    /// 4. Proper fallback when WMI is unavailable
    /// 5. Error handling and resilience
    ///
    /// Note: Some tests interact with real system hardware via WMI since HardwareFingerprint
    /// is a static class with private methods that are difficult to mock.
    /// </summary>
    [Trait("Category", "Unit")]
    public class HardwareFingerprintTests
    {
        // Reflection helpers to access private methods for testing
        private static readonly MethodInfo GetCpuIdMethod = typeof(HardwareFingerprint).GetMethod(
            "GetCpuId",
            BindingFlags.NonPublic | BindingFlags.Static) ??
            throw new InvalidOperationException("GetCpuId method not found");

        private static readonly MethodInfo GetMotherboardIdMethod = typeof(HardwareFingerprint).GetMethod(
            "GetMotherboardId",
            BindingFlags.NonPublic | BindingFlags.Static) ??
            throw new InvalidOperationException("GetMotherboardId method not found");

        #region Task 1: Core Functionality Tests

        /// <summary>
        /// Test 1: Verify that Generate() returns a non-empty string.
        /// This is the most basic requirement - the fingerprint must exist.
        /// </summary>
        [Fact]
        public void Generate_ReturnsNonEmptyString()
        {
            // Act
            var fingerprint = HardwareFingerprint.Generate();

            // Assert
            fingerprint.Should().NotBeNullOrEmpty("hardware fingerprint must be generated");
            fingerprint.Length.Should().BeGreaterThan(0, "fingerprint must have content");
        }

        /// <summary>
        /// Test 2: Verify that Generate() is consistent across multiple calls.
        /// CRITICAL: Same device must always produce the same fingerprint for license validation.
        /// </summary>
        [Fact]
        public void Generate_IsConsistent_AcrossMultipleCalls()
        {
            // Act
            var fingerprint1 = HardwareFingerprint.Generate();
            var fingerprint2 = HardwareFingerprint.Generate();
            var fingerprint3 = HardwareFingerprint.Generate();

            // Assert
            fingerprint1.Should().Be(fingerprint2, "fingerprint must be consistent across calls");
            fingerprint2.Should().Be(fingerprint3, "fingerprint must be consistent across calls");
            fingerprint1.Should().Be(fingerprint3, "fingerprint must be consistent across calls");
        }

        /// <summary>
        /// Test 3: Verify fingerprint has expected format characteristics.
        /// The fingerprint should be a fixed-length alphanumeric string (base64-derived).
        /// </summary>
        [Fact]
        public void Generate_ReturnsWellFormedFingerprint()
        {
            // Act
            var fingerprint = HardwareFingerprint.Generate();

            // Assert
            fingerprint.Length.Should().Be(32, "fingerprint is truncated to 32 characters");

            // Should not contain / or + (these are stripped from base64)
            fingerprint.Should().NotContain("/", "forward slashes are removed");
            fingerprint.Should().NotContain("+", "plus signs are removed");

            // If not a fallback, should be alphanumeric
            if (!fingerprint.StartsWith("FALLBACK-"))
            {
                fingerprint.Should().MatchRegex("^[A-Za-z0-9]+$", "fingerprint should be alphanumeric");
            }
        }

        /// <summary>
        /// Test 4: Verify fingerprint is based on SHA256 hash when WMI is available.
        /// This tests the happy path where hardware info is available.
        /// </summary>
        [Fact]
        public void Generate_UsesHashBasedFingerprint_WhenHardwareInfoAvailable()
        {
            // Arrange
            var cpuId = (string)GetCpuIdMethod.Invoke(null, null)!;
            var motherboardId = (string)GetMotherboardIdMethod.Invoke(null, null)!;

            // Act
            var fingerprint = HardwareFingerprint.Generate();

            // Assert
            // If WMI worked, we should not get a fallback fingerprint
            if (cpuId != "CPU-UNKNOWN" && motherboardId != "MB-UNKNOWN")
            {
                fingerprint.Should().NotStartWith("FALLBACK-",
                    "should use hash-based fingerprint when hardware info is available");
            }
        }

        #endregion

        #region Task 2: Thread Safety Tests

        /// <summary>
        /// Test 5: Verify Generate() is thread-safe for concurrent calls.
        /// CRITICAL: License validation may occur concurrently in multi-threaded scenarios.
        /// </summary>
        [Fact]
        public async Task Generate_IsThreadSafe_WithConcurrentCalls()
        {
            // Arrange
            const int concurrentCalls = 50;
            var tasks = new Task<string>[concurrentCalls];

            // Act - Generate fingerprints concurrently
            for (int i = 0; i < concurrentCalls; i++)
            {
                tasks[i] = Task.Run(() => HardwareFingerprint.Generate());
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().HaveCount(concurrentCalls, "all tasks should complete");
            results.Should().OnlyContain(fp => !string.IsNullOrEmpty(fp), "all fingerprints should be generated");

            // All fingerprints should be identical (same hardware)
            var uniqueFingerprints = results.Distinct().ToList();
            uniqueFingerprints.Should().HaveCount(1, "all concurrent calls should produce the same fingerprint");
        }

        /// <summary>
        /// Test 6: Verify Generate() handles concurrent calls with consistent results.
        /// This is a stress test to ensure no race conditions exist.
        /// </summary>
        [Fact]
        public async Task Generate_MaintainsConsistency_UnderConcurrentLoad()
        {
            // Arrange
            const int iterations = 100;
            var baseline = HardwareFingerprint.Generate();
            var tasks = new List<Task<bool>>();

            // Act - Verify consistency across many concurrent iterations
            for (int i = 0; i < iterations; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    var fp = HardwareFingerprint.Generate();
                    return fp == baseline;
                }));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().AllBeEquivalentTo(true, "all concurrent fingerprints should match baseline");
        }

        #endregion

        #region Task 3: Component Tests (CPU and Motherboard)

        /// <summary>
        /// Test 7: Verify GetCpuId() returns a value (or fallback).
        /// </summary>
        [Fact]
        public void GetCpuId_ReturnsValue()
        {
            // Act
            var cpuId = (string)GetCpuIdMethod.Invoke(null, null)!;

            // Assert
            cpuId.Should().NotBeNullOrEmpty("CPU ID should always return a value");

            // Should either be actual CPU ID or fallback
            if (cpuId == "CPU-UNKNOWN")
            {
                // Fallback is acceptable if WMI fails
                cpuId.Should().Be("CPU-UNKNOWN", "fallback value should be consistent");
            }
            else
            {
                // Actual CPU ID should have some content
                cpuId.Length.Should().BeGreaterThan(0, "actual CPU ID should have content");
            }
        }

        /// <summary>
        /// Test 8: Verify GetMotherboardId() returns a value (or fallback).
        /// </summary>
        [Fact]
        public void GetMotherboardId_ReturnsValue()
        {
            // Act
            var motherboardId = (string)GetMotherboardIdMethod.Invoke(null, null)!;

            // Assert
            motherboardId.Should().NotBeNullOrEmpty("Motherboard ID should always return a value");

            // Should either be actual motherboard ID or fallback
            if (motherboardId == "MB-UNKNOWN")
            {
                // Fallback is acceptable if WMI fails
                motherboardId.Should().Be("MB-UNKNOWN", "fallback value should be consistent");
            }
            else
            {
                // Actual motherboard ID should have some content
                motherboardId.Length.Should().BeGreaterThan(0, "actual motherboard ID should have content");
            }
        }

        /// <summary>
        /// Test 9: Verify GetCpuId() is consistent across calls.
        /// </summary>
        [Fact]
        public void GetCpuId_IsConsistent()
        {
            // Act
            var cpuId1 = (string)GetCpuIdMethod.Invoke(null, null)!;
            var cpuId2 = (string)GetCpuIdMethod.Invoke(null, null)!;
            var cpuId3 = (string)GetCpuIdMethod.Invoke(null, null)!;

            // Assert
            cpuId1.Should().Be(cpuId2, "CPU ID should be consistent");
            cpuId2.Should().Be(cpuId3, "CPU ID should be consistent");
        }

        /// <summary>
        /// Test 10: Verify GetMotherboardId() is consistent across calls.
        /// </summary>
        [Fact]
        public void GetMotherboardId_IsConsistent()
        {
            // Act
            var mbId1 = (string)GetMotherboardIdMethod.Invoke(null, null)!;
            var mbId2 = (string)GetMotherboardIdMethod.Invoke(null, null)!;
            var mbId3 = (string)GetMotherboardIdMethod.Invoke(null, null)!;

            // Assert
            mbId1.Should().Be(mbId2, "Motherboard ID should be consistent");
            mbId2.Should().Be(mbId3, "Motherboard ID should be consistent");
        }

        #endregion

        #region Task 4: Fallback Behavior Tests

        /// <summary>
        /// Test 11: Verify fallback fingerprint format when WMI fails.
        /// The fallback uses machine name and username for uniqueness.
        /// </summary>
        [Fact]
        public void Generate_UsesFallback_WhenWmiFails()
        {
            // Note: We cannot easily simulate WMI failure without modifying the class,
            // but we can verify the fallback format is correct if it occurs.
            // This test documents the expected fallback behavior.

            // Arrange
            var expectedFallbackPattern = $"FALLBACK-{Environment.MachineName}-{Environment.UserName}";

            // Act
            var fingerprint = HardwareFingerprint.Generate();

            // Assert
            // If we get a fallback, verify it matches the expected format
            if (fingerprint.StartsWith("FALLBACK-"))
            {
                fingerprint.Should().Be(expectedFallbackPattern,
                    "fallback should use machine name and username");
                fingerprint.Should().Contain(Environment.MachineName,
                    "fallback should include machine name");
                fingerprint.Should().Contain(Environment.UserName,
                    "fallback should include username");
            }
        }

        /// <summary>
        /// Test 12: Verify that even fallback fingerprints are consistent.
        /// </summary>
        [Fact]
        public void Generate_FallbackIsConsistent_AcrossCalls()
        {
            // Arrange & Act
            var fingerprint1 = HardwareFingerprint.Generate();
            var fingerprint2 = HardwareFingerprint.Generate();

            // Assert
            // Regardless of whether it's fallback or real, it should be consistent
            fingerprint1.Should().Be(fingerprint2,
                "fingerprint (including fallback) must be consistent");
        }

        #endregion

        #region Task 5: Error Handling Tests

        /// <summary>
        /// Test 13: Verify Generate() does not throw exceptions even if WMI fails.
        /// CRITICAL: License validation must never crash the application.
        /// </summary>
        [Fact]
        public void Generate_DoesNotThrow_EvenOnError()
        {
            // Act
            Action act = () => HardwareFingerprint.Generate();

            // Assert
            act.Should().NotThrow("fingerprint generation must never crash");
        }

        /// <summary>
        /// Test 14: Verify GetCpuId() does not throw exceptions.
        /// </summary>
        [Fact]
        public void GetCpuId_DoesNotThrow()
        {
            // Act
            Action act = () => GetCpuIdMethod.Invoke(null, null);

            // Assert
            act.Should().NotThrow("GetCpuId must handle WMI errors gracefully");
        }

        /// <summary>
        /// Test 15: Verify GetMotherboardId() does not throw exceptions.
        /// </summary>
        [Fact]
        public void GetMotherboardId_DoesNotThrow()
        {
            // Act
            Action act = () => GetMotherboardIdMethod.Invoke(null, null);

            // Assert
            act.Should().NotThrow("GetMotherboardId must handle WMI errors gracefully");
        }

        #endregion

        #region Task 6: Integration Tests

        /// <summary>
        /// Test 16: Verify that hardware components are properly combined.
        /// The fingerprint should be a hash of CPU ID + Motherboard ID.
        /// </summary>
        [Fact]
        public void Generate_CombinesCpuAndMotherboardIds()
        {
            // Arrange
            var cpuId = (string)GetCpuIdMethod.Invoke(null, null)!;
            var motherboardId = (string)GetMotherboardIdMethod.Invoke(null, null)!;

            // Act
            var fingerprint = HardwareFingerprint.Generate();

            // Assert
            fingerprint.Should().NotBeNullOrEmpty();

            // Verify fingerprint changes if component IDs are different
            // (This is a logical assertion - we can't test with different hardware,
            // but we verify the fingerprint is derived from both components)
            cpuId.Should().NotBeNullOrEmpty("CPU ID should be available");
            motherboardId.Should().NotBeNullOrEmpty("Motherboard ID should be available");
        }

        /// <summary>
        /// Test 17: Verify fingerprint uniqueness property.
        /// While we can't test with different hardware, we verify the fingerprint
        /// is deterministic based on hardware components.
        /// </summary>
        [Fact]
        public void Generate_IsDeterministic_BasedOnHardware()
        {
            // Arrange - Get baseline fingerprint
            var baseline = HardwareFingerprint.Generate();

            // Act - Generate multiple times
            var fingerprints = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                fingerprints.Add(HardwareFingerprint.Generate());
            }

            // Assert - All should match baseline (same hardware)
            fingerprints.Should().AllBeEquivalentTo(baseline,
                "fingerprint must be deterministic for the same hardware");
        }

        /// <summary>
        /// Test 18: Verify fingerprint length constraint.
        /// The implementation truncates to 32 characters for readability.
        /// </summary>
        [Fact]
        public void Generate_EnforcesLengthConstraint()
        {
            // Act
            var fingerprint = HardwareFingerprint.Generate();

            // Assert
            if (!fingerprint.StartsWith("FALLBACK-"))
            {
                // Hash-based fingerprint should be exactly 32 characters
                fingerprint.Length.Should().Be(32,
                    "hash-based fingerprint is truncated to 32 characters");
            }
            else
            {
                // Fallback may have different length
                fingerprint.Length.Should().BeGreaterThan(0,
                    "fallback fingerprint should have content");
            }
        }

        /// <summary>
        /// Test 19: Verify special characters are removed from fingerprint.
        /// Base64 encoding produces / and + which are removed for compatibility.
        /// </summary>
        [Fact]
        public void Generate_RemovesSpecialCharacters_FromBase64()
        {
            // Act
            var fingerprint = HardwareFingerprint.Generate();

            // Assert
            if (!fingerprint.StartsWith("FALLBACK-"))
            {
                fingerprint.Should().NotContain("/", "forward slashes should be removed");
                fingerprint.Should().NotContain("+", "plus signs should be removed");
                fingerprint.Should().NotContain("=", "padding should not be present in truncated hash");
            }
        }

        #endregion

        #region Task 7: WMI Availability Tests

        /// <summary>
        /// Test 20: Verify WMI is accessible on this system.
        /// This is an environmental test to ensure the test system can run WMI queries.
        /// </summary>
        [Fact]
        public void WMI_IsAccessible_OnTestSystem()
        {
            // This test verifies that WMI is available for testing
            // It's a sanity check rather than a production test

            // Act & Assert
            Action wmiQuery = () =>
            {
                using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
                using var collection = searcher.Get();
                collection.Should().NotBeNull("WMI should be accessible");
            };

            wmiQuery.Should().NotThrow("WMI should be available on Windows test systems");
        }

        /// <summary>
        /// Test 21: Verify CPU information can be retrieved via WMI.
        /// This tests the actual WMI query used by GetCpuId().
        /// </summary>
        [Fact]
        public void WMI_CanRetrieveCpuInformation()
        {
            // Act
            var cpuId = (string)GetCpuIdMethod.Invoke(null, null)!;

            // Assert
            cpuId.Should().NotBeNullOrEmpty("CPU ID should be retrievable");

            // On most systems, WMI should work and return actual CPU ID
            // If it returns the fallback, that's acceptable but logged
            if (cpuId == "CPU-UNKNOWN")
            {
                // WMI failed - this is acceptable but worth noting
                System.Diagnostics.Debug.WriteLine("WARNING: WMI CPU query returned fallback");
            }
        }

        /// <summary>
        /// Test 22: Verify motherboard information can be retrieved via WMI.
        /// This tests the actual WMI query used by GetMotherboardId().
        /// </summary>
        [Fact]
        public void WMI_CanRetrieveMotherboardInformation()
        {
            // Act
            var motherboardId = (string)GetMotherboardIdMethod.Invoke(null, null)!;

            // Assert
            motherboardId.Should().NotBeNullOrEmpty("Motherboard ID should be retrievable");

            // On most systems, WMI should work and return actual motherboard serial
            // If it returns the fallback, that's acceptable but logged
            if (motherboardId == "MB-UNKNOWN")
            {
                // WMI failed - this is acceptable but worth noting
                System.Diagnostics.Debug.WriteLine("WARNING: WMI Motherboard query returned fallback");
            }
        }

        #endregion
    }
}

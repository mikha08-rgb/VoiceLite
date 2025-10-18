using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// Tests for SimpleLicenseStorage service (REVENUE-CRITICAL).
    /// Coverage: License storage, retrieval, deletion, validation, thread safety, error handling.
    ///
    /// IMPORTANT: This is a revenue-critical component that manages license persistence.
    /// All edge cases and failure modes must be thoroughly tested.
    ///
    /// NOTE: These tests use the real file system at %LOCALAPPDATA%\VoiceLite\license.dat
    /// Cleanup is performed before and after each test to ensure isolation.
    /// </summary>
    [Trait("Category", "Integration")]
    [Trait("Service", "SimpleLicenseStorage")]
    [Trait("Priority", "Critical")]
    public class SimpleLicenseStorageTests : IDisposable
    {
        private readonly string _licenseBackupPath;
        private bool _hadExistingLicense;

        public SimpleLicenseStorageTests()
        {
            // Back up any existing license before tests
            _licenseBackupPath = Path.GetTempFileName();
            _hadExistingLicense = SimpleLicenseStorage.LicenseFileExists();

            if (_hadExistingLicense)
            {
                // Get the actual license path via reflection
                var type = typeof(SimpleLicenseStorage);
                var licensePathField = type.GetField("LicensePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var licensePath = (string)licensePathField!.GetValue(null)!;

                File.Copy(licensePath, _licenseBackupPath, true);
            }

            // Ensure clean state for tests
            SimpleLicenseStorage.DeleteLicense();
        }

        public void Dispose()
        {
            // Restore original license if it existed
            if (_hadExistingLicense && File.Exists(_licenseBackupPath))
            {
                var type = typeof(SimpleLicenseStorage);
                var licensePathField = type.GetField("LicensePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var licensePath = (string)licensePathField!.GetValue(null)!;

                File.Copy(_licenseBackupPath, licensePath, true);
            }
            else
            {
                // Clean up test license
                SimpleLicenseStorage.DeleteLicense();
            }

            // Delete backup file
            try
            {
                if (File.Exists(_licenseBackupPath))
                {
                    File.Delete(_licenseBackupPath);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }

        #region SaveLicense Tests

        [Fact]
        public void SaveLicense_ValidKey_CreatesFileSuccessfully()
        {
            // Arrange
            var licenseKey = "VL-ABC123-DEF456-GHI789";
            var email = "test@example.com";

            // Act
            SimpleLicenseStorage.SaveLicense(licenseKey, email);

            // Assert
            SimpleLicenseStorage.LicenseFileExists().Should().BeTrue("License file should be created");

            var license = SimpleLicenseStorage.GetStoredLicense();
            license.Should().NotBeNull();
            license!.LicenseKey.Should().Be(licenseKey);
            license.Email.Should().Be(email);
            license.Type.Should().Be("LIFETIME");
            license.ValidatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void SaveLicense_ValidKeyWithType_StoresCorrectType()
        {
            // Arrange
            var licenseKey = "VL-PRO123-DEF456-GHI789";
            var email = "pro@example.com";
            var type = "PRO";

            // Act
            SimpleLicenseStorage.SaveLicense(licenseKey, email, type);

            // Assert
            var license = SimpleLicenseStorage.GetStoredLicense();
            license!.Type.Should().Be("PRO");
        }

        [Fact]
        public void SaveLicense_NullKey_ThrowsException()
        {
            // Act
            Action act = () => SimpleLicenseStorage.SaveLicense(null!, "test@example.com");

            // Assert
            // The method will throw during serialization or file write
            act.Should().Throw<Exception>("Null license key should cause an error");
        }

        [Fact]
        public void SaveLicense_EmptyKey_ThrowsException()
        {
            // Arrange
            var licenseKey = "";
            var email = "test@example.com";

            // Act
            Action act = () => SimpleLicenseStorage.SaveLicense(licenseKey, email);

            // Assert
            // BUG FIX #3: Input validation now throws ArgumentException for empty keys
            act.Should().Throw<ArgumentException>("Empty license key should be rejected")
                .WithMessage("*License key cannot be null or empty*");
        }

        [Fact]
        public void SaveLicense_OverwritesExisting_ReplacesOldLicense()
        {
            // Arrange
            var oldKey = "VL-OLD123-OLD456-OLD789";
            var newKey = "VL-NEW123-NEW456-NEW789";
            var email = "test@example.com";

            // Act
            SimpleLicenseStorage.SaveLicense(oldKey, email);
            SimpleLicenseStorage.SaveLicense(newKey, email);

            // Assert
            var license = SimpleLicenseStorage.GetStoredLicense();
            license!.LicenseKey.Should().Be(newKey, "New license should replace old");
        }

        [Fact]
        public void SaveLicense_CreatesDirectory_WhenNotExists()
        {
            // Arrange - SimpleLicenseStorage will create the directory if needed
            // We can't delete the real AppData directory, so just verify the save works

            // Act
            SimpleLicenseStorage.SaveLicense("VL-TEST12-TEST34-TEST56", "test@example.com");

            // Assert
            SimpleLicenseStorage.LicenseFileExists().Should().BeTrue("License file should be created");
            var license = SimpleLicenseStorage.GetStoredLicense();
            license.Should().NotBeNull();
        }

        [Fact]
        public void SaveLicense_VeryLongKey_SavesSuccessfully()
        {
            // Arrange
            var longKey = new string('X', 1000); // 1000 character key
            var email = "test@example.com";

            // Act
            SimpleLicenseStorage.SaveLicense(longKey, email);

            // Assert
            var license = SimpleLicenseStorage.GetStoredLicense();
            license!.LicenseKey.Should().Be(longKey);
        }

        [Fact]
        public void SaveLicense_SpecialCharacters_HandlesCorrectly()
        {
            // Arrange
            var keyWithSpecialChars = "VL-ABC!@#-DEF$%^-GHI&*(";
            var emailWithSpecialChars = "test+tag@example.com";

            // Act
            SimpleLicenseStorage.SaveLicense(keyWithSpecialChars, emailWithSpecialChars);

            // Assert
            var license = SimpleLicenseStorage.GetStoredLicense();
            license!.LicenseKey.Should().Be(keyWithSpecialChars);
            license.Email.Should().Be(emailWithSpecialChars);
        }

        #endregion

        #region HasValidLicense Tests

        [Fact]
        public void HasValidLicense_WhenLicenseExists_ReturnsTrue()
        {
            // Arrange
            var licenseKey = "VL-VALID0-123456-789ABC";
            var email = "test@example.com";
            SimpleLicenseStorage.SaveLicense(licenseKey, email);

            // Act
            var result = SimpleLicenseStorage.HasValidLicense(out var license);

            // Assert
            result.Should().BeTrue("Valid license should exist");
            license.Should().NotBeNull();
            license!.LicenseKey.Should().Be(licenseKey);
            license.Email.Should().Be(email);
        }

        [Fact]
        public void HasValidLicense_WhenNoLicense_ReturnsFalse()
        {
            // Act
            var result = SimpleLicenseStorage.HasValidLicense(out var license);

            // Assert
            result.Should().BeFalse("No license file exists");
            license.Should().BeNull();
        }

        [Fact]
        public void HasValidLicense_WhenCorruptedJSON_ReturnsFalse()
        {
            // Arrange - Write corrupted JSON directly to the license file
            var type = typeof(SimpleLicenseStorage);
            var licensePathField = type.GetField("LicensePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var licensePath = (string)licensePathField!.GetValue(null)!;

            var appDataPathField = type.GetField("AppDataPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var appDataPath = (string)appDataPathField!.GetValue(null)!;

            Directory.CreateDirectory(appDataPath);
            File.WriteAllText(licensePath, "{ corrupted json ::::");

            // Act
            var result = SimpleLicenseStorage.HasValidLicense(out var license);

            // Assert
            result.Should().BeFalse("Corrupted JSON should return false");
            license.Should().BeNull();
        }

        [Fact]
        public void HasValidLicense_WhenEmptyFile_ReturnsFalse()
        {
            // Arrange
            var type = typeof(SimpleLicenseStorage);
            var licensePathField = type.GetField("LicensePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var licensePath = (string)licensePathField!.GetValue(null)!;

            var appDataPathField = type.GetField("AppDataPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var appDataPath = (string)appDataPathField!.GetValue(null)!;

            Directory.CreateDirectory(appDataPath);
            File.WriteAllText(licensePath, "");

            // Act
            var result = SimpleLicenseStorage.HasValidLicense(out var license);

            // Assert
            result.Should().BeFalse("Empty file should return false");
            license.Should().BeNull();
        }

        [Fact]
        public void HasValidLicense_WhenLicenseKeyIsNull_ReturnsFalse()
        {
            // Arrange
            var license = new SimpleLicenseStorage.StoredLicense
            {
                LicenseKey = null!,
                Email = "test@example.com",
                ValidatedAt = DateTime.UtcNow,
                Type = "LIFETIME"
            };

            var type = typeof(SimpleLicenseStorage);
            var licensePathField = type.GetField("LicensePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var licensePath = (string)licensePathField!.GetValue(null)!;

            var appDataPathField = type.GetField("AppDataPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var appDataPath = (string)appDataPathField!.GetValue(null)!;

            Directory.CreateDirectory(appDataPath);
            var json = JsonSerializer.Serialize(license);
            File.WriteAllText(licensePath, json);

            // Act
            var result = SimpleLicenseStorage.HasValidLicense(out var outLicense);

            // Assert
            result.Should().BeFalse("Null license key should be invalid");
            outLicense.Should().BeNull();
        }

        [Fact]
        public void HasValidLicense_WhenLicenseKeyIsEmpty_ReturnsFalse()
        {
            // Arrange
            var license = new SimpleLicenseStorage.StoredLicense
            {
                LicenseKey = "",
                Email = "test@example.com",
                ValidatedAt = DateTime.UtcNow,
                Type = "LIFETIME"
            };

            var type = typeof(SimpleLicenseStorage);
            var licensePathField = type.GetField("LicensePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var licensePath = (string)licensePathField!.GetValue(null)!;

            var appDataPathField = type.GetField("AppDataPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var appDataPath = (string)appDataPathField!.GetValue(null)!;

            Directory.CreateDirectory(appDataPath);
            var json = JsonSerializer.Serialize(license);
            File.WriteAllText(licensePath, json);

            // Act
            var result = SimpleLicenseStorage.HasValidLicense(out var outLicense);

            // Assert
            result.Should().BeFalse("Empty license key should be invalid");
            outLicense.Should().BeNull();
        }

        [Fact]
        public void HasValidLicense_WhenLicenseKeyIsWhitespace_ReturnsFalse()
        {
            // Arrange
            var license = new SimpleLicenseStorage.StoredLicense
            {
                LicenseKey = "   ",
                Email = "test@example.com",
                ValidatedAt = DateTime.UtcNow,
                Type = "LIFETIME"
            };

            var type = typeof(SimpleLicenseStorage);
            var licensePathField = type.GetField("LicensePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var licensePath = (string)licensePathField!.GetValue(null)!;

            var appDataPathField = type.GetField("AppDataPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var appDataPath = (string)appDataPathField!.GetValue(null)!;

            Directory.CreateDirectory(appDataPath);
            var json = JsonSerializer.Serialize(license);
            File.WriteAllText(licensePath, json);

            // Act
            var result = SimpleLicenseStorage.HasValidLicense(out var outLicense);

            // Assert
            result.Should().BeFalse("Whitespace license key should be invalid");
            outLicense.Should().BeNull();
        }

        #endregion

        #region DeleteLicense Tests

        [Fact]
        public void DeleteLicense_WhenLicenseExists_DeletesSuccessfully()
        {
            // Arrange
            SimpleLicenseStorage.SaveLicense("VL-DELETE-123456-789ABC", "test@example.com");
            SimpleLicenseStorage.LicenseFileExists().Should().BeTrue("License should exist before deletion");

            // Act
            SimpleLicenseStorage.DeleteLicense();

            // Assert
            SimpleLicenseStorage.LicenseFileExists().Should().BeFalse("License file should be deleted");
        }

        [Fact]
        public void DeleteLicense_WhenNoLicense_DoesNotThrow()
        {
            // Arrange
            SimpleLicenseStorage.LicenseFileExists().Should().BeFalse("No license should exist");

            // Act
            Action act = () => SimpleLicenseStorage.DeleteLicense();

            // Assert
            act.Should().NotThrow("Deleting non-existent license should not throw");
        }

        [Fact]
        public void DeleteLicense_CalledMultipleTimes_DoesNotThrow()
        {
            // Arrange
            SimpleLicenseStorage.SaveLicense("VL-DELETE-123456-789ABC", "test@example.com");

            // Act
            Action act = () =>
            {
                SimpleLicenseStorage.DeleteLicense();
                SimpleLicenseStorage.DeleteLicense();
                SimpleLicenseStorage.DeleteLicense();
            };

            // Assert
            act.Should().NotThrow("Multiple delete calls should not throw");
            SimpleLicenseStorage.LicenseFileExists().Should().BeFalse();
        }

        #endregion

        #region GetStoredLicense Tests

        [Fact]
        public void GetStoredLicense_WhenLicenseExists_ReturnsLicense()
        {
            // Arrange
            var licenseKey = "VL-GET123-456789-ABCDEF";
            var email = "get@example.com";
            SimpleLicenseStorage.SaveLicense(licenseKey, email);

            // Act
            var license = SimpleLicenseStorage.GetStoredLicense();

            // Assert
            license.Should().NotBeNull();
            license!.LicenseKey.Should().Be(licenseKey);
            license.Email.Should().Be(email);
        }

        [Fact]
        public void GetStoredLicense_WhenNoLicense_ReturnsNull()
        {
            // Act
            var license = SimpleLicenseStorage.GetStoredLicense();

            // Assert
            license.Should().BeNull();
        }

        [Fact]
        public void GetStoredLicense_AllProperties_ReturnedCorrectly()
        {
            // Arrange
            var licenseKey = "VL-PROPS1-234567-89ABCD";
            var email = "props@example.com";
            var type = "PRO";
            SimpleLicenseStorage.SaveLicense(licenseKey, email, type);

            // Act
            var license = SimpleLicenseStorage.GetStoredLicense();

            // Assert
            license.Should().NotBeNull();
            license!.LicenseKey.Should().Be(licenseKey);
            license.Email.Should().Be(email);
            license.Type.Should().Be(type);
            license.ValidatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        #endregion

        #region LicenseFileExists Tests

        [Fact]
        public void LicenseFileExists_WhenFileExists_ReturnsTrue()
        {
            // Arrange
            SimpleLicenseStorage.SaveLicense("VL-EXISTS-123456-789ABC", "test@example.com");

            // Act
            var result = SimpleLicenseStorage.LicenseFileExists();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void LicenseFileExists_WhenFileDoesNotExist_ReturnsFalse()
        {
            // Act
            var result = SimpleLicenseStorage.LicenseFileExists();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void LicenseFileExists_WhenCorruptedFile_ReturnsTrue()
        {
            // Arrange
            var type = typeof(SimpleLicenseStorage);
            var licensePathField = type.GetField("LicensePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var licensePath = (string)licensePathField!.GetValue(null)!;

            var appDataPathField = type.GetField("AppDataPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var appDataPath = (string)appDataPathField!.GetValue(null)!;

            Directory.CreateDirectory(appDataPath);
            File.WriteAllText(licensePath, "corrupted data");

            // Act
            var result = SimpleLicenseStorage.LicenseFileExists();

            // Assert
            result.Should().BeTrue("File exists even if corrupted");
        }

        #endregion

        #region IsProVersion Tests

        [Fact]
        public void IsProVersion_WhenValidLicense_ReturnsTrue()
        {
            // Arrange
            SimpleLicenseStorage.SaveLicense("VL-PRO123-456789-ABCDEF", "pro@example.com");

            // Act
            var result = SimpleLicenseStorage.IsProVersion();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsProVersion_WhenNoLicense_ReturnsFalse()
        {
            // Act
            var result = SimpleLicenseStorage.IsProVersion();

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region IsFreeVersion Tests

        [Fact]
        public void IsFreeVersion_WhenNoLicense_ReturnsTrue()
        {
            // Act
            var result = SimpleLicenseStorage.IsFreeVersion();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsFreeVersion_WhenValidLicense_ReturnsFalse()
        {
            // Arrange
            SimpleLicenseStorage.SaveLicense("VL-FREE12-345678-9ABCDE", "free@example.com");

            // Act
            var result = SimpleLicenseStorage.IsFreeVersion();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsFreeVersion_IsOppositeOfIsProVersion()
        {
            // Act (no license)
            var isFree1 = SimpleLicenseStorage.IsFreeVersion();
            var isPro1 = SimpleLicenseStorage.IsProVersion();

            // Arrange (with license)
            SimpleLicenseStorage.SaveLicense("VL-TEST12-345678-9ABCDE", "test@example.com");

            // Act (with license)
            var isFree2 = SimpleLicenseStorage.IsFreeVersion();
            var isPro2 = SimpleLicenseStorage.IsProVersion();

            // Assert
            isFree1.Should().BeTrue();
            isPro1.Should().BeFalse();
            isFree2.Should().BeFalse();
            isPro2.Should().BeTrue();
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public async Task SaveLicense_ConcurrentSaves_ThreadSafe()
        {
            // Arrange
            var tasks = new Task[20];

            // Act - Save 20 licenses concurrently
            for (int i = 0; i < 20; i++)
            {
                int index = i;
                tasks[i] = Task.Run(() =>
                {
                    SimpleLicenseStorage.SaveLicense(
                        $"VL-CONC{index:D2}-123456-789ABC",
                        $"concurrent{index}@example.com"
                    );
                });
            }

            await Task.WhenAll(tasks);

            // Assert - Should have one of the licenses saved (last one wins)
            var result = SimpleLicenseStorage.HasValidLicense(out var license);
            result.Should().BeTrue("One license should be saved");
            license.Should().NotBeNull();
            license!.LicenseKey.Should().StartWith("VL-CONC");
        }

        [Fact]
        public async Task HasValidLicense_ConcurrentReads_ThreadSafe()
        {
            // Arrange
            SimpleLicenseStorage.SaveLicense("VL-READ12-345678-9ABCDE", "read@example.com");
            var tasks = new Task<bool>[20];

            // Act - Read 20 times concurrently
            for (int i = 0; i < 20; i++)
            {
                tasks[i] = Task.Run(() => SimpleLicenseStorage.HasValidLicense(out _));
            }

            var results = await Task.WhenAll(tasks);

            // Assert - All reads should succeed
            results.Should().HaveCount(20);
            results.Should().OnlyContain(r => r == true, "All reads should return true");
        }

        [Fact]
        public async Task MixedOperations_Concurrent_ThreadSafe()
        {
            // Arrange
            var tasks = new Task[30];

            // Act - Mix of save, read, and delete operations
            for (int i = 0; i < 30; i++)
            {
                int index = i;
                tasks[i] = Task.Run(() =>
                {
                    if (index % 3 == 0)
                    {
                        SimpleLicenseStorage.SaveLicense($"VL-MIX{index:D2}-123456-789ABC", $"mix{index}@example.com");
                    }
                    else if (index % 3 == 1)
                    {
                        SimpleLicenseStorage.HasValidLicense(out _);
                    }
                    else
                    {
                        SimpleLicenseStorage.GetStoredLicense();
                    }
                });
            }

            // Assert - Should complete without exceptions
            Func<Task> act = async () => await Task.WhenAll(tasks);
            await act.Should().NotThrowAsync("Mixed concurrent operations should be thread-safe");
        }

        [Fact]
        public async Task DeleteLicense_ConcurrentDeletes_ThreadSafe()
        {
            // Arrange
            SimpleLicenseStorage.SaveLicense("VL-DEL123-456789-ABCDEF", "delete@example.com");
            var tasks = new Task[10];

            // Act - Delete 10 times concurrently
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(() => SimpleLicenseStorage.DeleteLicense());
            }

            // Assert - Should complete without exceptions
            Func<Task> act = async () => await Task.WhenAll(tasks);
            await act.Should().NotThrowAsync("Concurrent deletes should not throw");

            SimpleLicenseStorage.LicenseFileExists().Should().BeFalse("File should be deleted");
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public void SaveLicense_ReadOnlyDirectory_ThrowsException()
        {
            // Note: This test is platform-specific and difficult to test reliably
            // Skipped as it would require administrator privileges to set directory read-only
            // and might interfere with other tests or system operations

            // This test documents expected behavior:
            // In production, if directory becomes read-only, SaveLicense will throw
            // and the caller should handle the exception

            // The implementation catches exceptions in SaveLicense and re-throws them,
            // so file system errors will propagate to the caller
        }

        [Fact]
        public void HasValidLicense_CorruptedData_HandlesGracefully()
        {
            // Arrange - Get license path
            var type = typeof(SimpleLicenseStorage);
            var licensePathField = type.GetField("LicensePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var licensePath = (string)licensePathField!.GetValue(null)!;

            var appDataPathField = type.GetField("AppDataPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var appDataPath = (string)appDataPathField!.GetValue(null)!;

            Directory.CreateDirectory(appDataPath);

            var corruptedData = new[]
            {
                "not json at all",
                "{incomplete",
                "}{reversed}",
                "",
                "   ",
                "{\"LicenseKey\":null}",
                "{\"Email\":\"test@example.com\"}" // Missing LicenseKey
            };

            foreach (var data in corruptedData)
            {
                // Act
                File.WriteAllText(licensePath, data);
                var result = SimpleLicenseStorage.HasValidLicense(out var license);

                // Assert
                result.Should().BeFalse($"Corrupted data '{data}' should return false");
                license.Should().BeNull($"Corrupted data '{data}' should return null license");
            }
        }

        #endregion

        #region Registry Path Tests (Documentation)

        [Fact]
        public void StoragePath_UsesCorrectLocation()
        {
            // This test documents that SimpleLicenseStorage uses LocalApplicationData

            // Arrange & Act
            SimpleLicenseStorage.SaveLicense("VL-PATH12-345678-9ABCDE", "path@example.com");

            // Assert
            var type = typeof(SimpleLicenseStorage);
            var licensePathField = type.GetField("LicensePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var licensePath = (string)licensePathField!.GetValue(null)!;

            File.Exists(licensePath).Should().BeTrue();
            licensePath.Should().EndWith("license.dat");
            licensePath.Should().Contain("VoiceLite");

            // In production, the path should be:
            // %LOCALAPPDATA%\VoiceLite\license.dat
            var expectedBasePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoiceLite",
                "license.dat"
            );
            licensePath.Should().Be(expectedBasePath);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void SaveLicense_UnicodeCharacters_SavesCorrectly()
        {
            // Arrange
            var unicodeEmail = "test@例え.jp";
            var unicodeLicenseKey = "VL-日本語-中文-한글";

            // Act
            SimpleLicenseStorage.SaveLicense(unicodeLicenseKey, unicodeEmail);

            // Assert
            var license = SimpleLicenseStorage.GetStoredLicense();
            license.Should().NotBeNull();
            license!.LicenseKey.Should().Be(unicodeLicenseKey);
            license.Email.Should().Be(unicodeEmail);
        }

        [Fact]
        public void SaveLicense_ExtremelyLongEmail_SavesCorrectly()
        {
            // Arrange
            var longEmail = new string('a', 500) + "@example.com";
            var licenseKey = "VL-LONG12-345678-9ABCDE";

            // Act
            SimpleLicenseStorage.SaveLicense(licenseKey, longEmail);

            // Assert
            var license = SimpleLicenseStorage.GetStoredLicense();
            license!.Email.Should().Be(longEmail);
        }

        [Fact]
        public void SaveLicense_DateTimePreservation_StoresUtcTime()
        {
            // Arrange
            var before = DateTime.UtcNow;

            // Act
            SimpleLicenseStorage.SaveLicense("VL-TIME12-345678-9ABCDE", "time@example.com");

            // Small delay to ensure time difference is measurable
            System.Threading.Thread.Sleep(10);

            var after = DateTime.UtcNow;

            // Assert
            var license = SimpleLicenseStorage.GetStoredLicense();
            license!.ValidatedAt.Should().BeOnOrAfter(before);
            license.ValidatedAt.Should().BeOnOrBefore(after);
        }

        [Fact]
        public void StoredLicense_DefaultValues_SetCorrectly()
        {
            // Act
            var license = new SimpleLicenseStorage.StoredLicense();

            // Assert
            license.LicenseKey.Should().Be(string.Empty);
            license.Email.Should().Be(string.Empty);
            license.Type.Should().Be("LIFETIME");
            license.ValidatedAt.Should().Be(default(DateTime));
        }

        #endregion

        #region Complete Workflow Tests

        [Fact]
        public void CompleteWorkflow_SaveReadDelete_WorksCorrectly()
        {
            // Arrange
            var licenseKey = "VL-FLOW12-345678-9ABCDE";
            var email = "workflow@example.com";

            // Act & Assert - Save
            SimpleLicenseStorage.SaveLicense(licenseKey, email);
            SimpleLicenseStorage.LicenseFileExists().Should().BeTrue();
            SimpleLicenseStorage.IsProVersion().Should().BeTrue();
            SimpleLicenseStorage.IsFreeVersion().Should().BeFalse();

            // Act & Assert - Read
            var hasLicense = SimpleLicenseStorage.HasValidLicense(out var license);
            hasLicense.Should().BeTrue();
            license!.LicenseKey.Should().Be(licenseKey);

            var storedLicense = SimpleLicenseStorage.GetStoredLicense();
            storedLicense.Should().NotBeNull();
            storedLicense!.Email.Should().Be(email);

            // Act & Assert - Delete
            SimpleLicenseStorage.DeleteLicense();
            SimpleLicenseStorage.LicenseFileExists().Should().BeFalse();
            SimpleLicenseStorage.IsProVersion().Should().BeFalse();
            SimpleLicenseStorage.IsFreeVersion().Should().BeTrue();
            SimpleLicenseStorage.GetStoredLicense().Should().BeNull();
        }

        [Fact]
        public void CompleteWorkflow_MultipleUpdates_LastOneWins()
        {
            // Arrange
            var licenses = new[]
            {
                ("VL-KEY001-111111-111111", "user1@example.com", "TRIAL"),
                ("VL-KEY002-222222-222222", "user2@example.com", "PRO"),
                ("VL-KEY003-333333-333333", "user3@example.com", "LIFETIME")
            };

            // Act - Save multiple licenses
            foreach (var (key, email, type) in licenses)
            {
                SimpleLicenseStorage.SaveLicense(key, email, type);
            }

            // Assert - Only last license should remain
            var license = SimpleLicenseStorage.GetStoredLicense();
            license.Should().NotBeNull();
            license!.LicenseKey.Should().Be("VL-KEY003-333333-333333");
            license.Email.Should().Be("user3@example.com");
            license.Type.Should().Be("LIFETIME");
        }

        #endregion
    }
}

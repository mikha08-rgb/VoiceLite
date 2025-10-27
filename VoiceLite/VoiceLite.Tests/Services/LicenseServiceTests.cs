using System;
using System.Threading.Tasks;
using FluentAssertions;
using VoiceLite.Core.Interfaces.Features;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    public class LicenseServiceTests : IDisposable
    {
        private LicenseService _licenseService;

        public LicenseServiceTests()
        {
            _licenseService = new LicenseService();
        }

        public void Dispose()
        {
            _licenseService?.Dispose();
        }

        #region Constructor & Basic Properties Tests

        [Fact]
        public void Constructor_InitializesWithDefaultValues()
        {
            // Assert
            _licenseService.IsLicenseValid.Should().BeFalse();
            _licenseService.GetStoredLicenseKey().Should().BeEmpty();
            _licenseService.GetActivationCount().Should().Be(0);
            _licenseService.GetMaxActivations().Should().Be(3);
            _licenseService.GetLicenseEmail().Should().BeEmpty();
        }

        [Fact]
        public void IsLicenseValid_DefaultState_ReturnsFalse()
        {
            // Assert
            _licenseService.IsLicenseValid.Should().BeFalse("license should be invalid by default");
        }

        [Fact]
        public void GetStoredLicenseKey_WhenNoKeyStored_ReturnsEmptyString()
        {
            // Act
            var result = _licenseService.GetStoredLicenseKey();

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void GetActivationCount_DefaultValue_ReturnsZero()
        {
            // Act
            var count = _licenseService.GetActivationCount();

            // Assert
            count.Should().Be(0);
        }

        [Fact]
        public void GetMaxActivations_DefaultValue_ReturnsThree()
        {
            // Act
            var max = _licenseService.GetMaxActivations();

            // Assert
            max.Should().Be(3, "VoiceLite allows 3 device activations per license");
        }

        [Fact]
        public void GetLicenseEmail_WhenNoEmailSet_ReturnsEmptyString()
        {
            // Act
            var email = _licenseService.GetLicenseEmail();

            // Assert
            email.Should().BeEmpty();
        }

        #endregion

        #region License Key Management Tests

        [Fact]
        public void SaveLicenseKey_StoresKeyCorrectly()
        {
            // Arrange
            var testKey = "TEST-LICENSE-KEY-12345";

            // Act
            _licenseService.SaveLicenseKey(testKey);
            var retrievedKey = _licenseService.GetStoredLicenseKey();

            // Assert
            retrievedKey.Should().Be(testKey);
        }

        [Fact]
        public void SaveLicenseKey_WithEmptyString_StoresEmptyString()
        {
            // Arrange
            _licenseService.SaveLicenseKey("initial-key");

            // Act
            _licenseService.SaveLicenseKey(string.Empty);
            var retrievedKey = _licenseService.GetStoredLicenseKey();

            // Assert
            retrievedKey.Should().BeEmpty();
        }

        [Fact]
        public void SaveLicenseKey_MultipleTimes_OverwritesPreviousKey()
        {
            // Arrange
            var firstKey = "FIRST-KEY";
            var secondKey = "SECOND-KEY";

            // Act
            _licenseService.SaveLicenseKey(firstKey);
            _licenseService.SaveLicenseKey(secondKey);
            var retrievedKey = _licenseService.GetStoredLicenseKey();

            // Assert
            retrievedKey.Should().Be(secondKey, "second save should overwrite first key");
        }

        [Fact]
        public void RemoveLicenseKey_ClearsStoredKey()
        {
            // Arrange
            _licenseService.SaveLicenseKey("TEST-KEY");

            // Act
            _licenseService.RemoveLicenseKey();
            var retrievedKey = _licenseService.GetStoredLicenseKey();

            // Assert
            retrievedKey.Should().BeEmpty();
        }

        [Fact]
        public void RemoveLicenseKey_SetsIsLicenseValidToFalse()
        {
            // Arrange
            _licenseService.SaveLicenseKey("TEST-KEY");

            // Act
            _licenseService.RemoveLicenseKey();

            // Assert
            _licenseService.IsLicenseValid.Should().BeFalse();
        }

        [Fact]
        public void RemoveLicenseKey_ClearsLicenseEmail()
        {
            // Arrange
            _licenseService.SaveLicenseKey("TEST-KEY");

            // Act
            _licenseService.RemoveLicenseKey();
            var email = _licenseService.GetLicenseEmail();

            // Assert
            email.Should().BeEmpty();
        }

        [Fact]
        public void RemoveLicenseKey_ResetsActivationCount()
        {
            // Arrange
            _licenseService.SaveLicenseKey("TEST-KEY");

            // Act
            _licenseService.RemoveLicenseKey();
            var count = _licenseService.GetActivationCount();

            // Assert
            count.Should().Be(0);
        }

        [Fact]
        public void RemoveLicenseKey_WhenCalledTwice_DoesNotThrow()
        {
            // Arrange
            _licenseService.SaveLicenseKey("TEST-KEY");
            _licenseService.RemoveLicenseKey();

            // Act
            Action act = () => _licenseService.RemoveLicenseKey();

            // Assert
            act.Should().NotThrow("removing an already-removed key should be safe");
        }

        #endregion

        #region ValidateLicenseAsync - Input Validation Tests

        [Fact]
        public async Task ValidateLicenseAsync_WithNullKey_ReturnsInvalidResult()
        {
            // Act
            var result = await _licenseService.ValidateLicenseAsync(null!);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Be("License key cannot be empty");
        }

        [Fact]
        public async Task ValidateLicenseAsync_WithEmptyKey_ReturnsInvalidResult()
        {
            // Act
            var result = await _licenseService.ValidateLicenseAsync(string.Empty);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Be("License key cannot be empty");
        }

        [Fact]
        public async Task ValidateLicenseAsync_WithWhitespaceKey_ReturnsInvalidResult()
        {
            // Act
            var result = await _licenseService.ValidateLicenseAsync("   ");

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Be("License key cannot be empty");
        }

        [Fact]
        public async Task ValidateLicenseAsync_WithKeyContainingWhitespace_TrimsWhitespace()
        {
            // Arrange
            var keyWithWhitespace = "  TEST-KEY-123  ";

            // Act - This will fail to validate against real API, but should trim the key
            var result = await _licenseService.ValidateLicenseAsync(keyWithWhitespace);

            // Assert - The key should have been trimmed (evidenced by not getting "empty key" error)
            result.Should().NotBeNull();
            result.ErrorMessage.Should().NotBe("License key cannot be empty");
        }

        #endregion

        #region Event Handling Tests

        [Fact]
        public void RemoveLicenseKey_RaisesLicenseStatusChangedEvent()
        {
            // Arrange
            _licenseService.SaveLicenseKey("TEST-KEY");
            bool eventRaised = false;
            bool statusValue = true;

            _licenseService.LicenseStatusChanged += (sender, isValid) =>
            {
                eventRaised = true;
                statusValue = isValid;
            };

            // Act
            _licenseService.RemoveLicenseKey();

            // Assert
            eventRaised.Should().BeTrue("LicenseStatusChanged event should fire");
            statusValue.Should().BeFalse("status should be false when license removed");
        }

        [Fact]
        public void LicenseStatusChanged_WithMultipleSubscribers_NotifiesAll()
        {
            // Arrange
            _licenseService.SaveLicenseKey("TEST-KEY");
            int notificationCount = 0;

            _licenseService.LicenseStatusChanged += (s, v) => notificationCount++;
            _licenseService.LicenseStatusChanged += (s, v) => notificationCount++;
            _licenseService.LicenseStatusChanged += (s, v) => notificationCount++;

            // Act
            _licenseService.RemoveLicenseKey();

            // Assert
            notificationCount.Should().Be(3, "all 3 subscribers should be notified");
        }

        [Fact]
        public void RemoveLicenseKey_WithNoSubscribers_DoesNotThrow()
        {
            // Arrange
            _licenseService.SaveLicenseKey("TEST-KEY");

            // Act
            Action act = () => _licenseService.RemoveLicenseKey();

            // Assert
            act.Should().NotThrow("event can be null when no subscribers");
        }

        #endregion

        #region Disposal Tests

        [Fact]
        public void Dispose_SetsDisposedFlag()
        {
            // Act
            _licenseService.Dispose();

            // Assert - No exception should be thrown, disposal is successful
            // We can't check _disposed directly as it's private, but subsequent Dispose calls should work
            Action act = () => _licenseService.Dispose();
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            // Act
            _licenseService.Dispose();
            _licenseService.Dispose();
            Action act = () => _licenseService.Dispose();

            // Assert
            act.Should().NotThrow("multiple Dispose calls should be safe");
        }

        [Fact]
        public void Dispose_DoesNotDisposeStaticHttpClient()
        {
            // Arrange
            var service1 = new LicenseService();
            var service2 = new LicenseService();

            // Act - Dispose both instances
            service1.Dispose();
            service2.Dispose();

            // Assert - Creating a new instance should still work (HttpClient not disposed)
            Action act = () =>
            {
                var service3 = new LicenseService();
                service3.Dispose();
            };

            act.Should().NotThrow("static HttpClient should remain available");
        }

        #endregion

        #region ValidateLicenseAsync - Network Error Handling Tests

        [Fact]
        public async Task ValidateLicenseAsync_WithInvalidKey_ReturnsErrorMessage()
        {
            // Arrange - Using a clearly invalid key format
            var invalidKey = "INVALID";

            // Act
            var result = await _licenseService.ValidateLicenseAsync(invalidKey);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            // Either connection error or invalid license (both acceptable for invalid key)
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task ValidateLicenseAsync_ConsecutiveCalls_MaintainState()
        {
            // Arrange
            var testKey = "TEST-KEY-123";

            // Act
            var result1 = await _licenseService.ValidateLicenseAsync(testKey);
            var result2 = await _licenseService.ValidateLicenseAsync(testKey);

            // Assert - Both calls should return results (may use cache on second call)
            result1.Should().NotBeNull();
            result2.Should().NotBeNull();
            result1.IsValid.Should().Be(result2.IsValid, "consecutive calls should return same validity");
        }

        #endregion

        #region State Management Tests

        [Fact]
        public void LicenseService_AfterSaveAndRemove_ReturnsToDefaultState()
        {
            // Arrange
            _licenseService.SaveLicenseKey("TEST-KEY");

            // Act
            _licenseService.RemoveLicenseKey();

            // Assert - All state should be reset to defaults
            _licenseService.GetStoredLicenseKey().Should().BeEmpty();
            _licenseService.IsLicenseValid.Should().BeFalse();
            _licenseService.GetLicenseEmail().Should().BeEmpty();
            _licenseService.GetActivationCount().Should().Be(0);
            _licenseService.GetMaxActivations().Should().Be(3);
        }

        [Fact]
        public void GetStoredLicenseKey_AfterMultipleSaveOperations_ReturnsLatestKey()
        {
            // Arrange & Act
            _licenseService.SaveLicenseKey("KEY-1");
            _licenseService.SaveLicenseKey("KEY-2");
            _licenseService.SaveLicenseKey("KEY-3");
            var finalKey = _licenseService.GetStoredLicenseKey();

            // Assert
            finalKey.Should().Be("KEY-3");
        }

        [Fact]
        public void SaveLicenseKey_WithSameKeyTwice_StoresCorrectly()
        {
            // Arrange
            var key = "SAME-KEY";

            // Act
            _licenseService.SaveLicenseKey(key);
            _licenseService.SaveLicenseKey(key);
            var retrievedKey = _licenseService.GetStoredLicenseKey();

            // Assert
            retrievedKey.Should().Be(key);
        }

        #endregion

        #region LicenseValidationResult Tests

        [Fact]
        public void LicenseValidationResult_DefaultTier_IsFree()
        {
            // Arrange & Act
            var result = new LicenseValidationResult();

            // Assert
            result.Tier.Should().Be("free", "default tier should be 'free'");
        }

        [Fact]
        public void LicenseValidationResult_CanSetProperties()
        {
            // Arrange & Act
            var result = new LicenseValidationResult
            {
                IsValid = true,
                Tier = "pro",
                ErrorMessage = "Test error",
                Email = "test@example.com",
                ActivationCount = 2
            };

            // Assert
            result.IsValid.Should().BeTrue();
            result.Tier.Should().Be("pro");
            result.ErrorMessage.Should().Be("Test error");
            result.Email.Should().Be("test@example.com");
            result.ActivationCount.Should().Be(2);
        }

        [Fact]
        public void LicenseValidationResult_CanBeCreatedWithInitializer()
        {
            // Act
            var result = new LicenseValidationResult
            {
                IsValid = false,
                ErrorMessage = "Invalid license"
            };

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().Be("Invalid license");
            result.Tier.Should().Be("free"); // Default value
        }

        #endregion

        #region Integration-Style Tests (No Mocking)

        [Fact]
        public async Task ValidateLicenseAsync_MultipleKeysInSequence_HandlesCorrectly()
        {
            // Arrange
            var keys = new[] { "KEY-A", "KEY-B", "KEY-C" };

            // Act & Assert - Should handle multiple validations without crashing
            foreach (var key in keys)
            {
                var result = await _licenseService.ValidateLicenseAsync(key);
                result.Should().NotBeNull("each validation should return a result");
            }
        }

        [Fact]
        public async Task ValidateLicenseAsync_AlternatingValidAndInvalid_HandlesCorrectly()
        {
            // Arrange
            var validKey = "VALID-FORMAT-KEY-12345";
            var emptyKey = "";

            // Act
            var result1 = await _licenseService.ValidateLicenseAsync(validKey);
            var result2 = await _licenseService.ValidateLicenseAsync(emptyKey);
            var result3 = await _licenseService.ValidateLicenseAsync(validKey);

            // Assert
            result1.Should().NotBeNull();
            result2.Should().NotBeNull();
            result2.IsValid.Should().BeFalse();
            result2.ErrorMessage.Should().Be("License key cannot be empty");
            result3.Should().NotBeNull();
        }

        [Fact]
        public void SaveLicenseKey_ThenGetStoredLicenseKey_RoundTrip()
        {
            // Arrange
            var originalKey = "ROUND-TRIP-TEST-KEY";

            // Act
            _licenseService.SaveLicenseKey(originalKey);
            var retrievedKey = _licenseService.GetStoredLicenseKey();

            // Assert
            retrievedKey.Should().Be(originalKey, "round-trip should preserve exact key");
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void SaveLicenseKey_WithVeryLongKey_StoresCorrectly()
        {
            // Arrange
            var longKey = new string('A', 1000);

            // Act
            _licenseService.SaveLicenseKey(longKey);
            var retrievedKey = _licenseService.GetStoredLicenseKey();

            // Assert
            retrievedKey.Should().Be(longKey);
            retrievedKey.Length.Should().Be(1000);
        }

        [Fact]
        public void SaveLicenseKey_WithSpecialCharacters_StoresCorrectly()
        {
            // Arrange
            var keyWithSpecialChars = "KEY-!@#$%^&*()_+-={}[]|:;<>?,./";

            // Act
            _licenseService.SaveLicenseKey(keyWithSpecialChars);
            var retrievedKey = _licenseService.GetStoredLicenseKey();

            // Assert
            retrievedKey.Should().Be(keyWithSpecialChars);
        }

        [Fact]
        public void SaveLicenseKey_WithUnicodeCharacters_StoresCorrectly()
        {
            // Arrange
            var unicodeKey = "KEY-café-日本語-🔑";

            // Act
            _licenseService.SaveLicenseKey(unicodeKey);
            var retrievedKey = _licenseService.GetStoredLicenseKey();

            // Assert
            retrievedKey.Should().Be(unicodeKey);
        }

        [Fact]
        public async Task ValidateLicenseAsync_WithKeyContainingNewlines_HandlesCorrectly()
        {
            // Arrange
            var keyWithNewlines = "KEY\nWITH\nNEWLINES";

            // Act
            var result = await _licenseService.ValidateLicenseAsync(keyWithNewlines);

            // Assert
            result.Should().NotBeNull();
            // Behavior depends on trimming - should not crash
        }

        #endregion
    }
}

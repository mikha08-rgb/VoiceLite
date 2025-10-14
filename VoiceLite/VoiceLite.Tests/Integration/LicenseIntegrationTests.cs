using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using VoiceLite.Models;
using VoiceLite.Services;
using VoiceLite.Tests.Helpers;
using Xunit;

namespace VoiceLite.Tests.Integration
{
    /// <summary>
    /// Integration tests for full license validation workflow
    /// Phase 3 of Test Coverage Improvements (TEST-001)
    /// Tests end-to-end flow: format check → API call → settings update
    /// </summary>
    [Trait("Category", "Integration")]
    public class LicenseIntegrationTests
    {
        #region Full Validation Workflow Tests

        [Fact]
        public async Task FullValidationFlow_ValidLicense_UpdatesSettings()
        {
            // Arrange
            var licenseKey = "VL-VALID0-123456-789ABC";
            var settings = new Settings();
            var mockHandler = new MockHttpMessageHandler(req =>
            {
                var responseJson = JsonSerializer.Serialize(new
                {
                    valid = true,
                    status = "ACTIVE",
                    type = "LIFETIME",
                    email = "test@example.com",
                    expiresAt = (string?)null
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                };
            });
            var httpClient = new HttpClient(mockHandler);
            var validator = new LicenseValidator(httpClient);

            // Act - Step 1: Format check
            var isValidFormat = LicenseValidator.IsValidFormat(licenseKey);

            // Act - Step 2: API validation
            var response = await validator.ValidateAsync(licenseKey);

            // Act - Step 3: Update settings (simulating what the UI does)
            if (response.valid)
            {
                settings.LicenseKey = licenseKey;
                settings.LicenseIsValid = true;
                settings.LicenseValidatedAt = DateTime.UtcNow;
            }

            // Assert
            isValidFormat.Should().BeTrue("License key format should be valid");
            response.valid.Should().BeTrue("API should validate license");
            response.status.Should().Be("ACTIVE");
            settings.LicenseKey.Should().Be(licenseKey, "Settings should store license key");
            settings.LicenseIsValid.Should().BeTrue("Settings should mark license as valid");
            settings.LicenseValidatedAt.Should().NotBeNull("Settings should store validation timestamp");
        }

        [Fact]
        public async Task FullValidationFlow_InvalidFormat_FailsEarly()
        {
            // Arrange
            var licenseKey = "INVALID-FORMAT";
            var settings = new Settings();
            var mockHandler = new MockHttpMessageHandler((Func<HttpRequestMessage, HttpResponseMessage>)(req =>
            {
                // API should not be called if format is invalid
                throw new InvalidOperationException("API should not be called for invalid format");
            }));
            var httpClient = new HttpClient(mockHandler);
            var validator = new LicenseValidator(httpClient);

            // Act - Step 1: Format check (should fail)
            var isValidFormat = LicenseValidator.IsValidFormat(licenseKey);

            // Act - Step 2: Skip API call if format invalid (simulating UI behavior)
            if (isValidFormat)
            {
                await validator.ValidateAsync(licenseKey);
            }

            // Assert
            isValidFormat.Should().BeFalse("Invalid format should be rejected");
            settings.LicenseKey.Should().BeNull("Settings should not be updated for invalid format");
            settings.LicenseIsValid.Should().BeFalse("Settings should remain invalid");
        }

        [Fact]
        public async Task FullValidationFlow_ExpiredLicense_UpdatesSettingsAsInvalid()
        {
            // Arrange
            var licenseKey = "VL-EXPIRE-123456-789ABC";
            var settings = new Settings();
            var mockHandler = new MockHttpMessageHandler(req =>
            {
                var responseJson = JsonSerializer.Serialize(new
                {
                    valid = false,
                    status = "EXPIRED",
                    error = "License has expired"
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                };
            });
            var httpClient = new HttpClient(mockHandler);
            var validator = new LicenseValidator(httpClient);

            // Act - Step 1: Format check
            var isValidFormat = LicenseValidator.IsValidFormat(licenseKey);

            // Act - Step 2: API validation
            var response = await validator.ValidateAsync(licenseKey);

            // Act - Step 3: Update settings (simulating UI behavior for expired license)
            settings.LicenseKey = licenseKey;
            settings.LicenseIsValid = response.valid;
            settings.LicenseValidatedAt = DateTime.UtcNow;

            // Assert
            isValidFormat.Should().BeTrue("License key format should be valid");
            response.valid.Should().BeFalse("Expired license should be invalid");
            response.status.Should().Be("EXPIRED");
            settings.LicenseKey.Should().Be(licenseKey, "Settings should store expired license key");
            settings.LicenseIsValid.Should().BeFalse("Settings should mark license as invalid");
            settings.LicenseValidatedAt.Should().NotBeNull("Settings should store validation timestamp even for expired license");
        }

        #endregion

        #region Network Failure Handling Tests

        [Fact]
        public async Task ValidationFlow_NetworkTimeout_PreservesExistingSettings()
        {
            // Arrange - User has valid cached license
            var licenseKey = "VL-CACHED-123456-789ABC";
            var settings = new Settings
            {
                LicenseKey = licenseKey,
                LicenseIsValid = true,
                LicenseValidatedAt = DateTime.UtcNow.AddDays(-1) // Last validated yesterday
            };
            var mockHandler = new MockHttpMessageHandler((Func<HttpRequestMessage, Task<HttpResponseMessage>>)(req =>
            {
                throw new TaskCanceledException("Network timeout");
            }));
            var httpClient = new HttpClient(mockHandler);
            var validator = new LicenseValidator(httpClient);

            // Act - Attempt revalidation (simulating user clicking "Validate" button)
            var response = await validator.ValidateAsync(licenseKey);

            // Act - UI should NOT update settings on network error (preserve cached state)
            // Only update if response.valid is true AND response.error is null
            if (response.valid && string.IsNullOrEmpty(response.error))
            {
                settings.LicenseIsValid = response.valid;
                settings.LicenseValidatedAt = DateTime.UtcNow;
            }

            // Assert
            response.valid.Should().BeFalse("Timeout should return valid=false");
            response.error.Should().Contain("timed out", "Error should mention timeout");
            settings.LicenseKey.Should().Be(licenseKey, "Cached license key should be preserved");
            settings.LicenseIsValid.Should().BeTrue("Cached validity should be preserved on network error");
            settings.LicenseValidatedAt!.Value.Should().BeCloseTo(DateTime.UtcNow.AddDays(-1), TimeSpan.FromSeconds(1), "Cached timestamp should be preserved");
        }

        [Fact]
        public async Task ValidationFlow_ServerError_PreservesExistingSettings()
        {
            // Arrange - User has valid cached license
            var licenseKey = "VL-CACHED-123456-789ABC";
            var settings = new Settings
            {
                LicenseKey = licenseKey,
                LicenseIsValid = true,
                LicenseValidatedAt = DateTime.UtcNow.AddHours(-2) // Last validated 2 hours ago
            };
            var mockHandler = new MockHttpMessageHandler(req =>
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Internal server error", Encoding.UTF8, "text/plain")
                };
            });
            var httpClient = new HttpClient(mockHandler);
            var validator = new LicenseValidator(httpClient);

            // Act - Attempt revalidation
            var response = await validator.ValidateAsync(licenseKey);

            // Act - UI should NOT update settings on server error (preserve cached state)
            if (response.valid && string.IsNullOrEmpty(response.error))
            {
                settings.LicenseIsValid = response.valid;
                settings.LicenseValidatedAt = DateTime.UtcNow;
            }

            // Assert
            response.valid.Should().BeFalse("Server error should return valid=false");
            response.error.Should().Contain("Validation failed", "Error should indicate validation failure");
            settings.LicenseKey.Should().Be(licenseKey, "Cached license key should be preserved");
            settings.LicenseIsValid.Should().BeTrue("Cached validity should be preserved on server error");
            settings.LicenseValidatedAt!.Value.Should().BeCloseTo(DateTime.UtcNow.AddHours(-2), TimeSpan.FromSeconds(1), "Cached timestamp should be preserved");
        }

        #endregion

        #region Settings Persistence Integration Tests

        [Fact]
        public async Task LicenseActivation_RoundTrip_SuccessfullyPersists()
        {
            // Arrange
            var licenseKey = "VL-ROUNDT-123456-789ABC";
            var mockHandler = new MockHttpMessageHandler(req =>
            {
                var responseJson = JsonSerializer.Serialize(new
                {
                    valid = true,
                    status = "ACTIVE",
                    type = "LIFETIME",
                    email = "roundtrip@example.com"
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                };
            });
            var httpClient = new HttpClient(mockHandler);
            var validator = new LicenseValidator(httpClient);

            // Act - Step 1: Validate license
            var response = await validator.ValidateAsync(licenseKey);

            // Act - Step 2: Update settings
            var settings = new Settings
            {
                LicenseKey = licenseKey,
                LicenseIsValid = response.valid,
                LicenseValidatedAt = DateTime.UtcNow
            };

            // Act - Step 3: Serialize settings (simulating save to disk)
            var json = JsonSerializer.Serialize(settings);

            // Act - Step 4: Deserialize settings (simulating load from disk)
            var loadedSettings = JsonSerializer.Deserialize<Settings>(json);

            // Assert
            loadedSettings.Should().NotBeNull();
            loadedSettings!.LicenseKey.Should().Be(licenseKey, "License key should survive round-trip");
            loadedSettings.LicenseIsValid.Should().BeTrue("License validity should survive round-trip");
            loadedSettings.LicenseValidatedAt.Should().NotBeNull("Validation timestamp should survive round-trip");
        }

        #endregion
    }
}

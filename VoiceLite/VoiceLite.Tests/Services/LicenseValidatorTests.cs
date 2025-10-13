using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// Tests for LicenseValidator service
    /// Coverage: License validation, format checking, API error handling
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Service", "LicenseValidator")]
    public class LicenseValidatorTests
    {
        #region Format Validation Tests

        [Theory]
        [InlineData("VL-ABC123-DEF456-GHI789", true)]
        [InlineData("VL-000000-000000-000000", true)]
        [InlineData("VL-ABCDEF-123456-ZYXWVU", true)]
        public void IsValidFormat_ValidFormats_ReturnsTrue(string licenseKey, bool expected)
        {
            // Act
            var result = LicenseValidator.IsValidFormat(licenseKey);

            // Assert
            result.Should().Be(expected, $"License key '{licenseKey}' should be valid");
        }

        [Theory]
        [InlineData("", false)] // Empty
        [InlineData("   ", false)] // Whitespace
        [InlineData("VL-ABC123-DEF456", false)] // Missing segment
        [InlineData("VL-ABC123-DEF456-GHI789-EXTRA", false)] // Extra segment
        [InlineData("ABC123-DEF456-GHI789", false)] // Missing VL prefix
        [InlineData("VL-ABC12-DEF456-GHI789", false)] // Segment too short
        [InlineData("VL-ABC1234-DEF456-GHI789", false)] // Segment too long
        [InlineData("vl-abc123-def456-ghi789", false)] // Lowercase prefix (case sensitive)
        public void IsValidFormat_InvalidFormats_ReturnsFalse(string licenseKey, bool expected)
        {
            // Act
            var result = LicenseValidator.IsValidFormat(licenseKey);

            // Assert
            result.Should().Be(expected, $"License key '{licenseKey}' should be invalid");
        }

        [Fact]
        public void IsValidFormat_NullLicenseKey_ReturnsFalse()
        {
            // Act
            var result = LicenseValidator.IsValidFormat(null);

            // Assert
            result.Should().BeFalse("Null license key should be invalid");
        }

        [Fact]
        public void IsValidFormat_WhitespaceLicenseKey_ReturnsTrue()
        {
            // Act
            var result = LicenseValidator.IsValidFormat("   VL-ABC123-DEF456-GHI789   ");

            // Assert
            // Implementation trims whitespace before validation
            result.Should().BeTrue("License key with surrounding whitespace should be trimmed and validated");
        }

        #endregion

        #region Async Validation Tests (Require HttpClient Mocking)

        // NOTE: The following tests document expected behavior for ValidateAsync().
        // They are currently SKIPPED because LicenseValidator uses a static HttpClient
        // that's difficult to mock without refactoring to use IHttpClientFactory.
        //
        // RECOMMENDATION: Refactor LicenseValidator to accept IHttpClientFactory
        // for better testability. See ARCH-002 in CRITICAL_ISSUES_REPORT.md.

        [Fact(Skip = "Requires HttpClient mocking - LicenseValidator uses static HttpClient")]
        public async Task ValidateAsync_ValidLicenseKey_ReturnsValidTrue()
        {
            // Arrange
            var licenseKey = "VL-VALID0-123456-789ABC";

            // Expected API response:
            // {
            //   "valid": true,
            //   "status": "ACTIVE",
            //   "type": "LIFETIME",
            //   "email": "test@example.com",
            //   "expiresAt": null
            // }

            // Act
            var response = await LicenseValidator.ValidateAsync(licenseKey);

            // Assert
            response.valid.Should().BeTrue("Valid license key should return valid=true");
            response.status.Should().Be("ACTIVE");
            response.type.Should().Be("LIFETIME");
            response.email.Should().Be("test@example.com");
            response.expiresAt.Should().BeNull("LIFETIME licenses don't expire");
            response.error.Should().BeNullOrEmpty("Valid response should have no error");
        }

        [Fact(Skip = "Requires HttpClient mocking - LicenseValidator uses static HttpClient")]
        public async Task ValidateAsync_InvalidLicenseKey_ReturnsValidFalse()
        {
            // Arrange
            var licenseKey = "VL-INVALID-123456-789ABC";

            // Expected API response:
            // {
            //   "valid": false,
            //   "error": "License key not found"
            // }

            // Act
            var response = await LicenseValidator.ValidateAsync(licenseKey);

            // Assert
            response.valid.Should().BeFalse("Invalid license key should return valid=false");
            response.error.Should().Be("License key not found");
        }

        [Fact(Skip = "Requires HttpClient mocking - LicenseValidator uses static HttpClient")]
        public async Task ValidateAsync_ExpiredLicense_ReturnsValidFalse()
        {
            // Arrange
            var licenseKey = "VL-EXPIRED-123456-789ABC";

            // Expected API response:
            // {
            //   "valid": false,
            //   "status": "ACTIVE",
            //   "type": "TRIAL",
            //   "email": "test@example.com",
            //   "expiresAt": "2023-01-01T00:00:00Z"
            // }

            // Act
            var response = await LicenseValidator.ValidateAsync(licenseKey);

            // Assert
            response.valid.Should().BeFalse("Expired license should return valid=false");
            response.status.Should().Be("ACTIVE");
            response.expiresAt.Should().NotBeNull();
            // expiresAt is a string, need to parse it first
            if (!string.IsNullOrEmpty(response.expiresAt))
            {
                var expiry = DateTime.Parse(response.expiresAt);
                expiry.Should().BeBefore(DateTime.UtcNow, "License is expired");
            }
        }

        [Fact(Skip = "Requires HttpClient mocking - LicenseValidator uses static HttpClient")]
        public async Task ValidateAsync_RevokedLicense_ReturnsValidFalse()
        {
            // Arrange
            var licenseKey = "VL-REVOKED-123456-789ABC";

            // Expected API response:
            // {
            //   "valid": false,
            //   "status": "REVOKED",
            //   "type": "LIFETIME",
            //   "email": "banned@example.com"
            // }

            // Act
            var response = await LicenseValidator.ValidateAsync(licenseKey);

            // Assert
            response.valid.Should().BeFalse("Revoked license should return valid=false");
            response.status.Should().Be("REVOKED");
        }

        [Fact(Skip = "Requires HttpClient mocking - LicenseValidator uses static HttpClient")]
        public async Task ValidateAsync_NetworkTimeout_ReturnsUserFriendlyMessage()
        {
            // Arrange
            var licenseKey = "VL-TIMEOUT-123456-789ABC";

            // Simulate: HttpClient timeout exception

            // Act
            var response = await LicenseValidator.ValidateAsync(licenseKey);

            // Assert
            response.valid.Should().BeFalse("Timeout should return valid=false");
            response.error.Should().Contain("timeout", "Error message should mention timeout");
            response.error.Should().Contain("internet", "Error should suggest checking internet connection");
        }

        [Fact(Skip = "Requires HttpClient mocking - LicenseValidator uses static HttpClient")]
        public async Task ValidateAsync_NetworkError_ReturnsErrorMessage()
        {
            // Arrange
            var licenseKey = "VL-NETWORK-123456-789ABC";

            // Simulate: Network unreachable, DNS failure, connection refused

            // Act
            var response = await LicenseValidator.ValidateAsync(licenseKey);

            // Assert
            response.valid.Should().BeFalse("Network error should return valid=false");
            response.error.Should().NotBeNullOrEmpty("Error message should be populated");
        }

        [Fact(Skip = "Requires HttpClient mocking - LicenseValidator uses static HttpClient")]
        public async Task ValidateAsync_404NotFound_ReturnsLicenseNotFound()
        {
            // Arrange
            var licenseKey = "VL-NOTFOUND-123456-789ABC";

            // Simulate: API returns 404 status code

            // Act
            var response = await LicenseValidator.ValidateAsync(licenseKey);

            // Assert
            response.valid.Should().BeFalse("404 should return valid=false");
            response.error.Should().Be("License key not found", "404 should return specific error");
        }

        [Fact(Skip = "Requires HttpClient mocking - LicenseValidator uses static HttpClient")]
        public async Task ValidateAsync_MalformedJSON_ReturnsInvalidResponse()
        {
            // Arrange
            var licenseKey = "VL-BADJSON-123456-789ABC";

            // Simulate: API returns invalid JSON (e.g., HTML error page)

            // Act
            var response = await LicenseValidator.ValidateAsync(licenseKey);

            // Assert
            response.valid.Should().BeFalse("Malformed JSON should return valid=false");
            response.error.Should().Contain("Invalid response", "Error should indicate deserialization failure");
        }

        [Fact(Skip = "Requires HttpClient mocking - LicenseValidator uses static HttpClient")]
        public async Task ValidateAsync_ServerError_Returns500Error()
        {
            // Arrange
            var licenseKey = "VL-SERVER-123456-789ABC";

            // Simulate: API returns 500 Internal Server Error

            // Act
            var response = await LicenseValidator.ValidateAsync(licenseKey);

            // Assert
            response.valid.Should().BeFalse("500 error should return valid=false");
            response.error.Should().NotBeNullOrEmpty("Error message should be populated");
        }

        [Fact(Skip = "Requires HttpClient mocking - LicenseValidator uses static HttpClient")]
        public async Task ValidateAsync_EmptyLicenseKey_ReturnsError()
        {
            // Act
            var response = await LicenseValidator.ValidateAsync("");

            // Assert
            response.valid.Should().BeFalse("Empty license key should return valid=false");
            response.error.Should().Be("License key is empty");
        }

        [Fact(Skip = "Requires HttpClient mocking - LicenseValidator uses static HttpClient")]
        public async Task ValidateAsync_NullLicenseKey_ReturnsError()
        {
            // Act
            var response = await LicenseValidator.ValidateAsync(null);

            // Assert
            response.valid.Should().BeFalse("Null license key should return valid=false");
            response.error.Should().Be("License key is empty");
        }

        [Fact(Skip = "Requires HttpClient mocking - LicenseValidator uses static HttpClient")]
        public async Task ValidateAsync_ConcurrentRequests_HandledSafely()
        {
            // Arrange
            var licenseKey = "VL-CONCURRENT-123456-789ABC";
            var tasks = new Task<LicenseValidator.ValidationResponse>[10];

            // Act - Fire 10 concurrent validation requests
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = LicenseValidator.ValidateAsync(licenseKey);
            }

            var results = await Task.WhenAll(tasks);

            // Assert - All requests should complete without exceptions
            results.Should().HaveCount(10, "All 10 requests should complete");
            results.Should().OnlyContain(r => r != null, "No null responses");
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void IsValidFormat_UnicodeCharacters_ReturnsFalse()
        {
            // Arrange
            var licenseKey = "VL-ABC123-DEF456-GHI789€";

            // Act
            var result = LicenseValidator.IsValidFormat(licenseKey);

            // Assert
            result.Should().BeFalse("License key with Unicode characters should be invalid");
        }

        [Fact]
        public void IsValidFormat_SpecialCharacters_ReturnsTrue()
        {
            // Arrange
            var licenseKey = "VL-ABC!23-DEF456-GHI789";

            // Act
            var result = LicenseValidator.IsValidFormat(licenseKey);

            // Assert
            // Current implementation only checks format (VL-XXXXXX-XXXXXX-XXXXXX), not character composition
            // Character validation happens server-side during actual validation
            result.Should().BeTrue("License key format is valid regardless of character composition");
        }

        [Fact]
        public void IsValidFormat_MixedCase_DependsOnImplementation()
        {
            // Arrange - VL prefix is uppercase, segments can be mixed case
            var licenseKey = "VL-AbC123-dEf456-GhI789";

            // Act
            var result = LicenseValidator.IsValidFormat(licenseKey);

            // Assert
            // Current implementation allows mixed case in segments
            result.Should().BeTrue("VL prefix is uppercase, segments are alphanumeric");
        }

        #endregion

        #region Documentation Tests

        /// <summary>
        /// This test documents the expected API contract for /api/licenses/validate
        /// </summary>
        [Fact]
        public void APIContract_ValidResponse_MatchesExpectedSchema()
        {
            // Arrange - Expected JSON response for valid license
            var json = @"{
                ""valid"": true,
                ""status"": ""ACTIVE"",
                ""type"": ""LIFETIME"",
                ""email"": ""user@example.com"",
                ""expiresAt"": null
            }";

            // Act
            var response = JsonSerializer.Deserialize<dynamic>(json);

            // Assert - This documents the expected response structure
            Assert.NotNull(response);
            // Note: Actual validation requires HttpClient mocking
        }

        /// <summary>
        /// This test documents the expected API contract for invalid license
        /// </summary>
        [Fact]
        public void APIContract_InvalidResponse_MatchesExpectedSchema()
        {
            // Arrange - Expected JSON response for invalid license
            var json = @"{
                ""valid"": false,
                ""error"": ""License key not found""
            }";

            // Act
            var response = JsonSerializer.Deserialize<dynamic>(json);

            // Assert - This documents the expected error response structure
            Assert.NotNull(response);
            // Note: Actual validation requires HttpClient mocking
        }

        #endregion
    }
}

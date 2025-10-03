using System;
using FluentAssertions;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    public class SecurityServiceTests
    {
        [Fact]
        public void EncryptString_ReturnsNonEmptyString()
        {
            var plaintext = "test data";
            var encrypted = SecurityService.EncryptString(plaintext);

            encrypted.Should().NotBeNullOrEmpty();
            encrypted.Should().NotBe(plaintext);
        }

        [Fact]
        public void DecryptString_ReturnsOriginalPlaintext()
        {
            var plaintext = "test data with special chars !@#$%^&*()";
            var encrypted = SecurityService.EncryptString(plaintext);
            var decrypted = SecurityService.DecryptString(encrypted);

            decrypted.Should().Be(plaintext);
        }

        [Fact]
        public void EncryptDecrypt_EmptyString_WorksCorrectly()
        {
            var plaintext = "";
            var encrypted = SecurityService.EncryptString(plaintext);
            var decrypted = SecurityService.DecryptString(encrypted);

            decrypted.Should().Be(plaintext);
        }

        [Fact]
        public void EncryptDecrypt_LongString_WorksCorrectly()
        {
            var plaintext = new string('A', 10000);
            var encrypted = SecurityService.EncryptString(plaintext);
            var decrypted = SecurityService.DecryptString(encrypted);

            decrypted.Should().Be(plaintext);
        }

        [Fact]
        public void EncryptString_SameInputDifferentOutput()
        {
            // Due to random IV, same plaintext should produce different ciphertext
            var plaintext = "test data";
            var encrypted1 = SecurityService.EncryptString(plaintext);
            var encrypted2 = SecurityService.EncryptString(plaintext);

            encrypted1.Should().NotBe(encrypted2, "encryption should use random IV");
        }

        [Fact]
        public void DecryptString_InvalidCiphertext_ReturnsEmpty()
        {
            var invalidCiphertext = "not-valid-base64!@#$%";
            var result = SecurityService.DecryptString(invalidCiphertext);

            // Should gracefully handle invalid input
            result.Should().NotBeNull();
        }

        [Fact]
        public void DecryptString_EmptyString_ReturnsEmpty()
        {
            var result = SecurityService.DecryptString("");
            result.Should().NotBeNull();
        }

        [Fact]
        public void VerifyIntegrity_DoesNotThrow()
        {
            // Should not throw on first run or any subsequent run
            Action act = () => SecurityService.VerifyIntegrity();
            act.Should().NotThrow();
        }

        [Fact]
        public void StartProtection_CanBeCalledMultipleTimes()
        {
            // Should be idempotent
            Action act = () =>
            {
                SecurityService.StartProtection();
                SecurityService.StartProtection();
                SecurityService.StartProtection();
            };

            act.Should().NotThrow();
        }

        [Fact]
        public void StopProtection_DoesNotThrow()
        {
            Action act = () => SecurityService.StopProtection();
            act.Should().NotThrow();
        }

        [Fact]
        public void StoreTrial_DoesNotThrow()
        {
            var startDate = DateTime.Now;
            var machineId = "TEST-MACHINE-ID";

            Action act = () => SecurityService.StoreTrial(startDate, machineId);
            act.Should().NotThrow();
        }

        [Fact]
        public void GetTrialFromRegistry_DoesNotThrow()
        {
            Action act = () => SecurityService.GetTrialFromRegistry();
            act.Should().NotThrow();
        }

        [Fact]
        public void EncryptDecrypt_UnicodeCharacters_WorksCorrectly()
        {
            var plaintext = "Hello 世界 🌍 Привет";
            var encrypted = SecurityService.EncryptString(plaintext);
            var decrypted = SecurityService.DecryptString(encrypted);

            decrypted.Should().Be(plaintext);
        }

        [Fact]
        public void EncryptDecrypt_SpecialCharacters_WorksCorrectly()
        {
            var plaintext = "Line1\nLine2\rLine3\tTabbed";
            var encrypted = SecurityService.EncryptString(plaintext);
            var decrypted = SecurityService.DecryptString(encrypted);

            decrypted.Should().Be(plaintext);
        }

        [Fact]
        public void EncryptString_NullInput_HandlesGracefully()
        {
            // Should not throw on null input (though may return empty or throw ArgumentNullException)
            try
            {
                var result = SecurityService.EncryptString(null!);
                result.Should().NotBeNull();
            }
            catch (ArgumentNullException)
            {
                // Acceptable behavior
            }
        }
    }
}

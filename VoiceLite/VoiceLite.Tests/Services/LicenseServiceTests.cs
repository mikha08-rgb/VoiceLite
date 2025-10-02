using System;
using System.Text;
using FluentAssertions;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using VoiceLite.Services.Licensing;
using Xunit;

namespace VoiceLite.Tests.Services
{
    public class LicenseServiceTests
    {
        [Fact]
        public void VerifySignedLicense_UsesProvidedPublicKey()
        {
            var (signedLicense, publicKeyB64) = CreateSignedBlob(payloadJson: "{\"license_id\":\"test\"}");
            var service = new LicenseService();

            service.VerifySignedLicense(signedLicense, publicKeyB64).Should().BeTrue();
        }

        [Fact]
        public void VerifyAndParseCRL_RespectsEnvironmentOverride()
        {
            var (signedCrl, publicKeyB64) = CreateSignedBlob(payloadJson: "{\"version\":1,\"updated_at\":\"2024-01-01T00:00:00Z\",\"revoked_license_ids\":[],\"key_version\":1}");
            Environment.SetEnvironmentVariable("VOICELITE_CRL_PUBLIC_KEY", publicKeyB64);
            try
            {
                var service = new LicenseService();
                var crl = service.VerifyAndParseCRL(signedCrl);

                crl.Should().NotBeNull();
                crl!.RevokedLicenseIds.Should().BeEmpty();
            }
            finally
            {
                Environment.SetEnvironmentVariable("VOICELITE_CRL_PUBLIC_KEY", null);
            }
        }

        private static (string signedPayload, string publicKeyB64) CreateSignedBlob(string payloadJson)
        {
            var generator = new Ed25519KeyPairGenerator();
            generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
            var keyPair = generator.GenerateKeyPair();
            var privateKey = (Ed25519PrivateKeyParameters)keyPair.Private;
            var publicKey = (Ed25519PublicKeyParameters)keyPair.Public;

            var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
            var payloadB64 = Base64UrlEncode(payloadBytes);

            var signer = new Org.BouncyCastle.Crypto.Signers.Ed25519Signer();
            signer.Init(true, privateKey);
            signer.BlockUpdate(payloadBytes, 0, payloadBytes.Length);
            var signature = signer.GenerateSignature();
            var signatureB64 = Base64UrlEncode(signature);

            var publicKeyB64 = Base64UrlEncode(publicKey.GetEncoded());
            return ($"{payloadB64}.{signatureB64}", publicKeyB64);
        }

        private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
        {
            var base64 = Convert.ToBase64String(bytes);
            return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }
    }
}

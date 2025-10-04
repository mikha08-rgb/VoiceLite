using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Management;
using VoiceLite.Services.Auth;
using VoiceLite.Models;
using Org.BouncyCastle.Crypto.Signers;

namespace VoiceLite.Services.Licensing
{
    public sealed class LicenseService : ILicenseService
    {
        private const string ActivateEndpoint = "/api/licenses/activate";
        private const string ProfileEndpoint = "/api/me";
        private const string CRLEndpoint = "/api/licenses/crl";
        private const string IssueEndpoint = "/api/licenses/issue";

        private const string LicensePublicKeyEnvVar = "VOICELITE_LICENSE_PUBLIC_KEY";
        private const string CrlPublicKeyEnvVar = "VOICELITE_CRL_PUBLIC_KEY";
        private const string LicensePublicKeyFallback = "fRR5l40q-wt8ptAFcOGsWIBHtLDBjnb_T3Z9HMLwgCc";
        private const string CrlPublicKeyFallback = "19Y5ul1S-ISjja7f827O5epfupvaBBMyhb_uVWLLf8M";

        private static string ResolvedLicensePublicKey => GetKeyOrFallback(LicensePublicKeyEnvVar, LicensePublicKeyFallback);
        private static string ResolvedCrlPublicKey => GetKeyOrFallback(CrlPublicKeyEnvVar, CrlPublicKeyFallback, LicensePublicKeyFallback);


        private readonly LicenseStorage storage = new();
        private LicenseStatus cachedStatus = LicenseStatus.Unknown;
        private string? activeLicenseKey;

        public async Task<LicenseStatus> GetCurrentStatusAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await ApiClient.Client.GetAsync(ProfileEndpoint, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    cachedStatus = LicenseStatus.Unknown;
                    activeLicenseKey = null;
                    return cachedStatus;
                }

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var profile = await JsonSerializer.DeserializeAsync<ProfileResponse>(stream, ApiClient.JsonOptions, cancellationToken).ConfigureAwait(false);

                if (profile?.licenses is null || profile.licenses.Length == 0)
                {
                    cachedStatus = LicenseStatus.Unlicensed;
                    activeLicenseKey = null;
                    return cachedStatus;
                }

                var active = profile.licenses.FirstOrDefault(l => l.status == "ACTIVE");
                if (active != null)
                {
                    cachedStatus = LicenseStatus.Active;
                    activeLicenseKey = active.licenseKey;
                    return cachedStatus;
                }

                if (profile.licenses.Any(l => l.status == "EXPIRED"))
                {
                    cachedStatus = LicenseStatus.Expired;
                    activeLicenseKey = null;
                    return cachedStatus;
                }

                cachedStatus = LicenseStatus.Unlicensed;
                activeLicenseKey = null;
                return cachedStatus;
            }
            catch
            {
                cachedStatus = LicenseStatus.Unknown;
                activeLicenseKey = null;
                return cachedStatus;
            }
        }

        public async Task SyncAsync(CancellationToken cancellationToken = default)
        {
            if (activeLicenseKey is null)
            {
                await GetCurrentStatusAsync(cancellationToken).ConfigureAwait(false);
            }

            if (cachedStatus != LicenseStatus.Active || string.IsNullOrEmpty(activeLicenseKey))
            {
                return;
            }

            var payload = JsonSerializer.Serialize(new
            {
                licenseKey = activeLicenseKey,
                machineId = GetMachineId(),
                machineLabel = Environment.MachineName,
                machineHash = GetMachineHash(),
            }, ApiClient.JsonOptions);

            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await ApiClient.Client.PostAsync(ActivateEndpoint, content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "License activation failed" : error);
            }
        }

        private static string GetMachineId()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
                var cpuId = searcher.Get().Cast<ManagementObject>().FirstOrDefault()?["ProcessorId"]?.ToString() ?? "UNKNOWN";
                var machineGuid = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography")?.GetValue("MachineGuid")?.ToString() ?? "UNKNOWN";
                var combined = $"{cpuId}|{machineGuid}|VoiceLite";
                using var sha = SHA256.Create();
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(combined));
                return Convert.ToBase64String(hash).Substring(0, 32);
            }
            catch
            {
                return Environment.MachineName;
            }
        }

        private static string GetMachineHash()
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(Environment.MachineName + Environment.UserName);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Verify Ed25519 signature on a signed license string.
        /// </summary>
        /// <param name="signedLicense">Format: base64url(payload).base64url(signature)</param>
        /// <param name="publicKeyB64">Base64url encoded public key (32 bytes). If null, uses the configured VoiceLite license verification key.</param>
        /// <returns>True if signature is valid, false otherwise</returns>
        public bool VerifySignedLicense(string signedLicense, string? publicKeyB64 = null)
        {
            try
            {
                var parts = signedLicense.Split('.');
                if (parts.Length != 2) return false;

                var payloadBytes = Base64UrlDecode(parts[0]);
                var signatureBytes = Base64UrlDecode(parts[1]);
                var publicKeyBytes = Base64UrlDecode(publicKeyB64 ?? ResolvedLicensePublicKey);

                if (publicKeyBytes.Length != 32 || signatureBytes.Length != 64)
                {
                    return false;
                }

                var signer = new Ed25519Signer();
                var publicKeyParams = new Org.BouncyCastle.Crypto.Parameters.Ed25519PublicKeyParameters(publicKeyBytes, 0);
                signer.Init(false, publicKeyParams);
                signer.BlockUpdate(payloadBytes, 0, payloadBytes.Length);

                return signer.VerifySignature(signatureBytes);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Parse and deserialize a signed license into a LicensePayload object.
        /// Does NOT verify the signature - call VerifySignedLicense first.
        /// </summary>
        public LicensePayload? ParseLicensePayload(string signedLicense)
        {
            try
            {
                var parts = signedLicense.Split('.');
                if (parts.Length != 2) return null;

                var payloadBytes = Base64UrlDecode(parts[0]);
                var json = Encoding.UTF8.GetString(payloadBytes);

                return JsonSerializer.Deserialize<LicensePayload>(json, ApiClient.JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Verify CRL signature and parse payload.
        /// </summary>
        public CRLPayload? VerifyAndParseCRL(string signedCRL, string? publicKeyB64 = null)
        {
            try
            {
                var parts = signedCRL.Split('.');
                if (parts.Length != 2) return null;

                var payloadBytes = Base64UrlDecode(parts[0]);
                var signatureBytes = Base64UrlDecode(parts[1]);
                var publicKeyBytes = Base64UrlDecode(publicKeyB64 ?? ResolvedCrlPublicKey);

                if (publicKeyBytes.Length != 32 || signatureBytes.Length != 64)
                {
                    return null;
                }

                var signer = new Ed25519Signer();
                var publicKeyParams = new Org.BouncyCastle.Crypto.Parameters.Ed25519PublicKeyParameters(publicKeyBytes, 0);
                signer.Init(false, publicKeyParams);
                signer.BlockUpdate(payloadBytes, 0, payloadBytes.Length);

                if (!signer.VerifySignature(signatureBytes))
                {
                    return null;
                }

                var json = Encoding.UTF8.GetString(payloadBytes);
                return JsonSerializer.Deserialize<CRLPayload>(json, ApiClient.JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Fetch the latest CRL from the server and cache it locally.
        /// </summary>
        public async Task<bool> RefreshCRLAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await ApiClient.Client.GetAsync(CRLEndpoint, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var crlResponse = await JsonSerializer.DeserializeAsync<CRLResponse>(stream, ApiClient.JsonOptions, cancellationToken).ConfigureAwait(false);

                if (crlResponse?.crl is null)
                {
                    return false;
                }

                // Verify CRL signature before saving
                var crlPayload = VerifyAndParseCRL(crlResponse.crl);
                if (crlPayload is null)
                {
                    return false;
                }

                storage.SaveCRL(crlResponse.crl);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if a license ID is revoked according to the cached CRL.
        /// Automatically refreshes CRL if it's older than 24 hours.
        /// </summary>
        public async Task<bool> IsLicenseRevokedAsync(string licenseId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Refresh CRL if needed
                if (storage.ShouldRefreshCRL())
                {
                    await RefreshCRLAsync(cancellationToken).ConfigureAwait(false);
                }

                var signedCRL = storage.LoadCRL();
                if (signedCRL is null)
                {
                    return false; // No CRL available, assume not revoked
                }

                var crlPayload = VerifyAndParseCRL(signedCRL);
                if (crlPayload is null)
                {
                    return false; // Invalid CRL, assume not revoked
                }

                return crlPayload.RevokedLicenseIds.Contains(licenseId);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Request a signed license file for the current device and save it locally.
        /// Requires active authentication session.
        /// </summary>
        public async Task<bool> FetchAndSaveLicenseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var deviceFingerprint = GetMachineId();
                var payload = JsonSerializer.Serialize(new
                {
                    deviceFingerprint
                }, ApiClient.JsonOptions);

                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                using var response = await ApiClient.Client.PostAsync(IssueEndpoint, content, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var issueResponse = await JsonSerializer.DeserializeAsync<IssueResponse>(stream, ApiClient.JsonOptions, cancellationToken).ConfigureAwait(false);

                if (issueResponse?.signedLicense is null)
                {
                    return false;
                }

                // Verify license signature before saving
                if (!VerifySignedLicense(issueResponse.signedLicense))
                {
                    return false;
                }

                storage.SaveLicense(issueResponse.signedLicense);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validate the locally stored license file.
        /// Checks signature, expiry, device fingerprint, and revocation status.
        /// </summary>
        public async Task<LicenseValidationResult> ValidateLocalLicenseAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var signedLicense = storage.LoadLicense();
                if (signedLicense is null)
                {
                    return new LicenseValidationResult { IsValid = false, Reason = "No license file found" };
                }

                // Verify signature
                if (!VerifySignedLicense(signedLicense))
                {
                    return new LicenseValidationResult { IsValid = false, Reason = "Invalid license signature" };
                }

                // Parse payload
                var payload = ParseLicensePayload(signedLicense);
                if (payload is null)
                {
                    return new LicenseValidationResult { IsValid = false, Reason = "Failed to parse license payload" };
                }

                // Check expiry
                if (payload.IsExpired())
                {
                    return new LicenseValidationResult { IsValid = false, Reason = "License expired", Payload = payload };
                }

                // Check device fingerprint
                var currentFingerprint = GetMachineId();
                if (payload.DeviceFingerprint != currentFingerprint)
                {
                    return new LicenseValidationResult { IsValid = false, Reason = "License not issued for this device", Payload = payload };
                }

                // Check revocation status
                var isRevoked = await IsLicenseRevokedAsync(payload.LicenseId, cancellationToken).ConfigureAwait(false);
                if (isRevoked)
                {
                    return new LicenseValidationResult { IsValid = false, Reason = "License has been revoked", Payload = payload };
                }

                return new LicenseValidationResult
                {
                    IsValid = true,
                    Payload = payload,
                    IsInGracePeriod = payload.IsInGracePeriod()
                };
            }
            catch (Exception ex)
            {
                return new LicenseValidationResult { IsValid = false, Reason = $"Validation error: {ex.Message}" };
            }
        }

        private static string GetKeyOrFallback(string environmentVariable, string primaryFallback, string? secondaryFallback = null)
        {
            var candidate = Environment.GetEnvironmentVariable(environmentVariable);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate.Trim();
            }

            if (!string.IsNullOrWhiteSpace(secondaryFallback))
            {
                return secondaryFallback!;
            }

            return primaryFallback;
        }

        /// <summary>
        /// Decode base64url string to bytes.
        /// </summary>
        private static byte[] Base64UrlDecode(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException("Input cannot be null or whitespace", nameof(input));
            }

            var base64 = input.Replace('-', '+').Replace('_', '/');
            var padding = (4 - base64.Length % 4) % 4;
            if (padding > 0)
            {
                base64 += new string('=', padding);
            }

            return Convert.FromBase64String(base64);
        }

        private sealed record ProfileResponse(UserDto? user, LicenseDto[]? licenses);

        private sealed record UserDto(string id, string email);

        private sealed record LicenseDto(string id, string licenseKey, string status, string type, DateTime? expiresAt);

        private sealed record CRLResponse(string crl, int count);

        private sealed record IssueResponse(string signedLicense, string licenseKey, string type, DateTime? expiresAt);
    }

    /// <summary>
    /// Result of validating a local license file.
    /// </summary>
    public sealed class LicenseValidationResult
    {
        public bool IsValid { get; set; }
        public string? Reason { get; set; }
        public LicensePayload? Payload { get; set; }
        public bool IsInGracePeriod { get; set; }
    }
}

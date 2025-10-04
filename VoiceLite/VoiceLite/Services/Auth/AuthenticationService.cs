using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VoiceLite.Interfaces;
using VoiceLite.Models;

namespace VoiceLite.Services.Auth
{
    /// <summary>
    /// Concrete implementation that talks to the VoiceLite web API for magic-link authentication
    /// and session persistence.
    /// </summary>
    public sealed class AuthenticationService : IAuthenticationService
    {
        private UserSession? cachedSession;

        public async Task RequestMagicLinkAsync(string email, CancellationToken cancellationToken = default)
        {
            using var content = new StringContent(JsonSerializer.Serialize(new { email }), Encoding.UTF8, "application/json");
            using var response = await ApiClient.Client.PostAsync("/api/auth/request", content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var error = await SafeReadErrorAsync(response).ConfigureAwait(false);
                throw new InvalidOperationException(error ?? "Failed to send magic link");
            }
        }

        public async Task<UserSession?> GetCachedSessionAsync(CancellationToken cancellationToken = default)
        {
            if (cachedSession != null && cachedSession.ExpiresAtUtc > DateTime.UtcNow)
            {
                return cachedSession;
            }

            var profile = await FetchProfileAsync(cancellationToken).ConfigureAwait(false);
            cachedSession = profile;
            return cachedSession;
        }

        public async Task<UserSession> SignInAsync(string email, string otpCode, CancellationToken cancellationToken = default)
        {
            var payload = JsonSerializer.Serialize(new { email, otp = otpCode });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await ApiClient.Client.PostAsync("/api/auth/otp", content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var error = await SafeReadErrorAsync(response).ConfigureAwait(false);
                throw new InvalidOperationException(error ?? "Invalid code. Please try again.");
            }

            var session = await FetchProfileAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Unable to load profile after sign-in.");

            cachedSession = session;

            // Persist session cookies to disk
            ApiClient.SaveCookies();

            return session;
        }

        public async Task SignOutAsync(CancellationToken cancellationToken = default)
        {
            using var response = await ApiClient.Client.PostAsync("/api/auth/logout", content: null, cancellationToken).ConfigureAwait(false);

            cachedSession = null;
            ApiClient.ClearCookies();

            if (!response.IsSuccessStatusCode)
            {
                var error = await SafeReadErrorAsync(response).ConfigureAwait(false);
                throw new InvalidOperationException(error ?? "Failed to sign out.");
            }
        }

        public async Task<bool> RefreshSessionAsync(CancellationToken cancellationToken = default)
        {
            var profile = await FetchProfileAsync(cancellationToken).ConfigureAwait(false);
            cachedSession = profile;
            return profile != null;
        }

        private async Task<UserSession?> FetchProfileAsync(CancellationToken cancellationToken)
        {
            using var response = await ApiClient.Client.GetAsync("/api/me", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var dto = await JsonSerializer.DeserializeAsync<ProfileResponse>(stream, ApiClient.JsonOptions, cancellationToken).ConfigureAwait(false);
            if (dto?.user == null)
            {
                return null;
            }

            var session = new UserSession
            {
                UserId = dto.user.id,
                Email = dto.user.email,
                AccessToken = string.Empty,
                RefreshToken = null,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
            };

            return session;
        }

        private static async Task<string?> SafeReadErrorAsync(HttpResponseMessage response)
        {
            try
            {
                var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(text))
                {
                    return response.ReasonPhrase;
                }

                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("error", out var errorProperty))
                {
                    return errorProperty.GetString();
                }

                return text;
            }
            catch
            {
                return response.ReasonPhrase;
            }
        }

        private sealed record ProfileResponse(UserDto? user);

        private sealed record UserDto(string id, string email);
    }
}

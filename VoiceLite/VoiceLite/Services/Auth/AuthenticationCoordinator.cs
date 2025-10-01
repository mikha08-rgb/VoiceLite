using System.Threading;
using System.Threading.Tasks;
using VoiceLite.Interfaces;
using VoiceLite.Models;

namespace VoiceLite.Services.Auth
{
    /// <summary>
    /// Thin orchestration layer intended to bridge UI flows (login dialogs,
    /// settings screens) with the underlying <see cref="IAuthenticationService"/>.
    /// Developers implementing authentication can inject additional services here
    /// (e.g. telemetry, messaging) without bloating the UI code-behind.
    /// </summary>
    public sealed class AuthenticationCoordinator
    {
        private readonly IAuthenticationService authenticationService;

        public AuthenticationCoordinator(IAuthenticationService authenticationService)
        {
            this.authenticationService = authenticationService;
        }

        public Task RequestMagicLinkAsync(string email, CancellationToken cancellationToken = default)
        {
            return authenticationService.RequestMagicLinkAsync(email, cancellationToken);
        }

        public Task<UserSession?> TryRestoreSessionAsync(CancellationToken cancellationToken = default)
        {
            return authenticationService.GetCachedSessionAsync(cancellationToken);
        }

        public Task<UserSession> SignInAsync(string email, string otpCode, CancellationToken cancellationToken = default)
        {
            return authenticationService.SignInAsync(email, otpCode, cancellationToken);
        }

        public Task SignOutAsync(CancellationToken cancellationToken = default)
        {
            return authenticationService.SignOutAsync(cancellationToken);
        }

        public Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
        {
            return authenticationService.RefreshSessionAsync(cancellationToken);
        }
    }
}

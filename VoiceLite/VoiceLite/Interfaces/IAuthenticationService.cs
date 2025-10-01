using System.Threading;
using System.Threading.Tasks;
using VoiceLite.Models;

namespace VoiceLite.Interfaces
{
    /// <summary>
    /// Contract for authenticating VoiceLite desktop users against a remote identity service.
    /// </summary>
    public interface IAuthenticationService
    {
        Task RequestMagicLinkAsync(string email, CancellationToken cancellationToken = default);

        Task<UserSession?> GetCachedSessionAsync(CancellationToken cancellationToken = default);

        Task<UserSession> SignInAsync(string email, string otpCode, CancellationToken cancellationToken = default);

        Task SignOutAsync(CancellationToken cancellationToken = default);

        Task<bool> RefreshSessionAsync(CancellationToken cancellationToken = default);
    }
}

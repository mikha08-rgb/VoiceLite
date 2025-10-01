using System.Threading;
using System.Threading.Tasks;

namespace VoiceLite.Services.Licensing
{
    /// <summary>
    /// Contract for retrieving and validating VoiceLite license entitlements once user auth is in place.
    /// </summary>
    public interface ILicenseService
    {
        Task<LicenseStatus> GetCurrentStatusAsync(CancellationToken cancellationToken = default);

        Task SyncAsync(CancellationToken cancellationToken = default);
    }

    public enum LicenseStatus
    {
        Unknown,
        Unlicensed,
        Trial,
        Active,
        Expired
    }
}

using System;

namespace VoiceLite.Models
{
    /// <summary>
    /// Represents an authenticated VoiceLite user session. Concrete authentication
    /// implementation will populate these values from the backend identity service.
    /// </summary>
    public sealed class UserSession
    {
        public string UserId { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string AccessToken { get; set; } = string.Empty;

        public string? RefreshToken { get; set; }

        public DateTime ExpiresAtUtc { get; set; }

        public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    }
}

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VoiceLite.Services.Auth
{
    internal static class ApiClient
    {
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoiceLite"
        );

        private static readonly string CookiesFilePath = Path.Combine(AppDataPath, "cookies.dat");

        internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        internal static readonly CookieContainer CookieJar = LoadCookies();

        internal static readonly HttpClientHandler Handler = new()
        {
            CookieContainer = CookieJar,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = true,
        };

        internal static readonly HttpClient Client = new(Handler)
        {
#if DEBUG
            // In DEBUG builds, allow environment variable override for local testing
            BaseAddress = new Uri(Environment.GetEnvironmentVariable("VOICELITE_API_BASE_URL")
                                   ?? "https://app.voicelite.com"),
#else
            // In RELEASE builds, hardcode production URL to prevent hijacking
            BaseAddress = new Uri("https://app.voicelite.com"),
#endif
            Timeout = TimeSpan.FromSeconds(30),
        };

        /// <summary>
        /// Load persisted cookies from disk using DPAPI encryption.
        /// </summary>
        private static CookieContainer LoadCookies()
        {
            var container = new CookieContainer();

            try
            {
                if (!File.Exists(CookiesFilePath))
                {
                    return container;
                }

                var encryptedBytes = File.ReadAllBytes(CookiesFilePath);
                var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);

                var json = Encoding.UTF8.GetString(decryptedBytes);
                var cookies = JsonSerializer.Deserialize<CookieDto[]>(json, JsonOptions);

                if (cookies is null)
                {
                    return container;
                }

                foreach (var cookieDto in cookies)
                {
                    var cookie = new Cookie(
                        cookieDto.Name,
                        cookieDto.Value,
                        cookieDto.Path,
                        cookieDto.Domain
                    );

                    if (!string.IsNullOrEmpty(cookieDto.Expires))
                    {
                        cookie.Expires = DateTime.Parse(cookieDto.Expires);
                    }

                    cookie.HttpOnly = cookieDto.HttpOnly;
                    cookie.Secure = cookieDto.Secure;

                    container.Add(cookie);
                }
            }
            catch
            {
                // Ignore errors, return empty container
            }

            return container;
        }

        /// <summary>
        /// Save current cookies to disk with DPAPI encryption.
        /// Should be called after successful authentication.
        /// </summary>
        public static void SaveCookies()
        {
            try
            {
                Directory.CreateDirectory(AppDataPath);

                var baseUri = Client.BaseAddress ?? new Uri("https://app.voicelite.com");
                var cookies = CookieJar.GetCookies(baseUri).Cast<Cookie>()
                    .Select(c => new CookieDto
                    {
                        Name = c.Name,
                        Value = c.Value,
                        Domain = c.Domain,
                        Path = c.Path,
                        Expires = c.Expires == DateTime.MinValue ? null : c.Expires.ToString("O"),
                        HttpOnly = c.HttpOnly,
                        Secure = c.Secure,
                    })
                    .ToArray();

                var json = JsonSerializer.Serialize(cookies, JsonOptions);
                var plainBytes = Encoding.UTF8.GetBytes(json);
                var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

                File.WriteAllBytes(CookiesFilePath, encryptedBytes);
            }
            catch
            {
                // Ignore errors during cookie save
            }
        }

        /// <summary>
        /// Clear persisted cookies from disk.
        /// Should be called during logout.
        /// </summary>
        public static void ClearCookies()
        {
            try
            {
                if (File.Exists(CookiesFilePath))
                {
                    File.Delete(CookiesFilePath);
                }

                // Clear in-memory cookies
                var baseUri = Client.BaseAddress ?? new Uri("https://app.voicelite.com");
                var cookies = CookieJar.GetCookies(baseUri).Cast<Cookie>().ToArray();
                foreach (var cookie in cookies)
                {
                    cookie.Expired = true;
                }
            }
            catch
            {
                // Ignore errors
            }
        }

        private sealed record CookieDto
        {
            public string Name { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
            public string Domain { get; set; } = string.Empty;
            public string Path { get; set; } = string.Empty;
            public string? Expires { get; set; }
            public bool HttpOnly { get; set; }
            public bool Secure { get; set; }
        }
    }
}

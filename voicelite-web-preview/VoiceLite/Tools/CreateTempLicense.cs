using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// Standalone utility to create a temporary Pro license for testing
/// Run with: dotnet run CreateTempLicense.cs
/// </summary>
class CreateTempLicense
{
    public class StoredLicense
    {
        public string LicenseKey { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime ValidatedAt { get; set; }
        public string Type { get; set; } = "LIFETIME";
    }

    static void Main()
    {
        try
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoiceLite"
            );

            var licensePath = Path.Combine(appDataPath, "license.dat");

            // Create temp license
            var licenseKey = "VL-TEMP-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var license = new StoredLicense
            {
                LicenseKey = licenseKey,
                Email = "temp@voicelite.local",
                ValidatedAt = DateTime.UtcNow,
                Type = "LIFETIME"
            };

            // Ensure directory exists
            Directory.CreateDirectory(appDataPath);

            // Serialize to JSON
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(license, options);

            // Encrypt using Windows DPAPI
            var plaintextBytes = Encoding.UTF8.GetBytes(json);
            var encryptedData = ProtectedData.Protect(
                plaintextBytes,
                null,
                DataProtectionScope.CurrentUser
            );

            // Save encrypted license
            File.WriteAllBytes(licensePath, encryptedData);

            Console.WriteLine("✅ Temporary Pro License Created!\n");
            Console.WriteLine("License Key: " + licenseKey);
            Console.WriteLine("Email: " + license.Email);
            Console.WriteLine("Type: LIFETIME (Pro)");
            Console.WriteLine("Location: " + licensePath);
            Console.WriteLine("\n🎯 VoiceLite is now running in PRO mode!");
            Console.WriteLine("\nRestart VoiceLite to see Pro features:");
            Console.WriteLine("- Models tab visible in Settings");
            Console.WriteLine("- Can download Base, Small, Medium, Large models");
            Console.WriteLine("- Can switch between all 5 models");
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Error: " + ex.Message);
            Environment.Exit(1);
        }
    }
}

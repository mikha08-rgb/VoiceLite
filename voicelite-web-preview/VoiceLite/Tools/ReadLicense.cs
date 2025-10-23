using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

class Program
{
    static void Main()
    {
        var licensePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VoiceLite",
            "license.dat"
        );

        if (!File.Exists(licensePath))
        {
            Console.WriteLine("License file not found!");
            return;
        }

        try
        {
            var encryptedData = File.ReadAllBytes(licensePath);
            var decryptedBytes = ProtectedData.Unprotect(
                encryptedData,
                null,
                DataProtectionScope.CurrentUser
            );
            var json = Encoding.UTF8.GetString(decryptedBytes);

            Console.WriteLine("License file contents:");
            Console.WriteLine(json);

            // Pretty print
            using var doc = JsonDocument.Parse(json);
            var prettyJson = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine("\nPretty printed:");
            Console.WriteLine(prettyJson);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}

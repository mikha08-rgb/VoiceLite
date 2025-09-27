using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    public class ModelEncryptionService
    {
        private readonly LicenseManager licenseManager;
        private readonly string modelDirectory;
        private readonly string encryptedModelDirectory;
        private readonly byte[] salt = Encoding.UTF8.GetBytes("VL@Model#2024");

        public ModelEncryptionService(LicenseManager licenseManager)
        {
            this.licenseManager = licenseManager;
            modelDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper");
            encryptedModelDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper", "encrypted");

            Directory.CreateDirectory(encryptedModelDirectory);
        }

        // Encrypt all models on first run or update
        public void EncryptModelsIfNeeded()
        {
            try
            {
                var modelFiles = Directory.GetFiles(modelDirectory, "*.bin");

                foreach (var modelFile in modelFiles)
                {
                    var fileName = Path.GetFileName(modelFile);
                    var encryptedPath = Path.Combine(encryptedModelDirectory, fileName + ".enc");

                    // Skip if already encrypted
                    if (File.Exists(encryptedPath))
                        continue;

                    EncryptFile(modelFile, encryptedPath);

                    // DO NOT delete original - we need it for the app to work!
                    // The encryption is just for obfuscation/protection
                    // The actual models are still needed by whisper.exe
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to encrypt models", ex);
            }
        }

        // Decrypt model to temp location when needed
        public string? GetDecryptedModelPath(string modelName)
        {
            try
            {
                // Check license allows this model
                if (!licenseManager.CheckModelAccess(modelName))
                {
                    ErrorLogger.LogMessage($"License does not allow access to model: {modelName}");
                    return null;
                }

                var encryptedPath = Path.Combine(encryptedModelDirectory, modelName + ".enc");

                // If encrypted version doesn't exist, check for original
                if (!File.Exists(encryptedPath))
                {
                    var originalPath = Path.Combine(modelDirectory, modelName);
                    if (File.Exists(originalPath))
                    {
                        // Encrypt it first
                        EncryptFile(originalPath, encryptedPath);
                    }
                    else
                    {
                        ErrorLogger.LogMessage($"Model not found: {modelName}");
                        return null;
                    }
                }

                // Create temp directory for decrypted models
                var tempDir = Path.Combine(Path.GetTempPath(), "VoiceLite", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                var tempModelPath = Path.Combine(tempDir, modelName);

                // Decrypt to temp location
                if (DecryptFile(encryptedPath, tempModelPath))
                {
                    // Mark for deletion on process exit
                    ScheduleFileForDeletion(tempModelPath);
                    ScheduleFileForDeletion(tempDir);

                    return tempModelPath;
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError($"Failed to decrypt model {modelName}", ex);
            }

            return null;
        }

        private void EncryptFile(string inputFile, string outputFile)
        {
            try
            {
                using (var aes = Aes.Create())
                {
                    // Generate key from license and machine info
                    var key = GenerateKey();
                    aes.Key = key;
                    aes.GenerateIV();

                    using (var inputStream = File.OpenRead(inputFile))
                    using (var outputStream = File.Create(outputFile))
                    {
                        // Write IV to beginning of file
                        outputStream.Write(aes.IV, 0, aes.IV.Length);

                        using (var encryptor = aes.CreateEncryptor())
                        using (var cryptoStream = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write))
                        {
                            // Encrypt in chunks for large files
                            var buffer = new byte[1024 * 1024]; // 1MB chunks
                            int bytesRead;

                            while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                cryptoStream.Write(buffer, 0, bytesRead);
                            }
                        }
                    }
                }

                ErrorLogger.LogMessage($"Successfully encrypted {Path.GetFileName(inputFile)}");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError($"Failed to encrypt {inputFile}", ex);
                throw;
            }
        }

        private bool DecryptFile(string inputFile, string outputFile)
        {
            try
            {
                using (var aes = Aes.Create())
                {
                    var key = GenerateKey();
                    aes.Key = key;

                    using (var inputStream = File.OpenRead(inputFile))
                    {
                        // Read IV from beginning of file
                        var iv = new byte[16];
                        inputStream.Read(iv, 0, 16);
                        aes.IV = iv;

                        using (var outputStream = File.Create(outputFile))
                        using (var decryptor = aes.CreateDecryptor())
                        using (var cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read))
                        {
                            var buffer = new byte[1024 * 1024]; // 1MB chunks
                            int bytesRead;

                            while ((bytesRead = cryptoStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                outputStream.Write(buffer, 0, bytesRead);
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError($"Failed to decrypt {inputFile}", ex);
                return false;
            }
        }

        private byte[] GenerateKey()
        {
            // Generate key based on machine ID and a secret
            var machineId = GetMachineId();
            var secret = "VoiceLite@2024#Secure";
            var combined = $"{machineId}|{secret}";

            using (var pbkdf2 = new Rfc2898DeriveBytes(combined, salt, 10000))
            {
                return pbkdf2.GetBytes(32); // 256-bit key
            }
        }

        private string GetMachineId()
        {
            try
            {
                // Use same machine ID as license manager for consistency
                var license = licenseManager.GetCurrentLicense();
                return license?.MachineId ?? "DEFAULT";
            }
            catch
            {
                return "DEFAULT";
            }
        }

        private void ScheduleFileForDeletion(string path)
        {
            try
            {
                // Schedule deletion on process exit
                AppDomain.CurrentDomain.ProcessExit += (s, e) =>
                {
                    try
                    {
                        if (File.Exists(path))
                            File.Delete(path);
                        else if (Directory.Exists(path))
                            Directory.Delete(path, true);
                    }
                    catch { }
                };

                // Also try to mark as temporary
                if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Temporary);
                }
            }
            catch { }
        }

        // Clean up any leftover temp files
        public static void CleanupTempFiles()
        {
            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "VoiceLite");
                if (Directory.Exists(tempDir))
                {
                    // Delete directories older than 1 day
                    var dirs = Directory.GetDirectories(tempDir);
                    foreach (var dir in dirs)
                    {
                        var info = new DirectoryInfo(dir);
                        if (info.CreationTime < DateTime.Now.AddDays(-1))
                        {
                            try
                            {
                                Directory.Delete(dir, true);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        }
    }
}
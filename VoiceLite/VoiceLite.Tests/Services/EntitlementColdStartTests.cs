using System;
using System.IO;
using System.Text.Json;
using AwesomeAssertions;
using VoiceLite.Models;
using VoiceLite.Services;
using Xunit;

namespace VoiceLite.Tests.Services
{
    /// <summary>
    /// Regression suite for the v2.4.0 cold-start licensing bug: MainWindow ran its
    /// entitlement check (ValidateTranscriptionModel) from LoadSettings() in the ctor,
    /// but LicenseService was only constructed later in InitializeServicesAsync. The
    /// check therefore always saw a null service, reset Pro users to Free on every
    /// cold start, and wiped the legacy plaintext key before LicenseService's DPAPI
    /// migration could consume it.
    ///
    /// These tests replay the FIXED startup order against an isolated state directory
    /// (never the real %LOCALAPPDATA%\VoiceLite) via LicenseService's internal ctor:
    ///   1. new LicenseService(dir)  — legacy plaintext migration + DPAPI load
    ///   2. load settings.json the way MainWindow.LoadSettings does
    ///   3. MainWindow.ApplyStartupEntitlementCheck — the exact production check
    /// </summary>
    public class EntitlementColdStartTests : IDisposable
    {
        private readonly string _stateDir;

        public EntitlementColdStartTests()
        {
            _stateDir = Path.Combine(
                Path.GetTempPath(), "VoiceLiteTests", "entitlement-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_stateDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_stateDir, recursive: true); } catch { /* best effort */ }
        }

        private string SettingsPath => Path.Combine(_stateDir, "settings.json");
        private string LicenseDatPath => Path.Combine(_stateDir, "license.dat");

        private void WriteSettingsFile(Settings settings)
        {
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }

        /// <summary>
        /// Seeds a DPAPI license.dat in the isolated directory the same way the app does
        /// (LicenseService.SaveLicenseKey), then discards the service instance.
        /// </summary>
        private void SeedDpapiLicense(string key, string? email = null)
        {
            using var seeder = new LicenseService(_stateDir);
            seeder.SaveLicenseKey(key, email);
        }

        /// <summary>
        /// Replays MainWindow's fixed cold-start order. Returns the constructed service,
        /// the settings as MainWindow would hold them after LoadSettings, and whether the
        /// entitlement check requested a settings save.
        /// </summary>
        private (LicenseService Service, Settings Settings, bool NeedsSave) ColdStart()
        {
            // Step 1: LicenseService first — runs legacy plaintext migration + DPAPI load.
            var service = new LicenseService(_stateDir);

            // Step 2: load settings the way MainWindow.LoadSettings does (deserialize +
            // ValidateAndRepair, defaults on corruption).
            Settings settings;
            try
            {
                settings = File.Exists(SettingsPath)
                    ? SettingsValidator.ValidateAndRepair(
                        JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath)))
                    : new Settings();
            }
            catch (Exception)
            {
                settings = new Settings();
            }

            // Step 3: the exact production entitlement check.
            bool needsSave = MainWindow.ApplyStartupEntitlementCheck(settings, service);
            if (needsSave)
            {
                WriteSettingsFile(settings); // mirror SaveSettingsInternalAsync persisting the result
            }

            return (service, settings, needsSave);
        }

        // (a) An existing DPAPI Pro license must survive a cold start untouched.
        [Fact]
        public void DpapiProLicense_SurvivesColdStart()
        {
            SeedDpapiLicense("PRO-KEY-12345", "pro@example.com");
            WriteSettingsFile(new Settings { IsProLicense = true });

            var (service, settings, needsSave) = ColdStart();
            using (service)
            {
                settings.IsProLicense.Should().BeTrue("a DPAPI-backed Pro license must never be reset on cold start");
                needsSave.Should().BeFalse("nothing changed, so nothing should be written to disk");
                service.GetStoredLicenseKey().Should().Be("PRO-KEY-12345");
                service.GetLicenseEmail().Should().Be("pro@example.com");
                File.Exists(LicenseDatPath).Should().BeTrue("license.dat must not be deleted");
            }
        }

        // (b) A legacy plaintext key in settings.json must be migrated to DPAPI and Pro retained.
        [Fact]
        public void LegacyPlaintextKey_IsMigratedToDpapi_AndProRetained()
        {
            WriteSettingsFile(new Settings { LicenseKey = "LEGACY-PLAINTEXT-KEY", IsProLicense = true });
            File.Exists(LicenseDatPath).Should().BeFalse("precondition: no DPAPI file yet");

            var (service, settings, _) = ColdStart();
            using (service)
            {
                File.Exists(LicenseDatPath).Should().BeTrue("migration must create the DPAPI license file");
                service.GetStoredLicenseKey().Should().Be("LEGACY-PLAINTEXT-KEY", "the migrated key is authoritative");
                settings.IsProLicense.Should().BeTrue("Pro must be retained because the migrated DPAPI key backs it");

                // The plaintext key must be gone from settings.json (migration rewrote it).
                var onDisk = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath));
                onDisk!.LicenseKey.Should().BeNullOrEmpty("plaintext key must not survive on disk after migration");
            }
        }

        // (b-regression) The pre-fix ordering bug: the entitlement check must never clear
        // or request a save when the service has not been constructed yet.
        [Fact]
        public void EntitlementCheck_WithNullService_NeverClearsOrSaves()
        {
            var settings = new Settings { IsProLicense = true, LicenseKey = "LEGACY-PLAINTEXT-KEY" };

            bool needsSave = MainWindow.ApplyStartupEntitlementCheck(settings, null);

            needsSave.Should().BeFalse("no save may be triggered off a not-yet-constructed service");
            settings.IsProLicense.Should().BeTrue("entitlement must never be cleared off a null service");
            settings.LicenseKey.Should().Be("LEGACY-PLAINTEXT-KEY",
                "the plaintext key must be preserved for the DPAPI migration to consume");
        }

        // (c) Expiry rules: the app enforces expiry only through online re-validation (the
        // 14-day cache lives in memory; license.dat carries no expiry). A stored license —
        // even one the server would reject as expired — must therefore still be present and
        // NOT validated after a cold start; entitlement stays until validation says otherwise.
        [Fact]
        public void StoredLicense_ColdStart_DefersExpiryToOnlineValidation()
        {
            SeedDpapiLicense("EXPIRED-ON-SERVER-KEY");
            WriteSettingsFile(new Settings { IsProLicense = true });

            var (service, settings, needsSave) = ColdStart();
            using (service)
            {
                service.GetStoredLicenseKey().Should().Be("EXPIRED-ON-SERVER-KEY",
                    "cold start must not delete a stored key; only online validation/removal may");
                service.IsLicenseValid.Should().BeFalse(
                    "cold start alone never marks a license validated - that requires ValidateLicenseAsync");
                settings.IsProLicense.Should().BeTrue("local entitlement persists until validation revokes it");
                needsSave.Should().BeFalse();
            }
        }

        // (d) A corrupted settings.json must not destroy DPAPI license state.
        [Fact]
        public void CorruptedSettingsJson_DoesNotDestroyLicenseState()
        {
            SeedDpapiLicense("PRO-KEY-12345", "pro@example.com");
            File.WriteAllText(SettingsPath, "{ this is not valid json !!!");

            var (service, settings, needsSave) = ColdStart();
            using (service)
            {
                File.Exists(LicenseDatPath).Should().BeTrue("license.dat must survive settings corruption");
                service.GetStoredLicenseKey().Should().Be("PRO-KEY-12345", "the DPAPI key must still load");
                needsSave.Should().BeFalse("defaults hold no entitlement, so nothing needs clearing or saving");
                settings.IsProLicense.Should().BeFalse(
                    "defaults are Free until the user's next validation restores the flag - but the key survives");
            }
        }

        // (d) Corrupted settings.json with no license.dat: the ctor migration path must not throw.
        [Fact]
        public void CorruptedSettingsJson_WithoutLicenseFile_DoesNotThrow()
        {
            File.WriteAllText(SettingsPath, "\0\0garbage\0");

            Action act = () =>
            {
                using var service = new LicenseService(_stateDir);
                service.GetStoredLicenseKey().Should().BeEmpty();
            };

            act.Should().NotThrow("migration is best-effort and must never crash startup");
        }

        // Tamper detection must still work after the fix: Pro flag with NO backing DPAPI
        // license (and no plaintext key to migrate) is reset to Free and saved.
        [Fact]
        public void ProFlagWithoutAnyLicense_IsStillResetToFree()
        {
            WriteSettingsFile(new Settings { IsProLicense = true });

            var (service, settings, needsSave) = ColdStart();
            using (service)
            {
                settings.IsProLicense.Should().BeFalse("manual settings.json edits must not grant Pro");
                needsSave.Should().BeTrue("the reset must be persisted");
            }
        }
    }
}

using System;
using VoiceLite.Services;

namespace VoiceLite.Tests.Helpers
{
    /// <summary>
    /// Test helper to enable mock license validation for unit tests.
    /// This class provides a clean way to bypass license checks in tests that need Pro features.
    ///
    /// Usage:
    ///   [SetUp]
    ///   public void Setup()
    ///   {
    ///       LicenseTestHelper.EnableProLicense();
    ///   }
    ///
    ///   [TearDown]
    ///   public void TearDown()
    ///   {
    ///       LicenseTestHelper.DisableTestMode();
    ///   }
    /// </summary>
    public static class LicenseTestHelper
    {
        /// <summary>
        /// Enable test mode with a valid Pro license.
        /// This allows tests to use Pro models (Base, Small, Medium, Large) without actual licensing.
        /// </summary>
        public static void EnableProLicense()
        {
#if DEBUG
            SimpleLicenseStorage._testMode = true;
            SimpleLicenseStorage._mockHasValidLicense = true;
            SimpleLicenseStorage._mockLicense = new SimpleLicenseStorage.StoredLicense
            {
                LicenseKey = "TEST-LICENSE-KEY-12345678",
                Email = "test@voicelite.app",
                ValidatedAt = DateTime.UtcNow,
                Type = "LIFETIME"
            };
#endif
        }

        /// <summary>
        /// Enable test mode with no license (Free tier).
        /// This allows tests to verify Free tier behavior.
        /// </summary>
        public static void EnableFreeTier()
        {
#if DEBUG
            SimpleLicenseStorage._testMode = true;
            SimpleLicenseStorage._mockHasValidLicense = false;
            SimpleLicenseStorage._mockLicense = null;
#endif
        }

        /// <summary>
        /// Disable test mode and restore normal license validation.
        /// Call this in [TearDown] to ensure tests don't affect each other.
        /// </summary>
        public static void DisableTestMode()
        {
#if DEBUG
            SimpleLicenseStorage._testMode = false;
            SimpleLicenseStorage._mockHasValidLicense = false;
            SimpleLicenseStorage._mockLicense = null;
#endif
        }

        /// <summary>
        /// Check if test mode is currently enabled.
        /// </summary>
        public static bool IsTestModeEnabled()
        {
#if DEBUG
            return SimpleLicenseStorage._testMode;
#else
            return false;
#endif
        }
    }
}

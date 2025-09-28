using System;
using System.IO;
using System.Text.Json;
using System.Timers;

namespace VoiceLite.Services
{
    public class UsageTracker
    {
        private readonly string usagePath;
        private UsageData currentUsage;
        private readonly Timer resetTimer;
        private readonly SimpleLicenseManager licenseManager;

        public class UsageData
        {
            public DateTime WeekStartDate { get; set; }
            public double MinutesUsed { get; set; }
            public int TranscriptionCount { get; set; }
            public DateTime LastUsed { get; set; }
        }

        public UsageTracker(SimpleLicenseManager licenseManager)
        {
            this.licenseManager = licenseManager;
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VoiceLite");
            Directory.CreateDirectory(appData);
            usagePath = Path.Combine(appData, "usage.json");

            LoadUsage();

            // Set up weekly reset timer
            resetTimer = new Timer();
            SetupWeeklyReset();
        }

        private void LoadUsage()
        {
            try
            {
                if (File.Exists(usagePath))
                {
                    var json = File.ReadAllText(usagePath);
                    currentUsage = JsonSerializer.Deserialize<UsageData>(json) ?? new UsageData { WeekStartDate = GetWeekStartDate(DateTime.Today) };

                    // Reset if it's a new week
                    if (currentUsage.WeekStartDate < GetWeekStartDate(DateTime.Today))
                    {
                        ResetWeeklyUsage();
                    }
                }
                else
                {
                    currentUsage = new UsageData { WeekStartDate = GetWeekStartDate(DateTime.Today) };
                    SaveUsage();
                }
            }
            catch
            {
                currentUsage = new UsageData { WeekStartDate = GetWeekStartDate(DateTime.Today) };
            }
        }

        private void SaveUsage()
        {
            try
            {
                var json = JsonSerializer.Serialize(currentUsage, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(usagePath, json);
            }
            catch
            {
                // Ignore save failures
            }
        }

        private void SetupWeeklyReset()
        {
            // Calculate time until next Monday
            var now = DateTime.Now;
            var daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0) daysUntilMonday = 7; // If today is Monday, reset next Monday
            var nextMonday = now.Date.AddDays(daysUntilMonday);
            var timeUntilMonday = nextMonday - now;

            // Set timer to reset on Monday
            resetTimer.Interval = timeUntilMonday.TotalMilliseconds;
            resetTimer.Elapsed += (s, e) =>
            {
                ResetWeeklyUsage();
                // Reset timer for next week (7 days)
                resetTimer.Interval = TimeSpan.FromDays(7).TotalMilliseconds;
            };
            resetTimer.Start();
        }

        private void ResetWeeklyUsage()
        {
            currentUsage = new UsageData
            {
                WeekStartDate = GetWeekStartDate(DateTime.Today),
                MinutesUsed = 0,
                TranscriptionCount = 0,
                LastUsed = DateTime.MinValue
            };
            SaveUsage();
        }

        private DateTime GetWeekStartDate(DateTime date)
        {
            // Get the Monday of the current week
            int daysFromMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            return date.AddDays(-daysFromMonday).Date;
        }

        public bool CanUseApp()
        {
            // If user has a Pro subscription, always allow
            if (licenseManager.GetLicenseType() == SimpleLicenseType.Pro)
            {
                return true;
            }

            // For free users, check weekly limit (10 minutes)
            return currentUsage.MinutesUsed < 10.0;
        }

        public double GetRemainingMinutes()
        {
            // Unlimited for Pro users
            if (licenseManager.GetLicenseType() == SimpleLicenseType.Pro)
            {
                return -1; // Indicates unlimited
            }

            // Calculate remaining for free users (10 min/week)
            var remaining = 10.0 - currentUsage.MinutesUsed;
            return Math.Max(0, remaining);
        }

        public void RecordUsage(double durationSeconds)
        {
            // Don't track for Pro users (they have unlimited)
            if (licenseManager.GetLicenseType() == SimpleLicenseType.Pro)
            {
                return;
            }

            var durationMinutes = durationSeconds / 60.0;
            currentUsage.MinutesUsed += durationMinutes;
            currentUsage.TranscriptionCount++;
            currentUsage.LastUsed = DateTime.Now;
            SaveUsage();
        }

        public string GetUsageStatus()
        {
            if (licenseManager.GetLicenseType() == SimpleLicenseType.Pro)
            {
                return "Pro Subscription - Unlimited Usage";
            }

            var remaining = GetRemainingMinutes();
            if (remaining <= 0)
            {
                return "Weekly limit reached (10 min) - Upgrade for unlimited";
            }

            if (remaining < 1)
            {
                var seconds = (int)(remaining * 60);
                return $"Free tier - {seconds} seconds remaining this week";
            }

            return $"Free tier - {remaining:F1} minutes remaining this week";
        }

        public bool IsNearLimit()
        {
            if (licenseManager.GetLicenseType() == SimpleLicenseType.Pro)
            {
                return false;
            }

            var remaining = GetRemainingMinutes();
            return remaining > 0 && remaining < 2; // Warning when less than 2 minutes left this week
        }

        public UsageData GetTodayUsage()
        {
            return currentUsage;
        }

        public void Dispose()
        {
            resetTimer?.Stop();
            resetTimer?.Dispose();
        }
    }
}
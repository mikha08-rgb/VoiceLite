using System;

namespace VoiceLite.Models
{
    public enum LicenseType
    {
        Trial,        // 14-day trial
        Personal,     // $29 - personal use
        Pro,          // $79 - all features
        Business      // $199 - multi-device
    }

    public enum LicenseStatus
    {
        Valid,
        Expired,
        Invalid,
        NoLicense,
        TrialExpired
    }

    public class LicenseInfo
    {
        public string? LicenseKey { get; set; }
        public LicenseType Type { get; set; }
        public LicenseStatus Status { get; set; }
        public DateTime? ActivationDate { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string? RegisteredTo { get; set; }
        public string? Email { get; set; }
        public int DeviceLimit { get; set; } = 1;
        public string? MachineId { get; set; }

        // Trial specific
        public DateTime? TrialStartDate { get; set; }
        public int TrialDaysRemaining => CalculateTrialDaysRemaining();

        // Feature flags based on license type
        public bool CanUseAllModels => Type >= LicenseType.Pro;
        public bool CanUseAdvancedFeatures => Type >= LicenseType.Pro;
        public bool HasPrioritySupport => Type >= LicenseType.Pro;
        public bool AllowsCommercialUse => Type == LicenseType.Business;
        public bool IsUnlimited => Type != LicenseType.Trial;

        // Model restrictions
        public string[] AllowedModels => GetAllowedModels();

        private string[] GetAllowedModels()
        {
            return Type switch
            {
                LicenseType.Trial => new[] { "ggml-tiny.bin" },
                LicenseType.Personal => new[] { "ggml-tiny.bin", "ggml-base.bin", "ggml-small.bin" },
                LicenseType.Pro => new[] { "ggml-tiny.bin", "ggml-base.bin", "ggml-small.bin", "ggml-medium.bin", "ggml-large-v3.bin" },
                LicenseType.Business => new[] { "ggml-tiny.bin", "ggml-base.bin", "ggml-small.bin", "ggml-medium.bin", "ggml-large-v3.bin" },
                _ => new[] { "ggml-tiny.bin" }
            };
        }

        private int CalculateTrialDaysRemaining()
        {
            if (Type != LicenseType.Trial || !TrialStartDate.HasValue)
                return 0;

            var elapsed = DateTime.Now - TrialStartDate.Value;
            var remaining = 14 - (int)elapsed.TotalDays;
            return Math.Max(0, remaining);
        }

        public bool IsValid()
        {
            if (Status != LicenseStatus.Valid)
                return false;

            if (Type == LicenseType.Trial)
                return TrialDaysRemaining > 0;

            if (ExpirationDate.HasValue)
                return DateTime.Now < ExpirationDate.Value;

            return true;
        }

        public string GetStatusMessage()
        {
            return Status switch
            {
                LicenseStatus.Valid when Type == LicenseType.Trial => $"Trial: {TrialDaysRemaining} days remaining",
                LicenseStatus.Valid => $"{Type} License - Active",
                LicenseStatus.Expired => "License has expired",
                LicenseStatus.TrialExpired => "Trial period has ended",
                LicenseStatus.Invalid => "Invalid license key",
                LicenseStatus.NoLicense => "No license found",
                _ => "Unknown license status"
            };
        }
    }
}
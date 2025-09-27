using System;
using System.Diagnostics;
using System.Windows;
using VoiceLite.Models;
using VoiceLite.Services;

namespace VoiceLite
{
    public partial class LicenseWindow : Window
    {
        private readonly LicenseManager licenseManager;
        private LicenseInfo currentLicense = new LicenseInfo();

        public LicenseWindow()
        {
            InitializeComponent();
            licenseManager = new LicenseManager();
            LoadLicenseInfo();
        }

        private void LoadLicenseInfo()
        {
            currentLicense = licenseManager.GetCurrentLicense();
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (currentLicense == null)
            {
                LicenseStatusText.Text = "No license found";
                LicenseStatusText.Foreground = System.Windows.Media.Brushes.Gray;
                return;
            }

            // Update status header
            LicenseStatusText.Text = currentLicense.GetStatusMessage();
            LicenseStatusText.Foreground = currentLicense.IsValid()
                ? System.Windows.Media.Brushes.LightGreen
                : System.Windows.Media.Brushes.Orange;

            // Update license details
            LicenseTypeText.Text = currentLicense.Type.ToString();
            StatusText.Text = currentLicense.Status.ToString();

            // Expiry information
            if (currentLicense.Type == LicenseType.Trial)
            {
                ExpiryText.Text = $"{currentLicense.TrialDaysRemaining} days remaining";
            }
            else if (currentLicense.ExpirationDate.HasValue)
            {
                ExpiryText.Text = currentLicense.ExpirationDate.Value.ToShortDateString();
            }
            else
            {
                ExpiryText.Text = "Lifetime";
            }

            // Features
            FeaturesText.Text = GetFeaturesList();

            // Update button states
            if (currentLicense.Type == LicenseType.Trial)
            {
                TrialButton.IsEnabled = false;
                TrialButton.Content = $"Trial Active ({currentLicense.TrialDaysRemaining} days left)";
            }
            else
            {
                TrialButton.Visibility = Visibility.Collapsed;
            }
        }

        private string GetFeaturesList()
        {
            if (currentLicense == null)
                return "None";

            var features = new System.Text.StringBuilder();

            if (currentLicense.Type == LicenseType.Trial)
            {
                features.AppendLine("• Tiny model only");
                features.AppendLine("• 10 minutes daily limit");
                features.AppendLine("• Basic features");
            }
            else if (currentLicense.Type == LicenseType.Personal)
            {
                features.AppendLine("• Small, Base, and Tiny models");
                features.AppendLine("• Unlimited usage");
                features.AppendLine("• Personal use only");
            }
            else if (currentLicense.Type == LicenseType.Pro)
            {
                features.AppendLine("• All models including Large");
                features.AppendLine("• Unlimited usage");
                features.AppendLine("• Priority support");
                features.AppendLine("• Advanced features");
            }
            else if (currentLicense.Type == LicenseType.Business)
            {
                features.AppendLine("• All Pro features");
                features.AppendLine("• 5 device licenses");
                features.AppendLine("• Commercial use allowed");
                features.AppendLine("• Phone support");
            }

            return features.ToString().TrimEnd();
        }

        private void ActivateButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;

            string licenseKey = LicenseKeyTextBox.Text.Trim();
            string email = EmailTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                ShowError("Please enter a license key.");
                return;
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                ShowError("Please enter your email address.");
                return;
            }

            if (!IsValidEmail(email))
            {
                ShowError("Please enter a valid email address.");
                return;
            }

            // Format license key (add dashes if missing)
            if (licenseKey.Length == 16 && !licenseKey.Contains("-"))
            {
                licenseKey = $"{licenseKey.Substring(0, 4)}-{licenseKey.Substring(4, 4)}-" +
                            $"{licenseKey.Substring(8, 4)}-{licenseKey.Substring(12, 4)}";
                LicenseKeyTextBox.Text = licenseKey;
            }

            if (licenseManager.ActivateLicense(licenseKey, email))
            {
                MessageBox.Show("License activated successfully!\n\nVoiceLite will now restart to apply the changes.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // Restart application
                Process.Start(Process.GetCurrentProcess().MainModule.FileName);
                Application.Current.Shutdown();
            }
            else
            {
                ShowError("Invalid license key. Please check your key and try again.");
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void PurchaseButton_Click(object sender, RoutedEventArgs e)
        {
            // Open purchase page (placeholder URL)
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://voicelite.com/purchase",
                UseShellExecute = true
            });
        }

        private void TrialButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
using System;
using System.Diagnostics;
using System.Windows;
using VoiceLite.Services;

namespace VoiceLite
{
    public partial class SimpleLicenseWindow : Window
    {
        private readonly SimpleLicenseManager licenseManager;

        public SimpleLicenseWindow()
        {
            InitializeComponent();
            licenseManager = new SimpleLicenseManager();
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            StatusText.Text = licenseManager.GetLicenseStatus();

            var daysRemaining = licenseManager.GetTrialDaysRemaining();
            if (daysRemaining >= 0)
            {
                DetailsText.Text = $"You are using the trial version. After {daysRemaining} days, you'll need to purchase a license.";
                if (daysRemaining <= 3)
                {
                    StatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);
                }
            }
            else
            {
                var licenseType = licenseManager.GetLicenseType();
                DetailsText.Text = $"Thank you for purchasing VoiceLite {licenseType}!";
                StatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                ActivateBtn.IsEnabled = false;
                LicenseKeyBox.IsEnabled = false;
            }
        }

        private void ActivateBtn_Click(object sender, RoutedEventArgs e)
        {
            var key = LicenseKeyBox.Text.Trim();

            if (string.IsNullOrEmpty(key))
            {
                ShowError("Please enter a license key.");
                return;
            }

            // Format the key if needed
            if (!key.Contains("-"))
            {
                // Try to format: XXXAAAAAAAAAAAAAAAA -> XXX-AAAA-AAAA-AAAA-AAAA
                if (key.Length == 19)
                {
                    key = $"{key.Substring(0, 3)}-{key.Substring(3, 4)}-{key.Substring(7, 4)}-{key.Substring(11, 4)}-{key.Substring(15, 4)}";
                }
            }

            if (licenseManager.ActivateLicense(key))
            {
                MessageBox.Show("License activated successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                UpdateStatus();
                ErrorText.Visibility = Visibility.Collapsed;
            }
            else
            {
                ShowError("Invalid license key. Please check and try again.");
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void BuyBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://voicelite.app",
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show("Please visit https://voicelite.app to purchase a license.",
                    "Purchase License", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!licenseManager.IsValid())
            {
                var result = MessageBox.Show(
                    "Your trial has expired. The application will close.\n\nWould you like to purchase a license?",
                    "Trial Expired",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    BuyBtn_Click(sender, e);
                }

                Application.Current.Shutdown();
            }
            else
            {
                Close();
            }
        }
    }
}
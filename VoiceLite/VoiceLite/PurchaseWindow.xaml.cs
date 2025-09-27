using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VoiceLite.Models;
using VoiceLite.Services;

namespace VoiceLite
{
    public partial class PurchaseWindow : Window
    {
        private readonly PaymentProcessor paymentProcessor;
        private readonly LicenseManager licenseManager;

        public PurchaseWindow()
        {
            InitializeComponent();
            paymentProcessor = new PaymentProcessor();
            licenseManager = new LicenseManager();
        }

        private async void PurchasePersonalBtn_Click(object sender, RoutedEventArgs e)
        {
            await ProcessPurchase(LicenseType.Personal);
        }

        private async void PurchaseProBtn_Click(object sender, RoutedEventArgs e)
        {
            await ProcessPurchase(LicenseType.Pro);
        }

        private async void PurchaseBusinessBtn_Click(object sender, RoutedEventArgs e)
        {
            await ProcessPurchase(LicenseType.Business);
        }

        private async Task ProcessPurchase(LicenseType licenseType)
        {
            try
            {
                // Disable all buttons during processing
                SetButtonsEnabled(false);

                // Get user email (in production, show input dialog)
                var emailDialog = new EmailInputDialog();
                if (emailDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(emailDialog.Email))
                {
                    SetButtonsEnabled(true);
                    return;
                }

                var email = emailDialog.Email;

                // Generate payment link
                var paymentLink = await paymentProcessor.GeneratePaymentLink(licenseType, email);
                if (paymentLink == null)
                {
                    MessageBox.Show(
                        "Unable to generate payment link. Please try again or contact support.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    SetButtonsEnabled(true);
                    return;
                }

                // Open payment link in browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = paymentLink.Url,
                    UseShellExecute = true
                });

                // Show waiting dialog
                var result = MessageBox.Show(
                    $"A browser window has opened for payment.\n\n" +
                    $"License Type: {licenseType}\n" +
                    $"Price: ${paymentLink.Price:F2}\n\n" +
                    $"After completing payment, click OK to activate your license.\n" +
                    $"Click Cancel if you want to activate later.",
                    "Complete Payment",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Information
                );

                if (result == MessageBoxResult.OK)
                {
                    // In production, this would poll the server or wait for webhook
                    await ActivateLicenseAfterPayment(email);
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Purchase failed", ex);
                MessageBox.Show(
                    "An error occurred during purchase. Please contact support.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                SetButtonsEnabled(true);
            }
        }

        private async Task ActivateLicenseAfterPayment(string email)
        {
            // Show activation dialog
            var activationDialog = new ActivationDialog();
            if (activationDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(activationDialog.PaymentId))
            {
                var result = await paymentProcessor.VerifyAndActivateLicense(
                    activationDialog.PaymentId,
                    email
                );

                if (result.Success)
                {
                    // Activate license locally
                    var activated = licenseManager.ActivateLicense(result.LicenseKey, email);

                    if (activated)
                    {
                        MessageBox.Show(
                            $"License activated successfully!\n\n" +
                            $"License Type: {result.LicenseType}\n" +
                            $"Email: {email}\n" +
                            $"Key: {result.LicenseKey}\n\n" +
                            $"Thank you for purchasing VoiceLite!",
                            "Success",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information
                        );

                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        MessageBox.Show(
                            "License key is valid but activation failed locally. Please contact support.",
                            "Activation Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                    }
                }
                else
                {
                    MessageBox.Show(
                        result.ErrorMessage ?? "License activation failed. Please contact support.",
                        "Activation Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
        }

        private async void RestoreLicenseBtn_Click(object sender, RoutedEventArgs e)
        {
            // Show license restoration dialog
            var restoreDialog = new RestoreLicenseDialog();
            if (restoreDialog.ShowDialog() == true)
            {
                var email = restoreDialog.Email;
                var licenseKey = restoreDialog.LicenseKey;

                if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(licenseKey))
                {
                    // Attempt to activate the license
                    if (licenseManager.ActivateLicense(licenseKey, email))
                    {
                        MessageBox.Show(
                            "License restored successfully!",
                            "Success",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information
                        );
                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        MessageBox.Show(
                            "Invalid license key or email. Please check your information and try again.",
                            "Restoration Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );
                    }
                }
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SetButtonsEnabled(bool enabled)
        {
            PurchasePersonalBtn.IsEnabled = enabled;
            PurchaseProBtn.IsEnabled = enabled;
            PurchaseBusinessBtn.IsEnabled = enabled;
            RestoreLicenseBtn.IsEnabled = enabled;
        }
    }

    // Helper dialogs (would be separate files in production)
    public class EmailInputDialog : Window
    {
        public string Email { get; private set; } = "";

        public EmailInputDialog()
        {
            Title = "Enter Email";
            Width = 400;
            Height = 200;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = "Please enter your email address:",
                Margin = new Thickness(20, 20, 20, 10),
                FontSize = 14
            };
            grid.Children.Add(label);

            var textBox = new TextBox
            {
                Margin = new Thickness(20, 10, 20, 10),
                FontSize = 14,
                Height = 30
            };
            Grid.SetRow(textBox, 1);
            grid.Children.Add(textBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(20)
            };

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                IsDefault = true
            };
            okButton.Click += (s, e) =>
            {
                Email = textBox.Text;
                DialogResult = true;
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                IsCancel = true
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            Content = grid;
        }
    }

    public class ActivationDialog : Window
    {
        public string PaymentId { get; private set; } = "";

        public ActivationDialog()
        {
            Title = "Enter Payment ID";
            Width = 450;
            Height = 200;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = "Enter your payment confirmation ID from the email:",
                Margin = new Thickness(20, 20, 20, 10),
                FontSize = 14
            };
            grid.Children.Add(label);

            var textBox = new TextBox
            {
                Margin = new Thickness(20, 10, 20, 10),
                FontSize = 14,
                Height = 30
            };
            Grid.SetRow(textBox, 1);
            grid.Children.Add(textBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(20)
            };

            var okButton = new Button
            {
                Content = "Activate",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                IsDefault = true
            };
            okButton.Click += (s, e) =>
            {
                PaymentId = textBox.Text;
                DialogResult = true;
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                IsCancel = true
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            Content = grid;
        }
    }

    public class RestoreLicenseDialog : Window
    {
        public string Email { get; private set; } = "";
        public string LicenseKey { get; private set; } = "";

        public RestoreLicenseDialog()
        {
            Title = "Restore License";
            Width = 450;
            Height = 250;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var emailLabel = new TextBlock
            {
                Text = "Email:",
                Margin = new Thickness(20, 20, 20, 5),
                FontSize = 14
            };
            grid.Children.Add(emailLabel);

            var emailBox = new TextBox
            {
                Margin = new Thickness(20, 5, 20, 10),
                FontSize = 14,
                Height = 30
            };
            Grid.SetRow(emailBox, 1);
            grid.Children.Add(emailBox);

            var keyLabel = new TextBlock
            {
                Text = "License Key:",
                Margin = new Thickness(20, 10, 20, 5),
                FontSize = 14
            };
            Grid.SetRow(keyLabel, 2);
            grid.Children.Add(keyLabel);

            var keyBox = new TextBox
            {
                Margin = new Thickness(20, 5, 20, 10),
                FontSize = 14,
                Height = 30,
                CharacterCasing = CharacterCasing.Upper
            };
            Grid.SetRow(keyBox, 3);
            grid.Children.Add(keyBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(20)
            };

            var okButton = new Button
            {
                Content = "Restore",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                IsDefault = true
            };
            okButton.Click += (s, e) =>
            {
                Email = emailBox.Text;
                LicenseKey = keyBox.Text;
                DialogResult = true;
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                IsCancel = true
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 4);
            grid.Children.Add(buttonPanel);

            Content = grid;
        }
    }
}
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using VoiceLite.Models;
using VoiceLite.Services.Auth;

namespace VoiceLite
{
    /// <summary>
    /// Minimal login window scaffold. Actual authentication flow will be wired up
    /// once the backend and <see cref="Services.Auth.AuthenticationCoordinator"/> are complete.
    /// </summary>
    public partial class LoginWindow : Window
    {
        private readonly AuthenticationCoordinator authenticationCoordinator;

        public string Email => EmailTextBox.Text.Trim();

        public string OtpCode => OtpTextBox.Text.Trim();

        public UserSession? Session { get; private set; }

        public LoginWindow(AuthenticationCoordinator authenticationCoordinator)
        {
            InitializeComponent();
            this.authenticationCoordinator = authenticationCoordinator;
        }

        private async void SendLinkButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(Email))
                {
                    UpdateStatus("Enter your email address.", isError: true);
                    return;
                }

                UpdateStatus("Sending magic link...", isError: false);
                await authenticationCoordinator.RequestMagicLinkAsync(Email);
                UpdateStatus("Magic link sent! Check your email for the link or enter the OTP below.", isError: false);
            });
        }

        private async void VerifyButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(Email))
                {
                    UpdateStatus("Enter your email address.", isError: true);
                    return;
                }

                if (OtpCode.Length != 6)
                {
                    UpdateStatus("Enter the 6-digit code from your email.", isError: true);
                    return;
                }

                UpdateStatus("Verifying code...", isError: false);
                Session = await authenticationCoordinator.SignInAsync(Email, OtpCode);
                UpdateStatus("Signed in successfully.", isError: false);
                DialogResult = true;
            });
        }

        private async Task ExecuteAsync(Func<Task> operation)
        {
            try
            {
                ToggleInputs(false);
                await operation();
            }
            catch (Exception ex)
            {
                UpdateStatus(ex.Message, isError: true);
            }
            finally
            {
                ToggleInputs(true);
            }
        }

        private void ToggleInputs(bool isEnabled)
        {
            EmailTextBox.IsEnabled = isEnabled && Session == null;
            OtpTextBox.IsEnabled = isEnabled;
            SendLinkButton.IsEnabled = isEnabled;
            VerifyButton.IsEnabled = isEnabled;
        }

        private void UpdateStatus(string message, bool isError)
        {
            StatusLabel.Text = message;
            StatusLabel.Foreground = isError ? System.Windows.Media.Brushes.IndianRed : System.Windows.Media.Brushes.SteelBlue;
        }
    }
}

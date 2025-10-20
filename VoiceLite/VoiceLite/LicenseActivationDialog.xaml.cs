using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;
using VoiceLite.Services;

namespace VoiceLite
{
    // AUDIT FIX (LEAK-CRIT-2): Implement IDisposable to allow using statement and ensure handle cleanup
    public partial class LicenseActivationDialog : Window, IDisposable
    {
        private const string API_BASE_URL = "https://voicelite.app/api";
        private bool _disposed = false;

        /// <summary>
        /// Callback to refresh license status in MainWindow after successful activation.
        /// This allows Pro features to be unlocked immediately without requiring app restart.
        /// </summary>
        public Action? OnLicenseActivated { get; set; }

        public LicenseActivationDialog()
        {
            InitializeComponent();

            // Focus on license key input
            Loaded += (s, e) => LicenseKeyTextBox.Focus();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Close the window if it's still open
                    if (IsLoaded)
                    {
                        Close();
                    }
                }
                _disposed = true;
            }
        }

        private async void Activate_Click(object sender, RoutedEventArgs e)
        {
            // AUDIT FIX (ERROR-CRIT-1): Wrap entire method in try-catch to prevent app crashes
            // Previously: Lines before try block could throw unhandled exceptions
            try
            {
                var licenseKey = LicenseKeyTextBox.Text.Trim();

                if (string.IsNullOrEmpty(licenseKey))
                {
                    ShowError("Please enter a license key");
                    return;
                }

                // Disable button during activation
                ActivateButton.IsEnabled = false;
                ActivateButton.Content = "Activating...";

                try
                {
                // Get hardware fingerprint
                var machineId = HardwareFingerprint.Generate();
                var machineLabel = Environment.MachineName;

                ShowInfo("Activating license...");

                // Call activation API
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                var payload = new
                {
                    licenseKey,
                    machineId,
                    machineLabel
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(
                    $"{API_BASE_URL}/licenses/activate",
                    content
                );

                // AUDIT FIX: Check for null response content before reading
                if (response.Content == null)
                {
                    ShowError("Empty response from server. Please try again.");
                    return;
                }

                var responseJson = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Parse response
                    using var doc = JsonDocument.Parse(responseJson);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("success", out var successProp) && successProp.GetBoolean())
                    {
                        // SECURITY FIX: Use TryGetProperty to prevent crash on malformed JSON
                        string email = "";
                        if (root.TryGetProperty("license", out var licenseProp) &&
                            licenseProp.TryGetProperty("email", out var emailProp))
                        {
                            email = emailProp.GetString() ?? "";
                        }

                        // Save license locally
                        SimpleLicenseStorage.SaveLicense(licenseKey, email, "LIFETIME");

                        // Reload license status in MainWindow immediately (no restart needed)
                        OnLicenseActivated?.Invoke();

                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        ShowError("Activation failed. Please check your license key.");
                    }
                }
                else
                {
                    // Parse error response
                    try
                    {
                        using var doc = JsonDocument.Parse(responseJson);
                        var errorMsg = doc.RootElement.GetProperty("error").GetString();
                        ShowError(errorMsg ?? "Activation failed");
                    }
                    catch
                    {
                        ShowError($"Activation failed with status {response.StatusCode}");
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                ShowError($"Network error: {ex.Message}\n\nPlease check your internet connection.");
            }
            catch (TaskCanceledException)
            {
                ShowError("Activation timed out. Please check your internet connection and try again.");
            }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("License activation failed", ex);
                    ShowError($"Activation failed: {ex.Message}");
                }
                finally
                {
                    ActivateButton.IsEnabled = true;
                    ActivateButton.Content = "Activate Pro";
                }
            }
            catch (Exception outerEx)
            {
                // AUDIT FIX (ERROR-CRIT-1): Catch-all for unhandled exceptions outside inner try
                ErrorLogger.LogError("CRITICAL: Unhandled exception in Activate_Click", outerEx);
                try
                {
                    ShowError($"Activation failed: {outerEx.Message}");
                    ActivateButton.IsEnabled = true;
                    ActivateButton.Content = "Activate Pro";
                }
                catch
                {
                    // Last resort - can't even show error UI
                }
            }
        }

        private void UseFree_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Continue with Free version?\n\n" +
                "Free version includes:\n" +
                "• Base Whisper model (good accuracy)\n" +
                "• Full voice typing features\n\n" +
                "Upgrade to Pro anytime at voicelite.app",
                "Continue with Free Version",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                DialogResult = false;
                Close();
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                // AUDIT FIX: Use 'using' to ensure Process disposal
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to open browser", ex);
            }
        }

        private void ShowError(string message)
        {
            StatusBorder.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(248, 215, 218));
            StatusBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(220, 53, 69));
            StatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(114, 28, 36));
            StatusTextBlock.Text = "❌ " + message;
            StatusBorder.Visibility = Visibility.Visible;
        }

        private void ShowInfo(string message)
        {
            StatusBorder.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(209, 236, 241));
            StatusBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(23, 162, 184));
            StatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(12, 84, 96));
            StatusTextBlock.Text = "ℹ️ " + message;
            StatusBorder.Visibility = Visibility.Visible;
        }
    }
}

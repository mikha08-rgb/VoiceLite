using System;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using VoiceLite.Models;
using VoiceLite.Services;

namespace VoiceLite
{
    public partial class FeedbackWindow : Window
    {
        private readonly Settings _settings;
        private readonly string _lastError;

        public FeedbackWindow(Settings settings, string? lastError = null)
        {
            InitializeComponent();
            _settings = settings;
            _lastError = lastError ?? string.Empty;

            LoadMetadataInfo();
        }

        private void LoadMetadataInfo()
        {
            try
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                var versionString = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "Unknown";

                var osVersion = Environment.OSVersion.ToString();
                var modelName = _settings.WhisperModel ?? "Unknown";

                var metadata = $"App Version: {versionString}\n" +
                             $"OS: {osVersion}\n" +
                             $"Model: {modelName}\n" +
                             $"Language: {_settings.Language}";

                if (!string.IsNullOrEmpty(_lastError))
                {
                    metadata += $"\nLast Error: {_lastError.Substring(0, Math.Min(100, _lastError.Length))}...";
                }

                MetadataTextBlock.Text = metadata;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("LoadMetadataInfo", ex);
                MetadataTextBlock.Text = "Unable to load diagnostic information";
            }
        }

        private async void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(SubjectTextBox.Text))
                {
                    MessageBox.Show("Please enter a subject.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    SubjectTextBox.Focus();
                    return;
                }

                if (SubjectTextBox.Text.Length < 5)
                {
                    MessageBox.Show("Subject must be at least 5 characters.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    SubjectTextBox.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(MessageTextBox.Text))
                {
                    MessageBox.Show("Please enter a message.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    MessageTextBox.Focus();
                    return;
                }

                if (MessageTextBox.Text.Length < 10)
                {
                    MessageBox.Show("Message must be at least 10 characters.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    MessageTextBox.Focus();
                    return;
                }

                // Optional email validation
                if (!string.IsNullOrWhiteSpace(EmailTextBox.Text))
                {
                    if (!IsValidEmail(EmailTextBox.Text))
                    {
                        MessageBox.Show("Please enter a valid email address or leave it blank.", "Validation Error",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        EmailTextBox.Focus();
                        return;
                    }
                }

                // Disable button during submission
                SubmitButton.IsEnabled = false;
                SubmitButton.Content = "Submitting...";

                // Get selected feedback type
                var selectedType = ((ComboBoxItem)FeedbackTypeComboBox.SelectedItem).Tag.ToString() ?? "GENERAL";

                // Prepare metadata
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                var versionString = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "Unknown";

                var feedbackData = new
                {
                    type = selectedType,
                    subject = SubjectTextBox.Text.Trim(),
                    message = MessageTextBox.Text.Trim(),
                    email = string.IsNullOrWhiteSpace(EmailTextBox.Text) ? (string?)null : EmailTextBox.Text.Trim(),
                    metadata = new
                    {
                        appVersion = versionString,
                        osVersion = Environment.OSVersion.ToString(),
                        whisperModel = _settings.WhisperModel,
                        language = _settings.Language,
                        lastError = string.IsNullOrEmpty(_lastError) ? null : _lastError.Substring(0, Math.Min(500, _lastError.Length))
                    }
                };

                // Submit to API
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var jsonContent = JsonSerializer.Serialize(feedbackData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync("https://voicelite.app/api/feedback/submit", content);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        "Thank you for your feedback! We've received your submission and will review it soon.",
                        "Feedback Submitted",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    DialogResult = true;
                    Close();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorLogger.LogMessage($"Feedback submission failed: {response.StatusCode} - {errorContent}");

                    MessageBox.Show(
                        $"Failed to submit feedback (HTTP {response.StatusCode}). Please try again later or contact support.",
                        "Submission Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (HttpRequestException httpEx)
            {
                ErrorLogger.LogError("FeedbackWindow.SubmitButton_Click (Network)", httpEx);
                MessageBox.Show(
                    "Network error - please check your internet connection and try again.",
                    "Network Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("FeedbackWindow.SubmitButton_Click", ex);
                MessageBox.Show(
                    $"An unexpected error occurred: {ex.Message}\n\nPlease try again or contact support.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SubmitButton.IsEnabled = true;
                SubmitButton.Content = "Submit Feedback";
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
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
    }
}

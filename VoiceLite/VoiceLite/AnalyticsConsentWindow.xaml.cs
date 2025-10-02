using System;
using System.Windows;
using VoiceLite.Models;

namespace VoiceLite
{
    public partial class AnalyticsConsentWindow : Window
    {
        private readonly Settings settings;
        public bool UserConsented { get; private set; }

        public AnalyticsConsentWindow(Settings settings)
        {
            InitializeComponent();
            this.settings = settings;
            UserConsented = false;
        }

        private void EnableButton_Click(object sender, RoutedEventArgs e)
        {
            // User opted in to analytics
            settings.EnableAnalytics = true;
            settings.AnalyticsConsentDate = DateTime.UtcNow;
            UserConsented = true;

            DialogResult = true;
            Close();
        }

        private void DeclineButton_Click(object sender, RoutedEventArgs e)
        {
            // User opted out of analytics
            settings.EnableAnalytics = false;
            settings.AnalyticsConsentDate = DateTime.UtcNow;
            UserConsented = false;

            DialogResult = false;
            Close();
        }
    }
}

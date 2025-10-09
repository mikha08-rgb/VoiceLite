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
            // Analytics removed - just close
            UserConsented = true;
            DialogResult = true;
            Close();
        }

        private void DeclineButton_Click(object sender, RoutedEventArgs e)
        {
            // Analytics removed - just close
            UserConsented = false;
            DialogResult = false;
            Close();
        }
    }
}

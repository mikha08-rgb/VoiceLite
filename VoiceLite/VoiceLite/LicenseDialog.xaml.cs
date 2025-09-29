using System.Text.RegularExpressions;
using System.Windows;

namespace VoiceLite
{
    public partial class LicenseDialog : Window
    {
        public string LicenseKey { get; private set; } = "";
        public string Email { get; private set; } = "";

        public LicenseDialog()
        {
            InitializeComponent();
        }

        private void Activate_Click(object sender, RoutedEventArgs e)
        {
            var regex = new Regex(@"^PRO-2024-[A-Z0-9]{5}-[A-Z0-9]{3}$");

            if (!regex.IsMatch(LicenseBox.Text))
            {
                MessageBox.Show("Invalid license format", "Error", MessageBoxButton.OK);
                return;
            }

            LicenseKey = LicenseBox.Text;
            Email = EmailBox.Text;
            DialogResult = true;
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
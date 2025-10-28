using System;
using System.Windows;
using VoiceLite.Models;

namespace VoiceLite
{
    /// <summary>
    /// Dialog for adding or editing custom shortcuts
    /// </summary>
    public partial class ShortcutEditDialog : Window
    {
        /// <summary>
        /// The shortcut being edited (or newly created)
        /// </summary>
        public CustomShortcut Shortcut { get; private set; }

        /// <summary>
        /// Constructor for adding a new shortcut
        /// </summary>
        public ShortcutEditDialog()
        {
            InitializeComponent();
            Shortcut = new CustomShortcut();
            DialogTitle.Text = "Add Custom Shortcut";
        }

        /// <summary>
        /// Constructor for editing an existing shortcut
        /// </summary>
        /// <param name="existingShortcut">The shortcut to edit</param>
        public ShortcutEditDialog(CustomShortcut existingShortcut) : this()
        {
            if (existingShortcut == null)
                throw new ArgumentNullException(nameof(existingShortcut));

            Shortcut = existingShortcut;
            DialogTitle.Text = "Edit Custom Shortcut";

            // Load existing values
            TriggerTextBox.Text = existingShortcut.Trigger;
            ReplacementTextBox.Text = existingShortcut.Replacement;
            EnabledCheckBox.IsChecked = existingShortcut.IsEnabled;

            // Enable save button since we have valid data
            ValidateForm();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Update shortcut properties
            Shortcut.Trigger = TriggerTextBox.Text.Trim();
            Shortcut.Replacement = ReplacementTextBox.Text; // Don't trim replacement (might have intentional spaces)
            Shortcut.IsEnabled = EnabledCheckBox.IsChecked ?? true;

            DialogResult = true;
            Close();
        }

        private void TriggerTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ValidateForm();
        }

        private void ReplacementTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ValidateForm();
        }

        /// <summary>
        /// Validates the form and enables/disables the Save button
        /// </summary>
        private void ValidateForm()
        {
            // Require both trigger and replacement to be non-empty
            var triggerValid = !string.IsNullOrWhiteSpace(TriggerTextBox.Text);
            var replacementValid = !string.IsNullOrWhiteSpace(ReplacementTextBox.Text);

            SaveButton.IsEnabled = triggerValid && replacementValid;
        }
    }
}

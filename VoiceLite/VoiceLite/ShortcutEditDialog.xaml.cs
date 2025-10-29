using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using VoiceLite.Models;

namespace VoiceLite
{
    /// <summary>
    /// Dialog for adding or editing custom shortcuts
    /// </summary>
    public partial class ShortcutEditDialog : Window
    {
        private readonly List<CustomShortcut> _existingShortcuts;
        private readonly string? _originalTrigger; // For edit mode: original trigger to allow unchanged

        /// <summary>
        /// The shortcut being edited (or newly created)
        /// </summary>
        public CustomShortcut Shortcut { get; private set; }

        /// <summary>
        /// Constructor for adding a new shortcut
        /// </summary>
        public ShortcutEditDialog(List<CustomShortcut> existingShortcuts)
        {
            InitializeComponent();
            _existingShortcuts = existingShortcuts ?? new List<CustomShortcut>();
            _originalTrigger = null;
            Shortcut = new CustomShortcut();
            DialogTitle.Text = "Add Custom Shortcut";
        }

        /// <summary>
        /// Constructor for editing an existing shortcut
        /// </summary>
        /// <param name="existingShortcut">The shortcut to edit</param>
        /// <param name="allShortcuts">All existing shortcuts for duplicate checking</param>
        public ShortcutEditDialog(CustomShortcut existingShortcut, List<CustomShortcut> allShortcuts)
            : this(allShortcuts)
        {
            if (existingShortcut == null)
                throw new ArgumentNullException(nameof(existingShortcut));

            Shortcut = existingShortcut;
            _originalTrigger = existingShortcut.Trigger; // Store original to allow unchanged
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
            // Final validation before save
            var validationError = ValidateShortcut(TriggerTextBox.Text.Trim(), ReplacementTextBox.Text);
            if (!string.IsNullOrEmpty(validationError))
            {
                MessageBox.Show(validationError, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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
            if (SaveButton == null || TriggerTextBox == null || ReplacementTextBox == null)
                return;

            var trigger = TriggerTextBox.Text.Trim();
            var replacement = ReplacementTextBox.Text;

            var validationError = ValidateShortcut(trigger, replacement);

            if (string.IsNullOrEmpty(validationError))
            {
                SaveButton.IsEnabled = true;
                TriggerTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0xBD, 0xC3, 0xC7)); // Default
                ReplacementTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0xBD, 0xC3, 0xC7));
            }
            else
            {
                SaveButton.IsEnabled = false;
                // Show visual feedback on invalid field
                if (validationError.Contains("trigger", StringComparison.OrdinalIgnoreCase))
                {
                    TriggerTextBox.BorderBrush = Brushes.OrangeRed;
                }
                if (validationError.Contains("replacement", StringComparison.OrdinalIgnoreCase))
                {
                    ReplacementTextBox.BorderBrush = Brushes.OrangeRed;
                }
            }
        }

        /// <summary>
        /// Validates shortcut data and returns error message if invalid, null if valid
        /// </summary>
        private string? ValidateShortcut(string trigger, string replacement)
        {
            // 1. Check trigger is not empty or whitespace-only
            if (string.IsNullOrWhiteSpace(trigger))
                return "Trigger phrase cannot be empty or whitespace-only.";

            // 2. Check replacement is not empty or whitespace-only
            if (string.IsNullOrWhiteSpace(replacement))
                return "Replacement text cannot be empty or whitespace-only.";

            // 3. Check trigger length (prevent extremely long triggers)
            if (trigger.Length > 100)
                return "Trigger phrase is too long (max 100 characters).";

            // 4. Check replacement length (prevent memory issues)
            if (replacement.Length > 5000)
                return "Replacement text is too long (max 5000 characters).";

            // 5. Check for trigger == replacement (case-insensitive)
            if (trigger.Equals(replacement.Trim(), StringComparison.OrdinalIgnoreCase))
                return "Trigger and replacement cannot be the same (prevents infinite loop).";

            // 6. Check for duplicate triggers (case-insensitive)
            // Allow unchanged trigger in edit mode
            var isDuplicate = _existingShortcuts.Any(s =>
                s.Trigger.Equals(trigger, StringComparison.OrdinalIgnoreCase) &&
                !s.Trigger.Equals(_originalTrigger, StringComparison.OrdinalIgnoreCase)
            );

            if (isDuplicate)
                return $"A shortcut with trigger '{trigger}' already exists.";

            // 7. Check if replacement contains the trigger (could cause cascading replacements)
            if (replacement.Contains(trigger, StringComparison.OrdinalIgnoreCase))
                return "Warning: Replacement contains the trigger phrase. This may cause unexpected behavior.";

            return null; // Valid
        }
    }
}

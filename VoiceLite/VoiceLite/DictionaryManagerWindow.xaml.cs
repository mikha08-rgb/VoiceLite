using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using VoiceLite.Models;

namespace VoiceLite
{
    public partial class DictionaryManagerWindow : Window
    {
        private ObservableCollection<DictionaryEntry> allEntries;
        private ObservableCollection<DictionaryEntry> filteredEntries;
        private Settings settings;
        private System.Windows.Threading.DispatcherTimer? toastTimer;

        public DictionaryManagerWindow(Settings settings)
        {
            InitializeComponent();
            this.settings = settings;

            // Initialize collections
            allEntries = new ObservableCollection<DictionaryEntry>(settings.CustomDictionaryEntries);
            filteredEntries = new ObservableCollection<DictionaryEntry>(allEntries);

            EntriesDataGrid.ItemsSource = filteredEntries;

            // Initialize toast timer
            toastTimer = new System.Windows.Threading.DispatcherTimer();
            toastTimer.Interval = TimeSpan.FromSeconds(2);
            toastTimer.Tick += (s, e) =>
            {
                ToastNotification.Visibility = Visibility.Collapsed;
                toastTimer?.Stop();
            };
        }

        private void ShowToast(string message)
        {
            ToastText.Text = message;
            ToastNotification.Visibility = Visibility.Visible;
            toastTimer?.Stop();
            toastTimer?.Start();
        }

        private void CategoryRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radio)
            {
                FilterEntries(radio.Name);
            }
        }

        private void FilterEntries(string? radioName)
        {
            if (allEntries == null) return;

            filteredEntries.Clear();

            // Apply category filter
            IEnumerable<DictionaryEntry> filtered = radioName switch
            {
                nameof(AllCategoriesRadio) => allEntries,
                nameof(GeneralCategoryRadio) => allEntries.Where(e => e.Category == DictionaryCategory.General),
                nameof(MedicalCategoryRadio) => allEntries.Where(e => e.Category == DictionaryCategory.Medical),
                nameof(LegalCategoryRadio) => allEntries.Where(e => e.Category == DictionaryCategory.Legal),
                nameof(TechCategoryRadio) => allEntries.Where(e => e.Category == DictionaryCategory.Tech),
                nameof(PersonalCategoryRadio) => allEntries.Where(e => e.Category == DictionaryCategory.Personal),
                _ => allEntries
            };

            // Apply search filter if search text is not empty
            var searchText = SearchTextBox?.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(e =>
                    e.Pattern.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    e.Replacement.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var entry in filtered)
            {
                filteredEntries.Add(entry);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshFilter();
        }

        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (SearchTextBox != null)
            {
                SearchTextBox.Text = string.Empty;
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddEditEntryDialog(null);
            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                allEntries.Add(dialog.Result);
                settings.CustomDictionaryEntries.Add(dialog.Result);
                RefreshFilter();
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (EntriesDataGrid.SelectedItem is DictionaryEntry selected)
            {
                var dialog = new AddEditEntryDialog(selected);
                if (dialog.ShowDialog() == true && dialog.Result != null)
                {
                    var index = allEntries.IndexOf(selected);
                    allEntries[index] = dialog.Result;
                    settings.CustomDictionaryEntries[index] = dialog.Result;
                    RefreshFilter();
                }
            }
            else
            {
                MessageBox.Show("Please select an entry to edit.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (EntriesDataGrid.SelectedItems.Count > 0)
            {
                var count = EntriesDataGrid.SelectedItems.Count;
                var message = count == 1
                    ? $"Are you sure you want to delete this entry?\n\nPattern: {(EntriesDataGrid.SelectedItem as DictionaryEntry)?.Pattern}\nReplacement: {(EntriesDataGrid.SelectedItem as DictionaryEntry)?.Replacement}"
                    : $"Are you sure you want to delete {count} selected entries?";

                var result = MessageBox.Show(message, "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var toDelete = EntriesDataGrid.SelectedItems.Cast<DictionaryEntry>().ToList();
                    foreach (var entry in toDelete)
                    {
                        allEntries.Remove(entry);
                        settings.CustomDictionaryEntries.Remove(entry);
                    }
                    RefreshFilter();
                    ShowToast($"✓ Deleted {count} {(count == 1 ? "entry" : "entries")}");
                }
            }
            else
            {
                MessageBox.Show("Please select one or more entries to delete.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LoadMedicalButton_Click(object sender, RoutedEventArgs e)
        {
            LoadTemplate(CustomDictionaryTemplates.GetMedicalTemplate(), "Medical");
        }

        private void LoadLegalButton_Click(object sender, RoutedEventArgs e)
        {
            LoadTemplate(CustomDictionaryTemplates.GetLegalTemplate(), "Legal");
        }

        private void LoadTechButton_Click(object sender, RoutedEventArgs e)
        {
            LoadTemplate(CustomDictionaryTemplates.GetTechTemplate(), "Tech");
        }

        private void LoadTemplate(List<DictionaryEntry> template, string templateName)
        {
            var result = MessageBox.Show($"Load {template.Count} {templateName} entries?\n\nThis will add new entries without removing existing ones.",
                "Load Template", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                int addedCount = 0;
                foreach (var entry in template)
                {
                    // Avoid duplicates
                    if (!allEntries.Any(e => e.Pattern.Equals(entry.Pattern, StringComparison.OrdinalIgnoreCase)))
                    {
                        allEntries.Add(entry);
                        settings.CustomDictionaryEntries.Add(entry);
                        addedCount++;
                    }
                }
                RefreshFilter();
                ShowToast($"✓ Loaded {addedCount} {templateName} entries");
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import VoiceShortcuts",
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = "json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = File.ReadAllText(dialog.FileName);
                    var imported = JsonSerializer.Deserialize<List<DictionaryEntry>>(json);

                    if (imported != null && imported.Count > 0)
                    {
                        var result = MessageBox.Show($"Import {imported.Count} entries?\n\nThis will add new entries without removing existing ones.",
                            "Import Dictionary", MessageBoxButton.YesNo, MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            int addedCount = 0;
                            foreach (var entry in imported)
                            {
                                if (!allEntries.Any(e => e.Pattern.Equals(entry.Pattern, StringComparison.OrdinalIgnoreCase)))
                                {
                                    allEntries.Add(entry);
                                    settings.CustomDictionaryEntries.Add(entry);
                                    addedCount++;
                                }
                            }
                            RefreshFilter();
                            ShowToast($"✓ Imported {addedCount} entries");
                        }
                    }
                    else
                    {
                        MessageBox.Show("No entries found in the file.", "Import Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to import dictionary:\n{ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (allEntries.Count == 0)
            {
                MessageBox.Show("No entries to export.", "Export Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export VoiceShortcuts",
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = "json",
                FileName = "voicelite-shortcuts.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = JsonSerializer.Serialize(allEntries.ToList(), new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(dialog.FileName, json);
                    ShowToast($"✓ Exported {allEntries.Count} entries");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export dictionary:\n{ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Save changes to settings
            DialogResult = true;
            Close();
        }

        private void RefreshFilter()
        {
            var selectedRadio = new[] { AllCategoriesRadio, GeneralCategoryRadio, MedicalCategoryRadio, LegalCategoryRadio, TechCategoryRadio, PersonalCategoryRadio }
                .FirstOrDefault(r => r.IsChecked == true);
            FilterEntries(selectedRadio?.Name);
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Ctrl+S to save and close
            if (e.Key == System.Windows.Input.Key.S && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                e.Handled = true;
                CloseButton_Click(this, new RoutedEventArgs());
            }
            // Escape to close
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                e.Handled = true;
                DialogResult = true;
                Close();
            }
        }

        private void EntriesDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Only trigger if double-clicking on a row (not headers or empty space)
            if (EntriesDataGrid.SelectedItem is DictionaryEntry)
            {
                EditButton_Click(sender, e);
            }
        }
    }

    // Add/Edit Entry Dialog
    public partial class AddEditEntryDialog : Window
    {
        public DictionaryEntry? Result { get; private set; }

        public AddEditEntryDialog(DictionaryEntry? existing)
        {
            Width = 500;
            Height = 350;
            Title = existing == null ? "Add Entry" : "Edit Entry";
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = System.Windows.Media.Brushes.White;

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Pattern
            var patternLabel = new TextBlock { Text = "Pattern (what to replace):", Margin = new Thickness(0, 0, 0, 5) };
            var patternBox = new TextBox { Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(5), Text = existing?.Pattern ?? "" };
            Grid.SetRow(patternLabel, 0);
            Grid.SetRow(patternBox, 1);

            // Replacement
            var replacementLabel = new TextBlock { Text = "Replacement (replace with):", Margin = new Thickness(0, 0, 0, 5) };
            var replacementBox = new TextBox { Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(5), Text = existing?.Replacement ?? "" };
            Grid.SetRow(replacementLabel, 2);
            Grid.SetRow(replacementBox, 3);

            // Category
            var categoryLabel = new TextBlock { Text = "Category:", Margin = new Thickness(0, 0, 0, 5) };
            var categoryCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 12) };
            categoryCombo.Items.Add(DictionaryCategory.General);
            categoryCombo.Items.Add(DictionaryCategory.Medical);
            categoryCombo.Items.Add(DictionaryCategory.Legal);
            categoryCombo.Items.Add(DictionaryCategory.Tech);
            categoryCombo.Items.Add(DictionaryCategory.Personal);
            categoryCombo.SelectedItem = existing?.Category ?? DictionaryCategory.Personal;
            Grid.SetRow(categoryLabel, 4);
            Grid.SetRow(categoryCombo, 5);

            // Checkboxes
            var caseSensitiveCheck = new CheckBox { Content = "Case Sensitive", Margin = new Thickness(0, 0, 0, 5), IsChecked = existing?.CaseSensitive ?? false };
            var wholeWordCheck = new CheckBox { Content = "Match Whole Words Only", IsChecked = existing?.WholeWord ?? true };
            var checkPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            checkPanel.Children.Add(caseSensitiveCheck);
            checkPanel.Children.Add(wholeWordCheck);
            Grid.SetRow(checkPanel, 6);

            // Buttons
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var saveButton = new Button { Content = "Save", Width = 80, Height = 32, Margin = new Thickness(0, 0, 8, 0) };
            var cancelButton = new Button { Content = "Cancel", Width = 80, Height = 32 };

            saveButton.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(patternBox.Text) || string.IsNullOrWhiteSpace(replacementBox.Text))
                {
                    MessageBox.Show("Pattern and Replacement are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Result = new DictionaryEntry
                {
                    Pattern = patternBox.Text.Trim(),
                    Replacement = replacementBox.Text.Trim(),
                    Category = (DictionaryCategory)categoryCombo.SelectedItem,
                    CaseSensitive = caseSensitiveCheck.IsChecked ?? false,
                    WholeWord = wholeWordCheck.IsChecked ?? true,
                    IsEnabled = true
                };

                DialogResult = true;
                Close();
            };

            cancelButton.Click += (s, e) => { DialogResult = false; Close(); };

            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 7);

            grid.Children.Add(patternLabel);
            grid.Children.Add(patternBox);
            grid.Children.Add(replacementLabel);
            grid.Children.Add(replacementBox);
            grid.Children.Add(categoryLabel);
            grid.Children.Add(categoryCombo);
            grid.Children.Add(checkPanel);
            grid.Children.Add(buttonPanel);

            Content = grid;
        }
    }
}

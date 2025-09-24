using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using VoiceLite.Models;
using VoiceLite.Services;

namespace VoiceLite.Controls
{
    public partial class ModelComparisonControl : UserControl
    {
        private WhisperModelInfo? selectedModel;
        private List<WhisperModelInfo> models = new List<WhisperModelInfo>();
        private string whisperPath;

        public event EventHandler<WhisperModelInfo>? ModelSelected;

        public WhisperModelInfo? SelectedModel
        {
            get => selectedModel;
            set
            {
                selectedModel = value;
                UpdateSelection();
            }
        }

        public ModelComparisonControl()
        {
            InitializeComponent();
            whisperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whisper");
            LoadModels();
        }

        private void LoadModels()
        {
            try
            {
                models = WhisperModelInfo.GetAvailableModels(whisperPath);
                ModelsItemsControl.ItemsSource = models;

                // Calculate total installed size
                var totalSize = models.Where(m => m.IsInstalled).Sum(m => m.FileSizeBytes);
                TotalSizeText.Text = FormatFileSize(totalSize);

                // Update recommendation
                UpdateRecommendation();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading models: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateRecommendation()
        {
            try
            {
                double availableRAM = GetAvailableRAMGB();
                bool prioritizeSpeed = PrioritizeSpeedCheckBox?.IsChecked ?? false;
                var recommended = WhisperModelInfo.GetRecommendedModel(availableRAM, prioritizeSpeed);

                if (recommended != null)
                {
                    string priority = prioritizeSpeed ? "speed priority" : "best accuracy";
                    RecommendationText.Text = $"Based on your {availableRAM:F1}GB available RAM and {priority}, we recommend '{recommended.DisplayName}'";

                    // Mark the recommended model
                    foreach (var model in models)
                    {
                        model.IsRecommended = (model.FileName == recommended.FileName)
                            ? System.Windows.Visibility.Visible
                            : System.Windows.Visibility.Collapsed;
                    }
                }
            }
            catch
            {
                RecommendationText.Text = "We recommend 'Small' for balanced performance";
            }
        }

        private double GetAvailableRAMGB()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        ulong totalVisible = (ulong)obj["TotalVisibleMemorySize"];
                        ulong freePhysical = (ulong)obj["FreePhysicalMemory"];
                        return freePhysical / (1024.0 * 1024.0); // Convert KB to GB
                    }
                }
            }
            catch { }
            return 4.0; // Default to 4GB if detection fails
        }

        private void ModelCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is WhisperModelInfo model)
            {
                SelectModel(model);
            }
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is WhisperModelInfo model)
            {
                if (!model.IsInstalled)
                {
                    DownloadModel(model);
                }
                else
                {
                    SelectModel(model);
                }
            }
        }

        private void SelectModel(WhisperModelInfo model)
        {
            if (!model.IsInstalled)
            {
                MessageBox.Show($"Please download {model.DisplayName} model first.",
                    "Model Not Installed", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            selectedModel = model;
            UpdateSelection();
            ModelSelected?.Invoke(this, model);
            TestModelButton.IsEnabled = true;
        }

        private void UpdateSelection()
        {
            // Update visual selection state
            foreach (var item in ModelsItemsControl.Items)
            {
                var container = ModelsItemsControl.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                if (container != null)
                {
                    var border = FindVisualChild<Border>(container);
                    if (border != null)
                    {
                        var model = item as WhisperModelInfo;
                        if (model == selectedModel)
                        {
                            border.Style = FindResource("SelectedCardStyle") as Style;
                            // Update button text for selected model
                            var button = FindVisualChild<Button>(border);
                            if (button != null)
                            {
                                button.Content = "Selected ✓";
                                button.IsEnabled = false;
                            }
                        }
                        else if (model?.IsInstalled == true)
                        {
                            border.Style = FindResource("ModelCardStyle") as Style;
                            var button = FindVisualChild<Button>(border);
                            if (button != null)
                            {
                                button.Content = "Select";
                                button.IsEnabled = true;
                            }
                        }
                    }
                }
            }
        }

        private void DownloadModel(WhisperModelInfo model)
        {
            MessageBox.Show($"Model download functionality would be implemented here.\n\n" +
                $"Model: {model.DisplayName}\n" +
                $"Size: {model.FileSizeDisplay}\n" +
                $"Download URL: https://huggingface.co/ggerganov/whisper.cpp/resolve/main/{model.FileName}",
                "Download Model", MessageBoxButton.OK, MessageBoxImage.Information);

            // TODO: Implement actual download functionality
            // This would download from Hugging Face and show progress
        }

        private void TestModel_Click(object sender, RoutedEventArgs e)
        {
            if (selectedModel == null)
            {
                MessageBox.Show("Please select a model first.", "No Model Selected",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // TODO: Implement model testing functionality
            MessageBox.Show($"Model testing would be implemented here.\n\n" +
                $"Selected Model: {selectedModel.DisplayName}\n" +
                $"This would:\n" +
                $"1. Record a sample audio\n" +
                $"2. Transcribe with selected model\n" +
                $"3. Show timing and accuracy metrics",
                "Test Model", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PriorityChanged(object sender, RoutedEventArgs e)
        {
            UpdateRecommendation();
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.#} {sizes[order]}";
        }
    }

}
using System;
using System.Windows.Media;

namespace VoiceLite.Presentation.ViewModels
{
    /// <summary>
    /// ViewModel for status display (text and indicator).
    /// Minimal extraction - provides bindable properties for status UI.
    /// </summary>
    public class StatusViewModel : ViewModelBase
    {
        #region Fields

        private string _statusText = "Ready";
        private Brush _statusTextColor = Brushes.Green;
        private Brush _statusIndicatorFill = Brushes.Green;
        private double _statusIndicatorOpacity = 1.0;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the status text to display
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        /// <summary>
        /// Gets or sets the status text foreground color
        /// </summary>
        public Brush StatusTextColor
        {
            get => _statusTextColor;
            set => SetProperty(ref _statusTextColor, value);
        }

        /// <summary>
        /// Gets or sets the status indicator fill color
        /// </summary>
        public Brush StatusIndicatorFill
        {
            get => _statusIndicatorFill;
            set => SetProperty(ref _statusIndicatorFill, value);
        }

        /// <summary>
        /// Gets or sets the status indicator opacity
        /// </summary>
        public double StatusIndicatorOpacity
        {
            get => _statusIndicatorOpacity;
            set => SetProperty(ref _statusIndicatorOpacity, value);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Updates the status text and color.
        /// Replaces MainWindow.UpdateStatus(string, Brush) method.
        /// </summary>
        /// <param name="text">Status text to display</param>
        /// <param name="color">Color for both text and indicator</param>
        public void UpdateStatus(string text, Brush color)
        {
            StatusText = text;
            StatusTextColor = color;
            StatusIndicatorFill = color;
            StatusIndicatorOpacity = 1.0;
        }

        /// <summary>
        /// Sets status to Ready with green color
        /// </summary>
        public void SetReady()
        {
            UpdateStatus("Ready", Brushes.Green);
        }

        /// <summary>
        /// Sets status to Recording with red color
        /// </summary>
        public void SetRecording(string elapsed = "0:00")
        {
            UpdateStatus($"Recording {elapsed}", Brushes.Red);
        }

        /// <summary>
        /// Sets status to Processing with orange color
        /// </summary>
        public void SetProcessing(string message = "Processing...")
        {
            UpdateStatus(message, Brushes.Orange);
        }

        /// <summary>
        /// Sets status to Error with red color
        /// </summary>
        public void SetError(string message = "Error")
        {
            UpdateStatus(message, Brushes.Red);
        }

        #endregion
    }
}

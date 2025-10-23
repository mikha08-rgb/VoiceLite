using System;
using System.Globalization;
using System.Windows.Data;

namespace VoiceLite.Utilities
{
    /// <summary>
    /// Converts a DateTime to a human-friendly relative time string (e.g., "2m ago", "Just now").
    /// Used in the history panel to show when transcriptions were created.
    /// </summary>
    public class RelativeTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime timestamp)
            {
                var diff = DateTime.Now - timestamp;

                // Less than a minute ago
                if (diff.TotalSeconds < 60)
                    return "Just now";

                // Less than an hour ago
                if (diff.TotalMinutes < 60)
                    return $"{(int)diff.TotalMinutes}m ago";

                // Less than 24 hours ago
                if (diff.TotalHours < 24)
                {
                    var hours = (int)diff.TotalHours;
                    return $"{hours}h ago";
                }

                // Less than a week ago
                if (diff.TotalDays < 7)
                {
                    var days = (int)diff.TotalDays;
                    return $"{days}d ago";
                }

                // More than a week ago - show actual date
                return timestamp.ToString("MMM d, h:mm tt");
            }

            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("RelativeTimeConverter does not support two-way binding");
        }
    }
}

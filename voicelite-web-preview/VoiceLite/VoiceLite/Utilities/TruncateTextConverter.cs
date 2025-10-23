using System;
using System.Globalization;
using System.Windows.Data;

namespace VoiceLite.Utilities
{
    /// <summary>
    /// Truncates long text strings to a maximum length for compact display.
    /// Adds "..." at the end if text is truncated.
    /// </summary>
    public class TruncateTextConverter : IValueConverter
    {
        private const int DEFAULT_MAX_LENGTH = 100;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                // BUG FIX (BUG-003): Add null/empty check to prevent NullReferenceException
                if (string.IsNullOrEmpty(text))
                    return string.Empty;

                // Allow parameter to override max length
                int maxLength = DEFAULT_MAX_LENGTH;
                if (parameter is int paramLength)
                {
                    maxLength = paramLength;
                }
                else if (parameter is string paramString && int.TryParse(paramString, out var parsedLength))
                {
                    maxLength = parsedLength;
                }

                if (text.Length <= maxLength)
                    return text;

                return text.Substring(0, maxLength) + "...";
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("TruncateTextConverter does not support two-way binding");
        }
    }
}

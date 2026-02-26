using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BackupDbServer.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolean)
            {
                // التحقق من وجود معامل "Inverted"
                bool invert = parameter?.ToString()?.Equals("Inverted", StringComparison.OrdinalIgnoreCase) == true;
                
                if (invert)
                {
                    return boolean ? Visibility.Collapsed : Visibility.Visible;
                }
                else
                {
                    return boolean ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                bool invert = parameter?.ToString()?.Equals("Inverted", StringComparison.OrdinalIgnoreCase) == true;
                
                if (invert)
                {
                    return visibility == Visibility.Collapsed;
                }
                else
                {
                    return visibility == Visibility.Visible;
                }
            }
            return false;
        }
    }
}

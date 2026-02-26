using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace BackupDbServer.Converters
{
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }




   
    public class ConnectionStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                if (status.Contains("نجح") || status.Contains("متصل") || status.Contains("يعمل"))
                {
                    return new SolidColorBrush(Colors.Green);
                }
                else if (status.Contains("فشل") || status.Contains("خطأ") || status.Contains("غير متصل"))
                {
                    return new SolidColorBrush(Colors.Red);
                }
                else if (status.Contains("جاري") || status.Contains("انتظار"))
                {
                    return new SolidColorBrush(Colors.Orange);
                }
            }
            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }





    }


using System;
using System.Globalization;
using System.Windows.Data;

namespace BackupDbServer.Converters
{
    /// <summary>
    /// محول لتحويل Boolean إلى نص للعمليات الجارية
    /// </summary>
    public class BoolToRunningTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isRunning)
            {
                return isRunning ? "1" : "0";
            }
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

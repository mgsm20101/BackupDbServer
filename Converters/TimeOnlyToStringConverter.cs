using System;
using System.Globalization;
using System.Windows.Data;

namespace BackupDbServer.Converters
{
    /// <summary>
    /// محول لتحويل TimeOnly إلى نص والعكس للربط مع TextBox
    /// يدعم تنسيقات متعددة للوقت
    /// </summary>
    public class TimeOnlyToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeOnly timeOnly)
            {
                return timeOnly.ToString("HH:mm");
            }
            return "02:00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string timeString && !string.IsNullOrWhiteSpace(timeString))
            {
                // محاولة تحليل النص إلى TimeOnly بتنسيقات مختلفة
                string[] formats = { "HH:mm", "H:mm", "HH:m", "H:m", "HH", "H" };
                
                foreach (var format in formats)
                {
                    if (TimeOnly.TryParseExact(timeString.Trim(), format, culture, DateTimeStyles.None, out var result))
                    {
                        return result;
                    }
                }
                
                // محاولة أخيرة بالتحليل العام
                if (TimeOnly.TryParse(timeString.Trim(), out var generalResult))
                {
                    return generalResult;
                }
                
                // إذا فشل التحليل، إرجاع الوقت الافتراضي 02:00
                return new TimeOnly(2, 0);
            }
            
            return new TimeOnly(2, 0);
        }
    }
}

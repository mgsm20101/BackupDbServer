using System;
using System.Text.RegularExpressions;
using WpfApp = System.Windows.Application; // تحديد WPF Application بوضوح

namespace BackupDbServer.Helpers
{
    /// <summary>
    /// مساعد موحد لإنشاء وإدارة Progress Handlers
    /// </summary>
    public static class ProgressHelper
    {
        /// <summary>
        /// إنشاء Progress Handler مع دعم تحديث الحالة، النسبة المئوية، والتسجيل
        /// </summary>
        /// <param name="statusUpdate">Action لتحديث نص الحالة</param>
        /// <param name="percentageUpdate">Action لتحديث النسبة المئوية (اختياري)</param>
        /// <param name="logger">Logger للتسجيل (اختياري)</param>
        /// <param name="dispatcher">Dispatcher للـ UI thread (اختياري)</param>
        public static IProgress<string> CreateProgress(
            Action<string> statusUpdate,
            Action<int>? percentageUpdate = null,
            ILogger? logger = null,
            System.Windows.Threading.Dispatcher? dispatcher = null)
        {
            return new Progress<string>(message =>
            {
                // التنفيذ على UI thread إذا تم توفير Dispatcher
                Action execute = () =>
                {
                    // تحديث الحالة
                    statusUpdate?.Invoke(message);

                    // التسجيل
                    logger?.LogInformation(message);

                    // استخراج وتحديث النسبة المئوية إن وجدت
                    if (percentageUpdate != null)
                    {
                        var percentage = ExtractPercentage(message);
                        if (percentage.HasValue)
                        {
                            percentageUpdate(percentage.Value);
                        }
                    }
                };

                if (dispatcher != null)
                {
                    dispatcher.Invoke(execute);
                }
                else if (WpfApp.Current?.Dispatcher != null)
                {
                    WpfApp.Current.Dispatcher.Invoke(execute);
                }
                else
                {
                    execute();
                }
            });
        }

        /// <summary>
        /// إنشاء Progress Handler بسيط لتحديث الحالة فقط
        /// </summary>
        public static IProgress<string> CreateSimpleProgress(Action<string> statusUpdate)
        {
            return CreateProgress(statusUpdate);
        }

        /// <summary>
        /// إنشاء Progress Handler مع تسجيل تلقائي
        /// </summary>
        public static IProgress<string> CreateProgressWithLogging(
            Action<string> statusUpdate,
            ILogger logger)
        {
            return CreateProgress(statusUpdate, null, logger);
        }

        /// <summary>
        /// إنشاء Progress Handler كامل مع جميع الميزات
        /// </summary>
        public static IProgress<string> CreateFullProgress(
            Action<string> statusUpdate,
            Action<int> percentageUpdate,
            ILogger logger)
        {
            return CreateProgress(statusUpdate, percentageUpdate, logger);
        }

        /// <summary>
        /// استخراج النسبة المئوية من رسالة التقدم
        /// </summary>
        /// <param name="message">الرسالة التي قد تحتوي على نسبة مئوية</param>
        /// <returns>النسبة المئوية أو null إذا لم توجد</returns>
        private static int? ExtractPercentage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return null;

            // البحث عن نمط النسبة المئوية: أرقام متبوعة بـ %
            var match = Regex.Match(message, @"(\d+)\s*%");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var percent))
            {
                // التأكد من أن النسبة في النطاق الصحيح
                if (percent >= 0 && percent <= 100)
                {
                    return percent;
                }
            }

            // البحث عن نمط "X من Y"
            var fractionMatch = Regex.Match(message, @"(\d+)\s*من\s*(\d+)");
            if (fractionMatch.Success &&
                int.TryParse(fractionMatch.Groups[1].Value, out var current) &&
                int.TryParse(fractionMatch.Groups[2].Value, out var total) &&
                total > 0)
            {
                return (int)((current / (double)total) * 100);
            }

            return null;
        }

        /// <summary>
        /// تحويل الرسالة الإنجليزية إلى عربية (اختياري)
        /// </summary>
        public static string LocalizeMessage(string message)
        {
            // يمكن توسيع هذه الدالة لترجمة الرسائل المشتركة
            return message
                .Replace("Connecting", "جاري الاتصال")
                .Replace("Starting", "جاري البدء")
                .Replace("Completed", "مكتمل")
                .Replace("Failed", "فشل")
                .Replace("Success", "نجح");
        }

        /// <summary>
        /// دمج عدة Progress Handlers في واحد
        /// </summary>
        public static IProgress<string> CombineProgress(params IProgress<string>[] progresses)
        {
            return new Progress<string>(message =>
            {
                foreach (var progress in progresses)
                {
                    ((IProgress<string>)progress).Report(message);
                }
            });
        }
    }
}

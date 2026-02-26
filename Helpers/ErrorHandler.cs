using System;
using System.Threading.Tasks;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;
using WpfMessageBoxResult = System.Windows.MessageBoxResult;
using WpfApp = System.Windows.Application;

namespace BackupDbServer.Helpers
{
    /// <summary>
    /// مساعد موحد لمعالجة الأخطاء وعرضها للمستخدم
    /// </summary>
    public static class ErrorHandler
    {
        /// <summary>
        /// معالجة خطأ وعرضه للمستخدم مع التسجيل
        /// </summary>
        /// <param name="ex">الاستثناء</param>
        /// <param name="operation">اسم العملية التي فشلت</param>
        /// <param name="logger">Logger للتسجيل</param>
        /// <param name="showMessageBox">هل يتم عرض MessageBox للمستخدم</param>
        public static void Handle(
            Exception ex,
            string operation,
            ILogger logger,
            bool showMessageBox = true)
        {
            var message = $"خطأ في {operation}: {ex.Message}";

            // التسجيل
            logger.LogError(message, ex);

            // عرض للمستخدم
            if (showMessageBox)
            {
                ShowError(operation, ex.Message);
            }
        }

        /// <summary>
        /// عرض رسالة خطأ للمستخدم
        /// </summary>
        public static void ShowError(string operation, string errorMessage)
        {
            ExecuteOnUIThread(() =>
            {
                WpfMessageBox.Show(
                    $"❌ فشل في {operation}\n\nالخطأ: {errorMessage}",
                    "خطأ",
                    WpfMessageBoxButton.OK,
                    WpfMessageBoxImage.Error);
            });
        }

        /// <summary>
        /// عرض رسالة تحذير للمستخدم
        /// </summary>
        public static void ShowWarning(string title, string message)
        {
            ExecuteOnUIThread(() =>
            {
                WpfMessageBox.Show(
                    $"⚠️ {message}",
                    title,
                    WpfMessageBoxButton.OK,
                    WpfMessageBoxImage.Warning);
            });
        }

        /// <summary>
        /// عرض رسالة نجاح للمستخدم
        /// </summary>
        public static void ShowSuccess(string title, string message)
        {
            ExecuteOnUIThread(() =>
            {
                WpfMessageBox.Show(
                    $"✅ {message}",
                    title,
                    WpfMessageBoxButton.OK,
                    WpfMessageBoxImage.Information);
            });
        }

        /// <summary>
        /// طلب تأكيد من المستخدم
        /// </summary>
        public static bool Confirm(string title, string message)
        {
            var result = WpfMessageBoxResult.No;
            
            ExecuteOnUIThread(() =>
            {
                result = WpfMessageBox.Show(
                    message,
                    title,
                    WpfMessageBoxButton.YesNo,
                    WpfMessageBoxImage.Question);
            });

            return result == WpfMessageBoxResult.Yes;
        }

        /// <summary>
        /// تنفيذ عملية مع معالجة الأخطاء التلقائية
        /// </summary>
        public static async Task<T?> TryExecuteAsync<T>(
            Func<Task<T>> operation,
            string operationName,
            ILogger logger,
            bool showErrorToUser = true)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                Handle(ex, operationName, logger, showErrorToUser);
                return default;
            }
        }

        /// <summary>
        /// تنفيذ عملية مع معالجة الأخطاء التلقائية (بدون قيمة إرجاع)
        /// </summary>
        public static async Task<bool> TryExecuteAsync(
            Func<Task> operation,
            string operationName,
            ILogger logger,
            bool showErrorToUser = true)
        {
            try
            {
                await operation();
                return true;
            }
            catch (Exception ex)
            {
                Handle(ex, operationName, logger, showErrorToUser);
                return false;
            }
        }

        /// <summary>
        /// تنفيذ عملية متزامنة مع معالجة الأخطاء
        /// </summary>
        public static T? TryExecute<T>(
            Func<T> operation,
            string operationName,
            ILogger logger,
            bool showErrorToUser = true)
        {
            try
            {
                return operation();
            }
            catch (Exception ex)
            {
                Handle(ex, operationName, logger, showErrorToUser);
                return default;
            }
        }

        /// <summary>
        /// تنفيذ عملية متزامنة مع معالجة الأخطاء (بدون قيمة إرجاع)
        /// </summary>
        public static bool TryExecute(
            Action operation,
            string operationName,
            ILogger logger,
            bool showErrorToUser = true)
        {
            try
            {
                operation();
                return true;
            }
            catch (Exception ex)
            {
                Handle(ex, operationName, logger, showErrorToUser);
                return false;
            }
        }

        /// <summary>
        /// تنفيذ كود على UI Thread
        /// </summary>
        private static void ExecuteOnUIThread(Action action)
        {
            if (WpfApp.Current?.Dispatcher != null)
            {
                if (WpfApp.Current.Dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    WpfApp.Current.Dispatcher.Invoke(action);
                }
            }
            else
            {
                // إذا لم يكن هناك Application.Current (مثل في الاختبارات)
                action();
            }
        }

        /// <summary>
        /// تحويل الاستثناء إلى رسالة مفهومة
        /// </summary>
        public static string GetFriendlyMessage(Exception ex)
        {
            return ex switch
            {
                ArgumentNullException => "قيمة مطلوبة مفقودة",
                ArgumentException => "قيمة غير صالحة",
                UnauthorizedAccessException => "لا توجد صلاحيات كافية",
                System.IO.FileNotFoundException => "الملف غير موجود",
                System.IO.DirectoryNotFoundException => "المجلد غير موجود",
                System.IO.IOException => "خطأ في الوصول إلى الملف",
                TimeoutException => "انتهى وقت العملية",
                TaskCanceledException => "تم إلغاء العملية",
                _ => ex.Message
            };
        }

        /// <summary>
        /// الحصول على تفاصيل كاملة للخطأ (للتسجيل)
        /// </summary>
        public static string GetDetailedMessage(Exception ex)
        {
            var details = $"الرسالة: {ex.Message}\n";
            details += $"النوع: {ex.GetType().Name}\n";
            
            if (ex.InnerException != null)
            {
                details += $"الاستثناء الداخلي: {ex.InnerException.Message}\n";
            }
            
            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                details += $"Stack Trace:\n{ex.StackTrace}";
            }
            
            return details;
        }
    }
}

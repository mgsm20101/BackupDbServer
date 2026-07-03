using System.Configuration;
using System.Data;
using System.Windows;
using BackupDbServer.Helpers;

namespace BackupDbServer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private HealthMonitor? _healthMonitor;

        protected override void OnStartup(StartupEventArgs e)
        {
            // تهيئة ServiceContainer - نقطة واحدة لإدارة جميع الخدمات
            ServiceContainer.Initialize();
            
            // الحصول على Logger من ServiceContainer
            var logger = ServiceContainer.Logger;
            
            // تفعيل نظام الحماية من الأخطاء
            CrashProtectionManager.Initialize(logger);
            
            // تفعيل نظام مراقبة الصحة
            _healthMonitor = new HealthMonitor(logger);
            
            // التحقق من إغلاق مفاجئ سابق
            if (CrashProtectionManager.CheckForPreviousCrash())
            {
                logger.LogWarning("تم اكتشاف إغلاق مفاجئ في الجلسة السابقة");
                System.Windows.MessageBox.Show("تم اكتشاف إغلاق مفاجئ في الجلسة السابقة.\nتم حفظ تفاصيل الحالة في ملفات السجل.", 
                              "استرداد من إغلاق مفاجئ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            
            // التحقق من ملف صحة سابق
            if (_healthMonitor.CheckForPreviousHealthFile())
            {
                logger.LogInformation("تم العثور على ملف مراقبة صحة من جلسة سابقة");
            }

            // التحقق من أن هذه هي النسخة الوحيدة المشغلة
            if (!SingleInstanceManager.IsFirstInstance())
            {
                // إذا كان هناك نسخة أخرى تعمل، إنهاء هذه النسخة
                Current.Shutdown();
                return;
            }

            // بوابة كلمة السر عند بدء التشغيل: إذا كانت الحماية مفعّلة، اطلبها فوراً قبل ظهور النافذة
            var appLock = ServiceContainer.AppLock;
            if (appLock.IsEnabled)
            {
                appLock.Lock();

                // منع الإغلاق التلقائي عند إغلاق نافذة الحوار قبل إنشاء النافذة الرئيسية
                var previousShutdownMode = ShutdownMode;
                ShutdownMode = ShutdownMode.OnExplicitShutdown;

                bool unlocked;
                try
                {
                    unlocked = appLock.TryUnlock();
                }
                catch (Exception ex)
                {
                    logger.LogError("خطأ أثناء طلب كلمة السر عند بدء التشغيل", ex);
                    Current.Shutdown();
                    return;
                }

                ShutdownMode = previousShutdownMode;

                if (!unlocked)
                {
                    // فشل/إلغاء إدخال كلمة السر — إنهاء التطبيق
                    logger.LogWarning("تم إلغاء فتح البرنامج: لم تُدخَل كلمة السر الصحيحة عند بدء التشغيل");
                    Current.Shutdown();
                    return;
                }
            }

            logger.LogInformation("بدء تشغيل التطبيق مع نظام الحماية المفعل وServiceContainer");
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                var logger = ServiceContainer.Logger;
                logger.LogInformation($"إغلاق التطبيق بدء - كود الخروج: {e.ApplicationExitCode}");
                
                // تنظيف نظام مراقبة الصحة
                _healthMonitor?.Dispose();
                
                // تنظيف نظام الحماية
                CrashProtectionManager.Cleanup();
                
                // تنظيف موارد SingleInstanceManager
                SingleInstanceManager.Cleanup();
                
                // إيقاف جميع الخدمات عبر ServiceContainer
                ServiceContainer.Shutdown();
                
                logger.LogInformation("تم إغلاق التطبيق بنجاح");
            }
            catch (Exception ex)
            {
                // حفظ آخر خطأ في ملف طوارئ
                try
                {
                    var errorFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "final_error.log");
                    System.IO.File.WriteAllText(errorFile, $"[{DateTime.Now}] خطأ في OnExit: {ex}");
                }
                catch { /* لا يمكن فعل المزيد */ }
            }
            finally
            {
                base.OnExit(e);
            }
        }
    }

}

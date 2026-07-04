using System;
using System.IO;
using System.Text.Json;
using BackupDbServer.Models;
using BackupDbServer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BackupDbServer.Helpers
{
    /// <summary>
    /// نقطة وصول موحّدة للخدمات المشتركة. تُحلّ الخدمات الآن من حاوية DI
    /// (انظر Core/Infrastructure/ServiceCollectionExtensions.cs) بدل الإنشاء اليدوي.
    /// تُبقي هذه الواجهة الثابتة متوافقة مع كل الاستدعاءات الحالية (ServiceContainer.X)
    /// أثناء هجرة تدريجية لباقي الكود نحو حقن التبعيات المباشر.
    /// </summary>
    public static class ServiceContainer
    {
        private static IServiceProvider? _provider;
        private static bool _isInitialized;

        public static ILogger Logger => Resolve<ILogger>();
        public static BackupRestoreService BackupService => Resolve<BackupRestoreService>();
        public static GoogleDriveServiceFixed GoogleDriveService => Resolve<GoogleDriveServiceFixed>();
        public static IAutoBackupService AutoBackupService => Resolve<IAutoBackupService>();
        public static IScheduledBackupService ScheduledBackupService => Resolve<IScheduledBackupService>();
        public static DailyTaskService DailyTaskService => Resolve<DailyTaskService>();
        public static BackupRetentionPolicy RetentionPolicy => Resolve<BackupRetentionPolicy>();
        public static AppLockManager AppLock => Resolve<AppLockManager>();
        public static XmlMaintenanceService XmlMaintenance => Resolve<XmlMaintenanceService>();

        public static bool IsInitialized => _isInitialized;

        private static T Resolve<T>() where T : notnull
        {
            if (_provider == null)
                throw new InvalidOperationException("ServiceContainer.Initialize(provider) لم يُستدعَ بعد.");
            return _provider.GetRequiredService<T>();
        }

        /// <summary>
        /// تهيئة الحاوية بمزوّد الخدمات الناتج عن الـ Host (يُستدعى مرة واحدة من App.xaml.cs).
        /// </summary>
        public static void Initialize(IServiceProvider provider)
        {
            if (_isInitialized)
            {
                Logger.LogWarning("⚠️ ServiceContainer مُهيأ مسبقاً");
                return;
            }

            _provider = provider;

            Logger.LogInformation("🚀 بدء تهيئة ServiceContainer...");

            // التأكد من إنشاء الخدمات الأساسية (نفس ترتيب الإنشاء القديم)
            _ = Logger;
            _ = BackupService;
            _ = ScheduledBackupService;

            // بدء خدمة المهام اليومية
            DailyTaskService.Start();

            // بدء خدمة النسخ التلقائي إذا كانت مفعلة
            _ = StartAutoBackupIfEnabledAsync();

            _isInitialized = true;
            Logger.LogInformation("✅ تم تهيئة ServiceContainer بنجاح");
        }

        /// <summary>
        /// إيقاف جميع الخدمات عند إغلاق التطبيق
        /// </summary>
        public static void Shutdown()
        {
            if (_provider == null) return;

            Logger.LogInformation("🛑 بدء إيقاف ServiceContainer...");

            try
            {
                AutoBackupService.StopAsync().Wait();
                Logger.LogInformation("✅ تم إيقاف AutoBackupService");

                ScheduledBackupService.StopAsync().Wait();
                Logger.LogInformation("✅ تم إيقاف ScheduledBackupService");

                DailyTaskService.Stop();
                Logger.LogInformation("✅ تم إيقاف DailyTaskService");

                Logger.LogInformation("✅ تم إيقاف ServiceContainer بنجاح");
            }
            catch (Exception ex)
            {
                Logger.LogError($"❌ خطأ أثناء إيقاف ServiceContainer: {ex.Message}", ex);
            }
            finally
            {
                _isInitialized = false;
            }
        }

        /// <summary>
        /// الحصول على مسار ملف اعتماد Google Drive (يُستخدم من تسجيل DI أيضاً).
        /// </summary>
        internal static string GetGoogleDriveCredentialsPath()
        {
            try
            {
                var autoBackupConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BackupDbServer",
                    "auto_backup_config.json");

                if (File.Exists(autoBackupConfigPath))
                {
                    var configJson = File.ReadAllText(autoBackupConfigPath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var config = JsonSerializer.Deserialize<AutoBackupConfig>(configJson, options);

                    if (!string.IsNullOrEmpty(config?.GoogleDriveCredentialsPath))
                    {
                        return config.GoogleDriveCredentialsPath;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"⚠️ فشل تحميل مسار Google Drive Credentials: {ex.Message}");
            }

            // المسار الافتراضي
            return "credentials.json";
        }

        /// <summary>
        /// بدء خدمة النسخ التلقائي إذا كانت مفعلة في الإعدادات
        /// </summary>
        private static async System.Threading.Tasks.Task StartAutoBackupIfEnabledAsync()
        {
            try
            {
                var config = AutoBackupService.GetConfig();
                if (config.IsEnabled)
                {
                    Logger.LogInformation("🔄 خدمة النسخ التلقائي مفعلة - جاري البدء...");
                    await AutoBackupService.StartAsync();
                    Logger.LogInformation("✅ تم بدء خدمة النسخ التلقائي");
                }
                else
                {
                    Logger.LogInformation("ℹ️ خدمة النسخ التلقائي غير مفعلة");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"❌ فشل بدء خدمة النسخ التلقائي: {ex.Message}", ex);
            }
        }
    }
}

using BackupDbServer.Models;
using BackupDbServer.Services;
using BackupDbServer.Service; // إضافة namespace للـ DailyTaskService
using System;
using System.IO;
using System.Text.Json;

namespace BackupDbServer.Helpers
{
    /// <summary>
    /// حاوية الخدمات المركزية - نمط Singleton
    /// تدير دورة حياة جميع الخدمات المشتركة في التطبيق
    /// </summary>
    public static class ServiceContainer
    {
        private static readonly object _lock = new object();
        
        // الخدمات الأساسية
        private static ILogger? _logger;
        private static BackupRestoreService? _backupService;
        private static IAutoBackupService? _autoBackupService;
        private static IScheduledBackupService? _scheduledBackupService;
        private static DailyTaskService? _dailyTaskService;
        private static GoogleDriveServiceFixed? _googleDriveService;
        private static BackupRetentionPolicy? _retentionPolicy;
        
        // حالة التهيئة
        private static bool _isInitialized = false;

        /// <summary>
        /// خدمة التسجيل (Logger) - Singleton
        /// </summary>
        public static ILogger Logger
        {
            get
            {
                if (_logger == null)
                {
                    lock (_lock)
                    {
                        _logger ??= new FileLogger();
                    }
                }
                return _logger;
            }
        }

        /// <summary>
        /// خدمة النسخ الاحتياطي والاستعادة - Singleton
        /// </summary>
        public static BackupRestoreService BackupService
        {
            get
            {
                if (_backupService == null)
                {
                    lock (_lock)
                    {
                        if (_backupService == null)
                        {
                            _backupService = new BackupRestoreService();
                            Logger.LogInformation("✅ تم إنشاء BackupRestoreService");
                        }
                    }
                }
                return _backupService;
            }
        }

        /// <summary>
        /// خدمة Google Drive - Singleton
        /// </summary>
        public static GoogleDriveServiceFixed GoogleDriveService
        {
            get
            {
                if (_googleDriveService == null)
                {
                    lock (_lock)
                    {
                        if (_googleDriveService == null)
                        {
                            var credentialsPath = GetGoogleDriveCredentialsPath();
                            _googleDriveService = new GoogleDriveServiceFixed(Logger, credentialsPath);
                            Logger.LogInformation($"✅ تم إنشاء GoogleDriveService - Credentials: {credentialsPath}");
                        }
                    }
                }
                return _googleDriveService;
            }
        }

        /// <summary>
        /// خدمة النسخ الاحتياطي التلقائي - Singleton
        /// </summary>
        public static IAutoBackupService AutoBackupService
        {
            get
            {
                if (_autoBackupService == null)
                {
                    lock (_lock)
                    {
                        if (_autoBackupService == null)
                        {
                            _autoBackupService = new AutoBackupService(
                                Logger,
                                BackupService,
                                GoogleDriveService);
                            Logger.LogInformation("✅ تم إنشاء AutoBackupService");
                        }
                    }
                }
                return _autoBackupService;
            }
        }

        /// <summary>
        /// خدمة النسخ الاحتياطي المجدول - Singleton
        /// </summary>
        public static IScheduledBackupService ScheduledBackupService
        {
            get
            {
                if (_scheduledBackupService == null)
                {
                    lock (_lock)
                    {
                        if (_scheduledBackupService == null)
                        {
                            _scheduledBackupService = new ScheduledBackupService(
                                BackupService,
                                Logger);
                            Logger.LogInformation("✅ تم إنشاء ScheduledBackupService");
                        }
                    }
                }
                return _scheduledBackupService;
            }
        }

        /// <summary>
        /// خدمة المهام اليومية - Singleton
        /// </summary>
        public static DailyTaskService DailyTaskService
        {
            get
            {
                if (_dailyTaskService == null)
                {
                    lock (_lock)
                    {
                        if (_dailyTaskService == null)
                        {
                            _dailyTaskService = new DailyTaskService(Logger);
                            Logger.LogInformation("✅ تم إنشاء DailyTaskService");
                        }
                    }
                }
                return _dailyTaskService;
            }
        }

        /// <summary>
        /// خدمة سياسة الاحتفاظ بالنسخ الاحتياطية - Singleton
        /// </summary>
        public static BackupRetentionPolicy RetentionPolicy
        {
            get
            {
                if (_retentionPolicy == null)
                {
                    lock (_lock)
                    {
                        if (_retentionPolicy == null)
                        {
                            var settings = new BackupRetentionPolicy.RetentionSettings
                            {
                                DailyRetentionDays = 7,      // آخر 7 أيام: جميع النسخ
                                WeeklyRetentionWeeks = 4,    // 4 أسابيع: نسخة الجمعة
                                MonthlyRetentionMonths = 12, // 12 شهر: نسخة أول الشهر
                                DeleteOlderThanYear = false, // الاحتفاظ بالنسخ القديمة
                                WeeklyBackupDay = DayOfWeek.Friday
                            };
                            _retentionPolicy = new BackupRetentionPolicy(Logger, settings);
                            Logger.LogInformation("✅ تم إنشاء BackupRetentionPolicy");
                        }
                    }
                }
                return _retentionPolicy;
            }
        }

        /// <summary>
        /// تهيئة جميع الخدمات الأساسية عند بدء التطبيق
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized)
            {
                Logger.LogWarning("⚠️ ServiceContainer مُهيأ مسبقاً");
                return;
            }

            lock (_lock)
            {
                if (_isInitialized) return;

                Logger.LogInformation("🚀 بدء تهيئة ServiceContainer...");

                // التأكد من إنشاء الخدمات الأساسية
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
        }

        /// <summary>
        /// إيقاف جميع الخدمات عند إغلاق التطبيق
        /// </summary>
        public static void Shutdown()
        {
            lock (_lock)
            {
                Logger.LogInformation("🛑 بدء إيقاف ServiceContainer...");

                try
                {
                    // إيقاف خدمة النسخ التلقائي
                    if (_autoBackupService != null)
                    {
                        _autoBackupService.StopAsync().Wait();
                        Logger.LogInformation("✅ تم إيقاف AutoBackupService");
                    }

                    // إيقاف خدمة النسخ المجدول
                    if (_scheduledBackupService != null)
                    {
                        _scheduledBackupService.StopAsync().Wait();
                        Logger.LogInformation("✅ تم إيقاف ScheduledBackupService");
                    }

                    // إيقاف خدمة المهام اليومية
                    if (_dailyTaskService != null)
                    {
                        _dailyTaskService.Stop();
                        Logger.LogInformation("✅ تم إيقاف DailyTaskService");
                    }

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
        }

        /// <summary>
        /// إعادة تعيين جميع الخدمات (للاختبارات فقط)
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                Logger.LogWarning("⚠️ إعادة تعيين ServiceContainer...");
                
                Shutdown();
                
                _logger = null;
                _backupService = null;
                _autoBackupService = null;
                _scheduledBackupService = null;
                _dailyTaskService = null;
                _googleDriveService = null;
                _isInitialized = false;
            }
        }

        /// <summary>
        /// تهيئة للاختبارات - يسمح بحقن الخدمات الوهمية
        /// </summary>
        public static void InitializeForTesting(
            ILogger? logger = null,
            BackupRestoreService? backupService = null,
            IAutoBackupService? autoBackupService = null,
            IScheduledBackupService? scheduledBackupService = null)
        {
            lock (_lock)
            {
                _logger = logger;
                _backupService = backupService;
                _autoBackupService = autoBackupService;
                _scheduledBackupService = scheduledBackupService;
                _isInitialized = true;
                
                Logger.LogInformation("✅ تم تهيئة ServiceContainer لوضع الاختبار");
            }
        }

        /// <summary>
        /// الحصول على مسار ملف اعتماد Google Drive
        /// </summary>
        private static string GetGoogleDriveCredentialsPath()
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

        /// <summary>
        /// التحقق من تهيئة ServiceContainer
        /// </summary>
        public static bool IsInitialized => _isInitialized;
    }
}

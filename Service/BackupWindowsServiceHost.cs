using BackupDbServer.Helpers;
using BackupDbServer.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BackupDbServer.Services
{
    public class BackupWindowsServiceHost : IDisposable
    {
        private readonly IScheduledBackupService _scheduledBackupService;
        private readonly IAutoBackupService _autoBackupService;
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task? _serviceTask;
        private bool _isRunning;

        public bool IsRunning => _isRunning;

        public BackupWindowsServiceHost()
        {
            try
            {
                _logger = new FileLogger();
                var backupService = new BackupRestoreService();
                var googleDriveService = new GoogleDriveServiceFixed(_logger);
                
                _scheduledBackupService = new ScheduledBackupService(backupService, _logger);
                _autoBackupService = new AutoBackupService(_logger, backupService, googleDriveService);
                _cancellationTokenSource = new CancellationTokenSource();
                
                _logger.LogInformation("تم تهيئة مضيف خدمة النسخ الاحتياطي بنجاح");
            }
            catch (Exception ex)
            {
                // إنشاء مسجل أساسي في حالة فشل التهيئة
                _logger ??= new FileLogger();
                _logger.LogError($"خطأ في تهيئة مضيف الخدمة: {ex.Message}", ex);
                
                // كتابة الخطأ في سجل أحداث ويندوز كاحتياطي
                try
                {
                    System.Diagnostics.EventLog.WriteEntry("BackupDbServer", 
                        $"خطأ في تهيئة خدمة النسخ الاحتياطي: {ex.Message}", 
                        System.Diagnostics.EventLogEntryType.Error);
                }
                catch
                {
                    // تجاهل الأخطاء في كتابة سجل الأحداث
                }
                
                throw;
            }
        }

        public async Task StartAsync()
        {
            if (_isRunning)
            {
                return;
            }

            _logger.LogInformation("بدء تشغيل خدمة النسخ الاحتياطي التلقائي");

            try
            {
                _isRunning = true;

                // التحقق من إمكانية الوصول للملفات والمجلدات
                await ValidatePermissionsAsync();

                // بدء الخدمات الفرعية مع معالجة أخطاء كل خدمة منفصلة
                try
                {
                    if (_scheduledBackupService.Config.IsEnabled)
                    {
                        await _scheduledBackupService.StartAsync();
                        _logger.LogInformation("تم تشغيل خدمة الجدولة التلقائية");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"فشل تشغيل خدمة الجدولة التلقائية: {ex.Message}");
                }

                try
                {
                    var autoConfig = _autoBackupService.GetConfig();
                    if (autoConfig.IsEnabled)
                    {
                        await _autoBackupService.StartAsync();
                        _logger.LogInformation("تم تشغيل خدمة النسخ الاحتياطي التلقائي");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"فشل تشغيل خدمة النسخ الاحتياطي التلقائي: {ex.Message}");
                }

                // بدء المهمة الرئيسية
                _serviceTask = RunServiceAsync(_cancellationTokenSource.Token);

                _logger.LogInformation("تم بدء تشغيل خدمة النسخ الاحتياطي بنجاح");
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في بدء تشغيل الخدمة: {ex.Message}", ex);
                _isRunning = false;
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (!_isRunning)
            {
                return;
            }

            _logger.LogInformation("بدء إيقاف خدمة النسخ الاحتياطي");

            try
            {
                _isRunning = false;

                // إيقاف المهمة الرئيسية
                _cancellationTokenSource.Cancel();

                if (_serviceTask != null)
                {
                    await _serviceTask;
                }

                // إيقاف الخدمات الفرعية
                await _scheduledBackupService.StopAsync();
                await _autoBackupService.StopAsync();

                _logger.LogInformation("تم إيقاف خدمة النسخ الاحتياطي بنجاح");
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في إيقاف الخدمة: {ex.Message}", ex);
                throw;
            }
        }

        private async Task RunServiceAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("بدء تشغيل المهمة الرئيسية لخدمة النسخ الاحتياطي");

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // فحص دوري للإعدادات كل دقيقة
                        await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);

                        // إعادة تحميل الإعدادات والتحقق من التغييرات
                        await CheckAndUpdateConfigurationsAsync();
                    }
                    catch (OperationCanceledException)
                    {
                        // طبيعي عند إيقاف الخدمة
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"خطأ في المهمة الرئيسية للخدمة: {ex.Message}", ex);
                        // المتابعة حتى لو حدث خطأ
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ غير متوقع في المهمة الرئيسية: {ex.Message}", ex);
            }

            _logger.LogInformation("انتهت المهمة الرئيسية لخدمة النسخ الاحتياطي");
        }

        private async Task CheckAndUpdateConfigurationsAsync()
        {
            try
            {
                // التحقق من إعدادات الجدولة التلقائية
                var currentScheduledConfig = _scheduledBackupService.Config;
                var newScheduledConfig = LoadScheduledBackupConfig();

                if (ConfigurationChanged(currentScheduledConfig, newScheduledConfig))
                {
                    _logger.LogInformation("تم اكتشاف تغيير في إعدادات الجدولة التلقائية");
                    await _scheduledBackupService.UpdateConfigAsync(newScheduledConfig);

                    if (newScheduledConfig.IsEnabled && !_scheduledBackupService.IsRunning)
                    {
                        await _scheduledBackupService.StartAsync();
                    }
                    else if (!newScheduledConfig.IsEnabled && _scheduledBackupService.IsRunning)
                    {
                        await _scheduledBackupService.StopAsync();
                    }
                }

                // التحقق من إعدادات النسخ الاحتياطي التلقائي
                var currentAutoConfig = _autoBackupService.GetConfig();
                var newAutoConfig = LoadAutoBackupConfig();

                if (ConfigurationChanged(currentAutoConfig, newAutoConfig))
                {
                    _logger.LogInformation("تم اكتشاف تغيير في إعدادات النسخ الاحتياطي التلقائي");
                    
                    if (newAutoConfig.IsEnabled && !_autoBackupService.IsRunning)
                    {
                        await _autoBackupService.StartAsync();
                    }
                    else if (!newAutoConfig.IsEnabled && _autoBackupService.IsRunning)
                    {
                        await _autoBackupService.StopAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في فحص الإعدادات: {ex.Message}", ex);
            }
        }

        private ScheduledBackupConfig LoadScheduledBackupConfig()
        {
            // تحميل إعدادات الجدولة من الملف
            return new ScheduledBackupConfig(); // TODO: تنفيذ التحميل الفعلي
        }

        private AutoBackupConfig LoadAutoBackupConfig()
        {
            // تحميل إعدادات النسخ التلقائي من الملف
            return new AutoBackupConfig(); // TODO: تنفيذ التحميل الفعلي
        }

        private bool ConfigurationChanged(ScheduledBackupConfig current, ScheduledBackupConfig updated)
        {
            // مقارنة بسيطة - يمكن تحسينها
            return current.IsEnabled != updated.IsEnabled ||
                   current.ScheduledTime != updated.ScheduledTime ||
                   !current.ScheduledDays.SequenceEqual(updated.ScheduledDays);
        }

        private bool ConfigurationChanged(AutoBackupConfig current, AutoBackupConfig updated)
        {
            // مقارنة بسيطة - يمكن تحسينها
            return current.IsEnabled != updated.IsEnabled ||
                   current.IntervalHours != updated.IntervalHours; // استخدم IntervalHours بدلاً من BackupIntervalHours
        }

        /// <summary>
        /// التحقق من الصلاحيات والوصول للملفات والمجلدات المطلوبة
        /// </summary>
        private async Task ValidatePermissionsAsync()
        {
            try
            {
                _logger.LogInformation("بدء التحقق من الصلاحيات...");

                // التحقق من إمكانية كتابة ملف السجل
                var logPath = Path.Combine(Environment.CurrentDirectory, "application.log");
                try
                {
                    await File.WriteAllTextAsync(logPath + ".test", "test");
                    File.Delete(logPath + ".test");
                    _logger.LogInformation("✓ الوصول لملف السجل متاح");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"⚠ مشكلة في الوصول لملف السجل: {ex.Message}");
                }

                // التحقق من إمكانية كتابة ملفات الإعدادات
                try
                {
                    var settingsTestPath = Path.Combine(Environment.CurrentDirectory, "settings_test.json");
                    await File.WriteAllTextAsync(settingsTestPath, "{}");
                    File.Delete(settingsTestPath);
                    _logger.LogInformation("✓ الوصول لملفات الإعدادات متاح");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"⚠ مشكلة في الوصول لملفات الإعدادات: {ex.Message}");
                }

                // التحقق من إعدادات النسخ الاحتياطي
                try
                {
                    var settings = SettingsManager.LoadSettings();
                    if (!string.IsNullOrEmpty(settings.BackupPath))
                    {
                        if (!Directory.Exists(settings.BackupPath))
                        {
                            Directory.CreateDirectory(settings.BackupPath);
                        }
                        var testFile = Path.Combine(settings.BackupPath, "permission_test.tmp");
                        await File.WriteAllTextAsync(testFile, "test");
                        File.Delete(testFile);
                        _logger.LogInformation($"✓ الوصول لمجلد النسخ الاحتياطي متاح: {settings.BackupPath}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"⚠ مشكلة في الوصول لمجلد النسخ الاحتياطي: {ex.Message}");
                }

                _logger.LogInformation("تم الانتهاء من التحقق من الصلاحيات");
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في التحقق من الصلاحيات: {ex.Message}", ex);
                // لا نرمي الخطأ هنا، فقط نسجله
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _serviceTask?.Wait(TimeSpan.FromSeconds(10));
            _cancellationTokenSource?.Dispose();
            
            // إزالة Dispose للـ services لأنها لا تنفذ IDisposable
            if (_scheduledBackupService is IDisposable disposableScheduled)
            {
                disposableScheduled.Dispose();
            }
        }
    }
}
using BackupDbServer.Helpers;
using BackupDbServer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BackupDbServer.Services
{
    public class ScheduledBackupService : IScheduledBackupService, IDisposable
    {
        private readonly BackupRestoreService _backupService;
        private readonly ILogger _logger;
        private System.Threading.Timer? _timer;
        private bool _isRunning;
        private ScheduledBackupConfig _config;
        private readonly object _lockObject = new();
        private const string ConfigFileName = "scheduled_backup_config.json";

        public bool IsRunning => Config.IsRuning;
        public ScheduledBackupConfig Config => _config;

        public event EventHandler<string>? StatusChanged;
        public event EventHandler<ScheduledBackupResult>? BackupCompleted;

        public ScheduledBackupService(BackupRestoreService backupService, ILogger logger)
        {
            _backupService = backupService;
            _logger = logger;
            _config = LoadConfig();
            
            // تشغيل الخدمة تلقائياً إذا كانت كانت تعمل سابقاً
            if (_config.IsEnabled && _config.IsRuning)
            {
                Task.Run(async () => await StartAsync());
            }
        }

        public async Task StartAsync()
        {
            lock (_lockObject)
            {
                if (_isRunning) return;
                _isRunning = true;
                Config.IsRuning = true;
            }

            _logger.LogInformation("بدء خدمة الجدولة التلقائية للنسخ الاحتياطي");
            StatusChanged?.Invoke(this, "تم تشغيل الجدولة التلقائية");

            await ScheduleNextBackupAsync();
            await SaveConfigAsync();
        }

        public async Task StopAsync()
        {
            lock (_lockObject)
            {
                if (!_isRunning) return;
                _isRunning = false;
            }

            _timer?.Dispose();
            _timer = null;

            // حفظ حالة الإيقاف
            _config.IsRuning = false;
            await SaveConfigAsync();

            _logger.LogInformation("تم إيقاف خدمة الجدولة التلقائية");
            StatusChanged?.Invoke(this, "تم إيقاف الجدولة التلقائية");
            
            await Task.CompletedTask;
        }

        public async Task UpdateConfigAsync(ScheduledBackupConfig config)
        {
            _config = config;
            await SaveConfigAsync();
            
            if (_isRunning)
            {
                await ScheduleNextBackupAsync();
            }
        }

        public async Task<ScheduledBackupResult> RunBackupNowAsync()
        {
            _logger.LogInformation("بدء النسخ الاحتياطي المجدول");
            StatusChanged?.Invoke(this, "جاري تنفيذ النسخ الاحتياطي...");

            var result = new ScheduledBackupResult
            {
                StartTime = DateTime.Now,
                IsScheduled = false
            };

            try
            {
                var settings = SettingsManager.LoadSettings();
                var selectedDatabases = settings.SelectedDatabases?.Where(db => !string.IsNullOrEmpty(db)).ToList() ?? new List<string>();

                if (!selectedDatabases.Any())
                {
                    result.ErrorMessage = "لم يتم تحديد أي قواعد بيانات للنسخ الاحتياطي";
                    result.IsSuccess = false;
                    _logger.LogWarning(result.ErrorMessage);
                    return result;
                }

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var successCount = 0;

                // تحديث إعدادات BackupRestoreService من الإعدادات الرئيسية
                _backupService.UpdateSettings(settings);

                // استخدام BackupRestoreService المحسن بدلاً من BackupExecutor
                var progress = new Progress<string>(message => 
                {
                    _logger.LogInformation(message);
                    StatusChanged?.Invoke(this, message);
                });

                try
                {
                    await _backupService.BackupDatabasesAsync(selectedDatabases, progress);
                    
                    // النسخ نجح - تحديث النتائج
                    result.BackedUpDatabases.AddRange(selectedDatabases);
                    successCount = selectedDatabases.Count;
                    _logger.LogInformation($"✅ تم نسخ {successCount} قاعدة بيانات بنجاح");
                }
                catch (Exception ex)
                {
                    result.FailedDatabases.AddRange(selectedDatabases);
                    _logger.LogError($"خطأ أثناء النسخ الاحتياطي: {ex.Message}");
                }

                result.IsSuccess = successCount > 0;
                result.EndTime = DateTime.Now;
                
                _config.LastBackupTime = result.EndTime;
                await SaveConfigAsync();

                // حذف النسخ القديمة إذا كان مفعلاً
                if (_config.DeleteOldBackups)
                {
                    await DeleteOldBackupsAsync();
                }

                var message = $"تم النسخ الاحتياطي بنجاح لـ {successCount} من أصل {selectedDatabases.Count} قواعد بيانات";
                _logger.LogInformation(message);
                StatusChanged?.Invoke(this, message);

                // إرسال إشعار إذا كان مفعلاً
                if (_config.NotifyOnCompletion && !string.IsNullOrEmpty(_config.NotificationEmail))
                {
                    await SendNotificationAsync(result);
                }
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.EndTime = DateTime.Now;
                
                _logger.LogError($"خطأ في النسخ الاحتياطي المجدول: {ex.Message}");
                StatusChanged?.Invoke(this, $"خطأ في النسخ الاحتياطي: {ex.Message}");

                if (_config.NotifyOnError && !string.IsNullOrEmpty(_config.NotificationEmail))
                {
                    await SendNotificationAsync(result);
                }
            }

            BackupCompleted?.Invoke(this, result);
            
            if (_isRunning)
            {
                await ScheduleNextBackupAsync();
            }

            return result;
        }

        private async Task RunScheduledBackupAsync()
        {
            try
            {
                _logger.LogInformation("بدء النسخ الاحتياطي المجدول التلقائي");
                
                var result = new ScheduledBackupResult
                {
                    StartTime = DateTime.Now,
                    IsScheduled = true
                };

                // تنفيذ النسخ الاحتياطي
                var settings = SettingsManager.LoadSettings();
                var selectedDatabases = settings.SelectedDatabases?.Where(db => !string.IsNullOrEmpty(db)).ToList() ?? new List<string>();

                if (!selectedDatabases.Any())
                {
                    result.ErrorMessage = "لم يتم تحديد أي قواعد بيانات للنسخ الاحتياطي";
                    result.IsSuccess = false;
                    _logger.LogWarning(result.ErrorMessage);
                    BackupCompleted?.Invoke(this, result);
                    
                    // جدولة النسخ التالي حتى لو فشل هذا النسخ
                    if (_isRunning && _config.IsEnabled)
                    {
                        await ScheduleNextBackupAsync();
                    }
                    return;
                }

                var successCount = 0;

                // تحديث إعدادات BackupRestoreService من الإعدادات الرئيسية
                _backupService.UpdateSettings(settings);

                // استخدام BackupRestoreService المحسن بدلاً من BackupExecutor
                var progress = new Progress<string>(message => 
                {
                    _logger.LogInformation(message);
                    StatusChanged?.Invoke(this, message);
                });

                try
                {
                    _logger.LogInformation($"بدء النسخ الاحتياطي التلقائي المجدول لـ {selectedDatabases.Count} قاعدة بيانات");
                    await _backupService.BackupDatabasesAsync(selectedDatabases, progress);
                    
                    // النسخ نجح - تحديث النتائج
                    result.BackedUpDatabases.AddRange(selectedDatabases);
                    successCount = selectedDatabases.Count;
                    _logger.LogInformation($"✅ تم النسخ التلقائي المجدول بنجاح لـ {successCount} قاعدة بيانات");
                }
                catch (Exception ex)
                {
                    result.FailedDatabases.AddRange(selectedDatabases);
                    _logger.LogError($"خطأ أثناء النسخ التلقائي المجدول: {ex.Message}");
                }

                result.IsSuccess = successCount > 0;
                result.EndTime = DateTime.Now;
                
                _config.LastBackupTime = result.EndTime;
                await SaveConfigAsync();

                // حذف النسخ القديمة إذا كان مفعلاً
                if (_config.DeleteOldBackups)
                {
                    await DeleteOldBackupsAsync();
                }

                var message = $"تم النسخ الاحتياطي التلقائي بنجاح لـ {successCount} من أصل {selectedDatabases.Count} قواعد بيانات";
                _logger.LogInformation(message);
                StatusChanged?.Invoke(this, message);

                // إرسال إشعار إذا كان مفعلاً
                if (_config.NotifyOnCompletion && !string.IsNullOrEmpty(_config.NotificationEmail))
                {
                    await SendNotificationAsync(result);
                }

                BackupCompleted?.Invoke(this, result);
            }
            catch (Exception ex)
            {
                var result = new ScheduledBackupResult
                {
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now,
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    IsScheduled = true
                };
                
                _logger.LogError($"خطأ في النسخ الاحتياطي المجدول التلقائي: {ex.Message}");
                StatusChanged?.Invoke(this, $"خطأ في النسخ الاحتياطي التلقائي: {ex.Message}");

                if (_config.NotifyOnError && !string.IsNullOrEmpty(_config.NotificationEmail))
                {
                    await SendNotificationAsync(result);
                }

                BackupCompleted?.Invoke(this, result);
            }
            finally
            {
                // هذا هو الأهم: جدولة النسخ التالي دائماً (سواء نجح أو فشل النسخ الحالي)
                if (_isRunning && _config.IsEnabled)
                {
                    _logger.LogInformation("جدولة النسخ الاحتياطي التالي...");
                    await ScheduleNextBackupAsync();
                }
                else
                {
                    _logger.LogInformation("لن يتم جدولة النسخ التالي - الخدمة متوقفة أو معطلة");
                }
            }
        }

        public DateTime? GetNextBackupTime()
        {
            if (!_config.IsEnabled || !_config.ScheduledDays.Any())
                return null;

            var now = DateTime.Now;
            var todayBackupTime = now.Date.Add(_config.ScheduledTime.ToTimeSpan());

            // إذا كان موعد اليوم لم يأت بعد واليوم من الأيام المجدولة
            if (todayBackupTime > now && _config.ScheduledDays.Contains(now.DayOfWeek))
            {
                return todayBackupTime;
            }

            // البحث عن اليوم التالي المجدول
            for (int i = 1; i <= 7; i++)
            {
                var nextDay = now.AddDays(i);
                if (_config.ScheduledDays.Contains(nextDay.DayOfWeek))
                {
                    return nextDay.Date.Add(_config.ScheduledTime.ToTimeSpan());
                }
            }

            return null;
        }

        private async Task ScheduleNextBackupAsync()
        {
            _timer?.Dispose();

            if (!_config.IsEnabled)
            {
                _config.NextBackupTime = null;
                StatusChanged?.Invoke(this, "الجدولة التلقائية معطلة");
                return;
            }

            var nextBackupTime = GetNextBackupTime();
            if (nextBackupTime == null)
            {
                StatusChanged?.Invoke(this, "لم يتم تحديد أيام للجدولة");
                return;
            }

            _config.NextBackupTime = nextBackupTime;
            await SaveConfigAsync();

            var delay = nextBackupTime.Value - DateTime.Now;
            if (delay.TotalMilliseconds > 0)
            {
                _timer = new System.Threading.Timer(async _ => await RunScheduledBackupAsync(), null, delay, Timeout.InfiniteTimeSpan);
                
                var message = $"النسخ الاحتياطي التالي: {nextBackupTime:yyyy-MM-dd HH:mm}";
                _logger.LogInformation(message);
                StatusChanged?.Invoke(this, message);
            }
        }

        private async Task DeleteOldBackupsAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    var settings = SettingsManager.LoadSettings();
                    var backupPath = settings.BackupPath;
                    
                    if (string.IsNullOrEmpty(backupPath) || !Directory.Exists(backupPath))
                        return;

                    var cutoffDate = DateTime.Now.AddDays(-_config.RetentionDays);
                    var files = Directory.GetFiles(backupPath, "*.bak")
                        .Where(file => File.GetCreationTime(file) < cutoffDate);

                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                            _logger.LogInformation($"تم حذف النسخة القديمة: {Path.GetFileName(file)}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"فشل في حذف الملف {file}: {ex.Message}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ أثناء حذف النسخ القديمة: {ex.Message}");
            }
        }

        private async Task SendNotificationAsync(ScheduledBackupResult result)
        {
            // TODO: تنفيذ إرسال الإشعارات بالبريد الإلكتروني
            await Task.CompletedTask;
        }

        private ScheduledBackupConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigFileName))
                {
                    var json = File.ReadAllText(ConfigFileName);
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    return JsonSerializer.Deserialize<ScheduledBackupConfig>(json, options) ?? new ScheduledBackupConfig();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في تحميل إعدادات الجدولة: {ex.Message}");
            }

            return new ScheduledBackupConfig();
        }

        public async Task SaveConfigAsync()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_config, options);
                await File.WriteAllTextAsync(ConfigFileName, json);
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في حفظ إعدادات الجدولة: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
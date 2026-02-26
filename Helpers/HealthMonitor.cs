using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using BackupDbServer.Helpers;

namespace BackupDbServer.Helpers
{
    /// <summary>
    /// نظام مراقبة صحة التطبيق والتعافي التلقائي
    /// </summary>
    public class HealthMonitor : IDisposable
    {
        private readonly ILogger _logger;
        private readonly System.Threading.Timer _healthCheckTimer;
        private readonly string _healthFile;
        private bool _disposed = false;
        private DateTime _lastHealthUpdate;
        private readonly TimeSpan _healthCheckInterval = TimeSpan.FromMinutes(1);
        private readonly TimeSpan _healthTimeout = TimeSpan.FromMinutes(5);

        public HealthMonitor(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _healthFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "health.status");
            _lastHealthUpdate = DateTime.Now;

            // بدء مراقبة الصحة كل دقيقة
            _healthCheckTimer = new System.Threading.Timer(PerformHealthCheck, null, _healthCheckInterval, _healthCheckInterval);
            
            _logger.LogInformation("تم تفعيل نظام مراقبة الصحة");
            UpdateHealthStatus("تم بدء التطبيق");
        }

        /// <summary>
        /// تحديث حالة الصحة
        /// </summary>
        public void UpdateHealthStatus(string status, bool isHealthy = true)
        {
            try
            {
                _lastHealthUpdate = DateTime.Now;
                
                var healthData = new
                {
                    Timestamp = _lastHealthUpdate,
                    Status = status,
                    IsHealthy = isHealthy,
                    ProcessId = Environment.ProcessId,
                    MemoryUsage = GC.GetTotalMemory(false),
                    ThreadCount = Process.GetCurrentProcess().Threads.Count,
                    UpTime = DateTime.Now - Process.GetCurrentProcess().StartTime
                };

                var json = System.Text.Json.JsonSerializer.Serialize(healthData, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(_healthFile, json);
                
                if (!isHealthy)
                {
                    _logger.LogWarning($"تحديث حالة صحة غير جيدة: {status}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في تحديث حالة الصحة: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// إجراء فحص صحة دوري
        /// </summary>
        private void PerformHealthCheck(object? state)
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var memoryUsage = GC.GetTotalMemory(false);
                var memoryUsageMB = memoryUsage / 1024 / 1024;

                // فحص استخدام الذاكرة
                if (memoryUsageMB > 500) // إذا تجاوز 500 ميجا
                {
                    _logger.LogWarning($"استخدام ذاكرة عالي: {memoryUsageMB} MB");
                    
                    // محاولة تحرير الذاكرة
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    
                    var newMemoryUsage = GC.GetTotalMemory(false) / 1024 / 1024;
                    _logger.LogInformation($"تم تحرير الذاكرة: {memoryUsageMB} MB -> {newMemoryUsage} MB");
                }

                // فحص عدد الخيوط
                var threadCount = currentProcess.Threads.Count;
                if (threadCount > 50)
                {
                    _logger.LogWarning($"عدد خيوط عالي: {threadCount}");
                }

                // فحص استجابة النظام
                var timeSinceLastUpdate = DateTime.Now - _lastHealthUpdate;
                if (timeSinceLastUpdate > _healthTimeout)
                {
                    _logger.LogError($"النظام غير مستجيب: آخر تحديث منذ {timeSinceLastUpdate.TotalMinutes:F1} دقيقة");
                    UpdateHealthStatus("النظام غير مستجيب", false);
                }
                else
                {
                    UpdateHealthStatus($"النظام يعمل بشكل طبيعي - ذاكرة: {memoryUsageMB} MB, خيوط: {threadCount}");
                }

                // فحص مساحة القرص الصلب
                CheckDiskSpace();

            }
            catch (Exception ex)
            {
                _logger.LogError("خطأ في فحص الصحة الدوري", ex);
                UpdateHealthStatus($"خطأ في فحص الصحة: {ex.Message}", false);
            }
        }

        /// <summary>
        /// فحص مساحة القرص الصلب
        /// </summary>
        private void CheckDiskSpace()
        {
            try
            {
                var currentDrive = new DriveInfo(AppDomain.CurrentDomain.BaseDirectory);
                var freeSpaceGB = currentDrive.AvailableFreeSpace / 1024 / 1024 / 1024;
                var totalSpaceGB = currentDrive.TotalSize / 1024 / 1024 / 1024;
                var usedPercentage = ((double)(totalSpaceGB - freeSpaceGB) / totalSpaceGB) * 100;

                if (freeSpaceGB < 1) // أقل من 1 جيجا متاح
                {
                    _logger.LogError($"مساحة القرص منخفضة جداً: {freeSpaceGB:F1} GB متاح من {totalSpaceGB:F1} GB");
                    UpdateHealthStatus($"مساحة القرص منخفضة: {freeSpaceGB:F1} GB متاح", false);
                }
                else if (usedPercentage > 90)
                {
                    _logger.LogWarning($"مساحة القرص منخفضة: {usedPercentage:F1}% مستخدم");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("خطأ في فحص مساحة القرص", ex);
            }
        }

        /// <summary>
        /// تقرير حالة مفصلة للنظام
        /// </summary>
        public string GenerateHealthReport()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var report = new System.Text.StringBuilder();
                
                report.AppendLine("=== تقرير صحة النظام ===");
                report.AppendLine($"الوقت: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine($"معرف العملية: {Environment.ProcessId}");
                report.AppendLine($"وقت البدء: {currentProcess.StartTime:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine($"مدة التشغيل: {DateTime.Now - currentProcess.StartTime:d\\:hh\\:mm\\:ss}");
                report.AppendLine($"استخدام الذاكرة: {GC.GetTotalMemory(false) / 1024 / 1024:F1} MB");
                report.AppendLine($"عدد الخيوط: {currentProcess.Threads.Count}");
                report.AppendLine($"آخر تحديث صحة: {_lastHealthUpdate:yyyy-MM-dd HH:mm:ss}");
                
                // معلومات النظام
                report.AppendLine($"نظام التشغيل: {Environment.OSVersion}");
                report.AppendLine($"اسم الجهاز: {Environment.MachineName}");
                report.AppendLine($"المستخدم: {Environment.UserName}");
                report.AppendLine($"مجلد العمل: {Environment.CurrentDirectory}");
                
                // معلومات القرص
                try
                {
                    var currentDrive = new DriveInfo(AppDomain.CurrentDomain.BaseDirectory);
                    var freeSpaceGB = currentDrive.AvailableFreeSpace / 1024 / 1024 / 1024;
                    var totalSpaceGB = currentDrive.TotalSize / 1024 / 1024 / 1024;
                    report.AppendLine($"مساحة القرص: {freeSpaceGB:F1} GB متاح من {totalSpaceGB:F1} GB");
                }
                catch (Exception ex)
                {
                    report.AppendLine($"خطأ في قراءة معلومات القرص: {ex.Message}");
                }
                
                report.AppendLine("=== نهاية التقرير ===");
                
                return report.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError("خطأ في إنشاء تقرير الصحة", ex);
                return $"خطأ في إنشاء تقرير الصحة: {ex.Message}";
            }
        }

        /// <summary>
        /// التحقق من وجود ملف صحة من جلسة سابقة
        /// </summary>
        public bool CheckForPreviousHealthFile()
        {
            try
            {
                if (File.Exists(_healthFile))
                {
                    var content = File.ReadAllText(_healthFile);
                    _logger.LogInformation($"تم العثور على ملف صحة من جلسة سابقة: {content}");
                    
                    // نقل الملف للأرشيف
                    var archiveFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                        $"health_archive_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                    File.Move(_healthFile, archiveFile);
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("خطأ في فحص ملف الصحة السابق", ex);
            }
            
            return false;
        }

        /// <summary>
        /// تنظيف الموارد
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    _healthCheckTimer?.Dispose();
                    UpdateHealthStatus("تم إيقاف نظام المراقبة");
                    
                    // حذف ملف الصحة عند الإغلاق الطبيعي
                    if (File.Exists(_healthFile))
                    {
                        File.Delete(_healthFile);
                    }
                    
                    _logger.LogInformation("تم إيقاف نظام مراقبة الصحة");
                }
                catch (Exception ex)
                {
                    _logger.LogError("خطأ في تنظيف نظام مراقبة الصحة", ex);
                }
                
                _disposed = true;
            }
        }
    }
}

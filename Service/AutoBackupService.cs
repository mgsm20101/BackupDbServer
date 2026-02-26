using BackupDbServer.Helpers;
using BackupDbServer.Models;
using System.IO;
using System.Text.Json;

namespace BackupDbServer.Services
{
    public class AutoBackupService : IAutoBackupService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly BackupRestoreService _backupService;
        private readonly IGoogleDriveService _googleDriveService;
        private System.Threading.Timer? _timer;
        private System.Threading.Timer? _googleDriveTimer; // مؤقت منفصل لرفع Google Drive
        private bool _isRunning;
        private bool _isBackupInProgress;
        private AutoBackupConfig _config = new();
        private readonly List<BackupScheduleEntry> _backupHistory = new();
        private readonly List<string> _pendingUploads = new(); // قائمة انتظار الرفع
        private readonly object _lockObject = new object();
        private System.Threading.Timer? _settingsMonitorTimer; // مؤقت مراقبة الإعدادات
        private DateTime _lastSettingsCheck = DateTime.MinValue;

        public bool IsRunning => _isRunning;

        public event EventHandler<BackupScheduleEntry>? BackupCompleted;
        public event EventHandler<string>? StatusChanged;

        public AutoBackupService(ILogger logger, BackupRestoreService backupService, IGoogleDriveService googleDriveService)
        {
            _logger = logger;
            _backupService = backupService;
            _googleDriveService = googleDriveService;
            
            // تحديد وقت آخر فحص للإعدادات
            _lastSettingsCheck = DateTime.Now;
            
            // تحميل الإعدادات
            LoadConfig();
            
            _logger.LogInformation($"تم إنشاء AutoBackupService - IsEnabled: {_config.IsEnabled}, UseScheduledTime: {_config.UseScheduledTime}");
        }

        public async Task StartAsync()
        {
            if (_isRunning) return;

            _logger.LogInformation("بدء خدمة النسخ الاحتياطي التلقائي");
            
            // إعادة تحميل الإعدادات للتأكد من أحدث البيانات
            LoadConfig();

            // تحديث قواعد البيانات المحددة من الإعدادات الرئيسية عند البدء
            UpdateSelectedDatabasesFromMainSettings();

            // بدء مراقبة تغييرات الإعدادات
            StartSettingsMonitoring();

            _isRunning = true;

            if (_config.IsEnabled)
            {
                _logger.LogInformation("النسخ التلقائي مفعل، جاري الجدولة...");
                await ScheduleNextBackup();
                await ScheduleNextGoogleDriveUpload(); // جدولة رفع Google Drive
                OnStatusChanged("تم تشغيل النسخ الاحتياطي التلقائي");
            }
            else
            {
                _logger.LogInformation("النسخ التلقائي غير مفعل");
                OnStatusChanged("خدمة النسخ الاحتياطي التلقائي جاهزة (غير مفعلة)");
            }
        }

        public Task StopAsync()
        {
            if (!_isRunning) return Task.CompletedTask;

            _logger.LogInformation("إيقاف خدمة النسخ الاحتياطي التلقائي");

            _timer?.Dispose();
            _googleDriveTimer?.Dispose(); // إيقاف مؤقت Google Drive أيضاً
            _timer = null;
            _isRunning = false;

            OnStatusChanged("تم إيقاف النسخ الاحتياطي التلقائي");
            
            return Task.CompletedTask;
        }

        public async Task<bool> RunBackupNowAsync()
        {
            if (_isBackupInProgress)
            {
                _logger.LogWarning("النسخ الاحتياطي قيد التنفيذ بالفعل");
                return false;
            }

            return await ExecuteBackupAsync();
        }

        private async Task<bool> ExecuteBackupAsync()
        {
            lock (_lockObject)
            {
                if (_isBackupInProgress) return false;
                _isBackupInProgress = true;
            }

            try
            {
                _logger.LogInformation("بدء تنفيذ النسخ الاحتياطي التلقائي");
                OnStatusChanged("جاري تنفيذ النسخ الاحتياطي...");

                // تحديث قائمة قواعد البيانات المحددة من الإعدادات الرئيسية
                UpdateSelectedDatabasesFromMainSettings();

                // التحقق من وجود قواعد بيانات محددة
                if (_config.SelectedDatabases == null || _config.SelectedDatabases.Count == 0)
                {
                    var errorMessage = "لا توجد قواعد بيانات محددة للنسخ الاحتياطي التلقائي";
                    _logger.LogWarning(errorMessage);
                    OnStatusChanged(errorMessage);
                    return false;
                }

                _logger.LogInformation($"سيتم نسخ {_config.SelectedDatabases.Count} قاعدة بيانات: {string.Join(", ", _config.SelectedDatabases)}");

                var backupTasks = new List<Task<BackupScheduleEntry>>();

                foreach (var dbName in _config.SelectedDatabases)
                {
                    backupTasks.Add(BackupDatabaseAsync(dbName));
                }

                var results = await Task.WhenAll(backupTasks);

                // إضافة النتائج إلى التاريخ
                foreach (var result in results)
                {
                    _backupHistory.Add(result);
                    BackupCompleted?.Invoke(this, result);
                }

                // تنظيف النسخ القديمة
                if (_config.DeleteOldBackups)
                {
                    await CleanupOldBackupsAsync();
                }

                // جدولة النسخ التالي
                await ScheduleNextBackup();

                var successCount = results.Count(r => r.Status == "Success");
                var message = $"اكتمل النسخ الاحتياطي: {successCount}/{results.Length} نجح";

                _logger.LogInformation(message);
                OnStatusChanged(message);

                return successCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في تنفيذ النسخ الاحتياطي التلقائي: {ex.Message}", ex);
                OnStatusChanged($"فشل النسخ الاحتياطي: {ex.Message}");
                return false;
            }
            finally
            {
                _isBackupInProgress = false;
            }
        }

        private async Task<BackupScheduleEntry> BackupDatabaseAsync(string databaseName)
        {
            var startTime = DateTime.Now;
            var entry = new BackupScheduleEntry
            {
                DatabaseName = databaseName,
                BackupTime = startTime,
                Status = "InProgress"
            };

            try
            {
                _logger.LogInformation($"بدء نسخ قاعدة البيانات: {databaseName}");

                // استخدام BackupRestoreService المحسن بدلاً من BackupExecutor
                var progress = new Progress<string>(message => 
                {
                    _logger.LogInformation($"[{databaseName}] {message}");
                });

                // تحديث إعدادات BackupRestoreService من الإعدادات الرئيسية
                var mainSettings = SettingsManager.LoadSettings();
                _backupService.UpdateSettings(mainSettings);

                // تنفيذ النسخ الاحتياطي باستخدام الخدمة المحسنة
                await _backupService.BackupDatabasesAsync(new List<string> { databaseName }, progress);

                // البحث عن الملف المنشئ
                var backupDirectory = GetBackupDirectory(mainSettings);
                var today = DateTime.Now.ToString("yyyyMMdd");
                var backupFiles = Directory.GetFiles(backupDirectory, $"{databaseName}_{today}*.zip")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(backupFiles) && File.Exists(backupFiles))
                {
                    entry.LocalFilePath = backupFiles;
                    entry.FileSizeBytes = new FileInfo(backupFiles).Length;
                    entry.Status = "Success";

                    _logger.LogInformation($"تم النسخ الاحتياطي بنجاح: {databaseName} -> {Path.GetFileName(backupFiles)}");

                    // رفع إلى Google Drive
                    if (_config.UploadToGoogleDrive)
                    {
                        if (_config.UseGoogleDriveScheduledTime)
                        {
                            // إضافة إلى قائمة انتظار الرفع المجدول
                            lock (_lockObject)
                            {
                                _pendingUploads.Add(entry.LocalFilePath);
                            }
                            _logger.LogInformation($"تم إضافة الملف إلى قائمة انتظار الرفع: {Path.GetFileName(entry.LocalFilePath)}");
                        }
                        else
                        {
                            // رفع فوري
                            await UploadToGoogleDriveAsync(entry);
                            
                            // حذف الملف المحلي إذا كان مطلوباً ونجح الرفع
                            if (_config.DeleteLocalAfterUpload && !string.IsNullOrEmpty(entry.GoogleDriveFileId))
                            {
                                File.Delete(entry.LocalFilePath);
                                entry.LocalFilePath = string.Empty;
                            }
                        }
                    }
                }
                else
                {
                    entry.Status = "Failed";
                    entry.ErrorMessage = "لم يتم العثور على ملف النسخة الاحتياطية المنشئ";
                    _logger.LogError($"فشل النسخ الاحتياطي: {databaseName} - لم يتم العثور على الملف");
                }
            }
            catch (Exception ex)
            {
                entry.Status = "Failed";
                entry.ErrorMessage = ex.Message;
                _logger.LogError($"فشل نسخ قاعدة البيانات {databaseName}: {ex.Message}", ex);
            }

            entry.Duration = DateTime.Now - startTime;
            return entry;
        }

        /// <summary>
        /// الحصول على مجلد النسخ الاحتياطي حسب نوع الاتصال
        /// </summary>
        private string GetBackupDirectory(BackupConfig settings)
        {
            if (!string.IsNullOrEmpty(settings.BackupPath) && Directory.Exists(settings.BackupPath))
            {
                return settings.BackupPath;
            }

            // المجلد الافتراضي حسب نوع الاتصال (نفس منطق BackupRestoreService)
            string defaultPath;
            switch (settings.ConnectionType)
            {
                case ConnectionType.Docker:
                    defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DockerBackups");
                    break;
                case ConnectionType.Local:
                    defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LocalBackups");
                    break;
                case ConnectionType.Remote:
                    defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RemoteBackups");
                    break;
                default:
                    defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SQLBackups");
                    break;
            }

            if (!Directory.Exists(defaultPath))
            {
                Directory.CreateDirectory(defaultPath);
            }

            return defaultPath;
        }

        private async Task UploadToGoogleDriveAsync(BackupScheduleEntry entry)
        {
            try
            {
                // تحديث مسار الاعتماد في خدمة Google Drive
                if (!string.IsNullOrEmpty(_config.GoogleDriveCredentialsPath))
                {
                    _googleDriveService.UpdateCredentialsPath(_config.GoogleDriveCredentialsPath);
                }

                if (!await _googleDriveService.IsAuthenticatedAsync())
                {
                    if (!await _googleDriveService.InitializeAsync(_config.GoogleDriveCredentialsPath))
                    {
                        entry.ErrorMessage = "فشل تهيئة Google Drive";
                        return;
                    }
                }

                // إنشاء مجلد إذا لم يكن موجوداً
                var folderId = await EnsureBackupFolderExists();

                if (string.IsNullOrEmpty(folderId))
                {
                    entry.ErrorMessage = "فشل إنشاء مجلد Google Drive";
                    return;
                }

                // رفع الملف
                var fileName = Path.GetFileName(entry.LocalFilePath);
                var fileId = await _googleDriveService.UploadFileAsync(entry.LocalFilePath, fileName, folderId);

                if (!string.IsNullOrEmpty(fileId))
                {
                    entry.GoogleDriveFileId = fileId;
                    _logger.LogInformation($"تم رفع الملف إلى Google Drive: {fileName}");
                }
                else
                {
                    entry.ErrorMessage = "فشل رفع الملف إلى Google Drive";
                }
            }
            catch (Exception ex)
            {
                entry.ErrorMessage = $"خطأ في رفع Google Drive: {ex.Message}";
                _logger.LogError($"فشل رفع الملف إلى Google Drive: {ex.Message}", ex);
            }
        }

        private async Task<string> EnsureBackupFolderExists()
        {
            // هذا تنفيذ مبسط، يمكنك تحسينه بحفظ معرف المجلد
            return await _googleDriveService.CreateFolderAsync(_config.GoogleDriveFolderName,_config.GoogleDriveCredentialsPath);
        }

        private async Task CleanupOldBackupsAsync()
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-_config.RetentionDays);
                var mainSettings = SettingsManager.LoadSettings();

                // الحصول على مجلد النسخ الاحتياطي الصحيح
                var backupDirectory = GetBackupDirectory(mainSettings);

                // تنظيف الملفات المحلية
                if (Directory.Exists(backupDirectory))
                {
                    var files = Directory.GetFiles(backupDirectory, "*.zip")
                        .Where(f => File.GetCreationTime(f) < cutoffDate);

                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                            _logger.LogInformation($"تم حذف الملف القديم: {Path.GetFileName(file)}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"فشل حذف الملف {file}: {ex.Message}");
                        }
                    }
                }

                // تنظيف الملفات من Google Drive
                if (_config.UploadToGoogleDrive)
                {
                    await CleanupGoogleDriveAsync(cutoffDate);
                }

                // تنظيف سجل التاريخ
                _backupHistory.RemoveAll(b => b.BackupTime < cutoffDate);
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في تنظيف النسخ القديمة: {ex.Message}", ex);
            }
        }

        private async Task CleanupGoogleDriveAsync(DateTime cutoffDate)
        {
            try
            {
                var folderId = await EnsureBackupFolderExists();
                if (string.IsNullOrEmpty(folderId)) return;

                var files = await _googleDriveService.GetFilesInFolderAsync(folderId);
                var oldFiles = files.Where(f => f.CreatedTimeDateTimeOffset < cutoffDate);

                foreach (var file in oldFiles)
                {
                    if (await _googleDriveService.DeleteFileAsync(file.Id))
                    {
                        _logger.LogInformation($"تم حذف الملف من Google Drive: {file.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في تنظيف Google Drive: {ex.Message}", ex);
            }
        }

        private Task ScheduleNextBackup()
        {
            if (!_config.IsEnabled) return Task.CompletedTask;

            DateTime nextBackupTime;

            _logger.LogInformation($"تشخيص الجدولة - UseScheduledTime: {_config.UseScheduledTime}, ScheduledTime: {_config.ScheduledTime}, RunOnlyOnce: {_config.RunOnlyOnce}");

            if (_config.UseScheduledTime)
            {
                // استخدام الوقت المحدد يومياً
                var today = DateTime.Today;
                var scheduledDateTime = today.Add(_config.ScheduledTime.ToTimeSpan());

                _logger.LogInformation($"اليوم: {today:yyyy-MM-dd}, الوقت المجدول اليوم: {scheduledDateTime:yyyy-MM-dd HH:mm:ss}, الوقت الحالي: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                if (scheduledDateTime <= DateTime.Now)
                {
                    // إذا كان الوقت المحدد قد مضى اليوم، حدد لغداً
                    if (_config.RunOnlyOnce)
                    {
                        // إذا كان التشغيل مرة واحدة فقط ومضى الوقت، لا تجدول
                        _logger.LogInformation("تم تخطي الجدولة: الوقت المحدد مضى والتشغيل مرة واحدة فقط");
                        return Task.CompletedTask;
                    }
                    nextBackupTime = today.AddDays(1).Add(_config.ScheduledTime.ToTimeSpan());
                    _logger.LogInformation($"الوقت المحدد مضى اليوم - سيتم النسخ غداً في: {nextBackupTime:yyyy-MM-dd HH:mm:ss}");
                }
                else
                {
                    nextBackupTime = scheduledDateTime;
                    _logger.LogInformation($"سيتم النسخ اليوم في: {nextBackupTime:yyyy-MM-dd HH:mm:ss}");
                }
            }
            else
            {
                // استخدام الفترة الزمنية العادية
                nextBackupTime = DateTime.Now.AddHours(_config.IntervalHours);
                _logger.LogInformation($"استخدام النظام القديم - سيتم النسخ خلال {_config.IntervalHours} ساعات في: {nextBackupTime:yyyy-MM-dd HH:mm:ss}");
            }

            _config.NextBackupTime = nextBackupTime;
            SaveConfig();

            var delay = nextBackupTime - DateTime.Now;
            if (delay.TotalMilliseconds > 0)
            {
                _timer?.Dispose();
                _timer = new System.Threading.Timer(async _ => await ExecuteBackupAsync(), null, delay, TimeSpan.FromMilliseconds(-1));

                _logger.LogInformation($"تم جدولة النسخ الاحتياطي التالي: {nextBackupTime:yyyy-MM-dd HH:mm:ss}");
                OnStatusChanged($"النسخ التالي: {nextBackupTime:yyyy-MM-dd HH:mm:ss}");
            }
            
            return Task.CompletedTask;
        }

        private Task ScheduleNextGoogleDriveUpload()
        {
            if (!_config.UploadToGoogleDrive || !_config.UseGoogleDriveScheduledTime) 
                return Task.CompletedTask;

            DateTime nextUploadTime;
            var today = DateTime.Today;
            var scheduledDateTime = today.Add(_config.GoogleDriveUploadTime.ToTimeSpan());

            if (scheduledDateTime <= DateTime.Now)
            {
                // إذا كان الوقت المحدد قد مضى اليوم، حدد لغداً
                nextUploadTime = today.AddDays(1).Add(_config.GoogleDriveUploadTime.ToTimeSpan());
            }
            else
            {
                nextUploadTime = scheduledDateTime;
            }

            var delay = nextUploadTime - DateTime.Now;
            if (delay.TotalMilliseconds > 0)
            {
                _googleDriveTimer?.Dispose();
                _googleDriveTimer = new System.Threading.Timer(async _ => await ProcessPendingUploadsAsync(), null, delay, TimeSpan.FromDays(1)); // يتكرر كل يوم

                _logger.LogInformation($"تم جدولة رفع Google Drive التالي: {nextUploadTime:yyyy-MM-dd HH:mm:ss}");
            }
            
            return Task.CompletedTask;
        }

        private async Task ProcessPendingUploadsAsync()
        {
            try
            {
                _logger.LogInformation("بدء معالجة الملفات المعلقة لرفع Google Drive");
                
                lock (_lockObject)
                {
                    if (_pendingUploads.Count == 0)
                    {
                        _logger.LogInformation("لا توجد ملفات معلقة للرفع");
                        return;
                    }
                }

                var uploads = new List<string>();
                lock (_lockObject)
                {
                    uploads.AddRange(_pendingUploads);
                }

                foreach (var filePath in uploads)
                {
                    if (File.Exists(filePath))
                    {
                        var entry = new BackupScheduleEntry
                        {
                            LocalFilePath = filePath,
                            BackupTime = DateTime.Now,
                            Status = "InProgress"
                        };

                        await UploadToGoogleDriveAsync(entry);

                        if (string.IsNullOrEmpty(entry.ErrorMessage))
                        {
                            lock (_lockObject)
                            {
                                _pendingUploads.Remove(filePath);
                            }
                            
                            // حذف الملف المحلي إذا كان مفعلاً
                            if (_config.DeleteLocalAfterUpload)
                            {
                                try
                                {
                                    File.Delete(filePath);
                                    _logger.LogInformation($"تم حذف الملف المحلي بعد الرفع: {filePath}");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError($"فشل حذف الملف المحلي {filePath}: {ex.Message}", ex);
                                }
                            }
                        }
                        else
                        {
                            _logger.LogError($"فشل رفع الملف {filePath}: {entry.ErrorMessage}");
                        }
                    }
                    else
                    {
                        lock (_lockObject)
                        {
                            _pendingUploads.Remove(filePath);
                        }
                        _logger.LogWarning($"الملف غير موجود وتم إزالته من قائمة الانتظار: {filePath}");
                    }
                }

                _logger.LogInformation("انتهت معالجة الملفات المعلقة لرفع Google Drive");
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في معالجة الملفات المعلقة: {ex.Message}", ex);
            }
        }

        public AutoBackupConfig GetConfig()
        {
            return _config;
        }

        public async Task UpdateConfigAsync(AutoBackupConfig config)
        {
            _logger.LogInformation("جاري تحديث إعدادات النسخ التلقائي...");
            
            // تحديث الإعدادات
            _config = config;
            
            // تحديث التاريخ التالي إذا كان يستخدم الوقت المحدد
            if (_config.UseScheduledTime)
            {
                _config.RefreshNextBackupTime();
                _logger.LogInformation($"تم تحديث التاريخ التالي للنسخ: {_config.NextBackupTime:yyyy-MM-dd HH:mm:ss}");
            }
            
            // حفظ الإعدادات الجديدة
            SaveConfig();

            if (_isRunning)
            {
                _logger.LogInformation("الخدمة تعمل، جاري إعادة الجدولة...");
                
                // إيقاف المؤقتات الحالية
                _timer?.Dispose();
                _googleDriveTimer?.Dispose();
                _timer = null;
                _googleDriveTimer = null;
                
                if (_config.IsEnabled)
                {
                    // إعادة جدولة كل من النسخ الاحتياطي ورفع Google Drive
                    await ScheduleNextBackup();
                    await ScheduleNextGoogleDriveUpload();
                    OnStatusChanged("تم تحديث جدولة النسخ الاحتياطي");
                }
                else
                {
                    OnStatusChanged("تم إيقاف النسخ الاحتياطي التلقائي");
                }
            }
            else
            {
                _logger.LogInformation("الخدمة غير متشغلة، سيتم تطبيق الإعدادات عند البدء");
            }
            
            _logger.LogInformation("تم تحديث الإعدادات بنجاح");
        }

        /// <summary>
        /// الحصول على الوقت المحفوظ كنص قابل للقراءة
        /// </summary>
        /// <returns></returns>
        public string GetScheduledTimeText()
        {
            return _config?.ScheduledTime.ToString("HH:mm") ?? "غير محدد";
        }

        /// <summary>
        /// التحقق من صحة إعدادات التشغيل
        /// </summary>
        /// <returns></returns>
        public bool ValidateConfiguration()
        {
            if (_config == null)
            {
                _logger.LogWarning("الإعدادات غير محملة");
                return false;
            }

            // التحقق من وجود قواعد البيانات المحددة
            if (_config.SelectedDatabases == null || !_config.SelectedDatabases.Any())
            {
                _logger.LogWarning("لا توجد قواعد بيانات محددة للنسخ الاحتياطي");
                return false;
            }

            // الحصول على مجلد الحفظ من الإعدادات العامة
            var settings = SettingsManager.LoadSettings();
            if (string.IsNullOrWhiteSpace(settings.BackupPath))
            {
                _logger.LogWarning("مجلد الحفظ غير محدد في الإعدادات العامة");
                return false;
            }

            try
            {
                if (!Directory.Exists(settings.BackupPath))
                {
                    Directory.CreateDirectory(settings.BackupPath);
                    _logger.LogInformation($"تم إنشاء مجلد الحفظ: {settings.BackupPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"فشل في إنشاء مجلد الحفظ {settings.BackupPath}: {ex.Message}");
                return false;
            }

            return true;
        }

        public List<BackupScheduleEntry> GetRecentBackups()
        {
            return _backupHistory.OrderByDescending(b => b.BackupTime).Take(50).ToList();
        }

        private void LoadConfig()
        {
            try
            {
                // استخدام المجلد المناسب للإعدادات (نفس مجلد الإعدادات الرئيسية)
                var configPath = GetConfigPath();
                
                _logger.LogInformation($"جاري تحميل الإعدادات من: {configPath}");
                
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    _logger.LogInformation($"محتوى ملف JSON: {json}");
                    
                    var options = new JsonSerializerOptions 
                    { 
                        WriteIndented = true,
                        PropertyNameCaseInsensitive = true, // إضافة هذا لتجاهل الأحرف الكبيرة/الصغيرة
                        Converters = { new TimeOnlyJsonConverter() }
                    };
                    
                    _config = JsonSerializer.Deserialize<AutoBackupConfig>(json, options) ?? new AutoBackupConfig();
                    
                    _logger.LogInformation("تم تحميل الإعدادات من الملف بنجاح");
                }
                else
                {
                    _logger.LogInformation("ملف الإعدادات غير موجود، سيتم استخدام الإعدادات الافتراضية");
                    _config = new AutoBackupConfig();
                    // حفظ الإعدادات الافتراضية
                    SaveConfig();
                }
                
                _logger.LogInformation($"تم تحميل الإعدادات:");
                _logger.LogInformation($"  - IsEnabled: {_config.IsEnabled}");
                _logger.LogInformation($"  - UseScheduledTime: {_config.UseScheduledTime}");
                _logger.LogInformation($"  - ScheduledTime: {_config.ScheduledTime}");
                _logger.LogInformation($"  - ScheduledTime (نص): {_config.ScheduledTime.ToString("HH:mm:ss")}");
                _logger.LogInformation($"  - IntervalHours: {_config.IntervalHours}");
                _logger.LogInformation($"  - SelectedDatabases: {(_config.SelectedDatabases?.Count ?? 0)} قاعدة");
                _logger.LogInformation($"  - NextBackupTime: {_config.NextBackupTime}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"فشل تحميل إعدادات النسخ التلقائي: {ex.Message}", ex);
                _config = new AutoBackupConfig();
            }
        }

        private void SaveConfig()
        {
            try
            {
                var configPath = GetConfigPath();
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = true,
                    Converters = { new TimeOnlyJsonConverter() }
                };
                var json = JsonSerializer.Serialize(_config, options);
                
                // التأكد من وجود المجلد
                var directory = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    _logger.LogInformation($"تم إنشاء مجلد الإعدادات: {directory}");
                }
                
                File.WriteAllText(configPath, json);
                
                _logger.LogInformation($"تم حفظ الإعدادات في: {configPath}");
                _logger.LogInformation($"محتوى JSON المحفوظ: {json}");
                _logger.LogInformation($"الإعدادات المحفوظة:");
                _logger.LogInformation($"  - IsEnabled: {_config.IsEnabled}");
                _logger.LogInformation($"  - UseScheduledTime: {_config.UseScheduledTime}");
                _logger.LogInformation($"  - ScheduledTime: {_config.ScheduledTime}");
                _logger.LogInformation($"  - ScheduledTime (نص): {_config.ScheduledTime.ToString("HH:mm:ss")}");
                _logger.LogInformation($"  - IntervalHours: {_config.IntervalHours}");
                _logger.LogInformation($"  - SelectedDatabases: {(_config.SelectedDatabases?.Count ?? 0)} قاعدة");
                _logger.LogInformation($"  - NextBackupTime: {_config.NextBackupTime}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"فشل حفظ إعدادات النسخ التلقائي: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// الحصول على مسار ملف إعدادات النسخ التلقائي
        /// يستخدم نفس مجلد الإعدادات الرئيسية
        /// </summary>
        private string GetConfigPath()
        {
            try
            {
                // استخدام نفس مجلد الإعدادات الرئيسية
                var settingsDirectory = SettingsManager.GetSettingsDirectory();
                return Path.Combine(settingsDirectory, "auto_backup_config.json");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"فشل في الحصول على مجلد الإعدادات: {ex.Message}");
                
                // استخدام مسار افتراضي في حالة الفشل
                var fallbackPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BackupDbServer",
                    "auto_backup_config.json"
                );
                
                _logger.LogInformation($"استخدام المسار الاحتياطي: {fallbackPath}");
                return fallbackPath;
            }
        }

        /// <summary>
        /// تحديث قائمة قواعد البيانات المحددة من الإعدادات الرئيسية
        /// </summary>
        private void UpdateSelectedDatabasesFromMainSettings()
        {
            try
            {
                // تحميل الإعدادات الرئيسية
                var mainSettings = SettingsManager.LoadSettings();
                
                if (mainSettings.SelectedDatabases != null && mainSettings.SelectedDatabases.Count > 0)
                {
                    // تحديث قائمة قواعد البيانات في إعدادات النسخ التلقائي
                    _config.SelectedDatabases = new List<string>(mainSettings.SelectedDatabases);
                    
                    // حفظ الإعدادات المحدثة
                    SaveConfig();
                    
                    _logger.LogInformation($"تم تحديث قواعد البيانات المحددة للنسخ التلقائي: {string.Join(", ", _config.SelectedDatabases)}");
                }
                else
                {
                    _logger.LogWarning("لا توجد قواعد بيانات محددة في الإعدادات الرئيسية");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في تحديث قواعد البيانات المحددة: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// بدء مراقبة تغييرات الإعدادات
        /// </summary>
        private void StartSettingsMonitoring()
        {
            try
            {
                // مراقبة الإعدادات كل دقيقة
                _settingsMonitorTimer = new System.Threading.Timer(
                    async _ => await CheckForSettingsChanges(),
                    null,
                    TimeSpan.FromMinutes(1),
                    TimeSpan.FromMinutes(1)
                );
                
                _logger.LogInformation("تم بدء مراقبة تغييرات الإعدادات");
            }
            catch (Exception ex)
            {
                _logger.LogError($"فشل بدء مراقبة الإعدادات: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// التحقق من تغييرات الإعدادات وإعادة التحميل
        /// </summary>
        private async Task CheckForSettingsChanges()
        {
            try
            {
                var configPath = GetConfigPath();
                var mainSettingsPath = SettingsManager.GetSettingsFilePath();
                
                // التحقق من تغييرات الإعدادات الرئيسية
                if (File.Exists(mainSettingsPath))
                {
                    var lastWriteTime = File.GetLastWriteTime(mainSettingsPath);
                    if (lastWriteTime > _lastSettingsCheck)
                    {
                        _logger.LogInformation("اكتشاف تغيير في الإعدادات الرئيسية، جاري إعادة التحميل...");
                        UpdateSelectedDatabasesFromMainSettings();
                        _lastSettingsCheck = lastWriteTime;
                    }
                }

                // التحقق من تغييرات إعدادات النسخ التلقائي
                if (File.Exists(configPath))
                {
                    var lastWriteTime = File.GetLastWriteTime(configPath);
                    if (lastWriteTime > _lastSettingsCheck)
                    {
                        _logger.LogInformation("اكتشاف تغيير في إعدادات النسخ التلقائي، جاري إعادة التحميل...");
                        
                        var oldConfig = _config;
                        LoadConfig();
                        
                        // إعادة جدولة إذا تغيرت الإعدادات
                        if (ConfigChanged(oldConfig, _config))
                        {
                            if (_config.IsEnabled && _isRunning)
                            {
                                await ScheduleNextBackup();
                                await ScheduleNextGoogleDriveUpload();
                            }
                        }
                        
                        _lastSettingsCheck = lastWriteTime;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في مراقبة الإعدادات: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// التحقق من تغيير الإعدادات
        /// </summary>
        private bool ConfigChanged(AutoBackupConfig oldConfig, AutoBackupConfig newConfig)
        {
            return oldConfig.IsEnabled != newConfig.IsEnabled ||
                   oldConfig.UseScheduledTime != newConfig.UseScheduledTime ||
                   oldConfig.ScheduledTime != newConfig.ScheduledTime ||
                   oldConfig.IntervalHours != newConfig.IntervalHours ||
                   oldConfig.UploadToGoogleDrive != newConfig.UploadToGoogleDrive ||
                   oldConfig.UseGoogleDriveScheduledTime != newConfig.UseGoogleDriveScheduledTime ||
                   oldConfig.GoogleDriveUploadTime != newConfig.GoogleDriveUploadTime;
        }

        private void OnStatusChanged(string status)
        {
            StatusChanged?.Invoke(this, status);
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _googleDriveTimer?.Dispose();
            _settingsMonitorTimer?.Dispose();
        }
    }
}

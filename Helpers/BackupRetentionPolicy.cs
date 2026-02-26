using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BackupDbServer.Helpers
{
    /// <summary>
    /// سياسة الاحتفاظ بالنسخ الاحتياطية (Backup Retention Policy)
    /// نمط Grandfather-Father-Son المُحسّن
    /// </summary>
    public class BackupRetentionPolicy
    {
        private readonly ILogger _logger;

        // إعدادات سياسة الاحتفاظ
        public class RetentionSettings
        {
            /// <summary>
            /// عدد الأيام للاحتفاظ بجميع النسخ اليومية (الافتراضي: 7 أيام)
            /// </summary>
            public int DailyRetentionDays { get; set; } = 7;

            /// <summary>
            /// عدد الأسابيع للاحتفاظ بنسخة أسبوعية (الافتراضي: 4 أسابيع)
            /// يتم الاحتفاظ بنسخة يوم الجمعة فقط
            /// </summary>
            public int WeeklyRetentionWeeks { get; set; } = 4;

            /// <summary>
            /// عدد الشهور للاحتفاظ بنسخة شهرية (الافتراضي: 12 شهر)
            /// يتم الاحتفاظ بنسخة أول يوم من كل شهر
            /// </summary>
            public int MonthlyRetentionMonths { get; set; } = 12;

            /// <summary>
            /// هل يتم حذف النسخ الأقدم من السنة؟ (الافتراضي: لا)
            /// </summary>
            public bool DeleteOlderThanYear { get; set; } = false;

            /// <summary>
            /// يوم الأسبوع للنسخ الأسبوعية (الافتراضي: الجمعة)
            /// </summary>
            public DayOfWeek WeeklyBackupDay { get; set; } = DayOfWeek.Friday;
        }

        public RetentionSettings Settings { get; set; }

        public BackupRetentionPolicy(ILogger logger, RetentionSettings? settings = null)
        {
            _logger = logger;
            Settings = settings ?? new RetentionSettings();
        }

        /// <summary>
        /// الحصول على مسار المجلد للنسخة الاحتياطية حسب التاريخ
        /// </summary>
        /// <param name="baseBackupPath">المسار الأساسي للنسخ</param>
        /// <param name="backupDate">تاريخ النسخة</param>
        /// <returns>المسار الكامل: BaseBackupPath\YYYY-MM-DD</returns>
        public string GetBackupFolderPath(string baseBackupPath, DateTime backupDate)
        {
            var folderName = backupDate.ToString("yyyy-MM-dd");
            return Path.Combine(baseBackupPath, folderName);
        }

        /// <summary>
        /// إنشاء مجلد النسخة الاحتياطية بالتاريخ (مع دعم مسارات الشبكة)
        /// </summary>
        public string CreateDatedBackupFolder(string baseBackupPath, DateTime backupDate)
        {
            var folderPath = GetBackupFolderPath(baseBackupPath, backupDate);
            
            // التحقق من نوع المسار
            var isNetwork = NetworkPathHelper.IsNetworkPath(baseBackupPath);
            if (isNetwork)
            {
                var pathInfo = NetworkPathHelper.GetNetworkPathInfo(baseBackupPath);
                _logger.LogInformation($"📡 {pathInfo.GetDescription()}");
            }
            
            if (!Directory.Exists(folderPath))
            {
                try
                {
                    Directory.CreateDirectory(folderPath);
                    _logger.LogInformation($"✅ تم إنشاء مجلد النسخة الاحتياطية: {folderPath}");
                }
                catch (IOException ex) when (isNetwork)
                {
                    _logger.LogError($"❌ فشل إنشاء المجلد على الشبكة: {ex.Message}", ex);
                    throw new InvalidOperationException($"تعذر الوصول إلى مسار الشبكة: {baseBackupPath}", ex);
                }
            }

            return folderPath;
        }

        /// <summary>
        /// تطبيق سياسة الاحتفاظ - حذف النسخ القديمة حسب السياسة (مع دعم مسارات الشبكة)
        /// </summary>
        /// <param name="baseBackupPath">المسار الأساسي للنسخ</param>
        /// <returns>عدد الملفات المحذوفة</returns>
        public async Task<int> ApplyRetentionPolicy(string baseBackupPath)
        {
            try
            {
                // التحقق من توفر مسار الشبكة
                var isNetwork = NetworkPathHelper.IsNetworkPath(baseBackupPath);
                if (isNetwork)
                {
                    _logger.LogInformation("🔍 التحقق من توفر مسار الشبكة...");
                    var isAvailable = await NetworkPathHelper.IsNetworkPathAvailableAsync(baseBackupPath, timeoutSeconds: 10);
                    
                    if (!isAvailable)
                    {
                        _logger.LogWarning($"⚠️ مسار الشبكة غير متاح حالياً: {baseBackupPath}");
                        return 0;
                    }
                    
                    _logger.LogInformation("✅ مسار الشبكة متاح");
                }
                
                if (!Directory.Exists(baseBackupPath))
                {
                    _logger.LogWarning($"مسار النسخ الاحتياطية غير موجود: {baseBackupPath}");
                    return 0;
                }

                _logger.LogInformation("🗂️ بدء تطبيق سياسة الاحتفاظ بالنسخ الاحتياطية...");

                var now = DateTime.Now;
                var deletedCount = 0;

                // الحصول على جميع مجلدات النسخ الاحتياطية (بصيغة YYYY-MM-DD)
                var backupFolders = GetBackupFolders(baseBackupPath);

                if (backupFolders.Count == 0)
                {
                    _logger.LogInformation("لا توجد مجلدات نسخ احتياطية للفحص");
                    return 0;
                }

                _logger.LogInformation($"تم العثور على {backupFolders.Count} مجلد نسخ احتياطي");

                // تصنيف المجلدات حسب العمر
                foreach (var folder in backupFolders)
                {
                    var folderDate = ParseFolderDate(folder.Name);
                    if (!folderDate.HasValue)
                    {
                        _logger.LogWarning($"تخطي مجلد بصيغة غير صحيحة: {folder.Name}");
                        continue;
                    }

                    var age = now - folderDate.Value;
                    var shouldDelete = ShouldDeleteBackup(folderDate.Value, now);

                    if (shouldDelete)
                    {
                        try
                        {
                            // استخدام Retry Logic لمسارات الشبكة
                            if (isNetwork)
                            {
                                await NetworkPathHelper.ExecuteWithRetryAsync(
                                    async () => 
                                    {
                                        await Task.Run(() => Directory.Delete(folder.FullName, recursive: true));
                                        return true;
                                    },
                                    maxRetries: 3,
                                    delayMilliseconds: 2000,
                                    logger: _logger
                                );
                                _logger.LogInformation($"✅ تم حذف نسخة قديمة (شبكة): {folder.Name} (عمرها {age.Days} يوم)");
                            }
                            else
                            {
                                Directory.Delete(folder.FullName, recursive: true);
                                _logger.LogInformation($"✅ تم حذف نسخة قديمة: {folder.Name} (عمرها {age.Days} يوم)");
                            }
                            
                            deletedCount++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"❌ فشل حذف المجلد {folder.Name}: {ex.Message}", ex);
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"✓ الاحتفاظ بالنسخة: {folder.Name} (عمرها {age.Days} يوم)");
                    }
                }

                _logger.LogInformation($"✅ انتهى تطبيق سياسة الاحتفاظ - تم حذف {deletedCount} مجلد");
                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في تطبيق سياسة الاحتفاظ: {ex.Message}", ex);
                return 0;
            }
        }

        /// <summary>
        /// الحصول على آخر جمعة من الشهر
        /// </summary>
        private DateTime GetLastFridayOfMonth(DateTime date)
        {
            // الحصول على آخر يوم في الشهر
            var lastDayOfMonth = new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
            
            // إذا كان آخر يوم هو جمعة، نرجعه مباشرة
            if (lastDayOfMonth.DayOfWeek == DayOfWeek.Friday)
                return lastDayOfMonth;
            
            // نرجع للخلف حتى نجد آخر جمعة
            int daysToSubtract = ((int)lastDayOfMonth.DayOfWeek - (int)DayOfWeek.Friday + 7) % 7;
            return lastDayOfMonth.AddDays(-daysToSubtract);
        }

        /// <summary>
        /// تحديد ما إذا كان يجب حذف النسخة الاحتياطية
        /// </summary>
        private bool ShouldDeleteBackup(DateTime backupDate, DateTime now)
        {
            var age = now - backupDate;

            // 1. آخر X أيام: الاحتفاظ بجميع النسخ اليومية
            if (age.TotalDays <= Settings.DailyRetentionDays)
            {
                return false; // الاحتفاظ
            }

            // 2. من (X+1) إلى (X + WeeklyRetentionWeeks * 7) يوم: الاحتفاظ بنسخة الجمعة فقط
            var weeklyRetentionEndDays = Settings.DailyRetentionDays + (Settings.WeeklyRetentionWeeks * 7);
            if (age.TotalDays <= weeklyRetentionEndDays)
            {
                // الاحتفاظ فقط بنسخة يوم الجمعة (أو اليوم المحدد)
                return backupDate.DayOfWeek != Settings.WeeklyBackupDay;
            }

            // 3. من نهاية الفترة الأسبوعية إلى (MonthlyRetentionMonths) شهر: الاحتفاظ بآخر جمعة من كل شهر
            var monthlyRetentionEndDate = now.AddMonths(-Settings.MonthlyRetentionMonths);
            if (backupDate >= monthlyRetentionEndDate)
            {
                // الاحتفاظ فقط بآخر جمعة من كل شهر
                var lastFriday = GetLastFridayOfMonth(backupDate);
                return backupDate.Date != lastFriday.Date;
            }

            // 4. أقدم من (MonthlyRetentionMonths) شهر
            if (Settings.DeleteOlderThanYear)
            {
                return true; // حذف
            }
            else
            {
                // الاحتفاظ بآخر جمعة من أول شهر كل ربع سنة (يناير، أبريل، يوليو، أكتوبر)
                if (backupDate.Month % 3 != 1)
                    return true; // ليس شهر ربع سنوي
                
                var lastFriday = GetLastFridayOfMonth(backupDate);
                return backupDate.Date != lastFriday.Date;
            }
        }

        /// <summary>
        /// الحصول على جميع مجلدات النسخ الاحتياطية
        /// </summary>
        private List<DirectoryInfo> GetBackupFolders(string baseBackupPath)
        {
            var directory = new DirectoryInfo(baseBackupPath);
            return directory.GetDirectories("????-??-??", SearchOption.TopDirectoryOnly)
                           .OrderByDescending(d => d.Name)
                           .ToList();
        }

        /// <summary>
        /// تحليل تاريخ المجلد من اسمه (YYYY-MM-DD)
        /// </summary>
        private DateTime? ParseFolderDate(string folderName)
        {
            if (DateTime.TryParseExact(folderName, "yyyy-MM-dd", 
                System.Globalization.CultureInfo.InvariantCulture, 
                System.Globalization.DateTimeStyles.None, 
                out var date))
            {
                return date;
            }
            return null;
        }

        /// <summary>
        /// الحصول على معلومات الاحتفاظ - للعرض في الواجهة
        /// </summary>
        public string GetRetentionSummary()
        {
            return $@"سياسة الاحتفاظ الحالية:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📅 النسخ اليومية: آخر {Settings.DailyRetentionDays} أيام (جميع النسخ)
📆 النسخ الأسبوعية: {Settings.WeeklyRetentionWeeks} أسابيع (يوم {GetArabicDayName(Settings.WeeklyBackupDay)})
📊 النسخ الشهرية: {Settings.MonthlyRetentionMonths} شهر (آخر جمعة من كل شهر)
🗓️ النسخ الربع سنوية: أقدم من سنة (آخر جمعة من كل ربع)
🗑️ الحذف التلقائي للنسخ الأقدم من سنة: {(Settings.DeleteOlderThanYear ? "مفعّل" : "معطّل")}
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";
        }

        /// <summary>
        /// الحصول على إحصائيات النسخ الاحتياطية
        /// </summary>
        public BackupStatistics GetBackupStatistics(string baseBackupPath)
        {
            var stats = new BackupStatistics();

            if (!Directory.Exists(baseBackupPath))
                return stats;

            var backupFolders = GetBackupFolders(baseBackupPath);
            var now = DateTime.Now;

            foreach (var folder in backupFolders)
            {
                var folderDate = ParseFolderDate(folder.Name);
                if (!folderDate.HasValue) continue;

                var age = now - folderDate.Value;

                // حساب حجم المجلد
                var folderSize = GetDirectorySize(folder.FullName);
                stats.TotalSize += folderSize;
                stats.TotalBackups++;

                // تصنيف حسب العمر
                if (age.TotalDays <= Settings.DailyRetentionDays)
                {
                    stats.DailyBackups++;
                    stats.DailyBackupsSize += folderSize;
                }
                else if (age.TotalDays <= Settings.DailyRetentionDays + (Settings.WeeklyRetentionWeeks * 7))
                {
                    stats.WeeklyBackups++;
                    stats.WeeklyBackupsSize += folderSize;
                }
                else if (folderDate.Value >= now.AddMonths(-Settings.MonthlyRetentionMonths))
                {
                    stats.MonthlyBackups++;
                    stats.MonthlyBackupsSize += folderSize;
                }
                else
                {
                    stats.OlderBackups++;
                    stats.OlderBackupsSize += folderSize;
                }
            }

            return stats;
        }

        /// <summary>
        /// حساب حجم المجلد بالبايت
        /// </summary>
        private long GetDirectorySize(string path)
        {
            try
            {
                var directory = new DirectoryInfo(path);
                return directory.GetFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// الحصول على اسم اليوم بالعربية
        /// </summary>
        private string GetArabicDayName(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Saturday => "السبت",
                DayOfWeek.Sunday => "الأحد",
                DayOfWeek.Monday => "الاثنين",
                DayOfWeek.Tuesday => "الثلاثاء",
                DayOfWeek.Wednesday => "الأربعاء",
                DayOfWeek.Thursday => "الخميس",
                DayOfWeek.Friday => "الجمعة",
                _ => day.ToString()
            };
        }

        /// <summary>
        /// إحصائيات النسخ الاحتياطية
        /// </summary>
        public class BackupStatistics
        {
            public int TotalBackups { get; set; }
            public long TotalSize { get; set; }
            
            public int DailyBackups { get; set; }
            public long DailyBackupsSize { get; set; }
            
            public int WeeklyBackups { get; set; }
            public long WeeklyBackupsSize { get; set; }
            
            public int MonthlyBackups { get; set; }
            public long MonthlyBackupsSize { get; set; }
            
            public int OlderBackups { get; set; }
            public long OlderBackupsSize { get; set; }

            public string GetSummary()
            {
                return $@"📊 إحصائيات النسخ الاحتياطية:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📦 إجمالي النسخ: {TotalBackups} نسخة ({FormatSize(TotalSize)})

📅 نسخ يومية: {DailyBackups} ({FormatSize(DailyBackupsSize)})
📆 نسخ أسبوعية: {WeeklyBackups} ({FormatSize(WeeklyBackupsSize)})
📊 نسخ شهرية: {MonthlyBackups} ({FormatSize(MonthlyBackupsSize)})
📁 نسخ قديمة: {OlderBackups} ({FormatSize(OlderBackupsSize)})
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";
            }

            private string FormatSize(long bytes)
            {
                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                double len = bytes;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }
                return $"{len:0.##} {sizes[order]}";
            }
        }
    }
}

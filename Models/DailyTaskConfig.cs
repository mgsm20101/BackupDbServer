using System;
using System.Collections.Generic;

namespace BackupDbServer.Models
{
    /// <summary>
    /// إعدادات المهمة اليومية
    /// </summary>
    public class DailyTaskConfig
    {
        public string TaskId { get; set; } = Guid.NewGuid().ToString();
        public string TaskName { get; set; } = "مهمة جديدة";
        public bool IsEnabled { get; set; } = true;
        
        // إعدادات قاعدة البيانات (يستخدم إعدادات البرنامج للاتصال)
        public string DatabaseName { get; set; } = "";
        
        // إعدادات التنفيذ
        public TimeOnly ExecutionTime { get; set; } = new TimeOnly(2, 0); // 2:00 صباحاً افتراضياً

        // نمط الجدولة: يومياً في وقت محدد، أو تكرار كل فترة زمنية
        public ScheduleMode Mode { get; set; } = ScheduleMode.Daily;
        public int IntervalMinutes { get; set; } = 60; // يُستخدم فقط في نمط Interval

        public TaskType Type { get; set; } = TaskType.SqlScript;
        
        // محتوى المهمة
        public string SqlScript { get; set; } = "";
        public string StoredProcedureName { get; set; } = "";
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        
        // إعدادات إضافية
        public bool SendEmailOnSuccess { get; set; } = false;
        public bool SendEmailOnFailure { get; set; } = true;
        public string EmailTo { get; set; } = "";
        
        public DateTime? LastExecutionTime { get; set; }
        public bool LastExecutionSuccess { get; set; }
        public string LastExecutionMessage { get; set; } = "";
        
        /// <summary>
        /// حساب الوقت المتبقي للتنفيذ التالي
        /// </summary>
        public string TimeUntilNextRun
        {
            get
            {
                if (!IsEnabled)
                    return "غير مفعل";

                var now = DateTime.Now;

                if (Mode == ScheduleMode.Interval)
                {
                    // لم يُنفّذ بعد → سيعمل في الفحص التالي (متوافق مع منطق الخدمة)
                    if (LastExecutionTime == null)
                        return "الآن";

                    // الوقت التالي = آخر تنفيذ + الفترة
                    var interval = Math.Max(1, IntervalMinutes);
                    var nextRun = LastExecutionTime.Value.AddMinutes(interval);
                    if (nextRun <= now)
                        return "الآن";

                    var remaining = nextRun - now;
                    if (remaining.TotalHours >= 1)
                        return $"{remaining.Hours:D2}:{remaining.Minutes:D2} ساعة";

                    return $"{remaining.Minutes} دقيقة";
                }

                var targetTime = new DateTime(now.Year, now.Month, now.Day, ExecutionTime.Hour, ExecutionTime.Minute, 0);

                // إذا فات الوقت اليوم، احسب للغد
                if (targetTime <= now)
                {
                    targetTime = targetTime.AddDays(1);
                }

                var timeUntil = targetTime - now;

                if (timeUntil.TotalDays >= 1)
                {
                    return $"غداً {ExecutionTime:HH:mm}";
                }
                else if (timeUntil.TotalHours >= 1)
                {
                    return $"{timeUntil.Hours:D2}:{timeUntil.Minutes:D2} ساعة";
                }
                else
                {
                    return $"{timeUntil.Minutes} دقيقة";
                }
            }
        }

        /// <summary>
        /// وصف مقروء لنمط الجدولة (للعرض في جدول الإدارة)
        /// </summary>
        public string ScheduleDescription
        {
            get
            {
                if (Mode == ScheduleMode.Daily)
                    return $"يومياً {ExecutionTime:HH:mm}";

                var minutes = Math.Max(1, IntervalMinutes);
                if (minutes % 60 == 0)
                {
                    var hours = minutes / 60;
                    if (hours == 1) return "كل ساعة";
                    if (hours == 2) return "كل ساعتين";
                    // 3-10 → جمع (ساعات)، 11 فأكثر → مفرد التمييز (ساعة)
                    return hours <= 10 ? $"كل {hours} ساعات" : $"كل {hours} ساعة";
                }

                return $"كل {minutes} دقيقة";
            }
        }
    }

    public enum ScheduleMode
    {
        Daily,      // مرة واحدة يومياً في وقت محدد
        Interval    // تكرار كل فترة زمنية (بالدقائق)
    }

    public enum TaskType
    {
        SqlScript,           // تنفيذ نص SQL
        StoredProcedure,     // تنفيذ إجراء مخزن
        BackupDatabase,      // نسخ احتياطي للقاعدة
        CheckIntegrity,      // فحص سلامة القاعدة
        UpdateStatistics,    // تحديث الإحصائيات
        RebuildIndexes,      // إعادة بناء الفهارس
        ShrinkDatabase,      // ضغط القاعدة
        Custom               // مهمة مخصصة
    }
}
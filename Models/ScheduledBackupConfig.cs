using System;
using System.Collections.Generic;

namespace BackupDbServer.Models
{
    public class ScheduledBackupConfig
    {
        public bool IsEnabled { get; set; } = false;
        public TimeOnly ScheduledTime { get; set; } = new TimeOnly(2, 0); // 02:00 صباحاً افتراضياً
        public List<DayOfWeek> ScheduledDays { get; set; } = new()
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
        };
        public bool DeleteOldBackups { get; set; } = true;
        public int RetentionDays { get; set; } = 7;
        public int SelectedMinute { get; set; } = 0;
        public int SelectedHour { get; set; } = 0;
        public bool NotifyOnCompletion { get; set; } = true;
        public bool NotifyOnError { get; set; } = true;
        public string NotificationEmail { get; set; } = string.Empty;
        public DateTime? LastBackupTime { get; set; }
        public DateTime? NextBackupTime { get; set; }
        public bool IsRuning { get;  set; }
    }
}

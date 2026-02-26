using BackupDbServer.Models;
using System;
using System.Threading.Tasks;

namespace BackupDbServer.Services
{
    public interface IScheduledBackupService
    {
        bool IsRunning { get; }
        ScheduledBackupConfig Config { get; }
        event EventHandler<string>? StatusChanged;
        event EventHandler<ScheduledBackupResult>? BackupCompleted;
        
        Task StartAsync();
        Task StopAsync();
        Task UpdateConfigAsync(ScheduledBackupConfig config);
        Task SaveConfigAsync();
        Task<ScheduledBackupResult> RunBackupNowAsync();
        DateTime? GetNextBackupTime();
    }

    public class ScheduledBackupResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsSuccess { get; set; }
        public bool IsScheduled { get; set; } = true;
        public string ErrorMessage { get; set; } = string.Empty;
        public List<string> BackedUpDatabases { get; set; } = new();
        public List<string> FailedDatabases { get; set; } = new();
        public TimeSpan Duration => EndTime - StartTime;
    }
}
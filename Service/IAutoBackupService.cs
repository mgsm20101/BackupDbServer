using BackupDbServer.Models;

namespace BackupDbServer.Services
{
    public interface IAutoBackupService
    {
        Task StartAsync();
        Task StopAsync();
        Task<bool> RunBackupNowAsync();
        bool IsRunning { get; }
        AutoBackupConfig GetConfig();
        Task UpdateConfigAsync(AutoBackupConfig config);
        event EventHandler<BackupScheduleEntry>? BackupCompleted;
        event EventHandler<string>? StatusChanged;
        List<BackupScheduleEntry> GetRecentBackups();
    }
}

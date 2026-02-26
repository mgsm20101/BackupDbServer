using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using BackupDbServer.Helpers;
using BackupDbServer.Models;

namespace BackupDbServer.Service
{
    public class DailyTaskService : IDisposable
    {
        private readonly ILogger _logger;
        private readonly List<DailyTaskConfig> _tasks;
        private readonly System.Timers.Timer _checkTimer;
        private readonly string _configPath;
        private readonly HashSet<string> _executedToday;
        private CancellationTokenSource? _cancellationTokenSource;

        public DailyTaskService(ILogger logger)
        {
            _logger = logger;
            _tasks = new List<DailyTaskConfig>();
            _executedToday = new HashSet<string>();
            _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "daily_tasks.json");
            
            // تحميل المهام من الملف
            LoadTasks();
            
            // إعداد مؤقت للفحص كل دقيقة
            _checkTimer = new System.Timers.Timer(60000); // كل دقيقة
            _checkTimer.Elapsed += OnTimerElapsed;
            _checkTimer.AutoReset = true;
        }

        public void Start()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _checkTimer.Start();
            _logger.LogInformation("خدمة المهام اليومية بدأت");
        }

        public void Stop()
        {
            _checkTimer.Stop();
            _cancellationTokenSource?.Cancel();
            _logger.LogInformation("خدمة المهام اليومية توقفت");
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                var now = DateTime.Now;
                var currentDate = now.Date;
                
                // مسح قائمة المهام المنفذة إذا تغير اليوم
                if (_executedToday.Any() && _executedToday.First().StartsWith(now.AddDays(-1).ToString("yyyy-MM-dd")))
                {
                    _executedToday.Clear();
                }

                foreach (var task in _tasks.Where(t => t.IsEnabled))
                {
                    var taskKey = $"{currentDate:yyyy-MM-dd}_{task.TaskId}";
                    
                    // تحقق إذا تم تنفيذ المهمة اليوم
                    if (_executedToday.Contains(taskKey))
                        continue;

                    // تحقق من وقت التنفيذ
                    var executionTime = task.ExecutionTime;
                    var targetTime = new DateTime(now.Year, now.Month, now.Day, executionTime.Hour, executionTime.Minute, 0);
                    
                    // إذا حان وقت التنفيذ (مع هامش دقيقة واحدة)
                    if (Math.Abs((now - targetTime).TotalMinutes) <= 1)
                    {
                        _executedToday.Add(taskKey);
                        _ = ExecuteTaskAsync(task);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في فحص المهام اليومية: {ex.Message}", ex);
            }
        }

        public async Task ExecuteTaskAsync(DailyTaskConfig task)
        {
            try
            {
                _logger.LogInformation($"بدء تنفيذ المهمة: {task.TaskName}");
                
                var startTime = DateTime.Now;
                string result = "";

                switch (task.Type)
                {
                    case TaskType.SqlScript:
                        result = await ExecuteSqlScriptAsync(task);
                        break;
                    case TaskType.StoredProcedure:
                        result = await ExecuteStoredProcedureAsync(task);
                        break;
                    case TaskType.BackupDatabase:
                        result = await BackupDatabaseAsync(task);
                        break;
                    case TaskType.CheckIntegrity:
                        result = await CheckIntegrityAsync(task);
                        break;
                    case TaskType.UpdateStatistics:
                        result = await UpdateStatisticsAsync(task);
                        break;
                    case TaskType.RebuildIndexes:
                        result = await RebuildIndexesAsync(task);
                        break;
                    case TaskType.ShrinkDatabase:
                        result = await ShrinkDatabaseAsync(task);
                        break;
                    default:
                        result = "نوع المهمة غير مدعوم";
                        break;
                }

                task.LastExecutionTime = startTime;
                task.LastExecutionSuccess = true;
                task.LastExecutionMessage = result;
                
                _logger.LogInformation($"تم تنفيذ المهمة بنجاح: {task.TaskName} - {result}");
                SaveTasks();
            }
            catch (Exception ex)
            {
                task.LastExecutionTime = DateTime.Now;
                task.LastExecutionSuccess = false;
                task.LastExecutionMessage = ex.Message;
                
                _logger.LogError($"فشل تنفيذ المهمة: {task.TaskName} - {ex.Message}", ex);
                SaveTasks();
            }
        }

        private async Task<string> ExecuteSqlScriptAsync(DailyTaskConfig task)
        {
            using var connection = new SqlConnection(GetConnectionString(task));
            await connection.OpenAsync();
            
            using var command = new SqlCommand(task.SqlScript, connection);
            command.CommandTimeout = 300; // 5 دقائق
            
            var rowsAffected = await command.ExecuteNonQueryAsync();
            return $"تم التنفيذ بنجاح - صفوف متأثرة: {rowsAffected}";
        }

        private async Task<string> ExecuteStoredProcedureAsync(DailyTaskConfig task)
        {
            using var connection = new SqlConnection(GetConnectionString(task));
            await connection.OpenAsync();
            
            using var command = new SqlCommand(task.StoredProcedureName, connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.CommandTimeout = 300;
            
            foreach (var param in task.Parameters)
            {
                command.Parameters.AddWithValue(param.Key, param.Value);
            }
            
            var result = await command.ExecuteScalarAsync();
            return $"تم التنفيذ بنجاح - النتيجة: {result}";
        }

        private async Task<string> BackupDatabaseAsync(DailyTaskConfig task)
        {
            var backupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DailyBackups", 
                $"{task.DatabaseName}_{DateTime.Now:yyyyMMdd_HHmmss}.bak");
            
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
            
            using var connection = new SqlConnection(GetConnectionString(task));
            await connection.OpenAsync();
            
            var sql = $"BACKUP DATABASE [{task.DatabaseName}] TO DISK = @backupPath WITH FORMAT, INIT, NAME = 'Daily Backup';";
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@backupPath", backupPath);
            command.CommandTimeout = 0;
            
            await command.ExecuteNonQueryAsync();
            return $"تم النسخ الاحتياطي إلى: {backupPath}";
        }

        private async Task<string> CheckIntegrityAsync(DailyTaskConfig task)
        {
            using var connection = new SqlConnection(GetConnectionString(task));
            await connection.OpenAsync();
            
            var sql = $"DBCC CHECKDB([{task.DatabaseName}]) WITH NO_INFOMSGS;";
            using var command = new SqlCommand(sql, connection);
            command.CommandTimeout = 0;
            
            await command.ExecuteNonQueryAsync();
            return "فحص سلامة القاعدة تم بنجاح";
        }

        private async Task<string> UpdateStatisticsAsync(DailyTaskConfig task)
        {
            using var connection = new SqlConnection(GetConnectionString(task));
            await connection.OpenAsync();
            
            var sql = $"USE [{task.DatabaseName}]; EXEC sp_updatestats;";
            using var command = new SqlCommand(sql, connection);
            command.CommandTimeout = 0;
            
            await command.ExecuteNonQueryAsync();
            return "تم تحديث الإحصائيات بنجاح";
        }

        private async Task<string> RebuildIndexesAsync(DailyTaskConfig task)
        {
            using var connection = new SqlConnection(GetConnectionString(task));
            await connection.OpenAsync();
            
            var sql = $@"
                USE [{task.DatabaseName}];
                DECLARE @TableName NVARCHAR(255);
                DECLARE TableCursor CURSOR FOR
                SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';
                
                OPEN TableCursor;
                FETCH NEXT FROM TableCursor INTO @TableName;
                
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    EXEC('ALTER INDEX ALL ON [' + @TableName + '] REBUILD');
                    FETCH NEXT FROM TableCursor INTO @TableName;
                END
                
                CLOSE TableCursor;
                DEALLOCATE TableCursor;
            ";
            
            using var command = new SqlCommand(sql, connection);
            command.CommandTimeout = 0;
            
            await command.ExecuteNonQueryAsync();
            return "تم إعادة بناء الفهارس بنجاح";
        }

        private async Task<string> ShrinkDatabaseAsync(DailyTaskConfig task)
        {
            using var connection = new SqlConnection(GetConnectionString(task));
            await connection.OpenAsync();
            
            var sql = $"DBCC SHRINKDATABASE([{task.DatabaseName}], 10);";
            using var command = new SqlCommand(sql, connection);
            command.CommandTimeout = 0;
            
            await command.ExecuteNonQueryAsync();
            return "تم ضغط القاعدة بنجاح";
        }

        private string GetConnectionString(DailyTaskConfig task)
        {
            var builder = new SqlConnectionStringBuilder();
            
            try
            {
                // استخدام إعدادات الاتصال من البرنامج مع قاعدة البيانات المحددة في المهمة
                var config = Helpers.SettingsManager.LoadSettings();
                
                // تحديد DataSource بناءً على نوع الاتصال (نفس منطق الصفحة الرئيسية)
                switch (config.ConnectionType)
                {
                    case Models.ConnectionType.Local:
                        builder.DataSource = config.Port != 1433 ? $"{config.Server},{config.Port}" : (config.Server ?? "localhost");
                        break;
                    case Models.ConnectionType.Docker:
                        builder.DataSource = config.Port != 1433 ? $"localhost,{config.Port}" : "localhost,1433";
                        break;
                    case Models.ConnectionType.Remote:
                        builder.DataSource = config.Port != 1433 ? $"{config.Server},{config.Port}" : (config.Server ?? "localhost");
                        break;
                    default:
                        builder.DataSource = config.Server ?? "localhost";
                        break;
                }
                
                builder.InitialCatalog = task.DatabaseName;
                builder.IntegratedSecurity = config.UseWindowsAuthentication;
                builder.ConnectTimeout = config.ConnectionTimeout;
                builder.TrustServerCertificate = config.TrustServerCertificate;
                builder.Encrypt = config.Encrypt;
                
                if (!config.UseWindowsAuthentication)
                {
                    builder.UserID = config.Username;
                    builder.Password = config.Password;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"فشل تحميل إعدادات الاتصال: {ex.Message}", ex);
                throw;
            }
            
            builder.TrustServerCertificate = true;
            builder.ConnectTimeout = 30;

            return builder.ToString();
        }

        public void AddTask(DailyTaskConfig task)
        {
            _tasks.Add(task);
            SaveTasks();
            _logger.LogInformation($"تمت إضافة المهمة: {task.TaskName}");
        }

        public void UpdateTask(DailyTaskConfig task)
        {
            var existing = _tasks.FirstOrDefault(t => t.TaskId == task.TaskId);
            if (existing != null)
            {
                _tasks.Remove(existing);
                _tasks.Add(task);
                SaveTasks();
                _logger.LogInformation($"تم تحديث المهمة: {task.TaskName}");
            }
        }

        public void DeleteTask(string taskId)
        {
            var task = _tasks.FirstOrDefault(t => t.TaskId == taskId);
            if (task != null)
            {
                _tasks.Remove(task);
                SaveTasks();
                _logger.LogInformation($"تم حذف المهمة: {task.TaskName}");
            }
        }

        public List<DailyTaskConfig> GetAllTasks()
        {
            return new List<DailyTaskConfig>(_tasks);
        }

        private void LoadTasks()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var tasks = JsonSerializer.Deserialize<List<DailyTaskConfig>>(json);
                    if (tasks != null)
                    {
                        _tasks.Clear();
                        _tasks.AddRange(tasks);
                        _logger.LogInformation($"تم تحميل {_tasks.Count} مهمة يومية");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في تحميل المهام اليومية: {ex.Message}", ex);
            }
        }

        private void SaveTasks()
        {
            try
            {
                var json = JsonSerializer.Serialize(_tasks, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في حفظ المهام اليومية: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            _checkTimer?.Stop();
            _checkTimer?.Dispose();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }
}
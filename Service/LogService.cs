using BackupDbServer.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BackupDbServer.Services
{
    public class LogService
    {
        private readonly string _logFilePath;
        private readonly ObservableCollection<LogEntry> _logEntries;
        private const int MaxLogEntries = 1000; // الحد الأقصى لعدد السجلات
        
        public ObservableCollection<LogEntry> LogEntries => _logEntries;
        
        public LogService()
        {
            _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "operation_log.json");
            _logEntries = new ObservableCollection<LogEntry>();
            _ = LoadLogAsync();
        }
        
        public void AddLogEntry(string operationType, string status, string databaseName, string description, string duration = "")
        {
            var entry = new LogEntry(operationType, status, databaseName, description)
            {
                Duration = duration
            };
            
            // إضافة في بداية القائمة (الأحدث أولاً)
            _logEntries.Insert(0, entry);
            
            // إزالة السجلات القديمة إذا تجاوزت الحد الأقصى
            while (_logEntries.Count > MaxLogEntries)
            {
                _logEntries.RemoveAt(_logEntries.Count - 1);
            }
            
            // حفظ السجل تلقائياً
            _ = SaveLogAsync();
        }
        
        public void AddLogEntry(LogEntry entry)
        {
            _logEntries.Insert(0, entry);
            
            while (_logEntries.Count > MaxLogEntries)
            {
                _logEntries.RemoveAt(_logEntries.Count - 1);
            }
            
            _ = SaveLogAsync();
        }
        
        public async Task SaveLogAsync()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_logEntries.ToList(), options);
                await File.WriteAllTextAsync(_logFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في حفظ السجل: {ex.Message}");
            }
        }
        
        public async Task LoadLogAsync()
        {
            try
            {
                if (File.Exists(_logFilePath))
                {
                    var json = await File.ReadAllTextAsync(_logFilePath);
                    var entries = JsonSerializer.Deserialize<List<LogEntry>>(json) ?? new List<LogEntry>();
                    
                    _logEntries.Clear();
                    foreach (var entry in entries.OrderByDescending(e => e.Timestamp))
                    {
                        _logEntries.Add(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تحميل السجل: {ex.Message}");
            }
        }
        
        public void ClearLog()
        {
            _logEntries.Clear();
            _ = SaveLogAsync();
        }
        
        public async Task<string> ExportLogAsync(string filePath)
        {
            try
            {
                var lines = new List<string>
                {
                    "التاريخ والوقت,نوع العملية,الحالة,قاعدة البيانات,الوصف,المدة الزمنية"
                };
                
                foreach (var entry in _logEntries.OrderBy(e => e.Timestamp))
                {
                    var line = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                              $"{entry.OperationType}," +
                              $"{entry.Status}," +
                              $"{entry.DatabaseName}," +
                              $"\"{entry.Description}\"," +
                              $"{entry.Duration}";
                    lines.Add(line);
                }
                
                await File.WriteAllLinesAsync(filePath, lines, System.Text.Encoding.UTF8);
                return $"تم تصدير {_logEntries.Count} سجل إلى: {filePath}";
            }
            catch (Exception ex)
            {
                return $"خطأ في التصدير: {ex.Message}";
            }
        }
        
        public int GetTotalOperationsCount() => _logEntries.Count;
        public int GetSuccessfulOperationsCount() => _logEntries.Count(e => e.IsSuccess);
        public int GetFailedOperationsCount() => _logEntries.Count(e => !e.IsSuccess && e.Status != "جاري");
        public int GetRunningOperationsCount() => _logEntries.Count(e => e.Status == "جاري");
        public DateTime? GetLastOperationTime() => _logEntries.FirstOrDefault()?.Timestamp;
        
        public List<LogEntry> FilterLogs(string? operationType = null, string? status = null, bool todayOnly = false, bool successOnly = false, bool failedOnly = false)
        {
            var filtered = _logEntries.AsEnumerable();
            
            if (!string.IsNullOrEmpty(operationType) && operationType != "الكل")
                filtered = filtered.Where(e => e.OperationType == operationType);
                
            if (!string.IsNullOrEmpty(status) && status != "الكل")
                filtered = filtered.Where(e => e.Status == status);
                
            if (todayOnly)
                filtered = filtered.Where(e => e.Timestamp.Date == DateTime.Today);
                
            if (successOnly)
                filtered = filtered.Where(e => e.IsSuccess);
                
            if (failedOnly)
                filtered = filtered.Where(e => !e.IsSuccess);
                
            return filtered.ToList();
        }
    }
}

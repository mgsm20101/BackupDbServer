using System;

namespace BackupDbServer.Models
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string OperationType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        
        public string StatusIcon => Status switch
        {
            "نجح" => "✅",
            "فشل" => "❌",
            "جاري" => "⏳",
            _ => "ℹ️"
        };
        
        public string OperationTypeIcon => OperationType switch
        {
            "نسخ احتياطي" => "💾",
            "استعادة" => "🔄",
            "نسخ تلقائي" => "⏰",
            "اختبار اتصال" => "🔗",
            "تحديث قواعد البيانات" => "📊",
            _ => "📝"
        };
        
        public LogEntry()
        {
            Timestamp = DateTime.Now;
        }
        
        public LogEntry(string operationType, string status, string databaseName, string description)
        {
            Timestamp = DateTime.Now;
            OperationType = operationType;
            Status = status;
            DatabaseName = databaseName;
            Description = description;
            IsSuccess = status == "نجح";
        }
    }
}

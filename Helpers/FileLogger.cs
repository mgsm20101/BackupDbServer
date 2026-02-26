using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BackupDbServer.Helpers
{

    public class FileLogger : ILogger
    {
        private readonly string _logPath;
        private readonly object _lock = new object();

        public FileLogger(string logPath = "application.log")
        {
            _logPath = logPath;
        }

        public void LogInformation(string message)
        {
            WriteLog("INFO", message);
        }

        public void LogWarning(string message)
        {
            WriteLog("WARN", message);
        }

        public void LogError(string message, Exception? ex = null)
        {
            var fullMessage = ex != null ? $"{message} - Exception: {ex}" : message;
            WriteLog("ERROR", fullMessage);
            
            // في حالة الأخطاء الحرجة، نحفظ ملف منفصل
            if (ex != null)
            {
                WriteCrashLog(message, ex);
            }
        }

        private void WriteLog(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] [PID:{Environment.ProcessId}] {message}";
                    
                    // إنشاء المجلد إذا لم يكن موجود
                    var directory = Path.GetDirectoryName(_logPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    
                    File.AppendAllText(_logPath, logEntry + Environment.NewLine);
                    
                    // تنظيف الملفات القديمة (الاحتفاظ بآخر 10 أيام فقط)
                    CleanOldLogs();
                }
            }
            catch (Exception ex)
            {
                // في حالة فشل الكتابة العادية، نحاول الكتابة في ملف طوارئ
                try
                {
                    var emergencyFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "emergency_log.txt");
                    var emergencyEntry = $"[{DateTime.Now}] FAILED TO WRITE LOG: {ex.Message}\nOriginal: [{level}] {message}\n";
                    File.AppendAllText(emergencyFile, emergencyEntry);
                }
                catch
                {
                    // آخر محاولة باستخدام EventLog
                    try
                    {
                        System.Diagnostics.EventLog.WriteEntry("Application", 
                            $"BackupDbServer Logging Failed: {ex.Message}\nOriginal: {message}",
                            System.Diagnostics.EventLogEntryType.Error);
                    }
                    catch { /* لا يمكن فعل شيء */ }
                }
            }
        }

        /// <summary>
        /// كتابة ملف خاص بالأخطاء الحرجة
        /// </summary>
        private void WriteCrashLog(string message, Exception ex)
        {
            try
            {
                var crashLogPath = Path.Combine(Path.GetDirectoryName(_logPath) ?? ".", 
                    $"crash_{DateTime.Now:yyyyMMdd}.log");
                
                var crashEntry = new StringBuilder();
                crashEntry.AppendLine($"=== CRASH REPORT - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                crashEntry.AppendLine($"Message: {message}");
                crashEntry.AppendLine($"Exception Type: {ex.GetType().FullName}");
                crashEntry.AppendLine($"Exception Message: {ex.Message}");
                crashEntry.AppendLine($"Source: {ex.Source}");
                crashEntry.AppendLine($"TargetSite: {ex.TargetSite}");
                crashEntry.AppendLine($"Stack Trace:\n{ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    crashEntry.AppendLine($"\nInner Exception: {ex.InnerException.GetType().FullName}");
                    crashEntry.AppendLine($"Inner Message: {ex.InnerException.Message}");
                    crashEntry.AppendLine($"Inner Stack Trace:\n{ex.InnerException.StackTrace}");
                }
                
                crashEntry.AppendLine($"\nEnvironment Info:");
                crashEntry.AppendLine($"OS: {Environment.OSVersion}");
                crashEntry.AppendLine($"CLR Version: {Environment.Version}");
                crashEntry.AppendLine($"Machine Name: {Environment.MachineName}");
                crashEntry.AppendLine($"User: {Environment.UserName}");
                crashEntry.AppendLine($"Working Directory: {Environment.CurrentDirectory}");
                crashEntry.AppendLine($"Process ID: {Environment.ProcessId}");
                crashEntry.AppendLine($"Thread ID: {Thread.CurrentThread.ManagedThreadId}");
                crashEntry.AppendLine("=== END CRASH REPORT ===\n");
                
                File.AppendAllText(crashLogPath, crashEntry.ToString());
            }
            catch
            {
                // إذا فشل حفظ ملف الـ crash، لا نفعل شيء
            }
        }

        /// <summary>
        /// تنظيف ملفات السجل القديمة
        /// </summary>
        private void CleanOldLogs()
        {
            try
            {
                var directory = Path.GetDirectoryName(_logPath);
                if (string.IsNullOrEmpty(directory)) return;

                var logFiles = Directory.GetFiles(directory, "*.log");
                var cutoffDate = DateTime.Now.AddDays(-10);

                foreach (var file in logFiles)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch
                        {
                            // تجاهل أخطاء الحذف
                        }
                    }
                }
            }
            catch
            {
                // تجاهل أخطاء التنظيف
            }
        }

        public void LogInfo(string message)
        {
            WriteLog("INFO", message);
        }
    }
}
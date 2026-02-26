using BackupDbServer.Helpers;
using BackupDbServer.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;

namespace BackupDbServer.Services
{
    public class DatabaseCopyService
    {
        public async Task CopyDatabaseWithModificationsAsync(
            string sourceDatabaseName,
            string targetDatabaseName,
            List<string> sqlScripts,
            Dictionary<string, string> fileReplacements,
            IProgress<string>? progress)
        {
            var config = SettingsManager.LoadSettings();
            string connectionString = $"Server={config.Server};User ID={config.Username};Password={config.Password};TrustServerCertificate=True;";

            try
            {
                // الخطوة 1: إنشاء نسخة احتياطية مؤقتة
                progress?.Report("إنشاء نسخة احتياطية مؤقتة...");
                string tempBackupPath = await CreateTemporaryBackupAsync(sourceDatabaseName, connectionString, progress);

                // الخطوة 2: استعادة النسخة الاحتياطية باسم جديد
                progress?.Report($"استعادة قاعدة البيانات باسم: {targetDatabaseName}...");
                await RestoreDatabaseWithNewNameAsync(tempBackupPath, targetDatabaseName, connectionString, progress);

                // الخطوة 3: تنفيذ سكربتات SQL على القاعدة الجديدة
                if (sqlScripts != null && sqlScripts.Count > 0)
                {
                    progress?.Report("تنفيذ سكربتات SQL على القاعدة الجديدة...");
                    await ExecuteSqlScriptsAsync(targetDatabaseName, sqlScripts, connectionString, progress);
                }

                // الخطوة 4: تطبيق تغييرات الملفات (إذا كانت مطلوبة)
                if (fileReplacements != null && fileReplacements.Count > 0)
                {
                    progress?.Report("تطبيق تغييرات على الملفات...");
                    await ApplyFileModificationsAsync(fileReplacements, progress);
                }

                // الخطوة 5: تنظيف الملف المؤقت
                if (File.Exists(tempBackupPath))
                {
                    File.Delete(tempBackupPath);
                }

                progress?.Report($"✅ تم نسخ قاعدة البيانات بنجاح: {targetDatabaseName}");
            }
            catch (Exception ex)
            {
                progress?.Report($"❌ خطأ أثناء نسخ قاعدة البيانات: {ex.Message}");
                throw;
            }
        }

        private async Task<string> CreateTemporaryBackupAsync(string databaseName, string connectionString, IProgress<string>? progress)
        {
            string tempBackupPath = Path.Combine(Path.GetTempPath(), $"{databaseName}_temp_{DateTime.Now:yyyyMMddHHmmss}.bak");

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                string backupCommand = $@"
                    BACKUP DATABASE [{databaseName}] 
                    TO DISK = N'{tempBackupPath}' 
                    WITH NOFORMAT, NOINIT, 
                    NAME = N'{databaseName}-Temp Backup', 
                    SKIP, NOREWIND, NOUNLOAD, STATS = 10";

                using (var command = new SqlCommand(backupCommand, connection))
                {
                    command.CommandTimeout = 1800; // 30 دقيقة
                    await command.ExecuteNonQueryAsync();
                }
            }

            return tempBackupPath;
        }

        private async Task RestoreDatabaseWithNewNameAsync(string backupPath, string newDatabaseName, string connectionString, IProgress<string>? progress)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // الحصول على قائمة الملفات في النسخة الاحتياطية
                var fileList = new List<(string LogicalName, string Type)>();
                string fileListCommand = $"RESTORE FILELISTONLY FROM DISK = N'{backupPath}'";

                using (var cmd = new SqlCommand(fileListCommand, connection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        fileList.Add((reader.GetString("LogicalName"), reader.GetString("Type")));
                    }
                }

                // بناء جمل MOVE للملفات
                var defaultDataPath = await GetDefaultDataPathAsync(connection);
                var moveClauses = new List<string>();

                foreach (var (logicalName, type) in fileList)
                {
                    string fileExtension = (type == "D") ? ".mdf" : ".ldf";
                    string newFileName = $"{newDatabaseName}_{logicalName}{fileExtension}";
                    string newFilePath = Path.Combine(defaultDataPath, newFileName);
                    moveClauses.Add($"MOVE N'{logicalName}' TO N'{newFilePath}'");
                }

                // تنفيذ أمر الاستعادة
                string restoreCommand = $@"
                    RESTORE DATABASE [{newDatabaseName}] 
                    FROM DISK = N'{backupPath}'
                    WITH REPLACE, RECOVERY, {string.Join(", ", moveClauses)}";

                using (var restoreCmd = new SqlCommand(restoreCommand, connection))
                {
                    restoreCmd.CommandTimeout = 1800; // 30 دقيقة
                    await restoreCmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task ExecuteSqlScriptsAsync(string databaseName, List<string> sqlScripts, string connectionString, IProgress<string>? progress)
        {
            string dbConnectionString = $"{connectionString};Database={databaseName};";

            using (var connection = new SqlConnection(dbConnectionString))
            {
                await connection.OpenAsync();

                for (int i = 0; i < sqlScripts.Count; i++)
                {
                    progress?.Report($"تنفيذ سكربت {i + 1} من {sqlScripts.Count}...");

                    using (var command = new SqlCommand(sqlScripts[i], connection))
                    {
                        command.CommandTimeout = 600; // 10 دقائق لكل سكربت
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        private async Task ApplyFileModificationsAsync(Dictionary<string, string> fileReplacements, IProgress<string>? progress)
        {
            foreach (var replacement in fileReplacements)
            {
                string filePath = replacement.Key;
                string replacementContent = replacement.Value;

                if (File.Exists(filePath))
                {
                    progress?.Report($"تعديل ملف: {Path.GetFileName(filePath)}...");

                    // قراءة محتوى الملف الحالي
                    string currentContent = await File.ReadAllTextAsync(filePath);

                    // تطبيق التغييرات (يمكن تخصيص هذا حسب الحاجة)
                    string newContent = ApplyReplacements(currentContent, replacementContent);

                    // كتابة المحتوى الجديد
                    await File.WriteAllTextAsync(filePath, newContent);
                }
            }
        }

        private string ApplyReplacements(string originalContent, string replacements)
        {
            // يمكن تخصيص هذه الدالة لتطبيق أنواع مختلفة من التغييرات
            // مثال بسيط: استبدال نص معين

            // يمكن تحسين هذا ليدعم JSON للتغييرات المعقدة
            var lines = replacements.Split('\n');
            string result = originalContent;

            foreach (var line in lines)
            {
                if (line.Contains("=>"))
                {
                    var parts = line.Split("=>");
                    if (parts.Length == 2)
                    {
                        string oldValue = parts[0].Trim();
                        string newValue = parts[1].Trim();
                        result = result.Replace(oldValue, newValue);
                    }
                }
            }

            return result;
        }

        private async Task<string> GetDefaultDataPathAsync(SqlConnection connection)
        {
            string query = "SELECT SERVERPROPERTY('InstanceDefaultDataPath') AS DefaultPath";
            using (var cmd = new SqlCommand(query, connection))
            {
                var result = await cmd.ExecuteScalarAsync();
                return result?.ToString() ?? @"C:\Program Files\Microsoft SQL Server\MSSQL15.MSSQLSERVER\MSSQL\DATA";
            }
        }
    }
}

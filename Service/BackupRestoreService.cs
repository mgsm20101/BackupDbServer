using BackupDbServer.Helpers;
using BackupDbServer.Models;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BackupDbServer.Services
{
    public class BackupRestoreService
    {
        public readonly ILogger _logger;
        public readonly Settings _settings;

        public BackupRestoreService()
        {
            _logger = new FileLogger();

            // تحميل الإعدادات من SettingsManager وتحويلها إلى Settings
            var backupConfig = SettingsManager.LoadSettings();
            _settings = ConvertToSettings(backupConfig);
        }

        // ...existing code...

        private Settings ConvertToSettings(BackupConfig backupConfig)
        {
            return new Settings
            {
                ServerName = GetPropertySafely(() => backupConfig.Server, "localhost"),
                Username = GetPropertySafely(() => backupConfig.Username, "sa"),
                Password = GetPropertySafely(() => backupConfig.Password, "Passw0rd"),
                UseWindowsAuthentication = GetPropertySafely(() => backupConfig.UseWindowsAuthentication, false),
                BackupPath = GetPropertySafely(() => backupConfig.BackupPath, @"C:\Backups"),
                SelectedDatabases = backupConfig.SelectedDatabases ?? new List<string>(),

                // إعدادات Docker - استخدام القيم المحفوظة
                ConnectionType = backupConfig.ConnectionType,
                DockerContainerName =  backupConfig.DockerContainerName,
                Port = GetPropertySafely(() => backupConfig.Port, 1433),
                UseCustomPort = true,
                ConnectionTimeout = GetPropertySafely(() => backupConfig.ConnectionTimeout, 30),
                TrustServerCertificate = GetPropertySafely(() => backupConfig.TrustServerCertificate, true),
                Encrypt = GetPropertySafely(() => backupConfig.Encrypt, false)
            };
        }

        /// <summary>
        /// تحديث الإعدادات في الخدمة
        /// </summary>
        /// <param name="backupConfig">الإعدادات الجديدة</param>
        public void UpdateSettings(BackupConfig backupConfig)
        {
            try
            {
                // تحديث الإعدادات بالقيم الجديدة
                var newSettings = ConvertToSettings(backupConfig);
                
                // نسخ القيم الجديدة إلى الإعدادات الحالية
                _settings.ServerName = newSettings.ServerName;
                _settings.Username = newSettings.Username;
                _settings.Password = newSettings.Password;
                _settings.UseWindowsAuthentication = newSettings.UseWindowsAuthentication;
                _settings.BackupPath = newSettings.BackupPath;
                _settings.SelectedDatabases = newSettings.SelectedDatabases;
                _settings.ConnectionType = newSettings.ConnectionType;
                _settings.DockerContainerName = newSettings.DockerContainerName;
                _settings.Port = newSettings.Port;
                _settings.UseCustomPort = newSettings.UseCustomPort;
                _settings.ConnectionTimeout = newSettings.ConnectionTimeout;
                _settings.TrustServerCertificate = newSettings.TrustServerCertificate;
                _settings.Encrypt = newSettings.Encrypt;

                _logger.LogInformation("تم تحديث إعدادات BackupRestoreService بنجاح");
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في تحديث إعدادات BackupRestoreService: {ex.Message}", ex);
                throw;
            }
        }

        // ...existing code...
        private T GetPropertySafely<T>(Func<T> propertyGetter, T defaultValue)
        {
            try
            {
                return propertyGetter();
            }
            catch
            {
                return defaultValue;
            }
        }

        public async Task<List<string>> GetDatabasesAsync()
        {
            var databases = new List<string>();

            try
            {
                using var connection = new SqlConnection(GetConnectionString());
                await connection.OpenAsync();

                const string query = @"
                    SELECT name 
                    FROM sys.databases 
                    WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb')
                    AND state = 0
                    ORDER BY name"; // ONLINE only

                using var command = new SqlCommand(query, connection);
                command.CommandTimeout = 30;

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    databases.Add(reader.GetString(0));
                }

                var connectionInfo = GetConnectionInfo();
                _logger.LogInfo($"تم العثور على {databases.Count} قاعدة بيانات في {connectionInfo}");
            }
            catch (Exception ex)
            {
                var connectionInfo = GetConnectionInfo();
                _logger.LogError($"خطأ في الحصول على قواعد البيانات من {connectionInfo}: {ex.Message}", ex);
                throw new InvalidOperationException($"فشل الاتصال بـ {connectionInfo}: {ex.Message}", ex);
            }

            return databases;
        }

        /// <summary>
        /// اختبار الاتصال بقاعدة البيانات مع تشخيص مفصل للأخطاء
        /// </summary>
        public async Task<(bool Success, string Message)> TestConnectionAsync()
        {
            try
            {
                var connectionString = GetConnectionString();
                _logger.LogInfo($"اختبار الاتصال باستخدام: {connectionString.Replace(_settings.Password, "***")}");

                using var connection = new SqlConnection(connectionString);
                
                // محاولة فتح الاتصال
                await connection.OpenAsync();
                
                // اختبار تنفيذ استعلام بسيط
                using var command = new SqlCommand("SELECT @@VERSION", connection);
                var version = await command.ExecuteScalarAsync() as string;
                
                _logger.LogInfo($"نجح الاتصال بـ SQL Server: {version?.Substring(0, Math.Min(50, version?.Length ?? 0))}...");
                
                return (true, $"✅ نجح الاتصال!\nSQL Server Version: {version?.Substring(0, Math.Min(100, version?.Length ?? 0))}...");
            }
            catch (SqlException sqlEx)
            {
                var errorMessage = GetSqlErrorDetails(sqlEx);
                _logger.LogError($"خطأ SQL في اختبار الاتصال: {errorMessage}", sqlEx);
                return (false, $"❌ فشل الاتصال - خطأ SQL:\n{errorMessage}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ عام في اختبار الاتصال: {ex.Message}", ex);
                return (false, $"❌ فشل الاتصال - خطأ عام:\n{ex.Message}");
            }
        }

        /// <summary>
        /// تفسير أخطاء SQL بشكل مفهوم
        /// </summary>
        private string GetSqlErrorDetails(SqlException sqlEx)
        {
            return sqlEx.Number switch
            {
                2 => "لا يمكن الوصول إلى الخادم. تحقق من:\n• اسم الخادم صحيح\n• SQL Server يعمل\n• Network Protocols مُفعلة",
                18456 => "فشل المصادقة. تحقق من:\n• اسم المستخدم وكلمة المرور صحيحة\n• حساب 'sa' مُفعل\n• SQL Server Authentication مُفعل",
                1326 => "فشل مصادقة Windows. تحقق من:\n• الحساب له صلاحيات للوصول\n• Domain settings صحيحة",
                233 => "انتهت مهلة الاتصال. تحقق من:\n• الشبكة تعمل بشكل طبيعي\n• Firewall settings\n• SQL Server Browser يعمل",
                53 => "لا يمكن العثور على الخادم. تحقق من:\n• اسم الخادم صحيح\n• SQL Server Browser يعمل\n• Named Pipes/TCP مُفعل",
                _ => $"خطأ SQL رقم {sqlEx.Number}: {sqlEx.Message}"
            };
        }

        public async Task BackupDatabasesAsync(List<string> databaseNames, IProgress<string> progress)
        {
            if (databaseNames == null || databaseNames.Count == 0)
            {
                throw new ArgumentException("لا توجد قواعد بيانات محددة للنسخ الاحتياطي");
            }

            // التأكد من وجود مجلد النسخ الاحتياطي
            var backupDirectory = GetBackupDirectory();
            if (!Directory.Exists(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
                _logger.LogInfo($"تم إنشاء مجلد النسخ الاحتياطي: {backupDirectory}");
            }

            var totalDatabases = databaseNames.Count;
            var completedDatabases = 0;

            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            var connectionInfo = GetConnectionInfo();
            _logger.LogInfo($"تم الاتصال بـ {connectionInfo} بنجاح");
            progress?.Report($"متصل بـ {connectionInfo}");

            foreach (var dbName in databaseNames)
            {
                try
                {
                    progress?.Report($"بدء النسخ الاحتياطي لقاعدة البيانات: {dbName} ({completedDatabases + 1}/{totalDatabases})");

                    await BackupSingleDatabaseAsync(connection, dbName, backupDirectory, progress);

                    completedDatabases++;
                    progress?.Report($"✅ تم الانتهاء من النسخ الاحتياطي: {dbName} ({completedDatabases}/{totalDatabases})");

                    _logger.LogInfo($"تم النسخ الاحتياطي بنجاح لقاعدة البيانات: {dbName}");
                }
                catch (Exception ex)
                {
                    var errorMessage = $"فشل النسخ الاحتياطي لقاعدة البيانات {dbName}: {ex.Message}";
                    _logger.LogError(errorMessage, ex);
                    progress?.Report($"❌ خطأ: {errorMessage}");

                    // نكمل مع باقي القواعد
                    continue;
                }
            }

            progress?.Report($"🎉 اكتمل النسخ الاحتياطي لـ {completedDatabases} من أصل {totalDatabases} قاعدة بيانات");
        }

        private async Task BackupSingleDatabaseAsync(SqlConnection connection, string databaseName, string backupDirectory, IProgress<string> progress)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFileName = $"{databaseName}_{timestamp}.bak";

            // مسار النسخة الاحتياطية (يختلف حسب نوع الاتصال)
            string backupPath;
            if (_settings.ConnectionType == ConnectionType.Docker)
            {
                backupPath = $"/var/opt/mssql/backup/{backupFileName}";
            }
            else
            {
                backupPath = Path.Combine(backupDirectory, backupFileName);
            }

            // مسار النسخة الاحتياطية المحلي
            var localBackupPath = Path.Combine(backupDirectory, backupFileName);

            try
            {
                // التحقق من حالة قاعدة البيانات
                progress?.Report($"فحص حالة قاعدة البيانات: {databaseName}");
                await ValidateDatabaseStateAsync(connection, databaseName);

                if (_settings.ConnectionType == ConnectionType.Docker)
                {
                    // معالجة خاصة بـ Docker
                    await HandleDockerBackupAsync(connection, databaseName, backupPath, localBackupPath, progress);
                }
                else
                {
                    // معالجة الاتصال المحلي أو عن بُعد
                    await HandleStandardBackupAsync(connection, databaseName, backupPath, progress);
                }

                // التحقق من نجاح النسخ
                var fileInfo = new FileInfo(localBackupPath);
                if (!fileInfo.Exists || fileInfo.Length == 0)
                {
                    throw new InvalidOperationException($"فشل في إنشاء النسخة الاحتياطية");
                }

                var fileSizeMB = Math.Round(fileInfo.Length / (1024.0 * 1024.0), 2);
                progress?.Report($"✅ تم إنشاء النسخة الاحتياطية: {backupFileName} (الحجم: {fileSizeMB} MB)");

                _logger.LogInfo($"تم النسخ الاحتياطي بنجاح: {localBackupPath} (الحجم: {fileSizeMB} MB)");

                // ضغط الملف
                await CompressionHelper.CompressBackupFileAsync(localBackupPath);
            }
            catch (Exception ex)
            {
                // حذف الملف المحلي في حالة الفشل
                if (File.Exists(localBackupPath))
                {
                    File.Delete(localBackupPath);
                }

                var errorMessage = $"خطأ أثناء النسخ الاحتياطي لقاعدة البيانات {databaseName}: {ex.Message}";
                _logger.LogError(errorMessage, ex);
                throw new InvalidOperationException(errorMessage, ex);
            }
        }

        /// <summary>
        /// معالجة النسخ الاحتياطي لحاويات Docker
        /// </summary>
        private async Task HandleDockerBackupAsync(SqlConnection connection, string databaseName, string containerBackupPath, string localBackupPath, IProgress<string> progress)
        {
            // إنشاء مجلد النسخ الاحتياطي داخل الحاوية إذا لم يكن موجوداً
            progress?.Report($"تجهيز مجلد النسخ الاحتياطي في الحاوية...");
            await CreateBackupDirectoryInContainerAsync(connection);

            // تنفيذ النسخ الاحتياطي داخل الحاوية
            progress?.Report($"تنفيذ النسخ الاحتياطي في حاوية Docker: {databaseName}");
            await PerformBackupInContainerAsync(connection, databaseName, containerBackupPath, progress);

            // نسخ الملف من الحاوية إلى النظام المحلي
            progress?.Report($"نسخ الملف من الحاوية إلى النظام المحلي...");
            await CopyBackupFromContainerAsync(containerBackupPath, localBackupPath);

            // تنظيف الملف من الحاوية (اختياري)
            await CleanupContainerBackupAsync(containerBackupPath);
        }

        /// <summary>
        /// معالجة النسخ الاحتياطي للاتصال المحلي أو عن بُعد
        /// </summary>
        private async Task HandleStandardBackupAsync(SqlConnection connection, string databaseName, string backupPath, IProgress<string> progress)
        {
            var connectionType = GetConnectionInfo();
            progress?.Report($"تنفيذ النسخ الاحتياطي على {connectionType}: {databaseName}");

            var backupQuery = $@"
                BACKUP DATABASE [{databaseName}] 
                TO DISK = N'{backupPath}'
                WITH FORMAT, 
                     
                     CHECKSUM,
                     INIT,
                     NAME = N'{databaseName}-Backup-{DateTime.Now:yyyyMMdd_HHmmss}',
                     SKIP, 
                     NOREWIND, 
                     NOUNLOAD,
                     STATS = 5";

            using var command = new SqlCommand(backupQuery, connection);
            command.CommandTimeout = 600; // 10 دقائق

            // إضافة معالج للتقدم
            command.StatementCompleted += (sender, e) =>
            {
                progress?.Report($"تقدم النسخ الاحتياطي: {e.RecordCount} سجل تم معالجته");
            };

            await command.ExecuteNonQueryAsync();

            // التحقق من صحة النسخة الاحتياطية
            progress?.Report($"التحقق من صحة النسخة الاحتياطية...");
            await VerifyStandardBackupAsync(connection, backupPath);
        }

        /// <summary>
        /// التحقق من صحة النسخة الاحتياطية للاتصال المحلي/عن بُعد
        /// </summary>
        private async Task VerifyStandardBackupAsync(SqlConnection connection, string backupPath)
        {
            const string verifyQuery = "RESTORE VERIFYONLY FROM DISK = @backupPath";

            using var command = new SqlCommand(verifyQuery, connection);
            command.Parameters.AddWithValue("@backupPath", backupPath);
            command.CommandTimeout = 120;

            try
            {
                await command.ExecuteNonQueryAsync();
                _logger.LogInfo($"تم التحقق من صحة النسخة الاحتياطية: {backupPath}");
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"فشل التحقق من صحة النسخة الاحتياطية: {ex.Message}", ex);
            }
        }

       
      
        private async Task VerifyBackupInContainerAsync(SqlConnection connection, string containerBackupPath)
        {
            const string verifyQuery = "RESTORE VERIFYONLY FROM DISK = @backupPath";

            using var command = new SqlCommand(verifyQuery, connection);
            command.Parameters.AddWithValue("@backupPath", containerBackupPath);
            command.CommandTimeout = 120;

            try
            {
                await command.ExecuteNonQueryAsync();
                _logger.LogInfo($"تم التحقق من صحة النسخة الاحتياطية داخل الحاوية: {containerBackupPath}");
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"فشل التحقق من صحة النسخة الاحتياطية: {ex.Message}", ex);
            }
        }

        private async Task CopyBackupFromContainerAsync(string containerPath, string localPath)
        {
            try
            {
                var containerName = _settings.DockerContainerName;
                var dockerCommand = $"docker cp {containerName}:{containerPath} \"{localPath}\"";

                _logger.LogInfo($"تنفيذ أمر Docker: {dockerCommand}");

                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {dockerCommand}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();

                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();

                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"فشل في نسخ الملف من الحاوية. الخطأ: {error}");
                    }

                    _logger.LogInfo($"تم نسخ الملف بنجاح من الحاوية: {output}");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"خطأ في نسخ الملف من حاوية Docker: {ex.Message}", ex);
            }
        }

       
        private async Task EnsureContainerIsRunningAsync(string containerName)
        {
            try
            {
                var checkCommand = $"docker ps --filter name={containerName} --format \"{{.Names}}\"";

                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {checkCommand}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    var output = await process.StandardOutput.ReadToEndAsync();

                    if (string.IsNullOrWhiteSpace(output) || !output.Contains(containerName))
                    {
                        throw new InvalidOperationException($"الحاوية {containerName} غير متاحة أو لا تعمل");
                    }

                    _logger.LogInfo($"الحاوية {containerName} تعمل بنجاح");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل التحقق من حالة الحاوية: {ex.Message}", ex);
            }
        }

        private async Task VerifyFileExistsInContainerAsync(string containerName, string containerPath)
        {
            try
            {
                var checkCommand = $"docker exec {containerName} ls -la {containerPath}";

                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {checkCommand}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();

                    if (process.ExitCode != 0 || output.Contains("No such file"))
                    {
                        throw new InvalidOperationException($"الملف غير موجود في الحاوية: {containerPath}");
                    }

                    _logger.LogInfo($"الملف موجود في الحاوية: {containerPath}");
                    _logger.LogInfo($"تفاصيل الملف: {output.Trim()}");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل التحقق من وجود الملف في الحاوية: {ex.Message}", ex);
            }
        }

        // تحسين طريقة إنشاء مجلد النسخ الاحتياطي في الحاوية
        private async Task CreateBackupDirectoryInContainerAsync(SqlConnection connection)
        {
            try
            {
                // استخدام أمر Docker بدلاً من xp_cmdshell dbs-2022-CTP23-1 الأمان
                var containerName = _settings.DockerContainerName;
                var createDirCommand = $"docker exec {containerName} mkdir -p /var/opt/mssql/backup";

                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {createDirCommand}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        _logger.LogInfo("تم إنشاء مجلد النسخ الاحتياطي في الحاوية بنجاح");
                    }
                }

                // التحقق من الصلاحيات
                var chmodCommand = $"docker exec {containerName} chmod 777 /var/opt/mssql/backup";
                var chmodProcess = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {chmodCommand}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var chmod = System.Diagnostics.Process.Start(chmodProcess);
                if (chmod != null)
                {
                    await chmod.WaitForExitAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"تحذير: لا يمكن إنشاء مجلد النسخ الاحتياطي في الحاوية: {ex.Message}");
                // نحاول استخدام xp_cmdshell كبديل
                try
                {
                    const string createDirQuery = @"EXEC xp_cmdshell 'mkdir -p /var/opt/mssql/backup && chmod 777 /var/opt/mssql/backup'";
                    using var command = new SqlCommand(createDirQuery, connection);
                    command.CommandTimeout = 30;
                    await command.ExecuteNonQueryAsync();
                    _logger.LogInfo("تم إنشاء المجلد باستخدام xp_cmdshell");
                }
                catch (Exception sqlEx)
                {
                    _logger.LogWarning($"فشل في إنشاء المجلد باستخدام SQL أيضاً: {sqlEx.Message}");
                }
            }
        }

        // تحسين طريقة النسخ الاحتياطي مع معلومات إضافية
        private async Task PerformBackupInContainerAsync(SqlConnection connection, string databaseName, string containerBackupPath, IProgress<string> progress)
        {
            var backupQuery = $@"
        BACKUP DATABASE [{databaseName}] 
        TO DISK = N'{containerBackupPath}'
        WITH FORMAT, 
             COMPRESSION,
             CHECKSUM,
             INIT,
             NAME = N'{databaseName}-Docker-Backup-{DateTime.Now:yyyyMMdd_HHmmss}',
             SKIP, 
             NOREWIND, 
             NOUNLOAD,
             STATS = 5";

            using var command = new SqlCommand(backupQuery, connection);
            command.CommandTimeout = 600; // 10 دقائق

            // إضافة معالج للتقدم
            command.StatementCompleted += (sender, e) =>
            {
                progress?.Report($"تقدم النسخ الاحتياطي: {e.RecordCount} سجل تم معالجته");
            };

            await command.ExecuteNonQueryAsync();

            // التحقق من حجم الملف في الحاوية
            progress?.Report($"التحقق من حجم الملف في الحاوية...");
            await CheckBackupFileSizeInContainerAsync(containerBackupPath);

            // التحقق من صحة النسخة الاحتياطية
            progress?.Report($"التحقق من صحة النسخة الاحتياطية...");
            await VerifyBackupInContainerAsync(connection, containerBackupPath);
        }

        private async Task CheckBackupFileSizeInContainerAsync(string containerBackupPath)
        {
            try
            {
                var containerName = _settings.DockerContainerName;
                var sizeCommand = $"docker exec {containerName} stat -c%s {containerBackupPath}";

                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {sizeCommand}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    var output = await process.StandardOutput.ReadToEndAsync();

                    if (process.ExitCode == 0 && long.TryParse(output.Trim(), out var fileSize))
                    {
                        var fileSizeMB = Math.Round(fileSize / (1024.0 * 1024.0), 2);
                        _logger.LogInfo($"حجم النسخة الاحتياطية في الحاوية: {fileSizeMB} MB");

                        if (fileSize == 0)
                        {
                            throw new InvalidOperationException("النسخة الاحتياطية فارغة في الحاوية");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"تحذير: لا يمكن التحقق من حجم الملف في الحاوية: {ex.Message}");
            }
        }


        // ...existing code...

        private async Task CleanupContainerBackupAsync(string containerBackupPath)
        {
            try
            {
                var containerName = _settings.DockerContainerName;
                var dockerCommand = $"docker exec {containerName} rm -f {containerBackupPath}";

                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {dockerCommand}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();

                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();

                    if (process.ExitCode == 0)
                    {
                        _logger.LogInfo($"تم حذف الملف من الحاوية بنجاح: {containerBackupPath}");
                    }
                    else
                    {
                        _logger.LogWarning($"تحذير: فشل حذف الملف من الحاوية: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"تحذير: لا يمكن حذف الملف من الحاوية: {ex.Message}");
                // نتجاهل هذا الخطأ لأنه ليس حرجاً
            }
        }

        // ...existing code...

        private async Task ValidateDatabaseStateAsync(SqlConnection connection, string databaseName)
        {
            const string query = @"
                SELECT state_desc, user_access_desc 
                FROM sys.databases 
                WHERE name = @databaseName";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@databaseName", databaseName);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var state = reader.GetString(0);
                var userAccess = reader.GetString(1);

                if (state != "ONLINE")
                {
                    throw new InvalidOperationException($"قاعدة البيانات {databaseName} غير متاحة (الحالة: {state})");
                }

                _logger.LogInfo($"حالة قاعدة البيانات {databaseName}: {state}, الوصول: {userAccess}");
            }
            else
            {
                throw new InvalidOperationException($"قاعدة البيانات {databaseName} غير موجودة");
            }
        }

        public async Task RestoreDatabaseAsync(string backupFilePath, string newDatabaseName, IProgress<string> progress)
        {
            if (string.IsNullOrEmpty(backupFilePath) || !File.Exists(backupFilePath))
            {
                throw new ArgumentException("ملف النسخة الاحتياطية غير موجود");
            }

            try
            {
                using var connection = new SqlConnection(GetConnectionString());
                await connection.OpenAsync();

                var connectionInfo = GetConnectionInfo();

                if (_settings.ConnectionType == ConnectionType.Docker)
                {
                    await HandleDockerRestoreAsync(connection, backupFilePath, newDatabaseName, progress);
                }
                else
                {
                    await HandleStandardRestoreAsync(connection, backupFilePath, newDatabaseName, progress);
                }

                progress?.Report($"تمت استعادة قاعدة البيانات بنجاح على {connectionInfo}: {newDatabaseName}");
                _logger.LogInfo($"تمت استعادة قاعدة البيانات: {newDatabaseName} من {backupFilePath} على {connectionInfo}");
            }
            catch (Exception ex)
            {
                var errorMessage = $"فشلت عملية الاستعادة: {ex.Message}";
                _logger.LogError(errorMessage, ex);
                throw new InvalidOperationException(errorMessage, ex);
            }
        }

        /// <summary>
        /// معالجة الاستعادة لحاويات Docker
        /// </summary>
        private async Task HandleDockerRestoreAsync(SqlConnection connection, string backupFilePath, string newDatabaseName, IProgress<string> progress)
        {
            progress?.Report("رفع ملف النسخة الاحتياطية إلى الحاوية...");

            // نسخ الملف إلى الحاوية
            var containerBackupPath = $"/var/opt/mssql/backup/{Path.GetFileName(backupFilePath)}";
            await CopyBackupToContainerAsync(backupFilePath, containerBackupPath);

            progress?.Report("فحص ملف النسخة الاحتياطية...");

            // الحصول على معلومات النسخة الاحتياطية
            var backupInfo = await GetBackupFileInfoAsync(connection, containerBackupPath);

            progress?.Report($"استعادة قاعدة البيانات في الحاوية باسم: {newDatabaseName}");

            // تنفيذ الاستعادة
            await PerformRestoreInContainerAsync(connection, containerBackupPath, newDatabaseName, backupInfo, progress);

            // تنظيف الملف من الحاوية
            await CleanupContainerBackupAsync(containerBackupPath);
        }

        /// <summary>
        /// معالجة الاستعادة للاتصال المحلي أو عن بُعد
        /// </summary>
        private async Task HandleStandardRestoreAsync(SqlConnection connection, string backupFilePath, string newDatabaseName, IProgress<string> progress)
        {
            var connectionInfo = GetConnectionInfo();
            progress?.Report($"فحص ملف النسخة الاحتياطية على {connectionInfo}...");

            // الحصول على معلومات النسخة الاحتياطية
            var backupInfo = await GetStandardBackupFileInfoAsync(connection, backupFilePath);

            progress?.Report($"استعادة قاعدة البيانات على {connectionInfo} باسم: {newDatabaseName}");

            // تنفيذ الاستعادة
            await PerformStandardRestoreAsync(connection, backupFilePath, newDatabaseName, backupInfo, progress);
        }

        /// <summary>
        /// الحصول على معلومات ملف النسخة الاحتياطية للاتصال المحلي/عن بُعد
        /// </summary>
        private async Task<dynamic> GetStandardBackupFileInfoAsync(SqlConnection connection, string backupFilePath)
        {
            const string query = "RESTORE FILELISTONLY FROM DISK = @backupPath";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@backupPath", backupFilePath);

            using var reader = await command.ExecuteReaderAsync();

            string? dataLogicalName = null;
            string? logLogicalName = null;

            while (await reader.ReadAsync())
            {
                var logicalName = reader.GetString(0); // LogicalName
                var type = reader.GetString(2); // Type

                if (type == "D") // Data file
                    dataLogicalName = logicalName;
                else if (type == "L") // Log file
                    logLogicalName = logicalName;
            }

            if (string.IsNullOrEmpty(dataLogicalName) || string.IsNullOrEmpty(logLogicalName))
            {
                throw new InvalidOperationException("لا يمكن العثور على ملفات البيانات واللوج في النسخة الاحتياطية");
            }

            return new { DataLogicalName = dataLogicalName, LogLogicalName = logLogicalName };
        }

        /// <summary>
        /// تنفيذ الاستعادة للاتصال المحلي/عن بُعد
        /// </summary>
        private async Task PerformStandardRestoreAsync(SqlConnection connection, string backupFilePath, string newDatabaseName, dynamic backupInfo, IProgress<string> progress)
        {
            // مسارات الملفات للخادم المحلي أو عن بُعد
            var dataFilePath = $"{newDatabaseName}.mdf";
            var logFilePath = $"{newDatabaseName}_Log.ldf";

            var restoreQuery = $@"
                RESTORE DATABASE [{newDatabaseName}] 
                FROM DISK = N'{backupFilePath}'
                WITH FILE = 1,
                     MOVE N'{backupInfo.DataLogicalName}' TO N'{dataFilePath}',
                     MOVE N'{backupInfo.LogLogicalName}' TO N'{logFilePath}',
                     NOUNLOAD,
                     REPLACE,
                     STATS = 10";

            using var command = new SqlCommand(restoreQuery, connection);
            command.CommandTimeout = 600; // 10 دقائق

            await command.ExecuteNonQueryAsync();
        }

        private async Task CopyBackupToContainerAsync(string localPath, string containerPath)
        {
            try
            {
                var containerName = _settings.DockerContainerName;
                var dockerCommand = $"docker cp \"{localPath}\" {containerName}:{containerPath}";

                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {dockerCommand}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();

                    var error = await process.StandardError.ReadToEndAsync();

                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"فشل في رفع الملف إلى الحاوية. الخطأ: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"خطأ في رفع الملف إلى حاوية Docker: {ex.Message}", ex);
            }
        }

        private async Task<dynamic> GetBackupFileInfoAsync(SqlConnection connection, string containerBackupPath)
        {
            const string query = "RESTORE FILELISTONLY FROM DISK = @backupPath";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@backupPath", containerBackupPath);

            using var reader = await command.ExecuteReaderAsync();

            string dataLogicalName = null;
            string logLogicalName = null;

            while (await reader.ReadAsync())
            {
                // استخدام الفهرس الرقمي بدلاً من اسم العمود لتجنب الأخطاء
                var logicalName = reader.GetString(0); // LogicalName
                var type = reader.GetString(2); // Type

                if (type == "D") // Data file
                    dataLogicalName = logicalName;
                else if (type == "L") // Log file
                    logLogicalName = logicalName;
            }

            if (string.IsNullOrEmpty(dataLogicalName) || string.IsNullOrEmpty(logLogicalName))
            {
                throw new InvalidOperationException("لا يمكن العثور على ملفات البيانات واللوج في النسخة الاحتياطية");
            }

            return new { DataLogicalName = dataLogicalName, LogLogicalName = logLogicalName };
        }

        private async Task PerformRestoreInContainerAsync(SqlConnection connection, string containerBackupPath, string newDatabaseName, dynamic backupInfo, IProgress<string> progress)
        {
            var dataFilePath = $"/var/opt/mssql/data/{newDatabaseName}.mdf";
            var logFilePath = $"/var/opt/mssql/data/{newDatabaseName}_Log.ldf";

            var restoreQuery = $@"
                RESTORE DATABASE [{newDatabaseName}] 
                FROM DISK = N'{containerBackupPath}'
                WITH FILE = 1,
                     MOVE N'{backupInfo.DataLogicalName}' TO N'{dataFilePath}',
                     MOVE N'{backupInfo.LogLogicalName}' TO N'{logFilePath}',
                     NOUNLOAD,
                     REPLACE,
                     STATS = 10";

            using var command = new SqlCommand(restoreQuery, connection);
            command.CommandTimeout = 600; // 10 دقائق

            await command.ExecuteNonQueryAsync();
        }

        // تعريف واحد فقط لطريقة GetConnectionString
        private string GetConnectionString()
        {
            var builder = new SqlConnectionStringBuilder();

            // إعدادات خاصة بـ Docker
            if (_settings.ConnectionType == ConnectionType.Docker)
            {
                builder.DataSource = $"localhost,{_settings.Port}";
                builder.UserID = _settings.Username ?? "sa";
                builder.Password = _settings.Password ?? "YourStrong@Passw0rd";
                builder.IntegratedSecurity = false; // دائماً SQL Auth مع Docker
            }
            else
            {
                builder.DataSource = _settings.ServerName ?? @".\SQLEXPRESS";
                builder.IntegratedSecurity = _settings.UseWindowsAuthentication;

                if (!_settings.UseWindowsAuthentication)
                {
                    builder.UserID = _settings.Username ?? "";
                    builder.Password = _settings.Password ?? "";
                }
            }

            // إصلاح - استخدام قيمة واحدة فقط لـ ConnectTimeout
            builder.ConnectTimeout = _settings.ConnectionTimeout;
            builder.TrustServerCertificate = _settings.TrustServerCertificate;
            builder.Encrypt = _settings.Encrypt;

            var connectionString = builder.ConnectionString;
            var connectionInfo = GetConnectionInfo();
            _logger.LogInfo($"سلسلة الاتصال لـ {connectionInfo}: {connectionString.Replace(_settings.Password ?? "", "***")}");

            return connectionString;
        }

        private string GetDockerConnectionInfo()
        {
            return $"الحاوية: {_settings.DockerContainerName}, المنفذ: {_settings.Port}, المستخدم: {_settings.Username}";
        }

        /// <summary>
        /// الحصول على معلومات الاتصال حسب نوع الاتصال
        /// </summary>
        private string GetConnectionInfo()
        {
            return _settings.ConnectionType switch
            {
                ConnectionType.Docker => $"حاوية Docker: {_settings.DockerContainerName}",
                ConnectionType.Local => $"خادم محلي: {_settings.ServerName}",
                ConnectionType.Remote => $"خادم عن بُعد: {_settings.ServerName}",
                _ => $"خادم SQL Server: {_settings.ServerName}"
            };
        }

        private string GetBackupDirectory()
        {
            if (!string.IsNullOrEmpty(_settings.BackupPath) && Directory.Exists(_settings.BackupPath))
            {
                return _settings.BackupPath;
            }

            // المجلد الافتراضي حسب نوع الاتصال
            string defaultPath;
            switch (_settings.ConnectionType)
            {
                case ConnectionType.Docker:
                    defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DockerBackups");
                    break;
                case ConnectionType.Local:
                    defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LocalBackups");
                    break;
                case ConnectionType.Remote:
                    defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RemoteBackups");
                    break;
                default:
                    defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SQLBackups");
                    break;
            }

            if (!Directory.Exists(defaultPath))
            {
                Directory.CreateDirectory(defaultPath);
            }

            return defaultPath;
        }
    }
}
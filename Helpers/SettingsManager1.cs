using BackupDbServer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BackupDbServer.Helpers
{
    public static class SettingsManager
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BackupDbServer",
            "settings.json");

        /// <summary>
        /// الحصول على مسار ملف الإعدادات
        /// </summary>
        public static string GetSettingsFilePath()
        {
            return SettingsFilePath;
        }

        /// <summary>
        /// الحصول على مجلد الإعدادات
        /// </summary>
        public static string GetSettingsDirectory()
        {
            return Path.GetDirectoryName(SettingsFilePath) ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BackupDbServer");
        }

        public static void SaveSettings(BackupConfig config)
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(SettingsFilePath, json);

                // تسجيل عملية الحفظ
                var logger = new FileLogger();
                logger.LogInfo($"تم حفظ الإعدادات بنجاح:");
                logger.LogInfo($"  - DockerContainerName: {config.DockerContainerName}");
                logger.LogInfo($"  - ConnectionType: {config.ConnectionType}");
                logger.LogInfo($"  - Port: {config.Port}");
                logger.LogInfo($"  - BackupPath: {config.BackupPath}");
            }
            catch (Exception ex)
            {
                var logger = new FileLogger();
                logger.LogError($"خطأ في حفظ الإعدادات: {ex.Message}", ex);
                throw;
            }
        }

        public static BackupConfig LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    var logger = new FileLogger();
                    logger.LogInfo("ملف الإعدادات غير موجود، سيتم إنشاء إعدادات افتراضية");

                    var defaultConfig = CreateDefaultConfig();
                    SaveSettings(defaultConfig);
                    return defaultConfig;
                }

                var json = File.ReadAllText(SettingsFilePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var config = JsonSerializer.Deserialize<BackupConfig>(json, options);

                // التأكد من وجود القيم الأساسية
                if (config != null)
                {
                    config.DockerContainerName ??= "dbs-2022-CTP23-1";
                    // عدم فرض نوع الاتصال - الاحتفاظ بالقيمة المحفوظة
                    // config.ConnectionType = ConnectionType.Docker; // فرض استخدام Docker

                    var logger = new FileLogger();
                    logger.LogInfo($"تم تحميل الإعدادات بنجاح:");
                    logger.LogInfo($"  - DockerContainerName: {config.DockerContainerName}");
                    logger.LogInfo($"  - ConnectionType: {config.ConnectionType}");
                    logger.LogInfo($"  - Port: {config.Port}");

                    return config;
                }

                return CreateDefaultConfig();
            }
            catch (Exception ex)
            {
                var logger = new FileLogger();
                logger.LogError($"خطأ في تحميل الإعدادات: {ex.Message}", ex);

                // إرجاع إعدادات افتراضية في حالة الخطأ
                return CreateDefaultConfig();
            }
        }

        private static BackupConfig CreateDefaultConfig()
        {
            return new BackupConfig
            {
                Server = "localhost",
                Username = "sa",
                Password = "Passw0rd",
                UseWindowsAuthentication = false,
                BackupPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DockerBackups"),
                DockerContainerName = "dbs-2022-CTP23-1", // القيمة الافتراضية الصحيحة
                ConnectionType = ConnectionType.Local,
                Port = 1433,
                ConnectionTimeout = 30,
                TrustServerCertificate = true,
                Encrypt = false,
                SelectedDatabases = new List<string>()
            };
        }

        public static void ResetToDefaults()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    File.Delete(SettingsFilePath);
                }

                var defaultConfig = CreateDefaultConfig();
                SaveSettings(defaultConfig);

                var logger = new FileLogger();
                logger.LogInfo("تم إعادة تعيين الإعدادات إلى القيم الافتراضية");
            }
            catch (Exception ex)
            {
                var logger = new FileLogger();
                logger.LogError($"خطأ في إعادة تعيين الإعدادات: {ex.Message}", ex);
                throw;
            }
        }
    }
}
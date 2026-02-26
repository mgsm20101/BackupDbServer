using System.Collections.Generic;

namespace BackupDbServer.Models
{
    public class Settings
    {
        public string ServerName { get; set; } = "localhost";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public bool UseWindowsAuthentication { get; set; } = true;
        public string BackupPath { get; set; } = @"C:\Backups";
        public string LogPath { get; set; } = @"C:\Logs";
        public List<string> SelectedDatabases { get; set; } = new();
        
        // إعدادات سياسة الاحتفاظ بالنسخ الاحتياطية
        public bool UseRetentionPolicy { get; set; } = true;
        public int DailyRetentionDays { get; set; } = 7;
        public int WeeklyRetentionWeeks { get; set; } = 4;
        public int MonthlyRetentionMonths { get; set; } = 12;
        public bool DeleteOlderThanYear { get; set; } = false;
        
        // إعدادات جديدة لدعم Docker
        public ConnectionType ConnectionType { get; set; } = ConnectionType.Local;
        public string DockerContainerName { get; set; } = "dbs-2022-CTP23-1";
        public int Port { get; set; } = 1433;
        public bool UseCustomPort { get; set; } = false;
        public int ConnectionTimeout { get; set; } = 30;
        public bool TrustServerCertificate { get; set; } = true;
        public bool Encrypt { get; set; } = false;
   

        // خصائص لسهولة الاستخدام
        public string GetConnectionString()
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
            {
                ConnectTimeout = ConnectionTimeout,
                TrustServerCertificate = TrustServerCertificate,
                Encrypt = Encrypt
            };

            switch (ConnectionType)
            {
                case ConnectionType.Local:
                   builder.DataSource = UseCustomPort ? $"{ServerName},{Port}" : ServerName;
                    break;
                    
                case ConnectionType.Docker:
                    builder.DataSource = UseCustomPort ? $"localhost,{Port}" : "localhost,1433";
                    break;
                    
                case ConnectionType.Remote:
                    builder.DataSource = UseCustomPort ? $"{ServerName},{Port}" : ServerName;
                    break;
            }

            if (UseWindowsAuthentication)
            {
               builder.IntegratedSecurity = true;
            }
            else
            {
                // إعدادات SQL Server Authentication
                builder.IntegratedSecurity = false;
                builder.UserID = Username;
                builder.Password = Password;
                
                // إضافة إعدادات أمان إضافية لـ SQL Authentication
                builder.PersistSecurityInfo = false;
                builder.MultipleActiveResultSets = true;
            }

            return builder.ConnectionString;

        }
        
        public string GetServerDisplayName()
        {
            return ConnectionType switch
            {
                ConnectionType.Local => $"محلي: {ServerName}",
                ConnectionType.Docker => $"Docker: {DockerContainerName}",
                ConnectionType.Remote => $"بعيد: {ServerName}",
                _ => ServerName
            };
        }
    }

    public enum ConnectionType
    {
        Local,    // SQL Server محلي
        Docker,   // SQL Server على Docker
        Remote    // SQL Server بعيد
    }
}
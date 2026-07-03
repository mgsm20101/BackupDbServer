using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BackupDbServer.Helpers;
using BackupDbServer.Models;
using Microsoft.Data.SqlClient;

namespace BackupDbServer.Service
{
    /// <summary>
    /// حالة صيانة الـ XmlPath لقاعدة بيانات معيّنة.
    /// </summary>
    public enum MaintenanceState
    {
        /// <summary>القاعدة لا تحتوي SP_Company_SelectAll — الميزة غير متاحة.</summary>
        NotAvailable,
        /// <summary>الإجراء طبيعي — يُرجِع المسار الحقيقي.</summary>
        Normal,
        /// <summary>الإجراء في وضع الصيانة — يُرجِع XmlPath = NULL.</summary>
        Maintenance
    }

    /// <summary>
    /// خدمة إيقاف/استرجاع عمل XmlPath عبر تعديل الإجراء المخزّن SP_Company_SelectAll.
    ///
    /// الآلية (معتمدة): استبدال جسم الإجراء كاملاً.
    /// - التفعيل: حفظ التعريف الأصلي في جدول BDS_MaintenanceBackup داخل قاعدة العميل،
    ///   ثم ALTER للإجراء بنسخة تُرجِع NULL لعمود XmlPath مع علامة /*BDS_MAINTENANCE_ON*/.
    /// - الاسترجاع: إعادة التعريف الأصلي المحفوظ.
    /// - كشف الحالة: فحص وجود العلامة في تعريف الإجراء.
    /// </summary>
    public class XmlMaintenanceService
    {
        private const string ProcName = "dbo.SP_Company_SelectAll";
        private const string Marker = "/*BDS_MAINTENANCE_ON*/";
        private const string BackupTable = "dbo.BDS_MaintenanceBackup";
        private const int CommandTimeoutSeconds = 120;

        private readonly ILogger _logger;

        public XmlMaintenanceService(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>كشف الحالة الحالية لقاعدة بيانات.</summary>
        public async Task<MaintenanceState> GetStateAsync(string databaseName)
        {
            using var connection = new SqlConnection(BuildConnectionString(databaseName));
            await connection.OpenAsync();

            var definition = await GetProcDefinitionAsync(connection);
            if (definition == null)
                return MaintenanceState.NotAvailable;

            return definition.Contains(Marker) ? MaintenanceState.Maintenance : MaintenanceState.Normal;
        }

        /// <summary>تفعيل الصيانة: جعل XmlPath يُرجِع NULL.</summary>
        public async Task EnableMaintenanceAsync(string databaseName)
        {
            using var connection = new SqlConnection(BuildConnectionString(databaseName));
            await connection.OpenAsync();

            var original = await GetProcDefinitionAsync(connection);
            if (original == null)
                throw new InvalidOperationException($"قاعدة البيانات '{databaseName}' لا تحتوي على الإجراء {ProcName}.");

            if (original.Contains(Marker))
            {
                _logger.LogInformation($"[XmlMaintenance] '{databaseName}' بالفعل في وضع الصيانة — لا تغيير.");
                return;
            }

            // 1) ضمان وجود جدول الحفظ وحفظ التعريف الأصلي إن لم يكن محفوظاً
            await EnsureBackupTableAsync(connection);
            await SaveOriginalIfMissingAsync(connection, original);

            // 2) توليد نسخة الصيانة (XmlPath = NULL + علامة)
            var maintenanceDefinition = BuildMaintenanceDefinition(original);

            // 3) تطبيق ALTER
            await ExecuteNonQueryAsync(connection, maintenanceDefinition);

            _logger.LogInformation($"[XmlMaintenance] تم تفعيل صيانة XmlPath على '{databaseName}'.");
        }

        /// <summary>استرجاع الإجراء الأصلي وإلغاء الصيانة.</summary>
        public async Task DisableMaintenanceAsync(string databaseName)
        {
            using var connection = new SqlConnection(BuildConnectionString(databaseName));
            await connection.OpenAsync();

            var original = await GetSavedOriginalAsync(connection);
            if (original == null)
            {
                // لا يوجد أصل محفوظ — تحقق إن كان الإجراء أصلاً غير معدّل
                var current = await GetProcDefinitionAsync(connection);
                if (current == null)
                    throw new InvalidOperationException($"قاعدة البيانات '{databaseName}' لا تحتوي على الإجراء {ProcName}.");
                if (!current.Contains(Marker))
                {
                    _logger.LogInformation($"[XmlMaintenance] '{databaseName}' ليست في وضع صيانة — لا تغيير.");
                    return;
                }
                throw new InvalidOperationException(
                    $"تعذّر الاسترجاع: لا يوجد تعريف أصلي محفوظ في {BackupTable} لقاعدة '{databaseName}'.");
            }

            var restoreDefinition = ToAlter(original);
            await ExecuteNonQueryAsync(connection, restoreDefinition);

            _logger.LogInformation($"[XmlMaintenance] تم استرجاع XmlPath الأصلي على '{databaseName}'.");
        }

        // ===== أدوات داخلية =====

        private static async Task<string?> GetProcDefinitionAsync(SqlConnection connection)
        {
            using var cmd = new SqlCommand($"SELECT OBJECT_DEFINITION(OBJECT_ID('{ProcName}'))", connection)
            {
                CommandTimeout = CommandTimeoutSeconds
            };
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? null : (string)result;
        }

        private static async Task EnsureBackupTableAsync(SqlConnection connection)
        {
            const string sql = @"
IF OBJECT_ID('dbo.BDS_MaintenanceBackup', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BDS_MaintenanceBackup
    (
        ObjectName         NVARCHAR(256) NOT NULL PRIMARY KEY,
        OriginalDefinition NVARCHAR(MAX) NOT NULL,
        SavedAtUtc         DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
    );
END";
            await ExecuteNonQueryAsync(connection, sql);
        }

        private static async Task SaveOriginalIfMissingAsync(SqlConnection connection, string originalDefinition)
        {
            const string sql = @"
IF NOT EXISTS (SELECT 1 FROM dbo.BDS_MaintenanceBackup WHERE ObjectName = @name)
    INSERT INTO dbo.BDS_MaintenanceBackup (ObjectName, OriginalDefinition)
    VALUES (@name, @def);";
            using var cmd = new SqlCommand(sql, connection) { CommandTimeout = CommandTimeoutSeconds };
            cmd.Parameters.AddWithValue("@name", ProcName);
            cmd.Parameters.AddWithValue("@def", originalDefinition);
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task<string?> GetSavedOriginalAsync(SqlConnection connection)
        {
            const string sql = @"
IF OBJECT_ID('dbo.BDS_MaintenanceBackup', 'U') IS NULL
    SELECT CAST(NULL AS NVARCHAR(MAX));
ELSE
    SELECT OriginalDefinition FROM dbo.BDS_MaintenanceBackup WHERE ObjectName = @name;";
            using var cmd = new SqlCommand(sql, connection) { CommandTimeout = CommandTimeoutSeconds };
            cmd.Parameters.AddWithValue("@name", ProcName);
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? null : (string)result;
        }

        /// <summary>
        /// توليد نسخة الصيانة: تحويل CREATE→ALTER واستبدال سطر إسقاط [XmlPath]
        /// بإدراج '1' قبل الشرطة المائلة الأخيرة (مثال: ...\open\ ← ...\open1\)،
        /// أو إضافته في النهاية إن لم ينتهِ المسار بشرطة. يحافظ على نوع NVARCHAR وغير فارغ.
        /// يستهدف سطر الإخراج فقط (لا يمسّ @XmlPath ولا Company.[XmlPath] في WHERE).
        /// </summary>
        private static string BuildMaintenanceDefinition(string originalDefinition)
        {
            var altered = ToAlter(originalDefinition);

            var projectionPattern = new Regex(@"(?m)^(\s*)\[XmlPath\],\s*$");
            if (!projectionPattern.IsMatch(altered))
            {
                throw new InvalidOperationException(
                    "تعذّر تحديد عمود [XmlPath] في قائمة الإخراج للإجراء — تم إيقاف العملية لتجنّب إتلاف الإجراء.");
            }

            // إدراج '1' قبل الشرطة المائلة الأخيرة (open\ ← open1\)، أو الإضافة في النهاية لو بلا شرطة
            const string maintenanceProjection =
                "CASE WHEN RIGHT([XmlPath],1)=N'\\' " +
                "THEN STUFF([XmlPath],LEN([XmlPath]),0,N'1') " +
                "ELSE [XmlPath]+N'1' END AS [XmlPath], ";

            return projectionPattern.Replace(altered, "${1}" + maintenanceProjection + Marker, 1);
        }

        /// <summary>تحويل تعريف الإجراء من CREATE PROC[EDURE] إلى ALTER PROCEDURE (أول مطابقة فقط).</summary>
        private static string ToAlter(string definition)
        {
            var regex = new Regex(@"CREATE\s+PROC(EDURE)?", RegexOptions.IgnoreCase);
            if (regex.IsMatch(definition))
            {
                return regex.Replace(definition, "ALTER PROCEDURE", 1);
            }
            // إن كان التعريف بالفعل ALTER فلا تغيير
            return definition;
        }

        private static async Task ExecuteNonQueryAsync(SqlConnection connection, string sql)
        {
            using var cmd = new SqlCommand(sql, connection) { CommandTimeout = CommandTimeoutSeconds };
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// بناء سلسلة الاتصال لقاعدة محددة (نمط DailyTaskService) — يضبط InitialCatalog.
        /// </summary>
        private static string BuildConnectionString(string databaseName)
        {
            var config = SettingsManager.LoadSettings();
            var builder = new SqlConnectionStringBuilder
            {
                ConnectTimeout = config.ConnectionTimeout > 0 ? config.ConnectionTimeout : 30,
                TrustServerCertificate = config.TrustServerCertificate,
                Encrypt = config.Encrypt,
                InitialCatalog = databaseName
            };

            switch (config.ConnectionType)
            {
                case ConnectionType.Local:
                    builder.DataSource = config.Port != 1433
                        ? $"{config.Server},{config.Port}"
                        : (string.IsNullOrEmpty(config.Server) ? "localhost" : config.Server);
                    break;
                case ConnectionType.Docker:
                    builder.DataSource = config.Port != 1433 ? $"localhost,{config.Port}" : "localhost,1433";
                    break;
                case ConnectionType.Remote:
                    builder.DataSource = config.Port != 1433 ? $"{config.Server},{config.Port}" : config.Server;
                    break;
            }

            if (config.UseWindowsAuthentication)
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.UserID = config.Username;
                builder.Password = config.Password;
            }

            return builder.ConnectionString;
        }
    }
}

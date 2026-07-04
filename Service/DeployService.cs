using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Xml;
using Microsoft.Web.Administration;
using BackupDbServer.Models;
using BackupDbServer.Helpers;

namespace BackupDbServer.Services
{
    public class DeployService
    {
        private readonly ILogger _logger;

        public DeployService(ILogger logger)
        {
            _logger = logger;
        }

        public void DeployApplication(DeployConfig cfg)
        {
            try
            {
                _logger.LogInformation("==[1/7] استعادة قاعدة البيانات من .bak ==");
                RestoreDatabaseFromBak(cfg);

                _logger.LogInformation("==[2/7] تحديث جدول الشركة بالاسم الجديد ==");
                UpdateCompanyName(cfg);

                _logger.LogInformation("==[3/7] إضافة سطر ترخيص في قاعدة بيانات اللايسنز ==");
                InsertLicenseRow(cfg);

                _logger.LogInformation("==[4/7] فك ضغط الحزمة إلى مجلد الشركة ==");
                var physicalPath = ExtractToCompanyFolder(cfg);

                _logger.LogInformation("==[5/7] تحديث web.config (ConnectionString + FolderName) ==");
                UpdateWebConfig(physicalPath, cfg);

                _logger.LogInformation("==[6/7] منح صلاحيات IIS_IUSRS على المجلد ==");
                GrantIisPermissions(physicalPath);

                _logger.LogInformation("==[7/7] إنشاء Sub-Application على IIS وتشغيله على DefaultAppPool ==");
                CreateIisApplication(cfg, physicalPath);

                _logger.LogInformation("تمّ كل شيء بنجاح ✅");
            }
            catch (Exception ex)
            {
                _logger.LogError($"فشل النشر ❌: {ex.Message}", ex);
                throw;
            }
        }

        // ====== SQL Helpers ======
        private string MasterConn(DeployConfig cfg)
        {
            var b = new SqlConnectionStringBuilder
            {
                DataSource = cfg.SqlServer,
                InitialCatalog = "master",
                IntegratedSecurity = cfg.UseIntegratedSecurity
            };
            if (!cfg.UseIntegratedSecurity)
            {
                b.UserID = cfg.SqlAuthUser;
                b.Password = cfg.SqlAuthPassword;
                b.TrustServerCertificate = true;
            }
            return b.ToString();
        }

        private string DbConn(DeployConfig cfg, string dbName)
        {
            var b = new SqlConnectionStringBuilder
            {
                DataSource = cfg.SqlServer,
                InitialCatalog = dbName,
                IntegratedSecurity = cfg.UseIntegratedSecurity
            };
            if (!cfg.UseIntegratedSecurity)
            {
                b.UserID = cfg.SqlAuthUser;
                b.Password = cfg.SqlAuthPassword;
                b.TrustServerCertificate = true;
            }
            return b.ToString();
        }

        private void RestoreDatabaseFromBak(DeployConfig cfg)
        {
            using var cn = new SqlConnection(MasterConn(cfg));
            cn.Open();

            // 1) الحصول على الأسماء المنطقية للملفات داخل النسخة
            DataTable fileList = new DataTable();
            using (var cmd = new SqlCommand($"RESTORE FILELISTONLY FROM DISK = @bak", cn))
            {
                cmd.Parameters.AddWithValue("@bak", cfg.BakPath);
                using var rdr = cmd.ExecuteReader();
                fileList.Load(rdr);
            }

            var dataFile = fileList.AsEnumerable().First(r => r.Field<string>("Type") == "D");
            var logFile = fileList.AsEnumerable().First(r => r.Field<string>("Type") == "L");

            string logicalData = dataFile.Field<string>("LogicalName");
            string logicalLog = logFile.Field<string>("LogicalName");

            string mdfPath = Path.Combine(cfg.DataFilesDir, $"{cfg.NewDatabaseName}.mdf");
            string ldfPath = Path.Combine(cfg.DataFilesDir, $"{cfg.NewDatabaseName}_log.ldf");

            // 2) قتل اتصالات إن وجد DB بنفس الاسم
            using (var kill = new SqlCommand($@"
IF DB_ID(@db) IS NOT NULL
BEGIN
    ALTER DATABASE [{cfg.NewDatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
END
", cn))
            {
                kill.Parameters.AddWithValue("@db", cfg.NewDatabaseName);
                kill.ExecuteNonQuery();
            }

            // 3) RESTORE WITH MOVE (+ WITH REPLACE إن لزم)
            var replace = cfg.WithReplace ? "REPLACE," : "";
            string restoreSql = $@"
RESTORE DATABASE [{cfg.NewDatabaseName}]
FROM DISK = @bak
WITH MOVE @ld TO @mdf,
     MOVE @ll TO @ldf,
     {replace}
     STATS = 5;
ALTER DATABASE [{cfg.NewDatabaseName}] SET MULTI_USER;
";
            using (var cmd = new SqlCommand(restoreSql, cn))
            {
                cmd.Parameters.AddWithValue("@bak", cfg.BakPath);
                cmd.Parameters.AddWithValue("@ld", logicalData);
                cmd.Parameters.AddWithValue("@mdf", mdfPath);
                cmd.Parameters.AddWithValue("@ll", logicalLog);
                cmd.Parameters.AddWithValue("@ldf", ldfPath);
                cmd.CommandTimeout = 0; // في حال الملفات كبيرة
                cmd.ExecuteNonQuery();
            }
        }

        private void UpdateCompanyName(DeployConfig cfg)
        {
            string sql = $@"
UPDATE {cfg.CompanyTable}
SET {cfg.CompanyNameColumn} = @name
";
            using var cn = new SqlConnection(DbConn(cfg, cfg.NewDatabaseName));
            cn.Open();
            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@name", cfg.NewCompanyDisplayName);
            cmd.Parameters.AddWithValue("@id", cfg.CompanyKeyValue);
            int rows = cmd.ExecuteNonQuery();
            _logger.LogInformation($"تم تحديث {rows} صف/صفوف في جدول الشركة.");
        }

        private void InsertLicenseRow(DeployConfig cfg)
        {
            string sql = $@"
INSERT INTO {cfg.LicenseTable} ({cfg.LicenseCompanyIdColumn}, {cfg.LicenseKeyColumn}, {cfg.LicenseExpiryColumn})
VALUES (@companyId, @key, @expiry);
";
            using var cn = new SqlConnection(DbConn(cfg, cfg.LicenseDbName));
            cn.Open();
            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@companyId", cfg.CompanyKeyValue);
            cmd.Parameters.AddWithValue("@key", cfg.LicenseKeyValue);
            cmd.Parameters.AddWithValue("@expiry", cfg.LicenseExpiry);
            cmd.ExecuteNonQuery();
        }

        // ====== File/Config Helpers ======
        private string ExtractToCompanyFolder(DeployConfig cfg)
        {
            string target = Path.Combine(cfg.WwwRootBase, cfg.CompanyFolderName);
            if (Directory.Exists(target))
            {
                _logger.LogInformation("المجلد موجود مسبقًا، سيتم تنظيفه...");
                Directory.Delete(target, true);
            }
            Directory.CreateDirectory(target);
            ZipFile.ExtractToDirectory(cfg.ZipPackagePath, target);
            return target;
        }

        private void UpdateWebConfig(string physicalPath, DeployConfig cfg)
        {
            string webConfig = Path.Combine(physicalPath, "web.config");
            if (!File.Exists(webConfig))
                throw new FileNotFoundException("لم يتم العثور على web.config داخل الحزمة!", webConfig);

            var doc = new XmlDocument();
            doc.Load(webConfig);

            // تحديث connectionStrings/ add[@name='DefaultConnection']
            var connStrings = doc.SelectSingleNode("/configuration/connectionStrings");
            if (connStrings == null)
                throw new InvalidOperationException("لا يوجد قسم connectionStrings في web.config");

            var connNode = connStrings.SelectSingleNode($"add[@name='{cfg.ConnectionStringName}']") as XmlElement;
            if (connNode == null)
            {
                connNode = doc.CreateElement("add");
                connNode.SetAttribute("name", cfg.ConnectionStringName);
                connStrings.AppendChild(connNode);
            }

            // مثال لسلسلة الاتصال (SqlAuth أو Integrated Security)
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = cfg.SqlServer,
                InitialCatalog = cfg.NewDatabaseName,
                IntegratedSecurity = cfg.UseIntegratedSecurity,
                TrustServerCertificate = true,
                MultipleActiveResultSets = true
            };
            if (!cfg.UseIntegratedSecurity)
            {
                builder.UserID = cfg.SqlAuthUser;
                builder.Password = cfg.SqlAuthPassword;
            }
            connNode.SetAttribute("connectionString", builder.ToString());
            connNode.SetAttribute("providerName", "Microsoft.Data.SqlClient");

            // تحديث appSettings key (اختياري)
            if (!string.IsNullOrWhiteSpace(cfg.AppSettingsFolderKey))
            {
                var appSettings = doc.SelectSingleNode("/configuration/appSettings");
                if (appSettings != null)
                {
                    var keyNode = appSettings.SelectSingleNode($"add[@key='{cfg.AppSettingsFolderKey}']") as XmlElement;
                    if (keyNode == null)
                    {
                        keyNode = doc.CreateElement("add");
                        keyNode.SetAttribute("key", cfg.AppSettingsFolderKey);
                        appSettings.AppendChild(keyNode);
                    }
                    keyNode.SetAttribute("value", cfg.CompanyFolderName);
                }
            }

            doc.Save(webConfig);
        }

        private void GrantIisPermissions(string path)
        {
            var dirInfo = new DirectoryInfo(path);
            var dirSec = dirInfo.GetAccessControl();
            var iisUsers = new SecurityIdentifier(WellKnownSidType.WinBuiltinIUsersSid, null); // بديل بسيط
            var iisIusrs = new NTAccount("IIS_IUSRS");
            dirSec.AddAccessRule(new FileSystemAccessRule(iisIusrs, FileSystemRights.Modify, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            dirInfo.SetAccessControl(dirSec);
        }

        // ====== IIS ======
        private void CreateIisApplication(DeployConfig cfg, string physicalPath)
        {
            using var server = new ServerManager();

            var site = server.Sites.FirstOrDefault(s => s.Name.Equals(cfg.IisSiteName, StringComparison.OrdinalIgnoreCase));
            if (site == null) throw new InvalidOperationException($"لم يتم العثور على موقع IIS باسم: {cfg.IisSiteName}");

            // احذف التطبيق إن كان موجودًا بنفس الاسم
            var existing = site.Applications.FirstOrDefault(a => a.Path.Equals("/" + cfg.IisAppAlias, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                site.Applications.Remove(existing);
            }

            var app = site.Applications.Add("/" + cfg.IisAppAlias, physicalPath);
            app.ApplicationPoolName = cfg.AppPoolName;

            // تأكد من وجود AppPool
            var pool = server.ApplicationPools.FirstOrDefault(p => p.Name.Equals(cfg.AppPoolName, StringComparison.OrdinalIgnoreCase));
            if (pool == null)
            {
                pool = server.ApplicationPools.Add(cfg.AppPoolName);
                pool.ManagedRuntimeVersion = "v4.0";
            }

            server.CommitChanges();
        }
    }
}

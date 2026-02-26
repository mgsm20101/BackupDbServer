using System;

namespace BackupDbServer.Models
{
    public class DeployConfig
    {
        // ========= إعدادات عامّة =========
        public string SqlServer { get; set; } = @"(local)";          // اسم/مثيل SQL
        public string SqlAuthUser { get; set; } = "sa";              // اتركه فارغًا إن كنت تستخدم Windows Auth
        public string SqlAuthPassword { get; set; } = "YourStrong#Pass!";
        public bool UseIntegratedSecurity { get; set; } = false;

        // ========= مسارات واستعادة DB =========
        public string BakPath { get; set; } = @"D:\backups\app_template.bak";
        public string NewDatabaseName { get; set; } = "App_Contoso";
        public string DataFilesDir { get; set; } = @"D:\SqlData";    // مجلد الـ MDF/LDF
        public bool WithReplace { get; set; } = true;

        // ========= تحديث جدول الشركة =========
        public string CompanyTable { get; set; } = "dbo.Company";
        public string CompanyNameColumn { get; set; } = "Name";
        public string CompanyKeyColumn { get; set; } = "Id";         // عدّل حسب تصميمك
        public int CompanyKeyValue { get; set; } = 1;                // السجل المطلوب تحديثه
        public string NewCompanyDisplayName { get; set; } = "شركة كونتوسو";

        // ========= إضافة ترخيص في DB أخرى =========
        public string LicenseDbName { get; set; } = "LicensingDB";
        public string LicenseTable { get; set; } = "dbo.Licenses";
        public string LicenseCompanyIdColumn { get; set; } = "CompanyId";
        public string LicenseKeyColumn { get; set; } = "LicenseKey";
        public string LicenseExpiryColumn { get; set; } = "ExpiryDate";
        public string LicenseKeyValue { get; set; } = "LIC-CTX-2025-0001";
        public DateTime LicenseExpiry { get; set; } = DateTime.UtcNow.AddYears(1);

        // ========= فك الضغط و IIS =========
        public string ZipPackagePath { get; set; } = @"D:\packages\app.zip"; // يحتوي الموقع (web.config بالداخل)
        public string WwwRootBase { get; set; } = @"C:\inetpub\wwwroot\Clients";
        public string CompanyFolderName { get; set; } = "Contoso";           // سيصبح المسار C:\inetpub\wwwroot\Clients\Contoso
        public string IisSiteName { get; set; } = "Default Web Site";
        public string IisAppAlias { get; set; } = "Contoso";                 // سيظهر كـ http://server/Contoso
        public string AppPoolName { get; set; } = "DefaultAppPool";

        // ========= تحديث web.config =========
        public string ConnectionStringName { get; set; } = "DefaultConnection"; // عدله حسب مشروعك
        public string AppSettingsFolderKey { get; set; } = "CompanyFolder";     // مفتاح في appSettings (اختياري)
    }
}

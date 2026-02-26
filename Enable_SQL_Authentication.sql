-- تفعيل SQL Server Authentication (Mixed Mode)
-- قم بتشغيل هذا السكريبت في SSMS باستخدام Windows Authentication

USE [master]
GO

PRINT '=== تفعيل SQL Server Authentication ==='

-- 1. تفعيل Mixed Mode Authentication
EXEC xp_instance_regwrite 
    N'HKEY_LOCAL_MACHINE', 
    N'Software\Microsoft\MSSQLServer\MSSQLServer', 
    N'LoginMode', 
    REG_DWORD, 
    2
GO

PRINT '✅ تم تفعيل Mixed Mode Authentication'

-- 2. تفعيل حساب sa
ALTER LOGIN [sa] ENABLE
GO

PRINT '✅ تم تفعيل حساب sa'

-- 3. تعيين كلمة مرور قوية لـ sa
ALTER LOGIN [sa] WITH PASSWORD = 'StrongP@ssw0rd2024!'
GO

PRINT '✅ تم تعيين كلمة مرور قوية لحساب sa'

-- 4. فحص الإعدادات الحالية
PRINT ''
PRINT '=== فحص الإعدادات الحالية ==='

SELECT 
    'Authentication Mode' AS Setting,
    CASE SERVERPROPERTY('IsIntegratedSecurityOnly')
        WHEN 1 THEN 'Windows Authentication Only ❌'
        WHEN 0 THEN 'Mixed Mode Authentication ✅'
    END AS Value

UNION ALL

SELECT 
    'sa Account Status' AS Setting,
    CASE 
        WHEN is_disabled = 0 THEN 'Enabled ✅'
        ELSE 'Disabled ❌'
    END AS Value
FROM sys.sql_logins 
WHERE name = 'sa'

-- 5. إنشاء حساب احتياطي للتطبيق
IF NOT EXISTS (SELECT * FROM sys.sql_logins WHERE name = 'BackupAppUser')
BEGIN
    CREATE LOGIN [BackupAppUser] WITH PASSWORD = 'BackupApp@2024!', 
                                      DEFAULT_DATABASE = [master],
                                      CHECK_EXPIRATION = OFF,
                                      CHECK_POLICY = OFF
    
    -- إضافة الحساب لدور sysadmin (للنسخ الاحتياطي)
    ALTER SERVER ROLE [sysadmin] ADD MEMBER [BackupAppUser]
    
    PRINT '✅ تم إنشاء حساب BackupAppUser'
END
ELSE
BEGIN
    PRINT '⚠️ حساب BackupAppUser موجود مسبقاً'
END

PRINT ''
PRINT '🔄 مطلوب: إعادة تشغيل SQL Server Service لتفعيل Mixed Mode'
PRINT ''
PRINT '📋 معلومات الاتصال للتطبيق:'
PRINT 'Username: sa'
PRINT 'Password: StrongP@ssw0rd2024!'
PRINT 'أو'
PRINT 'Username: BackupAppUser'
PRINT 'Password: BackupApp@2024!'
PRINT ''
PRINT '🧪 اختبار الاتصال:'
PRINT 'sqlcmd -S localhost -U sa -P "StrongP@ssw0rd2024!"'

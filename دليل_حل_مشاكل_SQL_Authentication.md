# دليل حل مشاكل الاتصال بـ SQL Server

## المشكلة: الاتصال بقاعدة البيانات لا ينجح إلا عن طريق Windows Authentication فقط

### الأسباب المحتملة وطرق الحل:

## 1. 🔐 SQL Server Authentication غير مُفعل

### التحقق:
1. افتح **SQL Server Management Studio (SSMS)**
2. اتصل بـ SQL Server باستخدام Windows Authentication
3. انقر بزر الماوس الأيمن على اسم الخادم → **Properties**
4. انتقل إلى **Security**
5. تحقق من أن **SQL Server and Windows Authentication mode** محدد

### الحل:
```sql
-- تفعيل Mixed Mode Authentication
USE [master]
GO
EXEC xp_instance_regwrite N'HKEY_LOCAL_MACHINE', 
    N'Software\Microsoft\MSSQLServer\MSSQLServer', 
    N'LoginMode', REG_DWORD, 2
GO
```

**⚠️ مهم:** بعد تنفيذ هذا الأمر، **أعد تشغيل SQL Server Service**

## 2. 👤 حساب 'sa' غير مُفعل أو كلمة المرور خاطئة

### التحقق والحل:
```sql
-- تفعيل حساب sa
ALTER LOGIN [sa] ENABLE
GO

-- تغيير كلمة مرور sa
ALTER LOGIN [sa] WITH PASSWORD = 'Passw0rd'
GO

-- التأكد من أن الحساب مُفعل
SELECT name, is_disabled FROM sys.sql_logins WHERE name = 'sa'
GO
```

## 3. 🌐 إعدادات الشبكة (Network Configuration)

### التحقق:
1. افتح **SQL Server Configuration Manager**
2. انتقل إلى **SQL Server Network Configuration** → **Protocols for [Instance Name]**
3. تأكد من تفعيل:
   - **TCP/IP** (Enabled)
   - **Named Pipes** (Enabled)

### إعدادات TCP/IP:
1. انقر بزر الماوس الأيمن على **TCP/IP** → **Properties**
2. في تبويب **IP Addresses**:
   - **IPAll** → **TCP Port**: 1433
   - **Enabled**: Yes
   - **Active**: Yes

**⚠️ مهم:** أعد تشغيل SQL Server Service بعد أي تغيير

## 4. 🔥 إعدادات Windows Firewall

### الحل:
1. افتح **Windows Firewall with Advanced Security**
2. أضف قاعدة جديدة للمنفذ 1433:
   - **Inbound Rules** → **New Rule**
   - **Port** → **TCP** → **1433**
   - **Allow the connection**

أو استخدم PowerShell:
```powershell
New-NetFirewallRule -DisplayName "SQL Server" -Direction Inbound -Protocol TCP -LocalPort 1433 -Action Allow
```

## 5. 🔍 SQL Server Browser Service

### التحقق والحل:
1. افتح **Services** (services.msc)
2. ابحث عن **SQL Server Browser**
3. تأكد من أنه **Running** و **Startup Type** = **Automatic**

أو استخدم PowerShell:
```powershell
Set-Service "SQLBrowser" -StartupType Automatic
Start-Service "SQLBrowser"
```

## 6. 🧪 اختبار الاتصال خطوة بخطوة

### 1. اختبار محلي بـ SSMS:
```
Server: localhost
Authentication: SQL Server Authentication
Login: sa
Password: Passw0rd
```

### 2. اختبار بـ sqlcmd:
```cmd
sqlcmd -S localhost -U sa -P Passw0rd
```

### 3. اختبار Connection String:
```
Server=localhost;Database=master;User Id=sa;Password=Passw0rd;TrustServerCertificate=true;
```

## 7. 📝 SQL Scripts للتشخيص

### فحص إعدادات الأمان:
```sql
-- فحص نمط المصادقة
SELECT 
    CASE SERVERPROPERTY('IsIntegratedSecurityOnly')
        WHEN 1 THEN 'Windows Authentication Only'
        WHEN 0 THEN 'Mixed Mode Authentication'
    END AS [Authentication Mode]

-- فحص حالة حساب sa
SELECT name, is_disabled, create_date, modify_date 
FROM sys.sql_logins 
WHERE name = 'sa'

-- فحص الاتصالات النشطة
SELECT 
    session_id,
    login_name,
    host_name,
    program_name,
    login_time
FROM sys.dm_exec_sessions
WHERE is_user_process = 1
```

## 8. 🛠️ حلول للمشاكل الشائعة

### خطأ: "Login failed for user 'sa'"
- **السبب**: كلمة المرور خاطئة أو الحساب مُعطل
- **الحل**: أعد تعيين كلمة المرور وتفعيل الحساب

### خطأ: "A network-related or instance-specific error"
- **السبب**: مشكلة في الشبكة أو الخدمة متوقفة
- **الحل**: تحقق من تشغيل SQL Server وإعدادات Firewall

### خطأ: "The server was not found or was not accessible"
- **السبب**: اسم الخادم خاطئ أو SQL Server Browser متوقف
- **الحل**: تحقق من اسم الخادم وتشغيل SQL Browser

## 9. 🔧 إعدادات التطبيق الموصى بها

### للبيئة المحلية (Local):
```json
{
  "server": "localhost",
  "useWindowsAuthentication": false,
  "username": "sa",
  "password": "Passw0rd",
  "port": 1433,
  "trustServerCertificate": true,
  "encrypt": false
}
```

### للبيئة المؤسسية (Domain):
```json
{
  "server": "SERVER_NAME\\INSTANCE_NAME",
  "useWindowsAuthentication": true,
  "port": 1433,
  "trustServerCertificate": true,
  "encrypt": true
}
```

## 10. ✅ قائمة فحص سريعة

- [ ] SQL Server Authentication مُفعل (Mixed Mode)
- [ ] حساب 'sa' مُفعل مع كلمة مرور صحيحة
- [ ] TCP/IP Protocol مُفعل على المنفذ 1433
- [ ] Windows Firewall يسمح بالمنفذ 1433
- [ ] SQL Server Browser Service يعمل
- [ ] SQL Server Service يعمل
- [ ] اختبار الاتصال بـ SSMS نجح
- [ ] Connection String صحيحة في التطبيق

---
**💡 نصيحة:** ابدأ بتجربة Windows Authentication أولاً للتأكد من أن SQL Server يعمل، ثم انتقل لإعداد SQL Authentication.

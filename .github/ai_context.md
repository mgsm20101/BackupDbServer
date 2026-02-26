# AI Context - BackupDbServer

## 📋 نظرة عامة على المشروع

**BackupDbServer** هو تطبيق WPF متقدم لإدارة النسخ الاحتياطي والاستعادة لقواعد بيانات SQL Server مع دعم Docker والاتصالات البعيدة.

### الهدف الأساسي
توفير حل شامل لإدارة قواعد بيانات SQL Server يشمل:
- النسخ الاحتياطي اليدوي والتلقائي
- النسخ المجدول بالوقت
- نشر تطبيقات IIS تلقائياً
- المهام المجدولة اليومية على قواعد البيانات
- رفع النسخ إلى Google Drive
- دعم Docker والاتصالات البعيدة

---

## 🏗️ البنية المعمارية

### النمط المعماري
- **MVVM Pattern** (Model-View-ViewModel)
- **Service Layer** لمنطق الأعمال
- **ServiceContainer Pattern** (Singleton) للإدارة المركزية للخدمات ⭐ NEW
- **Repository Pattern** عبر SettingsManager
- **Dependency Injection** في بعض الأجزاء

### ⚡ ServiceContainer - الإدارة المركزية للخدمات (NEW)

**الموقع:** `Helpers/ServiceContainer.cs`

**الغرض:** نقطة واحدة مركزية لإدارة جميع الخدمات المشتركة في التطبيق (Singleton Pattern).

**الخدمات المُدارة:**
```csharp
public static class ServiceContainer
{
    // خدمات Singleton
    public static ILogger Logger { get; }                          // FileLogger واحد للتطبيق
    public static BackupRestoreService BackupService { get; }      // خدمة النسخ/الاستعادة
    public static GoogleDriveServiceFixed GoogleDriveService { get; } // خدمة Google Drive
    public static IAutoBackupService AutoBackupService { get; }    // خدمة النسخ التلقائي
    public static IScheduledBackupService ScheduledBackupService { get; } // خدمة النسخ المجدول
    public static DailyTaskService DailyTaskService { get; }       // خدمة المهام اليومية
    
    // إدارة دورة الحياة
    public static void Initialize();    // تهيئة عند بدء التطبيق
    public static void Shutdown();      // إيقاف آمن عند الإغلاق
    public static void Reset();         // إعادة تعيين للاختبارات
}
```

**المزايا:**
- ✅ نسخة واحدة فقط من كل خدمة (Singleton)
- ✅ Thread-Safe (محمي بـ `lock`)
- ✅ إدارة مركزية لدورة الحياة
- ✅ تقليل استهلاك الذاكرة
- ✅ حالة متسقة عبر التطبيق
- ✅ سهولة الاختبار

**الاستخدام في ViewModels:**
```csharp
// ✅ الطريقة الصحيحة (NEW)
public class MyViewModel : ViewModelBase
{
    private ILogger _logger;
    private BackupRestoreService _backupService;
    
    public MyViewModel()
    {
        _logger = ServiceContainer.Logger;
        _backupService = ServiceContainer.BackupService;
    }
}

// ❌ الطريقة القديمة (لا تستخدمها)
// _logger = new FileLogger();  // ممنوع!
// _backupService = new BackupRestoreService();  // ممنوع!
```

**التكامل مع App.xaml.cs:**
```csharp
protected override void OnStartup(StartupEventArgs e)
{
    ServiceContainer.Initialize();  // تهيئة جميع الخدمات
    // ...
}

protected override void OnExit(ExitEventArgs e)
{
    ServiceContainer.Shutdown();    // إيقاف جميع الخدمات
    // ...
}
```

**⚠️ قواعد مهمة:**
1. **لا تُنشئ خدمات محلياً:** استخدم دائماً ServiceContainer
2. **لا تحفظ نسخ متعددة:** الخدمات Singleton
3. **استخدم في الـ Constructor:** احصل على الخدمات مرة واحدة فقط

### التقنيات المستخدمة

#### Frontend
- **.NET 8.0** (net8.0-windows10.0.17763.0)
- **WPF (Windows Presentation Foundation)**
- **XAML** للواجهات
- **Material Design** للأيقونات

#### Backend
- **Microsoft.Data.SqlClient** (مكتبة SQL الحديثة)
- **Docker.DotNet** للتكامل مع Docker
- **Microsoft.Web.Administration** لإدارة IIS
- **Google.Apis.Drive.v3** للرفع على Drive

#### Storage
- **JSON** لتخزين الإعدادات (System.Text.Json)
- **SQL Server** كقاعدة بيانات رئيسية
- **File System** للنسخ الاحتياطية

---

## 📁 هيكل المشروع

```
BackupDbServer/
├── .github/
│   ├── copilot-instructions.md    # بروتوكول التشغيل الصارم
│   └── ai_context.md               # هذا الملف
├── Converters/                     # محولات XAML
│   ├── BooleanToVisibilityConverter.cs
│   ├── BoolToRunningTextConverter.cs
│   ├── BoolToTextConverter.cs
│   ├── InvertedBooleanConverter.cs
│   ├── StringToBooleanConverter.cs
│   └── TimeOnlyToStringConverter.cs
├── Helpers/                        # أدوات مساعدة
│   ├── AsyncRelayCommand.cs        # أوامر Async
│   ├── BackupConfig.cs             # نموذج الإعدادات
│   ├── BackupService.cs            # خدمة النسخ القديمة (Obsolete)
│   ├── compress.cs                 # CompressionHelper
│   ├── CrashProtectionManager.cs   # حماية من التعطل
│   ├── ErrorHandler.cs             # معالجة موحدة للأخطاء ⭐ NEW
│   ├── FileLogger.cs               # سجل الملفات
│   ├── HealthMonitor.cs            # مراقبة الصحة
│   ├── ILogger.cs                  # واجهة السجل
│   ├── ProgressHelper.cs           # معالجة موحدة للـ Progress ⭐ NEW
│   ├── RelayCommand.cs             # أوامر MVVM
│   ├── ServiceContainer.cs         # إدارة مركزية للخدمات ⭐ NEW
│   ├── SettingsManager1.cs         # إدارة الإعدادات (SettingsManager)
│   ├── SingleInstanceManager.cs    # منع التشغيل المتعدد
│   ├── TimeOnlyJsonConverter.cs    # محول TimeOnly لـ JSON
│   └── TrayIconManager.cs          # أيقونة System Tray
├── Models/                         # نماذج البيانات
│   ├── AutoBackupConfig.cs         # إعدادات النسخ التلقائي
│   ├── Backupentry.cs              # سجل نسخة احتياطية
│   ├── DatabaseEntry.cs            # بيانات قاعدة بيانات
│   ├── DailyTaskConfig.cs          # إعدادات المهمة اليومية
│   ├── LogEntry.cs                 # سجل حدث
│   ├── OperationProgress.cs        # تقدم عملية
│   ├── ScheduledBackupConfig.cs    # إعدادات النسخ المجدول
│   └── Settings.cs                 # إعدادات عامة
├── Service/                        # خدمات الأعمال
│   ├── AutoBackupService.cs        # خدمة النسخ التلقائي
│   ├── BackupRestoreService.cs     # خدمة النسخ/الاستعادة الرئيسية ⭐
│   ├── BackupWindowsServiceHost.cs # مضيف خدمة Windows
│   ├── DailyTaskService.cs         # خدمة المهام اليومية
│   ├── DeployService.cs            # خدمة نشر IIS
│   ├── GoogleDriveService.cs       # خدمة Google Drive
│   ├── IScheduledBackupService.cs  # واجهة النسخ المجدول
│   └── ScheduledBackupService.cs   # خدمة النسخ المجدول
├── ViewModels/                     # ViewModels (MVVM)
│   ├── AutoBackupViewModel.cs      
│   ├── DailyTaskViewModel.cs       
│   ├── DeployViewModel.cs          
│   ├── MainViewModel.cs            # ViewModel الرئيسي
│   ├── ScheduledBackupViewModel.cs 
│   ├── SettingsViewModel.cs        
│   └── SqlExecutorViewModel.cs     
├── Views/                          # واجهات المستخدم
│   ├── AutoBackupWindow.xaml       # نافذة النسخ التلقائي
│   ├── BackupProgressWindow.xaml   # نافذة تقدم النسخ
│   ├── DailyTaskEditorWindow.xaml  # محرر المهام اليومية
│   ├── DailyTaskManagementWindow.xaml # إدارة المهام اليومية
│   ├── DeployWindow.xaml           # نافذة نشر IIS
│   ├── MainWindow.xaml             # النافذة الرئيسية
│   ├── ScheduledBackupWindow.xaml  # النسخ المجدول
│   ├── SettingsWindow.xaml         # نافذة الإعدادات
│   └── SqlExecutorWindow.xaml      # تنفيذ SQL
├── App.xaml                        # تطبيق WPF
├── Program.cs                      # نقطة الدخول
└── MainWindow.xaml                 # النافذة الرئيسية

### الملفات المحذوفة (لا تعيد إنشاءها)
- ❌ BackupExecutor.cs (تم استبداله بـ BackupRestoreService)
- ❌ CompressionHelper.cs (فارغ، الكود في compress.cs)
- ❌ SettingsRepairTool.cs (فارغ)
- ❌ TestSettings.cs (ملف اختبار قديم)
- ❌ Testing/ (مجلد اختبار قديم)
- ❌ Tools/ (مجلد فارغ)
- ❌ Test-*.ps1 (14 ملف PowerShell تجريبي)
- ❌ quick_test_service.bat
```

---

## 🔧 المكونات الأساسية

### 1. BackupRestoreService ⭐ (المكون المركزي)
**الموقع:** `Service/BackupRestoreService.cs`

**المسؤوليات:**
- النسخ الاحتياطي لقواعد البيانات
- استعادة قواعد البيانات
- دعم Docker (نسخ من/إلى حاويات)
- دعم الاتصالات المحلية والبعيدة
- ضغط النسخ الاحتياطية (ZIP)
- رفع النسخ إلى Google Drive

**أنواع الاتصال المدعومة:**
```csharp
public enum ConnectionType
{
    Local,      // SQL Server محلي
    Docker,     // حاوية Docker
    Remote      // خادم بعيد
}
```

**الدوال الرئيسية:**
- `BackupDatabasesAsync()` - نسخ قائمة قواعد بيانات
- `RestoreDatabaseAsync()` - استعادة قاعدة بيانات
- `UpdateSettings()` - تحديث الإعدادات ديناميكياً

**الاستخدام:**
```csharp
var service = new BackupRestoreService();
var progress = new Progress<string>(msg => Console.WriteLine(msg));
await service.BackupDatabasesAsync(databases, progress);
```

---

### 2. SettingsManager (إدارة الإعدادات)
**الموقع:** `Helpers/SettingsManager1.cs`

**نموذج الإعدادات (BackupConfig):**
```csharp
public class BackupConfig
{
    public string Server { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public bool UseWindowsAuthentication { get; set; }
    public ConnectionType ConnectionType { get; set; }
    public int Port { get; set; } = 1433;
    public string DockerContainerName { get; set; }
    public int ConnectionTimeout { get; set; } = 30;
    public bool TrustServerCertificate { get; set; } = true;
    public bool Encrypt { get; set; } = false;
    public string BackupPath { get; set; }
    public string TempPath { get; set; }
    public List<string> SelectedDatabases { get; set; }
}
```

**الدوال:**
- `LoadSettings()` - تحميل من JSON
- `SaveSettings(config)` - حفظ إلى JSON
- `GetSettingsFilePath()` - مسار ملف الإعدادات
- `GetSettingsDirectory()` - مجلد الإعدادات

**الموقع:** `%APPDATA%\BackupDbServer\settings.json`

---

### 3. AutoBackupService (النسخ التلقائي)
**الموقع:** `Service/AutoBackupService.cs`

**المميزات:**
- نسخ تلقائي بفترة زمنية محددة
- نسخ في وقت محدد يومياً
- رفع تلقائي إلى Google Drive
- حذف النسخ القديمة تلقائياً
- مزامنة مع الإعدادات الرئيسية

**ملف الإعدادات:** `auto_backup_config.json`

**الاستخدام:**
```csharp
var service = new AutoBackupService(backupRestoreService, googleDriveService, logger);
await service.StartAsync();
```

---

### 4. ScheduledBackupService (النسخ المجدول)
**الموقع:** `Service/ScheduledBackupService.cs`

**المميزات:**
- نسخ في أيام محددة
- نسخ في وقت محدد
- دعم تكرار الجدولة
- سجلات مفصلة

**ملف الإعدادات:** `scheduled_backup_config.json`

---

### 5. DailyTaskService (المهام اليومية)
**الموقع:** `Service/DailyTaskService.cs`

**أنواع المهام المدعومة:**
```csharp
public enum TaskType
{
    SqlScript,          // تنفيذ سكريبت SQL
    StoredProcedure,    // تنفيذ إجراء مخزن
    BackupDatabase,     // نسخ احتياطي
    CheckIntegrity,     // فحص السلامة (DBCC CHECKDB)
    UpdateStatistics,   // تحديث الإحصائيات
    RebuildIndexes,     // إعادة بناء الفهارس
    ShrinkDatabase,     // تقليص قاعدة البيانات
    Custom              // مهمة مخصصة
}
```

**المميزات:**
- مهام مجدولة بالدقيقة
- دعم 8 أنواع مهام
- عرض الوقت المتبقي لكل مهمة
- تنفيذ تلقائي في الوقت المحدد

**ملف الإعدادات:** `daily_tasks.json`

---

### 6. DeployService (نشر IIS)
**الموقع:** `Service/DeployService.cs`

**المميزات:**
- نشر تطبيقات ASP.NET إلى IIS
- 7 خطوات آلية كاملة:
  1. إنشاء Application Pool
  2. إنشاء الموقع في IIS
  3. إنشاء قاعدة بيانات
  4. تعديل Web.config
  5. نسخ الملفات
  6. ضبط الصلاحيات
  7. تشغيل الموقع

---

## 🔐 إدارة الاتصالات

### سلسلة الاتصال (Connection String)

**للاتصال المحلي:**
```csharp
Server=localhost;Database=MyDB;Integrated Security=True;TrustServerCertificate=True;
```

**للاتصال بمستخدم SQL:**
```csharp
Server=localhost;Database=MyDB;User ID=sa;Password=Pass123;TrustServerCertificate=True;
```

**للاتصال البعيد:**
```csharp
Server=192.168.1.100,1433;Database=MyDB;User ID=sa;Password=Pass123;
TrustServerCertificate=True;Connection Timeout=30;
```

**للاتصال بـ Docker:**
```csharp
Server=localhost,1433;Database=MyDB;User ID=sa;Password=Pass123;
TrustServerCertificate=True;Connection Timeout=30;
```

---

## 📊 نماذج البيانات الرئيسية

### DailyTaskConfig
```csharp
public class DailyTaskConfig
{
    public string TaskId { get; set; }
    public string TaskName { get; set; }
    public bool IsEnabled { get; set; }
    public string DatabaseName { get; set; }
    public TimeOnly ExecutionTime { get; set; }
    public TaskType Type { get; set; }
    public string SqlScript { get; set; }
    public DateTime? LastExecutionTime { get; set; }
    public bool LastExecutionSuccess { get; set; }
    public string LastExecutionMessage { get; set; }
    
    // خاصية محسوبة للوقت المتبقي
    public string TimeUntilNextRun { get; }
}
```

### AutoBackupConfig
```csharp
public class AutoBackupConfig
{
    public bool IsEnabled { get; set; }
    public int IntervalMinutes { get; set; }
    public bool UseScheduledTime { get; set; }
    public TimeOnly ScheduledTime { get; set; }
    public bool EnableGoogleDrive { get; set; }
    public int GoogleDriveUploadIntervalHours { get; set; }
    public bool DeleteOldBackups { get; set; }
    public int DaysToKeepBackups { get; set; }
    public DateTime? LastBackupTime { get; set; }
}
```

---

## 🎨 واجهات المستخدم (UI)

### MainWindow (النافذة الرئيسية)
**الوظائف:**
- عرض قائمة قواعد البيانات
- نسخ احتياطي يدوي
- استعادة قاعدة بيانات
- الوصول لجميع الأقسام

**الأزرار الرئيسية:**
- النسخ الاحتياطي التلقائي
- النسخ المجدول
- المهام اليومية
- نشر IIS
- الإعدادات

### DailyTaskManagementWindow
**الوظائف:**
- عرض جميع المهام المجدولة
- إضافة/تعديل/حذف مهام
- عرض الوقت المتبقي لكل مهمة
- عرض حالة كل مهمة (✅/❌/⏳)
- تفعيل/تعطيل المهام

**DataGrid Columns:**
1. اسم المهمة
2. قاعدة البيانات
3. الوقت المحدد
4. نوع المهمة
5. مفعل/معطل
6. **الوقت المتبقي** (محدث تلقائياً كل دقيقة)
7. آخر تنفيذ
8. الحالة

---

## ⚙️ الإعدادات والتكوين

### ملفات الإعدادات
جميع الملفات في: `%APPDATA%\BackupDbServer\`

1. **settings.json** - الإعدادات الرئيسية (BackupConfig)
2. **auto_backup_config.json** - إعدادات النسخ التلقائي
3. **scheduled_backup_config.json** - إعدادات النسخ المجدول
4. **daily_tasks.json** - المهام اليومية
5. **application.log** - سجل التطبيق

### مجلدات النسخ الاحتياطي
```
%USERPROFILE%\Documents\
├── LocalBackups/     - للاتصال المحلي
├── DockerBackups/    - لحاويات Docker
└── RemoteBackups/    - للخوادم البعيدة
```

---

## 🔄 سير العمل (Workflows)

### 1. النسخ الاحتياطي اليدوي
```
المستخدم → MainWindow → BackupProgressWindow → BackupRestoreService
```

### 2. النسخ التلقائي
```
AutoBackupService (Timer) → BackupRestoreService → Google Drive (اختياري)
```

### 3. النسخ المجدول
```
ScheduledBackupService (Timer) → BackupRestoreService → Cleanup (اختياري)
```

### 4. المهام اليومية
```
DailyTaskService (Timer 60s) → Check Time → Execute Task → Log Result
```

### 5. نشر IIS
```
DeployViewModel → DeployService → 7 Steps → IIS Manager
```

---

## 🛡️ الأمان والموثوقية

### CrashProtectionManager
- يكتشف الإغلاق المفاجئ
- يسترجع الحالة السابقة
- يسجل الأعطال

### SingleInstanceManager
- يمنع تشغيل نسخ متعددة
- يستخدم Mutex

### HealthMonitor
- يراقب صحة الخدمات
- يكتشف الأعطال

### FileLogger
- سجل مفصل لكل العمليات
- يكتب في `application.log`

---

## 📝 اتفاقيات البرمجة

### تسمية الملفات
- **Services:** `*Service.cs`
- **ViewModels:** `*ViewModel.cs`
- **Views:** `*Window.xaml`
- **Models:** أسماء واضحة (DatabaseEntry, BackupConfig)

### تسمية المتغيرات
```csharp
// Fields
private readonly BackupRestoreService _backupService;
private System.Timers.Timer _timer;

// Properties
public string DatabaseName { get; set; }
public bool IsEnabled { get; set; }

// Methods
public async Task StartAsync()
private void OnTimerElapsed()
```

### Async/Await
- جميع عمليات IO تستخدم `async/await`
- الدوال تنتهي بـ `Async`

### Progress Reporting
```csharp
var progress = new Progress<string>(message => 
{
    _logger.LogInformation(message);
    StatusChanged?.Invoke(this, message);
});
```

---

## 🚫 القيود والمحظورات

### ❌ **لا تستخدم:**
1. **System.Data.SqlClient** - استخدم Microsoft.Data.SqlClient
2. **BackupExecutor** - تم استبداله بـ BackupRestoreService
3. **Synchronous IO** - استخدم async/await
4. **SettingsManager1** - الاسم الحقيقي هو SettingsManager

### ⚠️ **تحذيرات:**
1. لا تعدل `copilot-instructions.md` بدون إذن صريح
2. لا تعدل `ai_context.md` بدون إذن صريح
3. لا تحذف ملفات الإعدادات في المستخدم
4. لا تعطل CrashProtectionManager

---

## 🔌 تكامل Docker

### استخدام Docker
```csharp
// التحقق من وجود Docker
await _dockerClient.Containers.ListContainersAsync(...)

// نسخ ملف من حاوية
await _dockerClient.Containers.GetArchiveFromContainerAsync(...)

// نسخ ملف إلى حاوية
await _dockerClient.Containers.ExtractArchiveToContainerAsync(...)
```

### متطلبات Docker
- Docker Desktop يجب أن يكون مثبتاً
- الحاوية يجب أن تكون قيد التشغيل
- SQL Server في الحاوية يجب أن يكون متاحاً

---

## 🌐 تكامل Google Drive

### GoogleDriveService
**الوظائف:**
- رفع النسخ الاحتياطية
- إدارة المجلدات
- OAuth 2.0 للمصادقة

**ملف Credentials:**
`DriveCredentials/Google.Apis.Auth.OAuth2.Responses.TokenResponse-user`

---

## 🐛 التشخيص والأخطاء

### سجلات التطبيق
**الموقع:** `%APPDATA%\BackupDbServer\application.log`

**مستويات السجل:**
- `[INFO]` - معلومات عامة
- `[WARNING]` - تحذيرات
- `[ERROR]` - أخطاء

### أخطاء شائعة

#### خطأ الاتصال
```
System.Data.SqlClient.SqlException: A network-related error
```
**الحل:** تحقق من:
- SQL Server يعمل
- اسم الخادم صحيح
- منفذ الاتصال صحيح (1433)
- Firewall لا يحجب الاتصال

#### خطأ المصادقة
```
Login failed for user 'sa'
```
**الحل:**
- تفعيل SQL Authentication
- تحقق من اسم المستخدم/كلمة المرور
- راجع `دليل_حل_مشاكل_SQL_Authentication.md`

#### خطأ Docker
```
Docker.DotNet.DockerApiException: Container not found
```
**الحل:**
- تحقق من تشغيل Docker Desktop
- تحقق من اسم الحاوية
- استخدم `docker ps` للتحقق

---

## 📚 الوثائق المرجعية

### ملفات MD في المشروع:
1. **إرشادات_BackupService_الجديد.md** - شرح BackupRestoreService
2. **إرشادات_الإعدادات_الجديدة.md** - نظام الإعدادات
3. **إصلاح_مشاكل_النسخ_التلقائي.md** - حل مشاكل AutoBackup
4. **إصلاح_مشكلة_جدولة_النسخ_الاحتياطي.md** - مزامنة الإعدادات
5. **تطبيق_BackupRestoreService_في_AutoBackupService.md** - التكامل
6. **تأكيد_تطبيق_BackupRestoreService.md** - التحقق
7. **حل_مشكلة_Access_Denied.md** - مشاكل الصلاحيات
8. **حل_مشكلة_حفظ_الإعدادات.md** - مشاكل الإعدادات
9. **دليل_حل_مشاكل_SQL_Authentication.md** - مشاكل SQL

---

## 🎯 أفضل الممارسات

### 1. إدارة الإعدادات
```csharp
// ✅ صحيح
var config = SettingsManager.LoadSettings();
service.UpdateSettings(config);

// ❌ خطأ - استخدام إعدادات قديمة
```

### 2. معالجة الأخطاء
```csharp
// ✅ صحيح
try
{
    await service.BackupDatabasesAsync(databases, progress);
}
catch (Exception ex)
{
    _logger.LogError($"خطأ: {ex.Message}");
    // معالجة الخطأ
}
```

### 3. تحديث UI
```csharp
// ✅ صحيح - من Thread الرئيسي
Application.Current.Dispatcher.Invoke(() => 
{
    StatusText.Text = "تم بنجاح";
});
```

### 4. التخلص من الموارد
```csharp
// ✅ صحيح
public void Dispose()
{
    _timer?.Stop();
    _timer?.Dispose();
}
```

---

## 🔧 صيانة المشروع

### قبل إضافة ميزة جديدة:
1. ✅ راجع ai_context.md
2. ✅ راجع copilot-instructions.md
3. ✅ تحقق من عدم وجود كود مكرر
4. ✅ استخدم BackupRestoreService للنسخ
5. ✅ استخدم Microsoft.Data.SqlClient

### قبل commit:
1. ✅ تأكد من البناء بنجاح
2. ✅ تحقق من عدم وجود أخطاء
3. ✅ حدّث التوثيق إذا لزم
4. ✅ اختبر الميزة الجديدة

---

## 🚀 الأداء والتحسين

### Timers
- **DailyTaskService:** 60 ثانية (فحص المهام)
- **DailyTaskViewModel:** 60 ثانية (تحديث UI)
- **AutoBackupService:** حسب الإعدادات (5-1440 دقيقة)
- **ScheduledBackupService:** حسب الجدول

### الذاكرة
- استخدام `using` للموارد
- تنظيف Timers في Dispose
- تجنب Memory Leaks في Event Handlers

---

## 📞 معلومات الدعم

### في حالة الأخطاء:
1. راجع `application.log`
2. راجع ملفات MD في المشروع
3. راجع الإعدادات في `%APPDATA%\BackupDbServer\`
4. تحقق من SQL Server
5. تحقق من Docker (إن وجد)

---

## 📜 سجل التحديثات الأخيرة

### ديسمبر 2025
- ✅ حذف 22 ملف غير مستخدم
- ✅ توحيد نظام النسخ (BackupRestoreService)
- ✅ تحديث لـ Microsoft.Data.SqlClient
- ✅ إضافة نظام المهام اليومية
- ✅ إضافة عرض الوقت المتبقي للمهام
- ✅ تحسين نظام النسخ المجدول

---

## ⚡ خلاصة سريعة

### نظام النسخ
**استخدم دائماً:** `BackupRestoreService`
```csharp
var service = new BackupRestoreService();
await service.BackupDatabasesAsync(databases, progress);
```

### الإعدادات
**استخدم دائماً:** `SettingsManager`
```csharp
var config = SettingsManager.LoadSettings();
SettingsManager.SaveSettings(config);
```

### السجلات
**استخدم دائماً:** `FileLogger`
```csharp
var logger = new FileLogger();
logger.LogInformation("رسالة");
```

### المكتبات
- ✅ Microsoft.Data.SqlClient
- ❌ System.Data.SqlClient

---

**آخر تحديث:** ديسمبر 13, 2025
**الإصدار:** 2.0
**المطور:** نظام النسخ الاحتياطي المتقدم

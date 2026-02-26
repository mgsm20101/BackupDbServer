# خطة توحيد وتنسيق البرنامج

## 🎯 الهدف
جعل البرنامج متكامل ومتسق وليس مجرد أجزاء مركبة

---

## 📊 التحليل الحالي

### ✅ **النقاط الإيجابية:**
1. جميع الخدمات تستخدم `BackupRestoreService` بشكل موحد
2. جميع الملفات محدثة لاستخدام `Microsoft.Data.SqlClient`
3. `SettingsManager` مستخدم في كل الأماكن بشكل موحد
4. لا يوجد كود مكرر (تم حذف BackupExecutor)

### ⚠️ **مشاكل التناسق المكتشفة:**

#### 1. **عدم تناسق في إنشاء الخدمات**

**المشكلة:**
- بعض ViewModels تنشئ `BackupRestoreService` مباشرة
- بعضها يستقبلها كـ Dependency Injection
- بعض النوافذ تنشئ `FileLogger` محلياً
- التكرار في إنشاء نفس الخدمات

**أمثلة:**
```csharp
// في MainViewModel - الطريقة الصحيحة (Singleton)
_logger = new FileLogger();
_backupService = new BackupRestoreService();
_scheduledBackupService = new ScheduledBackupService(_backupService, _logger);

// في ScheduledBackupViewModel - تكرار غير ضروري
var backupService = new BackupRestoreService();  // ❌ يُنشأ مرة أخرى!
_logger = new FileLogger();                      // ❌ يُنشأ مرة أخرى!

// في BackupProgressWindow - تكرار آخر
_backupService = new BackupRestoreService();     // ❌ يُنشأ مرة أخرى!

// في DatabaseCopyViewModel - تكرار آخر
_backupService = new BackupRestoreService();     // ❌ يُنشأ مرة أخرى!
```

#### 2. **عدم تناسق في إدارة AutoBackupService**

**المشكلة:**
- `MainViewModel` يملك `_autoBackupService` كـ Singleton
- `AutoBackupViewModel` يستقبله كـ parameter
- لكن بعض الأماكن تنشئه محلياً بدون مشاركة الحالة

#### 3. **تكرار في إعداد Progress Handlers**

**المشكلة:**
- كل مكان يستخدم BackupRestoreService ينشئ `Progress<string>` خاص به
- لا يوجد تنسيق في كيفية عرض التقدم

#### 4. **عدم تناسق في معالجة الأخطاء**

**المشكلة:**
- بعض الأماكن تستخدم MessageBox مباشرة
- بعض الأماكن تسجل في Logger فقط
- لا يوجد نمط موحد

---

## 🔧 الحلول المقترحة

### الحل 1: Service Container Pattern

**إنشاء ServiceLocator أو Container:**
```csharp
public static class ServiceContainer
{
    private static ILogger? _logger;
    private static BackupRestoreService? _backupService;
    private static IAutoBackupService? _autoBackupService;
    private static IScheduledBackupService? _scheduledBackupService;

    public static ILogger Logger => _logger ??= new FileLogger();
    
    public static BackupRestoreService BackupService 
        => _backupService ??= new BackupRestoreService();
    
    public static IAutoBackupService AutoBackupService 
        => _autoBackupService ??= InitializeAutoBackupService();
    
    public static IScheduledBackupService ScheduledBackupService 
        => _scheduledBackupService ??= new ScheduledBackupService(BackupService, Logger);
    
    private static IAutoBackupService InitializeAutoBackupService()
    {
        var googleDrive = new GoogleDriveServiceFixed(Logger, "credentials.json");
        return new AutoBackupService(Logger, BackupService, googleDrive);
    }
    
    public static void Initialize()
    {
        // التهيئة الأولية عند بدء التطبيق
        _ = Logger;
        _ = BackupService;
    }
    
    public static void Shutdown()
    {
        // تنظيف الموارد عند الإغلاق
        _autoBackupService?.StopAsync().Wait();
    }
}
```

### الحل 2: تعديل جميع ViewModels

**قبل:**
```csharp
public ScheduledBackupViewModel()
{
    var backupService = new BackupRestoreService();  // ❌
    _logger = new FileLogger();                      // ❌
    _scheduledBackupService = new ScheduledBackupService(backupService, _logger);
}
```

**بعد:**
```csharp
public ScheduledBackupViewModel()
{
    _scheduledBackupService = ServiceContainer.ScheduledBackupService;  // ✅
    _logger = ServiceContainer.Logger;                                  // ✅
}
```

### الحل 3: توحيد Progress Handling

**إنشاء ProgressHandler موحد:**
```csharp
public static class ProgressHelper
{
    public static IProgress<string> CreateProgress(
        Action<string> statusUpdate, 
        Action<int>? percentageUpdate = null,
        ILogger? logger = null)
    {
        return new Progress<string>(message =>
        {
            statusUpdate(message);
            logger?.LogInformation(message);
            
            // استخراج النسبة المئوية إن وجدت
            if (percentageUpdate != null)
            {
                // منطق لاستخراج النسبة من الرسالة
                var match = System.Text.RegularExpressions.Regex.Match(
                    message, @"(\d+)%");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var percent))
                {
                    percentageUpdate(percent);
                }
            }
        });
    }
}
```

### الحل 4: توحيد معالجة الأخطاء

**إنشاء ErrorHandler موحد:**
```csharp
public static class ErrorHandler
{
    public static void Handle(Exception ex, string operation, ILogger logger)
    {
        var message = $"خطأ في {operation}: {ex.Message}";
        
        // تسجيل في Logger
        logger.LogError(message, ex);
        
        // عرض للمستخدم
        Application.Current.Dispatcher.Invoke(() =>
        {
            MessageBox.Show(
                $"❌ فشل في {operation}\n\nالخطأ: {ex.Message}",
                "خطأ",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        });
    }
    
    public static async Task<T?> TryExecuteAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        ILogger logger)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            Handle(ex, operationName, logger);
            return default;
        }
    }
}
```

---

## 📝 خطة التنفيذ

### المرحلة 1: إنشاء البنية التحتية
- [x] إنشاء ServiceContainer.cs
- [ ] إنشاء ProgressHelper.cs
- [ ] إنشاء ErrorHandler.cs
- [ ] إنشاء IServiceInitializer.cs

### المرحلة 2: تحديث MainViewModel
- [ ] استخدام ServiceContainer بدلاً من الإنشاء المباشر
- [ ] تعديل طريقة فتح النوافذ لتمرير الخدمات الموحدة
- [ ] توحيد معالجة الأخطاء

### المرحلة 3: تحديث ViewModels الأخرى
- [ ] AutoBackupViewModel
- [ ] ScheduledBackupViewModel
- [ ] DatabaseCopyViewModel
- [ ] SettingsViewModel
- [ ] DeployViewModel
- [ ] DailyTaskViewModel
- [ ] SqlExecutorViewModel

### المرحلة 4: تحديث Views/Windows
- [ ] BackupProgressWindow
- [ ] DailyTaskEditorWindow
- [ ] DailyTaskManagementWindow
- [ ] جميع النوافذ الأخرى

### المرحلة 5: تحديث Services
- [ ] التأكد من أن جميع الخدمات thread-safe
- [ ] إضافة IDisposable حيث يلزم
- [ ] توحيد أنماط Logging

### المرحلة 6: الاختبار
- [ ] اختبار جميع السيناريوهات
- [ ] التأكد من عدم وجود memory leaks
- [ ] اختبار الأداء
- [ ] التحقق من البناء النهائي

---

## 🎨 المعايير الموحدة

### 1. تسمية المتغيرات
```csharp
// Fields
private readonly ILogger _logger;
private readonly BackupRestoreService _backupService;

// Properties
public string StatusText { get; set; }
public bool IsEnabled { get; set; }

// Methods
private async Task ExecuteAsync()
private void HandleError(Exception ex)
```

### 2. معالجة Async
```csharp
// ✅ صحيح
public async Task StartAsync()
{
    try
    {
        await SomeOperationAsync();
    }
    catch (Exception ex)
    {
        ErrorHandler.Handle(ex, "بدء العملية", _logger);
    }
}

// ❌ خطأ - عدم معالجة الأخطاء
public async Task StartAsync()
{
    await SomeOperationAsync(); // قد يرمي استثناء
}
```

### 3. Progress Reporting
```csharp
// ✅ صحيح - استخدام ProgressHelper
var progress = ProgressHelper.CreateProgress(
    msg => StatusText = msg,
    percent => ProgressPercentage = percent,
    _logger);

await _backupService.BackupDatabasesAsync(databases, progress);

// ❌ خطأ - إنشاء مختلف في كل مكان
var progress = new Progress<string>(msg => StatusText = msg);
```

### 4. Logging
```csharp
// ✅ صحيح
_logger.LogInformation($"بدء العملية: {operationName}");
_logger.LogError($"فشل العملية: {ex.Message}", ex);

// ❌ خطأ - استخدام Console
Console.WriteLine("رسالة"); // لا تفعل هذا
```

---

## 🚀 الفوائد المتوقعة

### 1. أداء أفضل
- تقليل عدد الكائنات المُنشأة
- استخدام Singleton للخدمات المشتركة
- تقليل استهلاك الذاكرة

### 2. صيانة أسهل
- كود موحد وسهل الفهم
- نمط واحد للتعامل مع الأخطاء
- سهولة إضافة ميزات جديدة

### 3. استقرار أعلى
- معالجة موحدة للأخطاء
- لا تكرار في الكود
- سهولة اختبار المكونات

### 4. تجربة مستخدم أفضل
- رسائل خطأ موحدة
- عرض تقدم متسق
- استجابة أسرع

---

## ⚡ ملاحظات مهمة

### Thread Safety
جميع الخدمات يجب أن تكون thread-safe لأنها Singleton:
```csharp
private readonly object _lock = new object();

public void SafeOperation()
{
    lock (_lock)
    {
        // عملية آمنة
    }
}
```

### Disposal
ServiceContainer يجب أن يُستدعى Shutdown عند إغلاق التطبيق:
```csharp
// في App.xaml.cs
protected override void OnExit(ExitEventArgs e)
{
    ServiceContainer.Shutdown();
    base.OnExit(e);
}
```

### Testing
يجب أن يدعم ServiceContainer التهيئة للاختبارات:
```csharp
public static void InitializeForTesting(
    ILogger? logger = null,
    BackupRestoreService? backupService = null)
{
    _logger = logger;
    _backupService = backupService;
}
```

---

## 📋 Checklist للمراجعة

- [ ] كل ViewModel يستخدم ServiceContainer
- [ ] لا يوجد `new BackupRestoreService()` إلا في ServiceContainer
- [ ] لا يوجد `new FileLogger()` إلا في ServiceContainer  
- [ ] كل معالجة أخطاء تستخدم ErrorHandler
- [ ] كل progress reporting يستخدم ProgressHelper
- [ ] جميع الـ async methods لها try-catch
- [ ] جميع الخدمات thread-safe
- [ ] تم اختبار جميع السيناريوهات
- [ ] البناء ينجح بدون أخطاء
- [ ] لا memory leaks

---

**التاريخ:** ديسمبر 13، 2025
**الحالة:** تحت التطوير
**الأولوية:** عالية

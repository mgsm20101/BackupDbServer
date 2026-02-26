# 🎯 ServiceContainer Pattern - دليل الاستخدام السريع

> **تم تطبيق نمط ServiceContainer لتوحيد إدارة الخدمات في التطبيق**

---

## ⚡ الاستخدام السريع

### في أي ViewModel أو Service:

```csharp
using BackupDbServer.Helpers;

public class MyViewModel : ViewModelBase
{
    public MyViewModel()
    {
        // ✅ احصل على الخدمات من ServiceContainer
        var logger = ServiceContainer.Logger;
        var backupService = ServiceContainer.BackupService;
        var autoBackup = ServiceContainer.AutoBackupService;
        var scheduled = ServiceContainer.ScheduledBackupService;
        var dailyTask = ServiceContainer.DailyTaskService;
        var googleDrive = ServiceContainer.GoogleDriveService;
    }
}
```

---

## ❌ الأخطاء الشائعة

```csharp
// ❌ لا تفعل هذا:
var logger = new FileLogger();
var backupService = new BackupRestoreService();

// ✅ افعل هذا بدلاً من ذلك:
var logger = ServiceContainer.Logger;
var backupService = ServiceContainer.BackupService;
```

---

## 🛠️ Utilities الجديدة

### 1️⃣ ProgressHelper
```csharp
// إنشاء Progress Handler موحد
var progress = ProgressHelper.CreateProgress(
    statusUpdate: msg => StatusText = msg,
    percentageUpdate: p => ProgressBar.Value = p,
    logger: ServiceContainer.Logger
);

await backupService.BackupDatabasesAsync(databases, progress);
```

### 2️⃣ ErrorHandler
```csharp
// معالجة بسيطة
try
{
    await SomeOperation();
}
catch (Exception ex)
{
    ErrorHandler.Handle(ex, "اسم العملية", ServiceContainer.Logger);
}

// معالجة تلقائية (مُفضل)
var result = await ErrorHandler.TryExecuteAsync(
    async () => await SomeOperation(),
    "اسم العملية",
    ServiceContainer.Logger
);

if (result)
{
    ErrorHandler.ShowSuccess("نجح", "تمت العملية بنجاح");
}
```

---

## 📋 القواعد الأساسية

1. **دائماً** استخدم `ServiceContainer` للحصول على الخدمات
2. **لا تُنشئ** نسخ محلية من الخدمات المشتركة
3. **استخدم** `ProgressHelper` بدلاً من `new Progress<string>()`
4. **استخدم** `ErrorHandler` بدلاً من `try-catch` يدوي

---

## 📚 الوثائق الكاملة

- **consistency-review-report.md** - تقرير شامل بكل التغييرات
- **CONSISTENCY_REVIEW_SUMMARY.md** - ملخص سريع
- **.github/ai_context.md** - السياق المحدث للـ AI

---

## ✅ حالة المشروع

```
Build: ✅ SUCCESS (0 Errors, 18 Warnings)
Architecture: ✅ ServiceContainer Pattern
Consistency: ✅ 100% Unified
Documentation: ✅ Complete
```

---

**للأسئلة:** راجع الوثائق الكاملة أو .github/copilot-instructions.md

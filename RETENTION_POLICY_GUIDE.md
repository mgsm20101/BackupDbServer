# 📚 دليل سياسة الاحتفاظ بالنسخ الاحتياطية (Backup Retention Policy)

## 🎯 نظرة عامة

تم تطبيق نظام **Grandfather-Father-Son** المُحسّن للاحتفاظ بالنسخ الاحتياطية بشكل منظم وآمن.

---

## ⚙️ كيف يعمل النظام؟

### 1️⃣ التنظيم التلقائي بالتاريخ
عند عمل نسخة احتياطية:
```
C:\Backups\
├── 2025-12-13\          ← مجلد بتاريخ اليوم
│   ├── MyDB_20251213_093045.zip
│   └── AnotherDB_20251213_093050.zip
├── 2025-12-12\
│   └── MyDB_20251212_140020.zip
└── 2025-12-11\
    └── MyDB_20251211_100015.zip
```

### 2️⃣ سياسة الاحتفاظ الافتراضية

| الفترة | السياسة | مثال |
|--------|---------|------|
| **آخر 7 أيام** | 📅 جميع النسخ اليومية | اليوم، أمس، قبل يومين... |
| **8-30 يوم** | 📆 نسخة الجمعة فقط | جمعة الأسبوع الماضي، جمعة الأسبوع قبل الماضي... |
| **31-365 يوم** | 📊 نسخة أول كل شهر | 1 يناير، 1 فبراير، 1 مارس... |
| **أكثر من سنة** | 📁 نسخة كل 3 شهور (ربع سنوي) | 1 يناير، 1 أبريل، 1 يوليو، 1 أكتوبر |

---

## 🔧 الاستخدام في الكود

### الطريقة التلقائية (مُفعّل افتراضياً):
```csharp
// عند الضغط، يتم تطبيق Retention Policy تلقائياً
var zipPath = await CompressionHelper.CompressBackupFileAsync(
    backupFilePath,
    progress,
    useRetentionPolicy: true  // ← الافتراضي
);

// النتيجة:
// ✅ الملف المضغوط في مجلد بالتاريخ: C:\Backups\2025-12-13\MyDB.zip
// ✅ تم تنظيف النسخ القديمة تلقائياً
```

### الطريقة اليدوية:
```csharp
var retentionPolicy = ServiceContainer.RetentionPolicy;

// 1. إنشاء مجلد بالتاريخ
var backupFolder = retentionPolicy.CreateDatedBackupFolder(
    @"C:\Backups",
    DateTime.Now
);

// 2. تطبيق سياسة الاحتفاظ (حذف القديم)
var deletedCount = retentionPolicy.ApplyRetentionPolicy(@"C:\Backups");

// 3. الحصول على إحصائيات
var stats = retentionPolicy.GetBackupStatistics(@"C:\Backups");
Console.WriteLine(stats.GetSummary());
```

---

## 📊 الإحصائيات والتقارير

### الحصول على ملخص السياسة:
```csharp
var retentionPolicy = ServiceContainer.RetentionPolicy;
var summary = retentionPolicy.GetRetentionSummary();

// الناتج:
// سياسة الاحتفاظ الحالية:
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// 📅 النسخ اليومية: آخر 7 أيام (جميع النسخ)
// 📆 النسخ الأسبوعية: 4 أسابيع (يوم الجمعة)
// 📊 النسخ الشهرية: 12 شهر (أول كل شهر)
// 🗑️ الحذف التلقائي للنسخ الأقدم من سنة: معطّل
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### الحصول على إحصائيات مفصلة:
```csharp
var stats = retentionPolicy.GetBackupStatistics(@"C:\Backups");
Console.WriteLine(stats.GetSummary());

// الناتج:
// 📊 إحصائيات النسخ الاحتياطية:
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// 📦 إجمالي النسخ: 45 نسخة (23.5 GB)
//
// 📅 نسخ يومية: 7 (8.2 GB)
// 📆 نسخ أسبوعية: 4 (4.5 GB)
// 📊 نسخ شهرية: 12 (9.8 GB)
// 📁 نسخ قديمة: 22 (1.0 GB)
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## ⚙️ تخصيص الإعدادات

### في ServiceContainer.cs:
```csharp
var settings = new BackupRetentionPolicy.RetentionSettings
{
    DailyRetentionDays = 14,         // الاحتفاظ بـ 14 يوم يومياً
    WeeklyRetentionWeeks = 8,        // الاحتفاظ بـ 8 أسابيع (الجمعة)
    MonthlyRetentionMonths = 24,     // الاحتفاظ بـ 24 شهر (أول الشهر)
    DeleteOlderThanYear = true,      // حذف النسخ الأقدم من سنة
    WeeklyBackupDay = DayOfWeek.Sunday  // نسخة الأحد بدل الجمعة
};

_retentionPolicy = new BackupRetentionPolicy(Logger, settings);
```

### في Models/Settings.cs:
```csharp
public class Settings
{
    // إعدادات سياسة الاحتفاظ
    public bool UseRetentionPolicy { get; set; } = true;
    public int DailyRetentionDays { get; set; } = 7;
    public int WeeklyRetentionWeeks { get; set; } = 4;
    public int MonthlyRetentionMonths { get; set; } = 12;
    public bool DeleteOlderThanYear { get; set; } = false;
}
```

---

## 🎯 أمثلة واقعية

### مثال 1: شركة صغيرة (حجم بيانات متوسط)
```csharp
var settings = new BackupRetentionPolicy.RetentionSettings
{
    DailyRetentionDays = 7,      // أسبوع كامل يومياً
    WeeklyRetentionWeeks = 4,    // شهر أسبوعياً
    MonthlyRetentionMonths = 12, // سنة شهرياً
    DeleteOlderThanYear = false  // الاحتفاظ بالنسخ القديمة
};
```

**النتيجة:**
- آخر 7 أيام: 7 نسخ يومية
- آخر شهر: 4 نسخ أسبوعية إضافية
- آخر سنة: 12 نسخة شهرية إضافية
- **الإجمالي: ~23 نسخة احتياطية**

---

### مثال 2: شركة كبيرة (حجم بيانات ضخم)
```csharp
var settings = new BackupRetentionPolicy.RetentionSettings
{
    DailyRetentionDays = 3,      // 3 أيام فقط يومياً
    WeeklyRetentionWeeks = 2,    // أسبوعين أسبوعياً
    MonthlyRetentionMonths = 6,  // 6 شهور شهرياً
    DeleteOlderThanYear = true   // حذف ما هو أقدم من 6 شهور
};
```

**النتيجة:**
- آخر 3 أيام: 3 نسخ يومية
- آخر أسبوعين: 2 نسخة أسبوعية إضافية
- آخر 6 شهور: 6 نسخ شهرية إضافية
- **الإجمالي: ~11 نسخة احتياطية**

---

### مثال 3: مشروع شخصي (بيانات صغيرة)
```csharp
var settings = new BackupRetentionPolicy.RetentionSettings
{
    DailyRetentionDays = 30,     // شهر كامل يومياً
    WeeklyRetentionWeeks = 12,   // 3 شهور أسبوعياً
    MonthlyRetentionMonths = 36, // 3 سنوات شهرياً
    DeleteOlderThanYear = false  // الاحتفاظ دائماً
};
```

**النتيجة:**
- آخر شهر: 30 نسخة يومية
- 3 شهور: 12 نسخة أسبوعية إضافية
- 3 سنوات: 36 نسخة شهرية إضافية
- **الإجمالي: ~78 نسخة احتياطية**

---

## 🔍 فحص وصيانة النسخ

### فحص يدوي:
```csharp
// الحصول على جميع الإحصائيات
var stats = ServiceContainer.RetentionPolicy.GetBackupStatistics(@"C:\Backups");

if (stats.TotalBackups == 0)
{
    Console.WriteLine("⚠️ لا توجد نسخ احتياطية!");
}
else
{
    Console.WriteLine($"✅ {stats.TotalBackups} نسخة احتياطية");
    Console.WriteLine($"💾 إجمالي الحجم: {FormatSize(stats.TotalSize)}");
    
    // تفاصيل
    Console.WriteLine($"📅 يومي: {stats.DailyBackups} نسخة");
    Console.WriteLine($"📆 أسبوعي: {stats.WeeklyBackups} نسخة");
    Console.WriteLine($"📊 شهري: {stats.MonthlyBackups} نسخة");
    Console.WriteLine($"📁 قديم: {stats.OlderBackups} نسخة");
}
```

### تنظيف يدوي:
```csharp
// تطبيق سياسة الاحتفاظ يدوياً
var deletedCount = ServiceContainer.RetentionPolicy.ApplyRetentionPolicy(@"C:\Backups");

if (deletedCount > 0)
{
    Console.WriteLine($"🧹 تم حذف {deletedCount} نسخة قديمة");
}
else
{
    Console.WriteLine("✅ جميع النسخ متوافقة مع السياسة");
}
```

---

## ⚠️ تحذيرات مهمة

### 1. الحذف نهائي!
```csharp
// ⚠️ الملفات المحذوفة لا يمكن استرجاعها
// تأكد من صحة الإعدادات قبل التفعيل
```

### 2. اختبر أولاً
```csharp
// 🧪 اختبر على بيانات وهمية أولاً
var testPath = @"C:\Backups_Test";
var deletedCount = retentionPolicy.ApplyRetentionPolicy(testPath);
```

### 3. راجع السجلات
```csharp
// 📋 دائماً راجع السجلات بعد التنظيف
var logger = ServiceContainer.Logger;
// السجلات تحتوي على تفاصيل كل ملف محذوف
```

---

## 🚀 التكامل مع النسخ التلقائي

### في AutoBackupService:
```csharp
// النسخ التلقائي يستخدم Retention Policy تلقائياً
var autoBackup = ServiceContainer.AutoBackupService;

// عند عمل نسخة تلقائية:
// ✅ يتم وضعها في مجلد بالتاريخ
// ✅ يتم تطبيق سياسة الاحتفاظ تلقائياً
// ✅ يتم تسجيل كل العمليات
```

### في ScheduledBackupService:
```csharp
// النسخ المجدول يستخدم Retention Policy تلقائياً
var scheduled = ServiceContainer.ScheduledBackupService;

// عند عمل نسخة مجدولة:
// ✅ يتم وضعها في مجلد بالتاريخ
// ✅ يتم تطبيق سياسة الاحتفاظ تلقائياً
// ✅ يتم إرسال تقرير بالنتائج
```

---

## 📝 ملخص الفوائد

✅ **تنظيم تلقائي:** مجلدات بالتاريخ (YYYY-MM-DD)  
✅ **توفير مساحة:** حذف ذكي للنسخ القديمة  
✅ **أمان:** الاحتفاظ بنسخ مهمة (جمعة، أول الشهر)  
✅ **مرونة:** إعدادات قابلة للتخصيص  
✅ **شفافية:** تقارير وإحصائيات مفصلة  
✅ **تلقائي:** يعمل مع كل عملية نسخ احتياطي  

---

## 🎓 للمطورين

### إضافة سياسة مخصصة:
```csharp
public class CustomRetentionPolicy : BackupRetentionPolicy
{
    public CustomRetentionPolicy(ILogger logger) : base(logger) { }
    
    protected override bool ShouldDeleteBackup(DateTime backupDate, DateTime now)
    {
        // منطقك الخاص
        return base.ShouldDeleteBackup(backupDate, now);
    }
}
```

---

**تم التنفيذ بواسطة:** ServiceContainer Pattern  
**التوثيق:** BackupRetentionPolicy.cs  
**التاريخ:** 13 ديسمبر 2025

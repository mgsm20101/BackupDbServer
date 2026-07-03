# 📘 ملف التعريف الشامل — BackupDbServer

> وثيقة مرجعية كاملة (Profile) لكل أجزاء البرنامج + اقتراحات التحسين.
> آخر تحديث: 2026-06-03 | الفرع الحالي: `001-modernize-ui`

---

## 1. نظرة عامة (Overview)

**BackupDbServer** هو تطبيق **WPF Desktop** لإدارة النسخ الاحتياطي والاستعادة لقواعد بيانات **SQL Server**، مع دعم:

- النسخ اليدوي والتلقائي والمجدول (Manual / Auto / Scheduled Backup).
- المهام اليومية المجدولة (Daily SQL Tasks) — 8 أنواع.
- النشر الآلي على **IIS** (Deployment — 7 خطوات).
- التكامل مع **Docker** (SQL Server داخل Container).
- التكامل مع **Google Drive** (رفع النسخ سحابياً).
- تشغيله كـ **Windows Service** أو كتطبيق GUI.

| العنصر | القيمة |
|--------|--------|
| **Target Framework** | `net8.0-windows10.0.17763.0` |
| **Output Type** | `WinExe` |
| **UI** | WPF + XAML (RTL / عربي) |
| **Pattern** | MVVM + Service Layer + ServiceContainer (Singleton) |
| **SQL Library** | `Microsoft.Data.SqlClient` (**إلزامي** — `System.Data.SqlClient` ممنوع) |
| **Nullable** | Enabled |
| **اللغة** | عربي (RTL) مع مصطلحات تقنية إنجليزية |

---

## 2. المعمارية (Architecture)

```
┌─────────────────────────────────────────────────────────┐
│                       Views (XAML)                        │
│  MainWindow + 10 نوافذ متخصصة (Settings, AutoBackup …)    │
└───────────────────────────┬─────────────────────────────┘
                            │ DataBinding
┌───────────────────────────▼─────────────────────────────┐
│                  ViewModels (MVVM)                        │
│  ViewModelBase → MainViewModel, SettingsViewModel …       │
│  Commands: RelayCommand / AsyncRelayCommand               │
└───────────────────────────┬─────────────────────────────┘
                            │ ServiceContainer.X
┌───────────────────────────▼─────────────────────────────┐
│              ServiceContainer (Singleton)                 │
│  Logger · BackupService · GoogleDriveService ·            │
│  AutoBackupService · ScheduledBackupService ·             │
│  DailyTaskService · RetentionPolicy                       │
└───────────────────────────┬─────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────┐
│          Services (Service/ + Helpers/)                   │
│  SQL Server · Docker · IIS · Google Drive · File System   │
└─────────────────────────────────────────────────────────┘
```

### قاعدة ذهبية (Critical Rule)
كل الخدمات المشتركة تُستدعى **حصرياً** عبر `ServiceContainer` — ممنوع `new` مباشر:

```csharp
// ✅ صحيح
var logger = ServiceContainer.Logger;
var backup = ServiceContainer.BackupService;

// ❌ ممنوع
var logger = new FileLogger();
var backup = new BackupRestoreService();
```

**دورة الحياة (Lifecycle):**
- `ServiceContainer.Initialize()` ← في `App.xaml.cs → OnStartup`
- `ServiceContainer.Shutdown()` ← في `App.xaml.cs → OnExit`
- `ServiceContainer.Reset()` / `InitializeForTesting()` ← للاختبار فقط

---

## 3. هيكل المشروع (Project Structure)

```
BackupDbServer/
├── App.xaml(.cs)              # نقطة البدء + تهيئة الخدمات + الثيم
├── MainWindow.xaml(.cs)       # الواجهة الرئيسية (Tabs)
│
├── Models/                    # نماذج البيانات
├── ViewModels/                # منطق العرض (MVVM)
├── Views/                     # النوافذ المتخصصة
├── Service/                   # الخدمات الأساسية
├── Helpers/                   # أدوات مساعدة + ServiceContainer
├── Converters/                # محولات القيم (XAML)
├── Resources/                 # الثيم + الأيقونات (جديد)
│
├── specs/001-modernize-ui/    # توثيق Spec-Driven للـ UI الجديد
└── .github/                   # بروتوكولات التشغيل (copilot + ai_context)
```

---

## 4. الخدمات الأساسية (Service/)

| الملف | الوظيفة | أهم الدوال |
|------|---------|-----------|
| **BackupRestoreService.cs** ⭐ | محرّك النسخ/الاستعادة المركزي (Local/Docker/Remote) | `BackupDatabasesAsync`, `RestoreDatabaseAsync`, `TestConnectionAsync`, `GetDatabasesAsync` |
| **AutoBackupService.cs** | نسخ تلقائي (interval أو وقت ثابت) + رفع Drive + retention | `StartAsync`, `StopAsync`, `RunBackupNowAsync`, `UpdateConfigAsync`, `GetRecentBackups` |
| **ScheduledBackupService.cs** | نسخ مجدول حسب أيام الأسبوع + إشعارات | `StartAsync`, `RunBackupNowAsync`, `GetNextBackupTime` |
| **DailyTaskService.cs** | جدولة مهام SQL يومية (8 أنواع) | `ExecuteTaskAsync`, `AddTask`, `UpdateTask`, `DeleteTask`, `Start/Stop` |
| **DeployService.cs** | نشر آلي على IIS (7 خطوات) | `DeployApplication(DeployConfig)` |
| **DatabaseCopyService.cs** | نسخ قاعدة بيانات مع تعديلات SQL/ملفات | `CopyDatabaseWithModificationsAsync` |
| **GoogleDriveService_Fixed.cs** | رفع/إدارة النسخ على Drive (النسخة المعتمدة) | `InitializeAsync`, `UploadFileAsync`, `CreateFolderAsync`, `DeleteFileAsync` |
| **DockerSqlServerManager.cs** | تحكم بحاويات Docker لـ SQL Server | `IsContainerRunningAsync`, `CreateSqlServerContainerAsync`, `Start/Stop/RemoveContainerAsync` |
| **WindowsServiceManager.cs** | تثبيت/تشغيل البرنامج كـ Windows Service | `InstallServiceAsync`, `Start/Stop/RestartServiceAsync`, `SetServiceStartupTypeAsync` |
| **BackupWindowsServiceHost.cs** | استضافة وضع الخدمة (Service mode host) | — |

**الواجهات (Interfaces):** `IAutoBackupService`, `IScheduledBackupService`, `IGoogleDriveService`.

### أنواع المهام اليومية (TaskType — 8)
`SqlScript` · `StoredProcedure` · `BackupDatabase` · `CheckIntegrity` (DBCC CHECKDB) · `UpdateStatistics` · `RebuildIndexes` · `ShrinkDatabase` · `Custom`

---

## 5. الأدوات المساعدة (Helpers/)

| الملف | الوظيفة |
|------|---------|
| **ServiceContainer.cs** ⭐ | حاوية Singleton لكل الخدمات (thread-safe بـ lock) |
| **BackupService.cs** | غلاف/خدمة نسخ إضافية (تتعايش مع BackupRestoreService) |
| **SettingsManager1.cs** | حفظ/تحميل الإعدادات JSON في `%APPDATA%\BackupDbServer\settings.json` |
| **FileLogger.cs** + **ILogger.cs** | سجلّات ملفية + crash reports + حذف تلقائي للسجلات >10 أيام |
| **ProgressHelper.cs** | تقارير التقدم الموحدة + marshalling لخيط الـ UI |
| **ErrorHandler.cs** | معالجة موحدة للأخطاء (`TryExecuteAsync`, `ShowError/Warning/Success`, `Confirm`) |
| **CrashProtectionManager.cs** | التقاط الاستثناءات غير المُعالَجة + حفظ حالة طوارئ |
| **SingleInstanceManager.cs** | منع تشغيل أكثر من نسخة (Mutex + Named Pipes) |
| **HealthMonitor.cs** | مراقبة صحة النظام |
| **TrayIconManager.cs** | أيقونة شريط المهام (System Tray) |
| **BackupRetentionPolicy.cs** | سياسة الاحتفاظ (يومي 7 / أسبوعي 4 / شهري 12) |
| **compress.cs** (CompressionHelper) | ضغط/فك ضغط ZIP للنسخ + مجلدات بالتاريخ |
| **NetworkPathHelper.cs** | دعم مسارات الشبكة (UNC) |
| **RelayCommand / AsyncRelayCommand** | أنماط Command لـ MVVM |
| **BackupConfig.cs** | DTO لنقل/حفظ الإعدادات |
| **TimeOnlyJsonConverter.cs** | تسلسل `TimeOnly` كـ "HH:mm:ss" |

---

## 6. النماذج (Models/)

| الملف | المحتوى |
|------|---------|
| **Settings.cs** | الإعدادات وقت التشغيل + `GetConnectionString()` + `ConnectionType` enum (Local/Docker/Remote) + سياسة الاحتفاظ |
| **AutoBackupConfig.cs** | إعدادات النسخ التلقائي (interval/scheduled, retention, Google Drive) + `BackupScheduleEntry` |
| **ScheduledBackupConfig.cs** | الجدولة حسب أيام الأسبوع + الإشعارات + تواريخ آخر/قادم نسخ |
| **DailyTaskConfig.cs** | تعريف المهمة + `TaskType` enum + `TimeUntilNextRun` (عدّاد تنازلي) |
| **DatabaseEntry.cs** | تتبّع حالة قاعدة البيانات (FailedCount, LastSuccess) |
| **Backupentry.cs** | سجل تدقيق للنسخة (Status, ErrorMessage, IsSuccessful) |

---

## 7. الواجهات (Views/ + ViewModels/)

### النوافذ (Views)
| النافذة | الوظيفة |
|--------|---------|
| **MainWindow** | المحور الرئيسي — تبويبات: قواعد البيانات / IIS / الإعدادات / سجل العمليات / تقدم العمليات |
| **SettingsWindow** | إعدادات الاتصال (نوع الاتصال، خادم، منفذ، اعتماد، مسار النسخ، retention) |
| **AutoBackupWindow** | إدارة خدمة النسخ التلقائي (تشغيل/إيقاف/تشغيل الآن + إحصائيات) |
| **ScheduledBackupWindow** | جدولة النسخ حسب الأيام والوقت |
| **DailyTaskManagementWindow** | إدارة كل المهام اليومية (Grid + عدّاد تنازلي) |
| **DailyTaskEditorWindow** | إنشاء/تعديل مهمة يومية |
| **DatabaseCopyWindow** | نسخ قاعدة بيانات مع تعديلات SQL |
| **DatabaseDetailsWindow** | سجل النسخ لقاعدة بيانات محددة |
| **BackupProgressWindow** | متابعة التقدم اللحظي |
| **SqlExecutorWindow** | تنفيذ SQL على قواعد مختارة |
| **automationIIs** | النشر الآلي على IIS |

### الـ ViewModels
`ViewModelBase` (يوفّر `INotifyPropertyChanged` + `SetProperty<T>`) → `MainViewModel`, `SettingsViewModel`, `AutoBackupViewModel`, `ScheduledBackupViewModel`, `DailyTaskViewModel`, `DatabaseCopyViewModel`, `SqlExecutorViewModel`, `DatabaseSelectionViewModel`, `DeployViewModel`.

### المحولات (Converters)
`StringToBooleanConverter` · `BoolToTextConverter` · `InvertedBooleanConverter` · `ConnectionStatusColorConverter` · `BooleanToVisibilityConverter` · `TimeOnlyToStringConverter` · `BoolToRunningTextConverter`.

---

## 8. الثيم الجديد (Resources/ — فرع modernize-ui)

| الملف | المحتوى |
|------|---------|
| **ModernDarkTheme.xaml** | ثيم داكن: خلفية `#1E1E2E`، سطح `#2D2D3F`، Primary `#10B981`، Secondary `#3B82F6`، Accent `#F59E0B`، Error `#EF4444`. خطوط: Segoe UI Variable + Almarai (عربي). أنماط: Header, Card (زوايا 12px + ظل), ActionButton (hover/press), DataGrid, ProgressBar (shimmer), TabControl. دعم RTL كامل. |
| **IconDictionary.xaml** | أيقونات Vector/Geometry (بديلاً عن الإيموجي): Refresh, Save, Database, Settings, Docker, Lock, Folder, Link, Cancel, Server, World, History. |

**حالة الـ Spec:** `specs/001-modernize-ui/` — 3 قصص مستخدم (US1 مظهر premium، US2 بطاقات بعمق، US3 تفاعلات دقيقة). التقدم ≈ **22/27** مهمة (المرحلة 6 «اللمسات الأخيرة» متبقية).

---

## 9. حزم NuGet (Dependencies)

| الحزمة | الإصدار |
|--------|---------|
| Microsoft.Data.SqlClient | 6.0.1 |
| Google.Apis.Drive.v3 | 1.68.0.3411 |
| Google.Apis.Auth | 1.68.0 |
| Microsoft.Extensions.Hosting | 8.0.0 |
| Microsoft.Extensions.Logging | 8.0.0 |
| Microsoft.Extensions.DependencyInjection | 8.0.0 |
| System.Data.SqlClient ⚠️ | 4.9.0 (يُفضّل إزالتها) |
| System.Text.Json | 9.0.7 |
| System.ServiceProcess.ServiceController | 8.0.0 |
| Hardcodet.NotifyIcon.Wpf | 1.1.0 |
| Microsoft.Web.Administration | 11.1.0 |

---

## 10. الإعدادات والملفات (Configuration)

**الموقع:** `%APPDATA%\BackupDbServer\`

| الملف | المحتوى |
|------|---------|
| `settings.json` | إعدادات الاتصال الرئيسية (BackupConfig) |
| `auto_backup_config.json` | إعدادات النسخ التلقائي |
| `scheduled_backup_config.json` | إعدادات النسخ المجدول |
| `daily_tasks.json` | تعريفات المهام اليومية |
| `application.log` | سجل التطبيق (INFO/WARNING/ERROR) |

**مسارات النسخ الافتراضية (Documents):** `LocalBackups/` · `DockerBackups/` · `RemoteBackups/`.

**أوامر التشغيل:**
```powershell
dotnet build BackupDbServer.sln                          # بناء
dotnet run --project BackupDbServer.csproj               # تشغيل GUI
dotnet run --project BackupDbServer.csproj -- --service  # وضع الخدمة (يتطلب Admin)
```

---

## 11. اقتراحات التحسين (Recommendations)

> مرتّبة حسب الأولوية. هذه اقتراحات مبدئية — أي تنفيذ يحتاج موافقتك أولاً.

### 🔴 أولوية عالية — أمان وموثوقية (Security & Reliability)

1. **تشفير كلمات المرور المخزّنة (Critical)**
   حالياً الإعدادات بما فيها كلمة مرور SQL تُحفَظ كـ **plain text** في `settings.json`. هذا يخالف قواعد الأمان (لا تخزين كلمات مرور بلا تشفير).
   **الحل:** استخدام **DPAPI** (`ProtectedData.Protect`) لتشفير الحقول الحساسة مرتبطة بمستخدم/جهاز Windows، أو **Windows Credential Manager**. وتفضيل **Windows Authentication** على SQL Auth حيثما أمكن.

2. **إزالة `System.Data.SqlClient` المكرّرة (Conflict)**
   الـ `CLAUDE.md` ينص: استخدم `Microsoft.Data.SqlClient` فقط — لكن `System.Data.SqlClient 4.9.0` ما زالت ضمن الحزم. وجودها يفتح باب استخدام خاطئ ويزيد سطح الهجوم.
   **الحل:** إزالتها من الـ `.csproj` والتأكد عبر بحث شامل أنها غير مُستوردة.

3. **توحيد خدمتي Google Drive (Tech Debt)**
   يوجد `GoogleDriveService.cs` و `GoogleDriveService_Fixed.cs` معاً — ازدواجية مربكة.
   **الحل:** اعتماد نسخة واحدة (`_Fixed`)، حذف القديمة، وإبقاء الواجهة `IGoogleDriveService` فقط.

4. **توضيح العلاقة بين `BackupService.cs` و `BackupRestoreService.cs`**
   ملفان لخدمة النسخ في مجلدين مختلفين (`Helpers/` و `Service/`). يجب توثيق أيهما المعتمد أو دمجهما لتجنّب التشتت.

### 🟠 أولوية متوسطة — جودة الكود (Code Quality)

5. **الانتقال إلى Generic Host + DI الحقيقي**
   لديك بالفعل `Microsoft.Extensions.Hosting/DependencyInjection` ضمن الحزم، لكن النمط الحالي `ServiceContainer` (Singleton يدوي). الانتقال إلى **`IHost` + `IServiceProvider`** يعطي: lifetimes صحيحة، اختبارية أفضل، و`IHostedService` للخدمات الخلفية بدل الـ Timers اليدوية.

6. **استبدال `Application.Current.Dispatcher.Invoke` بـ `IProgress<T>`**
   موجود جزئياً عبر `ProgressHelper` — يُفضّل تعميمه وإزالة أي `Dispatcher.Invoke` متناثر داخل الخدمات (فصل الـ UI عن الـ Logic).

7. **استخدام `CancellationToken` في كل العمليات الطويلة**
   عمليات النسخ/الاستعادة طويلة؛ يجب تمرير `CancellationToken` لإتاحة الإلغاء من الـ UI ومنع تعليق الإغلاق.

8. **توحيد نمط الأخطاء عبر `Result<T>`**
   تماشياً مع معاييرك (FluentResults / `Result<T>`) بدل رمي الاستثناءات للأخطاء المتوقعة (فشل اتصال، ملف غير موجود).

### 🟡 أولوية منخفضة — تجربة المطوّر والصيانة (DX & Maintainability)

9. **إضافة اختبارات آلية (Tests)**
   لا يوجد test suite. اقتراح: مشروع **xUnit** يختبر منطق الجدولة (`GetNextBackupTime`)، سياسة الاحتفاظ، وبناء الـ connection string — وهي أجزاء منطقية بحتة قابلة للاختبار دون UI.

10. **إكمال فرع `001-modernize-ui`**
    إنهاء المهام الـ5 المتبقية (Phase 6) وتطبيق `ModernDarkTheme` على كل النوافذ بشكل متّسق، ثم دمجه عبر PR.

11. **تنظيف مجلدات البناء من Git**
    مجلد `obj/` فيه عشرات ملفات `*_wpftmp.AssemblyInfo.cs` المؤقتة. التأكد من `.gitignore` يستبعد `bin/` و `obj/`.

12. **توثيق XML على الـ public APIs**
    تماشياً مع معاييرك، إضافة XML doc comments على دوال الخدمات العامة.

13. **مركزة بناء Connection String**
    التأكد أن كل بناء لسلسلة الاتصال يمر عبر `Settings.GetConnectionString()` فقط (مصدر حقيقة واحد)، مع `TrustServerCertificate` و `Encrypt` قابلين للضبط لكل بيئة (تطوير/إنتاج).

---

## 12. تدفقات العمل الرئيسية (Workflows)

```
نسخ يدوي:    MainWindow → اختيار قواعد → Backup → ضغط ZIP → (رفع Drive)
نسخ تلقائي:  AutoBackupService (Timer) → interval/وقت ثابت → retention → Drive
نسخ مجدول:   ScheduledBackupService → أيام محددة + وقت → إشعارات
مهام يومية:   DailyTaskService (فحص كل 60ث) → تنفيذ نوع المهمة → سجل
نشر IIS:     DeployService → استعادة DB → web.config → ملفات → صلاحيات → موقع IIS
```

---

*انتهت الوثيقة — جاهزة لأي توسعة أو تحديث عند الطلب.*

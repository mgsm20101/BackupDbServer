# PRD — إعادة هيكلة مشروع BackupDbServer (Refactor)

> **النوع:** ريفاكتور معماري (Behavior-preserving) — بدون ميزات جديدة.
> **التاريخ:** 2026-07-04  ·  **الفرع الحالي:** `001-modernize-ui`  ·  **الإصدار المُسلّم:** 1.0.0

---

## 1. السياق والدافع (Context)

مشروع **BackupDbServer** (تطبيق WPF لإدارة النسخ الاحتياطي لـ SQL Server) نما بشكل كبير وأُضيفت له وظائف كثيرة (Docker، IIS deploy، مهام يومية/دورية، Google Drive، صيانة XML، قفل بكلمة مرور...). هذا النمو أنتج **ملفات ضخمة (God-classes)، تكراراً في الكود، وبنية غير منظمة** تجعل الإضافة والصيانة أصعب وأكثر عرضة للأخطاء.

**الهدف:** إعادة تنظيم المشروع ليكون أوضح وأسهل صيانةً، عبر تنظيم **حسب الميزة (Feature-based)** وإدخال **حقن التبعيات الحقيقي (DI)** — **مع الحفاظ الكامل على السلوك الحالي** لأن نسخة 1.0.0 سُلّمت للعميل للتو.

---

## 2. الأهداف و اللا-أهداف

### الأهداف (Goals)
- تنظيم الكود **حسب الميزة** بدل الطبقات المتضخمة.
- استبدال `ServiceContainer` (Service Locator يدوي) بـ **Microsoft.Extensions.DependencyInjection + Generic Host** (الحزمة مضافة أصلاً وغير مستخدمة).
- تفكيك الـ **God-classes** إلى وحدات صغيرة ذات مسؤولية واحدة.
- حذف **الملفات المكررة/المهجورة**.
- تنظيف مخالفات **MVVM** في الـ code-behind.

### اللا-أهداف (Non-Goals — خارج النطاق)
- ❌ أي ميزة وظيفية جديدة أو تغيير في واجهة المستخدم.
- ❌ إدخال مشروع اختبارات (Unit Tests) — **مؤجّل لمرحلة لاحقة** (لكن الـ DI يمهّد له).
- ❌ التحول الكامل لـ Clean Architecture / CQRS (ثقيل على تطبيق WPF سطح مكتب).
- ❌ ترقية إصدارات الحزم أو تغيير التقنيات.

---

## 3. الوضع الحالي — نقاط الفوضى (مبنية على مسح الكود الفعلي)

### God-classes (الأكبر حجماً)
| الأسطر | الملف | المشكلة |
|---|---|---|
| 1692 | `ViewModels/MainViewModel.cs` | يمسك 11+ مسؤولية (backup, restore, Docker, IIS, daily tasks, settings, drive, logging, tray) |
| 1055 | `Service/BackupRestoreService.cs` | backup + restore مدموجان في خدمة واحدة |
| 946 | `Service/AutoBackupService.cs` | جدولة + مزامنة سحابية في نفس الخدمة |
| 675 | `ViewModels/ScheduledBackupViewModel.cs` | ViewModel متضخم |
| 665 | `ViewModels/SettingsViewModel.cs` | 600+ سطر خصائص/أوامر |
| 418 | `Helpers/ServiceContainer.cs` | Service Locator يدوي (anti-pattern) |
| 368 | `Views/DailyTaskEditorWindow.xaml.cs` | منطق ثقيل في الـ code-behind (مخالفة MVVM) |

### تكرار وأسماء ملتبسة
- `Service/GoogleDriveService.cs` **و** `Service/GoogleDriveService_Fixed.cs` — نسختان (المستخدَمة فعلياً هي `_Fixed`).
- `Helpers/SettingsManager1.cs` — لاحقة رقمية توحي بنسخة مكررة/غير مكتملة.
- مجلدان متشابهان: `Service/` (16 ملف) و `Services/` (ملف واحد: `LogService`).

### البنية
- `ServiceContainer` يدير 10 خدمات كـ singletons يدوية → **اقتران محكم** (`ServiceContainer.X`) منتشر في كل مكان.
- حزمة `Microsoft.Extensions.DependencyInjection` **مضافة في الـ csproj لكن غير مستخدمة إطلاقاً**.
- **صفر اختبارات** (لا يوجد test project).

---

## 4. البنية المستهدفة (Feature-based + DI)

```
BackupDbServer/
├── App.xaml.cs                 # إقلاع عبر Generic Host + تسجيل DI
├── Core/                       # المشترك عبر كل الميزات
│   ├── Mvvm/                   # ViewModelBase, RelayCommand, AsyncRelayCommand
│   ├── Services/               # FileLogger, SettingsManager (موحّد), ConnectionFactory
│   ├── Models/                 # Settings, BackupConfig, enums المشتركة
│   ├── Helpers/                # ErrorHandler, ProgressHelper, Compression, Retention
│   └── Infrastructure/         # ServiceCollectionExtensions (تسجيلات DI)
├── Features/
│   ├── Shell/                  # MainWindow + ShellViewModel (تنقّل/tray/حالة فقط)
│   ├── Backup/                 # BackupService + RestoreService + BackupViewModel + Views
│   ├── AutoBackup/             # جدولة منفصلة عن المزامنة السحابية
│   ├── ScheduledBackup/
│   ├── DailyTasks/             # DailyTaskService + VM (+ نقل منطق المحرر إلى VM)
│   ├── Deploy/                 # IIS deploy + maintenance
│   ├── Docker/                 # إدارة حاويات SQL
│   ├── GoogleDrive/            # خدمة واحدة فقط
│   ├── Maintenance/            # XML kill-switch + IIS speedup
│   └── Settings/
├── Resources/                  # الثيمات والأيقونات (ModernDarkTheme, IconDictionary)
└── Converters/                 # WPF value converters
```

**مبدأ التنظيم:** كل ميزة تحتوي View + ViewModel + الخدمة الخاصة بها في مجلد واحد؛ وكل ما هو مشترك في `Core/`.

---

## 5. الحركات التفصيلية للريفاكتور

### 5.1 — DI حقيقي (استبدال ServiceContainer)
- إنشاء `Core/Infrastructure/ServiceCollectionExtensions.cs` يسجّل كل الخدمات والـ ViewModels.
- في `App.xaml.cs`: بناء `IHost` عبر `Microsoft.Extensions.Hosting`، وتسجيل الخدمات كـ Singleton/Transient حسب الحاجة.
- تحويل الخدمات والـ VMs لاستقبال تبعياتها عبر **الـ constructor** بدل `ServiceContainer.X`.
- **استراتيجية آمنة:** إبقاء `ServiceContainer` كطبقة توافق رقيقة (shim) تقرأ من الـ Host أثناء الهجرة، ثم إزالتها في نهاية المرحلة.

### 5.2 — حذف المكررات وتوحيد المجلدات
- حذف `Service/GoogleDriveService.cs` القديم، وإعادة تسمية `GoogleDriveService_Fixed` → `GoogleDriveService`.
- توحيد `Helpers/SettingsManager1.cs` مع `SettingsManager` (نسخة واحدة في `Core/Services`).
- دمج مجلد `Services/` (LogService) داخل `Core/`.

### 5.3 — تفكيك الخدمات الضخمة
- `BackupRestoreService` (1055) → `BackupService` + `RestoreService` + `ConnectionFactory` مشترك.
- `AutoBackupService` (946) → فصل **الجدولة (Scheduling)** عن **المزامنة السحابية (Drive Sync)**.

### 5.4 — تفكيك MainViewModel
- `MainViewModel` (1692) → `ShellViewModel` نحيف (تنقّل/tray/حالة الاتصال) + توزيع المسؤوليات على VMs الميزات الموجودة (Backup/Deploy/Docker/Settings/Log)، مع نقل المنطق المضمّن (IIS/Docker/SQL inline) إلى خدماته.

### 5.5 — تنظيف MVVM
- نقل منطق `DailyTaskEditorWindow.xaml.cs` (368) إلى `DailyTaskEditorViewModel` مع bindings صحيحة.

---

## 6. استراتيجية الهجرة (تدريجية وآمنة)

| المرحلة | المحتوى | معيار "تمّت" |
|---|---|---|
| **P1** | أساس DI (Host + تسجيلات) بالتوازي مع ServiceContainer | التطبيق يقلع عبر الـ Host وكل الوظائف تعمل |
| **P2** | حذف المكررات + توحيد المجلدات (Core/Features) | لا ملفات `_Fixed`/`1`، مجلد `Services/` مدموج |
| **P3** | تفكيك الخدمات الضخمة (Backup/Restore/AutoBackup) | كل خدمة < ~400 سطر بمسؤولية واحدة |
| **P4** | تفكيك MainViewModel → Shell + VMs الميزات | MainViewModel القديم مُفكَّك، الـ shell نحيف |
| **P5** | تنظيف الـ code-behind الثقيل (DailyTaskEditor) | لا منطق أعمال في الـ code-behind |

**كل مرحلة = PR مستقل، قابل للبناء والتسليم، بلا تغيير سلوكي.**

---

## 7. القيود والمخاطر

- **الحفاظ على السلوك 100%:** لا تغيير وظيفي أو في الواجهة (منتَج تسليم عميل نشط).
- **لا توجد شبكة أمان اختبارات** → التحقق يدوي بعد كل مرحلة عبر السيناريوهات الأساسية + مراجعة `application.log`.
- **خطر رئيسي:** الاقتران المحكم بـ `ServiceContainer` منتشر — لذا الهجرة عبر shim تدريجي وليس دفعة واحدة (big-bang).
- **حساسية WPF مع DI:** ربط الـ Views بالـ ViewModels عبر الـ Host يتطلب انتباهاً (تسجيل النوافذ وحقنها).

---

## 8. معايير النجاح (Definition of Done)

- [ ] لا يوجد ملف مصدري يتجاوز ~400–500 سطر بلا مبرر.
- [ ] لا يوجد Service Locator يدوي — كل التبعيات عبر DI/constructor.
- [ ] لا ملفات مكررة (`_Fixed`, `SettingsManager1`)، ولا مجلد `Services/` منفصل.
- [ ] `MainViewModel` مفكَّك إلى shell + VMs ميزات.
- [ ] بناء نظيف `0 Errors`، والتطبيق يعمل بنفس سلوك 1.0.0 تماماً.
- [ ] كل مرحلة سُلّمت كـ PR منفصل موثّق.

---

## 9. ما بعد هذا الـ PRD

الخطوة التالية: تحويل هذا الـ PRD إلى **خطة تنفيذ تفصيلية** (implementation plan) تُقسّم كل مرحلة (P1–P5) إلى مهام صغيرة قابلة للتنفيذ والتحقق.

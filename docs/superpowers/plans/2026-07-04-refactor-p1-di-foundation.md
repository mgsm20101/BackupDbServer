# Refactor P1 — DI Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the hand-rolled `ServiceContainer` singleton locator with a real dependency-injection foundation (`Microsoft.Extensions.Hosting` + `Microsoft.Extensions.DependencyInjection` — both already referenced in the `.csproj` but unused), while keeping every existing `ServiceContainer.X` call site in the codebase working unchanged.

**Architecture:** Build a `Generic Host` in `App.xaml.cs` that owns construction of every service. `ServiceContainer` becomes a thin static shim whose properties resolve from the host's `IServiceProvider` instead of hand-rolling `??=` singletons. `MainWindow` and `MainViewModel` are registered in the container and resolved explicitly (replacing WPF's `StartupUri` auto-instantiation), so the whole object graph is built by DI from the top down. No other class in the codebase changes.

**Tech Stack:** .NET 8, WPF, `Microsoft.Extensions.Hosting` 8.0.0, `Microsoft.Extensions.DependencyInjection` 8.0.0 (already in `BackupDbServer.csproj`, currently unused).

---

## Context

This is **Phase P1** of [`REFACTOR_PRD.md`](../../../REFACTOR_PRD.md) (section 6, migration table). The PRD's own strategy calls for a **thin compatibility shim** during migration: `ServiceContainer` keeps its existing static API so none of the ~38 call sites across 13 files (`MainViewModel.cs`, `SettingsViewModel.cs`, `AppLockManager.cs`, `TrayIconManager.cs`, etc.) need to change in this phase. Only the *internals* of `ServiceContainer` change — from manual double-checked-locking singletons to DI resolution.

**Why P1 only, not P2–P5:** The PRD stages the refactor into 5 independent PRs (P1: DI foundation → P2: de-dup/folders → P3: split god-services → P4: split `MainViewModel` → P5: MVVM cleanup). Each later phase depends on decisions only visible once the previous one has landed (e.g., P4's `MainViewModel` split needs to decide which feature each extracted method belongs to, which is easier to see once P2's folder structure exists). Writing all 5 phases as one plan now would mean guessing at P3/P4 details rather than knowing them. This plan covers **P1 only**; P2–P5 each get their own plan written after the prior phase merges.

**Non-goals (explicitly out of scope for this plan):** No behavior change, no new tests (project has no test suite — see `CLAUDE.md` "Testing" section), no folder restructuring (that's P2), no touching `MainViewModel.cs` internals (that's P4).

**Before starting:** This changes app startup wiring in a client-shipped app (v1.0.0 delivered). Recommend creating an isolated branch/worktree first (e.g. `002-refactor-p1-di-foundation` off `main`, once PR #1 merges) via the `superpowers:using-git-worktrees` skill, rather than working directly on `001-modernize-ui`.

---

## File Structure

| Action | File | Responsibility |
|---|---|---|
| Create | `Core/Infrastructure/ServiceCollectionExtensions.cs` | One extension method, `AddApplicationServices()`, registering every service, `MainViewModel`, and `MainWindow` into an `IServiceCollection`. |
| Modify | `Helpers/ServiceContainer.cs` | Replace manual singleton fields with `IServiceProvider`-backed resolution. Same public API surface (minus dead `Reset()`/`InitializeForTesting()`, confirmed unused outside two `.md` docs). |
| Modify | `App.xaml.cs` | Build + start the `HostBuilder`, call `ServiceContainer.Initialize(IServiceProvider)`, resolve `MainWindow` from the container and show it explicitly (replacing `StartupUri`), dispose the host on exit. |
| Modify | `App.xaml` | Remove `StartupUri="MainWindow.xaml"` (WPF must no longer auto-construct `MainWindow`). |
| Modify | `MainWindow.xaml` | Remove the `<Window.DataContext><local:MainViewModel/></Window.DataContext>` block (DI now supplies the `MainViewModel` instance via constructor). |
| Modify | `MainWindow.xaml.cs` | Constructor takes `MainViewModel viewModel` and sets `DataContext = viewModel;` explicitly; rest of the class (tray icon, idle timer, closing handler) is untouched. |

No other file changes in this plan.

---

## Task 1: Add the DI registration extension

**Files:**
- Modify: `Helpers/ServiceContainer.cs:355` (visibility change only)
- Create: `Core/Infrastructure/ServiceCollectionExtensions.cs`

This task is purely additive — nothing calls the new extension method yet, so the build must stay green throughout.

- [ ] **Step 1: Widen `GetGoogleDriveCredentialsPath` from `private` to `internal`**

In `Helpers/ServiceContainer.cs`, change line 355 from:
```csharp
        private static string GetGoogleDriveCredentialsPath()
```
to:
```csharp
        internal static string GetGoogleDriveCredentialsPath()
```
(No other change to that method's body in this step — Task 2 rewrites the rest of the file.)

- [ ] **Step 2: Create `Core/Infrastructure/ServiceCollectionExtensions.cs`**

```csharp
using System;
using BackupDbServer.Helpers;
using BackupDbServer.Service;
using BackupDbServer.ViewModels;
using BackupDbServer.Views;
using Microsoft.Extensions.DependencyInjection;

namespace BackupDbServer.Core.Infrastructure
{
    /// <summary>
    /// تسجيل جميع الخدمات والـ ViewModels والنوافذ في حاوية DI.
    /// يحل هذا محل الإنشاء اليدوي للـ singletons داخل Helpers/ServiceContainer.cs.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // البنية التحتية
            services.AddSingleton<ILogger, FileLogger>();
            services.AddSingleton<AppLockManager>();

            // خدمات النسخ الاحتياطي والاستعادة
            services.AddSingleton<BackupRestoreService>();

            services.AddSingleton<GoogleDriveServiceFixed>(sp =>
                new GoogleDriveServiceFixed(
                    sp.GetRequiredService<ILogger>(),
                    ServiceContainer.GetGoogleDriveCredentialsPath()));
            services.AddSingleton<IGoogleDriveService>(sp =>
                sp.GetRequiredService<GoogleDriveServiceFixed>());

            services.AddSingleton<IAutoBackupService, AutoBackupService>();
            services.AddSingleton<IScheduledBackupService, ScheduledBackupService>();
            services.AddSingleton<DailyTaskService>();
            services.AddSingleton<XmlMaintenanceService>();

            services.AddSingleton(sp => new BackupRetentionPolicy(
                sp.GetRequiredService<ILogger>(),
                new BackupRetentionPolicy.RetentionSettings
                {
                    DailyRetentionDays = 7,
                    WeeklyRetentionWeeks = 52,
                    MonthlyRetentionMonths = 12,
                    DeleteOlderThanYear = false,
                    WeeklyBackupDay = DayOfWeek.Friday
                }));

            // الواجهة الرئيسية
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>();

            return services;
        }
    }
}
```

Note: the `RetentionSettings` values above are copied verbatim from the current `ServiceContainer.RetentionPolicy` getter (`Helpers/ServiceContainer.cs:179-186`) — same daily/weekly/monthly retention behavior, unchanged.

- [ ] **Step 3: Build to verify the new file compiles**

Run: `dotnet build BackupDbServer.csproj -nologo -clp:ErrorsOnly`
Expected: `0 Errors` (the new file is additive; nothing references it yet, so existing code paths are untouched).

> If the app is currently running (elevated), the build will fail with a file-lock error (`MSB3027`/`MSB3021` on `BackupDbServer.exe`). Close the running instance first — it cannot be killed from a non-elevated shell.

- [ ] **Step 4: Commit**

```bash
git add Helpers/ServiceContainer.cs Core/Infrastructure/ServiceCollectionExtensions.cs
git commit -m "refactor: add DI service registration extension (unused so far)"
```

---

## Task 2: Wire the Generic Host into startup and convert ServiceContainer into a DI shim

**Files:**
- Modify: `Helpers/ServiceContainer.cs` (full rewrite of the class body)
- Modify: `App.xaml.cs`
- Modify: `App.xaml:6`
- Modify: `MainWindow.xaml:45-48`
- Modify: `MainWindow.xaml.cs:1-23`

This is the single "flip the switch" task — all five files must change together or the app won't build. One commit at the end.

- [ ] **Step 1: Rewrite `Helpers/ServiceContainer.cs`**

Replace the entire file content with:

```csharp
using System;
using System.IO;
using System.Text.Json;
using BackupDbServer.Models;
using BackupDbServer.Service;
using Microsoft.Extensions.DependencyInjection;

namespace BackupDbServer.Helpers
{
    /// <summary>
    /// نقطة وصول موحّدة للخدمات المشتركة. تُحلّ الخدمات الآن من حاوية DI
    /// (انظر Core/Infrastructure/ServiceCollectionExtensions.cs) بدل الإنشاء اليدوي.
    /// تُبقي هذه الواجهة الثابتة متوافقة مع كل الاستدعاءات الحالية (ServiceContainer.X)
    /// أثناء هجرة تدريجية لباقي الكود نحو حقن التبعيات المباشر.
    /// </summary>
    public static class ServiceContainer
    {
        private static IServiceProvider? _provider;
        private static bool _isInitialized;

        public static ILogger Logger => Resolve<ILogger>();
        public static BackupRestoreService BackupService => Resolve<BackupRestoreService>();
        public static GoogleDriveServiceFixed GoogleDriveService => Resolve<GoogleDriveServiceFixed>();
        public static IAutoBackupService AutoBackupService => Resolve<IAutoBackupService>();
        public static IScheduledBackupService ScheduledBackupService => Resolve<IScheduledBackupService>();
        public static DailyTaskService DailyTaskService => Resolve<DailyTaskService>();
        public static BackupRetentionPolicy RetentionPolicy => Resolve<BackupRetentionPolicy>();
        public static AppLockManager AppLock => Resolve<AppLockManager>();
        public static XmlMaintenanceService XmlMaintenance => Resolve<XmlMaintenanceService>();

        public static bool IsInitialized => _isInitialized;

        private static T Resolve<T>() where T : notnull
        {
            if (_provider == null)
                throw new InvalidOperationException("ServiceContainer.Initialize(provider) لم يُستدعَ بعد.");
            return _provider.GetRequiredService<T>();
        }

        /// <summary>
        /// تهيئة الحاوية بمزوّد الخدمات الناتج عن الـ Host (يُستدعى مرة واحدة من App.xaml.cs).
        /// </summary>
        public static void Initialize(IServiceProvider provider)
        {
            if (_isInitialized)
            {
                Logger.LogWarning("⚠️ ServiceContainer مُهيأ مسبقاً");
                return;
            }

            _provider = provider;

            Logger.LogInformation("🚀 بدء تهيئة ServiceContainer...");

            // التأكد من إنشاء الخدمات الأساسية (نفس ترتيب الإنشاء القديم)
            _ = Logger;
            _ = BackupService;
            _ = ScheduledBackupService;

            // بدء خدمة المهام اليومية
            DailyTaskService.Start();

            // بدء خدمة النسخ التلقائي إذا كانت مفعلة
            _ = StartAutoBackupIfEnabledAsync();

            _isInitialized = true;
            Logger.LogInformation("✅ تم تهيئة ServiceContainer بنجاح");
        }

        /// <summary>
        /// إيقاف جميع الخدمات عند إغلاق التطبيق
        /// </summary>
        public static void Shutdown()
        {
            if (_provider == null) return;

            Logger.LogInformation("🛑 بدء إيقاف ServiceContainer...");

            try
            {
                AutoBackupService.StopAsync().Wait();
                Logger.LogInformation("✅ تم إيقاف AutoBackupService");

                ScheduledBackupService.StopAsync().Wait();
                Logger.LogInformation("✅ تم إيقاف ScheduledBackupService");

                DailyTaskService.Stop();
                Logger.LogInformation("✅ تم إيقاف DailyTaskService");

                Logger.LogInformation("✅ تم إيقاف ServiceContainer بنجاح");
            }
            catch (Exception ex)
            {
                Logger.LogError($"❌ خطأ أثناء إيقاف ServiceContainer: {ex.Message}", ex);
            }
            finally
            {
                _isInitialized = false;
            }
        }

        /// <summary>
        /// الحصول على مسار ملف اعتماد Google Drive (يُستخدم من تسجيل DI أيضاً).
        /// </summary>
        internal static string GetGoogleDriveCredentialsPath()
        {
            try
            {
                var autoBackupConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BackupDbServer",
                    "auto_backup_config.json");

                if (File.Exists(autoBackupConfigPath))
                {
                    var configJson = File.ReadAllText(autoBackupConfigPath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var config = JsonSerializer.Deserialize<AutoBackupConfig>(configJson, options);

                    if (!string.IsNullOrEmpty(config?.GoogleDriveCredentialsPath))
                    {
                        return config.GoogleDriveCredentialsPath;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"⚠️ فشل تحميل مسار Google Drive Credentials: {ex.Message}");
            }

            // المسار الافتراضي
            return "credentials.json";
        }

        /// <summary>
        /// بدء خدمة النسخ التلقائي إذا كانت مفعلة في الإعدادات
        /// </summary>
        private static async System.Threading.Tasks.Task StartAutoBackupIfEnabledAsync()
        {
            try
            {
                var config = AutoBackupService.GetConfig();
                if (config.IsEnabled)
                {
                    Logger.LogInformation("🔄 خدمة النسخ التلقائي مفعلة - جاري البدء...");
                    await AutoBackupService.StartAsync();
                    Logger.LogInformation("✅ تم بدء خدمة النسخ التلقائي");
                }
                else
                {
                    Logger.LogInformation("ℹ️ خدمة النسخ التلقائي غير مفعلة");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"❌ فشل بدء خدمة النسخ التلقائي: {ex.Message}", ex);
            }
        }
    }
}
```

**What changed vs. the original and why it's still behavior-preserving:**
- `Reset()` and `InitializeForTesting()` are dropped — grepped and confirmed unused outside two `.md` docs (no `.cs` caller anywhere in the repo). There's no test project to need them.
- The double-checked-locking (`_lock` object + `??=`) is gone. `Microsoft.Extensions.DependencyInjection` singletons are already thread-safe on first resolution, so the manual lock is redundant. `Initialize()`/`Shutdown()` are still only ever called once each, synchronously, from the WPF UI thread.
- Every service that was unconditionally forced to construct during the old `Initialize()` (`Logger`, `BackupService`, `ScheduledBackupService`, and — transitively, because `StartAutoBackupIfEnabledAsync` calls `AutoBackupService.GetConfig()` regardless of whether auto-backup is enabled — `AutoBackupService` and `GoogleDriveServiceFixed`) is still forced to construct at the same point, in the same order.
- `Shutdown()` no longer null-checks `_autoBackupService`/`_scheduledBackupService`/`_dailyTaskService` before stopping them. This is safe specifically because all three are unconditionally constructed during `Initialize()` (see previous bullet) — by the time `Shutdown()` runs, they already exist in the real app lifecycle. `RetentionPolicy`, `AppLock`, and `XmlMaintenance` remain genuinely lazy (constructed on first real use elsewhere) in both the old and new code — unchanged.
- **One visible side effect to expect during manual verification:** the old per-service `"✅ تم إنشاء BackupRestoreService"` / `"✅ تم إنشاء GoogleDriveService..."` etc. log lines (that lived inside each old lazy-getter) are gone from `application.log`, replaced by the single `"✅ تم تهيئة ServiceContainer بنجاح"` line. This is expected — it is not a bug.

- [ ] **Step 2: Rewrite `App.xaml.cs`**

Replace the entire file content with:

```csharp
using System.Configuration;
using System.Data;
using System.Windows;
using BackupDbServer.Core.Infrastructure;
using BackupDbServer.Helpers;
using BackupDbServer.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BackupDbServer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private HealthMonitor? _healthMonitor;
        private IHost? _host;

        protected override void OnStartup(StartupEventArgs e)
        {
            // بناء حاوية DI (Generic Host) وتسجيل كل الخدمات
            _host = new HostBuilder()
                .ConfigureServices(services => services.AddApplicationServices())
                .Build();
            _host.Start();

            // تهيئة ServiceContainer - نقطة توافق تُحلّ الخدمات من حاوية DI
            ServiceContainer.Initialize(_host.Services);

            // الحصول على Logger من ServiceContainer
            var logger = ServiceContainer.Logger;

            // تفعيل نظام الحماية من الأخطاء
            CrashProtectionManager.Initialize(logger);

            // تفعيل نظام مراقبة الصحة
            _healthMonitor = new HealthMonitor(logger);

            // التحقق من إغلاق مفاجئ سابق
            if (CrashProtectionManager.CheckForPreviousCrash())
            {
                logger.LogWarning("تم اكتشاف إغلاق مفاجئ في الجلسة السابقة");
                System.Windows.MessageBox.Show("تم اكتشاف إغلاق مفاجئ في الجلسة السابقة.\nتم حفظ تفاصيل الحالة في ملفات السجل.",
                              "استرداد من إغلاق مفاجئ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }

            // التحقق من ملف صحة سابق
            if (_healthMonitor.CheckForPreviousHealthFile())
            {
                logger.LogInformation("تم العثور على ملف مراقبة صحة من جلسة سابقة");
            }

            // التحقق من أن هذه هي النسخة الوحيدة المشغلة
            if (!SingleInstanceManager.IsFirstInstance())
            {
                // إذا كان هناك نسخة أخرى تعمل، إنهاء هذه النسخة
                Current.Shutdown();
                return;
            }

            // بوابة كلمة السر عند بدء التشغيل: إذا كانت الحماية مفعّلة، اطلبها فوراً قبل ظهور النافذة
            var appLock = ServiceContainer.AppLock;
            if (appLock.IsEnabled)
            {
                appLock.Lock();

                // منع الإغلاق التلقائي عند إغلاق نافذة الحوار قبل إنشاء النافذة الرئيسية
                var previousShutdownMode = ShutdownMode;
                ShutdownMode = ShutdownMode.OnExplicitShutdown;

                bool unlocked;
                try
                {
                    unlocked = appLock.TryUnlock();
                }
                catch (Exception ex)
                {
                    logger.LogError("خطأ أثناء طلب كلمة السر عند بدء التشغيل", ex);
                    Current.Shutdown();
                    return;
                }

                ShutdownMode = previousShutdownMode;

                if (!unlocked)
                {
                    // فشل/إلغاء إدخال كلمة السر — إنهاء التطبيق
                    logger.LogWarning("تم إلغاء فتح البرنامج: لم تُدخَل كلمة السر الصحيحة عند بدء التشغيل");
                    Current.Shutdown();
                    return;
                }
            }

            logger.LogInformation("بدء تشغيل التطبيق مع نظام الحماية المفعل وServiceContainer");

            // إنشاء وعرض النافذة الرئيسية عبر حاوية DI بدل StartupUri
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                var logger = ServiceContainer.Logger;
                logger.LogInformation($"إغلاق التطبيق بدء - كود الخروج: {e.ApplicationExitCode}");

                // تنظيف نظام مراقبة الصحة
                _healthMonitor?.Dispose();

                // تنظيف نظام الحماية
                CrashProtectionManager.Cleanup();

                // تنظيف موارد SingleInstanceManager
                SingleInstanceManager.Cleanup();

                // إيقاف جميع الخدمات عبر ServiceContainer
                ServiceContainer.Shutdown();

                logger.LogInformation("تم إغلاق التطبيق بنجاح");
            }
            catch (Exception ex)
            {
                // حفظ آخر خطأ في ملف طوارئ
                try
                {
                    var errorFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "final_error.log");
                    System.IO.File.WriteAllText(errorFile, $"[{DateTime.Now}] خطأ في OnExit: {ex}");
                }
                catch { /* لا يمكن فعل المزيد */ }
            }
            finally
            {
                _host?.Dispose();
                base.OnExit(e);
            }
        }
    }
}
```

Two deliberate additions beyond a straight port (call these out in the commit message, not hidden): (1) `_host?.Dispose()` in `OnExit`'s `finally` — the host was never disposed before because it didn't exist; disposing it releases the `IServiceProvider` cleanly. (2) `MainWindow` is now resolved and shown explicitly instead of via `StartupUri` — same window, same order relative to the crash/instance/applock checks, just constructed by the container instead of by the WPF XAML loader.

- [ ] **Step 3: Remove `StartupUri` from `App.xaml`**

In `App.xaml`, change:
```xaml
<Application x:Class="BackupDbServer.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:BackupDbServer"
             xmlns:converters="clr-namespace:BackupDbServer.Converters"
             StartupUri="MainWindow.xaml">
```
to:
```xaml
<Application x:Class="BackupDbServer.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:BackupDbServer"
             xmlns:converters="clr-namespace:BackupDbServer.Converters">
```

- [ ] **Step 4: Remove the `Window.DataContext` block from `MainWindow.xaml`**

In `MainWindow.xaml`, delete lines 45–48:
```xaml
    <Window.DataContext>
        <local:MainViewModel/>
    </Window.DataContext>

```
(The `xmlns:local="clr-namespace:BackupDbServer.ViewModels"` declaration on line 4 can stay — an unused xmlns mapping is harmless in XAML.)

- [ ] **Step 5: Give `MainWindow` a `MainViewModel` constructor parameter**

In `MainWindow.xaml.cs`, change the usings block (lines 1–6) from:
```csharp
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using BackupDbServer.Helpers;
using System.ComponentModel;
```
to:
```csharp
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using BackupDbServer.Helpers;
using BackupDbServer.ViewModels;
using System.ComponentModel;
```

Then change the constructor (lines 19–37) from:
```csharp
        public MainWindow()
        {
            try
            {
                InitializeComponent();
                _logger = new FileLogger();
                InitializeTrayIcon();
                _logger.LogInformation("تم تهيئة النافذة الرئيسية بنجاح");
            }
```
to:
```csharp
        public MainWindow(MainViewModel viewModel)
        {
            try
            {
                InitializeComponent();
                DataContext = viewModel;
                _logger = new FileLogger();
                InitializeTrayIcon();
                _logger.LogInformation("تم تهيئة النافذة الرئيسية بنجاح");
            }
```
Everything else in `MainWindow.xaml.cs` (tray icon setup, idle timer, `MainWindow_Closing`, `OnClosed`, etc.) is unchanged.

- [ ] **Step 6: Build**

Close any running instance of the app first (an elevated running copy locks the `.exe` and blocks the build — see the caveat in Task 1, Step 3).

Run: `dotnet build BackupDbServer.csproj -nologo -clp:ErrorsOnly`
Expected: `0 Errors`.

- [ ] **Step 7: Launch and smoke-test manually**

Run: `bin/Debug/net8.0-windows10.0.17763.0/BackupDbServer.exe` (approve the UAC prompt — `app.manifest` still requires admin, unchanged).

Verify:
- The main window opens and looks identical to before.
- `%APPDATA%\BackupDbServer\application.log` shows `"بدء تشغيل التطبيق مع نظام الحماية المفعل وServiceContainer"` and no `[ERROR]` lines.
- No exception dialog appears on startup.

- [ ] **Step 8: Commit**

```bash
git add Helpers/ServiceContainer.cs App.xaml.cs App.xaml MainWindow.xaml MainWindow.xaml.cs
git commit -m "refactor(P1): boot the app through a Generic Host; ServiceContainer becomes a DI-backed shim

ServiceContainer's static ServiceContainer.X properties are unchanged from the
caller's point of view (~38 call sites across 13 files untouched) - only the
internals moved from hand-rolled double-checked-locking singletons to
Microsoft.Extensions.DependencyInjection resolution. MainWindow/MainViewModel
are now resolved from the container instead of WPF's StartupUri/XAML DataContext.
No functional or UI change."
```

---

## Task 3: Manual regression pass

**Files:** none (verification only).

There is no automated test suite in this project (see `CLAUDE.md` → "Testing"), so this refactor's safety net is a manual pass over every surface that touches a service now resolved through the new DI shim.

- [ ] **Step 1: Exercise every ServiceContainer-backed feature**

With the app running (from Task 2, Step 7), walk through each of these and confirm no error dialog and no `[ERROR]` in `application.log` after each:

1. **Settings → Test Connection** (exercises `BackupService` indirectly via connection settings).
2. **Run a manual backup** from the main window (exercises `BackupService`).
3. **Open "الإعدادات" → enable/disable Auto Backup toggle** (exercises `AutoBackupService`, and — the first time it's toggled on — `GoogleDriveService`/`IGoogleDriveService`).
4. **Open "النسخ المجدول" (Scheduled Backup) window** (exercises `ScheduledBackupService`).
5. **Open "إدارة المهام اليومية"** and create/edit a task, including the interval mode (`ScheduleMode.Interval`) added in the prior session (exercises `DailyTaskService`).
6. **Right-click a database row → enable/disable maintenance** (exercises `XmlMaintenanceService` — the first genuinely lazy resolution in the new shim).
7. **Settings → enable the password lock, then lock/unlock via the tray icon** (exercises `AppLockManager`).
8. **Minimize to tray, then restore from the tray icon** (exercises `SingleInstanceManager`'s `Application.Current.MainWindow` access — confirms `MainWindow` was assigned correctly in `App.xaml.cs`).
9. **Close the app via the taskbar/tray "إنهاء البرنامج" and confirm a clean exit** — check `application.log` for `"🛑 بدء إيقاف ServiceContainer..."` through `"تم إغلاق التطبيق بنجاح"` with no `[ERROR]` in between.

- [ ] **Step 2: Confirm the expected log-shape change**

Open `application.log` from this session and confirm:
- ✅ Present: a single `"✅ تم تهيئة ServiceContainer بنجاح"` line near startup.
- ✅ Expected absence (not a bug — see Task 2 note): the old per-service `"✅ تم إنشاء ..."` lines are gone.

- [ ] **Step 3: Rebuild the client release and confirm it still succeeds**

Run the same command from `DEPLOYMENT.md`:
```powershell
dotnet publish BackupDbServer.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:Version=1.0.0 -p:AssemblyVersion=1.0.0.0 -p:FileVersion=1.0.0.0 `
  -p:DebugType=none -p:DebugSymbols=false `
  -o "publish/ClientRelease-x64"
```
Expected: `0 Errors`, single-file `BackupDbServer.exe` produced — confirming the DI/single-file combination (host + reflection-free `Microsoft.Extensions.DependencyInjection`) doesn't break single-file trimming/packaging.

No commit for this task (verification only, nothing to check in).

---

## Definition of Done for P1

- [ ] Tasks 1–2 committed; Task 3 verified with no `[ERROR]` entries.
- [ ] `Microsoft.Extensions.DependencyInjection` / `Microsoft.Extensions.Hosting` are now genuinely used (P0 finding from `REFACTOR_PRD.md` §3 resolved).
- [ ] `ServiceContainer` no longer hand-constructs singletons — matches PRD §5.1 exactly.
- [ ] Zero behavior change confirmed via the Task 3 manual pass.
- [ ] Ready to write the P2 plan (de-duplication + `Core`/`Features` folder move) as its own document.

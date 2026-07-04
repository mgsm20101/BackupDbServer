using System;
using BackupDbServer.Helpers;
using BackupDbServer.Services;
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

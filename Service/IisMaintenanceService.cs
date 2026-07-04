using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BackupDbServer.Helpers;
using BackupDbServer.Models;
using Microsoft.Web.Administration;

namespace BackupDbServer.Services
{
    /// <summary>
    /// خدمة صيانة وتسريع IIS:
    /// - تعداد Application Pools وإعداداتها الحالية.
    /// - تحسين الأداء (AlwaysRunning، إيقاف الخمول وإعادة التدوير الدورية، Preload، طول الطابور).
    /// - إعادة تدوير Pool، وإعادة تشغيل كامل لـ IIS (iisreset).
    /// تتطلب صلاحيات Administrator.
    /// </summary>
    public class IisMaintenanceService
    {
        // طول الطابور المستهدف عند التحسين
        private const long TargetQueueLength = 5000;

        private readonly ILogger _logger;

        public IisMaintenanceService(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>تعداد كل الـ Application Pools مع حالة التحسين الحالية.</summary>
        public List<AppPoolInfo> ListApplicationPools()
        {
            using var server = new ServerManager();
            return server.ApplicationPools.Select(MapPool).ToList();
        }

        /// <summary>تطبيق إعدادات التسريع على Pool محدد.</summary>
        public Task OptimizeApplicationPoolAsync(string poolName)
        {
            return Task.Run(() =>
            {
                using var server = new ServerManager();
                var pool = server.ApplicationPools.FirstOrDefault(
                    p => p.Name.Equals(poolName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"لم يتم العثور على Application Pool باسم: {poolName}");

                ApplyOptimization(pool);

                // تفعيل Preload + التشغيل التلقائي للتطبيقات المرتبطة بهذا الـ Pool
                foreach (var site in server.Sites)
                {
                    site.ServerAutoStart = true;
                    foreach (var app in site.Applications.Where(
                        a => a.ApplicationPoolName.Equals(poolName, StringComparison.OrdinalIgnoreCase)))
                    {
                        app["preloadEnabled"] = true;
                    }
                }

                server.CommitChanges();
                _logger.LogInformation($"[IIS] تم تحسين Application Pool: {poolName}");
            });
        }

        /// <summary>تحسين جميع الـ Application Pools.</summary>
        public Task OptimizeAllAsync()
        {
            return Task.Run(() =>
            {
                using var server = new ServerManager();
                foreach (var pool in server.ApplicationPools)
                {
                    ApplyOptimization(pool);
                }
                foreach (var site in server.Sites)
                {
                    site.ServerAutoStart = true;
                    foreach (var app in site.Applications)
                    {
                        app["preloadEnabled"] = true;
                    }
                }
                server.CommitChanges();
                _logger.LogInformation("[IIS] تم تحسين جميع الـ Application Pools.");
            });
        }

        /// <summary>إعادة تدوير Application Pool محدد.</summary>
        public Task RecycleAppPoolAsync(string poolName)
        {
            return Task.Run(() =>
            {
                using var server = new ServerManager();
                var pool = server.ApplicationPools.FirstOrDefault(
                    p => p.Name.Equals(poolName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"لم يتم العثور على Application Pool باسم: {poolName}");

                if (pool.State == ObjectState.Started)
                {
                    pool.Recycle();
                }
                else
                {
                    pool.Start();
                }
                _logger.LogInformation($"[IIS] تم إعادة تدوير Application Pool: {poolName}");
            });
        }

        /// <summary>إعادة تشغيل كامل لـ IIS عبر iisreset، وإرجاع المخرجات.</summary>
        public async Task<string> RestartIisAsync()
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "iisreset.exe",
                Arguments = "/restart",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo)
                ?? throw new InvalidOperationException("تعذّر تشغيل iisreset.");

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var combined = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(output)) combined.AppendLine(output.Trim());
            if (!string.IsNullOrWhiteSpace(error)) combined.AppendLine(error.Trim());

            if (process.ExitCode != 0)
            {
                _logger.LogWarning($"[IIS] iisreset انتهى برمز {process.ExitCode}: {combined}");
                throw new InvalidOperationException(
                    $"فشل iisreset (رمز {process.ExitCode}):\n{combined}");
            }

            _logger.LogInformation("[IIS] تم إعادة تشغيل IIS بنجاح (iisreset /restart).");
            return combined.ToString().Trim();
        }

        // ===== أدوات داخلية =====

        /// <summary>تطبيق إعدادات التسريع على كائن Pool (دون CommitChanges).</summary>
        private static void ApplyOptimization(ApplicationPool pool)
        {
            pool.AutoStart = true;
            pool.StartMode = StartMode.AlwaysRunning;
            pool.ManagedPipelineMode = ManagedPipelineMode.Integrated;

            // إيقاف إغلاق الخمول
            pool.ProcessModel.IdleTimeout = TimeSpan.Zero;

            // إيقاف إعادة التدوير الدورية (وقت/طلبات/ذاكرة)
            pool.Recycling.PeriodicRestart.Time = TimeSpan.Zero;
            pool.Recycling.PeriodicRestart.Requests = 0;
            pool.Recycling.PeriodicRestart.PrivateMemory = 0;
            pool.Recycling.PeriodicRestart.Memory = 0;

            // رفع طول الطابور
            pool.QueueLength = TargetQueueLength;
        }

        private static AppPoolInfo MapPool(ApplicationPool pool)
        {
            var idle = pool.ProcessModel.IdleTimeout.TotalMinutes;
            var periodic = pool.Recycling.PeriodicRestart.Time.TotalMinutes;

            var isOptimized =
                pool.AutoStart &&
                pool.StartMode == StartMode.AlwaysRunning &&
                idle == 0 &&
                periodic == 0 &&
                pool.Recycling.PeriodicRestart.Requests == 0 &&
                pool.QueueLength >= TargetQueueLength;

            return new AppPoolInfo
            {
                Name = pool.Name,
                State = pool.State.ToString(),
                StartMode = pool.StartMode.ToString(),
                PipelineMode = pool.ManagedPipelineMode.ToString(),
                ManagedRuntimeVersion = pool.ManagedRuntimeVersion ?? string.Empty,
                IdleTimeoutMinutes = idle,
                PeriodicRestartMinutes = periodic,
                QueueLength = pool.QueueLength,
                IsOptimized = isOptimized
            };
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace BackupDbServer.Services
{
    public class WindowsServiceManager
    {
        private const string SERVICE_NAME = "BackupDbServer";
        private const string SERVICE_DISPLAY_NAME = "BackupDbServer - خدمة النسخ الاحتياطي التلقائي";
        private const string SERVICE_DESCRIPTION = "خدمة Windows للنسخ الاحتياطي التلقائي لقواعد البيانات";

        public bool IsServiceInstalled()
        {
            try
            {
                using var service = new ServiceController(SERVICE_NAME);
                var status = service.Status; // سيرمي exception إذا لم تكن مثبتة
                return true;
            }
            catch
            {
                return false;
            }
        }

        public ServiceControllerStatus GetServiceStatus()
        {
            try
            {
                using var service = new ServiceController(SERVICE_NAME);
                service.Refresh();
                return service.Status;
            }
            catch
            {
                return ServiceControllerStatus.Stopped;
            }
        }

        public async Task<bool> InstallServiceAsync()
        {
            try
            {
                if (IsServiceInstalled())
                {
                    return true; // مثبتة بالفعل
                }

                var servicePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BackupDbServer.exe");
                
                var createArgs = $"create \"{SERVICE_NAME}\" binPath= \"\\\"{servicePath}\\\" --service\" " +
                               $"DisplayName= \"{SERVICE_DISPLAY_NAME}\" " +
                               $"start= auto";

                var createResult = await RunScCommandAsync(createArgs);
                
                if (createResult)
                {
                    // إضافة وصف للخدمة
                    var descArgs = $"description \"{SERVICE_NAME}\" \"{SERVICE_DESCRIPTION}\"";
                    await RunScCommandAsync(descArgs);
                }

                return createResult;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تثبيت الخدمة: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UninstallServiceAsync()
        {
            try
            {
                if (!IsServiceInstalled())
                {
                    return true; // غير مثبتة أصلاً
                }

                // إيقاف الخدمة أولاً
                await StopServiceAsync();
                await Task.Delay(2000);

                var deleteArgs = $"delete \"{SERVICE_NAME}\"";
                return await RunScCommandAsync(deleteArgs);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في إلغاء تثبيت الخدمة: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> StartServiceAsync()
        {
            try
            {
                if (!IsServiceInstalled())
                {
                    return false;
                }

                using var service = new ServiceController(SERVICE_NAME);
                service.Refresh();

                if (service.Status == ServiceControllerStatus.Running)
                {
                    return true; // تعمل بالفعل
                }

                if (service.Status == ServiceControllerStatus.Stopped)
                {
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    return service.Status == ServiceControllerStatus.Running;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تشغيل الخدمة: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> StopServiceAsync()
        {
            try
            {
                if (!IsServiceInstalled())
                {
                    return true;
                }

                using var service = new ServiceController(SERVICE_NAME);
                service.Refresh();

                if (service.Status == ServiceControllerStatus.Stopped)
                {
                    return true; // متوقفة بالفعل
                }

                if (service.Status == ServiceControllerStatus.Running)
                {
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    return service.Status == ServiceControllerStatus.Stopped;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في إيقاف الخدمة: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RestartServiceAsync()
        {
            var stopResult = await StopServiceAsync();
            if (!stopResult)
            {
                return false;
            }

            await Task.Delay(3000); // انتظار للتأكد من الإيقاف
            return await StartServiceAsync();
        }

        public string GetServiceStatusText()
        {
            if (!IsServiceInstalled())
            {
                return "غير مثبتة";
            }

            var status = GetServiceStatus();
            return status switch
            {
                ServiceControllerStatus.Running => "تعمل",
                ServiceControllerStatus.Stopped => "متوقفة",
                ServiceControllerStatus.StartPending => "جاري التشغيل",
                ServiceControllerStatus.StopPending => "جاري الإيقاف",
                ServiceControllerStatus.Paused => "متوقفة مؤقتاً",
                ServiceControllerStatus.PausePending => "جاري التوقف المؤقت",
                ServiceControllerStatus.ContinuePending => "جاري الاستكمال",
                _ => "حالة غير معروفة"
            };
        }

        private async Task<bool> RunScCommandAsync(string arguments)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = "sc.exe";
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.Verb = "runas"; // تشغيل كمدير
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

                process.Start();
                await process.WaitForExitAsync();

                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في تنفيذ أمر SC: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SetServiceStartupTypeAsync(string startupType = "auto")
        {
            try
            {
                if (!IsServiceInstalled())
                {
                    return false;
                }

                var configArgs = $"config \"{SERVICE_NAME}\" start= {startupType}";
                return await RunScCommandAsync(configArgs);
            }
            catch
            {
                return false;
            }
        }
    }
}
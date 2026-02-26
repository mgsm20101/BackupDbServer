using BackupDbServer.Services;
using BackupDbServer.Views;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace BackupDbServer
{
    public class Program
    {
        [STAThread]
        public static async Task Main(string[] args)
        {
            // التحقق من وسائط سطر الأوامر
            if (args.Contains("--service"))
            {
                // تشغيل كخدمة Windows
                await RunAsServiceAsync();
            }
            else
            {
                // تشغيل كتطبيق عادي
                RunAsApplication();
            }
        }

        private static async Task RunAsServiceAsync()
        {
            try
            {
                // التحقق من تشغيل التطبيق بصلاحيات إدارية
                if (!IsRunningAsAdministrator())
                {
                    var message = "تحذير: التطبيق لا يعمل بصلاحيات إدارية. قد تواجه مشاكل في الوصول لبعض الموارد.";
                    Console.WriteLine(message);
                    System.Diagnostics.EventLog.WriteEntry("BackupDbServer", message, 
                        System.Diagnostics.EventLogEntryType.Warning);
                }

                Console.WriteLine("بدء تشغيل خدمة النسخ الاحتياطي...");
                
                using var serviceHost = new BackupWindowsServiceHost();
                await serviceHost.StartAsync();

                Console.WriteLine("تم تشغيل الخدمة بنجاح. اضغط Ctrl+C للإيقاف.");

                // انتظار إشارة الإيقاف
                var exitEvent = new System.Threading.ManualResetEventSlim(false);
                
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    Console.WriteLine("تم تلقي إشارة الإيقاف...");
                    exitEvent.Set();
                };

                exitEvent.Wait();

                Console.WriteLine("جاري إيقاف الخدمة...");
                await serviceHost.StopAsync();
                Console.WriteLine("تم إيقاف الخدمة بنجاح.");
            }
            catch (UnauthorizedAccessException ex)
            {
                var message = $"خطأ في الصلاحيات: {ex.Message}. تحقق من تشغيل التطبيق بصلاحيات إدارية.";
                Console.WriteLine(message);
                System.Diagnostics.EventLog.WriteEntry("BackupDbServer", message, 
                    System.Diagnostics.EventLogEntryType.Error);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                var message = $"خطأ في النظام: {ex.Message}. كود الخطأ: {ex.NativeErrorCode}";
                Console.WriteLine(message);
                System.Diagnostics.EventLog.WriteEntry("BackupDbServer", message, 
                    System.Diagnostics.EventLogEntryType.Error);
            }
            catch (Exception ex)
            {
                var message = $"خطأ في خدمة النسخ الاحتياطي: {ex.Message}";
                Console.WriteLine(message);
                System.Diagnostics.EventLog.WriteEntry("BackupDbServer", message, 
                    System.Diagnostics.EventLogEntryType.Error);
            }
        }

        /// <summary>
        /// التحقق من تشغيل التطبيق بصلاحيات إدارية
        /// </summary>
        private static bool IsRunningAsAdministrator()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static void RunAsApplication()
        {
            var app = new App();
            app.Run();
        }
    }
}